namespace NoteService.Api.Models;

public class NoteTag
{
    public Guid NoteId { get; set; }

    /// <summary>Normalized (trimmed, lower-case) tag name.</summary>
    public required string Name { get; set; }
}
