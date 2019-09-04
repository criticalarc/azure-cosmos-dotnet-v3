﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Security;

    internal interface IComputeHash : IDisposable
    {
        byte[] ComputeHash(byte[] bytesToHash);

        SecureString Key
        {
            get;
        }
    }
}
