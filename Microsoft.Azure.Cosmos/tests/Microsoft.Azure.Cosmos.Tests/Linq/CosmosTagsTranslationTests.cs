//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosTagsTranslationTests
    {
        [TestMethod]
        public void ExcludedTermIsFalseOnlyWhenTheTagArrayContainsTheTag()
        {
            Expression<Func<TagsDocument, bool>> expr = d => CosmosTags.Match(d.Tags, new[] { "!ns:deleted=true" }, TagsQueryOptions.Basic);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual(
                "(NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=\"))"
                + " AND NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=true\")))",
                sql);
        }

        [TestMethod]
        public void ExcludedTermWithDocumentNotTagsIsFalseOnlyWhenTheTagArrayContainsTheTag()
        {
            Expression<Func<TagsDocument, bool>> expr = d => CosmosTags.Match(d.Tags, new[] { "!ns:deleted=true" }, TagsQueryOptions.Default);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual(
                "(NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=\"))"
                + " AND NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=true\")))",
                sql);
        }

        [TestMethod]
        public void RequiredTermStaysUnguardedBesideAnExcludedTerm()
        {
            Expression<Func<TagsDocument, bool>> expr = d => CosmosTags.Match(d.Tags, new[] { "*ns:deleted=true", "!ns:protected=true" }, TagsQueryOptions.Basic);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual(
                "((ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=\") OR ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:deleted=true\"))"
                + " AND NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:protected=\"))"
                + " AND NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:protected=true\")))",
                sql);
        }

        [TestMethod]
        public void RequiredAndExcludedTermsOfOneTagNameAreEachTranslated()
        {
            Expression<Func<TagsDocument, bool>> expr = d => CosmosTags.Match(d.Tags, new[] { "*ns:state=open", "!ns:state=closed" }, TagsQueryOptions.Basic);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual(
                "(NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:state=\"))"
                + " AND NOT(IS_DEFINED(d[\"Tags\"][\"tag\"]) AND ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:state=closed\"))"
                + " AND (ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:state=\") OR ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:state=open\")))",
                sql);
        }

        [TestMethod]
        public void ExcludedWildcardTermIsUnchanged()
        {
            Expression<Func<TagsDocument, bool>> expr = d => CosmosTags.Match(d.Tags, new[] { "!ns:group=" }, TagsQueryOptions.Basic);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(NOT(IS_DEFINED(d[\"Tags\"][\"tags\"][\"ns:group\"])))", sql);
        }

        [TestMethod]
        public void NegatedPlainTermIsUnchanged()
        {
            Expression<Func<TagsDocument, bool>> expr = d => !CosmosTags.Match(d.Tags, new[] { "ns:group=vip" }, TagsQueryOptions.Basic);

            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual(
                "(NOT ((NOT(IS_DEFINED(d[\"Tags\"][\"tags\"][\"ns:group\"])) OR ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:group=\") OR ARRAY_CONTAINS(d[\"Tags\"][\"tag\"], \"ns:group=vip\"))))",
                sql);
        }

        public class TagsDocument
        {
            public string[] Tags { get; set; }
        }
    }
}
