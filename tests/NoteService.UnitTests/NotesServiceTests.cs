using NoteService.Api.Contracts;
using NoteService.Api.Data;
using NoteService.Api.Models;
using NoteService.Api.Services;

using NSubstitute;

namespace NoteService.UnitTests;

public sealed class NotesServiceTests : IDisposable
{
    private const string Alice = "alice";
    private const string Bob = "bob";
    private const string Carol = "carol";

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly NoteDbContext _db;
    private readonly NotesService _sut;

    public NotesServiceTests()
    {
        _time.GetUtcNow().Returns(Start);
        _db = _database.CreateContext();
        _sut = new NotesService(_db, _time);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task CreateAsync_PersonalNote_SetsAuthorTimestampsAndInitialVersion()
    {
        // Arrange
        var request = new CreateNoteRequest("  Title  ", null, null, null);

        // Act
        var result = await _sut.CreateAsync(Alice, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var note = result.Value!;
        Assert.Equal("Title", note.Title);
        Assert.Equal(string.Empty, note.Content);
        Assert.Equal(Alice, note.AuthorId);
        Assert.Equal(Start, note.CreatedAt);
        Assert.Equal(Start, note.UpdatedAt);
        Assert.Equal(1, note.Version);
    }

    [Fact]
    public async Task CreateAsync_DuplicateMixedCaseTags_StoresNormalizedDistinctTags()
    {
        // Arrange
        var request = new CreateNoteRequest("Title", null, null, ["Work", " work ", "Ideas"]);

        // Act
        var result = await _sut.CreateAsync(Alice, request, CancellationToken.None);

        // Assert
        Assert.Equal(["ideas", "work"], result.Value!.Tags);
    }

    [Fact]
    public async Task CreateAsync_TeamCallerIsNotMemberOf_ReturnsForbidden()
    {
        // Arrange
        var teamId = await SeedTeamAsync((Bob, TeamRole.Owner));

        // Act
        var result = await _sut.CreateAsync(Alice, new CreateNoteRequest("Title", null, teamId, null), CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Forbidden, result.Error);
        Assert.Empty(_db.Notes);
    }

    [Fact]
    public async Task GetAsync_OtherUsersPersonalNote_ReturnsNotFound()
    {
        // Arrange
        var note = (await _sut.CreateAsync(Bob, new CreateNoteRequest("Private", null, null, null), CancellationToken.None)).Value!;

        // Act
        var result = await _sut.GetAsync(Alice, note.Id, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WithCurrentVersion_IncrementsVersionAndUpdatesTimestamp()
    {
        // Arrange
        var note = await CreatePersonalNoteAsync(Alice);
        var later = Start.AddMinutes(5);
        _time.GetUtcNow().Returns(later);

        // Act
        var result = await _sut.UpdateAsync(
            Alice, note.Id, new UpdateNoteRequest("Edited", "Body", null), note.Version, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Version);
        Assert.Equal("Edited", result.Value.Title);
        Assert.Equal(Start, result.Value.CreatedAt);
        Assert.Equal(later, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleVersion_ReturnsPreconditionFailed()
    {
        // Arrange
        var note = await CreatePersonalNoteAsync(Alice);
        await _sut.UpdateAsync(Alice, note.Id, new UpdateNoteRequest("First", null, null), 1, CancellationToken.None);

        // Act
        var result = await _sut.UpdateAsync(
            Alice, note.Id, new UpdateNoteRequest("Second", null, null), 1, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.PreconditionFailed, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WithoutExpectedVersion_SkipsConcurrencyCheck()
    {
        // Arrange
        var note = await CreatePersonalNoteAsync(Alice);
        await _sut.UpdateAsync(Alice, note.Id, new UpdateNoteRequest("First", null, null), 1, CancellationToken.None);

        // Act
        var result = await _sut.UpdateAsync(
            Alice, note.Id, new UpdateNoteRequest("Second", null, null), expectedVersion: null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Version);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentWriteBetweenReadAndSave_ReturnsPreconditionFailed()
    {
        // Arrange: two requests read version 1; the first one saves before the second.
        var note = await CreatePersonalNoteAsync(Alice);
        await using var otherRequestDb = _database.CreateContext();
        var otherRequest = new NotesService(otherRequestDb, _time);
        var stalePreload = await _db.Notes.FindAsync(note.Id);
        Assert.NotNull(stalePreload);
        await otherRequest.UpdateAsync(Alice, note.Id, new UpdateNoteRequest("Winner", null, null), 1, CancellationToken.None);

        // Act: _db still holds the tracked version-1 entity, so the stale version passes the in-memory check
        // and the conflict is only caught by the concurrency token when saving.
        var result = await _sut.UpdateAsync(Alice, note.Id, new UpdateNoteRequest("Loser", null, null), 1, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.PreconditionFailed, result.Error);
        await using var verifyDb = _database.CreateContext();
        Assert.Equal("Winner", (await verifyDb.Notes.FindAsync(note.Id))!.Title);
    }

    [Fact]
    public async Task UpdateAsync_ChangedTags_ReplacesTagSet()
    {
        // Arrange
        var note = (await _sut.CreateAsync(
            Alice, new CreateNoteRequest("Title", null, null, ["keep", "drop"]), CancellationToken.None)).Value!;

        // Act
        var result = await _sut.UpdateAsync(
            Alice, note.Id, new UpdateNoteRequest("Title", null, ["Keep", "add"]), 1, CancellationToken.None);

        // Assert
        Assert.Equal(["add", "keep"], result.Value!.Tags);
        await using var verifyDb = _database.CreateContext();
        Assert.Equal(["add", "keep"], verifyDb.NoteTags.Where(t => t.NoteId == note.Id).Select(t => t.Name).Order().ToList());
    }

    [Fact]
    public async Task UpdateAsync_TeamNoteByAnyMember_Succeeds()
    {
        // Arrange
        var teamId = await SeedTeamAsync((Alice, TeamRole.Owner), (Bob, TeamRole.Member));
        var note = (await _sut.CreateAsync(Alice, new CreateNoteRequest("Shared", null, teamId, null), CancellationToken.None)).Value!;

        // Act
        var result = await _sut.UpdateAsync(Bob, note.Id, new UpdateNoteRequest("Edited", null, null), 1, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_TeamNoteByNonAuthorMember_ReturnsForbidden()
    {
        // Arrange
        var teamId = await SeedTeamAsync((Alice, TeamRole.Owner), (Bob, TeamRole.Member), (Carol, TeamRole.Member));
        var note = (await _sut.CreateAsync(Bob, new CreateNoteRequest("Bob's", null, teamId, null), CancellationToken.None)).Value!;

        // Act
        var result = await _sut.DeleteAsync(Carol, note.Id, CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_TeamNoteByTeamOwner_DeletesNoteAndTags()
    {
        // Arrange
        var teamId = await SeedTeamAsync((Alice, TeamRole.Owner), (Bob, TeamRole.Member));
        var note = (await _sut.CreateAsync(Bob, new CreateNoteRequest("Bob's", null, teamId, ["x"]), CancellationToken.None)).Value!;

        // Act
        var result = await _sut.DeleteAsync(Alice, note.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await using var verifyDb = _database.CreateContext();
        Assert.Empty(verifyDb.Notes);
        Assert.Empty(verifyDb.NoteTags);
    }

    [Fact]
    public async Task ListAsync_MultiplePages_OrdersByMostRecentlyUpdated()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            _time.GetUtcNow().Returns(Start.AddMinutes(i));
            await _sut.CreateAsync(Alice, new CreateNoteRequest($"Note {i}", null, null, null), CancellationToken.None);
        }

        // Act
        var result = await _sut.ListAsync(Alice, new NoteQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Equal(["Note 2", "Note 1"], result.Value.Items.Select(n => n.Title));
    }

    [Fact]
    public async Task ListAsync_TeamFilterForNonMember_ReturnsForbidden()
    {
        // Arrange
        var teamId = await SeedTeamAsync((Bob, TeamRole.Owner));

        // Act
        var result = await _sut.ListAsync(Alice, new NoteQuery(TeamId: teamId), CancellationToken.None);

        // Assert
        Assert.Equal(ServiceError.Forbidden, result.Error);
    }

    private async Task<NoteResponse> CreatePersonalNoteAsync(string userId) =>
        (await _sut.CreateAsync(userId, new CreateNoteRequest("Title", null, null, null), CancellationToken.None)).Value!;

    private async Task<int> SeedTeamAsync(params (string UserId, TeamRole Role)[] members)
    {
        var team = new Team { Name = "Team", CreatedAt = Start };
        team.Members.AddRange(members.Select(m => new TeamMember { UserId = m.UserId, Role = m.Role, JoinedAt = Start }));

        await using var seedDb = _database.CreateContext();
        seedDb.Teams.Add(team);
        await seedDb.SaveChangesAsync();

        return team.Id;
    }
}
