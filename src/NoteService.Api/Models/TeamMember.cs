namespace NoteService.Api.Models;

public class TeamMember
{
    public int TeamId { get; set; }

    public required string UserId { get; set; }

    public TeamRole Role { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public Team? Team { get; set; }
}
