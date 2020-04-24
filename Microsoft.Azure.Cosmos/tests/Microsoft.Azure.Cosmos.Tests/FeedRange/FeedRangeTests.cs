﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FeedRangeTests
    {
        [TestMethod]
        public void FeedRangeEPK_Range()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEPK feedRangeEPK = new FeedRangeEPK(range);
            Assert.AreEqual(range, feedRangeEPK.Range);
        }

        [TestMethod]
        public void FeedRangePK_PK()
        {
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            Assert.AreEqual(partitionKey, feedRangePartitionKey.PartitionKey);
        }

        [TestMethod]
        public void FeedRangePKRangeId_PKRange()
        {
            string pkRangeId = Guid.NewGuid().ToString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(pkRangeId);
            Assert.AreEqual(pkRangeId, feedRangePartitionKeyRange.PartitionKeyRangeId);
        }

        [TestMethod]
        public async Task FeedRangeEPK_GetEffectiveRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEPK feedRangeEPK = new FeedRangeEPK(range);
            List<Documents.Routing.Range<string>> ranges = await feedRangeEPK.GetEffectiveRangesAsync(Mock.Of<Routing.IRoutingMapProvider>(), null, null);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(range, ranges[0]);
        }

        [TestMethod]
        public async Task FeedRangePK_GetEffectiveRangesAsync()
        {
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/id");
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            Documents.Routing.Range<string> range = Documents.Routing.Range<string>.GetPointRange(partitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition));
            List<Documents.Routing.Range<string>> ranges = await feedRangePartitionKey.GetEffectiveRangesAsync(Mock.Of<Routing.IRoutingMapProvider>(), null, partitionKeyDefinition);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(range, ranges[0]);
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetEffectiveRangesAsync()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            Routing.IRoutingMapProvider routingProvider = Mock.Of<Routing.IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.Is<string>(s => s == partitionKeyRange.Id), It.IsAny<bool>()))
                .ReturnsAsync(partitionKeyRange);
            List<Documents.Routing.Range<string>> ranges = await feedRangePartitionKeyRange.GetEffectiveRangesAsync(routingProvider, null, null);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(partitionKeyRange.ToRange().Min, ranges[0].Min);
            Assert.AreEqual(partitionKeyRange.ToRange().Max, ranges[0].Max);
        }

        [TestMethod]
        public async Task FeedRangeEPK_GetPartitionKeyRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = range.Min, MaxExclusive = range.Max };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            Routing.IRoutingMapProvider routingProvider = Mock.Of<Routing.IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetOverlappingRangesAsync(It.IsAny<string>(), It.Is<Documents.Routing.Range<string>>(s => s == range), It.IsAny<bool>()))
                .ReturnsAsync(new List<Documents.PartitionKeyRange>() { partitionKeyRange });

            FeedRangeEPK feedRangeEPK = new FeedRangeEPK(range);
            IEnumerable<string> pkRanges = await feedRangeEPK.GetPartitionKeyRangesAsync(routingProvider, null, null, default(CancellationToken));
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public async Task FeedRangePK_GetPartitionKeyRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = range.Min, MaxExclusive = range.Max };
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/id");
            PartitionKey partitionKey = new PartitionKey("test");
            Routing.IRoutingMapProvider routingProvider = Mock.Of<Routing.IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetOverlappingRangesAsync(It.IsAny<string>(), It.IsAny<Documents.Routing.Range<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<Documents.PartitionKeyRange>() { partitionKeyRange });

            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            IEnumerable<string> pkRanges = await feedRangePartitionKey.GetPartitionKeyRangesAsync(routingProvider, null, partitionKeyDefinition, default(CancellationToken));
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetPartitionKeyRangesAsync()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IEnumerable<string> pkRanges = await feedRangePartitionKeyRange.GetPartitionKeyRangesAsync(Mock.Of<Routing.IRoutingMapProvider>(), null, null, default(CancellationToken));
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public void FeedRangeEPK_RequestVisitor()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEPK feedRange = new FeedRangeEPK(range);
            RequestMessage requestMessage = new RequestMessage();
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            feedRange.Accept(feedRangeVisitor);
            Assert.AreEqual(0, requestMessage.Properties.Count);
        }

        [TestMethod]
        public void FeedRangePKRangeId_RequestVisitor()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            RequestMessage requestMessage = new RequestMessage();
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            feedRangePartitionKeyRange.Accept(feedRangeVisitor);
            Assert.IsNotNull(requestMessage.PartitionKeyRangeId);
            Assert.IsFalse(requestMessage.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void FeedRangePK_RequestVisitor()
        {
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            RequestMessage requestMessage = new RequestMessage();
            FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(requestMessage);
            feedRangePartitionKey.Accept(feedRangeVisitor);
            Assert.AreEqual(partitionKey.InternalKey.ToJsonString(), requestMessage.Headers.PartitionKey);
            Assert.IsFalse(requestMessage.IsPartitionKeyRangeHandlerRequired);
        }

        [TestMethod]
        public void FeedRangeEPK_ToJsonFromJson()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEPK feedRangeEPK = new FeedRangeEPK(range);
            string representation = feedRangeEPK.ToJsonString();
            FeedRangeEPK feedRangeEPKDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangeEPK;
            Assert.IsNotNull(feedRangeEPKDeserialized);
            Assert.AreEqual(feedRangeEPK.Range.Min, feedRangeEPKDeserialized.Range.Min);
            Assert.AreEqual(feedRangeEPK.Range.Max, feedRangeEPKDeserialized.Range.Max);
        }

        [TestMethod]
        public void FeedRangePK_ToJsonFromJson()
        {
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            string representation = feedRangePartitionKey.ToJsonString();
            FeedRangePartitionKey feedRangePartitionKeyDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangePartitionKey;
            Assert.IsNotNull(feedRangePartitionKeyDeserialized);
            Assert.AreEqual(feedRangePartitionKey.PartitionKey.ToJsonString(), feedRangePartitionKeyDeserialized.PartitionKey.ToJsonString());
        }

        [TestMethod]
        public void FeedRangePKRangeId_ToJsonFromJson()
        {
            string pkRangeId = Guid.NewGuid().ToString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(pkRangeId);
            string representation = feedRangePartitionKeyRange.ToJsonString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRangeDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangePartitionKeyRange;
            Assert.IsNotNull(feedRangePartitionKeyRangeDeserialized);
            Assert.AreEqual(feedRangePartitionKeyRange.PartitionKeyRangeId, feedRangePartitionKeyRangeDeserialized.PartitionKeyRangeId);
        }
    }
}
