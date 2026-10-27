using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using NoteService.Api.Data;

namespace NoteService.UnitTests;

/// <summary>An in-memory SQLite database that lives as long as this object; each test gets a fresh one.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a new context over the same database, e.g. to simulate a separate request.</summary>
    public NoteDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NoteDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
