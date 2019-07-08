﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class StandByFeedContinuationTokenTests
    {
        private const string ContainerRid = "containerRid";

        [TestMethod]
        public async Task EnsureInitialized_CreatesToken_WithNoInitialContinuation()
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken compositeToken = await StandByFeedContinuationToken.CreateAsync(StandByFeedContinuationTokenTests.ContainerRid, null, StandByFeedContinuationTokenTests.CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, token.Range.Max);
            Assert.AreEqual(keyRanges[0].Id, rangeId);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, token2.Range.Max);
            Assert.AreEqual(keyRanges[1].Id, rangeId2);
        }

        [TestMethod]
        public async Task EnsureInitialized_CreatesToken_WithInitialContinuation()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
                {
                    StandByFeedContinuationTokenTests.BuildTokenForRange("A", "B", "token1"),
                    StandByFeedContinuationTokenTests.BuildTokenForRange("C", "D", "token2")
                };

            string initialToken = JsonConvert.SerializeObject(compositeContinuationTokens);

            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken compositeToken = await StandByFeedContinuationToken.CreateAsync(StandByFeedContinuationTokenTests.ContainerRid, initialToken, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRanges[0].MaxExclusive, token.Range.Max);
            Assert.AreEqual(keyRanges[0].Id, rangeId);
            Assert.AreEqual(compositeContinuationTokens[0].Token, token.Token);

            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            Assert.AreEqual(keyRanges[1].MaxExclusive, token2.Range.Max);
            Assert.AreEqual(keyRanges[1].Id, rangeId2);
            Assert.AreEqual(compositeContinuationTokens[1].Token, token2.Token);
        }

        [TestMethod]
        public async Task SerializationIsExpected()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationTokenTests.BuildTokenForRange("A", "B", "C"),
                StandByFeedContinuationTokenTests.BuildTokenForRange("D", "E", "F")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = await StandByFeedContinuationToken.CreateAsync(StandByFeedContinuationTokenTests.ContainerRid, null, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentTokenAsync();
            token.Token = "C";
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentTokenAsync();
            token2.Token = "F";
            compositeToken.MoveToNextToken();

            Assert.AreEqual(expected, compositeToken.ToString());
        }

        [TestMethod]
        public async Task MoveToNextTokenCircles()
        {
            List<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "D", MaxExclusive ="E" },
            };
            StandByFeedContinuationToken compositeToken = await StandByFeedContinuationToken.CreateAsync(StandByFeedContinuationTokenTests.ContainerRid, null, CreateCacheFromRange(keyRanges));
            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[0].MinInclusive, token.Range.Min);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[1].MinInclusive, token2.Range.Min);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token3, string rangeId3) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[0].MinInclusive, token3.Range.Min);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token4, string rangeId4) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRanges[1].MinInclusive, token4.Range.Min);
        }

        [TestMethod]
        public async Task HandleSplitGeneratesChildren()
        {
            List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
            {
                StandByFeedContinuationTokenTests.BuildTokenForRange("A", "C", "token1"),
                StandByFeedContinuationTokenTests.BuildTokenForRange("C", "F", "token2")
            };

            string expected = JsonConvert.SerializeObject(compositeContinuationTokens);

            List<Documents.PartitionKeyRange> keyRangesAfterSplit = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B" },
                new Documents.PartitionKeyRange() { MinInclusive = "B", MaxExclusive ="C" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="F" },
            };

            StandByFeedContinuationToken compositeToken = await StandByFeedContinuationToken.CreateAsync(StandByFeedContinuationTokenTests.ContainerRid, expected, CreateCacheFromRange(keyRangesAfterSplit));

            (CompositeContinuationToken token, string rangeId) = await compositeToken.GetCurrentTokenAsync();
            // Current should be updated
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, token.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, token.Range.Max);
            Assert.AreEqual(compositeContinuationTokens[0].Token, token.Token);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token2, string rangeId2) = await compositeToken.GetCurrentTokenAsync();
            // Next should be the original second
            Assert.AreEqual(compositeContinuationTokens[1].Range.Min, token2.Range.Min);
            Assert.AreEqual(compositeContinuationTokens[1].Range.Max, token2.Range.Max);
            Assert.AreEqual(compositeContinuationTokens[1].Token, token2.Token);
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token3, string rangeId3) = await compositeToken.GetCurrentTokenAsync();
            // Finally the new children
            Assert.AreEqual(keyRangesAfterSplit[1].MinInclusive, token3.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[1].MaxExclusive, token3.Range.Max);
            Assert.AreEqual(compositeContinuationTokens[0].Token, token3.Token);
            // And go back to the beginning
            compositeToken.MoveToNextToken();
            (CompositeContinuationToken token5, string rangeId5) = await compositeToken.GetCurrentTokenAsync();
            Assert.AreEqual(keyRangesAfterSplit[0].MinInclusive, token5.Range.Min);
            Assert.AreEqual(keyRangesAfterSplit[0].MaxExclusive, token5.Range.Max);
            Assert.AreEqual(compositeContinuationTokens[0].Token, token5.Token);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task ConstructorWithInvalidTokenFormat()
        {
            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "C", MaxExclusive ="D", Id = "1" },
            };

            StandByFeedContinuationToken token = await StandByFeedContinuationToken.CreateAsync("containerRid", "notatoken", CreateCacheFromRange(keyRanges));
            await token.GetCurrentTokenAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task ConstructorWithNullContainer()
        {
            await StandByFeedContinuationToken.CreateAsync(null, null, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task ConstructorWithNullDelegate()
        {
            await StandByFeedContinuationToken.CreateAsync("something", null, null);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_ContinuationIsSet()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions(){ };

            ChangeFeedRequestOptions.FillContinuationToken(request, "something");
            requestOptions.PopulateRequestOptions(request);

            Assert.AreEqual("something", request.Headers.IfNoneMatch);
            Assert.IsNull(request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_DefaultValues()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions() { };

            requestOptions.PopulateRequestOptions(request);

            Assert.AreEqual(ChangeFeedRequestOptions.IfNoneMatchAllHeaderValue, request.Headers.IfNoneMatch);
            Assert.IsNull(request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_MaxItemSizeIsSet()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions() { };

            ChangeFeedRequestOptions.FillMaxItemCount(request, 10);
            requestOptions.PopulateRequestOptions(request);

            Assert.AreEqual("10", request.Headers[Documents.HttpConstants.HttpHeaders.PageSize]);
            Assert.AreEqual(ChangeFeedRequestOptions.IfNoneMatchAllHeaderValue, request.Headers.IfNoneMatch);
            Assert.IsNull(request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_ContinuationBeatsStartTime()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                StartTime = new DateTime(1985, 1, 1)
            };

            ChangeFeedRequestOptions.FillContinuationToken(request, "something");
            requestOptions.PopulateRequestOptions(request);

            Assert.AreEqual("something", request.Headers.IfNoneMatch);
            Assert.IsNull(request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_AddsStartTime()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                StartTime = new DateTime(1985, 1, 1, 0, 0,0, DateTimeKind.Utc)
            };

            requestOptions.PopulateRequestOptions(request);

            Assert.AreEqual("Tue, 01 Jan 1985 00:00:00 GMT", request.Headers[Documents.HttpConstants.HttpHeaders.IfModifiedSince]);
            Assert.IsNull(request.Headers.IfNoneMatch);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_AddsPartitionKeyRangeId()
        {
            RequestMessage request = new RequestMessage();
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions();

            ChangeFeedRequestOptions.FillPartitionKeyRangeId(request, "randomPK");

            Assert.AreEqual("randomPK", request.PartitionKeyRangeId.PartitionKeyRangeId);
        }

        private static StandByFeedContinuationToken.PartitionKeyRangeCacheDelegate CreateCacheFromRange(IReadOnlyList<Documents.PartitionKeyRange> keyRanges)
        {
            return (string containerRid, Documents.Routing.Range<string> ranges, bool forceRefresh) =>
            {
                if (ranges.Max.Equals(Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey))
                {
                    return Task.FromResult(keyRanges);
                }

                IReadOnlyList<Documents.PartitionKeyRange> filteredRanges = new List<Documents.PartitionKeyRange>(keyRanges.Where(range=> range.MinInclusive.CompareTo(ranges.Min) >= 0 && range.MaxExclusive.CompareTo(ranges.Max) <= 0));

                return Task.FromResult(filteredRanges);
            };
        }

        private static CompositeContinuationToken BuildTokenForRange(
            string min, 
            string max, 
            string token)
        {
            return new CompositeContinuationToken()
            {
                Token = token,
                Range = new Documents.Routing.Range<string>(min, max, true, false)
            };
        }
    }
}
