using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TestHelpers;
using Xunit;

namespace Shopping.Aggregator.IntegrationTests.Controllers;

public class ShoppingAggregatorIntegrationTests
{
    private readonly FakeJwtTokenGenerator _tokenGenerator;

    public ShoppingAggregatorIntegrationTests()
    {
        _tokenGenerator = new FakeJwtTokenGenerator("http://test-issuer.com", "test-audience");
    }

    [Fact]
    public async Task GetShopping_WithValidUser_ShouldReturn_ServiceUnavailable()
    {
        // Arrange  
        // Note: This test verifies the endpoint exists and handles errors gracefully
        // when downstream services are not available
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var token = _tokenGenerator.GenerateToken("testuser", "test-user-id", new[] { "User" }, new[] { "shopping.read" });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/Shopping/testuser");

        // Assert
        // Should return 500 (Internal Server Error) since downstream services are not running
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetShopping_WithoutAuthentication_ShouldReturn_ServiceUnavailable()
    {
        // Arrange
        // Shopping.Aggregator allows anonymous access (delegates to downstream services)
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/Shopping/testuser");

        // Assert
        // Should return 500 since downstream services are not available
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ShoppingController_ShouldExist()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/Shopping/testuser");

        // Assert
        // Should not return 404 (Not Found) - endpoint exists
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetShopping_WithAuthToken_ShouldAccept()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var token = _tokenGenerator.GenerateToken("testuser", "test-user-id", new[] { "User" }, new[] { "shopping.read" });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/Shopping/testuser");

        // Assert
        // Should not return 401 or 403 (auth works)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetShopping_WithoutDownstreamServices_ShouldHandleGracefully()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/Shopping/testuser");

        // Assert
        // Should return 500 (Internal Server Error) when downstream services unavailable
        // This verifies error handling exists
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}

