using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using NoteService.Api.Auth;
using NoteService.Api.Contracts;
using NoteService.Api.Services;

namespace NoteService.Api.Endpoints;

public static class NoteEndpoints
{
    private const string Resource = "note";
    private const string GetNoteRoute = "GetNote";

    public static RouteGroupBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes").RequireAuthorization();

        group.MapGet("/", ListNotes)
            .WithSummary("List notes visible to the caller, with optional filters and pagination.")
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapGet("/{id:int}", GetNote)
            .WithName(GetNoteRoute)
            .WithSummary("Get a note. The ETag response header carries its version.");
        group.MapPost("/", CreateNote)
            .WithSummary("Create a personal note, or a team note when teamId is set.")
            .ProducesValidationProblem();
        group.MapPut("/{id:int}", UpdateNote)
            .WithSummary("Replace a note's title, content and tags. Requires an If-Match header with the note's ETag.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        group.MapDelete("/{id:int}", DeleteNote)
            .WithSummary("Delete a note. Allowed for the author and for owners of the note's team.");

        return group;
    }

    private static async Task<Results<Ok<PagedResponse<NoteResponse>>, ProblemHttpResult>> ListNotes(
        [AsParameters] NoteQuery query, ClaimsPrincipal user, INotesService notes, CancellationToken cancellationToken)
    {
        var result = await notes.ListAsync(user.GetUserId(), query, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem(Resource);
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> GetNote(
        int id, ClaimsPrincipal user, INotesService notes, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await notes.GetAsync(user.GetUserId(), id, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem(Resource);
        }

        response.Headers.ETag = ETags.FromVersion(result.Value!.Version);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<CreatedAtRoute<NoteResponse>, ProblemHttpResult>> CreateNote(
        CreateNoteRequest request, ClaimsPrincipal user, INotesService notes, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await notes.CreateAsync(user.GetUserId(), request, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem(Resource);
        }

        response.Headers.ETag = ETags.FromVersion(result.Value!.Version);
        return TypedResults.CreatedAtRoute(result.Value, GetNoteRoute, new { id = result.Value.Id });
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> UpdateNote(
        int id, UpdateNoteRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, ClaimsPrincipal user,
        INotesService notes, HttpResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                detail: "Updates require an If-Match header containing the note's current ETag.");
        }

        if (!ETags.TryParseIfMatch(ifMatch, out var expectedVersion))
        {
            return ServiceError.PreconditionFailed.ToProblem(Resource);
        }

        var result = await notes.UpdateAsync(user.GetUserId(), id, request, expectedVersion, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem(Resource);
        }

        response.Headers.ETag = ETags.FromVersion(result.Value!.Version);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteNote(
        int id, ClaimsPrincipal user, INotesService notes, CancellationToken cancellationToken)
    {
        var result = await notes.DeleteAsync(user.GetUserId(), id, cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem(Resource);
    }
}
