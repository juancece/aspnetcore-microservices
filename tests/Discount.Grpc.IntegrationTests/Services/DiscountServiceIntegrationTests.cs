using Dapper;
using Discount.Grpc.Protos;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using TestHelpers;
using Xunit;

namespace Discount.Grpc.IntegrationTests.Services;

public class DiscountServiceIntegrationTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _dbFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private GrpcChannel _channel = null!;
    private DiscountProtoService.DiscountProtoServiceClient _client = null!;

    public DiscountServiceIntegrationTests(PostgreSqlFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.ResetDatabaseAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["DatabaseSettings:ConnectionString"] = _dbFixture.ConnectionString
                    });
                });
            });

        var client = _factory.CreateDefaultClient();
        _channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });
        _client = new DiscountProtoService.DiscountProtoServiceClient(_channel);
    }

    public Task DisposeAsync()
    {
        _channel?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetDiscount_WithExistingProduct_ShouldReturnCoupon()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Coupon (ProductName, Description, Amount) VALUES (@ProductName, @Description, @Amount)",
            new { ProductName = "Test Product", Description = "Test Desc", Amount = 50 });

        // Act
        var request = new GetDiscountRequest { ProductName = "Test Product" };
        var response = await _client.GetDiscountAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.ProductName.Should().Be("Test Product");
        response.Description.Should().Be("Test Desc");
        response.Amount.Should().Be(50);
    }

    [Fact]
    public async Task GetDiscount_WithNonExistentProduct_ShouldReturnDefaultCoupon()
    {
        // Arrange
        var request = new GetDiscountRequest { ProductName = "NonExistent" };

        // Act
        var response = await _client.GetDiscountAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.ProductName.Should().Be("NonExistent");
        response.Description.Should().Be("No Discount Desc");
        response.Amount.Should().Be(0);
    }

    [Fact]
    public async Task CreateDiscount_ShouldAddCouponToDatabase()
    {
        // Arrange
        var request = new CreateDiscountRequest
        {
            Coupon = new CouponModel
            {
                ProductName = "New Product",
                Description = "New Discount",
                Amount = 100
            }
        };

        // Act
        var response = await _client.CreateDiscountAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();

        // Verify in database
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var coupon = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM Coupon WHERE ProductName = @ProductName",
            new { ProductName = "New Product" });
        coupon.Should().NotBeNull();
        ((string)coupon.description).Should().Be("New Discount");
        ((int)coupon.amount).Should().Be(100);
    }

    [Fact]
    public async Task UpdateDiscount_WithExistingCoupon_ShouldModifyCoupon()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Coupon (ProductName, Description, Amount) VALUES (@ProductName, @Description, @Amount)",
            new { ProductName = "Update Product", Description = "Old Desc", Amount = 50 });

        var request = new UpdateDiscountRequest
        {
            Coupon = new CouponModel
            {
                ProductName = "Update Product",
                Description = "Updated Desc",
                Amount = 75
            }
        };

        // Act
        var response = await _client.UpdateDiscountAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();

        // Verify update
        var coupon = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM Coupon WHERE ProductName = @ProductName",
            new { ProductName = "Update Product" });
        ((string)coupon.description).Should().Be("Updated Desc");
        ((int)coupon.amount).Should().Be(75);
    }

    [Fact]
    public async Task DeleteDiscount_WithExistingCoupon_ShouldRemoveCoupon()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Coupon (ProductName, Description, Amount) VALUES (@ProductName, @Description, @Amount)",
            new { ProductName = "Delete Product", Description = "Delete Desc", Amount = 50 });

        var request = new DeleteDiscountRequest { ProductName = "Delete Product" };

        // Act
        var response = await _client.DeleteDiscountAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();

        // Verify deletion
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Coupon WHERE ProductName = @ProductName",
            new { ProductName = "Delete Product" });
        count.Should().Be(0);
    }

    [Fact]
    public async Task CreateDiscount_WithDuplicateProduct_ShouldFail()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Coupon (ProductName, Description, Amount) VALUES (@ProductName, @Description, @Amount)",
            new { ProductName = "Duplicate", Description = "Original", Amount = 50 });

        var request = new CreateDiscountRequest
        {
            Coupon = new CouponModel
            {
                ProductName = "Duplicate",
                Description = "Duplicate Attempt",
                Amount = 100
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await _client.CreateDiscountAsync(request));
        exception.StatusCode.Should().Be(StatusCode.Internal);
    }

    [Fact]
    public async Task GetDiscount_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        const string productName = "Test'Product\"With<>Chars";
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Coupon (ProductName, Description, Amount) VALUES (@ProductName, @Description, @Amount)",
            new { ProductName = productName, Description = "Special Chars", Amount = 25 });

        // Act
        var request = new GetDiscountRequest { ProductName = productName };
        var response = await _client.GetDiscountAsync(request);

        // Assert
        response.ProductName.Should().Be(productName);
        response.Amount.Should().Be(25);
    }

    [Fact]
    public async Task CreateDiscount_WithZeroAmount_ShouldSucceed()
    {
        // Arrange
        var request = new CreateDiscountRequest
        {
            Coupon = new CouponModel
            {
                ProductName = "Zero Amount",
                Description = "No Discount",
                Amount = 0
            }
        };

        // Act
        var response = await _client.CreateDiscountAsync(request);

        // Assert
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDiscount_WithNonExistentCoupon_ShouldReturnFalse()
    {
        // Arrange
        var request = new UpdateDiscountRequest
        {
            Coupon = new CouponModel
            {
                ProductName = "NonExistent",
                Description = "Should Fail",
                Amount = 100
            }
        };

        // Act
        var response = await _client.UpdateDiscountAsync(request);

        // Assert
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDiscount_WithNonExistentCoupon_ShouldReturnFalse()
    {
        // Arrange
        var request = new DeleteDiscountRequest { ProductName = "NonExistent" };

        // Act
        var response = await _client.DeleteDiscountAsync(request);

        // Assert
        response.Success.Should().BeFalse();
    }
}

