using Microsoft.EntityFrameworkCore;

using NoteService.Api.Models;

namespace NoteService.Api.Data;

public class NoteDbContext(DbContextOptions<NoteDbContext> options) : DbContext(options)
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 100_000;
    public const int MaxTagLength = 50;
    public const int MaxTeamNameLength = 100;
    public const int MaxUserIdLength = 100;

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteTag> NoteTags => Set<NoteTag>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(team =>
        {
            team.Property(t => t.Name).HasMaxLength(MaxTeamNameLength);
            team.HasMany(t => t.Members).WithOne(m => m.Team).HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(member =>
        {
            member.HasKey(m => new { m.TeamId, m.UserId });
            member.Property(m => m.UserId).HasMaxLength(MaxUserIdLength);
            member.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<Note>(note =>
        {
            note.Property(n => n.Title).HasMaxLength(MaxTitleLength);
            note.Property(n => n.Content).HasMaxLength(MaxContentLength);
            note.Property(n => n.AuthorId).HasMaxLength(MaxUserIdLength);
            note.Property(n => n.Version).IsConcurrencyToken();
            note.HasOne(n => n.Team).WithMany().HasForeignKey(n => n.TeamId).OnDelete(DeleteBehavior.Cascade);
            note.HasMany(n => n.Tags).WithOne().HasForeignKey(t => t.NoteId).OnDelete(DeleteBehavior.Cascade);
            note.HasIndex(n => n.AuthorId);
            note.HasIndex(n => new { n.TeamId, n.UpdatedAt });
        });

        modelBuilder.Entity<NoteTag>(tag =>
        {
            tag.HasKey(t => new { t.NoteId, t.Name });
            tag.Property(t => t.Name).HasMaxLength(MaxTagLength);
            tag.HasIndex(t => t.Name);
        });
    }
}
