using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common.Auth
{
    /// <summary>
    /// Extension methods for registering JWT authentication in services
    /// </summary>
    public static class AuthServiceExtensions
    {
        /// <summary>
        /// Adds JWT Bearer authentication with configuration from appsettings
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
            if (!jwtConfig.Enabled)
            {
                return services;
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
                    ClockSkew = jwtConfig.ClockSkew
                };

                // Handle authentication failures for better debugging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception != null)
                        {
                            context.Response.Headers.Add("Token-Error", context.Exception.Message);
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // Log authentication challenge for debugging
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "Unauthorized",
                            message = context.ErrorDescription ?? "Authentication required"
                        });
                        return context.Response.WriteAsync(result);
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

