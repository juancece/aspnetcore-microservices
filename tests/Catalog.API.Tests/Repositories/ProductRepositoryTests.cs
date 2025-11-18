using Catalog.API.Data;
using Catalog.API.Entities;
using Catalog.API.Repositories;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Catalog.API.Tests.Repositories
{
    [Trait("Category", "Unit")]
    public class ProductRepositoryTests
    {
        private readonly Mock<ICatalogContext> _mockContext;
        private readonly Mock<IMongoCollection<Product>> _mockCollection;
        private readonly Mock<IAsyncCursor<Product>> _mockCursor;
        private readonly ProductRepository _repository;

        public ProductRepositoryTests()
        {
            _mockContext = new Mock<ICatalogContext>();
            _mockCollection = new Mock<IMongoCollection<Product>>();
            _mockCursor = new Mock<IAsyncCursor<Product>>();
            
            _mockContext.Setup(c => c.Products).Returns(_mockCollection.Object);
            _repository = new ProductRepository(_mockContext.Object);
        }

        /// <summary>
        /// TDD Example: Red-Green-Refactor
        /// 1. Write this test first (it fails - RED)
        /// 2. Implement GetProducts method (it passes - GREEN)
        /// 3. Refactor for performance/clarity (REFACTOR)
        /// </summary>
        [Fact]
        public async Task GetProducts_ShouldReturnAllProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = "1", Name = "Product 1", Price = 100M },
                new Product { Id = "2", Name = "Product 2", Price = 200M }
            };

            _mockCursor.Setup(c => c.Current).Returns(products);
            _mockCursor
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<FindOptions<Product, Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockCursor.Object);

            // Act
            var result = await _repository.GetProducts();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(p => p.Name == "Product 1");
            result.Should().Contain(p => p.Name == "Product 2");
        }

        [Fact]
        public async Task GetProducts_ShouldReturnEmptyList_WhenNoProductsExist()
        {
            // Arrange
            var emptyProducts = new List<Product>();

            _mockCursor.Setup(c => c.Current).Returns(emptyProducts);
            _mockCursor
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<FindOptions<Product, Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockCursor.Object);

            // Act
            var result = await _repository.GetProducts();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439011";
            var product = new Product { Id = productId, Name = "Test Product", Price = 150M };

            _mockCursor.Setup(c => c.Current).Returns(new List<Product> { product });
            _mockCursor
                .SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            _mockCursor
                .Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<FindOptions<Product, Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockCursor.Object);

            // Act
            var result = await _repository.GetProduct(productId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(productId);
            result.Name.Should().Be("Test Product");
        }

        [Fact]
        public async Task CreateProduct_ShouldCallInsertOneAsync()
        {
            // Arrange
            var product = new Product
            {
                Id = "507f1f77bcf86cd799439011",
                Name = "New Product",
                Category = "Electronics",
                Price = 299.99M
            };

            _mockCollection
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<Product>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _repository.CreateProduct(product);

            // Assert
            _mockCollection.Verify(
                c => c.InsertOneAsync(
                    product,
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_ShouldReturnTrue_WhenProductIsUpdatedSuccessfully()
        {
            // Arrange
            var product = new Product { Id = "507f1f77bcf86cd799439011", Name = "Updated Product" };
            var replaceOneResult = new Mock<ReplaceOneResult>();
            replaceOneResult.Setup(r => r.IsAcknowledged).Returns(true);
            replaceOneResult.Setup(r => r.ModifiedCount).Returns(1);

            _mockCollection
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<Product>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(replaceOneResult.Object);

            // Act
            var result = await _repository.UpdateProduct(product);

            // Assert
            result.Should().BeTrue();
            _mockCollection.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    product,
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_ShouldReturnFalse_WhenProductUpdateFails()
        {
            // Arrange
            var product = new Product { Id = "507f1f77bcf86cd799439999", Name = "NonExistent" };
            var replaceOneResult = new Mock<ReplaceOneResult>();
            replaceOneResult.Setup(r => r.IsAcknowledged).Returns(true);
            replaceOneResult.Setup(r => r.ModifiedCount).Returns(0); // No documents modified

            _mockCollection
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<Product>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(replaceOneResult.Object);

            // Act
            var result = await _repository.UpdateProduct(product);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteProduct_ShouldReturnTrue_WhenProductIsDeletedSuccessfully()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439011";
            var deleteResult = new Mock<DeleteResult>();
            deleteResult.Setup(r => r.IsAcknowledged).Returns(true);
            deleteResult.Setup(r => r.DeletedCount).Returns(1);

            _mockCollection
                .Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResult.Object);

            // Act
            var result = await _repository.DeleteProduct(productId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteProduct_ShouldReturnFalse_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = "507f1f77bcf86cd799439999";
            var deleteResult = new Mock<DeleteResult>();
            deleteResult.Setup(r => r.IsAcknowledged).Returns(true);
            deleteResult.Setup(r => r.DeletedCount).Returns(0); // No documents deleted

            _mockCollection
                .Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResult.Object);

            // Act
            var result = await _repository.DeleteProduct(productId);

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// TDD Example: Testing edge case discovered during refactoring
        /// This test ensures null safety and proper error handling
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task GetProduct_ShouldHandleInvalidIds_Gracefully(string invalidId)
        {
            // Arrange
            _mockCursor.Setup(c => c.Current).Returns(new List<Product>());
            _mockCursor
                .Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<Product>>(),
                    It.IsAny<FindOptions<Product, Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockCursor.Object);

            // Act
            var result = await _repository.GetProduct(invalidId);

            // Assert
            result.Should().BeNull();
        }
    }
}

