﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.ComparableTask;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using PartitionKeyRange = Documents.PartitionKeyRange;
    using RequestChargeTracker = Documents.RequestChargeTracker;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// This class is responsible for maintaining a forest of <see cref="ItemProducerTree"/>.
    /// The trees in this forest are ordered using a priority queue and the nodes within the forest are internally ordered using a comparator.
    /// The ordering is determine by the concrete derived class.
    /// This class allows derived classes to iterate through the documents in the forest using Current and MoveNext semantics.
    /// This class is also responsible for prefetching documents if necessary using <see cref="ComparableTaskScheduler"/> whose ordering is also determined by the derived classes.
    /// This class also aggregated all metrics from sending queries to individual partitions.
    /// </summary>
    internal abstract class CosmosCrossPartitionQueryExecutionContext : CosmosQueryExecutionComponent, IDocumentQueryExecutionComponent
    {
        /// <summary>
        /// When a document producer tree successfully fetches a page we increase the page size by this factor so that any particular document producer will only ever make O(log(n)) roundtrips, while also only ever grabbing at most twice the number of documents needed.
        /// </summary>
        private const double DynamicPageSizeAdjustmentFactor = 1.6;

        /// <summary>
        /// Request Charge Tracker used to atomically add request charges (doubles).
        /// </summary>
        protected readonly RequestChargeTracker requestChargeTracker;

        /// <summary>
        /// Priority Queue of ItemProducerTrees that make a forest that can be iterated on.
        /// </summary>
        private readonly PriorityQueue<ItemProducerTree> itemProducerForest;

        /// <summary>
        /// Function used to determine which document producer to fetch from first
        /// </summary>
        private readonly Func<ItemProducerTree, int> fetchPrioirtyFunction;

        /// <summary>
        /// The task scheduler that kicks off all the prefetches behind the scenes.
        /// </summary>
        private readonly ComparableTaskScheduler comparableTaskScheduler;

        /// <summary>
        /// The equality comparer used to determine whether a document producer needs it's continuation token to be part of the composite continuation token.
        /// </summary>
        private readonly IEqualityComparer<CosmosElement> equalityComparer;
        /// <summary>
        /// The actual max page size after all the optimizations have been made it in the create document query execution context layer.
        /// </summary>
        private readonly long actualMaxPageSize;

        /// <summary>
        /// The actual max buffered item count after all the optimizations have been made it in the create document query execution context layer.
        /// </summary>
        private readonly long actualMaxBufferedItemCount;

        /// <summary>
        /// Injections used to reproduce special failure cases.
        /// Convert this to a mock in the future.
        /// </summary>
        private readonly TestInjections testSettings;

        private readonly CosmosQueryContext queryContext;

        private readonly bool returnResultsInDeterministicOrder;

        protected readonly CosmosQueryClient queryClient;

        /// <summary>
        /// This stores the running query metrics.
        /// When a feed response is returned he take a snapshot of this bag and store it in groupedQueryMetrics.
        /// The bag is then emptied and available to store the query metric for future continuations.
        /// </summary>
        /// <remarks>
        /// Due to the nature of parallel queries and prefetches the query metrics you get for a single continuation does not always 
        /// map to how much work was done to get that continuation.
        /// For example say for a simple cross partition query we return the first page of the results from the first partition,
        /// but behind the scenes we prefetched from other partitions.
        /// Another example is for an order by query we return one page of results but it only required us to use partial pages from each partition, 
        /// but we eventually used the whole page for the next continuation; which continuation reports the cost?
        /// Basically the only thing we can ensure is if you drain a query fully you should get back the same query metrics by the end.
        /// </remarks>
        private ConcurrentBag<QueryPageDiagnostics> diagnosticsPages;

        /// <summary>
        /// Total number of buffered items to determine if we can go for another prefetch while still honoring the MaxBufferedItemCount.
        /// </summary>
        private long totalBufferedItems;

        /// <summary>
        /// The total response length.
        /// </summary>
        private long totalResponseLengthBytes;

        /// <summary>
        /// Initializes a new instance of the CosmosCrossPartitionQueryExecutionContext class.
        /// </summary>
        /// <param name="queryContext">Constructor parameters for the base class.</param>
        /// <param name="maxConcurrency">The max concurrency</param>
        /// <param name="maxBufferedItemCount">The max buffered item count</param>
        /// <param name="maxItemCount">Max item count</param>
        /// <param name="moveNextComparer">Comparer used to figure out that document producer tree to serve documents from next.</param>
        /// <param name="fetchPrioirtyFunction">The priority function to determine which partition to fetch documents from next.</param>
        /// <param name="equalityComparer">Used to determine whether we need to return the continuation token for a partition.</param>
        /// <param name="returnResultsInDeterministicOrder">Whether or not to return results in deterministic order.</param>
        /// <param name="testSettings">Test settings.</param>
        protected CosmosCrossPartitionQueryExecutionContext(
            CosmosQueryContext queryContext,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount,
            IComparer<ItemProducerTree> moveNextComparer,
            Func<ItemProducerTree, int> fetchPrioirtyFunction,
            IEqualityComparer<CosmosElement> equalityComparer,
            bool returnResultsInDeterministicOrder,
            TestInjections testSettings)
        {
            if (moveNextComparer == null)
            {
                throw new ArgumentNullException(nameof(moveNextComparer));
            }

            this.queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            this.queryClient = queryContext.QueryClient ?? throw new ArgumentNullException(nameof(queryContext.QueryClient));
            this.itemProducerForest = new PriorityQueue<ItemProducerTree>(moveNextComparer, isSynchronized: true);
            this.fetchPrioirtyFunction = fetchPrioirtyFunction ?? throw new ArgumentNullException(nameof(fetchPrioirtyFunction));
            this.comparableTaskScheduler = new ComparableTaskScheduler(maxConcurrency.GetValueOrDefault(0));
            this.equalityComparer = equalityComparer ?? throw new ArgumentNullException(nameof(equalityComparer));
            this.testSettings = testSettings;
            this.requestChargeTracker = new RequestChargeTracker();
            this.diagnosticsPages = new ConcurrentBag<QueryPageDiagnostics>();
            this.actualMaxPageSize = maxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalMaxItemCount);

            if (this.actualMaxPageSize < 0)
            {
                throw new ArgumentOutOfRangeException("actualMaxPageSize should never be less than 0");
            }

            if (this.actualMaxPageSize > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("actualMaxPageSize should never be greater than int.MaxValue");
            }

            if (maxBufferedItemCount.HasValue)
            {
                this.actualMaxBufferedItemCount = maxBufferedItemCount.Value;
            }
            else
            {
                this.actualMaxBufferedItemCount = ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize;
            }

            if (this.actualMaxBufferedItemCount < 0)
            {
                throw new ArgumentOutOfRangeException("actualMaxBufferedItemCount should never be less than 0");
            }

            if (this.actualMaxBufferedItemCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("actualMaxBufferedItemCount should never be greater than int.MaxValue");
            }

            this.CanPrefetch = maxConcurrency.HasValue && maxConcurrency.Value != 0;

            this.returnResultsInDeterministicOrder = returnResultsInDeterministicOrder;
        }

        /// <summary>
        /// Gets a value indicating whether this context is done having documents drained.
        /// </summary>
        public override bool IsDone => !this.HasMoreResults;

        protected int ActualMaxBufferedItemCount => (int)this.actualMaxBufferedItemCount;

        protected int ActualMaxPageSize => (int)this.actualMaxPageSize;

        /// <summary>
        /// Gets the continuation token for the context.
        /// This method is overridden by the derived class, since they all have different continuation tokens.
        /// </summary>
        protected abstract string ContinuationToken
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether we are allowed to prefetch.
        /// </summary>
        private bool CanPrefetch { get; }

        /// <summary>
        /// Gets a value indicating whether the context still has more results.
        /// </summary>
        private bool HasMoreResults => (this.itemProducerForest.Count != 0) && this.CurrentItemProducerTree().HasMoreResults;

        /// <summary>
        /// Gets the number of documents we can still buffer.
        /// </summary>
        private long FreeItemSpace => this.actualMaxBufferedItemCount - Interlocked.Read(ref this.totalBufferedItems);

        /// <summary>
        /// After a split you need to maintain the continuation tokens for all the child document producers until a condition is met.
        /// For example lets say that a document producer is at continuation X and it gets split,
        /// then the children each get continuation X, but since you only drain from one of them at a time you are left with the first child having 
        /// continuation X + delta and the second child having continuation X (draw this out if you are following along).
        /// At this point you have the answer the question: "Which continuation token do you return to the user?".
        /// Let's say you return X, then when you come back to the first child you will be repeating work, thus returning some documents more than once.
        /// Let's say you return X + delta, then you fine when you return to the first child, but when you get to the second child you don't have a continuation token
        /// meaning that you will be repeating all the document for the second partition up until X and again you will be returning some documents more than once.
        /// Thus you have to return the continuation token for both children.
        /// Both this means you are returning more than 1 continuation token for the rest of the query.
        /// Well a naive optimization is to flush the continuation for a child partition once you are done draining from it, which isn't bad for a parallel query,
        /// but if you have an order by query you might not be done with a producer until the end of the query.
        /// The next optimization for a parallel query is to flush the continuation token the moment you start reading from a child partition.
        /// This works for a parallel query, but breaks for an order by query.
        /// The final realization is that for an order by query you are only choosing between multiple child partitions when their is a tie,
        /// so the key is that you can dump the continuation token the moment you come across a new order by item.
        /// For order by queries that is determined by the order by field and for parallel queries that is the moment you come by a new rid (which is any document, since rids are unique within a partition).
        /// So by passing an equality comparer to the document producers they can determine whether they are still "active".
        /// </summary>
        /// <returns>
        /// Returns all document producers whose continuation token you have to return.
        /// Only during a split will this list contain more than 1 item.
        /// </returns>
        public IEnumerable<ItemProducer> GetActiveItemProducers()
        {
            if (this.returnResultsInDeterministicOrder)
            {
                ItemProducerTree current = this.itemProducerForest.Peek().CurrentItemProducerTree;
                if (current.HasMoreResults && !current.IsActive)
                {
                    // If the current document producer tree has more results, but isn't active.
                    // then we still want to emit it, since it won't get picked up in the below for loop.
                    yield return current.Root;
                }

                foreach (ItemProducerTree itemProducerTree in this.itemProducerForest)
                {
                    foreach (ItemProducer itemProducer in itemProducerTree.GetActiveItemProducers())
                    {
                        yield return itemProducer;
                    }
                }
            }
            else
            {
                // Just return all item producers that have a continuation token
                foreach (ItemProducerTree itemProducerTree in this.itemProducerForest)
                {
                    foreach (ItemProducerTree leaf in itemProducerTree)
                    {
                        if (leaf.HasMoreResults)
                        {
                            yield return leaf.Root;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current document producer tree that should be drained from.
        /// </summary>
        /// <returns>The current document producer tree that should be drained from.</returns>
        public ItemProducerTree CurrentItemProducerTree()
        {
            return this.itemProducerForest.Peek();
        }

        /// <summary>
        /// Pushes a document producer back to the queue.
        /// </summary>
        public void PushCurrentItemProducerTree(ItemProducerTree itemProducerTree)
        {
            itemProducerTree.UpdatePriority();
            this.itemProducerForest.Enqueue(itemProducerTree);
        }

        /// <summary>
        /// Pops the current document producer tree that should be drained from.
        /// </summary>
        /// <returns>The current document producer tree that should be drained from.</returns>
        public ItemProducerTree PopCurrentItemProducerTree()
        {
            return this.itemProducerForest.Dequeue();
        }

        /// <summary>
        /// Disposes of the context and implements IDisposable.
        /// </summary>
        public override void Dispose()
        {
            this.comparableTaskScheduler.Dispose();
        }

        /// <summary>
        /// Stops the execution context.
        /// </summary>
        public override void Stop()
        {
            this.comparableTaskScheduler.Stop();
        }

        /// <summary>
        /// Initializes cross partition query execution context by initializing the necessary document producers.
        /// </summary>
        /// <param name="collectionRid">The collection to drain from.</param>
        /// <param name="partitionKeyRanges">The partitions to target.</param>
        /// <param name="initialPageSize">The page size to start the document producers off with.</param>
        /// <param name="querySpecForInit">The query specification for the rewritten query.</param>
        /// <param name="targetRangeToContinuationMap">Map from partition to it's corresponding continuation token.</param>
        /// <param name="deferFirstPage">Whether or not we should defer the fetch of the first page from each partition.</param>
        /// <param name="filter">The filter to inject in the predicate.</param>
        /// <param name="tryFilterAsync">The callback used to filter each partition.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        protected async Task<TryCatch> TryInitializeAsync(
            string collectionRid,
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            SqlQuerySpec querySpecForInit,
            IReadOnlyDictionary<string, string> targetRangeToContinuationMap,
            bool deferFirstPage,
            string filter,
            Func<ItemProducerTree, Task<TryCatch>> tryFilterAsync,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<ItemProducerTree> itemProducerTrees = new List<ItemProducerTree>();
            foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
            {
                string initialContinuationToken;
                if (targetRangeToContinuationMap != null)
                {
                    if (!targetRangeToContinuationMap.TryGetValue(partitionKeyRange.Id, out initialContinuationToken))
                    {
                        initialContinuationToken = null;
                    }
                }
                else
                {
                    initialContinuationToken = null;
                }

                ItemProducerTree itemProducerTree = new ItemProducerTree(
                    this.queryContext,
                    querySpecForInit,
                    partitionKeyRange,
                    this.OnItemProducerTreeCompleteFetching,
                    this.itemProducerForest.Comparer as IComparer<ItemProducerTree>,
                    this.equalityComparer,
                    this.testSettings,
                    deferFirstPage,
                    collectionRid,
                    initialPageSize,
                    initialContinuationToken)
                {
                    Filter = filter
                };

                // Prefetch if necessary, and populate consume queue.
                if (this.CanPrefetch)
                {
                    this.TryScheduleFetch(itemProducerTree);
                }

                itemProducerTrees.Add(itemProducerTree);
            }

            // Using loop fission so that we can load the document producers in parallel
            foreach (ItemProducerTree itemProducerTree in itemProducerTrees)
            {
                if (!deferFirstPage)
                {
                    while (true)
                    {
                        (bool movedToNextPage, QueryResponseCore? failureResponse) = await itemProducerTree.TryMoveNextPageAsync(cancellationToken);

                        if (failureResponse.HasValue)
                        {
                            return TryCatch.FromException(
                                new CosmosException(
                                    statusCode: failureResponse.Value.StatusCode,
                                    subStatusCode: (int)failureResponse.Value.SubStatusCode.GetValueOrDefault(0),
                                    message: failureResponse.Value.ErrorMessage,
                                    activityId: failureResponse.Value.ActivityId,
                                    requestCharge: failureResponse.Value.RequestCharge));
                        }

                        if (!movedToNextPage)
                        {
                            break;
                        }

                        if (itemProducerTree.IsAtBeginningOfPage)
                        {
                            break;
                        }

                        if (itemProducerTree.TryMoveNextDocumentWithinPage())
                        {
                            break;
                        }
                    }
                }

                if (tryFilterAsync != null)
                {
                    TryCatch tryFilter = await tryFilterAsync(itemProducerTree);
                    if (!tryFilter.Succeeded)
                    {
                        return tryFilter;
                    }
                }

                this.itemProducerForest.Enqueue(itemProducerTree);
            }

            return TryCatch.FromResult();
        }

        /// <summary>
        /// <para>
        /// If a query encounters split up resuming using continuation, we need to regenerate the continuation tokens. 
        /// Specifically, since after split we will have new set of ranges, we need to remove continuation token for the 
        /// parent partition and introduce continuation token for the child partitions. 
        /// </para>
        /// <para>
        /// This function does that. Also in that process, we also check validity of the input continuation tokens. For example, 
        /// even after split the boundary ranges of the child partitions should match with the parent partitions. If the Min and Max
        /// range of a target partition in the continuation token was Min1 and Max1. Then the Min and Max range info for the two 
        /// corresponding child partitions C1Min, C1Max, C2Min, and C2Max should follow the constrain below:
        ///  PMax = C2Max > C2Min > C1Max > C1Min = PMin.
        /// </para>
        /// </summary>
        /// <param name="partitionKeyRanges">The partition key ranges to extract continuation tokens for.</param>
        /// <param name="suppliedContinuationTokens">The continuation token that the user supplied.</param>
        /// <typeparam name="TContinuationToken">The type of continuation token to generate.</typeparam>
        /// <Remarks>
        /// The code assumes that merge doesn't happen and 
        /// </Remarks>
        /// <returns>The index of the partition whose MinInclusive is equal to the suppliedContinuationTokens along with the continuation tokens.</returns>
        protected static TryCatch<InitInfo<TContinuationToken>> TryFindTargetRangeAndExtractContinuationTokens<TContinuationToken>(
            List<PartitionKeyRange> partitionKeyRanges,
            IEnumerable<Tuple<TContinuationToken, Documents.Routing.Range<string>>> suppliedContinuationTokens)
        {
            if (partitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRanges));
            }

            if (partitionKeyRanges.Count < 1)
            {
                throw new ArgumentException(nameof(partitionKeyRanges));
            }

            foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
            {
                if (partitionKeyRange == null)
                {
                    throw new ArgumentException(nameof(partitionKeyRanges));
                }
            }

            if (suppliedContinuationTokens == null)
            {
                throw new ArgumentNullException(nameof(suppliedContinuationTokens));
            }

            if (suppliedContinuationTokens.Count() < 1)
            {
                throw new ArgumentException(nameof(suppliedContinuationTokens));
            }

            if (suppliedContinuationTokens.Count() > partitionKeyRanges.Count)
            {
                throw new ArgumentException($"{nameof(suppliedContinuationTokens)} can not have more elements than {nameof(partitionKeyRanges)}.");
            }

            Dictionary<string, TContinuationToken> targetRangeToContinuationTokenMap = new Dictionary<string, TContinuationToken>();

            // Find the minimum index.
            Tuple<TContinuationToken, Documents.Routing.Range<string>> firstContinuationTokenAndRange = suppliedContinuationTokens
                .OrderBy((tuple) => tuple.Item2.Min)
                .First();
            TContinuationToken firstContinuationToken = firstContinuationTokenAndRange.Item1;
            PartitionKeyRange firstContinuationRange = new PartitionKeyRange
            {
                MinInclusive = firstContinuationTokenAndRange.Item2.Min,
                MaxExclusive = firstContinuationTokenAndRange.Item2.Max
            };

            int minIndex = partitionKeyRanges.BinarySearch(
                firstContinuationRange,
                Comparer<PartitionKeyRange>.Create((range1, range2) => string.CompareOrdinal(range1.MinInclusive, range2.MinInclusive)));
            if (minIndex < 0)
            {
                return TryCatch<InitInfo<TContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"{RMResources.InvalidContinuationToken} - Could not find continuation token: {firstContinuationToken}"));
            }

            foreach (Tuple<TContinuationToken, Documents.Routing.Range<string>> suppledContinuationToken in suppliedContinuationTokens)
            {
                // find what ranges make up the supplied continuation token
                TContinuationToken continuationToken = suppledContinuationToken.Item1;
                Documents.Routing.Range<string> range = suppledContinuationToken.Item2;

                IEnumerable<PartitionKeyRange> replacementRanges = partitionKeyRanges
                    .Where((partitionKeyRange) =>
                        string.CompareOrdinal(range.Min, partitionKeyRange.MinInclusive) <= 0 &&
                        string.CompareOrdinal(range.Max, partitionKeyRange.MaxExclusive) >= 0)
                    .OrderBy((partitionKeyRange) => partitionKeyRange.MinInclusive);

                // Could not find the child ranges
                if (replacementRanges.Count() == 0)
                {
                    return TryCatch<InitInfo<TContinuationToken>>.FromException(
                        new MalformedContinuationTokenException(
                            $"{RMResources.InvalidContinuationToken} - Could not find continuation token: {continuationToken}"));
                }

                // PMax = C2Max > C2Min > C1Max > C1Min = PMin.
                string parentMax = range.Max;
                string child2Max = replacementRanges.Last().MaxExclusive;
                string child2Min = replacementRanges.Last().MinInclusive;
                string child1Max = replacementRanges.First().MaxExclusive;
                string child1Min = replacementRanges.First().MinInclusive;
                string parentMin = range.Min;

                if (!(parentMax == child2Max &&
                    string.CompareOrdinal(child2Max, child2Min) >= 0 &&
                    (replacementRanges.Count() == 1 ? true : string.CompareOrdinal(child2Min, child1Max) >= 0) &&
                    string.CompareOrdinal(child1Max, child1Min) >= 0 &&
                    child1Min == parentMin))
                {
                    return TryCatch<InitInfo<TContinuationToken>>.FromException(
                        new MalformedContinuationTokenException(
                            $"{RMResources.InvalidContinuationToken} - PMax = C2Max > C2Min > C1Max > C1Min = PMin: {continuationToken}"));
                }

                foreach (PartitionKeyRange partitionKeyRange in replacementRanges)
                {
                    targetRangeToContinuationTokenMap.Add(partitionKeyRange.Id, continuationToken);
                }
            }

            return TryCatch<InitInfo<TContinuationToken>>.FromResult(
                new InitInfo<TContinuationToken>(
                    minIndex,
                    targetRangeToContinuationTokenMap));
        }

        protected virtual long GetAndResetResponseLengthBytes()
        {
            return Interlocked.Exchange(ref this.totalResponseLengthBytes, 0);
        }

        protected virtual long IncrementResponseLengthBytes(long incrementValue)
        {
            return Interlocked.Add(ref this.totalResponseLengthBytes, incrementValue);
        }

        /// <summary>
        /// Since query metrics are being aggregated asynchronously to the feed responses as explained in the member documentation,
        /// this function allows us to take a snapshot of the query metrics.
        /// </summary>
        protected IReadOnlyCollection<QueryPageDiagnostics> GetAndResetDiagnostics()
        {
            // Safely swap the current ConcurrentBag<QueryPageDiagnostics> for a new instance. 
            ConcurrentBag<QueryPageDiagnostics> queryPageDiagnostics = Interlocked.Exchange(
                ref this.diagnosticsPages,
                new ConcurrentBag<QueryPageDiagnostics>());

            return queryPageDiagnostics;
        }

        /// <summary>
        /// Tries to schedule a fetch from the document producer tree.
        /// </summary>
        /// <param name="itemProducerTree">The document producer tree to schedule a fetch for.</param>
        /// <returns>Whether or not the fetch was successfully scheduled.</returns>
        private bool TryScheduleFetch(ItemProducerTree itemProducerTree)
        {
            return this.comparableTaskScheduler.TryQueueTask(
                new ItemProducerTreeComparableTask(
                    itemProducerTree,
                    this.fetchPrioirtyFunction),
                default);
        }

        /// <summary>
        /// Function that is given to all the document producers to call on once they are done fetching.
        /// This is so that the CosmosCrossPartitionQueryExecutionContext can aggregate metadata from them.
        /// </summary>
        /// <param name="producer">The document producer that just finished fetching.</param>
        /// <param name="itemsBuffered">The number of items that the producer just fetched.</param>
        /// <param name="resourceUnitUsage">The amount of RUs that the producer just consumed.</param>
        /// <param name="diagnostics">The query metrics that the producer just got back from the backend.</param>
        /// <param name="responseLengthBytes">The length of the response the producer just got back in bytes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// This function is by nature a bit racy.
        /// A query might be fully drained but a background task is still fetching documents so this will get called after the context is done.
        /// </remarks>
        private void OnItemProducerTreeCompleteFetching(
            ItemProducerTree producer,
            int itemsBuffered,
            double resourceUnitUsage,
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics,
            long responseLengthBytes,
            CancellationToken token)
        {
            // Update charge and states
            this.requestChargeTracker.AddCharge(resourceUnitUsage);
            Interlocked.Add(ref this.totalBufferedItems, itemsBuffered);
            this.IncrementResponseLengthBytes(responseLengthBytes);

            // Add the pages to the concurrent bag to safely merge all the list together.
            foreach (QueryPageDiagnostics diagnosticPage in diagnostics)
            {
                this.diagnosticsPages.Add(diagnosticPage);
            }

            // Adjust the producer page size so that we reach the optimal page size.
            producer.PageSize = Math.Min((long)(producer.PageSize * DynamicPageSizeAdjustmentFactor), this.actualMaxPageSize);

            // Adjust Max Degree Of Parallelism if necessary
            // (needs to wait for comparable task scheduler refactor).

            // Fetch again if necessary
            if (producer.HasMoreBackendResults)
            {
                // 4mb is the max response size
                long expectedResponseSize = Math.Min(producer.PageSize, 4 * 1024 * 1024);
                if (this.CanPrefetch && this.FreeItemSpace > expectedResponseSize)
                {
                    this.TryScheduleFetch(producer);
                }
            }
        }

        public bool TryGetContinuationToken(out string state)
        {
            state = this.ContinuationToken;
            return true;
        }

        public readonly struct InitInfo<TContinuationToken>
        {
            public InitInfo(int targetIndex, IReadOnlyDictionary<string, TContinuationToken> continuationTokens)
            {
                this.TargetIndex = targetIndex;
                this.ContinuationTokens = continuationTokens;
            }

            public int TargetIndex { get; }

            public IReadOnlyDictionary<string, TContinuationToken> ContinuationTokens { get; }
        }

        /// <summary>
        /// All CrossPartitionQueries need this information on top of the parameter for DocumentQueryExecutionContextBase.
        /// I moved it out into it's own type, so that we don't have to keep passing around all the individual parameters in the factory pattern.
        /// This also allows us to check the arguments once instead of in each of the constructors.
        /// </summary>
        public struct CrossPartitionInitParams
        {
            /// <summary>
            /// Initializes a new instance of the InitParams struct.
            /// </summary>
            /// <param name="sqlQuerySpec">The Sql query spec</param>
            /// <param name="collectionRid">The collection rid.</param>
            /// <param name="partitionedQueryExecutionInfo">The partitioned query execution info.</param>
            /// <param name="partitionKeyRanges">The partition key ranges.</param>
            /// <param name="initialPageSize">The initial page size.</param>
            /// <param name="maxConcurrency">The max concurrency</param>
            /// <param name="maxBufferedItemCount">The max buffered item count</param>
            /// <param name="maxItemCount">Max item count</param>
            /// <param name="returnResultsInDeterministicOrder">Whether or not to return results in a deterministic order.</param>
            /// <param name="testSettings">Test settings.</param>
            public CrossPartitionInitParams(
                SqlQuerySpec sqlQuerySpec,
                string collectionRid,
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
                List<PartitionKeyRange> partitionKeyRanges,
                int initialPageSize,
                int? maxConcurrency,
                int? maxItemCount,
                int? maxBufferedItemCount,
                bool returnResultsInDeterministicOrder,
                TestInjections testSettings)
            {
                if (string.IsNullOrWhiteSpace(collectionRid))
                {
                    throw new ArgumentException($"{nameof(collectionRid)} can not be null, empty, or white space.");
                }

                if (partitionKeyRanges == null)
                {
                    throw new ArgumentNullException($"{nameof(partitionKeyRanges)} can not be null.");
                }

                foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
                {
                    if (partitionKeyRange == null)
                    {
                        throw new ArgumentNullException($"{nameof(partitionKeyRange)} can not be null.");
                    }
                }

                if (initialPageSize <= 0)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(initialPageSize)} must be at least 1.");
                }

                //// Request continuation is allowed to be null
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException($"{nameof(sqlQuerySpec)} can not be null.");
                this.CollectionRid = collectionRid;
                this.PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo ?? throw new ArgumentNullException($"{nameof(partitionedQueryExecutionInfo)} can not be null.");
                this.PartitionKeyRanges = partitionKeyRanges;
                this.InitialPageSize = initialPageSize;
                this.MaxBufferedItemCount = maxBufferedItemCount;
                this.MaxConcurrency = maxConcurrency;
                this.MaxItemCount = maxItemCount;
                this.ReturnResultsInDeterministicOrder = returnResultsInDeterministicOrder;
                this.TestSettings = testSettings;
            }

            /// <summary>
            /// Get the sql query spec
            /// </summary>
            public SqlQuerySpec SqlQuerySpec { get; }

            /// <summary>
            /// Gets the collection rid to drain documents from.
            /// </summary>
            public string CollectionRid { get; }

            /// <summary>
            /// Gets the serialized version of the PipelinedDocumentQueryExecutionContext.
            /// </summary>
            public PartitionedQueryExecutionInfo PartitionedQueryExecutionInfo { get; }

            /// <summary>
            /// Gets the partition key ranges to fan out to.
            /// </summary>
            public List<PartitionKeyRange> PartitionKeyRanges { get; }

            /// <summary>
            /// Gets the initial page size for each document producer.
            /// </summary>
            public int InitialPageSize { get; }

            /// <summary>
            /// Gets the max concurrency
            /// </summary>
            public int? MaxConcurrency { get; }

            /// <summary>
            /// Gets the max item count
            /// </summary>
            public int? MaxItemCount { get; }

            /// <summary>
            /// Gets the max buffered item count
            /// </summary>
            public int? MaxBufferedItemCount { get; }

            public bool ReturnResultsInDeterministicOrder { get; }

            public TestInjections TestSettings { get; }
        }

        #region ItemProducerTreeComparableTask
        /// <summary>
        /// Comparable task for the ComparableTaskScheduler.
        /// This is specifically for tasks that fetch from partitions in a document producer tree.
        /// </summary>
        private sealed class ItemProducerTreeComparableTask : ComparableTask
        {
            /// <summary>
            /// The producer to fetch from.
            /// </summary>
            private readonly ItemProducerTree producer;

            /// <summary>
            /// Initializes a new instance of the ItemProducerTreeComparableTask class.
            /// </summary>
            /// <param name="producer">The producer to fetch from.</param>
            /// <param name="taskPriorityFunction">The callback to determine the fetch priority of the document producer.</param>
            public ItemProducerTreeComparableTask(
                ItemProducerTree producer,
                Func<ItemProducerTree, int> taskPriorityFunction)
                : base(taskPriorityFunction(producer))
            {
                this.producer = producer;
            }

            /// <summary>
            /// Entry point for the function to start fetching.
            /// </summary>
            /// <param name="token">The cancellation token.</param>
            /// <returns>A task to await on.</returns>
            public override Task StartAsync(CancellationToken token)
            {
                return this.producer.BufferMoreDocumentsAsync(token);
            }

            /// <summary>
            /// Determines whether this class is equal to another task.
            /// </summary>
            /// <param name="other">The other task</param>
            /// <returns>Whether this class is equal to another task.</returns>
            public override bool Equals(IComparableTask other)
            {
                return this.Equals(other as ItemProducerTreeComparableTask);
            }

            /// <summary>
            /// Gets the hash code for this task.
            /// </summary>
            /// <returns>The hash code for this task.</returns>
            public override int GetHashCode()
            {
                return this.producer.PartitionKeyRange.GetHashCode();
            }

            /// <summary>
            /// Internal implementation of equality.
            /// </summary>
            /// <param name="other">The other comparable task to check for equality.</param>
            /// <returns>Whether or not the comparable tasks are equal.</returns>
            private bool Equals(ItemProducerTreeComparableTask other)
            {
                return this.producer.PartitionKeyRange.Equals(other.producer.PartitionKeyRange);
            }
        }
        #endregion
    }
}
