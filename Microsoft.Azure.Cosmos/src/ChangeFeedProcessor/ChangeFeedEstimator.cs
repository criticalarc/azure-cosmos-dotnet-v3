﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Used to estimate the pending work remaining to be read by a <see cref="ChangeFeedProcessor"/> deployment.
    /// </summary>
    /// <remarks>
    /// The estimator is meant to monitor an existing deployment of <see cref="ChangeFeedProcessor"/> instances that are currently running.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/change-feed-processor"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/how-to-use-change-feed-estimator"/>
    public abstract class ChangeFeedEstimator
    {
        /// <summary>
        /// Gets the estimation per lease in the lease container.
        /// </summary>
        /// <param name="changeFeedEstimatorRequestOptions">(Optional) Customize the estimation iterator.</param>
        /// <returns>An iterator that yields an estimation of pending work in amount of documents per distributed lease token.</returns>
        public abstract FeedIterator<ChangeFeedProcessorState> GetCurrentStateIterator(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions = null);
    }
}
