# Implementation Plan: Testing, Authentication & Observability Infrastructure

**Branch**: `001-testing-auth-observability` | **Date**: 2025-11-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-testing-auth-observability/spec.md`

## Summary

This plan introduces comprehensive testing infrastructure (xUnit with TDD pattern, Docker-based integration tests), OAuth2/JWT authentication with policy-based authorization, and observability using Serilog + OpenTelemetry. The implementation follows a phased, non-breaking approach: existing endpoints remain anonymous, new security/observability features are opt-in, and all changes maintain backward compatibility.

## Technical Context

**Language/Version**: C# 8.0+, .NET 6.0/7.0 (verify exact version from existing projects)  
**Primary Dependencies**:
- Testing: `xUnit`, `xUnit.runner.visualstudio`, `Moq`, `FluentAssertions`, `Testcontainers`, `coverlet.collector`
- Auth: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Identity.Web` (optional for Entra ID)
- Observability: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `MassTransit.OpenTelemetry`

**Storage**: Existing (SQL Server, MongoDB, PostgreSQL, Redis) + test containers for integration tests  
**Testing**: xUnit framework, Testcontainers for Docker, coverlet for code coverage  
**Target Platform**: Linux containers (existing Docker Compose setup)  
**Project Type**: Microservices architecture (6 services: Catalog, Basket, Discount API/gRPC, Ordering, Shopping.Aggregator)

**Performance Goals**:
- Unit tests: <30 seconds total execution time
- Integration tests: <5 minutes with Docker containers
- Token validation overhead: <10ms per request
- Telemetry overhead: <5% latency increase

**Constraints**:
- **Non-breaking changes**: Existing API consumers must continue to work
- **Incremental rollout**: Auth and observability features phased in gradually
- **No public API changes**: HTTP response shapes remain unchanged
- **Backward compatibility**: Old clients without tokens can still access `[AllowAnonymous]` endpoints

**Scale/Scope**: 6 microservices, ~20-30 API endpoints, expected code coverage >80%

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Constitution Compliance

**Mandatory Patterns (from Constitution v1.1.0)**:

1. **✅ Thin Controllers (<15 lines)**: Tests will enforce this pattern
2. **✅ Input Validation**: Auth/validation tests will verify FluentValidation works correctly
3. **✅ Exception Handling**: Observability will enhance existing logging, tests will verify exception paths
4. **✅ Proper HTTP Status Codes**: Auth tests will verify 401/403, integration tests will verify all endpoints
5. **✅ Extension Methods for DI**: Auth/observability registration will follow `Add{Feature}Services()` pattern
6. **✅ Repository Pattern**: Integration tests will verify repository implementations

**New Cross-Cutting Concerns**:

7. **✅ Testing Strategy**: Introduces mandatory testing patterns (TDD, unit/integration separation)
8. **✅ Authentication/Authorization**: Fills gap in Section XIV (currently no auth implemented)
9. **✅ Observability**: Enhances Section XV (logging) and adds distributed tracing
10. **✅ Non-Breaking Changes**: Aligns with incremental improvement philosophy

### 🔍 Areas Requiring Constitution Amendment

**Add to Constitution**:
- **Section XXI: Testing Requirements** (currently empty, Section XIX notes zero tests)
  - TDD workflow mandatory for new features
  - Unit tests with mocked dependencies
  - Integration tests with Testcontainers
  - Code coverage thresholds (>80%)
  - Test categorization (traits)

- **Section XIV: Authentication & Authorization** (currently "not implemented")
  - JWT Bearer token validation
  - Policy-based authorization
  - Claims/roles from external IdP
  - Incremental rollout strategy
  - `[AllowAnonymous]` for backward compatibility

- **Section XXII: Observability & Monitoring** (enhance Section XV)
  - Structured logging with Serilog
  - OpenTelemetry for distributed tracing
  - Correlation IDs across services
  - OTLP export to backends
  - Non-breaking observability changes

### ⚠️ Legacy Exception Handling

- **Existing services lack validation/exception handling**: Tests will expose these gaps
- **Recommendation**: Add validation/exception tests first, then fix services to pass tests (TDD approach)

## Project Structure

### Documentation (this feature)

```text
specs/001-testing-auth-observability/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output (technology decisions)
├── data-model.md        # Phase 1 output (auth/telemetry entities)
├── quickstart.md        # Phase 1 output (developer guide)
└── contracts/           # Phase 1 output (auth API contracts)
    ├── jwt-validation.json      # JWT validation configuration schema
    ├── auth-policies.json       # Authorization policy definitions
    └── telemetry-schema.json    # OpenTelemetry configuration
```

### Source Code (repository root)

```text
# New Test Projects (one per service)
tests/
├── Catalog.API.Tests/              # Unit tests for Catalog
│   ├── Controllers/
│   ├── Repositories/
│   └── Catalog.API.Tests.csproj
├── Catalog.API.IntegrationTests/   # Integration tests with Docker
│   ├── Controllers/
│   ├── Fixtures/
│   └── Catalog.API.IntegrationTests.csproj
├── Basket.API.Tests/
├── Basket.API.IntegrationTests/
├── Discount.API.Tests/
├── Discount.API.IntegrationTests/
├── Ordering.Application.Tests/     # Unit tests for CQRS handlers
├── Ordering.API.IntegrationTests/
└── BuildingBlocks/
    ├── TestHelpers/                # Shared test utilities
    │   ├── JwtTestTokenGenerator.cs
    │   ├── TestContainerFixtures.cs
    │   └── FakeIdpServer.cs
    └── TestHelpers.csproj

# Shared Auth/Observability Infrastructure
src/BuildingBlocks/
├── Common.Auth/                     # NEW: Shared auth helpers
│   ├── JwtConfiguration.cs
│   ├── AuthServiceExtensions.cs
│   ├── PolicyConstants.cs
│   └── Common.Auth.csproj
└── Common.Observability/            # NEW: Shared observability
    ├── SerilogConfiguration.cs
    ├── OpenTelemetryExtensions.cs
    ├── TelemetryConstants.cs
    └── Common.Observability.csproj

# Updated Existing Services
src/Services/
├── Catalog/Catalog.API/
│   ├── Program.cs                   # Add auth + observability
│   ├── appsettings.json             # Add auth/serilog config
│   └── Controllers/                 # Add [Authorize] selectively
├── Basket/Basket.API/               # Same pattern
├── Discount/
│   ├── Discount.API/                # Same pattern
│   └── Discount.Grpc/               # Same pattern
├── Ordering/Ordering.API/           # Same pattern
└── ApiGateways/Shopping.Aggregator/ # Same pattern
```

**Structure Decision**: 

Separate test projects per service following .NET conventions:
- `{Service}.Tests` for fast, isolated unit tests
- `{Service}.IntegrationTests` for slower Docker-based tests
- Shared `TestHelpers` for common test infrastructure (JWT generation, Docker fixtures)

Shared BuildingBlocks for auth/observability to avoid duplication across 6 services.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A - Full compliance | All patterns align with constitution | N/A |

**Note**: This feature *enhances* the constitution by filling gaps (testing, auth, observability) rather than violating it.

---

## Phase 0: Research (Technology Decisions)

**Status**: To be generated  
**Output**: `research.md`

**Research Tasks**:

1. **xUnit vs NUnit vs MSTest** - Which test framework best fits ASP.NET Core microservices?
2. **Moq vs NSubstitute** - Which mocking library for unit tests?
3. **Testcontainers configuration** - How to configure Docker containers for SQL Server, MongoDB, PostgreSQL, Redis?
4. **JWT validation strategies** - Local validation vs introspection vs gateway-level?
5. **OpenID Connect providers** - Entra ID, Auth0, IdentityServer comparison for development/testing
6. **Serilog sinks** - Console, Seq, Application Insights, New Relic evaluation
7. **OpenTelemetry exporters** - OTLP vs Jaeger vs Zipkin for development
8. **MassTransit tracing** - How to instrument RabbitMQ with OpenTelemetry?
9. **Feature flag libraries** - Microsoft.FeatureManagement evaluation for incremental auth rollout
10. **Code coverage tools** - coverlet vs dotCover vs built-in Visual Studio

**Questions to Resolve**:
- Which IdP will be used for development/testing? (fake IdP, Auth0 dev tenant, or Entra ID?)
- What is the observability backend? (Jaeger local, cloud service, or just console logs initially?)
- Do we need feature flags, or is config-based toggle sufficient?
- What code coverage threshold is acceptable? (80%, 90%?)

---

## Phase 1: Design (Data Models & Contracts)

**Status**: To be generated after Phase 0  
**Prerequisites**: `research.md` complete

**Outputs**:
1. `data-model.md` - Auth/telemetry entity models
2. `contracts/jwt-validation.json` - JWT configuration schema
3. `contracts/auth-policies.json` - Authorization policy definitions
4. `contracts/telemetry-schema.json` - OpenTelemetry configuration
5. `quickstart.md` - Developer guide for testing/auth/observability

**Design Artifacts**:

### Data Model Entities

- **JwtConfiguration** (appsettings.json schema)
  - Authority (IdP URL)
  - Audience
  - RequireHttpsMetadata
  - ValidIssuers
  - ClockSkew

- **AuthPolicy** (policy definitions)
  - PolicyName
  - RequiredClaims (name + value)
  - RequiredRoles

- **TelemetryConfiguration** (OTLP settings)
  - ServiceName
  - ServiceVersion
  - OtlpEndpoint
  - ExporterType (OTLP, Jaeger, Console)

### API Contracts

**JWT Validation Endpoint** (for testing):
```
POST /api/v1/auth/validate
Authorization: Bearer {token}
Response: 200 OK { "valid": true, "claims": {...} }
```

**Protected Resource Example**:
```
GET /api/v1/catalog/products
Authorization: Bearer {token}
Response: 200 OK (if authorized), 401/403 (if not)
```

**Observability Endpoints**:
```
GET /health (health check with observability status)
GET /metrics (Prometheus-compatible metrics - optional)
```

---

## Phase 2: Implementation Tasks

**Status**: Not started (tasks generated by `/speckit.tasks` command)  
**Prerequisites**: Phase 0 + Phase 1 complete

**Output**: `tasks.md` (generated separately)

**Expected Task Categories**:
1. Setup (create test projects, add dependencies)
2. Shared Infrastructure (BuildingBlocks for auth/observability)
3. Testing Infrastructure (xUnit fixtures, Testcontainers setup, test helpers)
4. Authentication (JWT validation, policies, `[Authorize]` attributes)
5. Observability (Serilog, OpenTelemetry, correlation IDs)
6. Integration (update all 6 services)
7. CI/CD (update pipelines to run tests)
8. Documentation (update README, constitution)

---

## Constitution Amendment Plan

**Amendment Type**: MINOR version bump (1.1.0 → 1.2.0)

**New Sections to Add**:

### Section XXI: Testing Standards (Mandatory)

**Content**:
- TDD workflow required for new features
- Unit tests with mocked dependencies (xUnit + Moq)
- Integration tests with Testcontainers
- Test categorization using traits: `[Trait("Category", "Unit")]`
- Naming convention: `{ServiceName}.Tests`, `{ServiceName}.IntegrationTests`
- Code coverage >80% for new code
- Tests must run in <5 minutes total

### Section XIV: Authentication & Authorization (Update)

**Content** (replace placeholder):
- JWT Bearer token validation mandatory for protected endpoints
- External IdP for token issuance (Entra ID, Auth0, custom)
- Policy-based authorization using claims/roles
- `[AllowAnonymous]` for public endpoints
- `[Authorize]` with policies for protected endpoints
- Incremental rollout: new endpoints secured first, existing endpoints migrated gradually
- Feature flags/config for authorization enforcement
- 401 Unauthorized for invalid/missing tokens
- 403 Forbidden for insufficient permissions

### Section XXII: Observability & Monitoring (New)

**Content**:
- Structured logging with Serilog (JSON format)
- Required log properties: TraceId, SpanId, CorrelationId, ServiceName, Environment
- Minimum log levels: Information (prod), Debug (dev)
- OpenTelemetry for distributed tracing
- Instrument HTTP, gRPC, RabbitMQ calls
- OTLP exporter for telemetry export
- Logs/traces must share TraceId
- Metrics: request count, duration, error rate
- Non-breaking observability changes (no public API changes)

---

## Next Steps

1. **Review this plan** - Validate approach with team
2. **Run Phase 0** - Execute research tasks, generate `research.md`
3. **Run Phase 1** - Create data models, contracts, quickstart guide
4. **Update constitution** - Amend with testing/auth/observability standards
5. **Generate tasks** - Use `/speckit.tasks` to create implementation task list
6. **Begin implementation** - Start with shared infrastructure (BuildingBlocks)

**Branch**: `001-testing-auth-observability`  
**Estimated Effort**: 3-4 weeks (depends on team size)  
**Risk**: Integration tests with Docker may be slow; consider parallel execution

---

**Note**: This plan follows the constitution's principle of **incremental, non-breaking changes**. Existing functionality remains intact while new capabilities are introduced in opt-in mode.

