﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class TypeCheckFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> TypeCheckFunctionsDefinitions { get; set; }

        static TypeCheckFunctions()
        {
            TypeCheckFunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>();

            TypeCheckFunctionsDefinitions.Add("IsDefined",
                new SqlBuiltinFunctionVisitor("IS_DEFINED",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    }));

            TypeCheckFunctionsDefinitions.Add("IsNull",
                new SqlBuiltinFunctionVisitor("IS_NULL",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    }));

            TypeCheckFunctionsDefinitions.Add("IsPrimitive",
                new SqlBuiltinFunctionVisitor("IS_PRIMITIVE",
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    }));
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            BuiltinFunctionVisitor visitor = null;
            if (TypeCheckFunctionsDefinitions.TryGetValue(methodCallExpression.Method.Name, out visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
