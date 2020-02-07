﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents the diagnostics interface used in the SDK.
    /// </summary>
    internal abstract class CosmosDiagnosticsContext : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        public abstract DateTime StartUtc { get; }

        public abstract int TotalRequestCount { get; protected set; }

        public abstract int FailedRequestCount { get; protected set; }

        public abstract TimeSpan? TotalElapsedTime { get; protected set; }

        public abstract string UserAgent { get; protected set; }

        internal abstract CosmosDiagnosticScope CreateOverallScope(string name);

        internal abstract CosmosDiagnosticScope CreateScope(string name);

        internal abstract void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics);

        internal abstract void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics);

        internal abstract void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext);

        internal abstract void SetSdkUserAgent(string userAgent);

        public abstract IEnumerator<CosmosDiagnosticsInternal> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static CosmosDiagnosticsContext Create(RequestOptions requestOptions)
        {
            return requestOptions?.DiagnosticContext ?? CosmosDiagnosticsContext.Create();
        }

        internal static CosmosDiagnosticsContext Create()
        {
            return new CosmosDiagnosticsContextCore();
        }
    }
}