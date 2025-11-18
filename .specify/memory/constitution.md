<!--
  ============================================================================
  SYNC IMPACT REPORT
  ============================================================================
  
  Version Change: 1.1.0 → 1.2.0 (Testing, Authentication & Observability Standards)
  
  Modified Principles:
  - XIV. Authentication & Authorization → Complete implementation standards (was placeholder)
  - XIX. Testing Requirements → Comprehensive TDD/xUnit standards (was "zero tests" note)
  
  Added Sections:
  - XXII. Observability & Monitoring (NEW) - Serilog + OpenTelemetry standards
  - Testing subsections: Unit tests, Integration tests, TDD workflow, code coverage
  - Auth subsections: JWT validation, policy-based authorization, incremental rollout
  - Observability subsections: Structured logging, distributed tracing, correlation
  
  Removed Sections:
  - N/A
  
  Source Documents:
  - specs/001-testing-auth-observability/ (complete feature spec + design)
  - .specify/memory/architecture-comparison.md (previous analysis)
  
  Templates Requiring Updates:
  - ✅ plan-template.md: Constitution Check now includes testing/auth/observability gates
  - ✅ spec-template.md: Already compatible with new standards
  - ✅ tasks-template.md: Already supports testing/auth/observability tasks
  
  Follow-up TODOs:
  - Implement test projects per specs/001-testing-auth-observability/plan.md
  - Configure IdP (Entra ID or Auth0) for production
  - Set up observability backend (Jaeger/New Relic/Application Insights)
  - Add feature flags for incremental auth rollout (optional)
  
  ============================================================================
-->

# ASP.NET Microservices Constitution

## Technologies & Frameworks

### I. Core Technology Stack

**ASP.NET Core** - All microservices MUST use ASP.NET Core Web APIs:
- Controllers inherit from `ControllerBase` (API-only, no MVC views)
- Async/await pattern required for all I/O operations
- `[ApiController]` attribute on all controllers
- Minimal API style NOT used (attribute routing on controllers)

**Database Technologies Per Service**:
- **MongoDB**: Document storage (Catalog service) using MongoDB.Driver
- **Redis**: Distributed caching (Basket service) via `IDistributedCache`
- **PostgreSQL + Dapper**: Lightweight data access (Discount service)
- **SQL Server + EF Core**: Complex domain models (Ordering service)

**Rationale**: Each microservice chooses the database technology that best fits its access patterns and domain complexity. Polyglot persistence is intentional.

### II. Required Libraries

**Mapping** - AutoMapper MUST be used for all object-to-object mapping:
- Profile classes define all mappings (e.g., `BasketProfile : Profile`)
- Register with `AddAutoMapper(typeof(Program))` or `AddAutoMapper(Assembly.GetExecutingAssembly())`
- Use in constructors via `IMapper` injection

**Inter-Service Communication**:
- **gRPC** for synchronous inter-service calls (protobuf contracts required)
- **MassTransit + RabbitMQ** for asynchronous event-driven communication
- Event contracts defined in shared `BuildingBlocks/Eventbus.Messages` project

**API Documentation** - Swagger/OpenAPI MUST be configured:
- `AddSwaggerGen()` and `UseSwagger()` in all API projects
- `[ProducesResponseType]` attributes for status code documentation

**Legacy Exception**: Ordering service uses Clean Architecture with MediatR/CQRS/FluentValidation. Other services use simpler layered architecture. Full CQRS is optional, but validation/exception patterns MUST be adopted.

## Architecture & Layering Principles

### III. Microservices Architecture

**Service Independence** - Each microservice MUST:
- Own its database (no shared databases between services)
- Deploy independently
- Have its own repository and solution structure
- Expose functionality only through well-defined contracts (REST/gRPC)

**Project Structure**:
```
Services/
  [ServiceName]/
    [ServiceName].API/          # Web API layer
      Controllers/
      Program.cs
      appsettings.json
    [ServiceName].Domain/       # (Optional - for complex domains)
    [ServiceName].Application/  # (Optional - for CQRS/handlers)
    [ServiceName].Infrastructure/ # (Optional - when separating data access)
```

**Rationale**: Microservices must remain loosely coupled to enable independent scaling, deployment, and technology choices.

### IV. Layering Rules & Thin Controllers (Mandatory)

**All services MUST follow these controller rules** (regardless of architecture choice):

#### Thin Controllers Rule (MANDATORY)

Controllers MUST be thin dispatchers with minimal logic:
- **Maximum 15 lines per action** (including attributes, brackets, return statements)
- **Zero business logic** in controllers (no calculations, no loops over data, no conditionals beyond simple guards)
- **Move logic to**: Service classes (simple services) or Handlers (CQRS services)

**✅ Good Example (Ordering - 7 lines):**
```csharp
// src/Services/Ordering/Ordering.API/Controllers/OrderController.cs:23-29
[HttpGet("{userName}", Name = "GetOrder")]
[ProducesResponseType(typeof(IEnumerable<OrdersVm>), (int)HttpStatusCode.OK)]
public async Task<ActionResult<IEnumerable<OrdersVm>>> GetOrdersByUserName(string userName)
{
    var query = new GetOrdersListQuery(userName);
    var orders = await _mediator.Send(query);
    return Ok(orders);
}
```

**❌ Legacy Exception - DO NOT COPY (Basket - 16 lines, has business logic):**
```csharp
// src/Services/Basket/Basket.API/Controllers/BasketController.cs:39-48
public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
{
    // ❌ Business logic in controller
    foreach (var item in basket.Items)
    {
        var coupon = await _discountGrpcService.GetDiscount(item.ProductName);
        item.Price -= coupon.Amount;  // ❌ Price calculation
    }
    return Ok(await _repository.UpdateBasket(basket));
}
```

**Refactor business logic to service layer** for simple services without CQRS.

---

#### Simple Services (Catalog, Basket, Discount Pattern)

For services with simple domain logic, use this structure:
- `Controllers/` - Thin API endpoints (call services/repositories)
- `Services/` - Business logic, orchestration
- `Entities/` - Domain models/DTOs
- `Repositories/` - Data access abstractions
- `Data/` - Database contexts (if needed)

**Controllers** MUST:
- Depend on service/repository interfaces
- Validate input (see Section XII-A)
- Handle exceptions properly (see Section XVI)
- Return appropriate HTTP status codes

**Example**: `src/Services/Catalog/Catalog.API/` - MongoDB-based catalog service

---

#### Complex Services (Ordering Pattern - Optional)

**When to use Clean Architecture + CQRS**:
- Complex domain logic with multiple aggregates
- Clear command/query separation needed
- Business rules enforcement required
- Domain events or complex workflows

**Structure (when justified)**:
- **Domain**: Entities, value objects, domain logic
- **Application**: Commands/Queries/Handlers, validation, contracts
- **Infrastructure**: Repository implementations, DbContext, external services
- **API**: Controllers (ultra-thin dispatchers to MediatR)

**Requires Approval**: Document justification in PR when introducing Clean Architecture to new services.

**Legacy Exception**: Ordering service uses this pattern. Do NOT copy to simple CRUD services.

**Rationale**: Right-size architecture to domain complexity. Simple services stay simple, complex domains get appropriate structure.

### V. Repository Pattern (Mandatory)

**All data access** MUST go through repository pattern:
- Define `I{Entity}Repository` interface
- Implement `{Entity}Repository` concrete class
- Register as Scoped lifetime: `services.AddScoped<IRepository, Repository>()`

**Generic Repository** (optional, Ordering only):
- `IAsyncRepository<T>` provides CRUD operations
- Specific repositories inherit and add entity-specific queries
- Example: `src/Services/Ordering/Ordering.Application/Contracts/Persistence/IAsyncRepository.cs:11-27`

**Repository Responsibilities**:
- Own all database operations for an entity/aggregate
- Return domain entities, not DTOs (mapping happens in Application layer or controllers)
- Call `SaveChanges` within each repository method (no Unit of Work pattern)

**Example**: `src/Services/Catalog/Catalog.API/Repositories/ProductRepository.cs:7-13`

**Rationale**: Repository pattern provides testability, abstraction over data access, and clear separation of concerns.

### VI. Dependency Injection

**Service Registration** MUST use extension methods per layer:
- Infrastructure layer: `AddInfrastructureServices(configuration)`
- Application layer: `AddApplicationServices()` (if using separate application layer)
- Register in `Program.cs` during startup

**Example (recommended pattern from Ordering)**:
```csharp
// src/Services/Ordering/Ordering.Application/ApplicationServiceRegistration.cs:22-32
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddAutoMapper(Assembly.GetExecutingAssembly());
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    services.AddMediatR(Assembly.GetExecutingAssembly());
    
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    
    return services;
}
```

**Lifetime Rules**:
- **Scoped**: All repositories, DbContexts, business services
- **Transient**: MediatR pipeline behaviors, stateless utilities
- **Singleton**: Configuration, cached data only

**Constructor Injection Only**:
- All dependencies injected via constructor
- No property injection or service locator pattern
- Interfaces preferred over concrete types

**Rationale**: Extension methods encapsulate layer configuration, improve testability, and make dependencies explicit.

## Data Access Principles

### VII. DbContext Usage (EF Core Only)

**Registration** MUST happen in Infrastructure layer extension method:
```csharp
services.AddDbContext<OrderContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("ConnectionString")));
```

**Injection** into repositories via constructor:
```csharp
protected readonly OrderContext _dbContext;
public RepositoryBase(OrderContext dbContext) { _dbContext = dbContext; }
```

**SaveChanges Override** for audit fields (RECOMMENDED):
- Override `SaveChangesAsync` to populate `CreatedDate`, `CreatedBy`, `LastModifiedDate`, `LastModifiedBy`
- Use `ChangeTracker.Entries<EntityBase>()` to intercept entity state changes
- Example: `src/Services/Ordering/Order.Infrastructure/Persistence/OrderContext.cs:20-38`

**Rationale**: Centralized DbContext configuration and automatic audit field population improve consistency.

### VIII. Query Patterns

**EF Core (SQL Server)** - LINQ queries exclusively:
- Use `IQueryable<T>` for composable queries
- `.AsNoTracking()` for read-only queries (enabled by default in repositories)
- `.Include()` for eager loading (support both string and expression-based)
- Generic `Set<T>()` for generic repositories
- Example: `src/Services/Ordering/Order.Infrastructure/Repositories/RepositoryBase.cs:33-44`

**Dapper (PostgreSQL)** - Raw SQL with parameterization:
- Create new `NpgsqlConnection` per operation with `using` statement
- Use `QueryFirstOrDefaultAsync<T>()` for single results
- Use `ExecuteAsync()` for INSERT/UPDATE/DELETE
- Always use anonymous objects for parameters: `new { ProductName = productName }`
- Example: `src/Services/Discount/Discount.API/Repositories/DiscountRepository.cs:18-24`

**MongoDB** - Native driver API:
- Use `Find(predicate)` with lambda expressions for simple queries
- Use `FilterDefinition<T>` builders for complex queries
- Direct operations: `InsertOneAsync()`, `ReplaceOneAsync()`, `DeleteOneAsync()`
- Example: `src/Services/Catalog/Catalog.API/Repositories/ProductRepository.cs:16-28`

**Redis** - String-based with JSON serialization:
- Use `IDistributedCache` abstraction (not direct Redis client)
- Manual JSON serialization with `JsonConvert.SerializeObject/DeserializeObject`
- Username or unique key as cache key
- Example: `src/Services/Basket/Basket.API/Repositories/BasketRepository.cs:21-34`

**Rationale**: Each technology optimizes for its specific access patterns. EF Core for complex queries, Dapper for performance, MongoDB driver for document operations.

### IX. Transaction Handling

**Current Practice** - No explicit transactions:
- `SaveChanges()` called after each operation (implicit transaction)
- No `BeginTransaction()`, `TransactionScope`, or multi-operation transactions
- Each repository method commits independently

**Example**: `src/Services/Ordering/Order.Infrastructure/Repositories/RepositoryBase.cs:66-83`

**Future Consideration**: If multi-repository transactions needed, implement Unit of Work pattern at Application layer.

**Rationale**: Current simplicity sufficient for single-entity operations. Explicit transactions add complexity.

### X. Database Migration

**Migration at Startup** via extension methods on `IHost`:
- SQL Server (EF Core): `app.MigrateDatabase<TContext>(seeder)` with Polly retry
- PostgreSQL (Dapper): `app.MigrateDatabase<TContext>()` with manual DDL
- MongoDB: Seed data in context constructor
- Redis: No migration needed

**Retry Logic Required**:
- Exponential backoff for SQL Server (Polly library)
- Recursive retry for PostgreSQL (manual implementation)

**Examples**:
- `src/Services/Ordering/Ordering.API/Extensions/HostExtensions.cs:9-44`
- `src/Services/Discount/Discount.API/Extensions/HostExtensions.cs:11-65`

**Rationale**: Container orchestration may start services before databases are ready. Retry logic ensures eventual consistency.

## API Design Principles

### XI. Routing & Attributes

**Attribute Routing Only** (no conventional routing):
- `[ApiController]` on all controllers
- `[Route("api/v1/[controller]")]` pattern for versioning
- HTTP verb attributes: `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`
- Route parameters: `[HttpGet("{id}", Name = "GetResource")]`
- Route constraints: `{id:length(24)}` for MongoDB ObjectIds

**Named Routes** for `CreatedAtRoute()` responses:
```csharp
[HttpGet("{id}", Name = "GetProduct")]
[HttpPost]
public async Task<ActionResult> Create([FromBody] Product product) {
    await _repository.CreateProduct(product);
    return CreatedAtRoute("GetProduct", new { id = product.Id }, product);
}
```

**Example**: `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs:8-32`

**Rationale**: Explicit attribute routing is clearer and enables API versioning.

### XII. Model Binding & Validation

**Model Binding**:
- Complex objects: Explicit `[FromBody]` attribute required
- Route parameters: Implicit binding (no attribute needed)
- Query parameters: Implicit binding

---

### XII-A. Input Validation (Mandatory)

**⚠️ BREAKING WITH LEGACY**: All write operations (POST/PUT/PATCH) MUST validate input.

**Validation Approaches** (choose based on architecture):

#### Option 1: FluentValidation (Recommended - from Ordering)

**For CQRS services** - Use MediatR pipeline:
```csharp
// Validator
public class CheckoutOrderCommandValidator : AbstractValidator<CheckoutOrderCommand>
{
    public CheckoutOrderCommandValidator() 
    {
        RuleFor(p => p.UserName)
            .NotEmpty().WithMessage("{UserName} is required.")
            .MaximumLength(50).WithMessage("{UserName} must not exceed 50 characters.");
        
        RuleFor(p => p.TotalPrice)
            .GreaterThan(0).WithMessage("{TotalPrice} should be greater than zero.");
    }
}

// Registration (in AddApplicationServices)
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
```

**For simple services** - Manual validation:
```csharp
// In controller or service layer
private readonly IValidator<CreateProductRequest> _validator;

public async Task<ActionResult> CreateProduct([FromBody] CreateProductRequest request)
{
    var validationResult = await _validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return BadRequest(validationResult.Errors);
    }
    // ... proceed with creation
}
```

**Example**: `src/Services/Ordering/Ordering.Application/Features/Orders/Commands/CheckoutOrder/CheckoutOrderCommandValidator.cs:10-26`

#### Option 2: Data Annotations (Acceptable for simple cases)

```csharp
public class CreateProductRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }
    
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
}

// Controller checks ModelState
if (!ModelState.IsValid)
{
    return BadRequest(ModelState);
}
```

**❌ Legacy Exception - DO NOT COPY**: Catalog, Basket, Discount have **zero validation**. This is a security risk and MUST NOT be replicated.

**Rationale**: Input validation prevents invalid data from entering the system, provides clear error messages, and improves security.

---

### XIII. Response Patterns

**Return Types**:
- Prefer `ActionResult<T>` for type safety and documentation
- Use `IActionResult` when returning different types

**Success Responses**:
- `Ok(data)` for 200 with body
- `CreatedAtRoute(name, routeValues, data)` for 201
- `NoContent()` for 204 (successful DELETE/PUT with no content)
- `Accepted()` for 202 (async operations)

---

### XIII-A. Error Response Rules (Mandatory)

**🚫 FORBIDDEN: Ok(bool) Pattern**

**Never return `Ok(true)` or `Ok(false)` for operations**. Use proper HTTP status codes:

**❌ BAD (Legacy Exception from Catalog):**
```csharp
// src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs:63-65
[HttpPut]
public async Task<IActionResult> UpdateProduct([FromBody] Product product)
{
    return Ok(await _repository.UpdateProduct(product));  // ❌ Returns Ok(bool)
}
```

**✅ GOOD:**
```csharp
[HttpPut("{id}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> UpdateProduct(string id, [FromBody] Product product)
{
    var success = await _repository.UpdateProduct(product);
    if (!success)
    {
        return NotFound();
    }
    return NoContent();  // ✅ Proper REST response
}
```

**HTTP Status Code Rules**:
- **404 NotFound()**: Resource doesn't exist (check before operations)
- **400 BadRequest()**: Invalid input (validation failures, business rule violations)
- **204 NoContent()**: Successful operation with no response body (PUT/DELETE)
- **500 (automatic)**: Server errors (unhandled exceptions)

**Null Checks Required**:
- GET by ID: Return `NotFound()` if entity doesn't exist
- UPDATE: Return `NotFound()` if entity doesn't exist
- DELETE: Return `NotFound()` if entity doesn't exist

**❌ Legacy Exception - DO NOT COPY**: Catalog only checks nulls on GetById (line 35), but not on other operations. All operations must check.

---

**ProducesResponseType** MUST document status codes:
```csharp
[HttpGet("{id}")]
[ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
public async Task<ActionResult<Product>> GetById(string id) { ... }
```

**Examples**:
- Good: `src/Services/Ordering/Ordering.API/Controllers/OrderController.cs:41-48`
- Bad (legacy): `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs:63-72`

**Rationale**: Proper HTTP status codes are essential for REST API design and client-side error handling.

### XIV. Authentication & Authorization (Mandatory for New Endpoints)

**Implementation Status**: Transitioning from no auth to OAuth2/JWT Bearer

**Architecture**: All microservices are **resource APIs** that validate JWT access tokens:
- JWTs issued by external OpenID Connect provider (IdP): Entra ID, Auth0, or custom
- APIs use `Microsoft.AspNetCore.Authentication.JwtBearer` for token validation
- Authorization is **policy-based** using claims/roles from JWT

---

#### JWT Bearer Token Validation (Mandatory)

**Configuration** (appsettings.json):
```json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://login.microsoftonline.com/{tenantId}/v2.0",
      "Audience": "api://your-api-client-id",
      "RequireHttpsMetadata": true,
      "ValidIssuers": [
        "https://login.microsoftonline.com/{tenantId}/v2.0"
      ],
      "ClockSkew": "00:05:00",
      "ValidateLifetime": true
    }
  }
}
```

**Registration** (Program.cs):
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:JwtBearer:Authority"];
        options.Audience = builder.Configuration["Authentication:JwtBearer:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Authentication:JwtBearer:RequireHttpsMetadata");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// ... later in pipeline
app.UseAuthentication();
app.UseAuthorization();
```

**Validation Rules**:
- **Signature**: Must be valid using JWKS from IdP
- **Issuer (iss)**: Must match configured valid issuers
- **Audience (aud)**: Must match configured audience
- **Expiration (exp)**: Must be in the future (with clock skew tolerance)
- **Not Before (nbf)**: Must be in the past

---

#### Policy-Based Authorization (Mandatory)

**Define Policies** (Program.cs):
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadCatalog", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "catalog.read")
              .RequireRole("CatalogReader", "CatalogAdmin"));
    
    options.AddPolicy("WriteCatalog", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "catalog.write")
              .RequireRole("CatalogAdmin"));
    
    options.AddPolicy("AdminAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin", "SuperAdmin"));
});
```

**Apply Policies** (Controllers):
```csharp
// New endpoint - protected from day 1
[Authorize(Policy = "ReadCatalog")]
[HttpGet("new-endpoint")]
public async Task<ActionResult> NewSecureEndpoint() { ... }

// Existing endpoint - remains anonymous during Phase 1
[AllowAnonymous]
[HttpGet("existing-endpoint")]
public async Task<ActionResult> ExistingPublicEndpoint() { ... }
```

---

#### Incremental Rollout Strategy (Non-Breaking)

**Phase 1**: Existing endpoints remain `[AllowAnonymous]` to avoid breaking changes:
- New endpoints MUST use `[Authorize]` with appropriate policies
- Existing endpoints keep `[AllowAnonymous]` attribute
- Opt-in model: authentication infrastructure deployed but not enforced everywhere

**Phase 2** (Future): Migrate existing endpoints:
- Add `[Authorize]` to existing endpoints one-by-one
- Coordinate with API consumers (provide migration window)
- Optional: Use feature flags to toggle enforcement per endpoint

**Configuration Toggle** (optional):
```json
{
  "Features": {
    "EnableAuthentication": true,
    "RequireAuthForExistingEndpoints": false  // Phase 1: false, Phase 2: true
  }
}
```

---

#### HTTP Status Codes

**Authentication/Authorization Responses**:
- **401 Unauthorized**: Missing, invalid, or expired token
- **403 Forbidden**: Valid token but insufficient permissions (missing claims/roles)

**Example**:
```csharp
// Automatic from middleware:
// - Missing token → 401
// - Invalid signature → 401
// - Expired token → 401
// - Valid token, wrong role → 403

// Manual check (if needed):
if (!User.IsInRole("Admin"))
{
    return Forbid();  // Returns 403
}
```

---

#### Best Practices

**DO**:
- ✅ Use HTTPS in production (`RequireHttpsMetadata = true`)
- ✅ Validate token lifetime, issuer, audience, signature
- ✅ Use policy-based authorization (not hard-coded roles)
- ✅ Keep tokens short-lived (1-hour max recommended)
- ✅ Use claims for fine-grained permissions
- ✅ Test with fake JWT generator in integration tests

**DON'T**:
- ❌ Skip token validation
- ❌ Use symmetric keys (HS256) in production (use RSA/ECDSA)
- ❌ Hard-code roles/permissions in controller logic
- ❌ Remove `[AllowAnonymous]` from existing endpoints without coordination
- ❌ Store tokens in localStorage (use httpOnly cookies or memory)
- ❌ Use `[Authorize]` without specifying policy (use explicit policies)

---

**References**:
- JWT validation config schema: `specs/001-testing-auth-observability/contracts/jwt-validation.json`
- Policy definitions schema: `specs/001-testing-auth-observability/contracts/auth-policies.json`
- Implementation plan: `specs/001-testing-auth-observability/plan.md`
- Quickstart guide: `specs/001-testing-auth-observability/quickstart.md`

**Rationale**: OAuth2/JWT Bearer is industry standard for API authentication. Policy-based authorization provides flexibility. Incremental rollout prevents breaking existing API consumers.

## Cross-Cutting Concerns

### XV. Logging

**ILogger<T> Required**:
- Inject `ILogger<TClass>` via constructor in all controllers, services, handlers
- Use structured logging with placeholders: `_logger.LogInformation("Order {OrderId} created", orderId)`
- Log levels:
  - `LogInformation`: Successful operations, business events
  - `LogError`: Exceptions, failures (with exception object)
  - `LogWarning`: Degraded functionality
  - `LogDebug`: Detailed diagnostics (not in production)

**Examples**:
- `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs:37`
- `src/Services/Ordering/Ordering.Application/Features/Orders/Commands/CheckoutOrder/CheckoutOrderCommandHandler.cs:36`

**Rationale**: Centralized logging essential for microservices observability and debugging.

### XVI. Exception Handling (Mandatory)

**⚠️ BREAKING WITH LEGACY**: All services MUST implement centralized exception handling.

**Current State**: Only Ordering has exception handling. Others let exceptions bubble unhandled.

---

#### Golden Path: Exception Pipeline (from Ordering)

**For CQRS services** - Use MediatR pipeline behavior:
```csharp
// src/Services/Ordering/Ordering.Application/Behaviours/UnhandledExceptionBehaviour.cs:11-34
public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<TRequest> _logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogError(ex, "Application Request: Unhandled Exception for Request {Name} {@Request}", requestName, request);
            throw;  // Re-throw for framework, but logged
        }
    }
}
```

**For simple services** - Use global exception middleware:
```csharp
// Future pattern (to be implemented)
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        logger.LogError(exception, "Unhandled exception occurred");
        
        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    });
});
```

---

#### Custom Domain Exceptions (Recommended)

Define business-specific exceptions:

```csharp
// src/Services/Ordering/Ordering.Application/Exceptions/NotFoundException.cs:9-16
public class NotFoundException : ApplicationException
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    { }
}

// Usage:
if (order == null)
{
    throw new NotFoundException(nameof(Order), request.Id);
}
```

**Required Exception Types**:
- `NotFoundException` - Entity not found (maps to 404)
- `ValidationException` - Business rule violation (maps to 400)
- `BusinessRuleException` - Domain logic violation (maps to 400)

**❌ Legacy Exception - DO NOT COPY**: Catalog/Basket/Discount have no exception handling. Exceptions bubble unhandled and return generic 500 errors.

**⚠️ Inconsistency in Ordering**: UpdateOrderCommandHandler logs but doesn't throw when order not found (should throw NotFoundException like DeleteOrderCommandHandler).

**Rationale**: Centralized exception handling provides consistent error responses, improves observability, and separates infrastructure concerns from business logic.

### XVII. AutoMapper Configuration

**Registration** per assembly:
```csharp
services.AddAutoMapper(typeof(Program)); // Scan Program's assembly
// OR
services.AddAutoMapper(Assembly.GetExecutingAssembly());
```

**Profile Classes** MUST:
- Inherit from `Profile`
- Define mappings in constructor
- Use `ReverseMap()` for bidirectional mapping when appropriate

**Examples**:
- `src/Services/Basket/Basket.API/Mapper/BasketProfile.cs:7-14`
- `src/Services/Ordering/Ordering.Application/Mappings/MappingProfile.cs:14-22`

**Rationale**: AutoMapper eliminates manual mapping code and centralizes mapping configuration.

### XVIII. Inter-Service Communication

**gRPC for Synchronous Calls**:
- Define `.proto` contracts in service providing the API
- Register gRPC client in consuming service: `AddGrpcClient<TClient>(o => o.Address = new Uri(url))`
- Wrap gRPC client in service class (e.g., `DiscountGrpcService`)
- Example: `src/Services/Basket/Basket.API/GrpcServices/DiscountGrpcService.cs:5-19`

**MassTransit + RabbitMQ for Async Events**:
- Define event contracts in `BuildingBlocks/Eventbus.Messages/Events/`
- Events inherit from `IntegrationBaseEvent` (Id, CreationDate)
- Publishers use `IPublishEndpoint.Publish<TEvent>(eventMessage)`
- Consumers implement `IConsumer<TEvent>` with `Consume(ConsumeContext<TEvent>)`

**Examples**:
- Publisher: `src/Services/Basket/Basket.API/Controllers/BasketController.cs:78-80`
- Consumer: `src/Services/Ordering/Ordering.API/EventBusConsumer/BasketCheckoutConsumer.cs:9-28`

**Rationale**: gRPC for low-latency synchronous calls, message bus for decoupled asynchronous communication.

## Testing Requirements

### XIX. Testing Standards (Mandatory for New Code)

**Implementation Status**: Transitioning from zero tests to comprehensive test coverage

**Test Framework**: xUnit for all test projects  
**Mocking**: Moq for unit test mocks  
**Integration Tests**: Testcontainers for Docker-based database tests  
**Code Coverage**: coverlet + ReportGenerator (target: >80%)

---

#### Test Project Structure (Mandatory)

**Naming Convention**:
- Unit tests: `{ServiceName}.Tests` (e.g., `Catalog.API.Tests`)
- Integration tests: `{ServiceName}.IntegrationTests` (e.g., `Catalog.API.IntegrationTests`)

**Directory Structure**:
```
tests/
├── {ServiceName}.Tests/                # Unit tests (fast, mocked dependencies)
│   ├── Controllers/
│   │   └── CatalogControllerTests.cs
│   ├── Repositories/
│   │   └── ProductRepositoryTests.cs
│   └── {ServiceName}.Tests.csproj
│
├── {ServiceName}.IntegrationTests/     # Integration tests (real databases)
│   ├── Controllers/
│   │   └── CatalogControllerIntegrationTests.cs
│   ├── Fixtures/
│   │   └── DatabaseFixture.cs
│   └── {ServiceName}.IntegrationTests.csproj
│
└── BuildingBlocks/
    └── TestHelpers/                     # Shared test utilities
        ├── FakeJwtTokenGenerator.cs
        ├── TestContainerFixtures.cs
        └── TestHelpers.csproj
```

---

#### Unit Tests (Mandatory)

**Purpose**: Fast, isolated tests with mocked dependencies

**Requirements**:
- MUST mock all external dependencies (databases, HTTP clients, message queues)
- MUST execute in <30 seconds total
- MUST use `[Trait("Category", "Unit")]` attribute

**Example**:
```csharp
public class ProductRepositoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange
        var mockContext = new Mock<ICatalogContext>();
        var mockCollection = new Mock<IMongoCollection<Product>>();
        
        mockContext.Setup(c => c.Products).Returns(mockCollection.Object);
        mockCollection.Setup(c => c.Find(It.IsAny<FilterDefinition<Product>>()))
            .Returns(/* mock cursor */);
        
        var repository = new ProductRepository(mockContext.Object);
        
        // Act
        var product = await repository.GetProduct("123");
        
        // Assert
        product.Should().NotBeNull();
        product.Id.Should().Be("123");
    }
}
```

**Run Command**: `dotnet test --filter Category=Unit`

---

#### Integration Tests (Mandatory)

**Purpose**: Test with real databases running in Docker containers

**Requirements**:
- MUST use Testcontainers for database lifecycle
- MUST execute in <5 minutes total
- MUST use `[Trait("Category", "Integration")]` attribute
- MUST clean up containers after tests

**Example**:
```csharp
public class CatalogIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    
    public CatalogIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateProduct_WithValidData_PersistsToDatabase()
    {
        // Arrange: Use real MongoDB from Testcontainer
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
        var product = new { Name = "Test Product", Price = 99.99 };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/catalog", product);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdProduct = await response.Content.ReadFromJsonAsync<Product>();
        createdProduct.Name.Should().Be("Test Product");
    }
}
```

**Run Command**: `dotnet test --filter Category=Integration`

---

#### TDD Workflow (Mandatory for New Features)

**Red-Green-Refactor Cycle**:

1. **Red** - Write failing test first:
```csharp
[Fact]
public async Task DeleteProduct_WithNonExistentId_ReturnsNotFound()
{
    // Arrange
    var mockRepo = new Mock<IProductRepository>();
    mockRepo.Setup(r => r.DeleteProduct("999")).ReturnsAsync(false);
    var controller = new CatalogController(mockRepo.Object, Mock.Of<ILogger<CatalogController>>());
    
    // Act
    var result = await controller.DeleteProductById("999");
    
    // Assert
    result.Should().BeOfType<NotFoundResult>();  // ❌ This will fail initially
}
```

2. **Green** - Implement minimum code to pass:
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteProductById(string id)
{
    var success = await _repository.DeleteProduct(id);
    if (!success)
    {
        return NotFound();  // ✅ Now test passes
    }
    return NoContent();
}
```

3. **Refactor** - Improve while keeping tests green

**Rule**: Never commit failing tests. Tests MUST pass before code review.

---

#### Code Coverage (Target: >80%)

**Measurement**:
```bash
# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80

# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html

# View report
start coveragereport/index.html
```

**Thresholds**:
- **Line coverage**: >80% for new code
- **Branch coverage**: >75% for new code
- **Existing legacy code**: Exempt from coverage requirements (improve incrementally)

**CI/CD Gate**: Pipeline MUST fail if coverage drops below threshold

---

#### Test Categorization (Mandatory)

**Use xUnit Traits**:
```csharp
[Trait("Category", "Unit")]        // Fast, isolated
[Trait("Category", "Integration")]  // Docker-based
[Trait("Category", "Contract")]     // API contract tests (future)
[Trait("Category", "E2E")]          // End-to-end (future)
```

**Run Specific Categories**:
```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter "Category=Unit|Category=Integration"
```

---

#### Best Practices

**DO**:
- ✅ Follow TDD: Write test first, see it fail, make it pass
- ✅ Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- ✅ One logical assert per test (or use FluentAssertions chaining)
- ✅ Mock external dependencies in unit tests
- ✅ Use real databases in integration tests (via Testcontainers)
- ✅ Clean up test data/containers after tests
- ✅ Run tests in CI/CD pipeline
- ✅ Aim for >80% code coverage on new code

**DON'T**:
- ❌ Share state between tests (tests must be independent)
- ❌ Use `Thread.Sleep` (use async/await properly)
- ❌ Test implementation details (test behavior, not internals)
- ❌ Commit failing tests
- ❌ Skip tests with `[Fact(Skip = "reason")]` long-term
- ❌ Use in-memory databases for integration tests (use real DBs)
- ❌ Mix unit and integration tests in same project

---

**References**:
- Testing plan: `specs/001-testing-auth-observability/plan.md`
- Quickstart guide: `specs/001-testing-auth-observability/quickstart.md`
- Research decisions: `specs/001-testing-auth-observability/research.md`

**Rationale**: TDD improves design and prevents regressions. Unit tests provide fast feedback. Integration tests validate real-world scenarios. Code coverage ensures adequate testing.

## Anti-Patterns to Avoid

### XX. Forbidden Practices

**DO NOT**:

#### Architecture Anti-Patterns
- ❌ Share databases between microservices (violates service independence)
- ❌ Use conventional routing (attribute routing required)
- ❌ Inherit from `Controller` (use `ControllerBase` for APIs)
- ❌ Introduce Clean Architecture without justification (keep simple services simple)

#### Controller Anti-Patterns (from architecture comparison)
- ❌ **Business logic in controllers** (calculations, loops, complex conditionals)
  - **Bad Example**: `src/Services/Basket/Basket.API/Controllers/BasketController.cs:43-47` (price calculation in controller)
  - **Fix**: Move to service layer
- ❌ **Fat controllers** (>15 lines per action)
  - **Fix**: Extract logic to service/handler classes
- ❌ **Multiple responsibilities** (controller doing too much)
  - **Fix**: Single Responsibility Principle - delegate to services

#### Response Anti-Patterns
- ❌ **Return `Ok(bool)` for operations** (use proper HTTP status codes)
  - **Bad Example**: `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs:65, 72`
  - **Fix**: Return `NoContent()` on success, `NotFound()` on failure
- ❌ **Inconsistent null checking** (check in some methods but not others)
  - **Bad Example**: Catalog checks nulls only in GetById, not in Update/Delete
  - **Fix**: Check nulls consistently across all operations
- ❌ **Skip `[ProducesResponseType]` documentation attributes**
  - **Fix**: Document all possible status codes

#### Validation Anti-Patterns
- ❌ **No validation** on write operations
  - **Bad Example**: All services except Ordering have zero validation
  - **Fix**: Add FluentValidation or data annotations
- ❌ **Inconsistent validation** (validate some properties but not others)
  - **Fix**: Validate all required fields and business rules

#### Exception Handling Anti-Patterns
- ❌ **No exception logging** (let exceptions bubble silently)
  - **Bad Example**: Catalog/Basket/Discount have no exception handling
  - **Fix**: Add global exception middleware or pipeline behavior
- ❌ **Swallow exceptions** without logging
  - **Fix**: Always log before re-throwing or handling

#### Dependency Injection Anti-Patterns
- ❌ Create manual mapping code (use AutoMapper)
- ❌ Inject `IConfiguration` directly into repositories (use options pattern)
- ❌ Use service locator pattern (constructor injection required)
- ❌ Register all services in Program.cs (use extension methods per layer)

---

**Legacy Exceptions Documented But NOT to Repeat**:

These patterns exist in the codebase but MUST NOT be copied to new code:

1. **❌ No validation** (Catalog, Basket, Discount) - New code MUST validate
2. **❌ Business logic in controllers** (Basket price calculation) - New code MUST use services
3. **❌ Ok(bool) returns** (Catalog Update/Delete) - New code MUST use proper status codes
4. **❌ Inconsistent error handling** (Catalog checks nulls sometimes) - New code MUST be consistent
5. **❌ No exception logging** (most services) - New code MUST log exceptions
6. **❌ Mixed concerns in Program.cs** (simple services) - New code SHOULD use extension methods

**Rationale**: These anti-patterns reduce maintainability, testability, consistency, and security across the codebase.

---

## Observability & Monitoring

### XXI. Structured Logging with Serilog (Mandatory)

**Implementation Status**: Transitioning from basic ILogger to Serilog with structured JSON logging

**Logging Library**: Serilog.AspNetCore for all services  
**Output Format**: JSON for machine-parseable logs  
**Correlation**: TraceId/SpanId from OpenTelemetry for cross-service correlation

---

#### Serilog Configuration (Mandatory)

**Installation**:
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.CorrelationId
```

**Configuration** (Program.cs):
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

**Configuration** (appsettings.json):
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
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
    "Enrich": ["FromLogContext", "WithEnvironmentName", "WithCorrelationId"],
    "Properties": {
      "Application": "Catalog.API"
    }
  }
}
```

---

#### Structured Logging Best Practices (Mandatory)

**DO** - Use message templates with properties:
```csharp
// ✅ GOOD: Structured properties
_logger.LogInformation(
    "Product {ProductId} retrieved by user {UserId} in {ElapsedMs}ms",
    productId, userId, elapsed);

// ✅ GOOD: Exception with context
_logger.LogError(
    ex,
    "Failed to create order {OrderId} for user {UserId}",
    orderId, userId);
```

**DON'T** - Use string interpolation:
```csharp
// ❌ BAD: String interpolation (not searchable by properties)
_logger.LogInformation($"Product {productId} retrieved by user {userId}");

// ❌ BAD: No context for exception
_logger.LogError(ex, "An error occurred");
```

**Required Log Properties**:
- **TraceId**: Correlation across services (from OpenTelemetry Activity)
- **SpanId**: Correlation within service
- **ServiceName**: Which service generated the log
- **Environment**: Development/Staging/Production
- **UserId**: For authenticated requests (from JWT claims)
- **CorrelationId**: Custom client-initiated correlation

**Minimum Log Levels**:
- **Production**: Information (errors/warnings/info only)
- **Development**: Debug (detailed diagnostics)
- **Never log**: Passwords, tokens, PII, credit card numbers

---

### XXII. Distributed Tracing with OpenTelemetry (Mandatory)

**Implementation Status**: Adding OpenTelemetry instrumentation for distributed tracing

**Tracing Library**: OpenTelemetry.Extensions.Hosting  
**Export Protocol**: OTLP (OpenTelemetry Protocol)  
**Backend**: Jaeger (local dev), Application Insights/New Relic (production)

---

#### OpenTelemetry Configuration (Mandatory)

**Installation**:
```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package MassTransit.OpenTelemetry  # For RabbitMQ tracing
```

**Configuration** (Program.cs):
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName: builder.Configuration["Telemetry:ServiceName"],
                        serviceVersion: builder.Configuration["Telemetry:ServiceVersion"]))
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = (httpContext) => !httpContext.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)  // RabbitMQ
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

**Configuration** (appsettings.json):
```json
{
  "Telemetry": {
    "ServiceName": "Catalog.API",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://jaeger:4317",
    "Protocol": "Grpc",
    "EnableTracing": true,
    "EnableMetrics": true,
    "SamplingProbability": 1.0
  }
}
```

---

#### Trace Instrumentation (Automatic + Manual)

**Automatic Instrumentation** (built-in):
- HTTP requests (incoming/outgoing)
- HTTP client calls
- gRPC calls
- RabbitMQ messages (via MassTransit.OpenTelemetry)
- Database calls (EF Core, MongoDB driver)

**Manual Spans** (for custom operations):
```csharp
using System.Diagnostics;

[HttpGet("{id}")]
public async Task<ActionResult<Product>> GetProduct(string id)
{
    using var activity = Activity.Current?.Source.StartActivity("GetProduct.Custom");
    activity?.SetTag("product.id", id);
    activity?.SetTag("user.id", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    
    try
    {
        var product = await _repository.GetProduct(id);
        
        if (product == null)
        {
            activity?.SetTag("product.found", false);
            activity?.SetStatus(ActivityStatusCode.Error, "Product not found");
            return NotFound();
        }
        
        activity?.SetTag("product.found", true);
        activity?.SetTag("product.name", product.Name);
        activity?.SetTag("product.price", product.Price);
        
        return Ok(product);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

---

#### Correlation Between Logs and Traces (Mandatory)

**Serilog + OpenTelemetry Integration**:

Logs and traces MUST share the same TraceId for correlation:

```csharp
// Serilog automatically enriches logs with Activity.Current TraceId
_logger.LogInformation("Processing order {OrderId}", orderId);
// → Output includes: "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736"
```

**Log Output** (JSON with TraceId):
```json
{
  "@t": "2025-11-16T10:30:45.1234567Z",
  "@mt": "Processing order {OrderId}",
  "@l": "Information",
  "OrderId": "12345",
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",  // ← Same as OpenTelemetry trace
  "SpanId": "00f067aa0ba902b7",
  "ServiceName": "Ordering.API",
  "Environment": "Production"
}
```

**End-to-End Correlation**:
1. Request arrives at API Gateway → TraceId created
2. TraceId propagated to Basket service (HTTP headers: `traceparent`)
3. TraceId propagated to RabbitMQ message (MassTransit adds headers)
4. TraceId propagated to Ordering service (consumer receives headers)
5. All logs and spans share same TraceId → Full request flow visible

---

#### Observability Best Practices

**DO**:
- ✅ Use structured logging (properties, not string interpolation)
- ✅ Include TraceId in all logs (automatic with OpenTelemetry + Serilog)
- ✅ Add custom spans for important business operations
- ✅ Tag spans with relevant business context (order ID, user ID, product ID)
- ✅ Record exceptions in spans
- ✅ Sample traces in production (1-10% for high traffic)
- ✅ Export telemetry to centralized backend (Jaeger/Application Insights/New Relic)
- ✅ Include observability endpoint: `/health`

**DON'T**:
- ❌ Log sensitive data (passwords, tokens, credit cards, SSNs)
- ❌ Use string interpolation in logs
- ❌ Create too many custom spans (adds overhead)
- ❌ Sample at 100% in production high-traffic services (performance impact)
- ❌ Ignore trace context propagation (breaks correlation)
- ❌ Log at Debug level in production (noise)
- ❌ Forget to include exception details in error logs

---

#### Non-Breaking Change Principle (Mandatory)

**Phase 1 Observability Changes**:
- Logging and tracing changes MUST NOT alter HTTP response shapes
- Response bodies, status codes, headers remain unchanged
- Observability is infrastructure concern, not API contract change
- Existing API consumers unaffected

**Allowed**:
- ✅ Add `traceparent` response header (W3C standard, non-breaking)
- ✅ Add internal logging/tracing code
- ✅ Export telemetry to backend

**NOT Allowed**:
- ❌ Change response JSON structure to include tracing info
- ❌ Require clients to send tracing headers (support optional headers only)
- ❌ Break existing API contracts

---

**References**:
- Telemetry config schema: `specs/001-testing-auth-observability/contracts/telemetry-schema.json`
- Implementation plan: `specs/001-testing-auth-observability/plan.md`
- Quickstart guide: `specs/001-testing-auth-observability/quickstart.md`

**Rationale**: Structured logging enables querying by properties. OpenTelemetry provides vendor-neutral distributed tracing. TraceId correlation enables end-to-end request flow visualization. Non-breaking changes ensure existing clients continue to work.

---

## Governance

**Constitution Authority**: This constitution defines the architectural standards for all new code in this repository. Existing code may not comply (legacy exceptions noted above), but all new features MUST follow these principles.

**Golden Path (Ordering Module)**: The Ordering service demonstrates superior patterns in:
- Thin controllers (<15 lines)
- Input validation (FluentValidation)
- Exception handling (pipeline behaviors, domain exceptions)
- Separation of concerns (CQRS optional, but patterns extractable)
- Proper HTTP status codes
- Service registration (extension methods)

**Architecture Comparison**: See `.specify/memory/architecture-comparison.md` for detailed analysis of Ordering vs other modules.

**Amendment Process**:
1. Propose changes via pull request to `.specify/memory/constitution.md`
2. Document rationale for change
3. Update version number (semantic versioning)
4. Update affected templates in `.specify/templates/`
5. Obtain team approval before merge

**Compliance Review**:
- All pull requests MUST reference constitution compliance
- Code reviews MUST verify adherence to principles
- Legacy exceptions MUST NOT be copied to new code
- New patterns MUST be justified and documented before adoption

**Version Bumping Rules**:
- **MAJOR** (X.0.0): Backward-incompatible changes, principle removals/redefinitions
- **MINOR** (0.X.0): New principles added, materially expanded guidance
- **PATCH** (0.0.X): Clarifications, wording fixes, non-semantic refinements

**Complexity Justification**:
- Simple CRUD services default to layered architecture (Catalog/Basket/Discount pattern)
- Complex domains may justify Clean Architecture (Ordering pattern) with explicit approval
- Validation, exception handling, thin controllers are MANDATORY regardless of architecture choice
- All architecture decisions MUST include rationale in PR description

**Development Guidance**: Use this constitution when creating specifications, implementation plans, and task lists. Reference specific principles in code reviews.

---

**Version**: 1.2.0 | **Ratified**: 2025-11-16 | **Last Amended**: 2025-11-16
