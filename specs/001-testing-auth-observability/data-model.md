# Data Model: Testing, Authentication & Observability

**Date**: 2025-11-16  
**Purpose**: Define data structures and entity models for authentication, authorization, and observability

---

## 1. Authentication Entities

### JwtConfiguration

**Purpose**: Holds JWT validation settings from appsettings.json

```csharp
public class JwtConfiguration
{
    /// <summary>
    /// The OpenID Connect authority URL (IdP endpoint)
    /// Example: https://login.microsoftonline.com/{tenantId}/v2.0
    /// </summary>
    public string Authority { get; set; }
    
    /// <summary>
    /// Expected audience claim (usually the API client ID)
    /// Example: api://catalog-api or https://api.example.com
    /// </summary>
    public string Audience { get; set; }
    
    /// <summary>
    /// Whether to require HTTPS for metadata endpoint (false only for local dev)
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
    
    /// <summary>
    /// List of acceptable token issuers
    /// </summary>
    public List<string> ValidIssuers { get; set; }
    
    /// <summary>
    /// Allowed clock skew for token expiration validation (default 5 minutes)
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Whether to validate token lifetime (exp, nbf claims)
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;
    
    /// <summary>
    /// Whether to save the token in AuthenticationProperties (for logging/debugging)
    /// </summary>
    public bool SaveToken { get; set; } = false;
}
```

**Configuration Example** (appsettings.json):
```json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://login.microsoftonline.com/{tenantId}/v2.0",
      "Audience": "api://catalog-api",
      "RequireHttpsMetadata": true,
      "ValidIssuers": [
        "https://login.microsoftonline.com/{tenantId}/v2.0"
      ],
      "ClockSkew": "00:05:00",
      "ValidateLifetime": true,
      "SaveToken": false
    }
  }
}
```

---

## 2. Authorization Entities

### AuthPolicy

**Purpose**: Defines authorization policies based on claims/roles

```csharp
public class AuthPolicy
{
    /// <summary>
    /// Unique policy name (used in [Authorize(Policy = "PolicyName")])
    /// </summary>
    public string PolicyName { get; set; }
    
    /// <summary>
    /// Description of what this policy grants access to
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Required claims (claim type + value)
    /// </summary>
    public Dictionary<string, string> RequiredClaims { get; set; } = new();
    
    /// <summary>
    /// Required roles (any role in this list grants access)
    /// </summary>
    public List<string> RequiredRoles { get; set; } = new();
    
    /// <summary>
    /// Whether ALL required claims must be present (true) or ANY (false)
    /// </summary>
    public bool RequireAllClaims { get; set; } = true;
}
```

**Example Policies**:

```csharp
// Read-only access to catalog
public static readonly AuthPolicy ReadCatalog = new AuthPolicy
{
    PolicyName = "ReadCatalog",
    Description = "Read access to catalog products",
    RequiredClaims = new Dictionary<string, string>
    {
        { "scope", "catalog.read" }
    },
    RequiredRoles = new List<string> { "CatalogReader", "CatalogAdmin" }
};

// Write access to orders
public static readonly AuthPolicy WriteOrders = new AuthPolicy
{
    PolicyName = "WriteOrders",
    Description = "Create/update orders",
    RequiredClaims = new Dictionary<string, string>
    {
        { "scope", "orders.write" }
    },
    RequiredRoles = new List<string> { "OrderAdmin" },
    RequireAllClaims = true
};

// Admin access (any service)
public static readonly AuthPolicy AdminAccess = new AuthPolicy
{
    PolicyName = "AdminAccess",
    Description = "Full admin access to all resources",
    RequiredRoles = new List<string> { "Admin", "SuperAdmin" }
};
```

**Configuration Example** (appsettings.json):
```json
{
  "Authorization": {
    "Policies": [
      {
        "PolicyName": "ReadCatalog",
        "Description": "Read access to catalog",
        "RequiredClaims": {
          "scope": "catalog.read"
        },
        "RequiredRoles": ["CatalogReader", "CatalogAdmin"]
      },
      {
        "PolicyName": "WriteOrders",
        "Description": "Create/update orders",
        "RequiredClaims": {
          "scope": "orders.write"
        },
        "RequiredRoles": ["OrderAdmin"],
        "RequireAllClaims": true
      }
    ]
  }
}
```

---

## 3. Observability Entities

### TelemetryConfiguration

**Purpose**: OpenTelemetry and OTLP exporter settings

```csharp
public class TelemetryConfiguration
{
    /// <summary>
    /// Service name (appears in traces/logs)
    /// Example: Catalog.API, Basket.API
    /// </summary>
    public string ServiceName { get; set; }
    
    /// <summary>
    /// Service version (for tracking deployments)
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";
    
    /// <summary>
    /// OTLP exporter endpoint
    /// Example: http://localhost:4317 (Jaeger), http://otel-collector:4317
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    
    /// <summary>
    /// Exporter protocol (Grpc or HttpProtobuf)
    /// </summary>
    public string Protocol { get; set; } = "Grpc";
    
    /// <summary>
    /// Enable tracing
    /// </summary>
    public bool EnableTracing { get; set; } = true;
    
    /// <summary>
    /// Enable metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
    
    /// <summary>
    /// Sampling probability (0.0 to 1.0)
    /// 1.0 = trace everything, 0.1 = trace 10%
    /// </summary>
    public double SamplingProbability { get; set; } = 1.0;
    
    /// <summary>
    /// Additional resource attributes (environment, region, etc.)
    /// </summary>
    public Dictionary<string, string> ResourceAttributes { get; set; } = new();
}
```

**Configuration Example** (appsettings.json):
```json
{
  "Telemetry": {
    "ServiceName": "Catalog.API",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://jaeger:4317",
    "Protocol": "Grpc",
    "EnableTracing": true,
    "EnableMetrics": true,
    "SamplingProbability": 1.0,
    "ResourceAttributes": {
      "environment": "production",
      "region": "us-east-1",
      "deployment": "blue"
    }
  }
}
```

---

### LogContext

**Purpose**: Structured properties enriching all logs

```csharp
public class LogContext
{
    /// <summary>
    /// W3C Trace ID (for correlation across services)
    /// </summary>
    public string TraceId { get; set; }
    
    /// <summary>
    /// W3C Span ID (for correlation within service)
    /// </summary>
    public string SpanId { get; set; }
    
    /// <summary>
    /// Custom correlation ID (for client-initiated traces)
    /// </summary>
    public string CorrelationId { get; set; }
    
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; }
    
    /// <summary>
    /// Environment (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; }
    
    /// <summary>
    /// User ID (from JWT claims, if authenticated)
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// Request path
    /// </summary>
    public string RequestPath { get; set; }
    
    /// <summary>
    /// HTTP method
    /// </summary>
    public string HttpMethod { get; set; }
    
    /// <summary>
    /// Client IP address
    /// </summary>
    public string ClientIp { get; set; }
    
    /// <summary>
    /// Additional context (order ID, product ID, etc.)
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}
```

**Log Output Example** (JSON):
```json
{
  "@t": "2025-11-16T10:30:45.1234567Z",
  "@mt": "Product {ProductId} retrieved successfully",
  "@l": "Information",
  "ProductId": "602d2149e773f2a3990b47f5",
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "SpanId": "00f067aa0ba902b7",
  "CorrelationId": "abc123-request-456",
  "ServiceName": "Catalog.API",
  "Environment": "Production",
  "UserId": "user@example.com",
  "RequestPath": "/api/v1/catalog/products/602d2149e773f2a3990b47f5",
  "HttpMethod": "GET",
  "ClientIp": "203.0.113.42"
}
```

---

## 4. Testing Entities

### TestFixture (Integration Tests)

**Purpose**: Manages Docker container lifecycle for integration tests

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public IContainer SqlServerContainer { get; private set; }
    public IContainer MongoDbContainer { get; private set; }
    public IContainer PostgresContainer { get; private set; }
    public IContainer RedisContainer { get; private set; }
    
    public string SqlServerConnectionString { get; private set; }
    public string MongoDbConnectionString { get; private set; }
    public string PostgresConnectionString { get; private set; }
    public string RedisConnectionString { get; private set; }
    
    public async Task InitializeAsync()
    {
        // Start containers in parallel
        var sqlTask = StartSqlServerAsync();
        var mongoTask = StartMongoDbAsync();
        var postgresTask = StartPostgresAsync();
        var redisTask = StartRedisAsync();
        
        await Task.WhenAll(sqlTask, mongoTask, postgresTask, redisTask);
        
        SqlServerConnectionString = await sqlTask;
        MongoDbConnectionString = await mongoTask;
        PostgresConnectionString = await postgresTask;
        RedisConnectionString = await redisTask;
    }
    
    public async Task DisposeAsync()
    {
        // Stop containers in parallel
        await Task.WhenAll(
            SqlServerContainer?.StopAsync() ?? Task.CompletedTask,
            MongoDbContainer?.StopAsync() ?? Task.CompletedTask,
            PostgresContainer?.StopAsync() ?? Task.CompletedTask,
            RedisContainer?.StopAsync() ?? Task.CompletedTask
        );
    }
    
    private async Task<string> StartSqlServerAsync()
    {
        SqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();
            
        await SqlServerContainer.StartAsync();
        return SqlServerContainer.GetConnectionString();
    }
    
    // Similar methods for MongoDB, Postgres, Redis...
}
```

**Usage in Tests**:
```csharp
public class CatalogIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    
    public CatalogIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange: Use _fixture.MongoDbConnectionString
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"DatabaseSettings:ConnectionString", _fixture.MongoDbConnectionString}
                    });
                });
            });
        
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/v1/catalog/products/602d2149e773f2a3990b47f5");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

### FakeJwtToken (Testing Helper)

**Purpose**: Generate test JWT tokens for integration/auth tests

```csharp
public class FakeJwtToken
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _secret;
    
    public FakeJwtToken(string issuer, string audience, string secret)
    {
        _issuer = issuer;
        _audience = audience;
        _secret = secret;
    }
    
    public string GenerateToken(
        string userId,
        string[] roles = null,
        Dictionary<string, string> claims = null,
        DateTime? expires = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secret);
        
        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim("sub", userId)
        };
        
        if (roles != null)
        {
            claimsList.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        }
        
        if (claims != null)
        {
            claimsList.AddRange(claims.Select(c => new Claim(c.Key, c.Value)));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimsList),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
```

**Usage in Tests**:
```csharp
[Fact]
public async Task GetProducts_WithValidToken_ReturnsOk()
{
    // Arrange
    var tokenGenerator = new FakeJwtToken(
        "https://fake-idp.local",
        "api://test-api",
        "your-256-bit-secret-key-min-32-chars");
    
    var token = tokenGenerator.GenerateToken(
        userId: "test-user",
        roles: new[] { "CatalogReader" },
        claims: new Dictionary<string, string>
        {
            { "scope", "catalog.read" }
        });
    
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);
    
    // Act
    var response = await client.GetAsync("/api/v1/catalog/products");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

---

## Entity Relationships

```
JwtConfiguration
    ↓ (validates)
JWT Token (from IdP)
    ↓ (contains)
Claims/Roles
    ↓ (evaluated by)
AuthPolicy
    ↓ (enforces)
[Authorize] attribute
    ↓ (protects)
API Endpoint

---

TelemetryConfiguration
    ↓ (configures)
OpenTelemetry
    ↓ (instruments)
HTTP/gRPC/MassTransit
    ↓ (generates)
Traces + Metrics
    ↓ (exports via)
OTLP
    ↓ (to)
Observability Backend

---

LogContext
    ↓ (enriches)
Serilog Logs
    ↓ (correlates with)
OpenTelemetry Traces
    ↓ (same TraceId)
End-to-End Observability
```

---

## State Transitions

### Authentication Flow

```
1. Client obtains JWT from IdP
   ↓
2. Client sends request with Authorization: Bearer {token}
   ↓
3. API receives request
   ↓
4. JwtBearer middleware validates token
   ├─ Invalid → 401 Unauthorized
   └─ Valid → Continue
       ↓
5. Authorization middleware evaluates policies
   ├─ Insufficient permissions → 403 Forbidden
   └─ Authorized → Execute endpoint
```

### Observability Flow

```
1. Request arrives at API
   ↓
2. OpenTelemetry creates root span
   ↓
3. Serilog enriches log context with TraceId
   ↓
4. Request flows through middleware/services
   ├─ HTTP call to another service → Child span created
   ├─ RabbitMQ publish → Child span created, TraceId propagated
   └─ Database query → Child span created
   ↓
5. Spans/logs exported to backend
   ↓
6. Backend correlates by TraceId
```

---

## Validation Rules

### JWT Token Validation

- **Signature**: Must be valid using JWKS from IdP
- **Issuer (iss)**: Must match configured valid issuers
- **Audience (aud)**: Must match configured audience
- **Expiration (exp)**: Must be in the future (with clock skew tolerance)
- **Not Before (nbf)**: Must be in the past
- **Required Claims**: Must contain all policy-required claims/roles

### Policy Evaluation

- **RequireAllClaims = true**: ALL required claims must be present
- **RequireAllClaims = false**: ANY required claim grants access
- **Roles**: ANY role in RequiredRoles grants access
- **Empty policy**: No restrictions (authenticated users allowed)

### Telemetry Validation

- **ServiceName**: Required, non-empty
- **TraceId**: Must be valid W3C trace-id format (32 hex chars)
- **SpanId**: Must be valid W3C span-id format (16 hex chars)
- **SamplingProbability**: Must be between 0.0 and 1.0

---

## Next Steps

1. ✅ Data model complete
2. ➡️ Create API contracts (JSON schemas)
3. ➡️ Create quickstart guide
4. Update constitution with cross-cutting rules

