using System.ComponentModel.DataAnnotations;

namespace Common.Auth
{
    /// <summary>
    /// JWT Bearer authentication configuration settings with built-in validation.
    /// Use IOptions<JwtConfiguration> with ValidateDataAnnotations for startup validation.
    /// </summary>
    public class JwtConfiguration : IValidatableObject
    {
        public const string SectionName = "Authentication:JwtBearer";

        /// <summary>
        /// The OpenID Connect authority URL (e.g., https://login.microsoftonline.com/{tenantId}/v2.0)
        /// REQUIRED when Enabled=true
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// The expected audience claim value (e.g., api://your-api-client-id)
        /// REQUIRED when Enabled=true
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Whether to require HTTPS metadata (should be true in production)
        /// </summary>
        public bool RequireHttpsMetadata { get; set; } = true;

        /// <summary>
        /// List of valid token issuers
        /// </summary>
        public string[] ValidIssuers { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Clock skew tolerance for token expiration validation (default: 5 minutes)
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Whether to validate token lifetime (exp, nbf claims)
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Whether to validate the token issuer
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Whether to validate the token audience
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Whether to validate the issuer signing key
        /// </summary>
        public bool ValidateIssuerSigningKey { get; set; } = true;

        /// <summary>
        /// Whether authentication is enabled (for local dev without IdP)
        /// WARNING: Should NEVER be false in production environments
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Validates configuration on startup to catch misconfigurations early
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(Authority))
                {
                    yield return new ValidationResult(
                        "Authority is required when Authentication:JwtBearer:Enabled=true",
                        new[] { nameof(Authority) });
                }

                if (string.IsNullOrWhiteSpace(Audience))
                {
                    yield return new ValidationResult(
                        "Audience is required when Authentication:JwtBearer:Enabled=true",
                        new[] { nameof(Audience) });
                }

                // Validate Authority is a valid URI
                if (!string.IsNullOrWhiteSpace(Authority) && !Uri.TryCreate(Authority, UriKind.Absolute, out _))
                {
                    yield return new ValidationResult(
                        "Authority must be a valid absolute URI",
                        new[] { nameof(Authority) });
                }
            }
        }
    }
}

