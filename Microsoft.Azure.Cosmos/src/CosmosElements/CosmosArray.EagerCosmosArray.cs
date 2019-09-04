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
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        private sealed class EagerCosmosArray : CosmosArray
        {
            private readonly List<CosmosElement> cosmosElements;

            public EagerCosmosArray(IEnumerable<CosmosElement> elements)
            {
                if (elements == null)
                {
                    throw new ArgumentNullException($"{nameof(elements)}");
                }

                foreach (CosmosElement element in elements)
                {
                    if (element == null)
                    {
                        throw new ArgumentException($"{nameof(elements)} must not have null items.");
                    }
                }

                this.cosmosElements = new List<CosmosElement>(elements);
            }

            public override int Count => this.cosmosElements.Count;

            public override CosmosElement this[int index]
            {
                get
                {
                    return this.cosmosElements[index];
                }
            }

            public override IEnumerator<CosmosElement> GetEnumerator() => this.cosmosElements.GetEnumerator();

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteArrayStart();

                foreach (CosmosElement arrayItem in this)
                {
                    arrayItem.WriteTo(jsonWriter);
                }

                jsonWriter.WriteArrayEnd();
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}