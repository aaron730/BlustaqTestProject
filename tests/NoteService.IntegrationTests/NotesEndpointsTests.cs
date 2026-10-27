using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using NoteService.Api.Contracts;
using NoteService.IntegrationTests.Infrastructure;

using static NoteService.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace NoteService.IntegrationTests;

public class NotesEndpointsTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Fact]
    public async Task CreateNote_ValidRequest_ReturnsCreatedWithLocationAndETag()
    {
        // Arrange
        var userId = NewUserId();
        var client = factory.CreateClientFor(userId);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/notes", new CreateNoteRequest("  Meeting notes ", "# Agenda", null, ["Work", "work", " Planning "]));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await response.ReadAsAsync<NoteResponse>();
        Assert.Equal("Meeting notes", note.Title);
        Assert.Equal("# Agenda", note.Content);
        Assert.Equal(userId, note.AuthorId);
        Assert.Null(note.TeamId);
        Assert.Equal(["planning", "work"], note.Tags);
        Assert.Equal(1, note.Version);
        Assert.Equal($"/api/notes/{note.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task CreateNote_Twice_AssignsSequentialIds()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var first = await client.CreateNoteAsync("First");
        var second = await client.CreateNoteAsync("Second");

        // Assert
        Assert.True(first.Id > 0);
        Assert.Equal(first.Id + 1, second.Id);
    }

    [Fact]
    public async Task CreateNote_MissingTitle_ReturnsValidationProblem()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var response = await client.PostAsJsonAsync("/api/notes", new { content = "No title" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsAsync<ValidationProblemDetails>();
        Assert.Contains(problem.Errors.Keys, key => key.Equals("Title", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateNote_TooManyTags_ReturnsValidationProblem()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var tags = Enumerable.Range(0, TagRules.MaxTagsPerNote + 1).Select(i => $"tag{i}").ToList();

        // Act
        var response = await client.PostAsJsonAsync("/api/notes", new CreateNoteRequest("Title", null, null, tags));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsAsync<ValidationProblemDetails>();
        Assert.Contains(problem.Errors.Keys, key => key.Equals("Tags", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetNote_ExistingNote_ReturnsNoteWithETag()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync("Read me");

        // Act
        var response = await client.GetAsync($"/api/notes/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        var note = await response.ReadAsAsync<NoteResponse>();
        Assert.Equal(created.Id, note.Id);
        Assert.Equal("Read me", note.Title);
    }

    [Fact]
    public async Task GetNote_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var response = await client.GetAsync("/api/notes/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_MatchingETag_ReturnsUpdatedNoteWithNewETag()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync("Draft", tags: ["old"]);

        // Act
        var response = await client.PutNoteAsync(
            created.Id, new UpdateNoteRequest("Final", "Done", ["new"]), ETagFor(created));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        var updated = await response.ReadAsAsync<NoteResponse>();
        Assert.Equal("Final", updated.Title);
        Assert.Equal("Done", updated.Content);
        Assert.Equal(["new"], updated.Tags);
        Assert.Equal(2, updated.Version);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateNote_StaleETag_ReturnsPreconditionFailedAndKeepsNote()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync("Original");
        var staleETag = ETagFor(created);
        (await client.PutNoteAsync(created.Id, new UpdateNoteRequest("First edit", null, null), staleETag))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PutNoteAsync(created.Id, new UpdateNoteRequest("Second edit", null, null), staleETag);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        var current = await (await client.GetAsync($"/api/notes/{created.Id}")).ReadAsAsync<NoteResponse>();
        Assert.Equal("First edit", current.Title);
    }

    [Fact]
    public async Task UpdateNote_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync();

        // Act
        var response = await client.PutNoteAsync(created.Id, new UpdateNoteRequest("Edit", null, null), ifMatch: null);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_WildcardIfMatch_UpdatesNote()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync();

        // Act
        var response = await client.PutNoteAsync(created.Id, new UpdateNoteRequest("Edit", null, null), "*");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_EmptyTitle_ReturnsValidationProblem()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync();

        // Act
        var response = await client.PutNoteAsync(created.Id, new UpdateNoteRequest("", null, null), ETagFor(created));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DeleteNote_OwnNote_ReturnsNoContentAndRemovesNote()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var created = await client.CreateNoteAsync();

        // Act
        var response = await client.DeleteAsync($"/api/notes/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/notes/{created.Id}")).StatusCode);
    }

    private static string ETagFor(NoteResponse note) => $"\"{note.Version}\"";
}
