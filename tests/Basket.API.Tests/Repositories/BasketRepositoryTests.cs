using Basket.API.Entities;
using Basket.API.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace Basket.API.Tests.Repositories;

/// <summary>
/// Unit tests for BasketRepository
/// Tests repository logic with IDistributedCache mocked
/// </summary>
[Trait("Category", "Unit")]
public class BasketRepositoryTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly BasketRepository _repository;

    public BasketRepositoryTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _repository = new BasketRepository(_mockCache.Object);
    }

    [Fact]
    public async Task GetBasket_ShouldReturnBasket_WhenBasketExists()
    {
        // Arrange
        var userName = "testuser";
        var basketJson = "{\"UserName\":\"testuser\",\"Items\":[{\"Quantity\":2,\"Color\":\"Red\",\"Price\":100,\"ProductId\":\"1\",\"ProductName\":\"Product1\"}]}";
        var basketBytes = System.Text.Encoding.UTF8.GetBytes(basketJson);
        
        _mockCache
            .Setup(x => x.GetAsync(userName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(basketBytes);

        // Act
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(userName);
        result.Items.Should().HaveCount(1);
        result.Items[0].ProductName.Should().Be("Product1");
    }

    [Fact]
    public async Task GetBasket_ShouldReturnNull_WhenBasketDoesNotExist()
    {
        // Arrange
        var userName = "nonexistent";
        _mockCache
            .Setup(x => x.GetAsync(userName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _repository.GetBasket(userName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateBasket_ShouldSerializeAndStore()
    {
        // Arrange
        var basket = new ShoppingCart
        {
            UserName = "testuser",
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "Product1", Price = 100, Quantity = 2, Color = "Red", ProductId = "1" }
            }
        };

        var basketJson = Newtonsoft.Json.JsonConvert.SerializeObject(basket);
        var basketBytes = System.Text.Encoding.UTF8.GetBytes(basketJson);

        _mockCache
            .Setup(x => x.SetAsync(
                basket.UserName,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCache
            .Setup(x => x.GetAsync(basket.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(basketBytes);

        // Act
        var result = await _repository.UpdateBasket(basket);

        // Assert
        result.Should().NotBeNull();
        result.UserName.Should().Be(basket.UserName);
        result.Items.Should().HaveCount(1);
        
        _mockCache.Verify(
            x => x.SetAsync(
                basket.UserName,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteBasket_ShouldCallRemoveAsync()
    {
        // Arrange
        var userName = "testuser";
        _mockCache
            .Setup(x => x.RemoveAsync(userName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.DeleteBasket(userName);

        // Assert
        _mockCache.Verify(
            x => x.RemoveAsync(userName, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

