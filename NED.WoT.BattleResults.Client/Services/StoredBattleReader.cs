using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using NED.WoT.BattleResults.Client.Data;
using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Reads the stored battles of a folder straight into <see cref="BattleReport"/> and <see cref="Player"/> with two queries, without EF materialising
/// <c>StoredBattle</c> and <c>StoredPlayer</c> first (the EF read was 3-4 s of the warm load of 12,787 battles with 331k players).
/// The column lists are the ones of <c>BattleReportMapper.ToStored</c>; a migration that changes them changes this file too.
/// </summary>
public static class StoredBattleReader
{
    /// <summary>A stored file: what the cache check compares, and its report. The report still needs <see cref="BattleReportMapper.Complete"/> (unless unreadable).</summary>
    public sealed record Entry(long Id, long Length, long WriteTicks, BattleReport Report);

    private const string BATTLES = "SELECT Id, FileName, FileLength, FileWriteTicks, Unreadable, MatchStart, MapName, MapKey, RecorderName, ArenaUniqueId, MatchDuration, FinishReason, Team1Health, Team2Health, Team1IsWinner, Team2IsWinner, Team1ClanId, Team2ClanId FROM Battles WHERE Folder = $folder";

    // Players are read in insert order (= table order) per battle: FromStored and Complete rely on it for Number.
    private const string PLAYERS = "SELECT p.BattleId, p.TeamNumber, p.Name, p.Clan, p.Vehicle, p.AccountId, p.PrebattleId, p.ClanId, p.DamageDealt, p.DamageReceived, p.DamageBlocked, p.Piercings, p.HitsReceived, p.ShotsReceived, p.ShotsBlocked, p.ExperienceEarned, p.CreditsEarned, p.Health, p.MaxHealth, p.LifeTime, p.Kills, p.Shots, p.DirectHits, p.DeathReason, p.Spotted, p.CapturePoints, p.IsTeamKiller FROM Players p JOIN Battles b ON b.Id = p.BattleId WHERE b.Folder = $folder ORDER BY p.BattleId, p.Id";

    /// <returns>The stored files of <paramref name="folder"/> by file name (case-insensitive).</returns>
    public static Dictionary<string, Entry> Read(AppDbContext db, string folder)
    {
        Dictionary<long, Entry> byId = [];
        Dictionary<string, Entry> byName = new(StringComparer.OrdinalIgnoreCase);

        db.Database.OpenConnection();
        try
        {
            DbConnection connection = db.Database.GetDbConnection();

            using (DbCommand command = Command(connection, BATTLES, folder))
            using (DbDataReader r = command.ExecuteReader())
            {
                while (r.Read())
                {
                    string name = r.GetString(1);
                    DateTime matchStart = r.GetDateTime(5);
                    BattleReport report = r.GetBoolean(4)
                        ? BattleReportMapper.UnreadableRow(name, matchStart)
                        : new BattleReport
                        {
                            FileName = name,
                            MatchStart = matchStart,
                            MapName = Str(r, 6),
                            MapKey = Str(r, 7),
                            RecorderName = Str(r, 8),
                            ArenaUniqueId = Long(r, 9),
                            MatchDuration = Int(r, 10),
                            FinishReason = Int(r, 11),
                            Team1 = new Team { Number = 1, Health = Int(r, 12), IsWinner = Bool(r, 14), ClanId = Long(r, 16) },
                            Team2 = new Team { Number = 2, Health = Int(r, 13), IsWinner = Bool(r, 15), ClanId = Long(r, 17) }
                        };

                    Entry entry = new(r.GetInt64(0), r.GetInt64(2), r.GetInt64(3), report);
                    byId[entry.Id] = entry;
                    byName[name] = entry;
                }
            }

            using (DbCommand command = Command(connection, PLAYERS, folder))
            using (DbDataReader r = command.ExecuteReader())
            {
                long lastId = -1;
                BattleReport? report = null;
                while (r.Read())
                {
                    long battleId = r.GetInt64(0);
                    if (battleId != lastId)
                    {
                        lastId = battleId;
                        report = byId.GetValueOrDefault(battleId)?.Report;
                    }

                    if (report is null)
                    {
                        continue;
                    }

                    Team team = r.GetInt32(1) == 1 ? report.Team1 : report.Team2;
                    team.Players.Add(new Player(team)
                    {
                        Name = Str(r, 2),
                        Clan = Str(r, 3),
                        Vehicle = Str(r, 4),
                        AccountId = Long(r, 5),
                        PrebattleId = Long(r, 6),
                        ClanId = Long(r, 7),
                        DamageDealt = Int(r, 8),
                        DamageReceived = Int(r, 9),
                        DamageBlocked = Int(r, 10),
                        Piercings = Int(r, 11),
                        HitsReceived = Int(r, 12),
                        ShotsReceived = Int(r, 13),
                        ShotsBlocked = Int(r, 14),
                        ExperienceEarned = Int(r, 15),
                        CreditsEarned = Int(r, 16),
                        Health = Int(r, 17),
                        MaxHealth = Int(r, 18),
                        LifeTime = Int(r, 19),
                        Kills = Int(r, 20),
                        Shots = Int(r, 21),
                        DirectHits = Int(r, 22),
                        DeathReason = Int(r, 23),
                        Spotted = Int(r, 24),
                        CapturePoints = Int(r, 25),
                        IsTeamKiller = Bool(r, 26)
                    });
                }
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }

        return byName;
    }

    private static DbCommand Command(DbConnection connection, string sql, string folder)
    {
        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$folder";
        parameter.Value = folder;
        command.Parameters.Add(parameter);

        return command;
    }

    private static string? Str(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static int? Int(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
    private static long? Long(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);
    private static bool? Bool(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetBoolean(i);
}
