# Feature Specification: Testing, Authentication & Observability Infrastructure

**Feature Branch**: `001-testing-auth-observability`  
**Created**: 2025-11-16  
**Status**: Draft  
**Input**: User description: "Add xUnit tests with TDD pattern, integration tests with docker test containers, OAuth2/JWT authentication, and observability (Serilog + OpenTelemetry)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer Testing Experience (Priority: P1)

As a developer, I need a comprehensive testing infrastructure so that I can verify my code works correctly before deployment.

**Why this priority**: Foundation for code quality and confidence in changes. Without tests, the team cannot safely refactor or add features.

**Independent Test**: Can be fully tested by running `dotnet test` and verifying all test categories execute successfully.

**Acceptance Scenarios**:

1. **Given** I have written a new service method, **When** I run unit tests, **Then** the method is tested in isolation with mocked dependencies
2. **Given** I have multiple services, **When** I run integration tests, **Then** the tests execute with real databases running in Docker containers
3. **Given** tests pass locally, **When** CI/CD pipeline runs, **Then** tests execute successfully in the pipeline environment
4. **Given** I follow TDD, **When** I write a test first, **Then** it fails initially and passes after implementation

---

### User Story 2 - API Client Authentication (Priority: P2)

As an API client (mobile app/SPA/service), I need to authenticate using OAuth2/JWT tokens so that I can securely access protected resources.

**Why this priority**: Security is critical but can be phased in incrementally without breaking existing anonymous access.

**Independent Test**: Can be tested by obtaining a JWT token from IdP and successfully calling protected endpoints.

**Acceptance Scenarios**:

1. **Given** I have a valid JWT token, **When** I call a protected endpoint with Authorization header, **Then** the request succeeds
2. **Given** I have an expired JWT token, **When** I call a protected endpoint, **Then** I receive 401 Unauthorized
3. **Given** I have no token, **When** I call an `[AllowAnonymous]` endpoint, **Then** the request succeeds
4. **Given** I have a token without required claims, **When** I call a policy-protected endpoint, **Then** I receive 403 Forbidden

---

### User Story 3 - Operations Observability (Priority: P3)

As an operations engineer, I need structured logging and distributed tracing so that I can diagnose issues across microservices.

**Why this priority**: Enhances operations but doesn't block feature development or security.

**Independent Test**: Can be tested by triggering requests and verifying logs/traces appear in the observability backend with correct correlation IDs.

**Acceptance Scenarios**:

1. **Given** a request hits the API Gateway, **When** it flows through multiple services, **Then** all logs share the same TraceId
2. **Given** an exception occurs in a service, **When** I search logs by TraceId, **Then** I see the full request context and error details
3. **Given** services are running, **When** I query the tracing backend, **Then** I see service dependencies and latency metrics
4. **Given** structured logs are enabled, **When** I query by properties (e.g., UserId, OrderId), **Then** I can filter relevant log entries

---

### Edge Cases

- What happens when IdP is unreachable during token validation?
- How does system handle malformed JWT tokens?
- What happens when Docker test containers fail to start?
- How are test data conflicts handled in parallel test execution?
- What happens when observability backend (OTLP collector) is down?
- How are integration tests isolated from each other?

## Requirements *(mandatory)*

### Functional Requirements

#### Testing (P1)

- **FR-001**: System MUST support xUnit test framework for all microservices
- **FR-002**: Unit tests MUST mock all external dependencies (databases, HTTP clients, message queues)
- **FR-003**: Integration tests MUST use Testcontainers for Docker to spin up real databases
- **FR-004**: Tests MUST be categorized (Unit, Integration, Contract) using xUnit traits
- **FR-005**: Test projects MUST follow naming convention: `{ServiceName}.Tests` for unit, `{ServiceName}.IntegrationTests`
- **FR-006**: TDD workflow MUST be supported: Red (fail) → Green (pass) → Refactor
- **FR-007**: Code coverage reporting MUST be available via coverlet

#### Authentication & Authorization (P2)

- **FR-008**: All microservices MUST validate JWT access tokens using `Microsoft.AspNetCore.Authentication.JwtBearer`
- **FR-009**: System MUST support external OpenID Connect providers (Entra ID, Auth0, or custom IdP)
- **FR-010**: Authorization MUST be policy-based using claims/roles from JWT
- **FR-011**: Existing endpoints MUST remain `[AllowAnonymous]` initially (non-breaking)
- **FR-012**: New endpoints MUST be protected with `[Authorize]` and appropriate policies
- **FR-013**: Authorization enforcement MUST be configurable per endpoint (feature flags optional)
- **FR-014**: System MUST return 401 for missing/invalid tokens, 403 for insufficient permissions
- **FR-015**: JWT validation MUST verify signature, issuer, audience, and expiration

#### Observability (P3)

- **FR-016**: System MUST use Serilog for structured logging in JSON format
- **FR-017**: Logs MUST include: TraceId, SpanId, CorrelationId, ServiceName, Environment, Timestamp
- **FR-018**: Minimum log level MUST be Information in production, Debug in development
- **FR-019**: System MUST use OpenTelemetry for distributed tracing
- **FR-020**: All HTTP requests, gRPC calls, and RabbitMQ messages MUST be traced
- **FR-021**: Telemetry MUST be exported via OTLP protocol
- **FR-022**: Logs and traces MUST share the same TraceId for correlation
- **FR-023**: Metrics MUST be collected for request count, duration, error rate
- **FR-024**: Observability changes MUST NOT alter HTTP response shapes (non-breaking)

### Key Entities *(include if feature involves data)*

- **TestFixture**: Represents test setup/teardown for integration tests
- **AuthPolicy**: Defines authorization rules based on claims/roles
- **TelemetryContext**: Carries TraceId, SpanId, and correlation data across service boundaries
- **JwtConfiguration**: Holds IdP settings (authority, audience, issuer)
- **LogContext**: Enriches logs with structured properties

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All microservices have >80% code coverage from unit + integration tests
- **SC-002**: Integration tests execute in <5 minutes with Docker containers
- **SC-003**: Protected endpoints reject invalid tokens with proper 401/403 responses
- **SC-004**: All cross-service requests have TraceId correlation in logs
- **SC-005**: Developers can trace a request end-to-end through observability tools
- **SC-006**: Zero breaking changes to existing API consumers during Phase 1 rollout
- **SC-007**: TDD workflow can be demonstrated: write failing test → implement → test passes
- **SC-008**: CI/CD pipeline runs all tests and fails on test failures

