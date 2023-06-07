﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry;
    using OpenTelemetry.Trace;

    /// <summary>
    /// It is a custom exporter for OpenTelemetry. It is used to validate the Activities generated by cosmosDb SDK.
    /// As of now, it doesn not capture Events from event Source.
    /// </summary>
    internal class CustomOtelExporter : BaseExporter<Activity>
    {
        private readonly string _name;

        public static List<Activity> CollectedActivities;
        
        public CustomOtelExporter(string name = "CustomOtelExporter")
        {
            this._name = name;
            CollectedActivities = new List<Activity>();
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            // SuppressInstrumentationScope should be used to prevent exporter
            // code from generating telemetry and causing live-loop.
            using IDisposable scope = SuppressInstrumentationScope.Begin();

            foreach (Activity activity in batch)
            {
                AssertActivity.IsValidOperationActivity(activity);
                
                CollectedActivities.Add(activity);
            }

            return ExportResult.Success;
        }
    }

    internal static class OTelExtensions
    {
        public static TracerProviderBuilder AddCustomOtelExporter(this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddProcessor(new SimpleActivityExportProcessor(new CustomOtelExporter()));
        }
    }
}
