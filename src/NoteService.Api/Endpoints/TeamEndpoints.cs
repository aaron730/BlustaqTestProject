using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using NoteService.Api.Auth;
using NoteService.Api.Contracts;
using NoteService.Api.Services;

namespace NoteService.Api.Endpoints;

public static class TeamEndpoints
{
    private const string Resource = "team";
    private const string GetTeamRoute = "GetTeam";

    public static RouteGroupBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams").RequireAuthorization();

        group.MapGet("/", ListTeams)
            .WithSummary("List the teams the caller belongs to.");
        group.MapGet("/{id:guid}", GetTeam)
            .WithName(GetTeamRoute)
            .WithSummary("Get a team and its members. Only visible to members.");
        group.MapPost("/", CreateTeam)
            .WithSummary("Create a team. The caller becomes its owner.")
            .ProducesValidationProblem();
        group.MapPost("/{id:guid}/members", AddMember)
            .WithSummary("Add a member to a team. Owners only.")
            .ProducesValidationProblem();
        group.MapDelete("/{id:guid}/members/{userId}", RemoveMember)
            .WithSummary("Remove a member from a team. Owners can remove anyone; members can remove themselves.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<TeamResponse>>> ListTeams(
        ClaimsPrincipal user, ITeamsService teams, CancellationToken cancellationToken) =>
        TypedResults.Ok(await teams.ListAsync(user.GetUserId(), cancellationToken));

    private static async Task<Results<Ok<TeamDetailsResponse>, ProblemHttpResult>> GetTeam(
        Guid id, ClaimsPrincipal user, ITeamsService teams, CancellationToken cancellationToken)
    {
        var result = await teams.GetAsync(user.GetUserId(), id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem(Resource);
    }

    private static async Task<CreatedAtRoute<TeamResponse>> CreateTeam(
        CreateTeamRequest request, ClaimsPrincipal user, ITeamsService teams, CancellationToken cancellationToken)
    {
        var team = await teams.CreateAsync(user.GetUserId(), request, cancellationToken);

        return TypedResults.CreatedAtRoute(team, GetTeamRoute, new { id = team.Id });
    }

    private static async Task<Results<CreatedAtRoute<TeamMemberResponse>, ProblemHttpResult>> AddMember(
        Guid id, AddTeamMemberRequest request, ClaimsPrincipal user, ITeamsService teams,
        CancellationToken cancellationToken)
    {
        var result = await teams.AddMemberAsync(user.GetUserId(), id, request, cancellationToken);

        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, GetTeamRoute, new { id })
            : result.Error.ToProblem(Resource);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveMember(
        Guid id, string userId, ClaimsPrincipal user, ITeamsService teams, CancellationToken cancellationToken)
    {
        var result = await teams.RemoveMemberAsync(user.GetUserId(), id, userId, cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem(Resource);
    }
}
