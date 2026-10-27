using System.Net;

using Microsoft.AspNetCore.Mvc;

using NoteService.IntegrationTests.Infrastructure;

namespace NoteService.IntegrationTests;

public class AuthenticationTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Theory]
    [InlineData("/api/notes")]
    [InlineData("/api/teams")]
    public async Task Get_WithoutUserHeader_ReturnsUnauthorizedProblem(string url)
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadAsAsync<ProblemDetails>();
        Assert.Equal(401, problem.Status);
        Assert.Contains("X-User-Id", problem.Detail);
    }

    [Fact]
    public async Task Get_WithBlankUserHeader_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClientFor("   ");

        // Act
        var response = await client.GetAsync("/api/notes");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
