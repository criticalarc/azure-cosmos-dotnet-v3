﻿//-----------------------------------------------------------------------
// <copyright file="SqlObjectVisitorBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Xml;
    using BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Sql;

    /// <summary>
    /// Baseline Tests for SqlObjectToString.
    /// </summary>
    [TestClass]
    public class SqlObjectVisitorBaselineTests : BaselineTests<SqlObjectVisitorInput, SqlObjectVisitorOutput>
    {
        [ClassInitialize]
        public static void Initialize(TestContext testContext)
        {
            // put class init code here
            JsonConvert.DefaultSettings = () =>
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                return settings;
            };
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlLiteral()
        {
            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();
            for (int i = 0; i < ' '; i++)
            {
                inputs.Add(
                    new SqlObjectVisitorInput(
                        $"Escape Sequence {i}",
                        SqlStringLiteral.Create(new string(new char[] { (char)i }))));
            }

            inputs.Add(new SqlObjectVisitorInput("Empty String", SqlStringLiteral.Create(string.Empty)));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlStringLiteral), SqlStringLiteral.Create("Hello")));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlStringLiteral) + " With Unicode", SqlStringLiteral.Create("💩")));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlNumberLiteral), SqlNumberLiteral.Create(0x5F3759DF)));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlNullLiteral), SqlNullLiteral.Singleton));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlBooleanLiteral) + "True", SqlBooleanLiteral.True));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlBooleanLiteral) + "False", SqlBooleanLiteral.False));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlUndefinedLiteral), SqlUndefinedLiteral.Create()));


            inputs.Add(new SqlObjectVisitorInput("Zero", SqlNumberLiteral.Create(0)));
            inputs.Add(new SqlObjectVisitorInput("Positive Number", SqlNumberLiteral.Create(1)));
            inputs.Add(new SqlObjectVisitorInput("Negative Number", SqlNumberLiteral.Create(-1)));

            inputs.Add(new SqlObjectVisitorInput("Double", SqlNumberLiteral.Create(1337.1337)));
            inputs.Add(new SqlObjectVisitorInput("E", SqlNumberLiteral.Create(Math.E)));
            inputs.Add(new SqlObjectVisitorInput("Pi", SqlNumberLiteral.Create(Math.PI)));
            inputs.Add(new SqlObjectVisitorInput("1/3", SqlNumberLiteral.Create(1.0 / 3.0)));
            long maxSafeInteger = 9007199254740991;
            inputs.Add(new SqlObjectVisitorInput($"Max Safe Integer {maxSafeInteger}", SqlNumberLiteral.Create(maxSafeInteger)));
            inputs.Add(new SqlObjectVisitorInput($"Max Safe Integer {maxSafeInteger} Plus One", SqlNumberLiteral.Create(maxSafeInteger + 1)));
            inputs.Add(new SqlObjectVisitorInput($"Max Safe Integer {maxSafeInteger} Minus One", SqlNumberLiteral.Create(maxSafeInteger - 1)));
            long minSafeInteger = -9007199254740991;
            inputs.Add(new SqlObjectVisitorInput($"Min Safe Integer: {minSafeInteger}", SqlNumberLiteral.Create(minSafeInteger)));
            inputs.Add(new SqlObjectVisitorInput($"Min Safe Integer: {minSafeInteger} Plus One", SqlNumberLiteral.Create(minSafeInteger + 1)));
            inputs.Add(new SqlObjectVisitorInput($"Min Safe Integer: {minSafeInteger} Minus One", SqlNumberLiteral.Create(minSafeInteger - 1)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(Double.PositiveInfinity)} {Double.PositiveInfinity}",
                SqlNumberLiteral.Create(Double.PositiveInfinity)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(Double.NegativeInfinity)} {Double.NegativeInfinity}",
                SqlNumberLiteral.Create(Double.NegativeInfinity)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(Double.NaN)} {Double.NaN}",
                SqlNumberLiteral.Create(Double.NaN)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(Double.Epsilon)} {Double.Epsilon}",
                SqlNumberLiteral.Create(Double.Epsilon)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(Double.MaxValue)} {Double.MaxValue}",
                SqlNumberLiteral.Create(Double.MaxValue)));
            inputs.Add(new SqlObjectVisitorInput(
               $"{nameof(Double.MinValue)} {Double.MinValue}",
               SqlNumberLiteral.Create(Double.MinValue)));

            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(long.MinValue)} {long.MinValue}",
                SqlNumberLiteral.Create(long.MinValue)));
            inputs.Add(new SqlObjectVisitorInput(
                $"{nameof(long.MaxValue)} {long.MaxValue}",
                SqlNumberLiteral.Create(long.MaxValue)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlScalarExpression()
        {
            SqlMemberIndexerScalarExpression somePath = SqlObjectBuilderUtils.CreateSqlMemberIndexerScalarExpression(
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("some")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("random")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("path")),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)));

            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlArrayCreateScalarExpression) + "Empty",
                SqlArrayCreateScalarExpression.Create()));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlArrayCreateScalarExpression) + "OneItem",
                SqlArrayCreateScalarExpression.Create(SqlLiteralScalarExpression.SqlNullLiteralScalarExpression)));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlArrayCreateScalarExpression) + "MultItems",
                SqlArrayCreateScalarExpression.Create(
                    SqlLiteralScalarExpression.SqlNullLiteralScalarExpression,
                    SqlLiteralScalarExpression.SqlNullLiteralScalarExpression,
                    SqlLiteralScalarExpression.SqlNullLiteralScalarExpression)));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlBetweenScalarExpression),
                SqlBetweenScalarExpression.Create(
                    somePath,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlBinaryScalarExpression),
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.Add,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3)))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlCoalesceScalarExpression),
                SqlCoalesceScalarExpression.Create(
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("if this is null")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("then return this")))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlConditionalScalarExpression),
                SqlConditionalScalarExpression.Create(
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("if true")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("then this")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("else this")))));

            foreach (bool not in new bool[] { true, false })
            {
                inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlInScalarExpression) + $" Not: {not}",
                SqlInScalarExpression.Create(
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("is this")),
                    not,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("this")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("set")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("of")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("values")))));
            }

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlObjectCreateScalarExpression) + " Empty",
                SqlObjectCreateScalarExpression.Create()));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlObjectCreateScalarExpression) + " OneProperty",
                SqlObjectCreateScalarExpression.Create(
                    SqlObjectProperty.Create(
                        SqlPropertyName.Create("Hello"),
                        SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"))))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlObjectCreateScalarExpression) + " MultiProperty",
                SqlObjectCreateScalarExpression.Create(
                    SqlObjectProperty.Create(
                        SqlPropertyName.Create("Hello"),
                        SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"))),
                    SqlObjectProperty.Create(
                        SqlPropertyName.Create("Hello"),
                        SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"))),
                    SqlObjectProperty.Create(
                        SqlPropertyName.Create("Hello"),
                        SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"))))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlParameterRefScalarExpression),
                SqlParameterRefScalarExpression.Create(
                    SqlParameter.Create("@param0"))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlPropertyRefScalarExpression),
                SqlPropertyRefScalarExpression.Create(
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("some")),
                    SqlIdentifier.Create("path"))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlUnaryScalarExpression),
                SqlUnaryScalarExpression.Create(
                    SqlUnaryScalarOperatorKind.BitwiseNot,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(nameof(SqlFunctionCallScalarExpression), SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Abs,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-42)))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlBinaryScalarOperators()
        {
            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();

            foreach (SqlBinaryScalarOperatorKind sqlBinaryScalarOperatorKind in Enum.GetValues(typeof(SqlBinaryScalarOperatorKind)))
            {
                inputs.Add(new SqlObjectVisitorInput(
                    sqlBinaryScalarOperatorKind.ToString(),
                    SqlBinaryScalarExpression.Create(
                        sqlBinaryScalarOperatorKind,
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0xDEADBEEF)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0xBAAAAAAD)))));
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlUnaryScalarOperators()
        {
            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();

            foreach (SqlUnaryScalarOperatorKind sqlUnaryScalarOperatorKind in Enum.GetValues(typeof(SqlUnaryScalarOperatorKind)))
            {
                inputs.Add(new SqlObjectVisitorInput(
                    sqlUnaryScalarOperatorKind.ToString(),
                    SqlUnaryScalarExpression.Create(
                        sqlUnaryScalarOperatorKind,
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0xDEADBEEF)))));
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlFunctionCalls()
        {
            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalCompareBsonBinaryData,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalCompareBsonBinaryData,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalCompareObjects,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalCompareObjects,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalObjectToArray,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalObjectToArray,
                    SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalProxyProjection,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalProxyProjection,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1234)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalRegexMatch,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalRegexMatch,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalStDistance,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalStDistance,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalStIntersects,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalStIntersects,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalStWithin,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalStWithin,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.InternalTryArrayContains,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.InternalTryArrayContains,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Abs,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Abs,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Acos,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Acos,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.All,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.All,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Any,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Any,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Array,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Array,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.ArrayConcat,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayConcat,
                    SqlArrayCreateScalarExpression.Create(
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3))),
                    SqlArrayCreateScalarExpression.Create(
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(4)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(6))))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.ArrayContains,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    SqlArrayCreateScalarExpression.Create(
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3))),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(true)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.ArrayLength,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayLength,
                     SqlArrayCreateScalarExpression.Create(
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3))))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.ArraySlice,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    SqlArrayCreateScalarExpression.Create(
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3))),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(4)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Asin,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Asin,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Atan,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Atan,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Atn2,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Atn2,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Avg,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Avg,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Ceiling,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Ceiling,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Concat,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Concat,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Contains,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Contains,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("strstr")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("str")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Cos,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Cos,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Cot,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Cot,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Count,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Count,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Degrees,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Degrees,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Documentid,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Documentid,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Endswith,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Endswith,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Does this string endswith endswith")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("endswith")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Exp,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Exp,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Floor,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Floor,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IndexOf,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IndexOf,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("banana")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("ana")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsArray,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsArray,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsBool,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsBool,
                    SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(true)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsDefined,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsDefined,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsFiniteNumber,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsFiniteNumber,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsNull,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsNull,
                    SqlLiteralScalarExpression.SqlNullLiteralScalarExpression)));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsNumber,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsNumber,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsObject,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsObject,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsPrimitive,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsPrimitive,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.IsString,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.IsString,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("hello")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Left,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Left,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Length,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Length,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Like,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Like,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("blah")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("blah")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("blah")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Log,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Log,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Log10,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Log10,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Lower,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Lower,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Ltrim,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Ltrim,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Max,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Max,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Min,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Min,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Pi,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Pi)));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Power,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Power,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Radians,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Radians,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Rand,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Rand)));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Replace,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replace,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("banana")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("ana")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("banana")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Replicate,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replicate,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("hello")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Reverse,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Reverse,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Right,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("hello")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Round,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Round,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Rtrim,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Rtrim,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Sign,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Sign,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Sin,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Sin,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Sqrt,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Sqrt,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Square,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Square,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Startswith,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Startswith,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("does this string startswith does")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("does")))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.StDistance,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.StDistance,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.StIntersects,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.StIntersects,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.StIsvalid,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.StIsvalid,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.StIsvaliddetailed,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.StIsvaliddetailed,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.StWithin,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.StWithin,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Substring,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Substring,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Sum,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Sum,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Tan,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Tan,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Trunc,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Trunc,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            inputs.Add(new SqlObjectVisitorInput(
                SqlFunctionCallScalarExpression.Names.Upper,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Upper,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlQueries()
        {
            SqlMemberIndexerScalarExpression somePath = SqlObjectBuilderUtils.CreateSqlMemberIndexerScalarExpression(
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("some")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("random")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("path")),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)));

            // SELECT
            List<SqlObjectVisitorInput> inputs = new List<SqlObjectVisitorInput>();
            foreach (bool useStar in new bool[] { true, false })
            {
                foreach (bool useTop in new bool[] { true, false })
                {
                    foreach (bool hasDistinct in new bool[] { true, false })
                    {
                        SqlSelectSpec selectSpec;
                        if (useStar)
                        {
                            selectSpec = SqlSelectStarSpec.Singleton;
                        }
                        else
                        {
                            selectSpec = SqlSelectListSpec.Create(
                                SqlSelectItem.Create(
                                    somePath,
                                    SqlIdentifier.Create("some alias")));
                        }

                        SqlTopSpec topSpec = useTop ? topSpec = SqlTopSpec.Create(
                            SqlNumberLiteral.Create(42)) : null;

                        inputs.Add(new SqlObjectVisitorInput(
                            nameof(SqlSelectClause) + $" useStar: {useStar}, useTop: {useTop}, hasDistinct: {hasDistinct}",
                            SqlSelectClause.Create(selectSpec, topSpec, hasDistinct)));
                    }
                }
            }

            // FROM
            SqlInputPathCollection sqlInputPathCollection = SqlInputPathCollection.Create(
                SqlIdentifier.Create("inputPathCollection"),
                SqlStringPathExpression.Create(
                    null,
                    SqlStringLiteral.Create("somePath")));

            SqlLiteralArrayCollection sqlLiteralArrayCollection = SqlLiteralArrayCollection.Create(
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("some")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("SqlLiteralArrayCollection")),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("items")));

            SqlSubqueryCollection sqlSubqueryCollection = SqlSubqueryCollection.Create(
                SqlQuery.Create(SqlSelectClause.SelectStar, null, null, null, null, null));

            SqlCollection[] sqlCollections = new SqlCollection[] { sqlInputPathCollection, sqlLiteralArrayCollection, sqlSubqueryCollection };
            SqlIdentifier sqlIdentifier = SqlIdentifier.Create("some alias");
            foreach (SqlCollection sqlCollection in sqlCollections)
            {
                SqlAliasedCollectionExpression sqlAliasedCollectionExpression = SqlAliasedCollectionExpression.Create(
                    sqlCollection,
                    sqlIdentifier);

                inputs.Add(new SqlObjectVisitorInput(
                    nameof(SqlAliasedCollectionExpression) + $" collectionType: {sqlCollection.GetType().Name}",
                    sqlAliasedCollectionExpression));

                SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression = SqlArrayIteratorCollectionExpression.Create(
                    sqlIdentifier,
                    sqlCollection);

                inputs.Add(new SqlObjectVisitorInput(
                    nameof(SqlArrayIteratorCollectionExpression) + $" collectionType: {sqlCollection.GetType().Name}",
                    sqlArrayIteratorCollectionExpression));

                SqlJoinCollectionExpression sqlJoinCollectionExpression = SqlJoinCollectionExpression.Create(
                    sqlAliasedCollectionExpression,
                    sqlArrayIteratorCollectionExpression);

                inputs.Add(new SqlObjectVisitorInput(
                    nameof(SqlJoinCollectionExpression) + $" collectionType: {sqlCollection.GetType().Name}",
                    sqlJoinCollectionExpression));
            }

            // WHERE
            SqlWhereClause sqlWhereClause = SqlWhereClause.Create(
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.LessThan,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("this path")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42))));
            inputs.Add(new SqlObjectVisitorInput(nameof(SqlWhereClause), sqlWhereClause));

            // GROUP BY
            inputs.Add(
                new SqlObjectVisitorInput(nameof(SqlGroupByClause) + " Single",
                SqlGroupByClause.Create(somePath)));

            inputs.Add(
               new SqlObjectVisitorInput(nameof(SqlGroupByClause) + " Multi",
               SqlGroupByClause.Create(somePath, somePath)));

            // ORDER BY
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlOrderbyClause) + " Single",
                SqlOrderbyClause.Create(
                    SqlOrderByItem.Create(somePath, false))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlOrderbyClause) + " Multi",
                SqlOrderbyClause.Create(
                    SqlOrderByItem.Create(somePath, false),
                    SqlOrderByItem.Create(somePath, true))));

            // OFFSET LIMIT
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlOffsetSpec),
                SqlOffsetSpec.Create(SqlNumberLiteral.Create(0))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlLimitSpec),
                SqlLimitSpec.Create(SqlNumberLiteral.Create(0))));

            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlOffsetLimitClause),
                SqlOffsetLimitClause.Create(
                    SqlOffsetSpec.Create(SqlNumberLiteral.Create(0)),
                    SqlLimitSpec.Create(SqlNumberLiteral.Create(0)))));

            // Query
            SqlQuery query = SqlQuery.Create(
                SqlSelectClause.SelectStar,
                SqlFromClause.Create(
                    SqlAliasedCollectionExpression.Create(
                        SqlInputPathCollection.Create(
                            SqlIdentifier.Create("inputPathCollection"),
                            SqlStringPathExpression.Create(
                                null,
                                SqlStringLiteral.Create("somePath"))),
                        SqlIdentifier.Create("some alias"))),
                sqlWhereClause,
                SqlGroupByClause.Create(somePath),
                SqlOrderbyClause.Create(
                    SqlOrderByItem.Create(somePath, false)),
                SqlOffsetLimitClause.Create(
                    SqlOffsetSpec.Create(SqlNumberLiteral.Create(0)),
                    SqlLimitSpec.Create(SqlNumberLiteral.Create(0))));
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlQuery),
                query));

            // (SUBQUERY)
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlSubqueryScalarExpression),
                SqlSubqueryScalarExpression.Create(query)));

            // ARRAY(SUBQUERY)
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlArrayScalarExpression),
                SqlArrayScalarExpression.Create(query)));

            // EXISTS(SUBQUERY)
            inputs.Add(new SqlObjectVisitorInput(
                nameof(SqlExistsScalarExpression),
                SqlExistsScalarExpression.Create(query)));

            this.ExecuteTestSuite(inputs);
        }

        public override SqlObjectVisitorOutput ExecuteTest(SqlObjectVisitorInput input)
        {
            SqlObjectVisitorOutput output;
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                // Add a culture info just to make sure that we are writing the same as culture invariant.
                CultureInfo.CurrentCulture = new CultureInfo("nl-BE");

                string textOutput = input.SqlObject.ToString();
                string prettyPrint = input.SqlObject.PrettyPrint();
                string obfuscatedQuery = input.SqlObject.GetObfuscatedObject().ToString();
                int hashCode = input.SqlObject.GetHashCode();

                output = new SqlObjectVisitorOutput(textOutput, prettyPrint, obfuscatedQuery, hashCode);

                string textOutputWithoutWhitespace = Regex.Replace(textOutput, @"\s+", "");
                string prettyPrintWithoutWhitespace = Regex.Replace(prettyPrint, @"\s+", "");
                Assert.AreEqual(textOutputWithoutWhitespace, prettyPrintWithoutWhitespace);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }

            return output;
        }
    }

    public sealed class SqlObjectVisitorInput : BaselineTestInput
    {
        internal SqlObject SqlObject { get; }

        internal SqlObjectVisitorInput(
            string description,
            SqlObject sqlObject)
            : base(description)
        {
            if (sqlObject == null)
            {
                throw new ArgumentNullException($"{nameof(sqlObject)} must not be null.");
            }

            this.SqlObject = sqlObject;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (xmlWriter == null)
            {
                throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
            }

            xmlWriter.WriteElementString("Description", this.Description);
            xmlWriter.WriteStartElement("SqlObject");
            xmlWriter.WriteCData(JsonConvert.SerializeObject(this.SqlObject, Newtonsoft.Json.Formatting.Indented));
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class SqlObjectVisitorOutput : BaselineTestOutput
    {
        public SqlObjectVisitorOutput(
            string textOutput,
            string prettyPrint,
            string obfuscatedQuery,
            int hashCode)
        {
            if (textOutput == null)
            {
                throw new ArgumentNullException(nameof(textOutput));
            }

            if (prettyPrint == null)
            {
                throw new ArgumentNullException(nameof(prettyPrint));
            }

            if (obfuscatedQuery == null)
            {
                throw new ArgumentNullException(nameof(obfuscatedQuery));
            }

            this.TextOutput = textOutput;
            this.PrettyPrint = prettyPrint;
            this.HashCode = hashCode;
            this.ObfusctedQuery = obfuscatedQuery;
        }

        public string TextOutput { get; }

        public string PrettyPrint { get; }

        public int HashCode { get; }

        public string ObfusctedQuery { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement($"{nameof(this.TextOutput)}");
            xmlWriter.WriteCData(this.TextOutput);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement($"{nameof(this.PrettyPrint)}");
            xmlWriter.WriteCData(Environment.NewLine + this.PrettyPrint + Environment.NewLine);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement($"{nameof(this.HashCode)}");
            xmlWriter.WriteValue(this.HashCode);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement($"{nameof(this.ObfusctedQuery)}");
            xmlWriter.WriteCData(this.ObfusctedQuery);
            xmlWriter.WriteEndElement();
        }
    }
}