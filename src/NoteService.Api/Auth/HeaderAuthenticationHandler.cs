using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using NoteService.Api.Data;

namespace NoteService.Api.Auth;

/// <summary>
/// Lightweight authentication for the MVP: the caller's identity is taken from the <c>X-User-Id</c> header,
/// as it would be when the service sits behind a gateway that has already authenticated the user.
/// </summary>
public class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UserHeader";
    public const string HeaderName = "X-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = values.Count == 1 ? values[0]?.Trim() : null;
        if (string.IsNullOrEmpty(userId) || userId.Length > NoteDbContext.MaxUserIdLength)
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"The {HeaderName} header must contain a single, non-empty value of at most {NoteDbContext.MaxUserIdLength} characters."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        var problemDetails = Context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails =
            {
                Status = StatusCodes.Status401Unauthorized,
                Detail = $"Send a non-empty {HeaderName} header (at most {NoteDbContext.MaxUserIdLength} characters) identifying the caller.",
            },
        });
    }
}
