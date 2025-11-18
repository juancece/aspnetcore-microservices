using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Contracts.Persistence;
using Ordering.Domain.Entities;
using Ordering.Infrastructure.Persistence;
using Ordering.Infrastructure.Repositories;
using TestHelpers;
using Xunit;

namespace Ordering.API.IntegrationTests.Repositories;

/// <summary>
/// Integration tests for OrderRepository using REAL SQL Server + EF Core
/// Tests actual queries, change tracking, transactions, and audit fields with Testcontainers
/// </summary>
[Trait("Category", "Integration")]
public class OrderRepositoryIntegrationTests : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private readonly SqlServerFixture _dbFixture;
    private IOrderRepository _repository = null!;
    private OrderContext _context = null!;

    public OrderRepositoryIntegrationTests(SqlServerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        // Create context and repository with real SQL Server connection
        var options = new DbContextOptionsBuilder<OrderContext>()
            .UseSqlServer(_dbFixture.ConnectionString)
            .Options;

        _context = new OrderContext(options);
        
        // Ensure database is created and schema exists
        await _context.Database.EnsureCreatedAsync();
        
        // Clear existing data
        await ClearData();
        
        _repository = new OrderRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task ClearData()
    {
        _context.Orders.RemoveRange(_context.Orders);
        await _context.SaveChangesAsync();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoOrders()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllOrders()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        var order2 = CreateTestOrder("user2");
        var order3 = CreateTestOrder("user3");

        await _repository.AddAsync(order1);
        await _repository.AddAsync(order2);
        await _repository.AddAsync(order3);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(o => o.UserName == "user1");
        result.Should().Contain(o => o.UserName == "user2");
        result.Should().Contain(o => o.UserName == "user3");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        // Act
        var result = await _repository.GetByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnOrder_WhenOrderExists()
    {
        // Arrange
        var order = CreateTestOrder("testuser");
        var added = await _repository.AddAsync(order);

        // Act
        var result = await _repository.GetByIdAsync(added.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(added.Id);
        result.UserName.Should().Be("testuser");
        result.TotalPrice.Should().Be(100.50m);
    }

    #endregion

    #region GetOrdersByUserName Tests

    [Fact]
    public async Task GetOrdersByUserName_ShouldReturnEmptyList_WhenNoOrdersForUser()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        await _repository.AddAsync(order1);

        // Act
        var result = await _repository.GetOrdersByUserName("nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrdersByUserName_ShouldReturnUserOrders()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        var order2 = CreateTestOrder("user2");
        var order3 = CreateTestOrder("user1"); // Another order for user1

        await _repository.AddAsync(order1);
        await _repository.AddAsync(order2);
        await _repository.AddAsync(order3);

        // Act
        var result = await _repository.GetOrdersByUserName("user1");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.UserName.Should().Be("user1"));
        result.Should().NotContain(o => o.UserName == "user2");
    }

    [Fact]
    public async Task GetOrdersByUserName_ShouldBeCaseSensitive()
    {
        // Arrange
        var order = CreateTestOrder("TestUser");
        await _repository.AddAsync(order);

        // Act
        var result = await _repository.GetOrdersByUserName("testuser");

        // Assert
        result.Should().BeEmpty("SQL Server by default is case-insensitive but depends on collation");
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_ShouldInsertOrder_AndGenerateId()
    {
        // Arrange
        var order = CreateTestOrder("newuser");

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.UserName.Should().Be("newuser");

        // Verify it was actually inserted
        var savedOrder = await _repository.GetByIdAsync(result.Id);
        savedOrder.Should().NotBeNull();
        savedOrder!.UserName.Should().Be("newuser");
    }

    [Fact]
    public async Task AddAsync_ShouldSetAuditFields_CreatedDateAndCreatedBy()
    {
        // Arrange
        var order = CreateTestOrder("audituser");

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        result.CreatedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        result.CreatedBy.Should().Be("swn");
        result.LastModifiedDate.Should().Be(default(DateTime));
        result.LastModifiedBy.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistAllProperties()
    {
        // Arrange
        var order = new Order
        {
            UserName = "fulluser",
            TotalPrice = 999.99m,
            FirstName = "John",
            LastName = "Doe",
            EmailAddress = "john.doe@test.com",
            AddressLine = "123 Test Street",
            Country = "USA",
            State = "California",
            ZipCode = "90210",
            CardName = "John Doe",
            CardNumber = "4111111111111111",
            Expiration = "12/25",
            CVV = "123",
            PaymentMethod = 1
        };

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        var saved = await _repository.GetByIdAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.UserName.Should().Be("fulluser");
        saved.TotalPrice.Should().Be(999.99m);
        saved.FirstName.Should().Be("John");
        saved.LastName.Should().Be("Doe");
        saved.EmailAddress.Should().Be("john.doe@test.com");
        saved.AddressLine.Should().Be("123 Test Street");
        saved.Country.Should().Be("USA");
        saved.State.Should().Be("California");
        saved.ZipCode.Should().Be("90210");
        saved.CardName.Should().Be("John Doe");
        saved.CardNumber.Should().Be("4111111111111111");
        saved.Expiration.Should().Be("12/25");
        saved.CVV.Should().Be("123");
        saved.PaymentMethod.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_ShouldHandleDecimalPrecision()
    {
        // Arrange
        var order = CreateTestOrder("decimaluser");
        order.TotalPrice = 123.456789m; // More precision than decimal(18,2)

        // Act
        var result = await _repository.AddAsync(order);

        // Assert - SQL Server will round to 2 decimal places
        var saved = await _repository.GetByIdAsync(result.Id);
        saved!.TotalPrice.Should().Be(123.46m); // Rounded
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateOrder()
    {
        // Arrange
        var order = CreateTestOrder("updateuser");
        var added = await _repository.AddAsync(order);

        // Modify the order
        added.TotalPrice = 200.00m;
        added.FirstName = "UpdatedName";

        // Act
        await _repository.UpdateAsync(added);

        // Assert
        var updated = await _repository.GetByIdAsync(added.Id);
        updated.Should().NotBeNull();
        updated!.TotalPrice.Should().Be(200.00m);
        updated.FirstName.Should().Be("UpdatedName");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetAuditFields_LastModifiedDateAndLastModifiedBy()
    {
        // Arrange
        var order = CreateTestOrder("auditupdate");
        var added = await _repository.AddAsync(order);
        var originalCreatedDate = added.CreatedDate;

        // Small delay to ensure different timestamps
        await Task.Delay(100);

        // Act
        added.TotalPrice = 300.00m;
        await _repository.UpdateAsync(added);

        // Assert
        var updated = await _repository.GetByIdAsync(added.Id);
        updated.Should().NotBeNull();
        updated!.CreatedDate.Should().Be(originalCreatedDate); // Should NOT change
        updated.CreatedBy.Should().Be("swn"); // Should NOT change
        updated.LastModifiedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        updated.LastModifiedBy.Should().Be("swn");
    }

    [Fact]
    public async Task UpdateAsync_ShouldOnlyUpdateSpecifiedOrder()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        var order2 = CreateTestOrder("user2");
        var added1 = await _repository.AddAsync(order1);
        var added2 = await _repository.AddAsync(order2);

        // Act - Update only order1
        added1.TotalPrice = 500.00m;
        await _repository.UpdateAsync(added1);

        // Assert - order1 should be updated
        var updated1 = await _repository.GetByIdAsync(added1.Id);
        updated1!.TotalPrice.Should().Be(500.00m);

        // Assert - order2 should remain unchanged
        var unchanged2 = await _repository.GetByIdAsync(added2.Id);
        unchanged2!.TotalPrice.Should().Be(100.50m);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveOrder()
    {
        // Arrange
        var order = CreateTestOrder("deleteuser");
        var added = await _repository.AddAsync(order);

        // Verify it exists
        var beforeDelete = await _repository.GetByIdAsync(added.Id);
        beforeDelete.Should().NotBeNull();

        // Act
        await _repository.DeleteAsync(added);

        // Assert
        var afterDelete = await _repository.GetByIdAsync(added.Id);
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotAffectOtherOrders()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        var order2 = CreateTestOrder("user2");
        var order3 = CreateTestOrder("user3");

        var added1 = await _repository.AddAsync(order1);
        var added2 = await _repository.AddAsync(order2);
        var added3 = await _repository.AddAsync(order3);

        // Act - Delete only order2
        await _repository.DeleteAsync(added2);

        // Assert
        var result1 = await _repository.GetByIdAsync(added1.Id);
        result1.Should().NotBeNull("order1 should still exist");

        var result2 = await _repository.GetByIdAsync(added2.Id);
        result2.Should().BeNull("order2 should be deleted");

        var result3 = await _repository.GetByIdAsync(added3.Id);
        result3.Should().NotBeNull("order3 should still exist");
    }

    #endregion

    #region GetAsync with Predicate Tests

    [Fact]
    public async Task GetAsync_WithPredicate_ShouldFilterOrders()
    {
        // Arrange
        var order1 = CreateTestOrder("user1");
        order1.TotalPrice = 100m;
        var order2 = CreateTestOrder("user2");
        order2.TotalPrice = 200m;
        var order3 = CreateTestOrder("user3");
        order3.TotalPrice = 300m;

        await _repository.AddAsync(order1);
        await _repository.AddAsync(order2);
        await _repository.AddAsync(order3);

        // Act - Get orders with TotalPrice > 150
        var result = await _repository.GetAsync(o => o.TotalPrice > 150m);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(o => o.UserName == "user2");
        result.Should().Contain(o => o.UserName == "user3");
        result.Should().NotContain(o => o.UserName == "user1");
    }

    #endregion

    #region EF Core Change Tracking Tests

    [Fact]
    public async Task EFCore_ShouldTrackChanges_WhenDisableTrackingFalse()
    {
        // Arrange
        var order = CreateTestOrder("trackuser");
        var added = await _repository.AddAsync(order);

        // Act - Get with tracking enabled (use 3-param overload explicitly)
        var tracked = await _repository.GetAsync(
            o => o.Id == added.Id,
            null,
            (string)null!,
            false);

        // Modify without explicitly calling UpdateAsync
        tracked.First().TotalPrice = 999.99m;
        await _context.SaveChangesAsync();

        // Assert - Change should be persisted
        var updated = await _repository.GetByIdAsync(added.Id);
        updated!.TotalPrice.Should().Be(999.99m);
    }

    [Fact]
    public async Task EFCore_ShouldNotTrackChanges_WhenDisableTrackingTrue()
    {
        // Arrange
        var order = CreateTestOrder("notrackuser");
        var added = await _repository.AddAsync(order);

        // Act - Get with tracking disabled (use 3-param overload explicitly)
        var untracked = await _repository.GetAsync(
            o => o.Id == added.Id,
            null,
            (string)null!,
            true);

        // Modify without explicitly calling UpdateAsync
        untracked.First().TotalPrice = 888.88m;
        await _context.SaveChangesAsync();

        // Assert - Change should NOT be persisted
        var notUpdated = await _repository.GetByIdAsync(added.Id);
        notUpdated!.TotalPrice.Should().Be(100.50m); // Original value
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentInserts_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<Order>>();

        // Act - Create 10 orders concurrently
        for (int i = 1; i <= 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Each task needs its own context for concurrency
                var options = new DbContextOptionsBuilder<OrderContext>()
                    .UseSqlServer(_dbFixture.ConnectionString)
                    .Options;
                using var context = new OrderContext(options);
                var repo = new OrderRepository(context);

                var order = CreateTestOrder($"concurrent{index}");
                return await repo.AddAsync(order);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyHaveUniqueItems(o => o.Id);

        // Verify all were inserted
        var allOrders = await _repository.GetAllAsync();
        allOrders.Should().HaveCountGreaterOrEqualTo(10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Order_ShouldHandleVeryLongStrings()
    {
        // Arrange
        var order = CreateTestOrder("longstring");
        order.AddressLine = new string('A', 500); // Very long address

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        var saved = await _repository.GetByIdAsync(result.Id);
        saved!.AddressLine.Should().HaveLength(500);
    }

    [Fact]
    public async Task Order_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var order = CreateTestOrder("User's \"Special\" & <Name>");
        order.EmailAddress = "test+tag@example.com";
        order.AddressLine = "123 O'Reilly St. Apt #5";

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        var saved = await _repository.GetByIdAsync(result.Id);
        saved!.UserName.Should().Be("User's \"Special\" & <Name>");
        saved.EmailAddress.Should().Be("test+tag@example.com");
        saved.AddressLine.Should().Be("123 O'Reilly St. Apt #5");
    }

    [Fact]
    public async Task Order_ShouldHandleZeroTotalPrice()
    {
        // Arrange
        var order = CreateTestOrder("zerouser");
        order.TotalPrice = 0m;

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        var saved = await _repository.GetByIdAsync(result.Id);
        saved!.TotalPrice.Should().Be(0m);
    }

    [Fact]
    public async Task Order_ShouldHandleNegativeTotalPrice()
    {
        // Arrange
        var order = CreateTestOrder("negativeuser");
        order.TotalPrice = -50.00m;

        // Act
        var result = await _repository.AddAsync(order);

        // Assert
        var saved = await _repository.GetByIdAsync(result.Id);
        saved!.TotalPrice.Should().Be(-50.00m);
    }

    #endregion

    #region Helper Methods

    private Order CreateTestOrder(string userName)
    {
        return new Order
        {
            UserName = userName,
            TotalPrice = 100.50m,
            FirstName = "Test",
            LastName = "User",
            EmailAddress = $"{userName}@test.com",
            AddressLine = "123 Test St",
            Country = "USA",
            State = "CA",
            ZipCode = "12345",
            CardName = "Test User",
            CardNumber = "4111111111111111",
            Expiration = "12/25",
            CVV = "123",
            PaymentMethod = 1
        };
    }

    #endregion
}

