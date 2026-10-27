using System.Net;
using System.Net.Http.Json;

using NoteService.Api.Contracts;
using NoteService.Api.Models;
using NoteService.IntegrationTests.Infrastructure;

using static NoteService.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace NoteService.IntegrationTests;

public class TeamsEndpointsTests(NoteServiceFactory factory) : IClassFixture<NoteServiceFactory>
{
    [Fact]
    public async Task CreateTeam_ValidRequest_ReturnsCreatedAndCallerIsOwner()
    {
        // Arrange
        var userId = NewUserId();
        var client = factory.CreateClientFor(userId);

        // Act
        var response = await client.PostAsJsonAsync("/api/teams", new CreateTeamRequest("Platform"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var team = await response.ReadAsAsync<TeamResponse>();
        Assert.Equal("Platform", team.Name);
        Assert.Equal(TeamRole.Owner, team.Role);
        Assert.Equal($"/api/teams/{team.Id}", response.Headers.Location?.AbsolutePath);

        var details = await (await client.GetAsync($"/api/teams/{team.Id}")).ReadAsAsync<TeamDetailsResponse>();
        var member = Assert.Single(details.Members);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(TeamRole.Owner, member.Role);
    }

    [Fact]
    public async Task CreateTeam_BlankName_ReturnsValidationProblem()
    {
        // Arrange
        var client = factory.CreateClientFor(NewUserId());

        // Act
        var response = await client.PostAsJsonAsync("/api/teams", new CreateTeamRequest(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListTeams_ReturnsOnlyTeamsCallerBelongsTo()
    {
        // Arrange
        var memberId = NewUserId();
        var owner = factory.CreateClientFor(NewUserId());
        var joined = await owner.CreateTeamAsync("Joined");
        await owner.CreateTeamAsync("Not joined");
        await owner.AddMemberAsync(joined.Id, memberId);

        // Act
        var teams = await (await factory.CreateClientFor(memberId).GetAsync("/api/teams"))
            .ReadAsAsync<List<TeamResponse>>();

        // Assert
        var team = Assert.Single(teams);
        Assert.Equal(joined.Id, team.Id);
        Assert.Equal(TeamRole.Member, team.Role);
    }

    [Fact]
    public async Task GetTeam_CallerNotMember_ReturnsNotFound()
    {
        // Arrange
        var team = await factory.CreateClientFor(NewUserId()).CreateTeamAsync();

        // Act
        var response = await factory.CreateClientFor(NewUserId()).GetAsync($"/api/teams/{team.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_ByOwner_ReturnsCreated()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var team = await owner.CreateTeamAsync();
        var newMemberId = NewUserId();

        // Act
        var response = await owner.PostAsJsonAsync($"/api/teams/{team.Id}/members", new AddTeamMemberRequest(newMemberId));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var member = await response.ReadAsAsync<TeamMemberResponse>();
        Assert.Equal(newMemberId, member.UserId);
        Assert.Equal(TeamRole.Member, member.Role);
    }

    [Fact]
    public async Task AddMember_ByNonOwnerMember_ReturnsForbidden()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var memberId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, memberId);

        // Act
        var response = await factory.CreateClientFor(memberId)
            .PostAsJsonAsync($"/api/teams/{team.Id}/members", new AddTeamMemberRequest(NewUserId()));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AlreadyMember_ReturnsConflict()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var memberId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, memberId);

        // Act
        var response = await owner.PostAsJsonAsync($"/api/teams/{team.Id}/members", new AddTeamMemberRequest(memberId));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_MemberLeavingTeam_ReturnsNoContent()
    {
        // Arrange
        var owner = factory.CreateClientFor(NewUserId());
        var memberId = NewUserId();
        var team = await owner.CreateTeamAsync();
        await owner.AddMemberAsync(team.Id, memberId);

        // Act
        var response = await factory.CreateClientFor(memberId).DeleteAsync($"/api/teams/{team.Id}/members/{memberId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_LastOwner_ReturnsConflict()
    {
        // Arrange
        var ownerId = NewUserId();
        var owner = factory.CreateClientFor(ownerId);
        var team = await owner.CreateTeamAsync();

        // Act
        var response = await owner.DeleteAsync($"/api/teams/{team.Id}/members/{ownerId}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
