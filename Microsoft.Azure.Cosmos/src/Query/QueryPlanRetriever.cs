﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal static class QueryPlanRetriever
    {
        private static readonly QueryFeatures SupportedQueryFeatures =
            QueryFeatures.Aggregate
            | QueryFeatures.Distinct
            | QueryFeatures.MultipleOrderBy
            | QueryFeatures.MultipleAggregates
            | QueryFeatures.OffsetAndLimit
            | QueryFeatures.OrderBy
            | QueryFeatures.Top
            | QueryFeatures.NonValueAggregate;

        private static readonly string SupportedQueryFeaturesString = SupportedQueryFeatures.ToString();

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken)
        {
            QueryPlanHandler queryPlanHandler = new QueryPlanHandler(queryClient);

            return queryPlanHandler.GetQueryPlanAsync(
                    sqlQuerySpec,
                    partitionKeyDefinition,
                    SupportedQueryFeatures,
                    hasLogicalPartitionKey,
                    cancellationToken);
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return client.ExecuteQueryPlanRequestAsync(
                resourceLink,
                ResourceType.Document,
                OperationType.QueryPlan,
                sqlQuerySpec,
                QueryPlanRequestEnricher,
                cancellationToken);
        }

        private static void QueryPlanRequestEnricher(
            RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQueryPlanRequest, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.SupportedQueryFeatures, SupportedQueryFeaturesString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.QueryVersion, new Version(major: 1, minor: 0).ToString());
            requestMessage.UseGatewayMode = true;
        }
    }
}