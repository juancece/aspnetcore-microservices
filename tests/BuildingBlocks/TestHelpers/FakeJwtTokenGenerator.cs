using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace TestHelpers
{
    /// <summary>
    /// Generates fake JWT tokens for testing authentication without a real IdP.
    /// 
    /// ⚠️ WARNING - TESTING ONLY:
    /// This class is STRICTLY for automated testing purposes and should NEVER be used in production.
    /// It generates cryptographic keys in-memory without secure storage or rotation.
    /// 
    /// RESOURCE MANAGEMENT:
    /// - Implements IDisposable to properly clean up RSA cryptographic resources
    /// - MUST be disposed after use to prevent memory leaks
    /// - Recommended pattern: Use in 'using' statement or dispose explicitly in test teardown
    /// 
    /// USAGE:
    /// ```csharp
    /// // Option 1: Using statement (preferred)
    /// using var tokenGenerator = new FakeJwtTokenGenerator();
    /// var token = tokenGenerator.GenerateToken("user1", "id1", new[] { "Admin" });
    /// 
    /// // Option 2: xUnit IClassFixture (for shared instance across tests)
    /// public class MyTests : IClassFixture<FakeJwtTokenGenerator>, IDisposable
    /// {
    ///     private readonly FakeJwtTokenGenerator _tokenGen;
    ///     public MyTests(FakeJwtTokenGenerator tokenGen) => _tokenGen = tokenGen;
    ///     public void Dispose() => _tokenGen.Dispose();
    /// }
    /// ```
    /// </summary>
    public class FakeJwtTokenGenerator : IDisposable
    {
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _securityKey;
        private readonly SigningCredentials _signingCredentials;
        private readonly string _issuer;
        private readonly string _audience;
        private bool _disposed;

        public FakeJwtTokenGenerator(
            string issuer = "https://fake-idp.local",
            string audience = "api://test-api")
        {
            _issuer = issuer;
            _audience = audience;

            // Generate RSA key pair for signing tokens
            // SECURITY: This is a test-only key generated in memory
            // Production systems must use secure key management (Azure Key Vault, AWS KMS, etc.)
            _rsa = RSA.Create(2048);
            _securityKey = new RsaSecurityKey(_rsa);
            _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.RsaSha256);
        }

        /// <summary>
        /// Generates a JWT token with custom claims
        /// </summary>
        /// <param name="userName">The user name</param>
        /// <param name="userId">The user ID</param>
        /// <param name="roles">User roles</param>
        /// <param name="scopes">OAuth2 scopes</param>
        /// <param name="expiresInMinutes">Token expiration time in minutes (default: 60)</param>
        /// <returns>JWT token string</returns>
        public string GenerateToken(
            string userName = "testuser",
            string userId = "test-user-id",
            string[]? roles = null,
            string[]? scopes = null,
            int expiresInMinutes = 60)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Add roles
            if (roles != null && roles.Length > 0)
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // Add scopes
            if (scopes != null && scopes.Length > 0)
            {
                foreach (var scope in scopes)
                {
                    claims.Add(new Claim("scope", scope));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = _signingCredentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Generates an expired JWT token for testing expiration scenarios
        /// </summary>
        public string GenerateExpiredToken(
            string userName = "testuser",
            string userId = "test-user-id")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Sub, userId)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(-10), // Expired 10 minutes ago
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = _signingCredentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Gets the RSA security key for token validation in tests
        /// </summary>
        public RsaSecurityKey GetSecurityKey() => _securityKey;

        /// <summary>
        /// Gets the issuer URL
        /// </summary>
        public string Issuer => _issuer;

        /// <summary>
        /// Gets the audience
        /// </summary>
        public string Audience => _audience;

        /// <summary>
        /// Disposes the RSA cryptographic resources to prevent memory leaks.
        /// IMPORTANT: Always call Dispose() or use 'using' statement when done with this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources (RSA key)
                    _rsa?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure RSA resources are released even if Dispose() is not called
        /// </summary>
        ~FakeJwtTokenGenerator()
        {
            Dispose(false);
        }
    }
}

