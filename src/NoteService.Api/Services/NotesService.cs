using Microsoft.EntityFrameworkCore;

using NoteService.Api.Contracts;
using NoteService.Api.Data;
using NoteService.Api.Models;

namespace NoteService.Api.Services;

public class NotesService(NoteDbContext db, TimeProvider timeProvider) : INotesService
{
    public async Task<ServiceResult<PagedResponse<NoteResponse>>> ListAsync(
        string userId, NoteQuery query, CancellationToken cancellationToken)
    {
        var notes = VisibleTo(userId);

        if (query.TeamId is { } teamId)
        {
            if (!await IsMemberAsync(userId, teamId, cancellationToken))
            {
                return ServiceError.Forbidden;
            }

            notes = notes.Where(n => n.TeamId == teamId);
        }

        if (!string.IsNullOrWhiteSpace(query.AuthorId))
        {
            notes = notes.Where(n => n.AuthorId == query.AuthorId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            notes = notes.Where(n => n.Title.ToLower().Contains(search) || n.Content.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim().ToLowerInvariant();
            notes = notes.Where(n => n.Tags.Any(t => t.Name == tag));
        }

        var totalCount = await notes.CountAsync(cancellationToken);
        var page = await notes
            .OrderByDescending(n => n.UpdatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(n => n.Tags)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResponse<NoteResponse>(page.Select(ToResponse).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<NoteResponse>> GetAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        var note = await VisibleTo(userId)
            .Include(n => n.Tags)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);

        return note is null ? ServiceError.NotFound : ToResponse(note);
    }

    public async Task<ServiceResult<NoteResponse>> CreateAsync(
        string userId, CreateNoteRequest request, CancellationToken cancellationToken)
    {
        if (request.TeamId is { } teamId && !await IsMemberAsync(userId, teamId, cancellationToken))
        {
            return ServiceError.Forbidden;
        }

        var now = timeProvider.GetUtcNow();
        var note = new Note
        {
            Title = request.Title.Trim(),
            Content = request.Content ?? string.Empty,
            AuthorId = userId,
            TeamId = request.TeamId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        note.Tags.AddRange(TagRules.Normalize(request.Tags).Select(name => new NoteTag { Name = name }));

        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(note);
    }

    public async Task<ServiceResult<NoteResponse>> UpdateAsync(
        string userId, int noteId, UpdateNoteRequest request, int? expectedVersion, CancellationToken cancellationToken)
    {
        var note = await VisibleTo(userId)
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);

        if (note is null)
        {
            return ServiceError.NotFound;
        }

        if (expectedVersion is { } expected && expected != note.Version)
        {
            return ServiceError.PreconditionFailed;
        }

        note.Title = request.Title.Trim();
        note.Content = request.Content ?? string.Empty;
        ReplaceTags(note, TagRules.Normalize(request.Tags));
        note.UpdatedAt = timeProvider.GetUtcNow();
        note.Version++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request updated the note between our read and write.
            return ServiceError.PreconditionFailed;
        }

        return ToResponse(note);
    }

    public async Task<ServiceResult> DeleteAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        var note = await VisibleTo(userId).FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);

        if (note is null)
        {
            return ServiceError.NotFound;
        }

        if (note.AuthorId != userId && !await IsOwnerAsync(userId, note.TeamId, cancellationToken))
        {
            return ServiceError.Forbidden;
        }

        db.Notes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    /// <summary>Personal notes are visible to their author; team notes to every member of the team.</summary>
    private IQueryable<Note> VisibleTo(string userId) =>
        db.Notes.Where(n =>
            (n.TeamId == null && n.AuthorId == userId) ||
            (n.TeamId != null && db.TeamMembers.Any(m => m.TeamId == n.TeamId && m.UserId == userId)));

    private Task<bool> IsMemberAsync(string userId, int teamId, CancellationToken cancellationToken) =>
        db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);

    private async Task<bool> IsOwnerAsync(string userId, int? teamId, CancellationToken cancellationToken) =>
        teamId is not null &&
        await db.TeamMembers.AnyAsync(
            m => m.TeamId == teamId && m.UserId == userId && m.Role == TeamRole.Owner, cancellationToken);

    private static void ReplaceTags(Note note, IReadOnlyList<string> tags)
    {
        note.Tags.RemoveAll(t => !tags.Contains(t.Name));

        var existing = note.Tags.Select(t => t.Name).ToHashSet();
        note.Tags.AddRange(tags.Where(t => !existing.Contains(t)).Select(name => new NoteTag { NoteId = note.Id, Name = name }));
    }

    private static NoteResponse ToResponse(Note note) => new(
        note.Id,
        note.Title,
        note.Content,
        note.AuthorId,
        note.TeamId,
        note.Tags.Select(t => t.Name).Order().ToList(),
        note.CreatedAt,
        note.UpdatedAt,
        note.Version);
}
