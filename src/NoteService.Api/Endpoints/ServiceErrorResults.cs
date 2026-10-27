using Microsoft.AspNetCore.Http.HttpResults;

using NoteService.Api.Services;

namespace NoteService.Api.Endpoints;

public static class ServiceErrorResults
{
    public static ProblemHttpResult ToProblem(this ServiceError error, string resource) => error switch
    {
        ServiceError.NotFound => TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound, detail: $"The {resource} was not found."),
        ServiceError.Forbidden => TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden, detail: $"You do not have permission to perform this action on the {resource}."),
        ServiceError.Conflict => TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict, detail: $"The request conflicts with the current state of the {resource}."),
        ServiceError.PreconditionFailed => TypedResults.Problem(
            statusCode: StatusCodes.Status412PreconditionFailed,
            detail: $"The {resource} has been modified since it was retrieved. Fetch the latest version and retry."),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Not an error."),
    };
}
