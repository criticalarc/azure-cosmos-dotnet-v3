//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This file isn't generated, but this comment is necessary to exclude it from StyleCop analysis.
// <auto-generated/>

using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <summary>
    /// Provides the base class for an encoding provider serializer, which supplies encodings that may be unavailable on a particular platform.
    /// </summary>
    /// <remarks>
    /// For .NET Core, we need to register the <see cref="CodePagesEncodingProvider"/> before attempting to get an Encoding from a CodePage
    /// For a default installation of SqlServer the encoding exchanged during Login is 1252. This encoding is not loaded by default.
    /// See Remarks at https://msdn.microsoft.com/en-us/library/system.text.encodingprovider(v=vs.110).aspx.
    /// </remarks>
    internal abstract class SqlCodePageEncodingSerializer : Serializer<string>
    {
        /// <summary>
        /// Static constructor is called at most one time, before any
        /// instance constructor is invoked or member is accessed.
        /// </summary>
        static SqlCodePageEncodingSerializer()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}