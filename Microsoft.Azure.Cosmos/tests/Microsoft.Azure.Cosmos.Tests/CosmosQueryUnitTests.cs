﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryUnitTests
    {
        [TestMethod]
        public void VerifyNegativeCosmosQueryResponseStream()
        {
            string contianerRid = "mockContainerRid";
            string errorMessage = "TestErrorMessage";
            string activityId = "TestActivityId";
            double requestCharge = 42.42;

            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create();
            QueryResponse queryResponse = QueryResponse.CreateFailure(
                        statusCode: HttpStatusCode.NotFound,
                        errorMessage: errorMessage,
                        requestMessage: null,
                        error: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            null,
                            null,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = requestCharge,
                            ActivityId = activityId
                        },
                        diagnostics: diagnostics);

            Assert.AreEqual(HttpStatusCode.NotFound, queryResponse.StatusCode);
            Assert.AreEqual(errorMessage, queryResponse.ErrorMessage);
            Assert.AreEqual(requestCharge, queryResponse.Headers.RequestCharge);
            Assert.AreEqual(activityId, queryResponse.Headers.ActivityId);
            Assert.AreEqual(diagnostics, queryResponse.Diagnostics);
            Assert.IsNull(queryResponse.Content);
        }

        [TestMethod]
        public void VerifyCosmosQueryResponseStream()
        {
            string contianerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: contianerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = response;

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: CosmosDiagnosticsContext.Create());

            using (Stream stream = queryResponse.Content)
            {
                using (Stream innerStream = queryResponse.Content)
                {
                    Assert.IsTrue(object.ReferenceEquals(stream, innerStream), "Content should return the same stream");
                }
            }
        }

        [TestMethod]
        public void VerifyItemQueryResponseResult()
        {
            string contianerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) factoryResponse = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: contianerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = factoryResponse.response;
            List<CosmosElement> cosmosElements = new List<CosmosElement>(responseCore.CosmosElements);

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: cosmosElements,
                        count: cosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: CosmosDiagnosticsContext.Create());

            QueryResponse<ToDoItem> itemQueryResponse = QueryResponseMessageFactory.CreateQueryResponse<ToDoItem>(queryResponse);
            List<ToDoItem> resultItems = new List<ToDoItem>(itemQueryResponse.Resource);
            ToDoItemComparer comparer = new ToDoItemComparer();

            Assert.AreEqual(factoryResponse.items.Count, resultItems.Count);
            for (int i = 0; i < factoryResponse.items.Count; i++)
            {
                Assert.AreNotSame(factoryResponse.items[i], resultItems[i]);
                Assert.AreEqual(0, comparer.Compare(factoryResponse.items[i], resultItems[i]));
            }
        }

        [TestMethod]
        public void VerifyItemQueryResponseCosmosElements()
        {
            string containerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) factoryResponse = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: containerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = factoryResponse.response;
            List<CosmosElement> cosmosElements = new List<CosmosElement>(responseCore.CosmosElements);

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: cosmosElements,
                        count: cosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            containerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: CosmosDiagnosticsContext.Create());

            QueryResponse<CosmosElement> itemQueryResponse = QueryResponseMessageFactory.CreateQueryResponse<CosmosElement>(queryResponse);
            List<CosmosElement> resultItems = new List<CosmosElement>(itemQueryResponse.Resource);

            Assert.AreEqual(cosmosElements.Count, resultItems.Count);
            for (int i = 0; i < cosmosElements.Count; i++)
            {
                Assert.AreSame(cosmosElements[i], resultItems[i]);
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentOnFailure()
        {
            (IList<IDocumentQueryExecutionComponent> components, QueryResponseCore response) setupContext = await this.GetAllExecutionComponents();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                QueryResponseCore response = await component.DrainAsync(1, default(CancellationToken));
                Assert.AreEqual(setupContext.response, response);
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentCancellation()
        {
            (IList<IDocumentQueryExecutionComponent> components, QueryResponseCore response) setupContext = await this.GetAllExecutionComponents();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                try
                {
                    QueryResponseCore response = await component.DrainAsync(1, cancellationTokenSource.Token);
                    Assert.Fail("cancellation token should have thrown an exception");
                }
                catch (OperationCanceledException e)
                {
                    Assert.IsNotNull(e.Message);
                }
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryPartitionKeyDefinition()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                Properties = new Dictionary<string, object>()
                {
                    {"x-ms-query-partitionkey-definition", partitionKeyDefinition }
                }
            };

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(@"select * from t where t.something = 42 ");
            bool allowNonValueAggregateQuery = true;
            bool isContinuationExpected = true;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationtoken = cancellationTokenSource.Token;

            Mock<CosmosQueryClient> client = new Mock<CosmosQueryClient>();
            string exceptionMessage = "Verified that the PartitionKeyDefinition was correctly set. Cancel the rest of the query";
            client
                .Setup(x => x.GetCachedContainerQueryPropertiesAsync(It.IsAny<Uri>(), It.IsAny<Cosmos.PartitionKey?>(), cancellationtoken))
                .ReturnsAsync(new ContainerQueryProperties("mockContainer", null, partitionKeyDefinition));
            client
                .Setup(x => x.ByPassQueryParsing())
                .Returns(false);
            client
                .Setup(x => x.TryGetPartitionedQueryExecutionInfoAsync(
                    It.IsAny<SqlQuerySpec>(),
                    It.IsAny<PartitionKeyDefinition>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(
                    new InvalidOperationException(
                        exceptionMessage)));

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: null,
                maxConcurrency: queryRequestOptions?.MaxConcurrency,
                maxItemCount: queryRequestOptions?.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions?.MaxBufferedItemCount,
                partitionKey: queryRequestOptions?.PartitionKey,
                properties: queryRequestOptions?.Properties,
                partitionedQueryExecutionInfo: null,
                executionEnvironment: queryRequestOptions?.ExecutionEnvironment,
                returnResultsInDeterministicOrder: true,
                testInjections: queryRequestOptions?.TestSettings);

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContextCore(
                client: client.Object,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                resourceLink: new Uri("dbs/mockdb/colls/mockColl", UriKind.Relative),
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: new Guid("221FC86C-1825-4284-B10E-A6029652CCA6"));

            CosmosQueryExecutionContext context = CosmosQueryExecutionContextFactory.Create(
                cosmosQueryContext,
                inputParameters);

            QueryResponseCore queryResponse = await context.ExecuteNextAsync(cancellationtoken);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsTrue(queryResponse.ErrorMessage.Contains(exceptionMessage), "response error message did not contain the proper substring.");
        }

        private async Task<(IList<IDocumentQueryExecutionComponent> components, QueryResponseCore response)> GetAllExecutionComponents()
        {
            (Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> func, QueryResponseCore response) = this.SetupBaseContextToVerifyFailureScenario();

            List<IDocumentQueryExecutionComponent> components = new List<IDocumentQueryExecutionComponent>();
            List<AggregateOperator> operators = new List<AggregateOperator>()
            {
                AggregateOperator.Average,
                AggregateOperator.Count,
                AggregateOperator.Max,
                AggregateOperator.Min,
                AggregateOperator.Sum
            };

            components.Add((await AggregateDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                operators.ToArray(),
                new Dictionary<string, AggregateOperator?>()
                {
                    { "test", AggregateOperator.Count }
                },
                new List<string>() { "test" },
                false,
                null,
                func)).Result);

            components.Add((await DistinctDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                null,
                func,
                DistinctQueryType.Ordered)).Result);

            components.Add((await SkipDocumentQueryExecutionComponent.TryCreateAsync(
                5,
                null,
                func)).Result);

            components.Add((await TakeDocumentQueryExecutionComponent.TryCreateLimitDocumentQueryExecutionComponentAsync(
                5,
                null,
                func)).Result);

            components.Add((await TakeDocumentQueryExecutionComponent.TryCreateTopDocumentQueryExecutionComponentAsync(
                5,
                null,
                func)).Result);

            return (components, response);
        }

        private (Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>>, QueryResponseCore) SetupBaseContextToVerifyFailureScenario()
        { 
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create();
            diagnosticsContext.AddDiagnosticsInternal( new PointOperationStatistics(
                    Guid.NewGuid().ToString(),
                    System.Net.HttpStatusCode.Unauthorized,
                    subStatusCode: SubStatusCodes.PartitionKeyMismatch,
                    requestCharge: 4,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: new Uri("http://localhost.com"),
                    requestSessionToken: null,
                    responseSessionToken: null,
                    clientSideRequestStatistics: null));
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics = new List<QueryPageDiagnostics>()
            {
                new QueryPageDiagnostics(
                    "0",
                    "SomeQueryMetricText",
                    "SomeIndexUtilText",
                diagnosticsContext,
                new SchedulingStopwatch())
            };

            QueryResponseCore failure = QueryResponseCore.CreateFailure(
                System.Net.HttpStatusCode.Unauthorized,
                SubStatusCodes.PartitionKeyMismatch,
                "Random error message",
                42.89,
                "TestActivityId",
                diagnostics);

            Mock<IDocumentQueryExecutionComponent> baseContext = new Mock<IDocumentQueryExecutionComponent>();
            baseContext.Setup(x => x.DrainAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<QueryResponseCore>(failure));
            Task<TryCatch<IDocumentQueryExecutionComponent>> callBack(string x) => Task.FromResult<TryCatch<IDocumentQueryExecutionComponent>>(TryCatch<IDocumentQueryExecutionComponent>.FromResult(baseContext.Object));
            return (callBack, failure);
        }
    }
}