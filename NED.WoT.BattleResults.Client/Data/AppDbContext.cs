using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

namespace NED.WoT.BattleResults.Client.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "gevechtslog.db");

    public DbSet<LobbyTeam> LobbyTeams => Set<LobbyTeam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

#if DEBUG
    /// <summary>Runs against a temporary file, never the real database. Called on demand (by #67), not at start-up.</summary>
    public static async Task SelfCheckAsync()
    {
        string path = Path.GetTempFileName();
        try
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

            await using (AppDbContext db = new(options))
            {
                // The app calls `await db.Database.MigrateAsync()` once before the first query (#67); this replaces the old EnsureCreated plan.
                await db.Database.MigrateAsync();
                Debug.Assert(await db.Database.SqlQuery<int>($"SELECT count(*) AS Value FROM sqlite_master WHERE name = 'LobbyTeams'").SingleAsync() == 1, "Migrate made no LobbyTeams table");
                db.LobbyTeams.Add(new LobbyTeam { LobbyKey = "2026-04-05|34288599", TeamName = "NED-1", ChangedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            await using (AppDbContext db = new(options))
            {
                Debug.Assert(await db.LobbyTeams.Where(x => x.LobbyKey == "2026-04-05|34288599").Select(x => x.TeamName).SingleAsync() == "NED-1");
                db.LobbyTeams.Add(new LobbyTeam { LobbyKey = "2026-04-05|34288599", TeamName = "NED-2", ChangedAt = DateTime.UtcNow });
                bool threw = false;
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException) { threw = true; }
                Debug.Assert(threw, "a duplicate LobbyKey must throw DbUpdateException");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
#endif
}
