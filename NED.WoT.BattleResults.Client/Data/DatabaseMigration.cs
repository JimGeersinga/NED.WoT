using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

namespace NED.WoT.BattleResults.Client.Data;

/// <summary>Applies the EF migrations once, in the background, from app start-up. Queries await <see cref="Ready"/> first.</summary>
public sealed class DatabaseMigration
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    /// <summary>Completes when the schema is current; faulted when migrating failed (the consumer decides what to do).</summary>
    public Task Ready { get; }

    public DatabaseMigration(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        Ready = Task.Run(MigrateAsync);
    }

    private async Task MigrateAsync()
    {
        try
        {
            await using AppDbContext db = await _factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database migration failed: {ex.Message}");
            throw;
        }
    }
}
