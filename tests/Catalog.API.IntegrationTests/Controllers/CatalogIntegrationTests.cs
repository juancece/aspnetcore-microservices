using Catalog.API.Entities;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TestHelpers;
using Xunit;

namespace Catalog.API.IntegrationTests.Controllers
{
    [Trait("Category", "Integration")]
    public class CatalogIntegrationTests : IClassFixture<MongoDbFixture>, IAsyncLifetime
    {
        private readonly MongoDbFixture _dbFixture;
        private TestWebApplicationFactory<Program> _factory;
        private HttpClient _client;

        public CatalogIntegrationTests(MongoDbFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task InitializeAsync()
        {
            // Reset database before each test
            await _dbFixture.ResetDatabaseAsync();
            
            // Create a fresh factory and client after database reset
            // This ensures CatalogContext is recreated and seed data is applied
            _factory = new TestWebApplicationFactory<Program>(_dbFixture);
            _client = _factory.CreateClient();
        }

        public Task DisposeAsync()
        {
            // Cleanup after each test
            _client?.Dispose();
            _factory?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetProducts_ShouldReturnOk_WithSeedData()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/Catalog");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var products = await response.Content.ReadFromJsonAsync<List<Product>>();
            products.Should().NotBeNull();
            products.Should().HaveCount(6); // Catalog.API seeds 6 products on startup
            products.Should().Contain(p => p.Name == "IPhone X");
        }

        [Fact]
        public async Task CreateProduct_ShouldReturn201Created_WhenProductIsValid()
        {
            // Arrange
            var newProduct = new Product
            {
                Name = "Integration Test Product",
                Category = "Test Category",
                Summary = "Test Summary",
                Description = "Test Description",
                ImageFile = "test.png",
                Price = 99.99M
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/Catalog", newProduct);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdProduct = await response.Content.ReadFromJsonAsync<Product>();
            createdProduct.Should().NotBeNull();
            createdProduct!.Name.Should().Be(newProduct.Name);
            createdProduct.Id.Should().NotBeNullOrEmpty(); // MongoDB generated ID
        }

        [Fact]
        public async Task GetProductById_ShouldReturn200_WhenProductExists()
        {
            // Arrange - First create a product
            var newProduct = new Product
            {
                Name = "Test Product for Get",
                Category = "Smart Phone",
                Summary = "Test",
                Description = "Test Description",
                ImageFile = "test.png",
                Price = 500.00M
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/Catalog", newProduct);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();

            // Act - Get the product by ID
            var response = await _client.GetAsync($"/api/v1/Catalog/{createdProduct!.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var retrievedProduct = await response.Content.ReadFromJsonAsync<Product>();
            retrievedProduct.Should().NotBeNull();
            retrievedProduct!.Id.Should().Be(createdProduct.Id);
            retrievedProduct.Name.Should().Be(newProduct.Name);
        }

        [Fact]
        public async Task GetProductById_ShouldReturn404_WhenProductDoesNotExist()
        {
            // Arrange - Use a valid MongoDB ObjectId format that doesn't exist
            var nonExistentId = "507f1f77bcf86cd799439011";

            // Act
            var response = await _client.GetAsync($"/api/v1/Catalog/{nonExistentId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetProductByCategory_ShouldReturnFilteredProducts()
        {
            // Arrange - Create products in different categories
            var smartPhoneProduct = new Product
            {
                Name = "iPhone 14",
                Category = "Smart Phone",
                Summary = "Apple smartphone",
                Description = "Latest iPhone",
                ImageFile = "iphone14.png",
                Price = 999.99M
            };

            var laptopProduct = new Product
            {
                Name = "MacBook Pro",
                Category = "Laptop",
                Summary = "Apple laptop",
                Description = "Professional laptop",
                ImageFile = "macbook.png",
                Price = 2499.99M
            };

            await _client.PostAsJsonAsync("/api/v1/Catalog", smartPhoneProduct);
            await _client.PostAsJsonAsync("/api/v1/Catalog", laptopProduct);

            // Act
            var response = await _client.GetAsync("/api/v1/Catalog/GetProductByCategory/Smart Phone");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var products = await response.Content.ReadFromJsonAsync<List<Product>>();
            products.Should().NotBeNull();
            products.Should().HaveCountGreaterOrEqualTo(1);
            products.Should().OnlyContain(p => p.Category == "Smart Phone");
        }

        [Fact]
        public async Task UpdateProduct_ShouldReturn200_WhenProductIsUpdated()
        {
            // Arrange - Create a product first
            var originalProduct = new Product
            {
                Name = "Original Product",
                Category = "Electronics",
                Summary = "Original",
                Description = "Original Description",
                ImageFile = "original.png",
                Price = 100.00M
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/Catalog", originalProduct);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();

            // Modify the product
            createdProduct!.Name = "Updated Product";
            createdProduct.Price = 150.00M;

            // Act
            var updateResponse = await _client.PutAsJsonAsync("/api/v1/Catalog", createdProduct);

            // Assert
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify the update
            var getResponse = await _client.GetAsync($"/api/v1/Catalog/{createdProduct.Id}");
            var updatedProduct = await getResponse.Content.ReadFromJsonAsync<Product>();
            updatedProduct!.Name.Should().Be("Updated Product");
            updatedProduct.Price.Should().Be(150.00M);
        }

        [Fact]
        public async Task DeleteProduct_ShouldReturn200_WhenProductIsDeleted()
        {
            // Arrange - Create a product first
            var productToDelete = new Product
            {
                Name = "Product to Delete",
                Category = "Test",
                Summary = "Will be deleted",
                Description = "Test deletion",
                ImageFile = "delete.png",
                Price = 50.00M
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/Catalog", productToDelete);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();

            // Act
            var deleteResponse = await _client.DeleteAsync($"/api/v1/Catalog/{createdProduct!.Id}");

            // Assert
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify the product is deleted
            var getResponse = await _client.GetAsync($"/api/v1/Catalog/{createdProduct.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Integration test demonstrating end-to-end CRUD workflow with real MongoDB
        /// This is the power of Testcontainers - real database, no mocks!
        /// </summary>
        [Fact]
        public async Task CompleteProductLifecycle_ShouldWorkEndToEnd()
        {
            // 1. Create
            var product = new Product
            {
                Name = "Lifecycle Test Product",
                Category = "Integration",
                Summary = "Full lifecycle test",
                Description = "Tests create, read, update, delete",
                ImageFile = "lifecycle.png",
                Price = 250.00M
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/Catalog", product);
            
            // Debug: Output response details if not successful
            if (!createResponse.IsSuccessStatusCode)
            {
                var errorContent = await createResponse.Content.ReadAsStringAsync();
                throw new Exception($"Create failed with {createResponse.StatusCode}. Response: {errorContent}");
            }
            
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();
            createdProduct!.Id.Should().NotBeNullOrEmpty();

            // 2. Read
            var readResponse = await _client.GetAsync($"/api/v1/Catalog/{createdProduct.Id}");
            readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var readProduct = await readResponse.Content.ReadFromJsonAsync<Product>();
            readProduct!.Name.Should().Be(product.Name);

            // 3. Update
            readProduct.Price = 300.00M;
            var updateResponse = await _client.PutAsJsonAsync("/api/v1/Catalog", readProduct);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify Update
            var verifyResponse = await _client.GetAsync($"/api/v1/Catalog/{createdProduct.Id}");
            var verifiedProduct = await verifyResponse.Content.ReadFromJsonAsync<Product>();
            verifiedProduct!.Price.Should().Be(300.00M);

            // 5. Delete
            var deleteResponse = await _client.DeleteAsync($"/api/v1/Catalog/{createdProduct.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Verify Deletion
            var finalResponse = await _client.GetAsync($"/api/v1/Catalog/{createdProduct.Id}");
            finalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}

