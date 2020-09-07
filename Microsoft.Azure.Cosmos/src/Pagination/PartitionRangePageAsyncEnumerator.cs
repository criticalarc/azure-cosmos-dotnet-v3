﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Has the ability to page through a partition range.
    /// </summary>
    internal abstract class PartitionRangePageAsyncEnumerator<TPage, TState> : IAsyncEnumerator<TryCatch<TPage>>
        where TPage : Page<TState>
        where TState : State
    {
        private bool hasStarted;

        protected PartitionRangePageAsyncEnumerator(PartitionKeyRange range, TState state = default)
        {
            this.Range = range;
            this.State = state;
        }

        public PartitionKeyRange Range { get; }

        public TryCatch<TPage> Current { get; private set; }

        public TState State { get; private set; }

        private bool HasMoreResults => !this.hasStarted || (this.State != default);

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!this.HasMoreResults)
            {
                return false;
            }

            this.hasStarted = true;

            this.Current = await this.GetNextPageAsync(cancellationToken: default);
            if (this.Current.Succeeded)
            {
                this.State = this.Current.Result.State;
            }

            return true;
        }

        protected abstract Task<TryCatch<TPage>> GetNextPageAsync(CancellationToken cancellationToken);

        public abstract ValueTask DisposeAsync();
    }
}
