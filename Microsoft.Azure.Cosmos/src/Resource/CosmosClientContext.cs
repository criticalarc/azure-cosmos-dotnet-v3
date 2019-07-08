﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class is used to get access to different client level operations without directly referencing the client object.
    /// This makes it easy to pass a reference to the client, and it makes it easy to mock for unit tests.
    /// </summary>
    internal abstract class CosmosClientContext
    {
        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal abstract CosmosClient Client { get; }

        internal abstract DocumentClient DocumentClient { get; }

        internal abstract IDocumentQueryClient DocumentQueryClient { get; }

        internal abstract CosmosSerializer CosmosSerializer { get; }

        internal abstract CosmosSerializer PropertiesSerializer { get; }

        internal abstract CosmosResponseFactory ResponseFactory { get; }

        internal abstract RequestInvokerHandler RequestHandler { get; }

        internal abstract CosmosClientOptions ClientOptions { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal abstract Uri CreateLink(
            string parentLink,
            string uriPathSegment,
            string id);

        internal abstract void ValidateResource(string id);

        /// <summary>
        /// This is a wrapper around ExecUtil method. This allows the calls to be mocked so logic done 
        /// in a resource can be unit tested.
        /// </summary>
        internal abstract Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken);

        /// <summary>
        /// This is a wrapper around request invoker method. This allows the calls to be mocked so logic done 
        /// in a resource can be unit tested.
        /// </summary>
        internal abstract Task<T> ProcessResourceOperationAsync<T>(
           Uri resourceUri,
           ResourceType resourceType,
           OperationType operationType,
           RequestOptions requestOptions,
           ContainerCore cosmosContainerCore,
           PartitionKey? partitionKey,
           Stream streamPayload,
           Action<RequestMessage> requestEnricher,
           Func<ResponseMessage, T> responseCreator,
           CancellationToken cancellationToken);
    }
}
