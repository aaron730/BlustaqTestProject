using System.Net;
using System.Net.Http.Json;

using NoteService.Api.Contracts;
using NoteService.IntegrationTests.Infrastructure;

using static NoteService.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace NoteService.IntegrationTests;

public class NoteVisibilityTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Fact]
    public async Task GetNote_PersonalNoteOfAnotherUser_ReturnsNotFound()
    {
        // Arrange
        var note = await factory.CreateClientFor(NewUserId()).CreateNoteAsync("Private");
        var otherUser = factory.CreateClientFor(NewUserId());

        // Act
        var response = await otherUser.GetAsync($"/api/notes/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TeamNote_TeamMember_CanReadAndUpdate()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var memberId = NewUserId();
        var member = factory.CreateClientFor(memberId);
        var team = await owner.CreateTeamAsync("Team A");
        await owner.AddMemberAsync(team.Id, memberId);
        var note = await owner.CreateNoteAsync("Shared", teamId: team.Id);

        // Act
        var getResponse = await member.GetAsync($"/api/notes/{note.Id}");
        var putResponse = await member.PutNoteAsync(note.Id, new UpdateNoteRequest("Edited by member", null, null), "\"1\"");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
    }

    [Fact]
    public async Task TeamNote_UserFromAnotherTeam_CannotReadUpdateOrDelete()
    {
        // Arrange
        var teamAUser = factory.CreateClientFor(NewUserId());
        var teamBUser = factory.CreateClientFor(NewUserId());
        var teamA = await teamAUser.CreateTeamAsync("Team A");
        await teamBUser.CreateTeamAsync("Team B");
        var note = await teamAUser.CreateNoteAsync("Team A secret", teamId: teamA.Id);

        // Act
        var getResponse = await teamBUser.GetAsync($"/api/notes/{note.Id}");
        var putResponse = await teamBUser.PutNoteAsync(note.Id, new UpdateNoteRequest("Hijacked", null, null), "*");
        var deleteResponse = await teamBUser.DeleteAsync($"/api/notes/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        var unchanged = await (await teamAUser.GetAsync($"/api/notes/{note.Id}")).ReadAsAsync<NoteResponse>();
        Assert.Equal("Team A secret", unchanged.Title);
    }

    [Fact]
    public async Task ListNotes_FilteredByTeamCallerIsNotIn_ReturnsForbidden()
    {
        // Arrange
        var teamA = await factory.CreateClientFor(NewUserId()).CreateTeamAsync("Team A");
        var outsider = factory.CreateClientFor(NewUserId());

        // Act
        var response = await outsider.GetAsync($"/api/notes?teamId={teamA.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_InTeamCallerIsNotIn_ReturnsForbidden()
    {
        // Arrange
        var teamA = await factory.CreateClientFor(NewUserId()).CreateTeamAsync("Team A");
        var outsider = factory.CreateClientFor(NewUserId());

        // Act
        var response = await outsider.PostAsJsonAsync("/api/notes", new CreateNoteRequest("Intrusion", null, teamA.Id, null));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_UnfilteredAcrossTeams_ReturnsOnlyVisibleNotes()
    {
        // Arrange
        var aliceId = NewUserId();
        var alice = factory.CreateClientFor(aliceId);
        var bob = factory.CreateClientFor(NewUserId());
        var aliceTeam = await alice.CreateTeamAsync("Alice team");
        var bobTeam = await bob.CreateTeamAsync("Bob team");
        var personal = await alice.CreateNoteAsync("Alice personal");
        var shared = await alice.CreateNoteAsync("Alice team note", teamId: aliceTeam.Id);
        await bob.CreateNoteAsync("Bob personal");
        await bob.CreateNoteAsync("Bob team note", teamId: bobTeam.Id);

        // Act
        var page = await (await alice.GetAsync("/api/notes")).ReadAsAsync<PagedResponse<NoteResponse>>();

        // Assert
        Assert.Equal(2, page.TotalCount);
        Assert.Equal([shared.Id, personal.Id], page.Items.Select(n => n.Id));
    }

    [Fact]
    public async Task DeleteNote_TeamNoteByNonAuthorMember_ReturnsForbidden()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var authorId = NewUserId();
        var otherMemberId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, authorId);
        await owner.AddMemberAsync(team.Id, otherMemberId);
        var note = await factory.CreateClientFor(authorId).CreateNoteAsync(teamId: team.Id);

        // Act
        var response = await factory.CreateClientFor(otherMemberId).DeleteAsync($"/api/notes/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_TeamNoteByTeamOwner_ReturnsNoContent()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var authorId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, authorId);
        var note = await factory.CreateClientFor(authorId).CreateNoteAsync(teamId: team.Id);

        // Act
        var response = await owner.DeleteAsync($"/api/notes/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TeamNote_AfterMemberRemoved_IsNoLongerVisibleToThem()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var memberId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, memberId);
        var note = await owner.CreateNoteAsync(teamId: team.Id);
        (await owner.DeleteAsync($"/api/teams/{team.Id}/members/{memberId}")).EnsureSuccessStatusCode();

        // Act
        var response = await factory.CreateClientFor(memberId).GetAsync($"/api/notes/{note.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
