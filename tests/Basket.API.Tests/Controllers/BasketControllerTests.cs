using AutoMapper;
using Basket.API.Controllers;
using Basket.API.Entities;
using Basket.API.GrpcServices;
using Basket.API.Repositories;
using Discount.Grpc.Protos;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Basket.API.Tests.Controllers;

/// <summary>
/// Unit tests for BasketController
/// Tests business logic with all dependencies mocked
/// </summary>
[Trait("Category", "Unit")]
public class BasketControllerTests
{
    private readonly Mock<IBasketRepository> _mockRepository;
    private readonly Mock<IDiscountGrpcService> _mockDiscountService;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<IMapper> _mockMapper;
    private readonly BasketController _controller;

    public BasketControllerTests()
    {
        _mockRepository = new Mock<IBasketRepository>();
        _mockDiscountService = new Mock<IDiscountGrpcService>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockMapper = new Mock<IMapper>();

        _controller = new BasketController(
            _mockRepository.Object,
            _mockDiscountService.Object,
            _mockPublishEndpoint.Object,
            _mockMapper.Object
        );
    }

    #region GetBasket Tests

    [Fact]
    public async Task GetBasket_ShouldReturnOk_WhenBasketExists()
    {
        // Arrange
        var userName = "testuser";
        var expectedBasket = new ShoppingCart
        {
            UserName = userName,
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "Product1", Price = 100, Quantity = 2 }
            }
        };

        _mockRepository
            .Setup(x => x.GetBasket(userName))
            .ReturnsAsync(expectedBasket);

        // Act
        var result = await _controller.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var basket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        basket.UserName.Should().Be(userName);
        basket.Items.Should().HaveCount(1);
        
        _mockRepository.Verify(x => x.GetBasket(userName), Times.Once);
    }

    [Fact]
    public async Task GetBasket_ShouldReturnEmptyBasket_WhenBasketDoesNotExist()
    {
        // Arrange
        var userName = "nonexistent";
        _mockRepository
            .Setup(x => x.GetBasket(userName))
            .ReturnsAsync((ShoppingCart?)null);

        // Act
        var result = await _controller.GetBasket(userName);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var basket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        basket.UserName.Should().Be(userName);
        basket.Items.Should().BeEmpty();
        
        _mockRepository.Verify(x => x.GetBasket(userName), Times.Once);
    }

    #endregion

    #region UpdateBasket Tests

    [Fact]
    public async Task UpdateBasket_ShouldApplyDiscount_WhenCouponExists()
    {
        // Arrange
        var basket = new ShoppingCart
        {
            UserName = "testuser",
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "IPhone X", Price = 1000, Quantity = 1, Color = "Black" }
            }
        };

        var coupon = new CouponModel
        {
            ProductName = "IPhone X",
            Amount = 150,
            Description = "IPhone X Discount"
        };

        _mockDiscountService
            .Setup(x => x.GetDiscount("IPhone X"))
            .ReturnsAsync(coupon);

        _mockRepository
            .Setup(x => x.UpdateBasket(It.IsAny<ShoppingCart>()))
            .ReturnsAsync((ShoppingCart b) => b);

        // Act
        var result = await _controller.UpdateBasket(basket);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedBasket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        
        // Price should be reduced by discount
        updatedBasket.Items[0].Price.Should().Be(850); // 1000 - 150
        
        _mockDiscountService.Verify(x => x.GetDiscount("IPhone X"), Times.Once);
        _mockRepository.Verify(x => x.UpdateBasket(It.IsAny<ShoppingCart>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBasket_ShouldNotApplyDiscount_WhenNoCouponExists()
    {
        // Arrange
        var basket = new ShoppingCart
        {
            UserName = "testuser",
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "Samsung 10", Price = 800, Quantity = 1, Color = "White" }
            }
        };

        var noCoupon = new CouponModel
        {
            ProductName = "No Discount",
            Amount = 0,
            Description = "No Discount Desc"
        };

        _mockDiscountService
            .Setup(x => x.GetDiscount("Samsung 10"))
            .ReturnsAsync(noCoupon);

        _mockRepository
            .Setup(x => x.UpdateBasket(It.IsAny<ShoppingCart>()))
            .ReturnsAsync((ShoppingCart b) => b);

        // Act
        var result = await _controller.UpdateBasket(basket);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedBasket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        
        // Price should remain unchanged
        updatedBasket.Items[0].Price.Should().Be(800);
        
        _mockDiscountService.Verify(x => x.GetDiscount("Samsung 10"), Times.Once);
    }

    [Fact]
    public async Task UpdateBasket_ShouldApplyDiscountToAllItems()
    {
        // Arrange
        var basket = new ShoppingCart
        {
            UserName = "testuser",
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "IPhone X", Price = 1000, Quantity = 1, Color = "Black" },
                new() { ProductName = "Samsung 10", Price = 800, Quantity = 2, Color = "White" }
            }
        };

        _mockDiscountService
            .Setup(x => x.GetDiscount("IPhone X"))
            .ReturnsAsync(new CouponModel { Amount = 150 });

        _mockDiscountService
            .Setup(x => x.GetDiscount("Samsung 10"))
            .ReturnsAsync(new CouponModel { Amount = 50 });

        _mockRepository
            .Setup(x => x.UpdateBasket(It.IsAny<ShoppingCart>()))
            .ReturnsAsync((ShoppingCart b) => b);

        // Act
        var result = await _controller.UpdateBasket(basket);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedBasket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        
        updatedBasket.Items[0].Price.Should().Be(850);  // 1000 - 150
        updatedBasket.Items[1].Price.Should().Be(750);  // 800 - 50
        
        _mockDiscountService.Verify(x => x.GetDiscount(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateBasket_ShouldCalculateTotalPriceCorrectly()
    {
        // Arrange
        var basket = new ShoppingCart
        {
            UserName = "testuser",
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "Product1", Price = 100, Quantity = 2, Color = "Red" },
                new() { ProductName = "Product2", Price = 50, Quantity = 3, Color = "Blue" }
            }
        };

        _mockDiscountService
            .Setup(x => x.GetDiscount(It.IsAny<string>()))
            .ReturnsAsync(new CouponModel { Amount = 0 });

        _mockRepository
            .Setup(x => x.UpdateBasket(It.IsAny<ShoppingCart>()))
            .ReturnsAsync((ShoppingCart b) => b);

        // Act
        var result = await _controller.UpdateBasket(basket);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedBasket = okResult.Value.Should().BeOfType<ShoppingCart>().Subject;
        
        // Total = (100 * 2) + (50 * 3) = 350
        updatedBasket.TotalPrice.Should().Be(350);
    }

    #endregion

    #region DeleteBasket Tests

    [Fact]
    public async Task DeleteBasket_ShouldReturnOk_WhenBasketIsDeleted()
    {
        // Arrange
        var userName = "testuser";
        _mockRepository
            .Setup(x => x.DeleteBasket(userName))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteBasket(userName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockRepository.Verify(x => x.DeleteBasket(userName), Times.Once);
    }

    #endregion

    #region Checkout Tests

    [Fact]
    public async Task Checkout_ShouldPublishEvent_AndDeleteBasket()
    {
        // Arrange
        var basketCheckout = new BasketCheckout
        {
            UserName = "testuser",
            TotalPrice = 1000,
            FirstName = "John",
            LastName = "Doe",
            EmailAddress = "john.doe@test.com",
            AddressLine = "123 Test St",
            Country = "USA",
            State = "CA",
            ZipCode = "12345",
            CardName = "John Doe",
            CardNumber = "4111111111111111",
            Expiration = "12/25",
            CVV = "123",
            PaymentMethod = 1
        };

        var basket = new ShoppingCart
        {
            UserName = basketCheckout.UserName,
            Items = new List<ShoppingCartItem>
            {
                new() { ProductName = "Product1", Price = 1000, Quantity = 1, ProductId = "1", Color = "Black" }
            }
        };

        var basketCheckoutEvent = new Eventbus.Messages.Events.BasketCheckoutEvent
        {
            UserName = basketCheckout.UserName,
            TotalPrice = basketCheckout.TotalPrice,
            FirstName = basketCheckout.FirstName,
            LastName = basketCheckout.LastName,
            EmailAddress = basketCheckout.EmailAddress,
            AddressLine = basketCheckout.AddressLine,
            Country = basketCheckout.Country,
            State = basketCheckout.State,
            ZipCode = basketCheckout.ZipCode,
            CardName = basketCheckout.CardName,
            CardNumber = basketCheckout.CardNumber,
            Expiration = basketCheckout.Expiration,
            CVV = basketCheckout.CVV,
            PaymentMethod = basketCheckout.PaymentMethod
        };

        _mockRepository
            .Setup(x => x.GetBasket(basketCheckout.UserName))
            .ReturnsAsync(basket);

        _mockMapper
            .Setup(x => x.Map<Eventbus.Messages.Events.BasketCheckoutEvent>(It.IsAny<BasketCheckout>()))
            .Returns(basketCheckoutEvent);

        _mockPublishEndpoint
            .Setup(x => x.Publish<Eventbus.Messages.Events.BasketCheckoutEvent>(It.IsAny<Eventbus.Messages.Events.BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRepository
            .Setup(x => x.DeleteBasket(basketCheckout.UserName))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Checkout(basketCheckout);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        
        _mockRepository.Verify(x => x.GetBasket(basketCheckout.UserName), Times.Once);
        _mockMapper.Verify(x => x.Map<Eventbus.Messages.Events.BasketCheckoutEvent>(It.IsAny<BasketCheckout>()), Times.Once);
        _mockPublishEndpoint.Verify(
            x => x.Publish<Eventbus.Messages.Events.BasketCheckoutEvent>(It.IsAny<Eventbus.Messages.Events.BasketCheckoutEvent>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockRepository.Verify(x => x.DeleteBasket(basketCheckout.UserName), Times.Once);
    }

    [Fact]
    public async Task Checkout_ShouldReturnBadRequest_WhenBasketIsEmpty()
    {
        // Arrange
        var basketCheckout = new BasketCheckout
        {
            UserName = "emptyuser",
            TotalPrice = 0
        };

        _mockRepository
            .Setup(x => x.GetBasket(basketCheckout.UserName))
            .ReturnsAsync((ShoppingCart?)null);

        // Act
        var result = await _controller.Checkout(basketCheckout);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
        
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), 
            Times.Never);
        _mockRepository.Verify(x => x.DeleteBasket(It.IsAny<string>()), Times.Never);
    }

    #endregion
}

