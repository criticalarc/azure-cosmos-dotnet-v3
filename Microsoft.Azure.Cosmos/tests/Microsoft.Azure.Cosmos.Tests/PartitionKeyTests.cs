//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;

    [TestClass]
    public class PartitionKeyTests
    {
        [TestMethod]
        public void NullValue()
        {
            new PartitionKey(null);
        }

        [TestMethod]
        public void ToStringGetsJsonString()
        {
            const string somePK = "somePK";
            string expected = $"[\"{somePK}\"]";
            PartitionKey pk = new PartitionKey(somePK);
            Assert.AreEqual(expected, pk.ToString());
        }

        [TestMethod]
        public void TestPartitionKeyValues()
        {
            Tuple<dynamic, string>[] testcases =
            {
                Tuple.Create<dynamic, string>(Documents.Undefined.Value, "[{}]"),
                Tuple.Create<dynamic, string>(Documents.Undefined.Value, "[{}]"),
                Tuple.Create<dynamic, string>(false, "[false]"),
                Tuple.Create<dynamic, string>(true, "[true]"),
                Tuple.Create<dynamic, string>(123.456, "[123.456]"),
                Tuple.Create<dynamic, string>("PartitionKeyValue", "[\"PartitionKeyValue\"]"),
            };

            foreach (Tuple<object, string> testcase in testcases)
            {
                Assert.AreEqual(testcase.Item2, new PartitionKey(testcase.Item1).ToString());
            }
        }

        [TestMethod]
        public void TestPartitionKeyDefinitionAreEquivalent()
        {
            //Different partition key path test
            PartitionKeyDefinition definition1 = new PartitionKeyDefinition();
            definition1.Paths.Add("/pk1");

            PartitionKeyDefinition definition2 = new PartitionKeyDefinition();
            definition2.Paths.Add("/pk2");

            Assert.IsFalse(PartitionKeyDefinition.AreEquivalent(definition1, definition2));

            //Different partition kind test
            definition1 = new PartitionKeyDefinition();
            definition1.Paths.Add("/pk1");
            definition1.Kind = PartitionKind.Hash;

            definition2 = new PartitionKeyDefinition();
            definition2.Paths.Add("/pk1");
            definition2.Kind = PartitionKind.Range;

            Assert.IsFalse(PartitionKeyDefinition.AreEquivalent(definition1, definition2));

            //Different partition version test
            definition1 = new PartitionKeyDefinition();
            definition1.Paths.Add("/pk1");
            definition1.Version = PartitionKeyDefinitionVersion.V1;

            definition2 = new PartitionKeyDefinition();
            definition2.Paths.Add("/pk1");
            definition2.Version = PartitionKeyDefinitionVersion.V2;

            Assert.IsFalse(PartitionKeyDefinition.AreEquivalent(definition1, definition2));

            //Same partition key path test
            definition1 = new PartitionKeyDefinition();
            definition1.Paths.Add("/pk1");

            definition2 = new PartitionKeyDefinition();
            definition2.Paths.Add("/pk1");

            Assert.IsTrue(PartitionKeyDefinition.AreEquivalent(definition1, definition2));
        }
    }
}