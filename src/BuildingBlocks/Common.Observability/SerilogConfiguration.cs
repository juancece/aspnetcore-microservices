using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Common.Observability
{
    /// <summary>
    /// Serilog configuration helper methods
    /// </summary>
    public static class SerilogConfiguration
    {
        /// <summary>
        /// Configures Serilog as the logging provider with structured logging
        /// </summary>
        /// <param name="builder">The web application builder</param>
        /// <returns>The web application builder for chaining</returns>
        public static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
        {
            var serviceName = builder.Configuration.GetValue<string>("Telemetry:ServiceName") ?? "Unknown";
            var environment = builder.Environment.EnvironmentName;

            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithProperty("ServiceName", serviceName)
                    .Enrich.WithProperty("Environment", environment);

                // LOGGING STRATEGY:
                // - Development: Human-readable console output for local debugging
                // - Production: Structured JSON output for centralized log aggregation (ELK, Splunk, etc.)
                // Rationale: Developers need readable logs, but production systems need machine-parseable structured logs
                if (context.HostingEnvironment.IsDevelopment())
                {
                    // Development: Human-readable format for console debugging
                    configuration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {ServiceName} - {Message:lj}{NewLine}{Exception}",
                        restrictedToMinimumLevel: LogEventLevel.Debug);
                    
                    configuration.MinimumLevel.Debug();
                }
                else
                {
                    // Production: Compact JSON format for log aggregation systems
                    // This enables structured queries, alerting, and correlation across distributed systems
                    configuration.WriteTo.Console(
                        formatter: new CompactJsonFormatter(),
                        restrictedToMinimumLevel: LogEventLevel.Information);
                    
                    configuration.MinimumLevel.Information();
                }

                // Override Microsoft log levels to reduce noise from framework internals
                configuration
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning);
                
                // SECURITY: Filter sensitive data from logs
                // Add Serilog.Expressions or custom filters to redact PII, tokens, passwords
                configuration.Filter.ByExcluding(logEvent =>
                {
                    // Example: Exclude logs containing sensitive headers (can be extended)
                    if (logEvent.Properties.TryGetValue("RequestHeaders", out var headers))
                    {
                        var headersStr = headers.ToString().ToLowerInvariant();
                        return headersStr.Contains("authorization") || headersStr.Contains("cookie");
                    }
                    return false;
                });
            });

            return builder;
        }

        /// <summary>
        /// Configures Serilog request logging middleware
        /// </summary>
        /// <param name="app">The web application</param>
        /// <returns>The web application for chaining</returns>
        public static WebApplication UseSerilogRequestLogging(this WebApplication app)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
                    
                    // Add correlation ID if present
                    if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
                    {
                        diagnosticContext.Set("CorrelationId", correlationId.ToString());
                    }
                };
            });

            return app;
        }
    }
}
