using NoteService.Api.Contracts;
using NoteService.Api.Data;
using NoteService.Api.Models;
using NoteService.Api.Services;

using NSubstitute;

namespace NoteService.UnitTests;

public sealed class TeamsServiceTests : IDisposable
{
    private const string Alice = "alice";
    private const string Bob = "bob";

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly NoteDbContext _db;
    private readonly TeamsService _sut;

    public TeamsServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _db = _database.CreateContext();
        _sut = new TeamsService(_db, time);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_MakesCallerOwner()
    {
        // Act
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest(" Platform "), CancellationToken.None);

        // Assert
        Assert.Equal("Platform", team.Name);
        Assert.Equal(TeamRole.Owner, team.Role);
        Assert.Equal(Now, team.CreatedAt);
        var member = Assert.Single(_db.TeamMembers);
        Assert.Equal((Alice, TeamRole.Owner), (member.UserId, member.Role));
    }

    [Fact]
    public async Task AddMemberAsync_CallerNotInTeam_ReturnsNotFound()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);

        // Act
        var result = await _sut.AddMemberAsync(Bob, team.Id, new AddTeamMemberRequest("carol"), CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_CallerIsPlainMember_ReturnsForbidden()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);
        await _sut.AddMemberAsync(Alice, team.Id, new AddTeamMemberRequest(Bob), CancellationToken.None);

        // Act
        var result = await _sut.AddMemberAsync(Bob, team.Id, new AddTeamMemberRequest("carol"), CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_UserAlreadyMember_ReturnsConflict()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);
        await _sut.AddMemberAsync(Alice, team.Id, new AddTeamMemberRequest(Bob), CancellationToken.None);

        // Act
        var result = await _sut.AddMemberAsync(Alice, team.Id, new AddTeamMemberRequest(Bob), CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task RemoveMemberAsync_MemberRemovingAnotherMember_ReturnsForbidden()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);
        await _sut.AddMemberAsync(Alice, team.Id, new AddTeamMemberRequest(Bob), CancellationToken.None);

        // Act
        var result = await _sut.RemoveMemberAsync(Bob, team.Id, Alice, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task RemoveMemberAsync_LastOwner_ReturnsConflict()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);

        // Act
        var result = await _sut.RemoveMemberAsync(Alice, team.Id, Alice, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task GetAsync_CallerNotInTeam_ReturnsNotFound()
    {
        // Arrange
        var team = await _sut.CreateAsync(Alice, new CreateTeamRequest("Team"), CancellationToken.None);

        // Act
        var result = await _sut.GetAsync(Bob, team.Id, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.NotFound, result.Error);
    }
}
