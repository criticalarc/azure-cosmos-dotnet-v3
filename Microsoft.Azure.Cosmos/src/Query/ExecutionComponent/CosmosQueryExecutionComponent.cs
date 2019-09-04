﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Interface for all DocumentQueryExecutionComponents
    /// </summary>
    internal abstract class CosmosQueryExecutionComponent : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this component is done draining documents.
        /// </summary>
        public abstract bool IsDone { get; }

        /// <summary>
        /// Drains documents from this component.
        /// </summary>
        /// <param name="maxElements">The maximum number of documents to drain.</param>
        /// <param name="token">The cancellation token to cancel tasks.</param>
        /// <returns>A task that when awaited on returns a feed response.</returns>
        public abstract Task<QueryResponse> DrainAsync(int maxElements, CancellationToken token);

        /// <summary>
        /// Stops this document query execution component.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Gets the QueryMetrics from this component.
        /// </summary>
        /// <returns>The QueryMetrics from this component.</returns>
        public abstract IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics();

        public abstract void Dispose();
    }
}
