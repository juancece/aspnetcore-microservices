using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Common.Observability
{
    /// <summary>
    /// Extension methods for registering OpenTelemetry instrumentation
    /// </summary>
    public static class OpenTelemetryExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry tracing and metrics with OTLP export
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddObservability(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var telemetryConfig = configuration.GetSection(TelemetryConfiguration.SectionName)
                .Get<TelemetryConfiguration>() ?? new TelemetryConfiguration();

            if (!telemetryConfig.EnableTracing && !telemetryConfig.EnableMetrics)
            {
                return services;
            }

            var builder = services.AddOpenTelemetry();

            // Configure resource with service information
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: telemetryConfig.ServiceName,
                    serviceVersion: telemetryConfig.ServiceVersion);

            // Add tracing
            if (telemetryConfig.EnableTracing)
            {
                builder.WithTracing(tracing => tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            // Don't trace health check endpoints
                            return !httpContext.Request.Path.StartsWithSegments("/health") &&
                                   !httpContext.Request.Path.StartsWithSegments("/healthz");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(telemetryConfig.OtlpEndpoint);
                        options.Protocol = telemetryConfig.Protocol == "Grpc"
                            ? OtlpExportProtocol.Grpc
                            : OtlpExportProtocol.HttpProtobuf;
                    }));
            }

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry with additional source for custom spans
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="sourceName">Additional source name to instrument (e.g., "MassTransit")</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddObservabilityWithSource(
            this IServiceCollection services,
            IConfiguration configuration,
            string sourceName)
        {
            services.AddObservability(configuration);

            // Add the additional source
            services.ConfigureOpenTelemetryTracerProvider(builder =>
                builder.AddSource(sourceName));

            return services;
        }
    }
}

