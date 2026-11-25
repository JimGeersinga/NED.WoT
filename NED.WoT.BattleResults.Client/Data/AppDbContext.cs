using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

using NED.WoT.BattleResults.Client.Data.Models;
#if DEBUG
using NED.WoT.BattleResults.Client.Models;
using NED.WoT.BattleResults.Client.Services;
#endif

namespace NED.WoT.BattleResults.Client.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "gevechtslog.db");

    public DbSet<LobbyTeam> LobbyTeams => Set<LobbyTeam>();

    public DbSet<StoredBattle> Battles => Set<StoredBattle>();

    public DbSet<StoredPlayer> Players => Set<StoredPlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

#if DEBUG
    /// <summary>Runs against a temporary file, never the real database. Debug builds run it once per process, from the first <c>BattleReportService</c>.</summary>
    public static async Task SelfCheckAsync()
    {
        string path = Path.GetTempFileName();
        try
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

            await using (AppDbContext db = new(options))
            {
                // The app migrates at start-up through DatabaseMigration (consumers await DatabaseMigration.Ready); this runs the same MigrateAsync on a temp file.
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

            // The replay cache: a battle with 2 players round-trips through the tables, a second row of the same file throws, the team names stay as they were.
            FileInfo replay = new(path);
            BattleReport report = new() { MatchStart = new DateTime(2026, 4, 5, 21, 30, 0), MapName = "Great Wall", Team1 = new Team { Number = 1 }, Team2 = new Team { Number = 2 } };
            report.Team1.Players.Add(new Player(report.Team1) { Name = "A", Clan = "NED", ExperienceEarned = 500, DamageDealt = 2500 });
            report.Team1.Players.Add(new Player(report.Team1) { Name = "C", Clan = "NED", ExperienceEarned = 500 });
            report.Team1.Players.Add(new Player(report.Team1) { Name = "D", Clan = "NED", ExperienceEarned = 500 });
            report.Team2.Players.Add(new Player(report.Team2) { Name = "B", Clan = "ABC", ExperienceEarned = 400 });
            await using (AppDbContext db = new(options))
            {
                db.Battles.Add(BattleReportMapper.ToStored(report, @"C:\replays", replay));
                await db.SaveChangesAsync();
            }

            await using (AppDbContext db = new(options))
            {
                string upperName = replay.Name.ToUpperInvariant();
                StoredBattle row = await db.Battles.Include(x => x.Players).SingleAsync(x => x.Folder == @"c:\REPLAYS" && x.FileName == upperName);
                BattleReport back = BattleReportMapper.FromStored(row, null);
                Debug.Assert(row.Players.Count == 4 && back.MapName == "Great Wall" && back.Team1.Players[0].DamageDealt == 2500 && back.Team2.Players[0].Name == "B", "a stored battle comes back, the folder and file name case-insensitive");
                Debug.Assert(back.Team1.Players.Select(x => x.Name).SequenceEqual(["A", "C", "D"]) && back.Team1.Players.Select(x => x.Number).SequenceEqual([1, 2, 3]), "players with equal experience keep their stored order and numbers");
                Debug.Assert(await db.LobbyTeams.CountAsync() == 1, "the team names are untouched");

                db.Battles.Add(BattleReportMapper.ToStored(report, @"C:\replays", replay));
                bool duplicate = false;
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException) { duplicate = true; }
                Debug.Assert(duplicate, "a second row of the same (Folder, FileName) must throw DbUpdateException");
            }
        }
        catch (Exception ex)
        {
            // Started without await: make a fault visible instead of losing it.
            Debug.Fail(ex.ToString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
#endif
}
