<img src="https://github.com/emregulistan/AspNetMicroservices/blob/main/diagram.png" width="auto">

# ASP.NET Core Microservices

A comprehensive microservices architecture implementing e-commerce functionality with **enterprise-grade authentication, observability, and testing**.

## 🆕 New Features

### 🔐 Authentication & Authorization
* **JWT Bearer Authentication** across all 6 microservices
* **Policy-based authorization** with role (Admin/User) and scope-based claims
* **Common.Auth BuildingBlock** for reusable authentication configuration
* Development mode with auth disabled by default for easier testing
* Production-ready JWT token validation with configurable issuers and audiences

### 📊 Observability & Distributed Tracing
* **OpenTelemetry** integration for distributed tracing
* **Serilog** structured logging across all services
* **Jaeger** backend for trace visualization and analysis
* Automatic HTTP request instrumentation
* MassTransit message bus instrumentation
* **Common.Observability BuildingBlock** for consistent telemetry configuration

### 🧪 Comprehensive Testing Infrastructure
* **63 tests with 100% pass rate**
* **Integration tests** using real databases via Testcontainers:
  - MongoDB for Catalog.API (9 tests)
  - Redis for Basket.API (14 tests)
  - PostgreSQL for Discount.API (19 tests)
* **Unit tests** with proper mocking (16 tests)
* **TestHelpers BuildingBlock** with reusable test fixtures
* JWT token generation for authentication testing
* Real infrastructure testing (no mock madness!)

### 📚 Spec-Driven Development
* **Constitution document** defining architectural rules and patterns
* Golden path patterns vs legacy exceptions clearly documented
* Comprehensive implementation summaries and testing guides

---

## 🏗️ Microservices Architecture

#### Catalog microservice which includes; 
* ASP.NET Core Web API application 
* REST API principles, CRUD operations
* **MongoDB database** connection and containerization
* Repository Pattern Implementation
* Swagger Open API implementation	

#### Basket microservice which includes;
* ASP.NET Web API application
* REST API principles, CRUD operations
* **Redis database** connection and containerization
* Consume Discount **Grpc Service** for inter-service sync communication to calculate product final price
* Publish BasketCheckout Queue with using **MassTransit and RabbitMQ**
  
#### Discount microservice which includes;
* ASP.NET **Grpc Server** application
* Build a Highly Performant **inter-service gRPC Communication** with Basket Microservice
* Exposing Grpc Services with creating **Protobuf messages**
* Using **Dapper for micro-orm implementation** to simplify data access and ensure high performance
* **PostgreSQL database** connection and containerization

#### Microservices Communication
* Sync inter-service **gRPC Communication**
* Async Microservices Communication with **RabbitMQ Message-Broker Service**
* Using **RabbitMQ Publish/Subscribe Topic** Exchange Model
* Using **MassTransit** for abstraction over RabbitMQ Message-Broker system
* Publishing BasketCheckout event queue from Basket microservices and Subscribing this event from Ordering microservices	
* Create **RabbitMQ EventBus.Messages library** and add references Microservices

#### Ordering Microservice
* Implementing **DDD, CQRS, and Clean Architecture** with using Best Practices
* Developing **CQRS with using MediatR, FluentValidation and AutoMapper packages**
* Consuming **RabbitMQ** BasketCheckout event queue with using **MassTransit-RabbitMQ** Configuration
* **SqlServer database** connection and containerization
* Using **Entity Framework Core ORM** and auto migrate to SqlServer when application startup
	
#### API Gateway Ocelot Microservice
* Implement **API Gateways with Ocelot**
* Sample microservices/containers to reroute through the API Gateways
* Run multiple different **API Gateway/BFF** container types	
* The Gateway aggregation pattern in Shopping.Aggregator

#### WebUI ShoppingApp Microservice
* ASP.NET Core Web Application with Bootstrap 4 and Razor template
* Call **Ocelot APIs with HttpClientFactory** and **Polly**

#### Shared BuildingBlocks Libraries
* **Common.Auth** - JWT authentication and authorization policies
* **Common.Observability** - OpenTelemetry and Serilog configuration
* **EventBus.Messages** - RabbitMQ message contracts

#### Docker Compose establishment with all microservices on docker;
* Containerization of microservices
* Containerization of databases
* **Jaeger** for distributed tracing
* Override Environment variables

## 🚀 Getting Started

### Prerequisites
* Docker Desktop
* .NET 6.0 SDK (for local development)
* Visual Studio 2022 or VS Code (optional)

### Running the Application

1. **Clone the repository**
   ```bash
   git clone https://github.com/juancece/aspnetcore-microservices.git
   cd aspnetcore-microservices
   ```

2. **Run with Docker Compose**
   ```bash
   cd src
   docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d
   ```

3. **Access the services**

#### Microservices APIs
* **Catalog API** → http://host.docker.internal:8000/swagger/index.html
* **Basket API** → http://host.docker.internal:8001/swagger/index.html
* **Discount API** → http://host.docker.internal:8002/swagger/index.html
* **Ordering API** → http://host.docker.internal:8004/swagger/index.html
* **Shopping.Aggregator** → http://host.docker.internal:8005/swagger/index.html
* **API Gateway** → http://host.docker.internal:8010/Catalog

#### Infrastructure & Monitoring
* **Jaeger Tracing UI** → http://host.docker.internal:16686 (Distributed tracing)
* **RabbitMQ Dashboard** → http://host.docker.internal:15672 (guest/guest)
* **Portainer** → http://host.docker.internal:9000 (admin/admin1234)
* **pgAdmin PostgreSQL** → http://host.docker.internal:5050 (admin@aspnetrun.com/admin1234)
* **Web UI** → http://host.docker.internal:8006

---

## 🔐 Authentication & Security

### JWT Authentication
All microservices implement JWT Bearer authentication with the following features:

* **Policy-based authorization** with scope and role claims
* **Development mode**: Authentication disabled by default (`appsettings.Development.json`)
* **Production mode**: Full JWT validation with configurable issuers

### Authorization Policies

| Service | Read Policy | Write Policy | Scopes |
|---------|-------------|--------------|--------|
| Catalog | `ReadCatalog` | `WriteCatalog` | `catalog.read`, `catalog.write` |
| Basket | `ReadBasket` | `WriteBasket` | `basket.read`, `basket.write` |
| Discount | `ReadDiscount` | `WriteDiscount` | `discount.read`, `discount.write` |
| Orders | `ReadOrders` | `WriteOrders` | `orders.read`, `orders.write` |
| Shopping | `ReadShopping` | - | `shopping.read` |

### Configuring Authentication

Update `appsettings.json` for each service:

```json
{
  "Authentication": {
    "JwtBearer": {
      "Enabled": true,
      "Authority": "https://your-identity-server.com",
      "Audience": "your-api-resource",
      "ValidIssuers": ["https://your-identity-server.com"],
      "RequireHttpsMetadata": true,
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ValidateLifetime": true
    }
  }
}
```

### Testing with Authentication

For integration tests, authentication is automatically handled by `TestAuthHandler` which injects test claims.

---

## 📊 Observability & Monitoring

### Distributed Tracing with Jaeger

All services export telemetry to Jaeger using **OpenTelemetry**:

1. **View traces**: http://host.docker.internal:16686
2. **Select service** from the dropdown (e.g., `Catalog.API`)
3. **Find traces** to see complete request flows across microservices

### Structured Logging with Serilog

All services use **Serilog** for structured logging:

* **Console logging** in development
* **Trace IDs** automatically included for correlation
* **Minimum levels** configurable per service

### Configuring Observability

Update `appsettings.json`:

```json
{
  "Telemetry": {
    "ServiceName": "Catalog.API",
    "OtlpEndpoint": "http://jaeger:4317"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

---

## 🧪 Testing

### Test Coverage

* **Total Tests**: 63 (100% pass rate)
* **Integration Tests**: 47 tests with real databases
* **Unit Tests**: 16 tests with mocked dependencies

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Catalog.API.IntegrationTests

# With coverage
dotnet test /p:CollectCoverage=true
```

### Test Projects

| Project | Type | Infrastructure | Tests |
|---------|------|----------------|-------|
| `Catalog.API.IntegrationTests` | Integration | MongoDB (Testcontainers) | 9 |
| `Basket.API.IntegrationTests` | Integration | Redis (Testcontainers) | 14 |
| `Discount.API.IntegrationTests` | Integration | PostgreSQL (Testcontainers) | 19 |
| `Shopping.Aggregator.IntegrationTests` | Integration | Mocked HTTP | 5 |
| `Basket.API.Tests` | Unit | Mocked | 14 |
| `Catalog.API.Tests` | Unit | Mocked | 2 |

### Testing Best Practices

* **Integration-first**: Test with real infrastructure (databases, Redis, etc.)
* **Testcontainers**: Automatically spin up Docker containers for tests
* **Real data**: No mocks at the data layer, catch real database behaviors
* **Authentication**: Use `FakeJwtTokenGenerator` for JWT tokens in tests

---

## 📁 Project Structure

```
aspnetcore-microservices/
├── src/
│   ├── BuildingBlocks/
│   │   ├── Common.Auth/              # JWT authentication library
│   │   ├── Common.Observability/     # OpenTelemetry + Serilog
│   │   └── EventBus.Messages/        # RabbitMQ contracts
│   ├── Services/
│   │   ├── Catalog/Catalog.API/      # MongoDB, Product catalog
│   │   ├── Basket/Basket.API/        # Redis, Shopping cart
│   │   ├── Discount/
│   │   │   ├── Discount.API/         # PostgreSQL, REST API
│   │   │   └── Discount.Grpc/        # PostgreSQL, gRPC service
│   │   └── Ordering/
│   │       ├── Ordering.API/         # SQL Server, CQRS entry point
│   │       ├── Ordering.Application/ # MediatR, FluentValidation
│   │       ├── Ordering.Domain/      # DDD entities
│   │       └── Ordering.Infrastructure/ # EF Core, repositories
│   ├── ApiGateways/
│   │   ├── OcelotApiGw/              # API Gateway
│   │   └── Shopping.Aggregator/      # BFF aggregator
│   └── WebApps/
│       └── AspnetRunBasics/          # Razor Pages UI
├── tests/
│   ├── BuildingBlocks/
│   │   └── TestHelpers/              # Shared test utilities
│   ├── Catalog.API.Tests/            # Unit tests
│   ├── Catalog.API.IntegrationTests/ # Integration tests
│   ├── Basket.API.Tests/             # Unit tests
│   ├── Basket.API.IntegrationTests/  # Integration tests
│   ├── Discount.API.IntegrationTests/ # Integration tests
│   └── Shopping.Aggregator.IntegrationTests/ # Integration tests
├── specs/                            # Feature specifications
└── .specify/                         # Spec-driven development artifacts
    ├── memory/
    │   └── constitution.md           # Architectural rules
    ├── testing/                      # Test documentation
    └── observability/                # Observability guides
```

---

## 📖 Documentation

* **[Constitution](.specify/memory/constitution.md)** - Architectural rules and patterns
* **[Testing Guide](.specify/testing/final-testing-summary.md)** - Comprehensive testing documentation
* **[Implementation Summary](.specify/MASTER-IMPLEMENTATION-SUMMARY.md)** - Complete feature overview
* **[Authentication Guide](.specify/testing/authentication-complete-summary.md)** - Auth setup and configuration
* **[Observability Guide](.specify/observability/observability-complete-summary.md)** - Tracing and logging setup

---

## 🛠️ Development

### Building Locally

```bash
# Build all projects
dotnet build aspnet-microservices.sln

# Build specific service
dotnet build src/Services/Catalog/Catalog.API
```

### Running Locally (without Docker)

1. **Start infrastructure** (databases, RabbitMQ, Jaeger)
   ```bash
   cd src
   docker-compose up -d catalogdb basketdb discountdb orderdb rabbitmq jaeger
   ```

2. **Run services**
   ```bash
   # Terminal 1
   dotnet run --project src/Services/Catalog/Catalog.API

   # Terminal 2
   dotnet run --project src/Services/Basket/Basket.API
   
   # ... etc
   ```

### Environment Variables

Services use environment variables from `docker-compose.override.yml`:

* `DatabaseSettings:*` - Database connection strings
* `Authentication:JwtBearer:Enabled` - Enable/disable auth (false in dev)
* `Telemetry:ServiceName` - Service name for traces
* `Telemetry:OtlpEndpoint` - Jaeger endpoint

---

## 🐛 Troubleshooting

### Common Issues

1. **Port conflicts**: Ensure ports 8000-8006, 15672, 16686 are available
2. **Database connection issues**: Check `docker-compose.override.yml` connection strings
3. **Authentication 401**: Set `Authentication:JwtBearer:Enabled: false` in development
4. **Tests failing**: Ensure Docker Desktop is running (Testcontainers requirement)

### Viewing Logs

```bash
# View all service logs
docker-compose logs -f

# View specific service
docker-compose logs -f catalog.api

# View Jaeger traces
# Open http://host.docker.internal:16686
```

---

## 📋 Spec-Driven Development with Spec-Kit

This project follows **spec-driven development** using the **Spec-Kit** methodology, which provides a structured approach to building features with AI assistance.

### What is Spec-Kit?

Spec-Kit is a specification-driven development framework that:

* **Captures requirements** as structured specifications
* **Generates actionable tasks** from specifications
* **Maintains architectural consistency** through a constitution
* **Tracks implementation progress** with automatic TODO management
* **Documents decisions** for future reference

### Available Spec-Kit Commands

Spec-Kit provides several Cursor AI commands (accessible via `Ctrl+L` or `Cmd+L`):

| Command | Purpose | When to Use |
|---------|---------|-------------|
| `@speckit.specify` | Create a detailed feature specification | Starting a new feature |
| `@speckit.plan` | Generate implementation plan from spec | After spec is approved |
| `@speckit.tasks` | Create actionable task list | Before implementation |
| `@speckit.implement` | Execute tasks with AI guidance | During development |
| `@speckit.analyze` | Analyze codebase for patterns | Understanding existing code |
| `@speckit.constitution` | Update architectural rules | After establishing patterns |
| `@speckit.clarify` | Clarify ambiguous requirements | When requirements unclear |
| `@speckit.checklist` | Generate validation checklist | Before deployment |

### Spec-Driven Workflow

#### 1️⃣ **Create Specification**

```bash
# Use Cursor command
@speckit.specify

# Or create manually in specs/
specs/
└── 00X-feature-name/
    ├── spec.md              # Main specification
    ├── plan.md              # Implementation plan
    ├── tasks.md             # Task breakdown
    ├── data-model.md        # Data structures
    └── contracts/           # API contracts
```

**Example Specification Structure:**

```markdown
# Feature: User Authentication

## User Story
As a developer, I want JWT authentication across all microservices
so that I can secure API endpoints with role-based access control.

## Acceptance Criteria
- [ ] All 6 microservices support JWT Bearer tokens
- [ ] Policy-based authorization with scopes
- [ ] Development mode with auth disabled
- [ ] Integration tests verify auth flows

## Technical Approach
- Create Common.Auth BuildingBlock
- Implement JwtConfiguration class
- Add PolicyConstants for all services
- Update all Program.cs files
```

#### 2️⃣ **Generate Implementation Plan**

```bash
@speckit.plan
```

This analyzes your spec and creates a dependency-ordered implementation plan in `specs/00X-feature-name/plan.md`.

#### 3️⃣ **Create Task Breakdown**

```bash
@speckit.tasks
```

Generates a detailed, actionable task list in `specs/00X-feature-name/tasks.md` with:

* **Dependency order** - Tasks sorted by what must be done first
* **File paths** - Exact files to create/modify
* **Parallel markers** - Tasks that can be done simultaneously
* **Time estimates** - Rough complexity indicators

#### 4️⃣ **Implement with AI Guidance**

```bash
@speckit.implement
```

The AI will:

* Read your tasks.md
* Execute tasks in order
* Update TODO list automatically
* Run tests after each phase
* Document decisions and bugs found

#### 5️⃣ **Update Constitution**

```bash
@speckit.constitution
```

After establishing patterns, document them in `.specify/memory/constitution.md` so future AI sessions follow the same rules.

### Spec-Kit Directory Structure

```
.specify/
├── memory/
│   └── constitution.md        # Architectural rules and patterns
├── analysis/
│   └── consistency-analysis.md # Codebase analysis results
├── testing/
│   ├── final-testing-summary.md
│   └── authentication-complete-summary.md
├── observability/
│   └── observability-complete-summary.md
├── templates/                 # Spec-Kit templates
│   ├── spec-template.md
│   ├── plan-template.md
│   └── tasks-template.md
└── scripts/                   # Automation scripts
    └── powershell/

specs/
└── 001-testing-auth-observability/  # Example feature
    ├── spec.md                      # Feature specification
    ├── plan.md                      # Implementation plan
    ├── tasks.md                     # Task breakdown
    ├── data-model.md                # Data structures
    └── contracts/                   # JSON contracts
        ├── auth-policies.json
        ├── jwt-validation.json
        └── telemetry-schema.json
```

### Constitution-Driven Development

The **constitution** (`.specify/memory/constitution.md`) defines:

* **Golden Path Patterns** - Recommended approaches to follow
* **Legacy Exceptions** - Patterns to avoid (but exist in old code)
* **Architectural Rules** - Must-follow principles
* **Technology Standards** - Approved libraries and frameworks

**Example Constitution Rules:**

```markdown
### Mandatory Principles

1. **Thin Controllers** - Controllers must delegate to services
2. **Input Validation** - Use FluentValidation for all write operations
3. **Exception Handling** - Use centralized exception middleware
4. **Extension Methods** - Register services via extension methods

### Testing Requirements

1. **Integration-First** - Test data layers with real databases
2. **Testcontainers** - Use Docker containers for integration tests
3. **100% Pass Rate** - All tests must pass before commit
```

### Best Practices

1. **Always start with a spec** - Don't jump straight to code
2. **Update the constitution** - Document patterns as you establish them
3. **Let AI read the constitution** - It will follow your established rules
4. **Use structured tasks** - Break features into small, testable units
5. **Document decisions** - Keep implementation summaries in `.specify/`

### Example: Adding a New Feature

```bash
# 1. Create specification
@speckit.specify
# AI asks questions and creates specs/002-feature-name/spec.md

# 2. Generate plan
@speckit.plan
# AI creates specs/002-feature-name/plan.md

# 3. Create tasks
@speckit.tasks
# AI creates specs/002-feature-name/tasks.md

# 4. Implement
@speckit.implement
# AI executes all tasks, runs tests, updates docs

# 5. Update constitution (if new patterns established)
@speckit.constitution
# AI updates .specify/memory/constitution.md
```

### Benefits of Spec-Kit

✅ **Consistency** - All features follow the same process  
✅ **Documentation** - Decisions are automatically captured  
✅ **Quality** - Tests are planned before implementation  
✅ **Onboarding** - New developers understand the "why" behind code  
✅ **AI Efficiency** - AI has clear context and constraints  
✅ **Traceability** - Requirements → Tasks → Implementation → Tests

### Troubleshooting Spec-Kit

**"AI isn't following the constitution"**
- Ensure `.specify/memory/constitution.md` exists
- Mention constitution explicitly: "Follow the constitution rules"
- Re-run `@speckit.constitution` to refresh

**"Tasks are too vague"**
- Run `@speckit.clarify` to ask detailed questions
- Update spec.md with more specific acceptance criteria
- Include data models and API contracts

**"Implementation is inconsistent"**
- Run `@speckit.analyze` to understand current patterns
- Update constitution with discovered patterns
- Mark legacy code as "exceptions - do not copy"

---

## 🤝 Contributing

This project follows **spec-driven development**:

1. Run `@speckit.specify` to create a feature spec in `specs/`
2. Run `@speckit.tasks` to generate actionable tasks
3. Write tests first (integration tests for data layers)
4. Run `@speckit.implement` to execute tasks with AI guidance
5. Update `.specify/memory/constitution.md` if new patterns are established
6. Document implementation in `.specify/` summaries

---

## 📄 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

* Original microservices architecture by AspNetRun
* Extended with enterprise-grade authentication, observability, and testing


