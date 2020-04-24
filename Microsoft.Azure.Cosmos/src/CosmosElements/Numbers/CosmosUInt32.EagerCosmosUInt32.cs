//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 107

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosUInt32 : CosmosNumber
    {
        private sealed class EagerCosmosUInt32 : CosmosUInt32
        {
            private readonly uint number;

            public EagerCosmosUInt32(uint number)
            {
                this.number = number;
            }

            public override uint GetValue()
            {
                return this.number;
            }
        }
    }
}
