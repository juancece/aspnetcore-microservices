using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;

namespace Common.Auth
{
    /// <summary>
    /// Extension methods for registering JWT authentication in services
    /// </summary>
    public static class AuthServiceExtensions
    {
        /// <summary>
        /// Adds JWT Bearer authentication with configuration from appsettings.
        /// SECURITY: In non-Development environments, this enforces HTTPS, validates all token claims,
        /// and avoids leaking sensitive error details to clients.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtConfig = configuration.GetSection(JwtConfiguration.SectionName)
                .Get<JwtConfiguration>() ?? new JwtConfiguration();

            // If authentication is disabled (for local dev), skip configuration
            // WARNING: This should NEVER be true in production
            if (!jwtConfig.Enabled)
            {
                return services;
            }

            // Get environment to determine security settings
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var isDevelopment = environment == "Development";

            // SECURITY: In production, always enforce HTTPS and strict validation
            if (!isDevelopment)
            {
                jwtConfig.RequireHttpsMetadata = true;
                jwtConfig.ValidateIssuer = true;
                jwtConfig.ValidateAudience = true;
                jwtConfig.ValidateLifetime = true;
                jwtConfig.ValidateIssuerSigningKey = true;
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = jwtConfig.Authority;
                options.Audience = jwtConfig.Audience;
                options.RequireHttpsMetadata = jwtConfig.RequireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtConfig.ValidateIssuer,
                    ValidateAudience = jwtConfig.ValidateAudience,
                    ValidateLifetime = jwtConfig.ValidateLifetime,
                    ValidateIssuerSigningKey = jwtConfig.ValidateIssuerSigningKey,
                    ValidIssuers = jwtConfig.ValidIssuers,
                    ValidAudience = jwtConfig.Audience,
                    ClockSkew = jwtConfig.ClockSkew,
                    
                    // Map claim types explicitly to handle non-standard IdP claim names
                    // This ensures role-based and name-based authorization work correctly
                    RoleClaimType = "role",      // or "roles" depending on your IdP
                    NameClaimType = "name"       // or "preferred_username", "unique_name", etc.
                };

                // Handle authentication failures with security in mind
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        // Get logger from DI (use Type object instead of static class)
                        var loggerFactory = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger("Common.Auth.JwtAuthentication");
                        
                        // Get trace ID for correlation
                        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                        
                        if (context.Exception != null)
                        {
                            // SECURITY: Log exception details server-side with correlation ID
                            logger.LogWarning(context.Exception,
                                "JWT authentication failed. TraceId: {TraceId}, Scheme: {Scheme}",
                                traceId, context.Scheme.Name);
                            
                            // SECURITY: Only expose details in Development
                            // In production, avoid leaking token validation errors to clients
                            if (isDevelopment)
                            {
                                context.Response.Headers.Add("X-Token-Error-DevOnly", context.Exception.Message);
                                context.Response.Headers.Add("X-Trace-Id", traceId);
                            }
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var loggerFactory = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger("Common.Auth.JwtAuthentication");
                        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                        
                        // SECURITY: Log challenge with correlation ID for traceability
                        logger.LogInformation(
                            "Authentication challenge issued. TraceId: {TraceId}, Error: {Error}, ErrorDescription: {ErrorDescription}",
                            traceId, context.Error, context.ErrorDescription);
                        
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        
                        // SECURITY: Generic message to client, details only in logs
                        // Both branches must return compatible types, so use explicit type
                        object responseBody = isDevelopment
                            ? (object)new
                            {
                                error = "Unauthorized",
                                message = context.ErrorDescription ?? "Authentication required",
                                traceId,
                                detail = context.Error  // Only in Development
                            }
                            : new
                            {
                                error = "Unauthorized",
                                message = "Authentication required. Please provide a valid bearer token.",
                                traceId,
                                detail = (string?)null  // Ensure both types have same shape
                            };
                        
                        var result = System.Text.Json.JsonSerializer.Serialize(responseBody);
                        return context.Response.WriteAsync(result);
                    },
                    OnForbidden = context =>
                    {
                        var loggerFactory = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger("Common.Auth.JwtAuthentication");
                        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                        var userName = context.HttpContext.User?.Identity?.Name ?? "anonymous";
                        
                        // SECURITY: Log forbidden access attempts with user info and correlation ID
                        logger.LogWarning(
                            "Access forbidden for user '{UserName}'. TraceId: {TraceId}, Path: {Path}",
                            userName, traceId, context.HttpContext.Request.Path);
                        
                        // Standard 403 response with minimal info
                        context.HttpContext.Response.StatusCode = 403;
                        context.HttpContext.Response.ContentType = "application/json";
                        
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "Forbidden",
                            message = "You do not have permission to access this resource.",
                            traceId
                        });
                        
                        return context.HttpContext.Response.WriteAsync(result);
                    }
                };
            });

            return services;
        }

        /// <summary>
        /// Adds common authorization policies for all microservices
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCommonAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Admin policies (cross-cutting)
                options.AddPolicy(PolicyConstants.AdminAccess, policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireRole(PolicyConstants.Roles.Admin, PolicyConstants.Roles.SuperAdmin));
            });

            return services;
        }
    }
}

