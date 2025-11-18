using Catalog.API.Controllers;
using Catalog.API.Entities;
using Catalog.API.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Catalog.API.Tests.Controllers
{
    [Trait("Category", "Unit")]
    public class CatalogControllerTests
    {
        private readonly Mock<IProductRepository> _mockRepository;
        private readonly Mock<ILogger<CatalogController>> _mockLogger;
        private readonly CatalogController _controller;

        public CatalogControllerTests()
        {
            _mockRepository = new Mock<IProductRepository>();
            _mockLogger = new Mock<ILogger<CatalogController>>();
            _controller = new CatalogController(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetProducts_ShouldReturnOkWithProducts_WhenProductsExist()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = "507f1f77bcf86cd799439011", Name = "IPhone X", Category = "Smart Phone", Price = 950.00M },
                new Product { Id = "507f1f77bcf86cd799439012", Name = "Samsung 10", Category = "Smart Phone", Price = 840.00M }
            };
            _mockRepository.Setup(r => r.GetProducts()).ReturnsAsync(products);

            // Act
            var result = await _controller.GetProducts();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProducts = okResult.Value.Should().BeAssignableTo<IEnumerable<Product>>().Subject;
            returnedProducts.Should().HaveCount(2);
            returnedProducts.Should().Contain(p => p.Name == "IPhone X");
        }

        [Fact]
        public async Task GetProducts_ShouldReturnOkWithEmptyList_WhenNoProductsExist()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetProducts()).ReturnsAsync(new List<Product>());

            // Act
            var result = await _controller.GetProducts();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProducts = okResult.Value.Should().BeAssignableTo<IEnumerable<Product>>().Subject;
            returnedProducts.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProductById_ShouldReturnOkWithProduct_WhenProductExists()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439011";
            var product = new Product { Id = productId, Name = "IPhone X", Category = "Smart Phone", Price = 950.00M };
            _mockRepository.Setup(r => r.GetProduct(productId)).ReturnsAsync(product);

            // Act
            var result = await _controller.GetProductById(productId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProduct = okResult.Value.Should().BeAssignableTo<Product>().Subject;
            returnedProduct.Id.Should().Be(productId);
            returnedProduct.Name.Should().Be("IPhone X");
        }

        [Fact]
        public async Task GetProductById_ShouldReturnNotFound_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439999";
            _mockRepository.Setup(r => r.GetProduct(productId)).ReturnsAsync((Product?)null);

            // Act
            var result = await _controller.GetProductById(productId);

            // Assert
            result.Result.Should().BeOfType<NotFoundResult>();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Product with id: {productId}, not found.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetProductByCategory_ShouldReturnOkWithFilteredProducts_WhenCategoryHasProducts()
        {
            // Arrange
            var category = "Smart Phone";
            var products = new List<Product>
            {
                new Product { Id = "507f1f77bcf86cd799439011", Name = "IPhone X", Category = category, Price = 950.00M },
                new Product { Id = "507f1f77bcf86cd799439012", Name = "Samsung 10", Category = category, Price = 840.00M }
            };
            _mockRepository.Setup(r => r.GetProductByCategory(category)).ReturnsAsync(products);

            // Act
            var result = await _controller.GetProductByCategory(category);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProducts = okResult.Value.Should().BeAssignableTo<IEnumerable<Product>>().Subject;
            returnedProducts.Should().HaveCount(2);
            returnedProducts.Should().OnlyContain(p => p.Category == category);
        }

        [Fact]
        public async Task GetProductByCategory_ShouldReturnOkWithEmptyList_WhenCategoryHasNoProducts()
        {
            // Arrange
            var category = "NonExistentCategory";
            _mockRepository.Setup(r => r.GetProductByCategory(category)).ReturnsAsync(new List<Product>());

            // Act
            var result = await _controller.GetProductByCategory(category);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProducts = okResult.Value.Should().BeAssignableTo<IEnumerable<Product>>().Subject;
            returnedProducts.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateProduct_ShouldReturnCreatedAtRoute_WhenProductIsValid()
        {
            // Arrange
            var product = new Product
            {
                Id = "507f1f77bcf86cd799439013",
                Name = "Test Product",
                Category = "Test Category",
                Summary = "Test Summary",
                Description = "Test Description",
                ImageFile = "test.png",
                Price = 100.00M
            };
            _mockRepository.Setup(r => r.CreateProduct(It.IsAny<Product>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CreateProduct(product);

            // Assert
            var createdResult = result.Result.Should().BeOfType<CreatedAtRouteResult>().Subject;
            createdResult.RouteName.Should().Be("GetProduct");
            createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(product.Id);
            createdResult.Value.Should().BeEquivalentTo(product);
            _mockRepository.Verify(r => r.CreateProduct(product), Times.Once);
        }

        [Theory]
        [InlineData("Smart Phone")]
        [InlineData("White Appliances")]
        [InlineData("Home Kitchen")]
        public async Task GetProductByCategory_ShouldHandleMultipleCategories(string category)
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = "1", Name = "Product 1", Category = category, Price = 100M }
            };
            _mockRepository.Setup(r => r.GetProductByCategory(category)).ReturnsAsync(products);

            // Act
            var result = await _controller.GetProductByCategory(category);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProducts = okResult.Value.Should().BeAssignableTo<IEnumerable<Product>>().Subject;
            returnedProducts.Should().HaveCount(1);
        }

        /// <summary>
        /// TDD Example: This test was written first (Red phase)
        /// Then the controller method was implemented (Green phase)
        /// Then refactored for clean code (Refactor phase)
        /// </summary>
        [Fact]
        public async Task UpdateProduct_ShouldReturnOk_WhenProductIsUpdatedSuccessfully()
        {
            // Arrange
            var product = new Product
            {
                Id = "507f1f77bcf86cd799439011",
                Name = "Updated Product",
                Category = "Updated Category",
                Price = 200.00M
            };
            _mockRepository.Setup(r => r.UpdateProduct(product)).ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateProduct(product);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(true);
            _mockRepository.Verify(r => r.UpdateProduct(product), Times.Once);
        }

        /// <summary>
        /// TDD Example: Testing edge case - update failure
        /// </summary>
        [Fact]
        public async Task UpdateProduct_ShouldReturnOkWithFalse_WhenProductUpdateFails()
        {
            // Arrange
            var product = new Product { Id = "507f1f77bcf86cd799439999", Name = "NonExistent" };
            _mockRepository.Setup(r => r.UpdateProduct(product)).ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateProduct(product);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(false);
        }

        [Fact]
        public async Task DeleteProductById_ShouldReturnOk_WhenProductIsDeletedSuccessfully()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439011";
            _mockRepository.Setup(r => r.DeleteProduct(productId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteProductById(productId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(true);
            _mockRepository.Verify(r => r.DeleteProduct(productId), Times.Once);
        }

        [Fact]
        public async Task DeleteProductById_ShouldReturnOkWithFalse_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439999";
            _mockRepository.Setup(r => r.DeleteProduct(productId)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteProductById(productId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(false);
        }
    }
}

