﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Newtonsoft.Json;

    /// <summary>
    /// <para>
    /// Executing an OrderBy query for a partitioned collection, say, "select * from root order by root.key ASC", 
    /// boils down to solving a version of k-way merge sort, where, each of the k partitions produces a sorted stream of values.
    /// </para>
    /// <para>
    /// Now if a query requires multiple ExecuteNextAsync, we return a serialized version of OrderByContinuationToken, to the users
    /// so that they can resume the query from where they left off at a later point in time. Below we describe the components of
    /// OrderByContinuation in detail. 
    /// </para> 
    /// A key notion of a OrderByContinuation is that of the "Target Partition", which is effectively 
    /// the partition from whose stream the last value was consumed during the execution of the query. 
    /// We construct our continuation token composing the information of the target partition and 
    /// the metadata related to the last value seen from the target partition.  
    /// <para>
    /// One key difference in our version of the k-way merge sort from the classical version of the k-way merge sort is that, in
    /// our case there is a partial order on the 2-tuple consisting of {partition, value seen from that partition}. 
    /// For example, (P1, 2) less than (P2, 2) if P1 is less than P2 (i.e., P1.MinRange is less than P2.MinRange). This difference allowed 
    /// us to shorten the continuation token (i.e., we only need to know the state of the target range), 
    /// but at the cost of performance penalties in pathological cases (e.g., Partition 0 is heavily throttled 
    /// but still serving value "2", while partition 1 has a large number of "2"s which can't be served
    /// to the user).
    /// </para>
    /// <para>
    /// Considering the above fact, three important points to note here are:
    ///     1. If the latest value seen at the target partition is X, then we have exhausted all value less than X (for ASC order),
    ///     all partitions that have smaller Range.Min than that of the target partition. 
    ///     2. All partitions, that have greater Range.Min than that of the target partition, have exhausted all values less than or equal to X 
    /// </para>    
    /// <para>
    /// Given this background, below is an example of order by continuation token. The class members below explains the different 
    /// component/states of the continuation token.
    /// </para> 
    /// </summary>
    /// <example>
    /// Order by continuation token example.
    /// <![CDATA[
    ///  {"compositeToken":{"token":"+RID:OpY0AN-mFAACAAAAAAAABA==#RT:1#TRC:1#RTD:qdTAEA==","range":{"min":"05C1D9CD673398","max":"05C1E399CD6732"}},"orderByItems"[{"item":2}],"rid":"OpY0AN-mFAACAAAAAAAABA==","skipCount":0,"filter":"r.key > 1"}
    /// ]]>
    /// </example>
    internal sealed class OrderByContinuationToken
    {
        private const string CompositeTokenName = "compositeToken";
        private const string OrderByItemsName = "orderByItems";
        private const string RidName = "rid";
        private const string SkipCountName = "skipCount";
        private const string FilterName = "filter";

        /// <summary>
        /// Initializes a new instance of the OrderByContinuationToken struct.
        /// </summary>
        /// <param name="compositeContinuationToken">The composite continuation token (refer to property documentation).</param>
        /// <param name="orderByItems">The order by items (refer to property documentation).</param>
        /// <param name="rid">The rid (refer to property documentation).</param>
        /// <param name="skipCount">The skip count (refer to property documentation).</param>
        /// <param name="filter">The filter (refer to property documentation).</param>
        public OrderByContinuationToken(
            CompositeContinuationToken compositeContinuationToken,
            IList<OrderByItem> orderByItems,
            string rid,
            int skipCount,
            string filter)
        {
            if (orderByItems.Count == 0)
            {
                throw new ArgumentException($"{nameof(orderByItems)} can not be empty.");
            }

            if (string.IsNullOrWhiteSpace(rid))
            {
                throw new ArgumentNullException($"{nameof(rid)} can not be null or empty or whitespace.");
            }

            if (skipCount < 0)
            {
                throw new ArgumentException($"{nameof(skipCount)} can not be negative.");
            }

            //// filter is allowed to be null.
            this.CompositeContinuationToken = compositeContinuationToken ?? throw new ArgumentNullException(nameof(compositeContinuationToken));
            this.OrderByItems = orderByItems ?? throw new ArgumentNullException(nameof(orderByItems));
            this.Rid = rid;
            this.SkipCount = skipCount;
            this.Filter = filter;
        }

        /// <summary>
        /// Gets: Target partition states, including backend continuation and partition key range information. 
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///  {"compositeToken":{"token":"+RID:OpY0AN-mFAACAAAAAAAABA==#RT:1#TRC:1#RTD:qdTAEA==","range":{"min":"05C1D9CD673398","max":"05C1E399CD6732"}}
        /// ]]>
        /// </example>
        [JsonProperty(CompositeTokenName)]
        public CompositeContinuationToken CompositeContinuationToken
        {
            get;
        }

        /// <summary>
        /// Gets: Values in the top most OrderByQueryResult from the target partition.
        /// orderByItems is used for filtering after we resume.
        /// </summary>
        /// <example>
        /// Here, the item 2 means that, it was an orderBy by integer field, and when the query paused,
        /// the latest value seen from the corresponding partition was 2. 
        /// <![CDATA[
        ///  "orderByItems"[{"item":2}]
        /// ]]>
        /// </example>
        /// <remarks>
        /// Right now, we don't support orderBy by multiple fields, so orderByItems is an array of one element. 
        /// </remarks>>
        [JsonProperty(OrderByItemsName)]
        public IList<OrderByItem> OrderByItems
        {
            get;
        }

        /// <summary>
        /// Gets: Rid in the top most OrderByQueryResult from the target partition.
        /// Rid is used for filtering after we resume, when orderByItems have the same value.
        /// </summary>
        /// <remarks>
        /// Note that, Rid is just a marker from the backend point of view, and the
        /// document with the Rid might not exist upon resuming a query (due to deletion or
        /// other reasons). The backend will just return the next available result logically 
        /// succeeding the marker. 
        /// </remarks>
        /// <example>
        /// <![CDATA[
        ///  "rid":"OpY0AN-mFAACAAAAAAAABA=="
        /// ]]>
        /// </example>
        [JsonProperty(RidName)]
        public string Rid
        {
            get;
        }

        /// <summary>
        /// <para>
        /// Gets: Skip count is necessary for JOIN queries to resume. Azure Cosmos DB's joins are much different from standard 
        /// SQL joins. While standard SQL joins happen across two tables, Azure Cosmos DB joins happens over a single collection 
        /// (think single table with each row having dynamic number of columns). While executing a join query, 
        /// each Azure Cosmos DB document (i.e, each row), though, can generate multiple result items. You can look up the documentation 
        /// online to understand this better. 
        /// </para>
        /// <para>
        /// This behavior has implications on how pagination work for CosmosDB queries, especially for order by queries across
        /// multiple partition. 
        /// </para>
        /// <para>
        /// To understand complexity, let's take an example. Let's say that there is only 1 partition in a collection, and the collection
        /// has 2 documents. And each document generate 6 results on a hypothetical join query. Now, if someone issues the query with a page size
        /// of 10, while fetching the second page of the query (as it has already exhausted the results produced by the join on the first document) 
        /// needs to resume from the second document and skip the first 6 - ((2 * 6) - 10) = 4 results. 
        /// </para>
        /// The skip count keeps track of that information. 
        /// </summary> 
        [JsonProperty(SkipCountName)]
        public int SkipCount
        {
            get;
        }

        /// <summary>
        /// Gets: We use the filter to rewrite the OrderBy query when resuming from a continuation token. 
        /// </summary>
        /// <example>
        /// <para>
        /// In this example snippet below the filter string indicates that the query was an OrderBy query 
        /// and when the query was paused it had already output all the values value greater than 1. 
        /// And when the query resumes it only needs to fetch value greater than 1. 
        /// </para>
        /// <para>
        /// Note that, if any value less than 1 that was inserted after the query started won't be delivered as a 
        /// part of the result. 
        /// <![CDATA[
        ///  "filter":"r.key > 1"
        /// ]]>
        /// </para>
        /// </example>
        [JsonProperty(FilterName)]
        public string Filter
        {
            get;
        }

        public static CosmosElement ToCosmosElement(OrderByContinuationToken orderByContinuationToken)
        {
            CosmosElement compositeContinuationToken = CompositeContinuationToken.ToCosmosElement(orderByContinuationToken.CompositeContinuationToken);
            List<CosmosElement> orderByItemsRaw = new List<CosmosElement>();
            foreach (OrderByItem orderByItem in orderByContinuationToken.OrderByItems)
            {
                orderByItemsRaw.Add(OrderByItem.ToCosmosElement(orderByItem));
            }

            CosmosArray orderByItems = CosmosArray.Create(orderByItemsRaw);

            CosmosElement filter = orderByContinuationToken.Filter == null ? (CosmosElement)CosmosNull.Create() : (CosmosElement)CosmosString.Create(orderByContinuationToken.Filter);

            CosmosObject cosmosObject = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { CompositeTokenName, compositeContinuationToken },
                    { OrderByItemsName, orderByItems },
                    { RidName, CosmosString.Create(orderByContinuationToken.Rid) },
                    { SkipCountName, CosmosNumber64.Create(orderByContinuationToken.SkipCount) },
                    { FilterName, filter },
                });

            return cosmosObject;
        }

        public static TryCatch<OrderByContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is not an object: {cosmosElement}"));
            }

            if (!cosmosObject.TryGetValue(CompositeTokenName, out CosmosElement compositeContinuationTokenElement))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is missing field: '{CompositeTokenName}': {cosmosElement}"));
            }

            TryCatch<CompositeContinuationToken> tryCompositeContinuation = CompositeContinuationToken.TryCreateFromCosmosElement(compositeContinuationTokenElement);
            if (!tryCompositeContinuation.Succeeded)
            {
                return TryCatch<OrderByContinuationToken>.FromException(tryCompositeContinuation.Exception);
            }

            CompositeContinuationToken compositeContinuationToken = tryCompositeContinuation.Result;

            if (!cosmosObject.TryGetValue(OrderByItemsName, out CosmosArray orderByItemsRaw))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is missing field: '{OrderByItemsName}': {cosmosElement}"));
            }

            List<OrderByItem> orderByItems = orderByItemsRaw.Select(x => OrderByItem.FromCosmosElement(x)).ToList();

            if (!cosmosObject.TryGetValue(RidName, out CosmosString ridRaw))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is missing field: '{RidName}': {cosmosElement}"));
            }

            string rid = ridRaw.Value;

            if (!cosmosObject.TryGetValue(SkipCountName, out CosmosNumber64 skipCountRaw))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is missing field: '{SkipCountName}': {cosmosElement}"));
            }

            int skipCount = (int)Number64.ToLong(skipCountRaw.GetValue());

            if (!cosmosObject.TryGetValue(FilterName, out CosmosElement filterRaw))
            {
                return TryCatch<OrderByContinuationToken>.FromException(
                    new MalformedContinuationTokenException($"{nameof(OrderByContinuationToken)} is missing field: '{FilterName}': {cosmosElement}"));
            }

            string filter;
            if (filterRaw is CosmosString filterStringRaw)
            {
                filter = filterStringRaw.Value;
            }
            else
            {
                filter = null;
            }

            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                compositeContinuationToken,
                orderByItems,
                rid,
                skipCount,
                filter);
            return TryCatch<OrderByContinuationToken>.FromResult(orderByContinuationToken);
        }
    }
}
