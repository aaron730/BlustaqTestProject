using System.Net;

using Microsoft.AspNetCore.Mvc;

using NoteService.Api.Contracts;
using NoteService.IntegrationTests.Infrastructure;

using static NoteService.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace NoteService.IntegrationTests;

public class NoteListingTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Fact]
    public async Task ListNotes_WithPageAndPageSize_ReturnsRequestedPageAndTotals()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        for (var i = 1; i <= 5; i++)
        {
            await client.CreateNoteAsync($"Note {i}");
        }

        // Act
        var page = await (await client.GetAsync("/api/notes?page=2&pageSize=2")).ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(["Note 3", "Note 2"], page.Items.Select(n => n.Title));
    }

    [Fact]
    public async Task ListNotes_WithoutPaging_UsesDefaults()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var page = await (await client.GetAsync("/api/notes")).ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task ListNotes_InvalidPaging_ReturnsValidationProblem(string queryString)
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var response = await client.GetAsync($"/api/notes?{queryString}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task ListNotes_FilteredByTag_IsCaseInsensitive()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var tagged = await client.CreateNoteAsync("Tagged", tags: ["Backend"]);
        await client.CreateNoteAsync("Untagged");

        // Act
        var page = await (await client.GetAsync("/api/notes?tag=BACKEND")).ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal([tagged.Id], page.Items.Select(n => n.Id));
    }

    [Fact]
    public async Task ListNotes_WithSearch_MatchesTitleOrContentCaseInsensitively()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());
        var titleMatch = await client.CreateNoteAsync("Quarterly ROADMAP", "Plans");
        var contentMatch = await client.CreateNoteAsync("Ideas", "Add these to the roadmap");
        await client.CreateNoteAsync("Groceries", "Milk");

        // Act
        var page = await (await client.GetAsync("/api/notes?search=roadmap")).ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal([contentMatch.Id, titleMatch.Id], page.Items.Select(n => n.Id));
    }

    [Fact]
    public async Task ListNotes_FilteredByTeamAndAuthor_ReturnsOnlyMatchingNotes()
    {
        // Arrange
        var ownerId = NewUserId();
        var memberId = NewUserId();
        var owner = factory.CreateClientFor(ownerId);
        var member = factory.CreateClientFor(memberId);
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, memberId);
        await owner.CreateNoteAsync("Owner team note", teamId: team.Id);
        var memberTeamNote = await member.CreateNoteAsync("Member team note", teamId: team.Id);
        await member.CreateNoteAsync("Member personal note");

        // Act
        var teamPage = await (await owner.GetAsync($"/api/notes?teamId={team.Id}"))
            .ReadAsAsync<PagedResponse<NoteResponse>>();
        var authorPage = await (await owner.GetAsync($"/api/notes?teamId={team.Id}&authorId={memberId}"))
            .ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal(2, teamPage.TotalCount);
        Assert.All(teamPage.Items, n => Assert.Equal(team.Id, n.TeamId));
        Assert.Equal([memberTeamNote.Id], authorPage.Items.Select(n => n.Id));
    }
}
