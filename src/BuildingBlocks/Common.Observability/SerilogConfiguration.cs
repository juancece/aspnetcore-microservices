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
                    .Enrich.WithProperty("Environment", environment)
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {ServiceName} - {Message:lj}{NewLine}{Exception}",
                        restrictedToMinimumLevel: LogEventLevel.Information)
                    .WriteTo.Console(
                        formatter: new CompactJsonFormatter(),
                        restrictedToMinimumLevel: LogEventLevel.Debug);

                // Set minimum level based on environment
                if (context.HostingEnvironment.IsDevelopment())
                {
                    configuration.MinimumLevel.Debug();
                }
                else
                {
                    configuration.MinimumLevel.Information();
                }

                // Override Microsoft log levels to reduce noise
                configuration
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning);
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
