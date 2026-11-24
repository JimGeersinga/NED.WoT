using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

namespace NED.WoT.BattleResults.Client.Data;

public sealed class TeamDb(DbContextOptions<TeamDb> options) : DbContext(options)
{
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "gevechtslog.db");

    public DbSet<LobbyTeam> LobbyTeams => Set<LobbyTeam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LobbyTeam>(e =>
        {
            e.HasIndex(x => x.LobbyKey).IsUnique();
            e.Property(x => x.LobbyKey).IsRequired().HasMaxLength(32);
            e.Property(x => x.TeamName).IsRequired().HasMaxLength(40);
        });
    }

#if DEBUG
    /// <summary>Runs against a temporary file, never the real database. Called on demand (by #67), not at start-up.</summary>
    public static async Task SelfCheckAsync()
    {
        string path = Path.GetTempFileName();
        try
        {
            DbContextOptions<TeamDb> options = new DbContextOptionsBuilder<TeamDb>().UseSqlite($"Data Source={path}").Options;

            await using (TeamDb db = new(options))
            {
                // GetTempFileName leaves an empty file; EnsureCreated treats a database without tables as new.
                Debug.Assert(await db.Database.EnsureCreatedAsync(), "EnsureCreated made no tables");
                db.LobbyTeams.Add(new LobbyTeam { LobbyKey = "2026-04-05|34288599", TeamName = "NED-1", ChangedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            await using (TeamDb db = new(options))
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
