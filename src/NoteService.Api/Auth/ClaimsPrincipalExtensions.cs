using System.Security.Claims;

namespace NoteService.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Returns the authenticated user's id. Only call from endpoints that require authorization.</summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The request is not authenticated.");
}
