﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class SqlObjectTextSerializer : SqlObjectVisitor
    {
        // Mongo's query translation tests do not use baseline files,
        // so changing whitespaces involve manually updating the expected output for each test.
        // When the tests are converted over to baseline files we can just bulk update them and remove this flag.
        private const bool MongoDoesNotUseBaselineFiles = true;
        private static readonly string Tab = "    ";
        private readonly StringWriter writer;
        private readonly bool prettyPrint;
        private int indentLevel;

        public SqlObjectTextSerializer(bool prettyPrint)
        {
            this.writer = new StringWriter(CultureInfo.InvariantCulture);
            this.prettyPrint = prettyPrint;
        }

        public override void Visit(SqlAliasedCollectionExpression sqlAliasedCollectionExpression)
        {
            sqlAliasedCollectionExpression.Collection.Accept(this);
            if (sqlAliasedCollectionExpression.Alias != null)
            {
                this.writer.Write(" AS ");
                sqlAliasedCollectionExpression.Alias.Accept(this);
            }
        }

        public override void Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
        {
            int numberOfItems = sqlArrayCreateScalarExpression.Items.Count();
            if (numberOfItems == 0)
            {
                this.writer.Write("[]");
            }
            else if (numberOfItems == 1)
            {
                this.writer.Write("[");
                sqlArrayCreateScalarExpression.Items[0].Accept(this);
                this.writer.Write("]");
            }
            else
            {
                this.WriteStartContext("[");

                for (int i = 0; i < sqlArrayCreateScalarExpression.Items.Count; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlArrayCreateScalarExpression.Items[i].Accept(this);
                }

                this.WriteEndContext("]");
            }
        }

        public override void Visit(SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
        {
            sqlArrayIteratorCollectionExpression.Alias.Accept(this);
            this.writer.Write(" IN ");
            sqlArrayIteratorCollectionExpression.Collection.Accept(this);
        }

        public override void Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
        {
            this.writer.Write("ARRAY");
            this.WriteStartContext("(");
            sqlArrayScalarExpression.SqlQuery.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
        {
            this.writer.Write("(");
            sqlBetweenScalarExpression.Expression.Accept(this);

            if (sqlBetweenScalarExpression.IsNot)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" BETWEEN ");
            sqlBetweenScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" AND ");
            sqlBetweenScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
        {
            this.writer.Write("(");
            sqlBinaryScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" ");
            this.writer.Write(SqlObjectTextSerializer.SqlBinaryScalarOperatorKindToString(sqlBinaryScalarExpression.OperatorKind));
            this.writer.Write(" ");
            sqlBinaryScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlBooleanLiteral sqlBooleanLiteral)
        {
            this.writer.Write(sqlBooleanLiteral.Value ? "true" : "false");
        }

        public override void Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
        {
            this.writer.Write("(");
            sqlCoalesceScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" ?? ");
            sqlCoalesceScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
        {
            this.writer.Write('(');
            sqlConditionalScalarExpression.ConditionExpression.Accept(this);
            this.writer.Write(" ? ");
            sqlConditionalScalarExpression.FirstExpression.Accept(this);
            this.writer.Write(" : ");
            sqlConditionalScalarExpression.SecondExpression.Accept(this);
            this.writer.Write(')');
        }

        public override void Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
        {
            this.writer.Write("EXISTS");
            this.WriteStartContext("(");
            sqlExistsScalarExpression.SqlQuery.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlFromClause sqlFromClause)
        {
            this.writer.Write("FROM ");
            sqlFromClause.Expression.Accept(this);
        }

        public override void Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
        {
            if (sqlFunctionCallScalarExpression.IsUdf)
            {
                this.writer.Write("udf.");
            }

            sqlFunctionCallScalarExpression.Name.Accept(this);
            int numberOfArguments = sqlFunctionCallScalarExpression.Arguments.Count();
            if (numberOfArguments == 0)
            {
                this.writer.Write("()");
            }
            else if (numberOfArguments == 1)
            {
                this.writer.Write("(");
                sqlFunctionCallScalarExpression.Arguments[0].Accept(this);
                this.writer.Write(")");
            }
            else
            {
                this.WriteStartContext("(");

                for (int i = 0; i < sqlFunctionCallScalarExpression.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlFunctionCallScalarExpression.Arguments[i].Accept(this);
                }

                this.WriteEndContext(")");
            }
        }

        public override void Visit(SqlGroupByClause sqlGroupByClause)
        {
            this.writer.Write("GROUP BY ");
            sqlGroupByClause.Expressions[0].Accept(this);
            for (int i = 1; i < sqlGroupByClause.Expressions.Count; i++)
            {
                this.writer.Write(", ");
                sqlGroupByClause.Expressions[i].Accept(this);
            }
        }

        public override void Visit(SqlIdentifier sqlIdentifier)
        {
            this.writer.Write(sqlIdentifier.Value);
        }

        public override void Visit(SqlIdentifierPathExpression sqlIdentifierPathExpression)
        {
            if (sqlIdentifierPathExpression.ParentPath != null)
            {
                sqlIdentifierPathExpression.ParentPath.Accept(this);
                this.writer.Write(".");
            }

            sqlIdentifierPathExpression.Value.Accept(this);
        }

        public override void Visit(SqlInputPathCollection sqlInputPathCollection)
        {
            sqlInputPathCollection.Input.Accept(this);
            if (sqlInputPathCollection.RelativePath != null)
            {
                sqlInputPathCollection.RelativePath.Accept(this);
            }
        }

        public override void Visit(SqlInScalarExpression sqlInScalarExpression)
        {
            this.writer.Write("(");
            sqlInScalarExpression.Expression.Accept(this);
            if (sqlInScalarExpression.Not)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" IN ");

            int numberOfItems = sqlInScalarExpression.Items.Count();
            if (numberOfItems == 0)
            {
                this.writer.Write("()");
            }
            else if (numberOfItems == 1)
            {
                this.writer.Write("(");
                sqlInScalarExpression.Items[0].Accept(this);
                this.writer.Write(")");
            }
            else
            {
                this.WriteStartContext("(");

                for (int i = 0; i < sqlInScalarExpression.Items.Count; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlInScalarExpression.Items[i].Accept(this);
                }

                this.WriteEndContext(")");
            }
            this.writer.Write(")");
        }

        public override void Visit(SqlJoinCollectionExpression sqlJoinCollectionExpression)
        {
            sqlJoinCollectionExpression.LeftExpression.Accept(this);
            this.WriteNewline();
            this.WriteTab();
            this.writer.Write(" JOIN ");
            sqlJoinCollectionExpression.RightExpression.Accept(this);
        }

        public override void Visit(SqlLimitSpec sqlObject)
        {
            this.writer.Write("LIMIT ");
            sqlObject.LimitExpression.Accept(this);
        }

        public override void Visit(SqlLiteralArrayCollection sqlLiteralArrayCollection)
        {
            this.writer.Write("[");

            for (int i = 0; i < sqlLiteralArrayCollection.Items.Count; i++)
            {
                if (i > 0)
                {
                    this.writer.Write(", ");
                }

                sqlLiteralArrayCollection.Items[i].Accept(this);
            }

            this.writer.Write("]");
        }

        public override void Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
        {
            sqlLiteralScalarExpression.Literal.Accept(this);
        }

        public override void Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
        {
            sqlMemberIndexerScalarExpression.MemberExpression.Accept(this);
            this.writer.Write("[");
            sqlMemberIndexerScalarExpression.IndexExpression.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlNullLiteral sqlNullLiteral)
        {
            this.writer.Write("null");
        }

        public override void Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            // We have to use InvariantCulture due to number formatting.
            // "1234.1234" is correct while "1234,1234" is incorrect.
            if (sqlNumberLiteral.Value.IsDouble)
            {
                string literalString = sqlNumberLiteral.Value.ToString(CultureInfo.InvariantCulture);
                double literalValue = 0.0;
                if (!sqlNumberLiteral.Value.IsNaN &&
                    !sqlNumberLiteral.Value.IsInfinity &&
                    (!double.TryParse(literalString, NumberStyles.Number, CultureInfo.InvariantCulture, out literalValue) ||
                    !Number64.ToDouble(sqlNumberLiteral.Value).Equals(literalValue)))
                {
                    literalString = sqlNumberLiteral.Value.ToString("G17", CultureInfo.InvariantCulture);
                }

                this.writer.Write(literalString);
            }
            else
            {
                this.writer.Write(sqlNumberLiteral.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public override void Visit(SqlNumberPathExpression sqlNumberPathExpression)
        {
            if (sqlNumberPathExpression.ParentPath != null)
            {
                sqlNumberPathExpression.ParentPath.Accept(this);
            }

            this.writer.Write("[");
            sqlNumberPathExpression.Value.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
        {
            int numberOfProperties = sqlObjectCreateScalarExpression.Properties.Count();
            if (numberOfProperties == 0)
            {
                this.writer.Write("{}");
            }
            else if (numberOfProperties == 1)
            {
                this.writer.Write("{");
                sqlObjectCreateScalarExpression.Properties.First().Accept(this);
                this.writer.Write("}");
            }
            else
            {
                this.WriteStartContext("{");
                bool firstItemProcessed = false;

                foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
                {
                    if (firstItemProcessed)
                    {
                        this.WriteDelimiter(",");
                    }

                    property.Accept(this);
                    firstItemProcessed = true;
                }

                this.WriteEndContext("}");
            }
        }

        public override void Visit(SqlObjectProperty sqlObjectProperty)
        {
            sqlObjectProperty.Name.Accept(this);
            this.writer.Write(": ");
            sqlObjectProperty.Expression.Accept(this);
        }

        public override void Visit(SqlOffsetLimitClause sqlObject)
        {
            sqlObject.OffsetSpec.Accept(this);
            this.writer.Write(" ");
            sqlObject.LimitSpec.Accept(this);
        }

        public override void Visit(SqlOffsetSpec sqlObject)
        {
            this.writer.Write("OFFSET ");
            sqlObject.OffsetExpression.Accept(this);
        }

        public override void Visit(SqlOrderbyClause sqlOrderByClause)
        {
            this.writer.Write("ORDER BY ");
            sqlOrderByClause.OrderbyItems[0].Accept(this);

            for (int i = 1; i < sqlOrderByClause.OrderbyItems.Count; i++)
            {
                this.writer.Write(", ");
                sqlOrderByClause.OrderbyItems[i].Accept(this);
            }
        }

        public override void Visit(SqlOrderByItem sqlOrderByItem)
        {
            sqlOrderByItem.Expression.Accept(this);
            if (sqlOrderByItem.IsDescending)
            {
                this.writer.Write(" DESC");
            }
            else
            {
                this.writer.Write(" ASC");
            }
        }

        public override void Visit(SqlParameter sqlParameter)
        {
            this.writer.Write(sqlParameter.Name);
        }

        public override void Visit(SqlParameterRefScalarExpression sqlParameterRefScalarExpression)
        {
            sqlParameterRefScalarExpression.Parameter.Accept(this);
        }

        public override void Visit(SqlProgram sqlProgram)
        {
            sqlProgram.Query.Accept(this);
        }

        public override void Visit(SqlPropertyName sqlPropertyName)
        {
            this.writer.Write('"');
            this.writer.Write(sqlPropertyName.Value);
            this.writer.Write('"');
        }

        public override void Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
        {
            if (sqlPropertyRefScalarExpression.MemberExpression != null)
            {
                sqlPropertyRefScalarExpression.MemberExpression.Accept(this);
                this.writer.Write(".");
            }

            sqlPropertyRefScalarExpression.PropertyIdentifier.Accept(this);
        }

        public override void Visit(SqlQuery sqlQuery)
        {
            sqlQuery.SelectClause.Accept(this);

            if (sqlQuery.FromClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.FromClause.Accept(this);
            }

            if (sqlQuery.WhereClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.WhereClause.Accept(this);
            }

            if (sqlQuery.GroupByClause != null)
            {
                sqlQuery.GroupByClause.Accept(this);
                this.writer.Write(" ");
            }

            if (sqlQuery.OrderbyClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.OrderbyClause.Accept(this);
            }

            if (sqlQuery.OffsetLimitClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.OffsetLimitClause.Accept(this);
            }

            if (MongoDoesNotUseBaselineFiles)
            {
                this.writer.Write(" ");
            }
        }

        public override void Visit(SqlSelectClause sqlSelectClause)
        {
            this.writer.Write("SELECT ");

            if (sqlSelectClause.HasDistinct)
            {
                this.writer.Write("DISTINCT ");
            }

            if (sqlSelectClause.TopSpec != null)
            {
                sqlSelectClause.TopSpec.Accept(this);
                this.writer.Write(" ");
            }

            sqlSelectClause.SelectSpec.Accept(this);
        }

        public override void Visit(SqlSelectItem sqlSelectItem)
        {
            sqlSelectItem.Expression.Accept(this);
            if (sqlSelectItem.Alias != null)
            {
                this.writer.Write(" AS ");
                sqlSelectItem.Alias.Accept(this);
            }
        }

        public override void Visit(SqlSelectListSpec sqlSelectListSpec)
        {
            int numberOfSelectSpecs = sqlSelectListSpec.Items.Count();
            if (numberOfSelectSpecs == 0)
            {
                throw new ArgumentException($"Expected {nameof(sqlSelectListSpec)} to have atleast 1 item.");
            }
            else if (numberOfSelectSpecs == 1)
            {
                sqlSelectListSpec.Items[0].Accept(this);
            }
            else
            {
                bool processedFirstItem = false;
                this.indentLevel++;
                this.WriteNewline();
                this.WriteTab();

                foreach (SqlSelectItem item in sqlSelectListSpec.Items)
                {
                    if (processedFirstItem)
                    {
                        this.WriteDelimiter(",");
                    }

                    item.Accept(this);
                    processedFirstItem = true;
                }

                this.indentLevel--;
            }
        }

        public override void Visit(SqlSelectStarSpec sqlSelectStarSpec)
        {
            this.writer.Write("*");
        }

        public override void Visit(SqlSelectValueSpec sqlSelectValueSpec)
        {
            this.writer.Write("VALUE ");
            sqlSelectValueSpec.Expression.Accept(this);
        }

        public override void Visit(SqlStringLiteral sqlStringLiteral)
        {
            this.writer.Write("\"");
            this.writer.Write(SqlObjectTextSerializer.GetEscapedString(sqlStringLiteral.Value));
            this.writer.Write("\"");
        }

        public override void Visit(SqlStringPathExpression sqlStringPathExpression)
        {
            if (sqlStringPathExpression.ParentPath != null)
            {
                sqlStringPathExpression.ParentPath.Accept(this);
            }

            this.writer.Write("[");
            sqlStringPathExpression.Value.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlSubqueryCollection sqlSubqueryCollection)
        {
            this.WriteStartContext("(");
            sqlSubqueryCollection.Query.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
        {
            this.WriteStartContext("(");
            sqlSubqueryScalarExpression.Query.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlTagsMatchExpression sqlObject)
        {
            const char nullOperator = '\0';
            const char requiredOperator = '*';
            const char notOperator = '!';
            
            (char Operator, string Namespace, string Name, string Value, string Tag, bool IsNot, bool IsRequired, bool IsWildcard) Parse(string tag)
            {
                var indexOfColon = tag.IndexOf(':');
                var indexOfEquals = tag.IndexOf('=');

                if (indexOfColon < 1 || indexOfEquals < 3 || indexOfColon > indexOfEquals)
                    throw new ArgumentException("Tag is not a machine tag");

                var op = tag[0] == notOperator || tag[0] == requiredOperator ? tag[0] : nullOperator;
                var ns = tag.Substring(op == nullOperator ? 0 : 1, op == nullOperator ? indexOfColon : indexOfColon - 1);
                var name = tag.Substring(indexOfColon + 1, indexOfEquals - (indexOfColon + 1));
                var value = tag.Substring(indexOfEquals + 1);

                return (op, ns, name, value, tag, op == '!', op == '*', value.Length == 0);
            }

            this.writer.Write("(");

            var supportDocumentRequiredTags = sqlObject.SupportDocumentRequiredTags;
            var tags = sqlObject.Tags;
            var tagsProp = sqlObject.TagsProperty;
            var tagProp = $"{tagsProp}[\"tag\"]";

            if (tags.Any())
            {
                var machineTags = tags.Select(x => Parse(x));
                var tagsByGroup = machineTags.GroupBy(x => x.Namespace + ":" + x.Name);
                foreach (var grouping in tagsByGroup)
                {
                    var tagName = grouping.Key;
                    var regulars = grouping.Where(x => x.Operator == '\0');
                    var nots = grouping.Where(x => x.IsNot);
                    var requireds = grouping.Where(x => x.IsRequired);
                    var wildcardTag = grouping.Key + "=";
                    var regularProp = $"{tagsProp}[\"tags\"][\"{tagName}\"]";
                    var notProp = $"{tagsProp}[\"tags\"][\"!{tagName}\"]";

                    if (nots.Any())
                    {
                        if (nots.Any(x => x.IsWildcard))
                        {
                            this.writer.Write($"NOT(IS_DEFINED({regularProp}))");
                        }
                        else if (nots.Any(x => !x.IsWildcard))
                        {
                            this.writer.Write($"NOT(ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\"))");
                            foreach (var not in nots)
                                this.writer.Write($" AND NOT(ARRAY_CONTAINS({tagProp}, \"{not.Tag.Substring(1)}\"))");
                        }
                    }
                    if (regulars.Any())
                    {
                        if (nots.Any())
                            this.writer.Write(" AND ");

                        if (regulars.Any(x => x.IsWildcard))
                        {
                            this.writer.Write($"NOT(IS_DEFINED({notProp}))");
                        }
                        else if (regulars.Any(x => !x.IsWildcard))
                        {
                            this.writer.Write($"(NOT(IS_DEFINED({notProp})) OR NOT(ARRAY_CONTAINS({tagProp}, \"!{wildcardTag}\"))");
                            foreach (var regular in regulars)
                                this.writer.Write($" AND NOT(ARRAY_CONTAINS({tagProp}, \"!{regular.Tag}\"))");
                            this.writer.Write($") AND (NOT(IS_DEFINED({regularProp})) OR ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\")");
                            foreach (var regular in regulars)
                                this.writer.Write($" OR ARRAY_CONTAINS({tagProp}, \"{regular.Tag}\")");
                            this.writer.Write(")");
                        }
                    }
                    if (requireds.Any())
                    {
                        if (nots.Any() || regulars.Any())
                            this.writer.Write(" AND ");
                        this.writer.Write($"ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\") OR (");
                        foreach (var (value, index) in requireds.Select((v, i) => (v, i)))
                        {
                            if (index > 0)
                                this.writer.Write(" AND ");
                            this.writer.Write($"ARRAY_CONTAINS({tagProp}, \"{value.Tag.Substring(1)}\")");
                        }
                        this.writer.Write(")");
                    }

                    this.writer.Write(" AND ");
                }
            }

            if (supportDocumentRequiredTags)
            {
                var tagsExpression = $"[{string.Join(",", tags.Select(x => $"\"{x}\""))}]";
                this.writer.Write($"udf.TagsMatch({tagProp}, {tagsExpression})");
            }
            else
            {
                this.writer.Write("1 = 1");
            }

            this.writer.Write(")");
        }

        public override void Visit(SqlTopSpec sqlTopSpec)
        {
            this.writer.Write("TOP ");
            sqlTopSpec.TopExpresion.Accept(this);
        }

        public override void Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
        {
            this.writer.Write("(");
            this.writer.Write(SqlObjectTextSerializer.SqlUnaryScalarOperatorKindToString(sqlUnaryScalarExpression.OperatorKind));
            this.writer.Write(" ");
            sqlUnaryScalarExpression.Expression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlUndefinedLiteral sqlUndefinedLiteral)
        {
            this.writer.Write("undefined");
        }

        public override void Visit(SqlWhereClause sqlWhereClause)
        {
            this.writer.Write("WHERE ");
            sqlWhereClause.FilterExpression.Accept(this);
        }

        public override string ToString()
        {
            return this.writer.ToString();
        }

        private void WriteStartContext(string startCharacter)
        {
            this.indentLevel++;
            this.writer.Write(startCharacter);
            this.WriteNewline();
            this.WriteTab();
        }

        private void WriteDelimiter(string delimiter)
        {
            this.writer.Write(delimiter);
            this.writer.Write(' ');
            this.WriteNewline();
            this.WriteTab();
        }

        private void WriteEndContext(string endCharacter)
        {
            this.indentLevel--;
            this.WriteNewline();
            this.WriteTab();
            this.writer.Write(endCharacter);
        }

        private void WriteNewline()
        {
            if (this.prettyPrint)
            {
                this.writer.WriteLine();
            }
        }

        private void WriteTab()
        {
            if (this.prettyPrint)
            {
                for (int i = 0; i < this.indentLevel; i++)
                {
                    this.writer.Write(Tab);
                }
            }
        }

        private static string GetEscapedString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (value.All(c => !IsEscapedCharacter(c)))
            {
                return value;
            }

            StringBuilder stringBuilder = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        stringBuilder.Append("\\\"");
                        break;
                    case '\\':
                        stringBuilder.Append("\\\\");
                        break;
                    case '\b':
                        stringBuilder.Append("\\b");
                        break;
                    case '\f':
                        stringBuilder.Append("\\f");
                        break;
                    case '\n':
                        stringBuilder.Append("\\n");
                        break;
                    case '\r':
                        stringBuilder.Append("\\r");
                        break;
                    case '\t':
                        stringBuilder.Append("\\t");
                        break;
                    default:
                        switch (CharUnicodeInfo.GetUnicodeCategory(c))
                        {
                            case UnicodeCategory.UppercaseLetter:
                            case UnicodeCategory.LowercaseLetter:
                            case UnicodeCategory.TitlecaseLetter:
                            case UnicodeCategory.OtherLetter:
                            case UnicodeCategory.DecimalDigitNumber:
                            case UnicodeCategory.LetterNumber:
                            case UnicodeCategory.OtherNumber:
                            case UnicodeCategory.SpaceSeparator:
                            case UnicodeCategory.ConnectorPunctuation:
                            case UnicodeCategory.DashPunctuation:
                            case UnicodeCategory.OpenPunctuation:
                            case UnicodeCategory.ClosePunctuation:
                            case UnicodeCategory.InitialQuotePunctuation:
                            case UnicodeCategory.FinalQuotePunctuation:
                            case UnicodeCategory.OtherPunctuation:
                            case UnicodeCategory.MathSymbol:
                            case UnicodeCategory.CurrencySymbol:
                            case UnicodeCategory.ModifierSymbol:
                            case UnicodeCategory.OtherSymbol:
                                stringBuilder.Append(c);
                                break;
                            default:
                                stringBuilder.AppendFormat("\\u{0:x4}", (int)c);
                                break;
                        }
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        private static bool IsEscapedCharacter(char c)
        {
            switch (c)
            {
                case '"':
                case '\\':
                case '\b':
                case '\f':
                case '\n':
                case '\r':
                case '\t':
                    return true;

                default:
                    switch (CharUnicodeInfo.GetUnicodeCategory(c))
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                        case UnicodeCategory.LetterNumber:
                        case UnicodeCategory.OtherNumber:
                        case UnicodeCategory.SpaceSeparator:
                        case UnicodeCategory.ConnectorPunctuation:
                        case UnicodeCategory.DashPunctuation:
                        case UnicodeCategory.OpenPunctuation:
                        case UnicodeCategory.ClosePunctuation:
                        case UnicodeCategory.InitialQuotePunctuation:
                        case UnicodeCategory.FinalQuotePunctuation:
                        case UnicodeCategory.OtherPunctuation:
                        case UnicodeCategory.MathSymbol:
                        case UnicodeCategory.CurrencySymbol:
                        case UnicodeCategory.ModifierSymbol:
                        case UnicodeCategory.OtherSymbol:
                            return false;

                        default:
                            return true;
                    }
            }
        }

        private static string SqlUnaryScalarOperatorKindToString(SqlUnaryScalarOperatorKind kind)
        {
            switch (kind)
            {
                case SqlUnaryScalarOperatorKind.BitwiseNot:
                    return "~";
                case SqlUnaryScalarOperatorKind.Not:
                    return "NOT";
                case SqlUnaryScalarOperatorKind.Minus:
                    return "-";
                case SqlUnaryScalarOperatorKind.Plus:
                    return "+";
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }

        private static string SqlBinaryScalarOperatorKindToString(SqlBinaryScalarOperatorKind kind)
        {
            switch (kind)
            {
                case SqlBinaryScalarOperatorKind.Add:
                    return "+";
                case SqlBinaryScalarOperatorKind.And:
                    return "AND";
                case SqlBinaryScalarOperatorKind.BitwiseAnd:
                    return "&";
                case SqlBinaryScalarOperatorKind.BitwiseOr:
                    return "|";
                case SqlBinaryScalarOperatorKind.BitwiseXor:
                    return "^";
                case SqlBinaryScalarOperatorKind.Coalesce:
                    return "??";
                case SqlBinaryScalarOperatorKind.Divide:
                    return "/";
                case SqlBinaryScalarOperatorKind.Equal:
                    return "=";
                case SqlBinaryScalarOperatorKind.GreaterThan:
                    return ">";
                case SqlBinaryScalarOperatorKind.GreaterThanOrEqual:
                    return ">=";
                case SqlBinaryScalarOperatorKind.LessThan:
                    return "<";
                case SqlBinaryScalarOperatorKind.LessThanOrEqual:
                    return "<=";
                case SqlBinaryScalarOperatorKind.Modulo:
                    return "%";
                case SqlBinaryScalarOperatorKind.Multiply:
                    return "*";
                case SqlBinaryScalarOperatorKind.NotEqual:
                    return "!=";
                case SqlBinaryScalarOperatorKind.Or:
                    return "OR";
                case SqlBinaryScalarOperatorKind.StringConcat:
                    return "||";
                case SqlBinaryScalarOperatorKind.Subtract:
                    return "-";
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }
    }
}
