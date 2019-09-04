﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    internal class GatewayAddressCache : IAddressCache, IDisposable
    {
        private const string protocolFilterFormat = "{0} eq {1}";

        private const string AddressResolutionBatchSize = "AddressResolutionBatchSize";
        private const int DefaultBatchSize = 50;

        private readonly Uri serviceEndpoint;
        private readonly Uri addressEndpoint;

        private readonly AsyncCache<PartitionKeyRangeIdentity, PartitionAddressInformation> serverPartitionAddressCache;
        private readonly ConcurrentDictionary<PartitionKeyRangeIdentity, DateTime> suboptimalServerPartitionTimestamps;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly long suboptimalPartitionForceRefreshIntervalInSeconds;

        private readonly Protocol protocol;
        private readonly string protocolFilter;
        private readonly IAuthorizationTokenProvider tokenProvider;

        private HttpClient httpClient;

        private Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> masterPartitionAddressCache;
        private DateTime suboptimalMasterPartitionTimestamp;

        public GatewayAddressCache(
            Uri serviceEndpoint,
            Protocol protocol,
            IAuthorizationTokenProvider tokenProvider,
            UserAgentContainer userAgent,
            IServiceConfigurationReader serviceConfigReader,
            TimeSpan requestTimeout,
            long suboptimalPartitionForceRefreshIntervalInSeconds = 600,
            HttpMessageHandler messageHandler = null,
            ApiType apiType = ApiType.None)
        {
            this.addressEndpoint = new Uri(serviceEndpoint + "/" + Paths.AddressPathSegment);
            this.protocol = protocol;
            this.tokenProvider = tokenProvider;
            this.serviceEndpoint = serviceEndpoint;
            this.serviceConfigReader = serviceConfigReader;
            this.serverPartitionAddressCache = new AsyncCache<PartitionKeyRangeIdentity, PartitionAddressInformation>();
            this.suboptimalServerPartitionTimestamps = new ConcurrentDictionary<PartitionKeyRangeIdentity, DateTime>();
            this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;

            this.suboptimalPartitionForceRefreshIntervalInSeconds = suboptimalPartitionForceRefreshIntervalInSeconds;

            this.httpClient = messageHandler == null ? new HttpClient() : new HttpClient(messageHandler);
            if (requestTimeout != null)
            {
                this.httpClient.Timeout = requestTimeout;
            }
            this.protocolFilter =
                string.Format(CultureInfo.InvariantCulture,
                GatewayAddressCache.protocolFilterFormat,
                Constants.Properties.Protocol,
                GatewayAddressCache.ProtocolString(this.protocol));

            // Set requested API version header for version enforcement.
            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            this.httpClient.AddUserAgentHeader(userAgent);
            this.httpClient.AddApiTypeHeader(apiType);
        }

        public Uri ServiceEndpoint
        {
            get
            {
                return this.serviceEndpoint;
            }
        }

        [SuppressMessage("", "AsyncFixer02", Justification = "Multi task completed with await")]
        [SuppressMessage("", "AsyncFixer04", Justification = "Multi task completed outside of await")]
        public async Task OpenAsync(
            string databaseName,
            ContainerProperties collection,
            IReadOnlyList<PartitionKeyRangeIdentity> partitionKeyRangeIdentities,
            CancellationToken cancellationToken)
        {
            List<Task<FeedResource<Address>>> tasks = new List<Task<FeedResource<Address>>>();
            int batchSize = GatewayAddressCache.DefaultBatchSize;

#if !(NETSTANDARD15 || NETSTANDARD16)
            int userSpecifiedBatchSize = 0;
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings[GatewayAddressCache.AddressResolutionBatchSize], out userSpecifiedBatchSize))
            {
                batchSize = userSpecifiedBatchSize;
            }
#endif

            string collectionAltLink = string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseName),
                Paths.CollectionsPathSegment, Uri.EscapeUriString(collection.Id));
            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                collectionAltLink,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                for (int i = 0; i < partitionKeyRangeIdentities.Count; i += batchSize)
                {
                    tasks.Add(this.GetServerAddressesViaGatewayAsync(
                        request,
                        collection.ResourceId,
                        partitionKeyRangeIdentities.Skip(i).Take(batchSize).Select(range => range.PartitionKeyRangeId),
                        false));
                }
            }

            foreach (FeedResource<Address> response in await Task.WhenAll(tasks))
            {
                IEnumerable<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> addressInfos =
                    response.Where(addressInfo => ProtocolFromString(addressInfo.Protocol) == this.protocol)
                        .GroupBy(address => address.PartitionKeyRangeId, StringComparer.Ordinal)
                        .Select(group => this.ToPartitionAddressAndRange(collection.ResourceId, @group.ToList()));

                foreach (Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> addressInfo in addressInfos)
                {
                    this.serverPartitionAddressCache.Set(
                        new PartitionKeyRangeIdentity(collection.ResourceId, addressInfo.Item1.PartitionKeyRangeId),
                        addressInfo.Item2);
                }
            }
        }

        public async Task<PartitionAddressInformation> TryGetAddressesAsync(
            DocumentServiceRequest request,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            ServiceIdentity serviceIdentity,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (partitionKeyRangeIdentity == null)
            {
                throw new ArgumentNullException("partitionKeyRangeIdentity");
            }

            try
            {
                if (partitionKeyRangeIdentity.PartitionKeyRangeId == PartitionKeyRange.MasterPartitionKeyRangeId)
                {
                    return (await this.ResolveMasterAsync(request, forceRefreshPartitionAddresses)).Item2;
                }

                DateTime suboptimalServerPartitionTimestamp;
                if (this.suboptimalServerPartitionTimestamps.TryGetValue(partitionKeyRangeIdentity, out suboptimalServerPartitionTimestamp))
                {
                    bool forceRefreshDueToSuboptimalPartitionReplicaSet =
                        DateTime.UtcNow.Subtract(suboptimalServerPartitionTimestamp) > TimeSpan.FromSeconds(this.suboptimalPartitionForceRefreshIntervalInSeconds);

                    if (forceRefreshDueToSuboptimalPartitionReplicaSet && this.suboptimalServerPartitionTimestamps.TryUpdate(partitionKeyRangeIdentity, DateTime.MaxValue, suboptimalServerPartitionTimestamp))
                    {
                        forceRefreshPartitionAddresses = true;
                    }
                }

                PartitionAddressInformation addresses;
                if (forceRefreshPartitionAddresses || request.ForceCollectionRoutingMapRefresh)
                {
                    addresses = await this.serverPartitionAddressCache.GetAsync(
                        partitionKeyRangeIdentity,
                        null,
                        () => this.GetAddressesForRangeIdAsync(
                            request,
                            partitionKeyRangeIdentity.CollectionRid,
                            partitionKeyRangeIdentity.PartitionKeyRangeId,
                            forceRefresh: forceRefreshPartitionAddresses),
                        cancellationToken,
                        forceRefresh: true);

                    DateTime ignoreDateTime;
                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out ignoreDateTime);
                }
                else
                {
                    addresses = await this.serverPartitionAddressCache.GetAsync(
                        partitionKeyRangeIdentity,
                        null,
                        () => this.GetAddressesForRangeIdAsync(
                            request,
                            partitionKeyRangeIdentity.CollectionRid,
                            partitionKeyRangeIdentity.PartitionKeyRangeId,
                            forceRefresh: false),
                        cancellationToken);
                }

                int targetReplicaSetSize = this.serviceConfigReader.UserReplicationPolicy.MaxReplicaSetSize;
                if (addresses.AllAddresses.Count() < targetReplicaSetSize)
                {
                    this.suboptimalServerPartitionTimestamps.TryAdd(partitionKeyRangeIdentity, DateTime.UtcNow);
                }

                return addresses;
            }
            catch (DocumentClientException ex)
            {
                if ((ex.StatusCode == HttpStatusCode.NotFound) ||
                    (ex.StatusCode == HttpStatusCode.Gone && ex.GetSubStatus() == SubStatusCodes.PartitionKeyRangeGone))
                {
                    //remove from suboptimal cache in case the the collection+pKeyRangeId combo is gone.
                    DateTime ignoreDateTime;
                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out ignoreDateTime);

                    return null;
                }

                throw;
            }
            catch (Exception)
            {
                if (forceRefreshPartitionAddresses)
                {
                    DateTime ignoreDateTime;
                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out ignoreDateTime);
                }

                throw;
            }
        }

        private async Task<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> ResolveMasterAsync(DocumentServiceRequest request, bool forceRefresh)
        {
            Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> masterAddressAndRange = this.masterPartitionAddressCache;

            int targetReplicaSetSize = this.serviceConfigReader.SystemReplicationPolicy.MaxReplicaSetSize;

            forceRefresh = forceRefresh ||
                (masterAddressAndRange != null &&
                masterAddressAndRange.Item2.AllAddresses.Count() < targetReplicaSetSize &&
                DateTime.UtcNow.Subtract(this.suboptimalMasterPartitionTimestamp) > TimeSpan.FromSeconds(this.suboptimalPartitionForceRefreshIntervalInSeconds));

            if (forceRefresh || request.ForceCollectionRoutingMapRefresh || this.masterPartitionAddressCache == null)
            {
                string entryUrl = PathsHelper.GeneratePath(
                   ResourceType.Database,
                   string.Empty,
                   true);

                try
                {
                    FeedResource<Address> masterAddresses = await this.GetMasterAddressesViaGatewayAsync(
                        request,
                        ResourceType.Database,
                        null,
                        entryUrl,
                        forceRefresh,
                        false);

                    masterAddressAndRange = this.ToPartitionAddressAndRange(string.Empty, masterAddresses.ToList());
                    this.masterPartitionAddressCache = masterAddressAndRange;
                    this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;
                }
                catch (Exception)
                {
                    this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;
                    throw;
                }
            }

            if (masterAddressAndRange.Item2.AllAddresses.Count() < targetReplicaSetSize && this.suboptimalMasterPartitionTimestamp.Equals(DateTime.MaxValue))
            {
                this.suboptimalMasterPartitionTimestamp = DateTime.UtcNow;
            }

            return masterAddressAndRange;
        }

        private async Task<PartitionAddressInformation> GetAddressesForRangeIdAsync(
            DocumentServiceRequest request,
            string collectionRid,
            string partitionKeyRangeId,
            bool forceRefresh)
        {
            FeedResource<Address> response =
                await this.GetServerAddressesViaGatewayAsync(request, collectionRid, new[] { partitionKeyRangeId }, forceRefresh);

            IEnumerable<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> addressInfos =
                response.Where(addressInfo => ProtocolFromString(addressInfo.Protocol) == this.protocol)
                    .GroupBy(address => address.PartitionKeyRangeId, StringComparer.Ordinal)
                    .Select(group => this.ToPartitionAddressAndRange(collectionRid, @group.ToList()));

            Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> result =
                addressInfos.SingleOrDefault(
                    addressInfo => StringComparer.Ordinal.Equals(addressInfo.Item1.PartitionKeyRangeId, partitionKeyRangeId));

            if (result == null)
            {
                string errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    RMResources.PartitionKeyRangeNotFound,
                    partitionKeyRangeId,
                    collectionRid);

                throw new PartitionKeyRangeGoneException(errorMessage) { ResourceAddress = collectionRid };
            }

            return result.Item2;
        }

        private async Task<FeedResource<Address>> GetMasterAddressesViaGatewayAsync(
            DocumentServiceRequest request,
            ResourceType resourceType,
            string resourceAddress,
            string entryUrl,
            bool forceRefresh,
            bool useMasterCollectionResolver)
        {
            INameValueCollection addressQuery = new DictionaryNameValueCollection(StringComparer.Ordinal);
            addressQuery.Add(HttpConstants.QueryStrings.Url, HttpUtility.UrlEncode(entryUrl));

            INameValueCollection headers = new DictionaryNameValueCollection(StringComparer.Ordinal);
            if (forceRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceRefresh, bool.TrueString);
            }

            if (useMasterCollectionResolver)
            {
                headers.Set(HttpConstants.HttpHeaders.UseMasterCollectionResolver, bool.TrueString);
            }

            if (request.ForceCollectionRoutingMapRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceCollectionRoutingMapRefresh, bool.TrueString);
            }

            addressQuery.Add(HttpConstants.QueryStrings.Filter, this.protocolFilter);

            string resourceTypeToSign = PathsHelper.GetResourcePath(resourceType);

            headers.Set(HttpConstants.HttpHeaders.XDate, DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture));
            string token = this.tokenProvider.GetUserAuthorizationToken(
                resourceAddress,
                resourceTypeToSign,
                HttpConstants.HttpMethods.Get,
                headers,
                AuthorizationTokenType.PrimaryMasterKey);

            headers.Set(HttpConstants.HttpHeaders.Authorization, token);

            Uri targetEndpoint = UrlUtility.SetQuery(this.addressEndpoint, UrlUtility.CreateQuery(addressQuery));

            string identifier = GatewayAddressCache.LogAddressResolutionStart(request, targetEndpoint);
            using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(targetEndpoint, headers))
            {
                using (DocumentServiceResponse documentServiceResponse =
                        await ClientExtensions.ParseResponseAsync(httpResponseMessage))
                {
                    GatewayAddressCache.LogAddressResolutionEnd(request, identifier);
                    return documentServiceResponse.GetResource<FeedResource<Address>>();
                }
            }
        }

        private async Task<FeedResource<Address>> GetServerAddressesViaGatewayAsync(
            DocumentServiceRequest request,
            string collectionRid,
            IEnumerable<string> partitionKeyRangeIds,
            bool forceRefresh)
        {
            string entryUrl = PathsHelper.GeneratePath(ResourceType.Document, collectionRid, true);

            INameValueCollection addressQuery = new DictionaryNameValueCollection();
            addressQuery.Add(HttpConstants.QueryStrings.Url, HttpUtility.UrlEncode(entryUrl));

            INameValueCollection headers = new DictionaryNameValueCollection();
            if (forceRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceRefresh, bool.TrueString);
            }

            if (request.ForceCollectionRoutingMapRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceCollectionRoutingMapRefresh, bool.TrueString);
            }

            addressQuery.Add(HttpConstants.QueryStrings.Filter, this.protocolFilter);
            addressQuery.Add(HttpConstants.QueryStrings.PartitionKeyRangeIds, string.Join(",", partitionKeyRangeIds));

            string resourceTypeToSign = PathsHelper.GetResourcePath(ResourceType.Document);

            headers.Set(HttpConstants.HttpHeaders.XDate, DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture));
            string token = null;
            try
            {
                token = this.tokenProvider.GetUserAuthorizationToken(
                    collectionRid,
                    resourceTypeToSign,
                    HttpConstants.HttpMethods.Get,
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey);
            }
            catch (UnauthorizedException)
            {
            }

            if (token == null && request.IsNameBased)
            {
                // User doesn't have rid based resource token. Maybe he has name based.
                string collectionAltLink = PathsHelper.GetCollectionPath(request.ResourceAddress);
                token = this.tokenProvider.GetUserAuthorizationToken(
                        collectionAltLink,
                        resourceTypeToSign,
                        HttpConstants.HttpMethods.Get,
                        headers,
                        AuthorizationTokenType.PrimaryMasterKey);
            }

            headers.Set(HttpConstants.HttpHeaders.Authorization, token);

            Uri targetEndpoint = UrlUtility.SetQuery(this.addressEndpoint, UrlUtility.CreateQuery(addressQuery));

            string identifier = GatewayAddressCache.LogAddressResolutionStart(request, targetEndpoint);
            using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(targetEndpoint, headers))
            {
                using (DocumentServiceResponse documentServiceResponse =
                        await ClientExtensions.ParseResponseAsync(httpResponseMessage))
                {
                    GatewayAddressCache.LogAddressResolutionEnd(request, identifier);

                    return documentServiceResponse.GetResource<FeedResource<Address>>();
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.httpClient != null)
                {
                    try
                    {
                        this.httpClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception);
                    }

                    this.httpClient = null;
                }
            }
        }

        internal Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> ToPartitionAddressAndRange(string collectionRid, IList<Address> addresses)
        {
            Address address = addresses.First();

            AddressInformation[] addressInfos =
                addresses.Select(
                    addr =>
                    new AddressInformation
                    {
                        IsPrimary = addr.IsPrimary,
                        PhysicalUri = addr.PhysicalUri,
                        Protocol = ProtocolFromString(addr.Protocol),
                        IsPublic = true
                    }).ToArray();

            return Tuple.Create(new PartitionKeyRangeIdentity(collectionRid, address.PartitionKeyRangeId), new PartitionAddressInformation(addressInfos));
        }

        private static string LogAddressResolutionStart(DocumentServiceRequest request, Uri targetEndpoint)
        {
            string identifier = null;
            if (request.RequestContext.ClientRequestStatistics != null)
            {
                identifier = request.RequestContext.ClientRequestStatistics.RecordAddressResolutionStart(targetEndpoint);
            }

            return identifier;
        }

        private static void LogAddressResolutionEnd(DocumentServiceRequest request, string identifier)
        {
            if (request.RequestContext.ClientRequestStatistics != null)
            {
                request.RequestContext.ClientRequestStatistics.RecordAddressResolutionEnd(identifier);
            }
        }

        private static Protocol ProtocolFromString(string protocol)
        {
            switch (protocol.ToLowerInvariant())
            {
                case RuntimeConstants.Protocols.HTTPS:
                    return Protocol.Https;

                case RuntimeConstants.Protocols.RNTBD:
                    return Protocol.Tcp;

                default:
                    throw new ArgumentOutOfRangeException("protocol");
            }
        }

        private static string ProtocolString(Protocol protocol)
        {
            switch ((int)protocol)
            {
                case (int)Protocol.Https:
                    return RuntimeConstants.Protocols.HTTPS;

                case (int)Protocol.Tcp:
                    return RuntimeConstants.Protocols.RNTBD;

                default:
                    throw new ArgumentOutOfRangeException("protocol");
            }
        }
    }
}
