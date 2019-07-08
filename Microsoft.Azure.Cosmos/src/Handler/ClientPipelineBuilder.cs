﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class ClientPipelineBuilder
    {
        private readonly CosmosClient client;
        private readonly RequestHandler invalidPartitionExceptionRetryHandler;
        private readonly RequestHandler transportHandler;
        private readonly RequestHandler partitionKeyRangeGoneRetryHandler;
        private IReadOnlyCollection<RequestHandler> customHandlers;
        private RequestHandler retryHandler;

        public ClientPipelineBuilder(
            CosmosClient client,
            IReadOnlyCollection<RequestHandler> customHandlers)
        {
            this.client = client;
            this.transportHandler = new TransportHandler(client);
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));

            this.partitionKeyRangeGoneRetryHandler = new PartitionKeyRangeGoneRetryHandler(this.client);
            Debug.Assert(this.partitionKeyRangeGoneRetryHandler.InnerHandler == null, "The partitionKeyRangeGoneRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.invalidPartitionExceptionRetryHandler = new NamedCacheRetryHandler();
            Debug.Assert(this.invalidPartitionExceptionRetryHandler.InnerHandler == null, "The invalidPartitionExceptionRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.PartitionKeyRangeHandler = new PartitionKeyRangeHandler(client);
            Debug.Assert(this.PartitionKeyRangeHandler.InnerHandler == null, "The PartitionKeyRangeHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.UseRetryPolicy();
            this.AddCustomHandlers(customHandlers);
        }

        internal IReadOnlyCollection<RequestHandler> CustomHandlers
        {
            get => this.customHandlers;
            private set
            {
                if (value != null && value.Any(x => x?.InnerHandler != null))
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CustomHandlers));
                }

                this.customHandlers = value;
            }
        }

        internal RequestHandler PartitionKeyRangeHandler { get; set; }

        /// <summary>
        /// This is the cosmos pipeline logic for the operations. 
        /// 
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |    RequestInvokerHandler    |
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       UserHandlers          |
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       RetryHandler          |-> RetryPolicy -> ResetSessionTokenRetryPolicyFactory -> ClientRetryPolicy -> ResourceThrottleRetryPolicy
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       RouteHandler          | 
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |                             |
        ///                                    |                             |
        ///                  +-----------------------------+         +---------------------------------------+
        ///                  | !IsPartitionedFeedOperation |         |    IsPartitionedFeedOperation         |
        ///                  |      TransportHandler       |         | invalidPartitionExceptionRetryHandler |
        ///                  |                             |         |                                       |
        ///                  +-----------------------------+         +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |   partitionKeyRangeGoneRetryHandler   |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |     PartitionKeyRangeHandler          |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |         TransportHandler              |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        /// </summary>
        /// <returns>The request invoker handler used to do calls to Cosmos DB</returns>
        public RequestInvokerHandler Build()
        {
            RequestInvokerHandler root = new RequestInvokerHandler(this.client);

            RequestHandler current = root;
            if (this.CustomHandlers != null && this.CustomHandlers.Any())
            {
                foreach (RequestHandler handler in this.CustomHandlers)
                {
                    current.InnerHandler = handler;
                    current = current.InnerHandler;
                }
            }

            Debug.Assert(this.retryHandler != null, nameof(this.retryHandler));
            current.InnerHandler = this.retryHandler;
            current = current.InnerHandler;

            // Have a router handler
            RequestHandler feedHandler = this.CreateDocumentFeedPipeline();

            Debug.Assert(feedHandler != null, nameof(feedHandler));
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));
            RequestHandler routerHandler = new RouterHandler(
                documentFeedHandler: feedHandler,
                pointOperationHandler: this.transportHandler);

            current.InnerHandler = routerHandler;
            current = current.InnerHandler;

            return root;
        }

        private static RequestHandler CreatePipeline(params RequestHandler[] requestHandlers)
        {
            RequestHandler head = null;
            int handlerCount = requestHandlers.Length;
            for (int i = handlerCount - 1; i >= 0; i--)
            {
                RequestHandler indexHandler = requestHandlers[i];
                if (indexHandler.InnerHandler != null)
                {
                    throw new ArgumentOutOfRangeException($"The requestHandlers[{i}].InnerHandler is required to be null to allow the pipeline to chain the handlers.");
                }

                if (head != null)
                {
                    indexHandler.InnerHandler = head;
                }
                head = indexHandler;
            }

            return head;
        }

        private ClientPipelineBuilder UseRetryPolicy()
        {
            this.retryHandler = new RetryHandler(this.client);
            Debug.Assert(this.retryHandler.InnerHandler == null, "The retryHandler.InnerHandler must be null to allow other handlers to be linked.");
            return this;
        }

        private ClientPipelineBuilder AddCustomHandlers(IReadOnlyCollection<RequestHandler> customHandlers)
        {
            this.CustomHandlers = customHandlers;
            return this;
        }

        private RequestHandler CreateDocumentFeedPipeline()
        {
            RequestHandler[] feedPipeline = new RequestHandler[]
                {
                    this.invalidPartitionExceptionRetryHandler,
                    this.partitionKeyRangeGoneRetryHandler,
                    this.PartitionKeyRangeHandler,
                    this.transportHandler,
                };

            return ClientPipelineBuilder.CreatePipeline(feedPipeline);
        }
    }
}
