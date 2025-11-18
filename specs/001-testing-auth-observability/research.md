# Research: Testing, Authentication & Observability

**Date**: 2025-11-16  
**Purpose**: Resolve technology decisions for implementing testing infrastructure, OAuth2/JWT authentication, and observability

---

## 1. Test Framework: xUnit vs NUnit vs MSTest

### Decision: **xUnit**

### Rationale:
- **Industry standard** for .NET Core/ASP.NET Core projects
- **Parallel execution** by default (faster test runs)
- **Extensibility** via attributes and custom traits
- **Theory support** for data-driven tests
- **No test state pollution** (fresh instance per test)
- **Microsoft recommendation** for new projects

### Alternatives Considered:
- **NUnit**: More features but heavier, parallel execution requires configuration
- **MSTest**: Built-in but less flexible, slower evolution

### References:
- https://xunit.net/
- https://learn.microsoft.com/en-us/dotnet/core/testing/

---

## 2. Mocking Library: Moq vs NSubstitute

### Decision: **Moq**

### Rationale:
- **Most widely adopted** in .NET ecosystem
- **Fluent API** for setup and verification
- **Mature and stable** (v4.x)
- **Excellent IDE support** (IntelliSense)
- **Works well with dependency injection**

### Alternatives Considered:
- **NSubstitute**: Cleaner syntax but less documentation
- **FakeItEasy**: Good alternative but smaller community

### Example Usage:
```csharp
var mockRepository = new Mock<IProductRepository>();
mockRepository.Setup(r => r.GetProduct(It.IsAny<string>()))
    .ReturnsAsync(new Product { Id = "123", Name = "Test" });
```

### References:
- https://github.com/moq/moq4

---

## 3. Integration Tests: Testcontainers Configuration

### Decision: **Testcontainers for .NET**

### Rationale:
- **Real database instances** in Docker (not mocks or in-memory)
- **Automatic lifecycle management** (start/stop/cleanup)
- **Isolation** between test runs
- **CI/CD compatible** (works in pipelines with Docker)
- **Supports all databases** in the solution

### Configuration:

#### SQL Server (Ordering)
```csharp
var sqlContainer = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
    .WithPassword("YourStrong@Passw0rd")
    .Build();

await sqlContainer.StartAsync();
var connectionString = sqlContainer.GetConnectionString();
```

#### MongoDB (Catalog)
```csharp
var mongoContainer = new MongoDbBuilder()
    .WithImage("mongo:6.0")
    .Build();

await mongoContainer.StartAsync();
var connectionString = mongoContainer.GetConnectionString();
```

#### PostgreSQL (Discount)
```csharp
var postgresContainer = new PostgreSqlBuilder()
    .WithImage("postgres:15-alpine")
    .WithDatabase("discountdb")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

await postgresContainer.StartAsync();
var connectionString = postgresContainer.GetConnectionString();
```

#### Redis (Basket)
```csharp
var redisContainer = new RedisBuilder()
    .WithImage("redis:7-alpine")
    .Build();

await redisContainer.StartAsync();
var connectionString = redisContainer.GetConnectionString();
```

### Performance Considerations:
- Container startup: ~5-15 seconds per database
- Use xUnit `IClassFixture<>` to share containers across tests in a class
- Use `ICollectionFixture<>` for sharing across test classes
- Total integration test time: <5 minutes target

### References:
- https://dotnet.testcontainers.org/
- https://www.testcontainers.com/

---

## 4. JWT Validation Strategy

### Decision: **Local JWT Validation with `Microsoft.AspNetCore.Authentication.JwtBearer`**

### Rationale:
- **Performance**: Local signature validation (no network call to IdP per request)
- **Offline validation**: Uses JWKS (JSON Web Key Set) caching
- **Standard compliance**: Validates signature, issuer, audience, expiration
- **Built-in refresh**: Automatic key rotation support
- **Middleware integration**: Seamless ASP.NET Core integration

### Architecture:
```
[Client] ---(JWT Token)---> [API Gateway/Service]
                                |
                                v
                          JWT Middleware validates:
                          - Signature (using JWKS from IdP)
                          - Issuer (matches configured Authority)
                          - Audience (matches configured value)
                          - Expiration (nbf, exp claims)
                          - Additional claims for policies
```

### Configuration:
```json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://login.microsoftonline.com/{tenantId}/v2.0",
      "Audience": "api://your-api-client-id",
      "RequireHttpsMetadata": true,
      "ValidIssuers": [
        "https://login.microsoftonline.com/{tenantId}/v2.0"
      ]
    }
  }
}
```

### Alternatives Considered:
- **Token Introspection**: Network call per request (high latency)
- **Gateway-level validation only**: Less secure, no policy enforcement per service
- **Symmetric keys (HS256)**: Shared secrets difficult to manage across services

### References:
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
- https://jwt.io/

---

## 5. OpenID Connect Provider for Dev/Testing

### Decision: **Duende IdentityServer (local fake IdP) for development, Entra ID for production**

### Rationale:
- **Development**: Self-contained fake IdP for local testing
  - No external dependencies
  - Fast token generation
  - Configurable claims/roles
  - Runs in Docker alongside services

- **Production**: Entra ID (Azure AD) or Auth0
  - Enterprise-ready
  - MFA, conditional access
  - Compliance (SOC 2, GDPR)
  - Audit logs

### Fake IdP Implementation:
```csharp
// For testing only - generates tokens without real IdP
public class FakeJwtTokenGenerator
{
    public string GenerateToken(string userId, string[] roles, Dictionary<string, string> claims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("your-256-bit-secret");
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
                ...roles.Select(r => new Claim(ClaimTypes.Role, r)),
                ...claims.Select(c => new Claim(c.Key, c.Value))
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "https://fake-idp.local",
            Audience = "api://test-api",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
```

### References:
- https://duendesoftware.com/products/identityserver
- https://learn.microsoft.com/en-us/entra/identity/

---

## 6. Serilog Sinks for Logging

### Decision: **Console sink for all environments, optional Seq/Application Insights for production**

### Rationale:
- **Console sink**: Universal, works in Docker, Kubernetes, local dev
- **Structured JSON**: Machine-parseable logs
- **Seq (optional)**: Local development log viewer
- **Application Insights/New Relic (optional)**: Production observability platform

### Configuration:
```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Catalog.API")
    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
    .Enrich.WithCorrelationId()
    .WriteTo.Console(new JsonFormatter()));
```

### Alternatives Considered:
- **File sink**: Problematic in containers (ephemeral)
- **Elasticsearch directly**: Adds complexity, prefer OTLP export
- **Cloud-only sinks**: Requires internet, not suitable for development

### References:
- https://serilog.net/
- https://github.com/serilog/serilog-sinks-console

---

## 7. OpenTelemetry Exporter

### Decision: **OTLP (OpenTelemetry Protocol) exporter to configurable backend**

### Rationale:
- **Vendor-neutral**: Works with Jaeger, Zipkin, New Relic, Datadog, etc.
- **Standard protocol**: Industry standard (CNCF)
- **Future-proof**: Easy to switch backends
- **Development**: Export to local Jaeger or console
- **Production**: Export to cloud provider (Azure Monitor, New Relic, etc.)

### Configuration:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

### Development Setup:
```yaml
# docker-compose.yml addition
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # UI
      - "4317:4317"    # OTLP gRPC
      - "4318:4318"    # OTLP HTTP
```

### References:
- https://opentelemetry.io/docs/instrumentation/net/
- https://github.com/open-telemetry/opentelemetry-dotnet

---

## 8. MassTransit OpenTelemetry Instrumentation

### Decision: **Use MassTransit.OpenTelemetry package**

### Rationale:
- **Official support** from MassTransit
- **Automatic tracing** of publish/consume operations
- **Context propagation** across RabbitMQ messages
- **Correlation** with HTTP requests

### Implementation:
```csharp
// In MassTransit configuration
builder.Services.AddMassTransit(config =>
{
    // ... existing consumer registration
    config.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"]);
        cfg.ConfigureEndpoints(ctx);
    });
});

// In OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)); // ✅ Adds MassTransit
```

### Trace Propagation:
- TraceId/SpanId automatically added to RabbitMQ message headers
- Consumer receives parent TraceId and creates child span
- End-to-end trace: API Gateway → Basket → RabbitMQ → Ordering

### References:
- https://masstransit.io/documentation/configuration/observability

---

## 9. Feature Flag Library

### Decision: **Microsoft.FeatureManagement (optional, config-based toggle sufficient initially)**

### Rationale:
- **Simple toggle**: appsettings.json feature flags sufficient for Phase 1
- **Microsoft.FeatureManagement**: Can be added later for advanced scenarios (A/B testing, gradual rollout)

### Initial Approach (Config-based):
```json
{
  "Features": {
    "EnableAuthentication": true,
    "RequireAuthForExistingEndpoints": false  // Incremental rollout
  }
}
```

```csharp
// In controller
[Authorize]  // Only enforced if EnableAuthentication = true
public async Task<ActionResult> NewSecureEndpoint() { ... }

[AllowAnonymous]  // Always allowed
public async Task<ActionResult> ExistingPublicEndpoint() { ... }
```

### Future Enhancement:
```csharp
// With Microsoft.FeatureManagement
[FeatureGate("RequireAuthForExistingEndpoints")]
[Authorize(Policy = "ReadAccess")]
public async Task<ActionResult> MigratedEndpoint() { ... }
```

### References:
- https://learn.microsoft.com/en-us/azure/azure-app-configuration/use-feature-flags-dotnet-core

---

## 10. Code Coverage Tool

### Decision: **coverlet with ReportGenerator**

### Rationale:
- **Built-in**: Included with .NET SDK
- **Cross-platform**: Works on Windows, Linux, macOS
- **CI/CD friendly**: Integrates with GitHub Actions, Azure Pipelines
- **Multiple formats**: Cobertura, OpenCover, lcov, etc.
- **ReportGenerator**: Beautiful HTML reports

### Usage:
```bash
# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html

# Open report
start coveragereport/index.html
```

### Threshold Configuration:
```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <CoverletOutput>./coverage/</CoverletOutput>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>80</Threshold>
  <ThresholdType>line,branch</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

### References:
- https://github.com/coverlet-coverage/coverlet
- https://github.com/danielpalme/ReportGenerator

---

## Summary of Decisions

| Decision Area | Choice | Rationale |
|---------------|--------|-----------|
| **Test Framework** | xUnit | Industry standard, parallel execution |
| **Mocking** | Moq | Most popular, fluent API |
| **Integration Tests** | Testcontainers | Real databases in Docker |
| **JWT Validation** | Local with JwtBearer | Performance, offline validation |
| **IdP Dev/Testing** | Fake IdP / Duende | Self-contained, fast |
| **IdP Production** | Entra ID / Auth0 | Enterprise-ready |
| **Logging** | Serilog + Console | Structured, universal |
| **Tracing** | OpenTelemetry + OTLP | Vendor-neutral standard |
| **MassTransit Tracing** | MassTransit.OpenTelemetry | Official support |
| **Feature Flags** | Config-based (initially) | Simple, sufficient |
| **Code Coverage** | coverlet + ReportGenerator | Built-in, CI-friendly |

---

## Next Steps

1. ✅ Research complete
2. ➡️ Proceed to Phase 1: Design (data-model.md, contracts, quickstart.md)
3. Update constitution with cross-cutting rules
4. Generate implementation tasks

