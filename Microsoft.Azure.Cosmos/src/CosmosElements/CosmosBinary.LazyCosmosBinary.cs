﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosBinary : CosmosElement
    {
        private sealed class LazyCosmosBinary : CosmosBinary
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<IReadOnlyList<byte>> lazyBytes;

            public LazyCosmosBinary(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
            {
                if (jsonNavigator == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigator)}");
                }

                if (jsonNavigatorNode == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigatorNode)}");
                }

                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Binary)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Binary} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyBytes = new Lazy<IReadOnlyList<byte>>(() =>
                {
                    return this.jsonNavigator.GetBinaryValue(this.jsonNavigatorNode);
                });
            }

            public override IReadOnlyList<byte> Value
            {
                get
                {
                    return this.lazyBytes.Value;
                }
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteJsonNode(this.jsonNavigator, this.jsonNavigatorNode);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
