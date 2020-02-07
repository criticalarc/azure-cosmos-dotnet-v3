﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate.Aggregators
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Concrete implementation of IAggregator that can take the global sum from the local sum of multiple partitions and continuations.
    /// Let sum_i,j be the sum from the ith continuation in the jth partition, 
    /// then the sum for the entire query is SUM(sum_i,j for all i and j).
    /// </summary>
    internal sealed class SumAggregator : IAggregator
    {
        /// <summary>
        /// The global sum.
        /// </summary>
        private double globalSum;

        private SumAggregator(double globalSum)
        {
            this.globalSum = globalSum;
        }

        /// <summary>
        /// Adds a local sum to the global sum.
        /// </summary>
        /// <param name="localSum">The local sum.</param>
        public void Aggregate(CosmosElement localSum)
        {
            // If someone tried to add an undefined just set the globalSum to NaN and it will stay that way for the duration of the aggregation.
            if (localSum == null)
            {
                this.globalSum = double.NaN;
            }
            else
            {
                if (!(localSum is CosmosNumber cosmosNumber))
                {
                    throw new ArgumentException("localSum must be a number.");
                }

                this.globalSum += Number64.ToDouble(cosmosNumber.Value);
            }
        }

        /// <summary>
        /// Gets the current sum.
        /// </summary>
        /// <returns>The current sum.</returns>
        public CosmosElement GetResult()
        {
            if (double.IsNaN(this.globalSum))
            {
                return null;
            }

            return CosmosNumber64.Create(this.globalSum);
        }

        public string GetContinuationToken()
        {
            return this.globalSum.ToString("G17", CultureInfo.InvariantCulture);
        }

        public static TryCatch<IAggregator> TryCreate(string continuationToken)
        {
            double partialSum;
            if (continuationToken != null)
            {
                if (!double.TryParse(continuationToken, out partialSum))
                {
                    return TryCatch<IAggregator>.FromException(
                        new MalformedContinuationTokenException($"Malformed {nameof(SumAggregator)} continuation token: {continuationToken}"));
                }
            }
            else
            {
                partialSum = 0.0;
            }

            return TryCatch<IAggregator>.FromResult(
                new SumAggregator(partialSum));
        }
    }
}
