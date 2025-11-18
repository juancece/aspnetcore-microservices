# Quickstart: Testing, Authentication & Observability

**Last Updated**: 2025-11-16  
**Audience**: Developers implementing or using the testing/auth/observability infrastructure

---

## Prerequisites

- .NET 6.0 or 7.0 SDK
- Docker Desktop (for integration tests and observability backend)
- IDE: Visual Studio 2022, VS Code, or Rider
- Basic understanding of xUnit, JWT, and OpenTelemetry

---

## Part 1: Running Tests

### Unit Tests

Unit tests run fast (<30 seconds) with mocked dependencies.

```bash
# Run all unit tests
dotnet test --filter Category=Unit

# Run tests for specific service
dotnet test tests/Catalog.API.Tests/

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML coverage report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
```

### Integration Tests

Integration tests use Docker containers for real databases (~5 minutes).

```bash
# Ensure Docker is running
docker ps

# Run all integration tests
dotnet test --filter Category=Integration

# Run integration tests for specific service
dotnet test tests/Catalog.API.IntegrationTests/

# Run specific test
dotnet test --filter "FullyQualifiedName~GetProduct_WithValidId_ReturnsProduct"
```

**Note**: First run downloads Docker images (one-time delay). Subsequent runs reuse cached images.

### TDD Workflow

1. **Red**: Write a failing test
```csharp
[Fact]
public async Task CreateProduct_WithValidData_ReturnsCreated()
{
    // Arrange
    var product = new Product { Name = "Test", Price = 99.99m };
    
    // Act
    var result = await _controller.CreateProduct(product);
    
    // Assert
    var createdResult = result.Result.Should().BeOfType<CreatedAtRouteResult>().Subject;
    createdResult.StatusCode.Should().Be(201);
}
```

2. **Green**: Implement to make test pass
```csharp
[HttpPost]
public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
{
    await _repository.CreateProduct(product);
    return CreatedAtRoute("GetProduct", new { id = product.Id }, product);
}
```

3. **Refactor**: Improve while keeping tests green

---

## Part 2: Authentication & Authorization

### Local Development (Fake IdP)

For local testing without external IdP:

```bash
# appsettings.Development.json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://fake-idp.local",
      "Audience": "api://test-api",
      "RequireHttpsMetadata": false,
      "ValidIssuers": ["https://fake-idp.local"]
    }
  },
  "Features": {
    "EnableAuthentication": true,
    "RequireAuthForExistingEndpoints": false
  }
}
```

**Generate Test Token**:

```csharp
var tokenGenerator = new FakeJwtTokenGenerator(
    issuer: "https://fake-idp.local",
    audience: "api://test-api",
    secret: "your-256-bit-secret-key-must-be-at-least-32-characters-long");

var token = tokenGenerator.GenerateToken(
    userId: "test-user",
    roles: new[] { "CatalogReader" },
    claims: new Dictionary<string, string>
    {
        { "scope", "catalog.read" },
        { "email", "test@example.com" }
    });

Console.WriteLine($"Bearer {token}");
```

**Use Token in API Calls**:

```bash
# HTTP request
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
     http://localhost:8000/api/v1/catalog/products
```

### Production (Entra ID / Auth0)

```bash
# appsettings.Production.json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://login.microsoftonline.com/{tenantId}/v2.0",
      "Audience": "api://catalog-api-prod",
      "RequireHttpsMetadata": true,
      "ValidIssuers": [
        "https://login.microsoftonline.com/{tenantId}/v2.0"
      ]
    }
  }
}
```

**Obtain Token from IdP**:

```bash
# Using OAuth2 client credentials flow
curl -X POST https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=your-client-id" \
  -d "client_secret=your-client-secret" \
  -d "scope=api://catalog-api-prod/.default" \
  -d "grant_type=client_credentials"
```

### Authorization Policies

**Protect New Endpoints**:

```csharp
[Authorize(Policy = "ReadCatalog")]
[HttpGet]
public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
{
    // Only users with "catalog.read" scope and CatalogReader/CatalogAdmin role
    var products = await _repository.GetProducts();
    return Ok(products);
}
```

**Keep Existing Endpoints Anonymous** (Phase 1):

```csharp
[AllowAnonymous]  // Existing endpoint, no auth yet
[HttpGet("{id}")]
public async Task<ActionResult<Product>> GetProductById(string id)
{
    var product = await _repository.GetProduct(id);
    return Ok(product);
}
```

**Check User Claims in Code**:

```csharp
[Authorize]
[HttpGet("my-orders")]
public async Task<ActionResult<IEnumerable<Order>>> GetMyOrders()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var orders = await _orderRepository.GetOrdersByUserId(userId);
    return Ok(orders);
}
```

---

## Part 3: Observability

### Local Development (Jaeger)

Start Jaeger for local tracing:

```bash
# docker-compose.yml (add to existing file)
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # UI
      - "4317:4317"    # OTLP gRPC
      - "4318:4318"    # OTLP HTTP
    environment:
      - COLLECTOR_OTLP_ENABLED=true

# Start services
docker-compose up -d jaeger

# Open Jaeger UI
start http://localhost:16686
```

### Configuration

```bash
# appsettings.Development.json
{
  "Telemetry": {
    "ServiceName": "Catalog.API",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://localhost:4317",
    "Protocol": "Grpc",
    "EnableTracing": true,
    "EnableMetrics": true,
    "SamplingProbability": 1.0
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Json.JsonFormatter, Serilog"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithCorrelationId"]
  }
}
```

### Viewing Traces

1. Trigger a request: `curl http://localhost:8000/api/v1/catalog/products`
2. Open Jaeger UI: http://localhost:16686
3. Select service: "Catalog.API"
4. Click "Find Traces"
5. Click on a trace to see spans

### Viewing Logs

Logs are output to console in JSON format:

```json
{
  "@t": "2025-11-16T10:30:45.1234567Z",
  "@mt": "Product {ProductId} retrieved",
  "@l": "Information",
  "ProductId": "602d2149e773f2a3990b47f5",
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "SpanId": "00f067aa0ba902b7",
  "ServiceName": "Catalog.API",
  "Environment": "Development"
}
```

### Correlating Logs and Traces

All logs share the same `TraceId` as their corresponding traces:

1. Copy `TraceId` from log entry
2. Search in Jaeger UI by TraceId
3. See full request flow across services

### Adding Custom Spans

```csharp
using System.Diagnostics;

[HttpGet("{id}")]
public async Task<ActionResult<Product>> GetProduct(string id)
{
    using var activity = Activity.Current?.Source.StartActivity("GetProduct");
    activity?.SetTag("product.id", id);
    
    var product = await _repository.GetProduct(id);
    
    if (product == null)
    {
        activity?.SetTag("product.found", false);
        return NotFound();
    }
    
    activity?.SetTag("product.found", true);
    activity?.SetTag("product.name", product.Name);
    
    return Ok(product);
}
```

### Adding Structured Logs

```csharp
_logger.LogInformation(
    "Product {ProductId} retrieved by user {UserId}",
    productId,
    userId);

// Avoid string interpolation - use structured parameters
// ❌ BAD: _logger.LogInformation($"Product {productId} retrieved");
// ✅ GOOD: _logger.LogInformation("Product {ProductId} retrieved", productId);
```

---

## Part 4: CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      docker:
        image: docker:dind
        options: --privileged
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '7.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Unit Tests
      run: dotnet test --no-build --filter Category=Unit --logger trx --collect:"XPlat Code Coverage"
    
    - name: Integration Tests
      run: dotnet test --no-build --filter Category=Integration --logger trx
    
    - name: Generate Coverage Report
      run: |
        dotnet tool install --global dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
    
    - name: Upload Coverage
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage.cobertura.xml
```

---

## Part 5: Troubleshooting

### Tests Failing

**Docker not available**:
```bash
# Check Docker is running
docker ps

# If not: start Docker Desktop
```

**Test containers failing to start**:
```bash
# Pull images manually
docker pull mcr.microsoft.com/mssql/server:2019-latest
docker pull mongo:6.0
docker pull postgres:15-alpine
docker pull redis:7-alpine

# Check disk space
docker system df
```

**Slow integration tests**:
- First run downloads images (one-time)
- Use `ICollectionFixture` to share containers
- Run tests in parallel: `dotnet test --parallel`

### Authentication Issues

**401 Unauthorized**:
- Check token is not expired (exp claim)
- Verify issuer matches configuration
- Verify audience matches configuration
- Check HTTPS metadata (set to false for local dev)

**403 Forbidden**:
- User is authenticated but lacks required claims/roles
- Check policy definition matches token claims
- Verify user has correct roles assigned in IdP

**Token validation fails**:
```bash
# Decode JWT to inspect claims
https://jwt.io/

# Check JWKS endpoint is reachable
curl https://login.microsoftonline.com/{tenantId}/discovery/v2.0/keys
```

### Observability Issues

**No traces in Jaeger**:
- Check Jaeger is running: `docker ps | grep jaeger`
- Check OTLP endpoint is correct in appsettings.json
- Check SamplingProbability > 0.0
- Check EnableTracing = true

**Logs not appearing**:
- Check Serilog configuration in appsettings.json
- Check minimum log level (Debug vs Information)
- Check console output redirection in Docker

**TraceId not correlating**:
- Ensure all services use OpenTelemetry
- Ensure HTTP/gRPC clients are instrumented
- Check MassTransit has OpenTelemetry source added

---

## Part 6: Best Practices

### Testing

✅ **DO**:
- Write tests first (TDD)
- Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- One assert per test (or use `Should().And` for related assertions)
- Mock external dependencies in unit tests
- Use real databases in integration tests

❌ **DON'T**:
- Share state between tests
- Use `Thread.Sleep` (use async properly)
- Test implementation details (test behavior)
- Commit failing tests

### Authentication

✅ **DO**:
- Use HTTPS in production (RequireHttpsMetadata = true)
- Validate token lifetime
- Use policy-based authorization (not role strings in code)
- Keep tokens short-lived (1-hour max)
- Rotate signing keys regularly

❌ **DON'T**:
- Store tokens in localStorage (use httpOnly cookies or memory)
- Use symmetric keys in production (use RSA/ECDSA)
- Hard-code roles/claims in controllers
- Skip token validation
- Use `[AllowAnonymous]` on sensitive endpoints

### Observability

✅ **DO**:
- Use structured logging (parameters, not string interpolation)
- Include TraceId in error responses (for customer support)
- Sample traces in high-traffic scenarios (< 1.0 probability)
- Add custom spans for important operations
- Use correlation IDs across services

❌ **DON'T**:
- Log sensitive data (passwords, tokens, PII)
- Use string interpolation in logs
- Create too many custom spans (noise)
- Sample at 100% in production (high volume)
- Ignore trace context propagation

---

## Summary

| Area | Command | URL |
|------|---------|-----|
| **Unit Tests** | `dotnet test --filter Category=Unit` | N/A |
| **Integration Tests** | `dotnet test --filter Category=Integration` | N/A |
| **Code Coverage** | `dotnet test /p:CollectCoverage=true` | `coveragereport/index.html` |
| **Jaeger UI** | `docker-compose up -d jaeger` | http://localhost:16686 |
| **Generate Token** | Use `FakeJwtTokenGenerator` | N/A |
| **API Swagger** | N/A | http://localhost:8000/swagger |

---

## Next Steps

1. ✅ Read this quickstart
2. ➡️ Run unit tests: `dotnet test --filter Category=Unit`
3. ➡️ Run integration tests: `dotnet test --filter Category=Integration`
4. ➡️ Start Jaeger: `docker-compose up -d jaeger`
5. ➡️ Generate test token and call protected endpoint
6. ➡️ View trace in Jaeger UI
7. Read full implementation plan: [plan.md](./plan.md)

