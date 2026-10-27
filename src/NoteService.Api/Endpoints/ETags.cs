namespace NoteService.Api.Endpoints;

/// <summary>Maps a note's concurrency version to and from HTTP entity tags.</summary>
public static class ETags
{
    public static string FromVersion(int version) => $"\"{version}\"";

    /// <summary>
    /// Parses an <c>If-Match</c> header value. <c>*</c> yields a null version (match any);
    /// strong and weak tags (<c>"3"</c>, <c>W/"3"</c>) yield their version.
    /// </summary>
    public static bool TryParseIfMatch(string? ifMatch, out int? version)
    {
        version = null;
        var value = ifMatch?.Trim();

        if (value == "*")
        {
            return true;
        }

        if (value is null)
        {
            return false;
        }

        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"' || !int.TryParse(value[1..^1], out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
