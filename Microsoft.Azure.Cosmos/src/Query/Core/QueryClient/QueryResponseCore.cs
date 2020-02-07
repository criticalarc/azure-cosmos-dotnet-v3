﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using SubStatusCodes = Documents.SubStatusCodes;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    struct QueryResponseCore
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyList = new List<CosmosElement>().AsReadOnly();
        internal static readonly string EmptyGuidString = Guid.Empty.ToString();
        internal static readonly IReadOnlyCollection<QueryPageDiagnostics> EmptyDiagnostics = new List<QueryPageDiagnostics>();

        private QueryResponseCore(
            IReadOnlyList<CosmosElement> result,
            bool isSuccess,
            HttpStatusCode statusCode,
            double requestCharge,
            string activityId,
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics,
            long responseLengthBytes,
            string disallowContinuationTokenMessage,
            string continuationToken,
            string errorMessage,
            SubStatusCodes? subStatusCode)
        {
            this.IsSuccess = isSuccess;
            this.CosmosElements = result;
            this.StatusCode = statusCode;
            this.ActivityId = activityId;
            this.Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            this.ResponseLengthBytes = responseLengthBytes;
            this.RequestCharge = requestCharge;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ContinuationToken = continuationToken;
            this.ErrorMessage = errorMessage;
            this.SubStatusCode = subStatusCode;
        }

        internal IReadOnlyList<CosmosElement> CosmosElements { get; }

        internal string ErrorMessage { get; }

        internal SubStatusCodes? SubStatusCode { get; }

        internal HttpStatusCode StatusCode { get; }

        internal string DisallowContinuationTokenMessage { get; }

        internal string ContinuationToken { get; }

        internal double RequestCharge { get; }

        internal string ActivityId { get; }

        internal IReadOnlyCollection<QueryPageDiagnostics> Diagnostics { get; }

        internal long ResponseLengthBytes { get; }

        internal bool IsSuccess { get; }

        internal static QueryResponseCore CreateSuccess(
            IReadOnlyList<CosmosElement> result,
            double requestCharge,
            string activityId,
            long responseLengthBytes,
            string disallowContinuationTokenMessage,
            string continuationToken,
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics)
        {
            QueryResponseCore cosmosQueryResponse = new QueryResponseCore(
               result: result,
               isSuccess: true,
               statusCode: HttpStatusCode.OK,
               requestCharge: requestCharge,
               activityId: activityId,
               diagnostics: diagnostics,
               responseLengthBytes: responseLengthBytes,
               disallowContinuationTokenMessage: disallowContinuationTokenMessage,
               continuationToken: continuationToken,
               errorMessage: null,
               subStatusCode: null);

            return cosmosQueryResponse;
        }

        internal static QueryResponseCore CreateFailure(
            HttpStatusCode statusCode,
            SubStatusCodes? subStatusCodes,
            string errorMessage,
            double requestCharge,
            string activityId,
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics)
        {
            QueryResponseCore cosmosQueryResponse = new QueryResponseCore(
                result: QueryResponseCore.EmptyList,
                isSuccess: false,
                statusCode: statusCode,
                requestCharge: requestCharge,
                activityId: activityId,
                diagnostics: diagnostics,
                responseLengthBytes: 0,
                disallowContinuationTokenMessage: null,
                continuationToken: null,
                errorMessage: errorMessage,
                subStatusCode: subStatusCodes);

            return cosmosQueryResponse;
        }
    }
}
