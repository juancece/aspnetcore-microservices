using System.ComponentModel.DataAnnotations;

namespace Common.Observability
{
    /// <summary>
    /// OpenTelemetry configuration settings with built-in validation.
    /// Use IOptions<TelemetryConfiguration> with ValidateDataAnnotations for startup validation.
    /// </summary>
    public class TelemetryConfiguration : IValidatableObject
    {
        public const string SectionName = "Telemetry";

        /// <summary>
        /// The name of this microservice (e.g., "Catalog.API")
        /// REQUIRED for proper service identification in distributed tracing
        /// </summary>
        [Required(ErrorMessage = "ServiceName is required for telemetry identification")]
        [MinLength(1, ErrorMessage = "ServiceName cannot be empty")]
        public string ServiceName { get; set; } = "Unknown";

        /// <summary>
        /// The version of this microservice (e.g., "1.0.0")
        /// Recommended for tracking deployments and troubleshooting
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// OTLP endpoint for exporting traces (e.g., "http://jaeger:4317")
        /// REQUIRED when EnableTracing=true
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";

        /// <summary>
        /// Protocol for OTLP export (Grpc or HttpProtobuf)
        /// </summary>
        [RegularExpression("^(Grpc|HttpProtobuf)$", ErrorMessage = "Protocol must be 'Grpc' or 'HttpProtobuf'")]
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
        /// Use lower values in high-traffic production environments
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "SamplingProbability must be between 0.0 and 1.0")]
        public double SamplingProbability { get; set; } = 1.0;

        /// <summary>
        /// Validates configuration on startup to catch misconfigurations early
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate ServiceName is not default/placeholder
            if (ServiceName == "Unknown" || string.IsNullOrWhiteSpace(ServiceName))
            {
                yield return new ValidationResult(
                    "ServiceName must be set to a meaningful value (not 'Unknown' or empty)",
                    new[] { nameof(ServiceName) });
            }

            // Validate OTLP endpoint when tracing is enabled
            if (EnableTracing)
            {
                if (string.IsNullOrWhiteSpace(OtlpEndpoint))
                {
                    yield return new ValidationResult(
                        "OtlpEndpoint is required when EnableTracing=true",
                        new[] { nameof(OtlpEndpoint) });
                }

                // Validate OTLP endpoint is a valid URI
                if (!string.IsNullOrWhiteSpace(OtlpEndpoint) && !Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out _))
                {
                    yield return new ValidationResult(
                        "OtlpEndpoint must be a valid absolute URI (e.g., http://jaeger:4317)",
                        new[] { nameof(OtlpEndpoint) });
                }
            }
        }
    }
}

