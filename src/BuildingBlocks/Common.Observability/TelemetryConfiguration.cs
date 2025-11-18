namespace Common.Observability
{
    /// <summary>
    /// OpenTelemetry configuration settings
    /// </summary>
    public class TelemetryConfiguration
    {
        public const string SectionName = "Telemetry";

        /// <summary>
        /// The name of this microservice (e.g., "Catalog.API")
        /// </summary>
        public string ServiceName { get; set; } = "Unknown";

        /// <summary>
        /// The version of this microservice (e.g., "1.0.0")
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// OTLP endpoint for exporting traces (e.g., "http://jaeger:4317")
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";

        /// <summary>
        /// Protocol for OTLP export (Grpc or HttpProtobuf)
        /// </summary>
        public string Protocol { get; set; } = "Grpc";

        /// <summary>
        /// Whether distributed tracing is enabled
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Whether metrics collection is enabled
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Sampling probability (0.0 to 1.0, where 1.0 = all traces)
        /// </summary>
        public double SamplingProbability { get; set; } = 1.0;
    }
}

