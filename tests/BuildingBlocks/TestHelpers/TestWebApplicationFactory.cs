using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace TestHelpers
{
    /// <summary>
    /// Base class for creating test web application factories for integration tests
    /// Provides common configuration and test container support
    /// </summary>
    /// <typeparam name="TProgram">The Program class of the service being tested</typeparam>
    public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        private readonly DatabaseFixture? _databaseFixture;
        private readonly MongoDbFixture? _mongoDbFixture;
        private readonly RedisFixture? _redisFixture;
        private readonly FakeJwtTokenGenerator? _tokenGenerator;

        public DatabaseFixture? DatabaseFixture => _databaseFixture;
        public FakeJwtTokenGenerator? TokenGenerator => _tokenGenerator;

        public TestWebApplicationFactory(
            DatabaseFixture? databaseFixture = null,
            FakeJwtTokenGenerator? tokenGenerator = null)
        {
            _databaseFixture = databaseFixture;
            _tokenGenerator = tokenGenerator;
        }

        public TestWebApplicationFactory(
            MongoDbFixture? mongoDbFixture,
            FakeJwtTokenGenerator? tokenGenerator = null)
        {
            _mongoDbFixture = mongoDbFixture;
            _tokenGenerator = tokenGenerator;
        }

        public TestWebApplicationFactory(
            RedisFixture? redisFixture,
            FakeJwtTokenGenerator? tokenGenerator = null)
        {
            _redisFixture = redisFixture;
            _tokenGenerator = tokenGenerator;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Add test-specific configuration
                var testConfig = new Dictionary<string, string?>
                {
                    ["Environment"] = "Test",
                    ["Authentication:JwtBearer:Enabled"] = "false" // Disable real auth in tests by default
                };

                // Override connection strings with test container URLs
                if (_databaseFixture != null)
                {
                    if (!string.IsNullOrEmpty(_databaseFixture.SqlServerConnectionString))
                    {
                        testConfig["ConnectionStrings:OrderingConnectionString"] = _databaseFixture.SqlServerConnectionString;
                    }

                    if (!string.IsNullOrEmpty(_databaseFixture.MongoDbConnectionString))
                    {
                        testConfig["DatabaseSettings:ConnectionString"] = _databaseFixture.MongoDbConnectionString;
                    }

                    if (!string.IsNullOrEmpty(_databaseFixture.PostgreSqlConnectionString))
                    {
                        testConfig["DatabaseSettings:ConnectionString"] = _databaseFixture.PostgreSqlConnectionString;
                    }

                    if (!string.IsNullOrEmpty(_databaseFixture.RedisConnectionString))
                    {
                        testConfig["CacheSettings:ConnectionString"] = _databaseFixture.RedisConnectionString;
                    }
                }

                // Override with specific fixture if provided
                if (_mongoDbFixture != null)
                {
                    testConfig["DatabaseSettings:ConnectionString"] = _mongoDbFixture.ConnectionString;
                    testConfig["DatabaseSettings:DatabaseName"] = "CatalogTestDb";
                    testConfig["DatabaseSettings:CollectionName"] = "Products";
                }

                // Override with Redis fixture if provided
                if (_redisFixture != null)
                {
                    testConfig["CacheSettings:ConnectionString"] = _redisFixture.ConnectionString;
                }

                // Configure fake JWT token validation if token generator is provided
                if (_tokenGenerator != null)
                {
                    testConfig["Authentication:JwtBearer:Enabled"] = "true";
                    testConfig["Authentication:JwtBearer:Authority"] = _tokenGenerator.Issuer;
                    testConfig["Authentication:JwtBearer:Audience"] = _tokenGenerator.Audience;
                    testConfig["Authentication:JwtBearer:RequireHttpsMetadata"] = "false";
                }

                config.AddInMemoryCollection(testConfig);
            });

            builder.ConfigureTestServices(services =>
            {
                // Remove the real authentication if present
                var authDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAuthenticationService));
                
                // Add test authentication scheme that auto-authenticates all requests
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options => { });

                // Add authorization
                services.AddAuthorization();
            });

            builder.UseEnvironment("Test");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Ensure test server is created
            return base.CreateHost(builder);
        }
    }

    /// <summary>
    /// Extension methods for HTTP client authentication in tests
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Adds a fake JWT Bearer token to the HTTP client for authentication tests
        /// </summary>
        public static HttpClient WithFakeJwtBearerToken(
            this HttpClient client,
            FakeJwtTokenGenerator tokenGenerator,
            string userName = "testuser",
            string userId = "test-user-id",
            string[]? roles = null,
            string[]? scopes = null)
        {
            var token = tokenGenerator.GenerateToken(userName, userId, roles, scopes);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>
        /// Removes authentication headers from the HTTP client
        /// </summary>
        public static HttpClient WithoutAuthentication(this HttpClient client)
        {
            client.DefaultRequestHeaders.Authorization = null;
            return client;
        }
    }

    /// <summary>
    /// Test authentication handler that automatically authenticates all requests
    /// </summary>
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string AuthenticationScheme = "Test";
        public const string UserId = "test-user-id";
        public const string UserName = "testuser";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Provide all scopes and roles for comprehensive test coverage
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, UserName),
                new Claim(ClaimTypes.Email, "testuser@test.com"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "User"),
                // Catalog scopes
                new Claim("scope", "catalog.read"),
                new Claim("scope", "catalog.write"),
                // Basket scopes
                new Claim("scope", "basket.read"),
                new Claim("scope", "basket.write"),
                // Discount scopes
                new Claim("scope", "discount.read"),
                new Claim("scope", "discount.write"),
                // Orders scopes
                new Claim("scope", "orders.read"),
                new Claim("scope", "orders.write"),
                // Shopping scopes
                new Claim("scope", "shopping.read"),
            };

            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }
    }
}

