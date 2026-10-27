using NoteService.Api.Contracts;

namespace NoteService.Api.Services;

public interface ITeamsService
{
    Task<IReadOnlyList<TeamResponse>> ListAsync(string userId, CancellationToken cancellationToken);

    Task<ServiceResult<TeamDetailsResponse>> GetAsync(string userId, Guid teamId, CancellationToken cancellationToken);

    Task<TeamResponse> CreateAsync(string userId, CreateTeamRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<TeamMemberResponse>> AddMemberAsync(
        string userId, Guid teamId, AddTeamMemberRequest request, CancellationToken cancellationToken);

    Task<ServiceResult> RemoveMemberAsync(string userId, Guid teamId, string memberUserId, CancellationToken cancellationToken);
}
