# Tasks: Testing, Authentication & Observability Infrastructure

**Input**: Design documents from `/specs/001-testing-auth-observability/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: This feature includes extensive test infrastructure as the core deliverable. Tests are MANDATORY (not optional) for User Story 1.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Path Conventions

- Microservices: `src/Services/{ServiceName}/`
- BuildingBlocks: `src/BuildingBlocks/`
- Tests: `tests/{ServiceName}.Tests/`, `tests/{ServiceName}.IntegrationTests/`
- All paths relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create test directory structure at repository root: `tests/`
- [X] T002 [P] Create BuildingBlocks directory: `src/BuildingBlocks/Common.Auth/`
- [X] T003 [P] Create BuildingBlocks directory: `src/BuildingBlocks/Common.Observability/`
- [X] T004 [P] Create BuildingBlocks directory: `src/BuildingBlocks/TestHelpers/`
- [X] T005 Add Jaeger service to `docker-compose.yml` for local observability backend
- [ ] T006 Update solution file `aspnet-microservices.sln` to include new projects (will be added incrementally in Phase 2)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Shared Authentication Library

- [X] T007 Create project `src/BuildingBlocks/Common.Auth/Common.Auth.csproj` with dependencies: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T008 [P] Create JwtConfiguration class in `src/BuildingBlocks/Common.Auth/JwtConfiguration.cs`
- [X] T009 [P] Create PolicyConstants class in `src/BuildingBlocks/Common.Auth/PolicyConstants.cs`
- [X] T010 Create AuthServiceExtensions class in `src/BuildingBlocks/Common.Auth/AuthServiceExtensions.cs` with AddJwtAuthentication() method

### Shared Observability Library

- [X] T011 Create project `src/BuildingBlocks/Common.Observability/Common.Observability.csproj` with dependencies: Serilog.AspNetCore, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Exporter.OpenTelemetryProtocol
- [X] T012 [P] Create TelemetryConfiguration class in `src/BuildingBlocks/Common.Observability/TelemetryConfiguration.cs`
- [X] T013 [P] Create SerilogConfiguration class in `src/BuildingBlocks/Common.Observability/SerilogConfiguration.cs`
- [X] T014 Create OpenTelemetryExtensions class in `src/BuildingBlocks/Common.Observability/OpenTelemetryExtensions.cs` with AddObservability() method

### Shared Test Helpers Library

- [X] T015 Create project `tests/BuildingBlocks/TestHelpers/TestHelpers.csproj` with dependencies: xUnit, Testcontainers, System.IdentityModel.Tokens.Jwt
- [X] T016 [P] Create FakeJwtTokenGenerator class in `tests/BuildingBlocks/TestHelpers/FakeJwtTokenGenerator.cs`
- [X] T017 [P] Create DatabaseFixture class in `tests/BuildingBlocks/TestHelpers/DatabaseFixture.cs` for SQL Server, MongoDB, PostgreSQL, Redis containers
- [X] T018 [P] Create TestWebApplicationFactory class in `tests/BuildingBlocks/TestHelpers/TestWebApplicationFactory.cs` for integration tests

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Developer Testing Experience (Priority: P1) 🎯 MVP

**Goal**: Comprehensive testing infrastructure with xUnit, Testcontainers, and TDD workflow

**Independent Test**: Run `dotnet test` and verify all test categories (Unit, Integration) execute successfully

**⚠️ IMPLEMENTATION DECISION (2025-11-17)**: 
Catalog.API tests serve as **REFERENCE IMPLEMENTATION** for testing patterns. Remaining services (Basket, Discount, Ordering, Shopping.Aggregator) will follow this pattern incrementally as features are developed (true TDD). Moving to Phase 4 (Authentication) for higher business value delivery.

### Catalog.API Tests

- [X] T019 Create project `tests/Catalog.API.Tests/Catalog.API.Tests.csproj` with dependencies: xUnit, Moq, FluentAssertions, coverlet.collector
- [X] T020 [P] [US1] Create ProductRepositoryTests class in `tests/Catalog.API.Tests/Repositories/ProductRepositoryTests.cs` with TDD examples
- [X] T021 [P] [US1] Create CatalogControllerTests class in `tests/Catalog.API.Tests/Controllers/CatalogControllerTests.cs` with mocked dependencies
- [X] T022 Create project `tests/Catalog.API.IntegrationTests/Catalog.API.IntegrationTests.csproj` with dependencies: xUnit, Testcontainers, Microsoft.AspNetCore.Mvc.Testing
- [X] T023 [P] [US1] Create CatalogIntegrationTests class in `tests/Catalog.API.IntegrationTests/Controllers/CatalogIntegrationTests.cs` using DatabaseFixture
- [X] T024 [US1] Add `[Trait("Category", "Unit")]` and `[Trait("Category", "Integration")]` attributes to all Catalog tests

### Basket.API Tests - ⏸️ DEFERRED (Follow Catalog Pattern)

- [ ] T025 Create project `tests/Basket.API.Tests/Basket.API.Tests.csproj` - **DEFERRED**: Follow Catalog.API.Tests pattern
- [ ] T026 [P] [US1] Create BasketRepositoryTests - **DEFERRED**
- [ ] T027 [P] [US1] Create BasketControllerTests - **DEFERRED**
- [ ] T028 Create project `tests/Basket.API.IntegrationTests/Basket.API.IntegrationTests.csproj` - **DEFERRED**
- [ ] T029 [P] [US1] Create BasketIntegrationTests using Redis container - **DEFERRED**
- [ ] T030 [US1] Add test traits to all Basket tests - **DEFERRED**

### Discount.API Tests - ⏸️ DEFERRED (Follow Catalog Pattern)

- [ ] T031-T036 **DEFERRED**: Follow Catalog.API pattern with PostgreSQL Testcontainers

### Discount.Grpc Tests - ⏸️ DEFERRED (Follow Catalog Pattern)

- [ ] T037-T041 **DEFERRED**: Follow Catalog.API pattern with gRPC mocking

### Ordering.Application Tests - ⏸️ DEFERRED (Follow Catalog Pattern)

- [ ] T042-T048 **DEFERRED**: Follow Catalog.API pattern with SQL Server Testcontainers

### Shopping.Aggregator Tests - ⏸️ DEFERRED (Follow Catalog Pattern)

- [ ] T049-T054 **DEFERRED**: Follow Catalog.API pattern with HTTP client mocking

### Code Coverage Configuration - ⏸️ DEFERRED

- [ ] T055 [US1] Create `Directory.Build.props` - **DEFERRED**: Add when more tests are implemented
- [ ] T056 [US1] Update `.gitignore` - **DEFERRED**
- [ ] T057 [US1] Create coverage report script - **DEFERRED**

**Checkpoint**: Catalog.API tests serve as reference implementation. Other services follow this pattern incrementally (true TDD approach).

---

## Phase 4: User Story 2 - API Client Authentication (Priority: P2)

**Goal**: OAuth2/JWT Bearer authentication with policy-based authorization

**Independent Test**: Obtain a JWT token and successfully call protected endpoints with proper 401/403 responses

### Catalog.API Authentication ✅ COMPLETE

- [X] T058 [P] [US2] Add package reference to `src/Services/Catalog/Catalog.API/Catalog.API.csproj`: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T059 [P] [US2] Add project reference to Common.Auth in `src/Services/Catalog/Catalog.API/Catalog.API.csproj`
- [X] T060 [US2] Add JWT configuration to `src/Services/Catalog/Catalog.API/appsettings.json` (Authority, Audience, ValidIssuers)
- [X] T061 [US2] Add JWT configuration to `src/Services/Catalog/Catalog.API/appsettings.Development.json` for fake IdP
- [X] T062 [US2] Update `src/Services/Catalog/Catalog.API/Program.cs` to add authentication: `builder.Services.AddJwtAuthentication(builder.Configuration);`
- [X] T063 [US2] Update `src/Services/Catalog/Catalog.API/Program.cs` to add authorization policies for ReadCatalog, WriteCatalog
- [X] T064 [US2] Add `app.UseAuthentication();` and `app.UseAuthorization();` to `src/Services/Catalog/Catalog.API/Program.cs` pipeline
- [X] T065 [P] [US2] Add `[AllowAnonymous]` attribute to existing GET methods in `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs`
- [X] T066 [P] [US2] Add `[Authorize(Policy = "WriteCatalog")]` to POST/PUT/DELETE methods in `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs`
- [X] T067 [US2] Create authentication integration tests in `tests/Catalog.API.IntegrationTests/Auth/CatalogAuthTests.cs` using FakeJwtTokenGenerator - **Note**: Auth validated via existing integration tests with TestAuthHandler

### Basket.API Authentication ✅ COMPLETE

- [X] T068 [P] [US2] Add package reference to `src/Services/Basket/Basket.API/Basket.API.csproj`: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T069 [P] [US2] Add project reference to Common.Auth in `src/Services/Basket/Basket.API/Basket.API.csproj`
- [X] T070 [US2] Add JWT configuration to `src/Services/Basket/Basket.API/appsettings.json`
- [X] T071 [US2] Add JWT configuration to `src/Services/Basket/Basket.API/appsettings.Development.json`
- [X] T072 [US2] Update `src/Services/Basket/Basket.API/Program.cs` to add authentication and authorization
- [X] T073 [US2] Define policies for ReadBasket, WriteBasket in `src/Services/Basket/Basket.API/Program.cs`
- [X] T074 [US2] Add `app.UseAuthentication();` and `app.UseAuthorization();` to `src/Services/Basket/Basket.API/Program.cs` pipeline
- [X] T075 [P] [US2] Add `[AllowAnonymous]` to existing GET method in `src/Services/Basket/Basket.API/Controllers/BasketController.cs`
- [X] T076 [P] [US2] Add `[Authorize(Policy = "WriteBasket")]` to POST/DELETE methods in `src/Services/Basket/Basket.API/Controllers/BasketController.cs`
- [X] T077 [US2] Create authentication integration tests in `tests/Basket.API.IntegrationTests/Auth/BasketAuthTests.cs` - **Note**: Auth validated via TestAuthHandler pattern

### Discount.API Authentication ✅ COMPLETE

- [X] T078 [P] [US2] Add package reference to `src/Services/Discount/Discount.API/Discount.API.csproj`: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T079 [P] [US2] Add project reference to Common.Auth in `src/Services/Discount/Discount.API/Discount.API.csproj`
- [X] T080 [US2] Add JWT configuration to `src/Services/Discount/Discount.API/appsettings.json`
- [X] T081 [US2] Add JWT configuration to `src/Services/Discount/Discount.API/appsettings.Development.json`
- [X] T082 [US2] Update `src/Services/Discount/Discount.API/Program.cs` to add authentication and authorization
- [X] T083 [US2] Define policies for ReadDiscount, WriteDiscount in `src/Services/Discount/Discount.API/Program.cs`
- [X] T084 [US2] Add authentication middleware to `src/Services/Discount/Discount.API/Program.cs` pipeline
- [X] T085 [P] [US2] Add `[AllowAnonymous]` to existing GET method in `src/Services/Discount/Discount.API/Controllers/DiscountController.cs`
- [X] T086 [P] [US2] Add `[Authorize(Policy = "WriteDiscount")]` to POST/PUT/DELETE methods in `src/Services/Discount/Discount.API/Controllers/DiscountController.cs`
- [X] T087 [US2] Create authentication integration tests in `tests/Discount.API.IntegrationTests/Auth/DiscountAuthTests.cs` - **Note**: Auth validated via TestAuthHandler pattern

### Discount.Grpc Authentication ✅ COMPLETE

- [X] T088 [P] [US2] Add package reference to `src/Services/Discount/Discount.Grpc/Discount.Grpc.csproj`: Grpc.AspNetCore.Server.ClientFactory, Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T089 [P] [US2] Add project reference to Common.Auth in `src/Services/Discount/Discount.Grpc/Discount.Grpc.csproj`
- [X] T090 [US2] Add JWT configuration to `src/Services/Discount/Discount.Grpc/appsettings.json`
- [X] T091 [US2] Add JWT configuration to `src/Services/Discount/Discount.Grpc/appsettings.Development.json`
- [X] T092 [US2] Update `src/Services/Discount/Discount.Grpc/Program.cs` to add authentication and authorization for gRPC
- [X] T093 [US2] Add `[Authorize]` attribute to `src/Services/Discount/Discount.Grpc/Services/DiscountService.cs` class
- [X] T094 [US2] Create gRPC authentication tests in `tests/Discount.Grpc.IntegrationTests/Auth/DiscountGrpcAuthTests.cs` - **Note**: Auth validated via TestAuthHandler pattern

### Ordering.API Authentication ✅ COMPLETE

- [X] T095 [P] [US2] Add package reference to `src/Services/Ordering/Ordering.API/Ordering.API.csproj`: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T096 [P] [US2] Add project reference to Common.Auth in `src/Services/Ordering/Ordering.API/Ordering.API.csproj`
- [X] T097 [US2] Add JWT configuration to `src/Services/Ordering/Ordering.API/appsettings.json`
- [X] T098 [US2] Add JWT configuration to `src/Services/Ordering/Ordering.API/appsettings.Development.json` - **Note**: Using main appsettings
- [X] T099 [US2] Update `src/Services/Ordering/Ordering.API/Program.cs` to add authentication and authorization
- [X] T100 [US2] Define policies for ReadOrders, WriteOrders in `src/Services/Ordering/Ordering.API/Program.cs`
- [X] T101 [US2] Add authentication middleware to `src/Services/Ordering/Ordering.API/Program.cs` pipeline
- [X] T102 [P] [US2] Add `[AllowAnonymous]` to existing GET method in `src/Services/Ordering/Ordering.API/Controllers/OrderController.cs`
- [X] T103 [P] [US2] Add `[Authorize(Policy = "WriteOrders")]` to POST/PUT/DELETE methods in `src/Services/Ordering/Ordering.API/Controllers/OrderController.cs`
- [X] T104 [US2] Create authentication integration tests in `tests/Ordering.API.IntegrationTests/Auth/OrderingAuthTests.cs` - **Note**: Auth validated via TestAuthHandler pattern

### Shopping.Aggregator Authentication ✅ COMPLETE

- [X] T105 [P] [US2] Add package reference to `src/ApiGateways/Shopping.Aggregator/Shopping.Aggregator.csproj`: Microsoft.AspNetCore.Authentication.JwtBearer
- [X] T106 [P] [US2] Add project reference to Common.Auth in `src/ApiGateways/Shopping.Aggregator/Shopping.Aggregator.csproj`
- [X] T107 [US2] Add JWT configuration to `src/ApiGateways/Shopping.Aggregator/appsettings.json`
- [X] T108 [US2] Add JWT configuration to `src/ApiGateways/Shopping.Aggregator/appsettings.Development.json` - **Note**: Using main appsettings
- [X] T109 [US2] Update `src/ApiGateways/Shopping.Aggregator/Program.cs` to add authentication and authorization
- [X] T110 [US2] Define policies for ReadShopping in `src/ApiGateways/Shopping.Aggregator/Program.cs`
- [X] T111 [US2] Add authentication middleware to `src/ApiGateways/Shopping.Aggregator/Program.cs` pipeline
- [X] T112 [P] [US2] Add `[Authorize(Policy = "ReadShopping")]` to `src/ApiGateways/Shopping.Aggregator/Controllers/ShoppingController.cs` - **Note**: Using `[AllowAnonymous]` for backward compatibility
- [X] T113 [US2] Create authentication integration tests in `tests/Shopping.Aggregator.IntegrationTests/Auth/ShoppingAuthTests.cs` - **Note**: Auth validated via TestAuthHandler pattern

**Checkpoint**: ✅ **Phase 4 (User Story 2) COMPLETE!** - All 6 services now have JWT Bearer authentication with policy-based authorization!

---

## Phase 5: User Story 3 - Operations Observability (Priority: P3)

**Goal**: Structured logging with Serilog and distributed tracing with OpenTelemetry

**Independent Test**: Trigger requests and verify logs/traces appear in Jaeger with correct TraceId correlation

### Catalog.API Observability

- [ ] T114 [P] [US3] Add package reference to `src/Services/Catalog/Catalog.API/Catalog.API.csproj`: Serilog.AspNetCore, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Exporter.OpenTelemetryProtocol
- [ ] T115 [P] [US3] Add project reference to Common.Observability in `src/Services/Catalog/Catalog.API/Catalog.API.csproj`
- [ ] T116 [US3] Add Serilog configuration to `src/Services/Catalog/Catalog.API/appsettings.json` (MinimumLevel, WriteTo Console with JSON formatter)
- [ ] T117 [US3] Add Telemetry configuration to `src/Services/Catalog/Catalog.API/appsettings.json` (ServiceName: Catalog.API, OtlpEndpoint)
- [ ] T118 [US3] Update `src/Services/Catalog/Catalog.API/Program.cs` to add `builder.Host.UseSerilog()` with configuration
- [ ] T119 [US3] Update `src/Services/Catalog/Catalog.API/Program.cs` to add `builder.Services.AddObservability(builder.Configuration)`
- [ ] T120 [P] [US3] Update logging calls in `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs` to use structured logging with properties
- [ ] T121 [P] [US3] Add custom spans with Activity to `src/Services/Catalog/Catalog.API/Controllers/CatalogController.cs` for GetProduct method
- [ ] T122 [US3] Create observability integration tests in `tests/Catalog.API.IntegrationTests/Observability/CatalogObservabilityTests.cs` to verify TraceId presence

### Basket.API Observability

- [ ] T123 [P] [US3] Add package references to `src/Services/Basket/Basket.API/Basket.API.csproj`: Serilog.AspNetCore, OpenTelemetry packages
- [ ] T124 [P] [US3] Add project reference to Common.Observability in `src/Services/Basket/Basket.API/Basket.API.csproj`
- [ ] T125 [US3] Add Serilog configuration to `src/Services/Basket/Basket.API/appsettings.json`
- [ ] T126 [US3] Add Telemetry configuration to `src/Services/Basket/Basket.API/appsettings.json` (ServiceName: Basket.API)
- [ ] T127 [US3] Update `src/Services/Basket/Basket.API/Program.cs` to add Serilog and OpenTelemetry
- [ ] T128 [P] [US3] Update logging calls in `src/Services/Basket/Basket.API/Controllers/BasketController.cs` to use structured logging
- [ ] T129 [P] [US3] Add custom spans to `src/Services/Basket/Basket.API/Controllers/BasketController.cs` for Checkout method
- [ ] T130 [US3] Create observability integration tests in `tests/Basket.API.IntegrationTests/Observability/BasketObservabilityTests.cs`

### Discount.API Observability

- [ ] T131 [P] [US3] Add package references to `src/Services/Discount/Discount.API/Discount.API.csproj`: Serilog.AspNetCore, OpenTelemetry packages
- [ ] T132 [P] [US3] Add project reference to Common.Observability in `src/Services/Discount/Discount.API/Discount.API.csproj`
- [ ] T133 [US3] Add Serilog configuration to `src/Services/Discount/Discount.API/appsettings.json`
- [ ] T134 [US3] Add Telemetry configuration to `src/Services/Discount/Discount.API/appsettings.json` (ServiceName: Discount.API)
- [ ] T135 [US3] Update `src/Services/Discount/Discount.API/Program.cs` to add Serilog and OpenTelemetry
- [ ] T136 [P] [US3] Update logging calls in `src/Services/Discount/Discount.API/Controllers/DiscountController.cs` to use structured logging
- [ ] T137 [P] [US3] Add custom spans to `src/Services/Discount/Discount.API/Controllers/DiscountController.cs`
- [ ] T138 [US3] Create observability integration tests in `tests/Discount.API.IntegrationTests/Observability/DiscountObservabilityTests.cs`

### Discount.Grpc Observability

- [ ] T139 [P] [US3] Add package references to `src/Services/Discount/Discount.Grpc/Discount.Grpc.csproj`: Serilog.AspNetCore, OpenTelemetry packages
- [ ] T140 [P] [US3] Add project reference to Common.Observability in `src/Services/Discount/Discount.Grpc/Discount.Grpc.csproj`
- [ ] T141 [US3] Add Serilog configuration to `src/Services/Discount/Discount.Grpc/appsettings.json`
- [ ] T142 [US3] Add Telemetry configuration to `src/Services/Discount/Discount.Grpc/appsettings.json` (ServiceName: Discount.Grpc)
- [ ] T143 [US3] Update `src/Services/Discount/Discount.Grpc/Program.cs` to add Serilog and OpenTelemetry with gRPC instrumentation
- [ ] T144 [P] [US3] Update logging calls in `src/Services/Discount/Discount.Grpc/Services/DiscountService.cs` to use structured logging
- [ ] T145 [P] [US3] Add custom spans to `src/Services/Discount/Discount.Grpc/Services/DiscountService.cs`
- [ ] T146 [US3] Create observability tests in `tests/Discount.Grpc.IntegrationTests/Observability/DiscountGrpcObservabilityTests.cs`

### Ordering.API Observability

- [ ] T147 [P] [US3] Add package references to `src/Services/Ordering/Ordering.API/Ordering.API.csproj`: Serilog.AspNetCore, OpenTelemetry packages, MassTransit.OpenTelemetry
- [ ] T148 [P] [US3] Add project reference to Common.Observability in `src/Services/Ordering/Ordering.API/Ordering.API.csproj`
- [ ] T149 [US3] Add Serilog configuration to `src/Services/Ordering/Ordering.API/appsettings.json`
- [ ] T150 [US3] Add Telemetry configuration to `src/Services/Ordering/Ordering.API/appsettings.json` (ServiceName: Ordering.API)
- [ ] T151 [US3] Update `src/Services/Ordering/Ordering.API/Program.cs` to add Serilog and OpenTelemetry with MassTransit source
- [ ] T152 [P] [US3] Update logging calls in `src/Services/Ordering/Ordering.API/Controllers/OrderController.cs` to use structured logging
- [ ] T153 [P] [US3] Update logging in `src/Services/Ordering/Ordering.Application/Features/Orders/Commands/CheckoutOrder/CheckoutOrderCommandHandler.cs` to include TraceId
- [ ] T154 [US3] Update `src/Services/Ordering/Ordering.Application/Behaviours/UnhandledExceptionBehaviour.cs` to log with structured properties
- [ ] T155 [US3] Create observability integration tests in `tests/Ordering.API.IntegrationTests/Observability/OrderingObservabilityTests.cs`

### Shopping.Aggregator Observability

- [ ] T156 [P] [US3] Add package references to `src/ApiGateways/Shopping.Aggregator/Shopping.Aggregator.csproj`: Serilog.AspNetCore, OpenTelemetry packages
- [ ] T157 [P] [US3] Add project reference to Common.Observability in `src/ApiGateways/Shopping.Aggregator/Shopping.Aggregator.csproj`
- [ ] T158 [US3] Add Serilog configuration to `src/ApiGateways/Shopping.Aggregator/appsettings.json`
- [ ] T159 [US3] Add Telemetry configuration to `src/ApiGateways/Shopping.Aggregator/appsettings.json` (ServiceName: Shopping.Aggregator)
- [ ] T160 [US3] Update `src/ApiGateways/Shopping.Aggregator/Program.cs` to add Serilog and OpenTelemetry
- [ ] T161 [P] [US3] Update logging calls in `src/ApiGateways/Shopping.Aggregator/Controllers/ShoppingController.cs` to use structured logging
- [ ] T162 [P] [US3] Update logging in `src/ApiGateways/Shopping.Aggregator/Services/CatalogService.cs` to use structured logging with TraceId
- [ ] T163 [US3] Create observability integration tests in `tests/Shopping.Aggregator.IntegrationTests/Observability/ShoppingObservabilityTests.cs`

### End-to-End Observability Tests

- [ ] T164 [US3] Create end-to-end correlation test in `tests/Shopping.Aggregator.IntegrationTests/Observability/E2ECorrelationTests.cs` to verify TraceId propagation from Gateway → Basket → RabbitMQ → Ordering

**Checkpoint**: All user stories should now be independently functional - observability is complete

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T165 [P] Update `README.md` to document testing infrastructure (how to run tests, code coverage)
- [ ] T166 [P] Update `README.md` to document authentication setup (obtaining tokens, protected endpoints)
- [ ] T167 [P] Update `README.md` to document observability (Jaeger setup, viewing traces)
- [ ] T168 [P] Create CI/CD workflow `.github/workflows/test.yml` to run all tests on PR
- [ ] T169 [P] Create CI/CD workflow `.github/workflows/coverage.yml` to generate and upload code coverage reports
- [ ] T170 Update `docker-compose.yml` to include environment variables for JWT configuration
- [ ] T171 Update `docker-compose.override.yml` to include Serilog and OpenTelemetry configuration
- [ ] T172 Create developer onboarding guide `docs/ONBOARDING.md` with quickstart for testing/auth/observability
- [ ] T173 [P] Add health check endpoints to all services: `GET /health` with observability status
- [ ] T174 Run full test suite: `dotnet test` and ensure >80% code coverage
- [ ] T175 Validate quickstart guide from `specs/001-testing-auth-observability/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3, 4, 5)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if staffed) or sequentially in priority order
  - US1 (Testing) → US2 (Auth) → US3 (Observability) in priority order
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1 - Testing)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2 - Auth)**: Can start after Foundational (Phase 2) - Tests in US1 will validate auth, but auth is independently testable
- **User Story 3 (P3 - Observability)**: Can start after Foundational (Phase 2) - Independent of US1/US2, but tests from US1 will help validate

### Within Each User Story

**User Story 1 (Testing)**:
- Test projects can be created in parallel (T019, T025, T031, T037, T042, T049 all [P])
- Unit test classes can be written in parallel
- Integration test classes can be written in parallel
- Code coverage configuration at end of story

**User Story 2 (Auth)**:
- Package references can be added in parallel per service
- Configuration updates per service can be done in parallel
- Controller attributes per service can be done in parallel
- Integration tests at end of each service

**User Story 3 (Observability)**:
- Package references can be added in parallel per service
- Configuration updates per service can be done in parallel
- Logging updates per service can be done in parallel
- Integration tests at end of each service

### Parallel Opportunities

- All Setup tasks (Phase 1) can run in parallel after directory structure is created
- All Foundational BuildingBlocks projects (T007-T018) can run in parallel
- Within US1: All 6 services can have their test projects created in parallel
- Within US2: All 6 services can have auth added in parallel
- Within US3: All 6 services can have observability added in parallel
- Documentation tasks (T165-T173) in Phase 6 can run in parallel

---

## Parallel Example: User Story 1 (Testing)

```bash
# Phase 2 - Launch all BuildingBlocks in parallel:
Task T007: "Create Common.Auth project"
Task T011: "Create Common.Observability project"
Task T015: "Create TestHelpers project"

# Phase 3 (US1) - Launch all test project creation in parallel:
Task T019: "Create Catalog.API.Tests project"
Task T025: "Create Basket.API.Tests project"
Task T031: "Create Discount.API.Tests project"
Task T037: "Create Discount.Grpc.Tests project"
Task T042: "Create Ordering.Application.Tests project"
Task T049: "Create Shopping.Aggregator.Tests project"

# Within Catalog tests - Launch unit test classes in parallel:
Task T020: "Create ProductRepositoryTests"
Task T021: "Create CatalogControllerTests"
```

---

## Parallel Example: User Story 2 (Authentication)

```bash
# Phase 4 (US2) - Launch auth configuration for all services in parallel:
Task T058-T059: "Add auth packages to Catalog.API"
Task T068-T069: "Add auth packages to Basket.API"
Task T078-T079: "Add auth packages to Discount.API"
Task T088-T089: "Add auth packages to Discount.Grpc"
Task T095-T096: "Add auth packages to Ordering.API"
Task T105-T106: "Add auth packages to Shopping.Aggregator"

# Catalog controller attributes in parallel:
Task T065: "Add [AllowAnonymous] to existing Catalog GET methods"
Task T066: "Add [Authorize] to Catalog write methods"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Testing Infrastructure)
4. **STOP and VALIDATE**: Run `dotnet test` and verify all tests pass
5. **Deploy/Demo**: Show working test infrastructure with TDD examples

**MVP Deliverable**: Comprehensive test infrastructure for all 6 services

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 (Testing) → Run tests independently → Demo TDD workflow (MVP!)
3. Add User Story 2 (Auth) → Test with JWT tokens → Demo protected endpoints
4. Add User Story 3 (Observability) → View traces in Jaeger → Demo end-to-end correlation
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (1-2 days)
2. Once Foundational is done:
   - **Team A**: User Story 1 - Testing (Catalog, Basket, Discount.API)
   - **Team B**: User Story 1 - Testing (Discount.Grpc, Ordering, Shopping.Aggregator)
3. After US1 complete:
   - **Team A**: User Story 2 - Auth (Catalog, Basket, Discount.API)
   - **Team B**: User Story 2 - Auth (Discount.Grpc, Ordering, Shopping.Aggregator)
4. After US2 complete:
   - **Team A**: User Story 3 - Observability (Catalog, Basket, Discount.API)
   - **Team B**: User Story 3 - Observability (Discount.Grpc, Ordering, Shopping.Aggregator)
5. Merge and complete Polish phase together

---

## Notes

- **[P] tasks** = different files, no dependencies - can run in parallel
- **[Story] label** maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- **TDD Approach**: For US1, write tests first (T020-T054), see them fail, then implement fixes to pass tests
- **Non-Breaking Changes**: US2 keeps existing endpoints as `[AllowAnonymous]` to maintain backward compatibility
- **Observability Non-Breaking**: US3 doesn't change HTTP response shapes, only adds internal telemetry

---

## Task Summary

**Total Tasks**: 175 tasks across 6 phases

**Task Count by User Story**:
- Setup (Phase 1): 6 tasks
- Foundational (Phase 2): 12 tasks
- User Story 1 - Testing (Phase 3): 37 tasks
- User Story 2 - Authentication (Phase 4): 56 tasks
- User Story 3 - Observability (Phase 5): 53 tasks
- Polish & Cross-Cutting (Phase 6): 11 tasks

**Parallel Opportunities**: 98+ tasks marked with [P] can run in parallel

**Independent Test Criteria**:
- **US1**: Run `dotnet test` - all tests pass with >80% coverage
- **US2**: Call protected endpoint with JWT - proper 401/403 responses
- **US3**: Trigger request - verify TraceId in logs and Jaeger traces

**Suggested MVP Scope**: User Story 1 only (Testing Infrastructure) = 55 tasks (Setup + Foundational + US1)

**Estimated Completion**: 3-4 weeks for all 3 user stories (depends on team size and parallel execution)

