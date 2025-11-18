# Integration-First Testing Strategy

## 🎯 Philosophy: "Test What Matters"

**Confidence comes from testing real integrations, not mocked behavior.**

---

## 📊 Testing Priorities (Revised)

### 🥇 **Priority 1: Integration Tests** (Repository/Data Layer)

**What**: Test data access layer with REAL infrastructure  
**Why**: Catches 80% of production bugs  
**Coverage Target**: 100% of repository methods

#### Services to Cover:
1. ✅ **Catalog.API** - Already has integration tests (8/8 passing)
2. 🔄 **Basket.API** - Create BasketRepository integration tests (Redis)
3. 🔄 **Discount.API** - Create DiscountRepository integration tests (PostgreSQL + Dapper)
4. 🔄 **Discount.Grpc** - Share Discount.API repository tests
5. 🔄 **Ordering.API** - Create OrderRepository integration tests (SQL Server + EF Core)
6. ⏸️ **Shopping.Aggregator** - No database (skip)

---

### 🥈 **Priority 2: Selective Unit Tests** (Business Logic Only)

**What**: Test ONLY complex business logic in isolation  
**Why**: When logic is truly independent of infrastructure  
**Coverage Target**: Only where it adds value

#### Examples of Good Unit Test Candidates:
- ✅ Complex calculations (pricing, discounts, taxes)
- ✅ Business rule validation (order validation, inventory checks)
- ✅ Pure algorithms (sorting, filtering, transformations)
- ❌ Controllers (usually just orchestration - skip)
- ❌ Simple CRUD repositories (covered by integration tests)

#### Current Status:
- ✅ **Basket.API** - 13 unit tests (keep as reference)
- 🤔 **Other services** - Add unit tests ONLY if complex logic exists

---

### 🥉 **Priority 3: E2E Tests** (Optional)

**What**: Full HTTP API with all services running  
**Why**: Validates complete user journeys  
**Coverage Target**: Critical paths only

**Status**: Deferred (not needed yet)

---

## 🎯 Implementation Plan: Integration Tests First

### **Step 1: Basket.API Repository Integration Tests** ⏳
**File**: `tests/Basket.API.IntegrationTests/Repositories/BasketRepositoryIntegrationTests.cs`

**What to Test**:
- ✅ GetBasket with real Redis
- ✅ UpdateBasket with real serialization
- ✅ DeleteBasket with real Redis
- ✅ Concurrent operations
- ✅ Large basket serialization
- ✅ Connection handling

**Infrastructure**: RedisFixture (Testcontainer)

**Estimated Time**: 20 minutes

---

### **Step 2: Discount.API Repository Integration Tests** ⏳
**File**: `tests/Discount.API.IntegrationTests/Repositories/DiscountRepositoryIntegrationTests.cs`

**What to Test**:
- ✅ GetDiscount with real PostgreSQL
- ✅ CreateDiscount with real INSERT
- ✅ UpdateDiscount with real UPDATE
- ✅ DeleteDiscount with real DELETE
- ✅ Dapper mapping (string to entity)
- ✅ Transaction handling
- ✅ Constraint violations

**Infrastructure**: PostgreSqlFixture (Testcontainer)

**Estimated Time**: 30 minutes

---

### **Step 3: Ordering.API Repository Integration Tests** ⏳
**File**: `tests/Ordering.API.IntegrationTests/Repositories/OrderRepositoryIntegrationTests.cs`

**What to Test**:
- ✅ GetOrdersByUserName with real SQL Server + EF Core
- ✅ AddAsync with real INSERT
- ✅ UpdateAsync with real UPDATE
- ✅ DeleteAsync with real DELETE
- ✅ EF Core change tracking
- ✅ Audit fields (CreatedDate, UpdatedDate)
- ✅ Navigation properties
- ✅ Transactions

**Infrastructure**: SqlServerFixture (Testcontainer)

**Estimated Time**: 40 minutes

---

### **Step 4: Optional Unit Tests** (Only if Needed)
**Criteria for Adding Unit Tests**:
- ❓ Is there complex business logic?
- ❓ Is it isolated from infrastructure?
- ❓ Would integration tests be too slow?
- ❓ Does it involve complex calculations?

**If YES to all** → Add unit tests  
**If NO** → Integration tests are enough

---

## 📈 Expected Coverage

### After Integration Tests Complete:

| Service | Repository Integration | Unit Tests | Total Confidence |
|---------|------------------------|------------|------------------|
| Catalog.API | ✅ 8 tests (done) | ⏸️ Optional | 🟢 HIGH |
| Basket.API | 🔄 ~6 tests | ✅ 13 tests | 🟢 HIGH |
| Discount.API | 🔄 ~8 tests | ⏸️ Optional | 🟢 HIGH |
| Discount.Grpc | 🔄 Shared | ⏸️ Optional | 🟢 HIGH |
| Ordering.API | 🔄 ~10 tests | ⏸️ Optional | 🟢 HIGH |
| Shopping.Aggregator | N/A | ⏸️ Optional | 🟡 MEDIUM |

---

## 🎓 Why Integration Tests Matter More

### Real-World Example: The Serialization Bug

**Unit Test (Mocked)**:
```csharp
// PASSES ✅ (but wrong!)
_mockCache.Setup(x => x.GetAsync("user1"))
    .ReturnsAsync(Encoding.UTF8.GetBytes("{\"UserName\":\"user1\"}"));
```

**Integration Test (Real Redis)**:
```csharp
// FAILS ❌ (catches bug!)
await _repository.UpdateBasket(basket);
var result = await _repository.GetBasket("user1");
// Discovers: Newtonsoft.Json vs System.Text.Json serialization mismatch
// Discovers: DateTime serialization format issues
// Discovers: Null handling differences
```

### Real-World Example: The SQL Query Bug

**Unit Test (Mocked)**:
```csharp
// PASSES ✅ (but query is wrong!)
_mockRepository.Setup(x => x.GetOrdersByUserName("user1"))
    .ReturnsAsync(new List<Order> { order1 });
```

**Integration Test (Real SQL Server)**:
```csharp
// FAILS ❌ (catches bug!)
var orders = await _repository.GetOrdersByUserName("user1");
// Discovers: JOIN query is missing
// Discovers: N+1 query problem
// Discovers: SQL syntax error in WHERE clause
```

---

## 🛠️ Testcontainers Benefits

Using Testcontainers for integration tests gives us:

1. ✅ **Real Database** - Actual PostgreSQL, SQL Server, MongoDB, Redis
2. ✅ **Isolated** - Each test gets fresh database
3. ✅ **Consistent** - Same behavior in dev and CI/CD
4. ✅ **Fast Setup** - Automatic Docker container management
5. ✅ **Cleanup** - Automatic container disposal
6. ✅ **Parallel** - Tests can run in parallel

---

## 🎯 Success Criteria

After completing integration tests, we should have:

✅ **Confidence**: Can deploy to production with minimal risk  
✅ **Coverage**: All database operations tested with real infrastructure  
✅ **Speed**: Tests run in < 30 seconds total  
✅ **Reliability**: Tests are deterministic and don't flake  
✅ **Documentation**: Tests serve as usage examples  

---

## 📝 Summary

**Old Approach** (Unit Test Heavy):
- 👎 Many mocks, less confidence
- 👎 Misses real integration issues
- 👎 Maintains mock setup code
- 👍 Fast execution

**New Approach** (Integration Test Heavy):
- 👍 Real infrastructure, high confidence
- 👍 Catches actual bugs
- 👍 Less mock maintenance
- 👍 Still fast with Testcontainers

---

**Next Step**: Create Basket.API repository integration tests with real Redis!

