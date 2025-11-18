using Basket.API.Entities;
using Basket.API.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using TestHelpers;
using Xunit;

namespace Basket.API.IntegrationTests.Repositories;

/// <summary>
/// Integration tests for BasketRepository using REAL Redis
/// Tests actual serialization, storage, and retrieval with Testcontainers
/// </summary>
[Trait("Category", "Integration")]
public class BasketRepositoryIntegrationTests : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private readonly RedisFixture _redisFixture;
    private IBasketRepository _repository = null!;
    private IDistributedCache _cache = null!;

    public BasketRepositoryIntegrationTests(RedisFixture redisFixture)
    {
        _redisFixture = redisFixture;
    }

    public async Task InitializeAsync()
    {
        // Reset Redis before each test
        await _redisFixture.ResetDatabaseAsync();
        
        // Create repository with real Redis connection
        _cache = _redisFixture.CreateDistributedCache();
        _repository = new BasketRepository(_cache);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region GetBasket Tests

    [Fact]
    public async Task GetBasket_ShouldReturnNull_WhenBasketDoesNotExist()
    {
        // Arrange
        var userName = "nonexistent-user";

        // Act
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBasket_ShouldReturnBasket_WhenBasketExists()
    {
        // Arrange
        var userName = "testuser";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "iPhone 13",
                    Price = 999.99m,
                    Quantity = 1,
                    Color = "Black"
                }
            }
        };

        // Act - Store first
        await _repository.UpdateBasket(basket);

        // Act - Then retrieve
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(userName);
        result.Items.Should().HaveCount(1);
        result.Items[0].ProductId.Should().Be("prod1");
        result.Items[0].ProductName.Should().Be("iPhone 13");
        result.Items[0].Price.Should().Be(999.99m);
        result.Items[0].Quantity.Should().Be(1);
        result.Items[0].Color.Should().Be("Black");
    }

    [Fact]
    public async Task GetBasket_ShouldHandleMultipleItems()
    {
        // Arrange
        var userName = "multiitem-user";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "iPhone 13",
                    Price = 999.99m,
                    Quantity = 2,
                    Color = "Black"
                },
                new()
                {
                    ProductId = "prod2",
                    ProductName = "AirPods Pro",
                    Price = 249.99m,
                    Quantity = 1,
                    Color = "White"
                },
                new()
                {
                    ProductId = "prod3",
                    ProductName = "MacBook Pro",
                    Price = 2499.99m,
                    Quantity = 1,
                    Color = "Silver"
                }
            }
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Items.Should().ContainSingle(i => i.ProductName == "iPhone 13");
        result.Items.Should().ContainSingle(i => i.ProductName == "AirPods Pro");
        result.Items.Should().ContainSingle(i => i.ProductName == "MacBook Pro");
    }

    #endregion

    #region UpdateBasket Tests

    [Fact]
    public async Task UpdateBasket_ShouldCreateNewBasket_WhenBasketDoesNotExist()
    {
        // Arrange
        var userName = "newuser";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "Test Product",
                    Price = 100m,
                    Quantity = 1,
                    Color = "Red"
                }
            }
        };

        // Act
        var result = await _repository.UpdateBasket(basket);

        // Assert
        result.Should().NotBeNull();
        result.UserName.Should().Be(userName);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateBasket_ShouldOverwriteExistingBasket()
    {
        // Arrange
        var userName = "updateuser";
        
        // Original basket
        var originalBasket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "Original Product",
                    Price = 100m,
                    Quantity = 1,
                    Color = "Red"
                }
            }
        };

        // Updated basket
        var updatedBasket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod2",
                    ProductName = "Updated Product",
                    Price = 200m,
                    Quantity = 2,
                    Color = "Blue"
                },
                new()
                {
                    ProductId = "prod3",
                    ProductName = "Another Product",
                    Price = 150m,
                    Quantity = 1,
                    Color = "Green"
                }
            }
        };

        // Act
        await _repository.UpdateBasket(originalBasket);
        await _repository.UpdateBasket(updatedBasket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(i => i.ProductName == "Original Product");
        result.Items.Should().ContainSingle(i => i.ProductName == "Updated Product");
        result.Items.Should().ContainSingle(i => i.ProductName == "Another Product");
    }

    [Fact]
    public async Task UpdateBasket_ShouldPersistAllProperties()
    {
        // Arrange
        var userName = "proptest-user";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod-123",
                    ProductName = "Test Product with Special Chars: éàü@#$%",
                    Price = 1234.56m,
                    Quantity = 99,
                    Color = "Multi-Color: Red/Blue/Green"
                }
            }
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert - Verify exact values
        result.Should().NotBeNull();
        var item = result!.Items[0];
        item.ProductId.Should().Be("prod-123");
        item.ProductName.Should().Be("Test Product with Special Chars: éàü@#$%");
        item.Price.Should().Be(1234.56m);
        item.Quantity.Should().Be(99);
        item.Color.Should().Be("Multi-Color: Red/Blue/Green");
    }

    [Fact]
    public async Task UpdateBasket_ShouldHandleLargeBasket()
    {
        // Arrange
        var userName = "largebasket-user";
        var items = new List<ShoppingCartItem>();
        
        // Create 50 items
        for (int i = 1; i <= 50; i++)
        {
            items.Add(new ShoppingCartItem
            {
                ProductId = $"prod-{i}",
                ProductName = $"Product {i}",
                Price = i * 10m,
                Quantity = i,
                Color = $"Color-{i}"
            });
        }

        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = items
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(50);
        result.Items.Should().ContainSingle(i => i.ProductId == "prod-1");
        result.Items.Should().ContainSingle(i => i.ProductId == "prod-50");
    }

    #endregion

    #region DeleteBasket Tests

    [Fact]
    public async Task DeleteBasket_ShouldRemoveBasket_WhenBasketExists()
    {
        // Arrange
        var userName = "deleteuser";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "Test Product",
                    Price = 100m,
                    Quantity = 1,
                    Color = "Red"
                }
            }
        };

        // Act - Create basket first
        await _repository.UpdateBasket(basket);
        var beforeDelete = await _repository.GetBasket(userName);
        
        // Act - Delete basket
        await _repository.DeleteBasket(userName);
        var afterDelete = await _repository.GetBasket(userName);

        // Assert
        beforeDelete.Should().NotBeNull();
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBasket_ShouldNotThrow_WhenBasketDoesNotExist()
    {
        // Arrange
        var userName = "nonexistent-delete-user";

        // Act
        Func<Task> act = async () => await _repository.DeleteBasket(userName);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteBasket_ShouldNotAffectOtherBaskets()
    {
        // Arrange
        var user1 = "user1";
        var user2 = "user2";
        var user3 = "user3";

        var basket1 = new ShoppingCart
        {
            UserName = user1,
            Items = new List<ShoppingCartItem>
            {
                new() { ProductId = "p1", ProductName = "Product 1", Price = 100m, Quantity = 1, Color = "Red" }
            }
        };

        var basket2 = new ShoppingCart
        {
            UserName = user2,
            Items = new List<ShoppingCartItem>
            {
                new() { ProductId = "p2", ProductName = "Product 2", Price = 200m, Quantity = 2, Color = "Blue" }
            }
        };

        var basket3 = new ShoppingCart
        {
            UserName = user3,
            Items = new List<ShoppingCartItem>
            {
                new() { ProductId = "p3", ProductName = "Product 3", Price = 300m, Quantity = 3, Color = "Green" }
            }
        };

        // Act - Create 3 baskets
        await _repository.UpdateBasket(basket1);
        await _repository.UpdateBasket(basket2);
        await _repository.UpdateBasket(basket3);

        // Act - Delete only basket2
        await _repository.DeleteBasket(user2);

        // Assert
        var result1 = await _repository.GetBasket(user1);
        var result2 = await _repository.GetBasket(user2);
        var result3 = await _repository.GetBasket(user3);

        result1.Should().NotBeNull("basket1 should still exist");
        result2.Should().BeNull("basket2 should be deleted");
        result3.Should().NotBeNull("basket3 should still exist");
    }

    #endregion

    #region Edge Cases & Serialization Tests

    [Fact]
    public async Task Basket_ShouldHandleEmptyItemsList()
    {
        // Arrange
        var userName = "emptyitems-user";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>()
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Basket_ShouldHandleDecimalPrecision()
    {
        // Arrange
        var userName = "decimal-user";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "Precise Product",
                    Price = 123.456789m, // High precision
                    Quantity = 1,
                    Color = "Blue"
                }
            }
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.Items[0].Price.Should().Be(123.456789m);
    }

    [Fact]
    public async Task Basket_ShouldHandleSpecialCharactersInUserName()
    {
        // Arrange
        var userName = "user+test@example.com";
        var basket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new()
                {
                    ProductId = "prod1",
                    ProductName = "Test Product",
                    Price = 100m,
                    Quantity = 1,
                    Color = "Red"
                }
            }
        };

        // Act
        await _repository.UpdateBasket(basket);
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(userName);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentUpdates_ShouldHandleMultipleUsers()
    {
        // Arrange
        var tasks = new List<Task>();
        var users = Enumerable.Range(1, 10).Select(i => $"concurrent-user-{i}").ToList();

        // Act - Create 10 baskets concurrently
        foreach (var user in users)
        {
            tasks.Add(Task.Run(async () =>
            {
                var basket = new ShoppingCart
                {
                    UserName = user,
                    Items = new List<ShoppingCartItem>
                    {
                        new()
                        {
                            ProductId = $"prod-{user}",
                            ProductName = $"Product for {user}",
                            Price = 100m,
                            Quantity = 1,
                            Color = "Red"
                        }
                    }
                };
                await _repository.UpdateBasket(basket);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All baskets should exist
        foreach (var user in users)
        {
            var result = await _repository.GetBasket(user);
            result.Should().NotBeNull($"basket for {user} should exist");
            result!.UserName.Should().Be(user);
        }
    }

    #endregion
}

