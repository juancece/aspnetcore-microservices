namespace Common.Auth
{
    /// <summary>
    /// JWT Bearer authentication configuration settings
    /// </summary>
    public class JwtConfiguration
    {
        public const string SectionName = "Authentication:JwtBearer";

        /// <summary>
        /// The OpenID Connect authority URL (e.g., https://login.microsoftonline.com/{tenantId}/v2.0)
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// The expected audience claim value (e.g., api://your-api-client-id)
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
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}

