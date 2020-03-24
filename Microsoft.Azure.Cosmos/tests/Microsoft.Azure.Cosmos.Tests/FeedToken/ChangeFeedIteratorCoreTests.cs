﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ChangeFeedIteratorCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Container()
        {
            new ChangeFeedIteratorCore(null, new ChangeFeedRequestOptions());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Token()
        {
            new ChangeFeedIteratorCore(Mock.Of<ContainerCore>(), null, new ChangeFeedRequestOptions());
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(-1)]
        [DataRow(0)]
        public void ChangeFeedIteratorCore_ValidateOptions(int maxItemCount)
        {
            new ChangeFeedIteratorCore(Mock.Of<ContainerCore>(), new ChangeFeedRequestOptions() { MaxItemCount = maxItemCount });
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_HasMoreResultsDefault()
        {
            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(Mock.Of<ContainerCore>(), null);
            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_FeedToken()
        {
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(Mock.Of<ContainerCore>(), feedToken, null);
            Assert.AreEqual(feedToken, changeFeedIteratorCore.FeedToken);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.AreEqual(feedToken, changeFeedIteratorCore.FeedToken);
            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OfT_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);

            bool creatorCalled = false;
            Func<ResponseMessage, FeedResponse<dynamic>> creator = (ResponseMessage r) =>
            {
                creatorCalled = true;
                return Mock.Of<FeedResponse<dynamic>>();
            };

            FeedIteratorCore<dynamic> changeFeedIteratorCoreOfT = new FeedIteratorCore<dynamic>(changeFeedIteratorCore, creator);
            FeedResponse<dynamic> response = await changeFeedIteratorCoreOfT.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.IsTrue(creatorCalled, "Response creator not called");
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_UpdatesContinuation_On304()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotModified);
            responseMessage.Headers.ETag = continuation;

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone);
            responseMessage.Headers.ETag = continuation;

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.IsFalse(changeFeedIteratorCore.HasMoreResults);

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_Retries()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .SetupSequence(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.IsAny<string>()), Times.Exactly(2));

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Mock.Get(cosmosClientContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_HandlesSplitsThroughPipeline()
        {
            int executionCount = 0;
            CosmosClientContext cosmosClientContext = GetMockedClientContext((RequestMessage requestMessage, CancellationToken cancellationToken) =>
            {
                // Force OnBeforeRequestActions call
                requestMessage.ToDocumentServiceRequest();
                if (executionCount++ == 0)
                {
                    return TestHandler.ReturnStatusCode(HttpStatusCode.Gone, Documents.SubStatusCodes.PartitionKeyRangeGone);
                }

                return TestHandler.ReturnStatusCode(HttpStatusCode.OK);
            });

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext);
            Mock.Get(containerCore)
                .Setup(c => c.LinkUri)
                .Returns(new Uri("https://dummy.documents.azure.com:443/dbs"));
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);

            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.AreEqual(1, executionCount, "PartitionKeyRangeGoneRetryHandler handled the Split");
            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.IsAny<string>()), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static CosmosClientContext GetMockedClientContext(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            CosmosClientContext clientContext = ClientContextCore.Create(
               client,
               new MockDocumentClient(),
               new CosmosClientOptions());
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = MockCosmosUtil.GetPartitionRoutingHelperMock("0");

            TestHandler testHandler = new TestHandler(handlerFunc);

            // Similar to FeedPipeline but with replaced transport
            RequestHandler[] feedPipeline = new RequestHandler[]
                {
                    new NamedCacheRetryHandler(),
                    new PartitionKeyRangeHandler(client),
                    testHandler,
                };

            RequestHandler feedHandler = ClientPipelineBuilder.CreatePipeline(feedPipeline);

            RequestHandler handler = clientContext.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = new RouterHandler(feedHandler, testHandler);
                    break;
                }

                handler = handler.InnerHandler;
            }

            return clientContext;
        }
    }
}
