using System.Net;

using NoteService.IntegrationTests.Infrastructure;

namespace NoteService.IntegrationTests;

public class HealthCheckTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Fact]
    public async Task GetHealth_ServiceRunning_ReturnsOk()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
