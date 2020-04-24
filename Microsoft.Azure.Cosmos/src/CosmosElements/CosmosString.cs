﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosString : CosmosElement
    {
        protected CosmosString()
            : base(CosmosElementType.String)
        {
        }

        public abstract string Value { get; }

        public abstract bool TryGetBufferedUtf8Value(out ReadOnlyMemory<byte> bufferedUtf8Value);

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this, input);
        }

        public static CosmosString Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosString(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosString Create(string value)
        {
            return new EagerCosmosString(value);
        }

        public static new CosmosString CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosString>(buffer);
        }

        public static new CosmosString Parse(string json)
        {
            return CosmosElement.Parse<CosmosString>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosString cosmosString)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosString>(buffer, out cosmosString);
        }

        public static bool TryParse(string json, out CosmosString cosmosString)
        {
            return CosmosElement.TryParse<CosmosString>(json, out cosmosString);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosString> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosString>(buffer);
            }

            public static TryCatch<CosmosString> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosString>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
