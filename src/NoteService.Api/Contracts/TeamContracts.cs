using System.ComponentModel.DataAnnotations;

using NoteService.Api.Data;
using NoteService.Api.Models;

namespace NoteService.Api.Contracts;

public record CreateTeamRequest(
    [property: Required, StringLength(NoteDbContext.MaxTeamNameLength, MinimumLength = 1)] string Name);

public record AddTeamMemberRequest(
    [property: Required, StringLength(NoteDbContext.MaxUserIdLength, MinimumLength = 1)] string UserId);

public record TeamResponse(Guid Id, string Name, DateTimeOffset CreatedAt, TeamRole Role);

public record TeamDetailsResponse(Guid Id, string Name, DateTimeOffset CreatedAt, IReadOnlyList<TeamMemberResponse> Members);

public record TeamMemberResponse(string UserId, TeamRole Role, DateTimeOffset JoinedAt);
