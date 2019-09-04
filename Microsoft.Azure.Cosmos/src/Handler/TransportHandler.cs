﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    //TODO: write unit test for this handler
    internal class TransportHandler : RequestHandler
    {
        private readonly CosmosClient client;

        public TransportHandler(CosmosClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                using (new ActivityScope(Guid.NewGuid()))
                {
                    DocumentServiceResponse response = await this.ProcessMessageAsync(request, cancellationToken);
                    return response.ToCosmosResponseMessage(request);
                }
            }
            //catch DocumentClientException and exceptions that inherit it. Other exception types happen before a backend request
            catch (DocumentClientException ex)
            {
                return ex.ToCosmosResponseMessage(request);
            }
            catch (CosmosException ce)
            {
                return ce.ToCosmosResponseMessage(request);
            }
            catch (AggregateException ex)
            {
                // TODO: because the SDK underneath this path uses ContinueWith or task.Result we need to catch AggregateExceptions here
                // in order to ensure that underlying DocumentClientExceptions get propagated up correctly. Once all ContinueWith and .Result 
                // is removed this catch can be safely removed.
                ResponseMessage errorMessage = AggregateExceptionConverter(ex, request);
                if (errorMessage != null)
                {
                    return errorMessage;
                }

                throw;
            }
        }

        internal Task<DocumentServiceResponse> ProcessMessageAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

            //TODO: extrace auth into a separate handler
            string authorization = ((IAuthorizationTokenProvider)this.client.DocumentClient).GetUserAuthorizationToken(
                serviceRequest.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                request.Method.ToString(), serviceRequest.Headers, AuthorizationTokenType.PrimaryMasterKey);
            serviceRequest.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;

            IStoreModel storeProxy = this.client.DocumentClient.GetStoreProxy(serviceRequest);
            if (request.OperationType == OperationType.Upsert)
            {
                return this.ProcessUpsertAsync(storeProxy, serviceRequest, cancellationToken);
            }
            else
            {
                return storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
            }
        }

        internal static ResponseMessage AggregateExceptionConverter(AggregateException aggregateException, RequestMessage request)
        {
            AggregateException innerExceptions = aggregateException.Flatten();
            DocumentClientException docClientException = (DocumentClientException)innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException);
            if (docClientException != null)
            {
                return docClientException.ToCosmosResponseMessage(request);
            }

            Exception exception = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException);
            CosmosException cosmosException = exception as CosmosException;
            if (cosmosException != null)
            {
                return cosmosException.ToCosmosResponseMessage(request);
            }

            return null;
        }

        private async Task<DocumentServiceResponse> ProcessUpsertAsync(IStoreModel storeProxy, DocumentServiceRequest serviceRequest, CancellationToken cancellationToken)
        {
            DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
            this.client.DocumentClient.CaptureSessionToken(serviceRequest, response);
            return response;
        }
    }
}
