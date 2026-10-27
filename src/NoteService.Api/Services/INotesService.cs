using NoteService.Api.Contracts;

namespace NoteService.Api.Services;

public interface INotesService
{
    Task<ServiceResult<PagedResponse<NoteResponse>>> ListAsync(string userId, NoteQuery query, CancellationToken cancellationToken);

    Task<ServiceResult<NoteResponse>> GetAsync(string userId, Guid noteId, CancellationToken cancellationToken);

    Task<ServiceResult<NoteResponse>> CreateAsync(string userId, CreateNoteRequest request, CancellationToken cancellationToken);

    /// <param name="expectedVersion">The version the caller last saw, or null to skip the concurrency check.</param>
    Task<ServiceResult<NoteResponse>> UpdateAsync(
        string userId, Guid noteId, UpdateNoteRequest request, int? expectedVersion, CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(string userId, Guid noteId, CancellationToken cancellationToken);
}
