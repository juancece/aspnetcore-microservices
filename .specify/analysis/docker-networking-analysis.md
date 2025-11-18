# Docker Networking Analysis: Testing, Auth & Observability

**Analysis Date**: 2025-11-17  
**Triggered By**: User verification of README.md Docker networking configuration  
**Status**: ⚠️ **GAPS IDENTIFIED AND FIXED**

---

## Executive Summary

**User's Question**: "Is Docker networking from README.md included in the previous analysis?"

**Answer**: ⚠️ **Partially** - The consistency analysis focused on code-level patterns but **did not explicitly verify Docker networking configuration**. This analysis fills that gap.

---

## README.md Docker Networking Requirements

From `README.md`, the application exposes services on **`host.docker.internal`** with these ports:

| Service | External URL | Internal Port | Container Name |
|---------|--------------|---------------|----------------|
| Catalog API | `http://host.docker.internal:8000` | 80 | `catalog.api` |
| Basket API | `http://host.docker.internal:8001` | 80 | `basket.api` |
| Discount API | `http://host.docker.internal:8002` | 80 | `discount.api` |
| Ordering API | `http://host.docker.internal:8004` | 80 | `ordering.api` |
| Shopping.Aggregator | `http://host.docker.internal:8005` | 80 | `shopping.aggregator` |
| API Gateway (Ocelot) | `http://host.docker.internal:8010` | 80 | `ocelotapigw` |
| Jaeger UI | **MISSING from README** | 16686 | `jaeger` |
| RabbitMQ Management | `http://host.docker.internal:15672` | 15672 | `rabbitmq` |
| Portainer | `http://host.docker.internal:9000` | 9000 | `portainer` |
| pgAdmin | `http://host.docker.internal:5050` | 5050 | `pgadmin` |
| Web UI | `http://host.docker.internal:8006` | 80 | `webui` |

**Note**: Discount.Grpc runs on port 8003 but is not listed in README (internal gRPC service).

---

## Docker Compose Configuration Analysis

### ✅ **What Was Correctly Configured**

1. **Port Mappings** (docker-compose.override.yml):
   - All services correctly map external ports to internal port 80
   - Matches README.md exactly: 8000, 8001, 8002, 8004, 8005, 8010
   - Database ports correctly exposed: MongoDB (27017), Redis (6379), PostgreSQL (5432), SQL Server (1433)

2. **Container Naming**:
   - All containers use explicit names matching README expectations
   - Example: `catalog.api`, `basket.api`, etc.

3. **Inter-Service Communication**:
   - Services use container names for internal communication
   - Example: `GrpcSettings:DiscountUrl=http://discount.grpc`
   - Example: `EventBusSettings:HostAddress=amqp://guest:guest@rabbitmq:5672`

### ⚠️ **What Was Missing (NOW FIXED)**

#### **Issue 1: Jaeger Not in docker-compose.override.yml**

**Problem**: Jaeger was added to `docker-compose.yml` but lacked environment configuration in `docker-compose.override.yml`.

**Impact**:
- ❌ Services couldn't connect to Jaeger for telemetry
- ❌ No OTLP endpoint configured for services
- ❌ Services not dependent on Jaeger startup

**Fix Applied**: ✅ Added to all service configurations:
```yaml
environment:
  - "Telemetry:ServiceName=Catalog.API"
  - "Telemetry:OtlpEndpoint=http://jaeger:4317"
  - "Authentication:JwtBearer:Enabled=false"  # Disabled by default for dev
depends_on:
  - jaeger
```

#### **Issue 2: Jaeger Not Listed in README.md**

**Problem**: README.md doesn't mention Jaeger URL for developers.

**Impact**:
- ⚠️ Developers won't know how to access Jaeger UI
- ⚠️ No documentation for observability backend

**Recommended Fix**: Update README.md to add:
```markdown
* **Jaeger Tracing UI -> http://host.docker.internal:16686**
```

#### **Issue 3: Ordering.API Incorrect SQL Server Connection String**

**Problem**: Ordering service connection string pointed to `Server=PC-EMRE` (developer's local machine).

**Fixed**: Changed to `Server=orderdb` (container name) for Docker networking:
```yaml
ConnectionStrings:OrderingConnectionString=Server=orderdb;...
```

---

## Integration Test Networking Considerations

### **Testcontainers vs Docker Compose**

The implementation plan includes **Testcontainers** for integration tests, which is **DIFFERENT** from the docker-compose networking:

| Aspect | docker-compose (dev) | Testcontainers (tests) |
|--------|---------------------|------------------------|
| Network | Default bridge network | Test-specific network |
| Database URLs | Container names (`catalogdb`) | Dynamic ports (localhost) |
| Service URLs | `http://catalog.api` | `http://localhost:RANDOM_PORT` |
| Lifecycle | Manual start/stop | Automatic per test suite |

**Why This Works**:
1. **Dev environment**: Uses docker-compose networking with fixed ports and container names
2. **Test environment**: Testcontainers spins up isolated databases with dynamic ports
3. **TestWebApplicationFactory** overrides connection strings in tests (implemented in TestHelpers)

---

## Authentication Across Docker Network

### **JWT Token Validation Networking**

When JWT authentication is implemented (User Story 2):

**Configuration Needed**:
```yaml
# docker-compose.override.yml (per service)
environment:
  - "Authentication:JwtBearer:Authority=https://your-idp.com"
  - "Authentication:JwtBearer:Audience=api://your-api"
  - "Authentication:JwtBearer:Enabled=false"  # Disabled by default
```

**Key Points**:
1. ✅ **Authority** can point to external IdP (e.g., Azure AD, Auth0)
2. ✅ **No intra-Docker networking needed** - services validate tokens locally using public keys from IdP
3. ✅ **Enabled=false by default** - allows services to run without auth in local dev

**For Testing**:
- Tests use `FakeJwtTokenGenerator` (already implemented)
- No real IdP needed for unit/integration tests
- Token validation disabled in test environment

---

## Observability Networking (Jaeger)

### **OTLP Export Configuration**

Services export telemetry to Jaeger using OpenTelemetry Protocol (OTLP):

**Container-to-Container** (docker-compose):
```yaml
environment:
  - "Telemetry:OtlpEndpoint=http://jaeger:4317"  # gRPC endpoint
```

**Developer Access** (host machine):
- Jaeger UI: `http://localhost:16686` or `http://host.docker.internal:16686`
- OTLP receiver (for external apps): `http://localhost:4317`

**How It Works**:
1. Services run inside Docker → use `http://jaeger:4317` (container name resolution)
2. Jaeger UI accessed from host → use `http://localhost:16686` (port mapping)
3. TraceId propagated via HTTP headers: `traceparent`, `tracestate`

---

## Inter-Service Communication Patterns

### **Synchronous (gRPC)**

Basket.API calls Discount.Grpc:
```yaml
# Basket configuration
GrpcSettings:DiscountUrl=http://discount.grpc  # Container name, no port (gRPC uses 80 in HTTP mode)
```

**Network Path**: `basket.api` → Docker bridge network → `discount.grpc:80`

### **Asynchronous (RabbitMQ)**

Basket.API publishes, Ordering.API subscribes:
```yaml
# Both services
EventBusSettings:HostAddress=amqp://guest:guest@rabbitmq:5672
```

**Network Path**: Services → Docker bridge network → `rabbitmq:5672`

### **API Gateway (Ocelot)**

WebUI calls Ocelot, Ocelot routes to backend services:
```yaml
# WebUI
ApiSettings:GatewayAddress=http://ocelotapigw

# Ocelot (routing config in ocelot.json, not shown)
# Routes to: http://catalog.api, http://basket.api, etc.
```

**Network Path**: `webui` → `ocelotapigw` → backend services (all via container names)

---

## Configuration Summary: What's Needed for Each User Story

### **User Story 1: Testing** ✅ Already Configured

**Docker Compose**:
- No changes needed (tests use Testcontainers, not docker-compose)

**Integration Tests**:
- ✅ `TestWebApplicationFactory` overrides connection strings
- ✅ `DatabaseFixture` starts isolated test containers
- ✅ Tests bind to `localhost` with dynamic ports

### **User Story 2: Authentication** ⚠️ Configuration Added, Implementation Pending

**Docker Compose** (✅ NOW CONFIGURED):
```yaml
environment:
  - "Authentication:JwtBearer:Enabled=false"  # Can flip to true when implementing
  - "Authentication:JwtBearer:Authority=..."  # Add real IdP URL when needed
  - "Authentication:JwtBearer:Audience=..."   # Add real audience when needed
```

**Code Changes** (⏳ PENDING):
- Add `builder.Services.AddJwtAuthentication(configuration)` to Program.cs
- Add `[AllowAnonymous]` / `[Authorize]` attributes to controllers

### **User Story 3: Observability** ✅ Configuration Complete

**Docker Compose** (✅ CONFIGURED):
```yaml
environment:
  - "Telemetry:ServiceName=Catalog.API"
  - "Telemetry:OtlpEndpoint=http://jaeger:4317"
depends_on:
  - jaeger
```

**Code Changes** (⏳ PENDING):
- Add `builder.Services.AddObservability(configuration)` to Program.cs
- Add `builder.Host.UseSerilog(...)` for structured logging

---

## Verification Checklist

### ✅ **Completed Fixes**

- [X] Jaeger service added to `docker-compose.yml` with all required ports
- [X] Telemetry configuration added to all services in `docker-compose.override.yml`
- [X] Authentication configuration placeholders added (disabled by default)
- [X] All services depend on `jaeger` for proper startup order
- [X] Ordering.API connection string fixed to use `orderdb` container name

### ⚠️ **Recommended Updates**

- [ ] Update `README.md` to include Jaeger UI URL: `http://host.docker.internal:16686`
- [ ] Update `README.md` to document Discount.Grpc URL (optional, internal service)
- [ ] Document authentication setup instructions when US2 is implemented
- [ ] Document observability setup instructions when US3 is implemented

### ✅ **Validated Networking**

- [X] All service ports match README.md (8000-8006, 8010)
- [X] All database ports correctly exposed (27017, 6379, 5432, 1433)
- [X] Inter-service communication uses container names (not localhost)
- [X] Jaeger accessible at ports 16686 (UI) and 4317 (OTLP)
- [X] RabbitMQ accessible at ports 5672 (AMQP) and 15672 (Management)

---

## Impact on Implementation

### **Did This Affect Previous Work?**

**Phase 1 & 2 (Completed)**: ✅ **No Impact**
- BuildingBlocks libraries are networking-agnostic
- TestHelpers support both docker-compose and Testcontainers

**Phase 3 (In Progress - Testing)**: ⚠️ **Minor Impact**
- Integration tests will use Testcontainers (isolated from docker-compose)
- No changes needed to test code already written

**Phase 4 (Pending - Authentication)**: ✅ **Properly Configured**
- Docker environment variables ready for JWT configuration
- Services can toggle auth on/off with `Enabled=false/true`

**Phase 5 (Pending - Observability)**: ✅ **Properly Configured**
- Jaeger backend ready to receive traces
- OTLP endpoints configured for all services

---

## Conclusion

**Original Analysis Coverage**: ⚠️ **Partially Addressed**
- ✅ Code-level patterns thoroughly analyzed
- ⚠️ Docker networking not explicitly verified
- ❌ Jaeger configuration missing from docker-compose.override.yml

**Current Status**: ✅ **RESOLVED**
- ✅ All Docker networking gaps identified and fixed
- ✅ Jaeger properly integrated into docker-compose setup
- ✅ Environment configuration ready for auth/observability implementation
- ⚠️ README.md needs update to document Jaeger (minor documentation gap)

**Recommendation**: **PROCEED** with implementation - Docker networking is now properly configured and validated.

---

**Generated**: 2025-11-17  
**Triggered By**: User verification question  
**Status**: ✅ Analysis Complete, Issues Fixed

