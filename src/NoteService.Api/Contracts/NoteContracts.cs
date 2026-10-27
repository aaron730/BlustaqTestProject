using System.ComponentModel.DataAnnotations;

using NoteService.Api.Data;

namespace NoteService.Api.Contracts;

public record CreateNoteRequest(
    [property: Required, StringLength(NoteDbContext.MaxTitleLength, MinimumLength = 1)] string Title,
    [property: StringLength(NoteDbContext.MaxContentLength)] string? Content,
    Guid? TeamId,
    IReadOnlyList<string>? Tags) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags, nameof(Tags));
}

public record UpdateNoteRequest(
    [property: Required, StringLength(NoteDbContext.MaxTitleLength, MinimumLength = 1)] string Title,
    [property: StringLength(NoteDbContext.MaxContentLength)] string? Content,
    IReadOnlyList<string>? Tags) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags, nameof(Tags));
}

/// <param name="TeamId">Only return notes shared with this team (caller must be a member).</param>
/// <param name="AuthorId">Only return notes written by this user.</param>
/// <param name="Search">Case-insensitive match against title or content.</param>
/// <param name="Tag">Only return notes carrying this tag.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of notes per page (1-100).</param>
public record NoteQuery(
    Guid? TeamId = null,
    [property: StringLength(NoteDbContext.MaxUserIdLength)] string? AuthorId = null,
    [property: StringLength(200)] string? Search = null,
    [property: StringLength(NoteDbContext.MaxTagLength)] string? Tag = null,
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);

public record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    string AuthorId,
    Guid? TeamId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public static class TagRules
{
    public const int MaxTagsPerNote = 20;

    public static IEnumerable<ValidationResult> Validate(IReadOnlyList<string>? tags, string memberName)
    {
        if (tags is null)
        {
            yield break;
        }

        if (tags.Count > MaxTagsPerNote)
        {
            yield return new ValidationResult($"A note can have at most {MaxTagsPerNote} tags.", [memberName]);
        }

        if (tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length > NoteDbContext.MaxTagLength))
        {
            yield return new ValidationResult(
                $"Tags must be non-empty and at most {NoteDbContext.MaxTagLength} characters.", [memberName]);
        }
    }

    /// <summary>Trims, lower-cases and de-duplicates tags so filtering is case-insensitive.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? tags) =>
        tags is null
            ? []
            : tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().Order().ToList();
}
