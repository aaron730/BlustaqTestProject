namespace NoteService.Api.Models;

public class Team
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<TeamMember> Members { get; set; } = [];
}
