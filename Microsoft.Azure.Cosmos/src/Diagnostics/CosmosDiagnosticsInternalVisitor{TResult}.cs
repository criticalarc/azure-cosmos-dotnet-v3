﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    internal abstract class CosmosDiagnosticsInternalVisitor<TResult>
    {
        public abstract TResult Visit(PointOperationStatistics pointOperationStatistics);
        public abstract TResult Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext);
        public abstract TResult Visit(CosmosDiagnosticScope cosmosDiagnosticScope);
        public abstract TResult Visit(QueryPageDiagnostics queryPageDiagnostics);
        public abstract TResult Visit(AddressResolutionStatistics addressResolutionStatistics);
        public abstract TResult Visit(StoreResponseStatistics storeResponseStatistics);
        public abstract TResult Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics);
    }
}
