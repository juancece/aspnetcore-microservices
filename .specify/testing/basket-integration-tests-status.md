# Basket.API Integration Tests - Status Report

## ✅ Completed

### Infrastructure
- ✅ Created `tests/Basket.API.IntegrationTests/` project
- ✅ Added Redis Testcontainer support via `RedisFixture`
- ✅ Fixed Redis admin mode for FLUSHALL command
- ✅ Integrated with `TestWebApplicationFactory`
- ✅ Authentication configured with `TestAuthHandler`

### Test Cases Created
1. `GetBasket_ShouldReturn200_WithEmptyBasket_WhenBasketDoesNotExist` ✅
2. `UpdateBasket_ShouldReturn200_WhenBasketIsValid` ❌
3. `GetBasket_ShouldReturnSavedBasket_AfterUpdate` ❌
4. `DeleteBasket_ShouldReturn200_WhenBasketExists` ❌
5. `Checkout_ShouldReturn202Accepted_WhenBasketCheckoutIsValid` ❌
6. `CompleteBasketLifecycle_ShouldWorkEndToEnd` ❌

## ⚠️ Blocking Issues

### External Dependencies
`Basket.API` has hard dependencies on external services that are not available in the test environment:

1. **Discount.Grpc Service** (line 51 in `BasketController.cs`)
   - Called in `UpdateBasket` to get discount information
   - Error: `Status(StatusCode="Unavailable", Detail="Error connecting to subchannel.")`
   - Impact: Any test calling `UpdateBasket`, `DeleteBasket`, or lifecycle tests fails

2. **RabbitMQ / MassTransit** (used in `Checkout`)
   - Used to publish `BasketCheckoutEvent` 
   - Error: `RabbitMQ.Client.Exceptions.BrokerUnreachableException: None of the specified endpoints were reachable`
   - Impact: Checkout tests fail

## 🔧 Solutions

### Option A: Mock External Dependencies (Recommended)
Update `TestWebApplicationFactory` to replace external service dependencies with mocks:

```csharp
builder.ConfigureTestServices(services =>
{
    // Replace Discount gRPC client with a mock
    services.AddTransient<IDiscountGrpcService, MockDiscountGrpcService>();
    
    // Replace MassTransit with in-memory test harness
    services.AddMassTransitInMemoryTestHarness(cfg =>
    {
        // Configure test harness
    });
});
```

### Option B: Test Only Core Functionality
Create separate test classes:
- `BasketCoreTests` - Tests that don't require external dependencies (GetBasket only)
- `BasketIntegrationTests` - Full integration tests (requires Docker Compose with all services)

### Option C: Use Test Doubles
Create test implementations:
- `MockDiscountGrpcService` - Returns fixed discount values
- `InMemoryEventBus` - Captures events without RabbitMQ

## 📊 Test Results

### Passing Tests (1/6)
- ✅ `GetBasket_ShouldReturn200_WithEmptyBasket_WhenBasketDoesNotExist` - Works because it doesn't call external services

### Failing Tests (5/6)
All tests that call `UpdateBasket` fail due to Discount.Grpc unavailability:
- ❌ `UpdateBasket_ShouldReturn200_WhenBasketIsValid`
- ❌ `GetBasket_ShouldReturnSavedBasket_AfterUpdate`
- ❌ `DeleteBasket_ShouldReturn200_WhenBasketExists`
- ❌ `Checkout_ShouldReturn202Accepted_WhenBasketCheckoutIsValid`
- ❌ `CompleteBasketLifecycle_ShouldWorkEndToEnd`

## 🎯 Recommended Next Steps

1. **For Now**: Mark Basket tests as "Partial - Core Functionality Only"
2. **Later**: Implement mocking infrastructure for external dependencies
3. **Eventually**: Create full end-to-end tests that spin up all dependent services

## 📝 Key Files

- `tests/Basket.API.IntegrationTests/Controllers/BasketIntegrationTests.cs`
- `tests/BuildingBlocks/TestHelpers/DatabaseFixture.cs` (Redis support added)
- `tests/BuildingBlocks/TestHelpers/TestWebApplicationFactory.cs` (Redis fixture added)

## 💡 Lessons Learned

1. **Service Dependencies**: Microservices with inter-service dependencies require careful test planning
2. **Test Isolation**: Integration tests should either mock external dependencies or orchestrate full environments
3. **Test Pyramid**: Unit tests for business logic, integration tests for data access, E2E tests for full workflows

## 🔄 Alternative Approach for Remaining Services

For **Discount.API**, **Discount.Grpc**, **Ordering.API**, and **Shopping.Aggregator**:
- Focus on services with minimal external dependencies first
- **Discount.API** (PostgreSQL only) - Good candidate ✅
- **Ordering.API** (SQL Server only) - Good candidate ✅  
- **Discount.Grpc** (PostgreSQL only) - Good candidate ✅
- **Shopping.Aggregator** (calls all other services) - Mock or skip ⚠️

