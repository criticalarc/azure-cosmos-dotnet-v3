﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal sealed class QueryIterator : FeedIteratorInternal
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly CosmosQueryExecutionContext cosmosQueryExecutionContext;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;
        private readonly RequestOptions requestOptions;

        private QueryIterator(
            CosmosQueryContext cosmosQueryContext,
            CosmosQueryExecutionContext cosmosQueryExecutionContext,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions,
            RequestOptions requestOptions)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
            this.requestOptions = requestOptions;
        }

        public static QueryIterator Create(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (queryRequestOptions == null)
            {
                queryRequestOptions = new QueryRequestOptions();
            }

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContextCore(
                client: client,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: Guid.NewGuid());

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: continuationToken,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: queryRequestOptions.ExecutionEnvironment,
                returnResultsInDeterministicOrder: queryRequestOptions.ReturnResultsInDeterministicOrder,
                testInjections: queryRequestOptions.TestSettings);

            return new QueryIterator(
                cosmosQueryContext,
                CosmosQueryExecutionContextFactory.Create(cosmosQueryContext, inputParameters),
                queryRequestOptions.CosmosSerializationFormatOptions,
                queryRequestOptions);
        }

        public override bool HasMoreResults => !this.cosmosQueryExecutionContext.IsDone;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.requestOptions);
            using (diagnostics.CreateScope("QueryReadNextAsync"))
            {
                // This catches exception thrown by the pipeline and converts it to QueryResponse
                QueryResponseCore responseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
                CosmosQueryContext cosmosQueryContext = this.cosmosQueryContext;

                foreach (QueryPageDiagnostics queryPage in responseCore.Diagnostics)
                {
                    diagnostics.AddDiagnosticsInternal(queryPage);
                }

                QueryResponse queryResponse;
                if (responseCore.IsSuccess)
                {
                    queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        diagnostics: diagnostics,
                        serializationOptions: this.cosmosSerializationFormatOptions,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId,
                            SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown
                        });
                }
                else
                {
                    queryResponse = QueryResponse.CreateFailure(
                        statusCode: responseCore.StatusCode,
                        error: null,
                        errorMessage: responseCore.ErrorMessage,
                        requestMessage: null,
                        diagnostics: diagnostics,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId,
                            SubStatusCode = responseCore.SubStatusCode ?? Documents.SubStatusCodes.Unknown
                        });
                }

                return queryResponse;
            }
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            return this.cosmosQueryExecutionContext.TryGetContinuationToken(out continuationToken);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
