using Microsoft.EntityFrameworkCore;

using NoteService.Api.Contracts;
using NoteService.Api.Data;
using NoteService.Api.Models;

namespace NoteService.Api.Services;

public class TeamsService(NoteDbContext db, TimeProvider timeProvider) : ITeamsService
{
    public async Task<IReadOnlyList<TeamResponse>> ListAsync(string userId, CancellationToken cancellationToken) =>
        await db.TeamMembers
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Team!.Name)
            .Select(m => new TeamResponse(m.TeamId, m.Team!.Name, m.Team.CreatedAt, m.Role))
            .ToListAsync(cancellationToken);

    public async Task<ServiceResult<TeamDetailsResponse>> GetAsync(string userId, int teamId, CancellationToken cancellationToken)
    {
        var team = await db.Teams
            .Include(t => t.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId && t.Members.Any(m => m.UserId == userId), cancellationToken);

        if (team is null)
        {
            return ServiceError.NotFound;
        }

        var members = team.Members
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.UserId, StringComparer.Ordinal)
            .Select(ToResponse)
            .ToList();

        return new TeamDetailsResponse(team.Id, team.Name, team.CreatedAt, members);
    }

    public async Task<TeamResponse> CreateAsync(string userId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var team = new Team { Name = request.Name.Trim(), CreatedAt = now };
        team.Members.Add(new TeamMember { UserId = userId, Role = TeamRole.Owner, JoinedAt = now });

        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);

        return new TeamResponse(team.Id, team.Name, team.CreatedAt, TeamRole.Owner);
    }

    public async Task<ServiceResult<TeamMemberResponse>> AddMemberAsync(
        string userId, int teamId, AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var callerRole = await GetRoleAsync(userId, teamId, cancellationToken);
        if (callerRole is null)
        {
            return ServiceError.NotFound;
        }

        if (callerRole != TeamRole.Owner)
        {
            return ServiceError.Forbidden;
        }

        var newUserId = request.UserId.Trim();
        if (await GetRoleAsync(newUserId, teamId, cancellationToken) is not null)
        {
            return ServiceError.Conflict;
        }

        var member = new TeamMember
        {
            TeamId = teamId,
            UserId = newUserId,
            Role = TeamRole.Member,
            JoinedAt = timeProvider.GetUtcNow(),
        };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(member);
    }

    /// <summary>Owners can remove anyone; members can only remove themselves (leave the team).</summary>
    public async Task<ServiceResult> RemoveMemberAsync(
        string userId, int teamId, string memberUserId, CancellationToken cancellationToken)
    {
        var callerRole = await GetRoleAsync(userId, teamId, cancellationToken);
        if (callerRole is null)
        {
            return ServiceError.NotFound;
        }

        if (callerRole != TeamRole.Owner && memberUserId != userId)
        {
            return ServiceError.Forbidden;
        }

        var member = await db.TeamMembers.FirstOrDefaultAsync(
            m => m.TeamId == teamId && m.UserId == memberUserId, cancellationToken);
        if (member is null)
        {
            return ServiceError.NotFound;
        }

        if (member.Role == TeamRole.Owner &&
            !await db.TeamMembers.AnyAsync(
                m => m.TeamId == teamId && m.Role == TeamRole.Owner && m.UserId != memberUserId, cancellationToken))
        {
            // A team must always keep at least one owner.
            return ServiceError.Conflict;
        }

        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private async Task<TeamRole?> GetRoleAsync(string userId, int teamId, CancellationToken cancellationToken) =>
        await db.TeamMembers
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .Select(m => (TeamRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

    private static TeamMemberResponse ToResponse(TeamMember member) => new(member.UserId, member.Role, member.JoinedAt);
}
