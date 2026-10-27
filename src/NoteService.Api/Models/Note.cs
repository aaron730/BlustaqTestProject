namespace NoteService.Api.Models;

/// <summary>
/// A note is either personal (<see cref="TeamId"/> is null, visible only to its author)
/// or shared with a team (visible to and editable by every member of that team).
/// </summary>
public class Note
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public required string AuthorId { get; set; }

    public int? TeamId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token, incremented on every update and exposed as the ETag.</summary>
    public int Version { get; set; }

    public List<NoteTag> Tags { get; set; } = [];

    public Team? Team { get; set; }
}
