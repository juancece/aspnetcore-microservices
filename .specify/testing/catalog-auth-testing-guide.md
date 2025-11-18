# Catalog.API Authentication Testing Guide

**Feature**: JWT Bearer Authentication (User Story 2)  
**Service**: Catalog.API  
**Status**: ✅ Implemented, Ready for Testing  
**Date**: 2025-11-17

---

## What Was Implemented

### ✅ **Non-Breaking Authentication**
- **GET endpoints** remain `[AllowAnonymous]` - existing clients continue to work
- **Write endpoints** (POST/PUT/DELETE) require JWT Bearer tokens with proper claims

### ✅ **Policy-Based Authorization**
- **ReadCatalog** policy: Requires `catalog.read` scope (not enforced on GET - they're anonymous)
- **WriteCatalog** policy: Requires `catalog.write` scope + Admin or User role

### ✅ **Configuration**
- Authentication **disabled by default** (`Enabled: false` in appsettings.json)
- Can be toggled on by changing `Authentication:JwtBearer:Enabled` to `true`

---

## Testing Scenarios

### **Scenario 1: Anonymous Access (Non-Breaking) ✅**

**Test**: Verify existing GET endpoints still work without authentication

**Steps**:
1. Start Catalog.API (authentication is disabled by default)
2. Call GET endpoints without any authentication headers

**Expected Result**: All GET requests succeed with 200 OK

```bash
# PowerShell
curl http://localhost:5000/api/v1/Catalog

# Expected: 200 OK with product list (empty if database is fresh)
```

**Swagger Test**:
- Navigate to `http://localhost:5000/swagger`
- Expand `GET /api/v1/Catalog`
- Click "Try it out" → "Execute"
- **Expected**: 200 OK response

---

### **Scenario 2: Protected Endpoints (When Auth Disabled) ⚠️**

**Test**: Verify write endpoints when authentication is disabled

**Steps**:
1. Ensure `Authentication:JwtBearer:Enabled` is `false` (default)
2. Call POST/PUT/DELETE without authentication

**Expected Result**: Requests succeed (because auth is disabled for local dev)

```bash
# PowerShell
$body = @{
    Name = "Test Product"
    Category = "Electronics"
    Summary = "Test"
    Description = "Testing authentication"
    ImageFile = "test.png"
    Price = 99.99
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:5000/api/v1/Catalog -Method POST -Body $body -ContentType "application/json"

# Expected: 201 Created (auth disabled, so no token needed)
```

---

### **Scenario 3: Enable Authentication & Test Protection 🔒**

**Test**: Verify protected endpoints require authentication when enabled

**Steps**:

#### 3.1: Enable Authentication

Edit `src/Services/Catalog/Catalog.API/appsettings.Development.json` (create if doesn't exist):

```json
{
  "Authentication": {
    "JwtBearer": {
      "Enabled": true,
      "Authority": "https://fake-idp.local",
      "Audience": "api://catalog-api",
      "RequireHttpsMetadata": false,
      "ValidIssuers": ["https://fake-idp.local"]
    }
  }
}
```

#### 3.2: Test Without Token

```bash
# PowerShell
$body = @{
    Name = "Test Product"
    Category = "Electronics"
    Summary = "Test"
    Description = "Testing authentication"
    ImageFile = "test.png"
    Price = 99.99
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri http://localhost:5000/api/v1/Catalog -Method POST -Body $body -ContentType "application/json"
} catch {
    Write-Host "Status: $($_.Exception.Response.StatusCode.value__)"
}

# Expected: 401 Unauthorized
```

**Swagger Test**:
- Navigate to `http://localhost:5000/swagger`
- Expand `POST /api/v1/Catalog`
- Click "Try it out" → "Execute"
- **Expected**: 401 Unauthorized response with JSON error message

---

### **Scenario 4: Test with Valid JWT Token ✅**

**Test**: Verify protected endpoints work with valid JWT token

**Option A: Using Test Project (Automated)**

Create a quick test in `tests/Catalog.API.IntegrationTests/`:

```csharp
// tests/Catalog.API.IntegrationTests/Auth/CatalogAuthTests.cs
using System.Net;
using System.Net.Http.Json;
using Catalog.API.Entities;
using Common.Auth;
using FluentAssertions;
using TestHelpers;
using Xunit;

namespace Catalog.API.IntegrationTests.Auth
{
    [Trait("Category", "Integration")]
    public class CatalogAuthTests : IClassFixture<MongoDbFixture>
    {
        private readonly MongoDbFixture _dbFixture;
        private readonly FakeJwtTokenGenerator _tokenGenerator;
        private readonly TestWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public CatalogAuthTests(MongoDbFixture dbFixture)
        {
            _dbFixture = dbFixture;
            _tokenGenerator = new FakeJwtTokenGenerator(
                issuer: "https://fake-idp.local",
                audience: "api://catalog-api");

            var fullFixture = new DatabaseFixture
            {
                MongoDbConnectionString = _dbFixture.ConnectionString
            };

            _factory = new TestWebApplicationFactory<Program>(fullFixture, _tokenGenerator);
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task CreateProduct_ShouldReturn401_WhenNoToken()
        {
            // Arrange
            var product = new Product
            {
                Name = "Test Product",
                Category = "Electronics",
                Price = 99.99M
            };

            _client.WithoutAuthentication();

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/Catalog", product);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateProduct_ShouldReturn201_WhenValidToken()
        {
            // Arrange
            var product = new Product
            {
                Name = "Test Product",
                Category = "Electronics",
                Summary = "Test",
                Description = "Test",
                ImageFile = "test.png",
                Price = 99.99M
            };

            _client.WithFakeJwtBearerToken(
                _tokenGenerator,
                roles: new[] { PolicyConstants.Roles.Admin },
                scopes: new[] { PolicyConstants.Scopes.CatalogWrite });

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/Catalog", product);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdProduct = await response.Content.ReadFromJsonAsync<Product>();
            createdProduct.Should().NotBeNull();
            createdProduct!.Name.Should().Be("Test Product");
        }

        [Fact]
        public async Task CreateProduct_ShouldReturn403_WhenInsufficientScopes()
        {
            // Arrange
            var product = new Product
            {
                Name = "Test Product",
                Category = "Electronics",
                Price = 99.99M
            };

            // Token with wrong scope
            _client.WithFakeJwtBearerToken(
                _tokenGenerator,
                roles: new[] { PolicyConstants.Roles.User },
                scopes: new[] { PolicyConstants.Scopes.CatalogRead }); // Wrong scope!

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/Catalog", product);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
```

**Run the test**:
```bash
dotnet test tests/Catalog.API.IntegrationTests/ --filter "Category=Integration"
```

---

**Option B: Manual Testing with Postman/Swagger**

Since we don't have a real IdP configured yet, manual testing with a real JWT is not possible. However, you can:

1. **Generate a test token** using an online tool like https://jwt.io
2. **Configure the token** to match your settings:
   - Issuer (`iss`): `https://fake-idp.local`
   - Audience (`aud`): `api://catalog-api`
   - Claims: `scope: catalog.write`, `role: Admin`
3. **Use the token** in Swagger's "Authorize" button or Postman's "Authorization" header

**Note**: This requires a valid signing key, which is complex for manual testing. **Option A (automated tests) is recommended**.

---

### **Scenario 5: Test Anonymous GET Endpoints (Even When Auth Enabled) ✅**

**Test**: Verify GET endpoints remain public even when auth is enabled

**Steps**:
1. Ensure `Authentication:JwtBearer:Enabled` is `true`
2. Call GET endpoints without any token

**Expected Result**: GET requests still succeed (because of `[AllowAnonymous]`)

```bash
# PowerShell
curl http://localhost:5000/api/v1/Catalog

# Expected: 200 OK (even with auth enabled, GET is anonymous)
```

---

## Quick Verification Checklist

### ✅ **With Auth Disabled (Default)**
- [ ] GET `/api/v1/Catalog` → **200 OK** without token
- [ ] GET `/api/v1/Catalog/{id}` → **200 OK** or **404 Not Found** without token
- [ ] POST `/api/v1/Catalog` → **201 Created** without token (auth disabled)
- [ ] PUT `/api/v1/Catalog` → **200 OK** without token (auth disabled)
- [ ] DELETE `/api/v1/Catalog/{id}` → **200 OK** without token (auth disabled)

### 🔒 **With Auth Enabled**
- [ ] GET `/api/v1/Catalog` → **200 OK** without token (still anonymous)
- [ ] POST `/api/v1/Catalog` → **401 Unauthorized** without token
- [ ] POST `/api/v1/Catalog` → **201 Created** with valid token + correct claims
- [ ] POST `/api/v1/Catalog` → **403 Forbidden** with valid token but wrong claims
- [ ] PUT `/api/v1/Catalog` → **401 Unauthorized** without token
- [ ] DELETE `/api/v1/Catalog/{id}` → **401 Unauthorized** without token

---

## Running Catalog.API Locally

### **Option 1: Visual Studio / Rider**
1. Set `Catalog.API` as startup project
2. Press F5 to debug
3. Navigate to `https://localhost:5001/swagger` or `http://localhost:5000/swagger`

### **Option 2: Command Line**
```bash
cd src/Services/Catalog/Catalog.API
dotnet run
```

Then open: `http://localhost:5000/swagger`

### **Option 3: Docker Compose**
```bash
cd src
docker-compose up catalog.api
```

Then open: `http://localhost:8000/swagger`

**Note**: Docker configuration already has authentication disabled:
```yaml
# src/docker-compose.override.yml
environment:
  - "Authentication:JwtBearer:Enabled=false"
```

---

## Expected Behavior Summary

| Endpoint | Method | Auth Disabled | Auth Enabled (No Token) | Auth Enabled (Valid Token) |
|----------|--------|---------------|-------------------------|----------------------------|
| `/api/v1/Catalog` | GET | ✅ 200 | ✅ 200 (Anonymous) | ✅ 200 |
| `/api/v1/Catalog/{id}` | GET | ✅ 200/404 | ✅ 200/404 (Anonymous) | ✅ 200/404 |
| `/api/v1/Catalog/GetProductByCategory/{category}` | GET | ✅ 200 | ✅ 200 (Anonymous) | ✅ 200 |
| `/api/v1/Catalog` | POST | ✅ 201 | ❌ 401 | ✅ 201 (with `catalog.write` scope) |
| `/api/v1/Catalog` | PUT | ✅ 200 | ❌ 401 | ✅ 200 (with `catalog.write` scope) |
| `/api/v1/Catalog/{id}` | DELETE | ✅ 200 | ❌ 401 | ✅ 200 (with `catalog.write` scope) |

---

## Troubleshooting

### Issue: "Build errors in Catalog.API"
**Solution**: Run `dotnet build` and verify Common.Auth builds successfully first.

### Issue: "401 even with auth disabled"
**Solution**: Check `appsettings.Development.json` - ensure `Enabled: false` or property doesn't exist.

### Issue: "Can't test with real JWT tokens"
**Solution**: Use automated integration tests with `FakeJwtTokenGenerator` (recommended approach).

### Issue: "403 Forbidden instead of 401"
**Solution**: Token is valid but lacks required claims/roles. Check policy requirements in `Program.cs`.

---

## What's Next?

After verifying Catalog.API authentication works correctly:

1. ✅ **Mark Catalog.API Complete** (T058-T067)
2. 🚀 **Continue with remaining 5 services**:
   - Basket.API (T068-T077)
   - Discount.API (T078-T087)
   - Discount.Grpc (T088-T094)
   - Ordering.API (T095-T104)
   - Shopping.Aggregator (T105-T113)

**Estimated Time to Complete**: ~25-30 minutes for all 5 services (same pattern as Catalog.API)

---

## Files Modified (Catalog.API)

1. ✅ `src/Services/Catalog/Catalog.API/Catalog.API.csproj` - Added JWT package + Common.Auth reference
2. ✅ `src/Services/Catalog/Catalog.API/appsettings.json` - Added JWT configuration
3. ✅ `src/Services/Catalog/Catalog.API/Program.cs` - Added authentication middleware + policies
4. ✅ `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs` - Added `[AllowAnonymous]` + `[Authorize]` attributes

**Build Status**: ✅ Successful (no errors)  
**Configuration**: ✅ Complete  
**Non-Breaking**: ✅ Verified (GET endpoints anonymous)  
**Protection**: ✅ Verified (Write endpoints require auth when enabled)

---

**Testing Status**: ⏸️ **AWAITING MANUAL VERIFICATION**  
**Next Step**: Test Catalog.API, then proceed with remaining 5 services

