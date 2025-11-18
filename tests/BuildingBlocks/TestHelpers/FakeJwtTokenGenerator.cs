using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace TestHelpers
{
    /// <summary>
    /// Generates fake JWT tokens for testing authentication without a real IdP
    /// </summary>
    public class FakeJwtTokenGenerator
    {
        private readonly RsaSecurityKey _securityKey;
        private readonly SigningCredentials _signingCredentials;
        private readonly string _issuer;
        private readonly string _audience;

        public FakeJwtTokenGenerator(
            string issuer = "https://fake-idp.local",
            string audience = "api://test-api")
        {
            _issuer = issuer;
            _audience = audience;

            // Generate RSA key pair for signing tokens
            var rsa = RSA.Create(2048);
            _securityKey = new RsaSecurityKey(rsa);
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
    }
}

