using Dapper;
using Discount.API.Entities;
using Discount.API.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TestHelpers;
using Xunit;

namespace Discount.API.IntegrationTests.Repositories;

/// <summary>
/// Integration tests for DiscountRepository using REAL PostgreSQL
/// Tests actual SQL queries, Dapper mapping, and database constraints with Testcontainers
/// </summary>
[Trait("Category", "Integration")]
public class DiscountRepositoryIntegrationTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _dbFixture;
    private IDiscountRepository _repository = null!;
    private IConfiguration _configuration = null!;

    public DiscountRepositoryIntegrationTests(PostgreSqlFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        // Create schema and reset database before each test
        await CreateSchema();
        await ClearData();
        
        // Create repository with real connection string
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = _dbFixture.ConnectionString
            })
            .Build();
        
        _repository = new DiscountRepository(_configuration);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task CreateSchema()
    {
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Coupon (
                Id SERIAL PRIMARY KEY,
                ProductName VARCHAR(24) NOT NULL,
                Description TEXT,
                Amount INTEGER
            );";

        await connection.ExecuteAsync(createTableSql);
    }

    private async Task ClearData()
    {
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM Coupon");
    }

    #region GetDiscount Tests

    [Fact]
    public async Task GetDiscount_ShouldReturnNoDiscount_WhenCouponDoesNotExist()
    {
        // Arrange
        var productName = "NonExistentProduct";

        // Act
        var result = await _repository.GetDiscount(productName);

        // Assert
        result.Should().NotBeNull();
        result.ProductName.Should().Be("No Discount");
        result.Amount.Should().Be(0);
        result.Description.Should().Be("No Discount Desc");
    }

    [Fact]
    public async Task GetDiscount_ShouldReturnCoupon_WhenCouponExists()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "IPhone X",
            Description = "IPhone X Discount",
            Amount = 150
        };
        await _repository.CreateDiscount(coupon);

        // Act
        var result = await _repository.GetDiscount("IPhone X");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ProductName.Should().Be("IPhone X");
        result.Description.Should().Be("IPhone X Discount");
        result.Amount.Should().Be(150);
    }

    [Fact]
    public async Task GetDiscount_ShouldBeCaseSensitive()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "IPhone X",
            Description = "IPhone X Discount",
            Amount = 150
        };
        await _repository.CreateDiscount(coupon);

        // Act
        var result = await _repository.GetDiscount("iphone x"); // lowercase

        // Assert - Should not find it (case-sensitive)
        result.ProductName.Should().Be("No Discount");
    }

    #endregion

    #region CreateDiscount Tests

    [Fact]
    public async Task CreateDiscount_ShouldInsertCoupon_AndGenerateId()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "Samsung 10",
            Description = "Samsung 10 Discount",
            Amount = 100
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        // Verify it was actually inserted
        var savedCoupon = await _repository.GetDiscount("Samsung 10");
        savedCoupon.Should().NotBeNull();
        savedCoupon.Id.Should().BeGreaterThan(0);
        savedCoupon.ProductName.Should().Be("Samsung 10");
        savedCoupon.Amount.Should().Be(100);
    }

    [Fact]
    public async Task CreateDiscount_ShouldAllowDuplicateProductNames()
    {
        // Arrange - Create two coupons with same product name
        var coupon1 = new Coupon
        {
            ProductName = "IPhone X",
            Description = "First Discount",
            Amount = 100
        };

        var coupon2 = new Coupon
        {
            ProductName = "IPhone X",
            Description = "Second Discount",
            Amount = 200
        };

        // Act
        var result1 = await _repository.CreateDiscount(coupon1);
        var result2 = await _repository.CreateDiscount(coupon2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();

        // Verify both exist in database
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Coupon WHERE ProductName = @ProductName",
            new { ProductName = "IPhone X" });
        count.Should().Be(2);
    }

    [Fact]
    public async Task CreateDiscount_ShouldHandleSpecialCharacters()
    {
        // Arrange - Product name must be <= 24 chars (schema constraint)
        var coupon = new Coupon
        {
            ProductName = "Product's \"Test\"",
            Description = "Description with special chars: é à ü $ % & @",
            Amount = 50
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var savedCoupon = await _repository.GetDiscount("Product's \"Test\"");
        savedCoupon.ProductName.Should().Be("Product's \"Test\"");
        savedCoupon.Description.Should().Be("Description with special chars: é à ü $ % & @");
    }

    [Fact]
    public async Task CreateDiscount_ShouldHandleNegativeAmount()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "Negative Product",
            Description = "Negative Discount (surcharge)",
            Amount = -50
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var savedCoupon = await _repository.GetDiscount("Negative Product");
        savedCoupon.Amount.Should().Be(-50);
    }

    [Fact]
    public async Task CreateDiscount_ShouldHandleZeroAmount()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "Zero Product",
            Description = "Zero Discount",
            Amount = 0
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var savedCoupon = await _repository.GetDiscount("Zero Product");
        savedCoupon.Amount.Should().Be(0);
    }

    #endregion

    #region UpdateDiscount Tests

    [Fact]
    public async Task UpdateDiscount_ShouldUpdateExistingCoupon()
    {
        // Arrange - Create initial coupon
        var originalCoupon = new Coupon
        {
            ProductName = "Original Product",
            Description = "Original Description",
            Amount = 100
        };
        await _repository.CreateDiscount(originalCoupon);
        var saved = await _repository.GetDiscount("Original Product");

        // Modify the coupon
        var updatedCoupon = new Coupon
        {
            Id = saved.Id,
            ProductName = "Updated Product",
            Description = "Updated Description",
            Amount = 200
        };

        // Act
        var result = await _repository.UpdateDiscount(updatedCoupon);

        // Assert
        result.Should().BeTrue();

        // Verify update
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var updated = await connection.QueryFirstOrDefaultAsync<Coupon>(
            "SELECT * FROM Coupon WHERE Id = @Id",
            new { Id = saved.Id });

        updated.Should().NotBeNull();
        updated!.ProductName.Should().Be("Updated Product");
        updated.Description.Should().Be("Updated Description");
        updated.Amount.Should().Be(200);
    }

    [Fact]
    public async Task UpdateDiscount_ShouldReturnFalse_WhenCouponDoesNotExist()
    {
        // Arrange - Coupon with non-existent ID
        var coupon = new Coupon
        {
            Id = 99999,
            ProductName = "NonExistent",
            Description = "Does not exist",
            Amount = 100
        };

        // Act
        var result = await _repository.UpdateDiscount(coupon);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateDiscount_ShouldUpdateOnlySpecifiedCoupon()
    {
        // Arrange - Create two coupons
        var coupon1 = new Coupon
        {
            ProductName = "Product1",
            Description = "Description1",
            Amount = 100
        };
        var coupon2 = new Coupon
        {
            ProductName = "Product2",
            Description = "Description2",
            Amount = 200
        };

        await _repository.CreateDiscount(coupon1);
        await _repository.CreateDiscount(coupon2);

        var saved1 = await _repository.GetDiscount("Product1");
        var saved2 = await _repository.GetDiscount("Product2");

        // Update only coupon1
        var updatedCoupon1 = new Coupon
        {
            Id = saved1.Id,
            ProductName = "UpdatedProduct1",
            Description = "UpdatedDescription1",
            Amount = 150
        };

        // Act
        await _repository.UpdateDiscount(updatedCoupon1);

        // Assert - coupon1 should be updated
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var result1 = await connection.QueryFirstOrDefaultAsync<Coupon>(
            "SELECT * FROM Coupon WHERE Id = @Id",
            new { Id = saved1.Id });
        result1!.ProductName.Should().Be("UpdatedProduct1");

        // Assert - coupon2 should remain unchanged
        var result2 = await connection.QueryFirstOrDefaultAsync<Coupon>(
            "SELECT * FROM Coupon WHERE Id = @Id",
            new { Id = saved2.Id });
        result2!.ProductName.Should().Be("Product2");
        result2.Amount.Should().Be(200);
    }

    #endregion

    #region DeleteDiscount Tests

    [Fact]
    public async Task DeleteDiscount_ShouldDeleteCoupon_WhenExists()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "ToDelete",
            Description = "Will be deleted",
            Amount = 100
        };
        await _repository.CreateDiscount(coupon);

        // Verify it exists
        var beforeDelete = await _repository.GetDiscount("ToDelete");
        beforeDelete.Id.Should().BeGreaterThan(0);

        // Act
        var result = await _repository.DeleteDiscount("ToDelete");

        // Assert
        result.Should().BeTrue();

        // Verify it was deleted
        var afterDelete = await _repository.GetDiscount("ToDelete");
        afterDelete.ProductName.Should().Be("No Discount");
    }

    [Fact]
    public async Task DeleteDiscount_ShouldReturnFalse_WhenCouponDoesNotExist()
    {
        // Act
        var result = await _repository.DeleteDiscount("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDiscount_ShouldDeleteAllMatchingCoupons()
    {
        // Arrange - Create two coupons with same product name
        var coupon1 = new Coupon
        {
            ProductName = "DuplicateProduct",
            Description = "First",
            Amount = 100
        };
        var coupon2 = new Coupon
        {
            ProductName = "DuplicateProduct",
            Description = "Second",
            Amount = 200
        };

        await _repository.CreateDiscount(coupon1);
        await _repository.CreateDiscount(coupon2);

        // Act - Delete by product name (deletes ALL matching records per SQL DELETE behavior)
        var result = await _repository.DeleteDiscount("DuplicateProduct");

        // Assert
        result.Should().BeTrue();

        // Verify all matching coupons were deleted (SQL DELETE deletes all matches)
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Coupon WHERE ProductName = @ProductName",
            new { ProductName = "DuplicateProduct" });
        count.Should().Be(0, "SQL DELETE removes ALL matching rows, not just first");
    }

    [Fact]
    public async Task DeleteDiscount_ShouldNotAffectOtherCoupons()
    {
        // Arrange - Create multiple coupons
        await _repository.CreateDiscount(new Coupon
        {
            ProductName = "Product1",
            Description = "Description1",
            Amount = 100
        });
        await _repository.CreateDiscount(new Coupon
        {
            ProductName = "Product2",
            Description = "Description2",
            Amount = 200
        });
        await _repository.CreateDiscount(new Coupon
        {
            ProductName = "Product3",
            Description = "Description3",
            Amount = 300
        });

        // Act - Delete only Product2
        await _repository.DeleteDiscount("Product2");

        // Assert - Other products should still exist
        var product1 = await _repository.GetDiscount("Product1");
        product1.Id.Should().BeGreaterThan(0);
        product1.Amount.Should().Be(100);

        var product2 = await _repository.GetDiscount("Product2");
        product2.ProductName.Should().Be("No Discount");

        var product3 = await _repository.GetDiscount("Product3");
        product3.Id.Should().BeGreaterThan(0);
        product3.Amount.Should().Be(300);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentInserts_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<bool>>();

        // Act - Create 10 coupons concurrently
        for (int i = 1; i <= 10; i++)
        {
            var index = i; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                var coupon = new Coupon
                {
                    ProductName = $"ConcurrentProduct{index}",
                    Description = $"Concurrent discount {index}",
                    Amount = index * 10
                };
                return await _repository.CreateDiscount(coupon);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        // Verify all were inserted
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Coupon WHERE ProductName LIKE 'ConcurrentProduct%'");
        count.Should().Be(10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Coupon_ShouldHandleVeryLongDescription()
    {
        // Arrange
        var longDescription = new string('A', 1000); // 1000 characters
        var coupon = new Coupon
        {
            ProductName = "LongDesc",
            Description = longDescription,
            Amount = 50
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var saved = await _repository.GetDiscount("LongDesc");
        saved.Description.Should().Be(longDescription);
    }

    [Fact]
    public async Task Coupon_ShouldHandleMaxIntAmount()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "MaxAmount",
            Description = "Max amount test",
            Amount = int.MaxValue
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var saved = await _repository.GetDiscount("MaxAmount");
        saved.Amount.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Coupon_ShouldHandleMinIntAmount()
    {
        // Arrange
        var coupon = new Coupon
        {
            ProductName = "MinAmount",
            Description = "Min amount test",
            Amount = int.MinValue
        };

        // Act
        var result = await _repository.CreateDiscount(coupon);

        // Assert
        result.Should().BeTrue();

        var saved = await _repository.GetDiscount("MinAmount");
        saved.Amount.Should().Be(int.MinValue);
    }

    #endregion
}

