using Microsoft.EntityFrameworkCore;

using NED.WoT.BattleResults.Client.Data;
using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Reads the stored battles of a folder with two EF projections, straight into <see cref="BattleReport"/> and <see cref="Player"/>: no <c>StoredBattle</c> or
/// <c>StoredPlayer</c> entities, no <c>Include</c> and no fix-up (the <c>Include</c> read cost about twice as much on the warm load of 12,787 battles with 331k players).
/// A new column goes into the projection here, next to the property it fills.
/// </summary>
public static class StoredBattleReader
{
    /// <summary>A stored file: what the cache check compares, and its report. The report still needs <see cref="BattleReportMapper.Complete"/> (unless unreadable).</summary>
    public sealed record Entry(long Id, long Length, long WriteTicks, BattleReport Report);

    /// <returns>The stored files of <paramref name="folder"/> by file name (case-insensitive).</returns>
    public static Dictionary<string, Entry> Read(AppDbContext db, string folder)
    {
        Dictionary<long, BattleReport> byId = [];
        Dictionary<string, Entry> byName = new(StringComparer.OrdinalIgnoreCase);

        var battles = db.Battles.AsNoTracking().Where(b => b.Folder == folder).Select(b => new
        {
            b.Id,
            b.FileLength,
            b.FileWriteTicks,
            b.Unreadable,
            Report = new BattleReport
            {
                FileName = b.FileName,
                MatchStart = b.MatchStart,
                MapName = b.MapName,
                MapKey = b.MapKey,
                RecorderName = b.RecorderName,
                ArenaUniqueId = b.ArenaUniqueId,
                MatchDuration = b.MatchDuration,
                FinishReason = b.FinishReason,
                Team1 = new Team { Number = 1, Health = b.Team1Health, IsWinner = b.Team1IsWinner, ClanId = b.Team1ClanId },
                Team2 = new Team { Number = 2, Health = b.Team2Health, IsWinner = b.Team2IsWinner, ClanId = b.Team2ClanId }
            }
        });

        foreach (var row in battles)
        {
            if (row.Unreadable)
            {
                row.Report.Error = BattleReportService.UnreadableMessage(row.Report.FileName!);
            }

            byId[row.Id] = row.Report;
            byName[row.Report.FileName!] = new Entry(row.Id, row.FileLength, row.FileWriteTicks, row.Report);
        }

        // In insert order (= table order) per battle: Complete relies on it for Number.
        var players = db.Players.AsNoTracking()
            .Where(p => db.Battles.Any(b => b.Id == p.BattleId && b.Folder == folder))
            .OrderBy(p => p.BattleId).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.BattleId,
                p.TeamNumber,
                Player = new Player(null!)
                {
                    Name = p.Name,
                    Clan = p.Clan,
                    Vehicle = p.Vehicle,
                    AccountId = p.AccountId,
                    PrebattleId = p.PrebattleId,
                    ClanId = p.ClanId,
                    DamageDealt = p.DamageDealt,
                    DamageReceived = p.DamageReceived,
                    DamageBlocked = p.DamageBlocked,
                    Piercings = p.Piercings,
                    HitsReceived = p.HitsReceived,
                    ShotsReceived = p.ShotsReceived,
                    ShotsBlocked = p.ShotsBlocked,
                    ExperienceEarned = p.ExperienceEarned,
                    CreditsEarned = p.CreditsEarned,
                    Health = p.Health,
                    MaxHealth = p.MaxHealth,
                    LifeTime = p.LifeTime,
                    Kills = p.Kills,
                    Shots = p.Shots,
                    DirectHits = p.DirectHits,
                    DeathReason = p.DeathReason,
                    Spotted = p.Spotted,
                    CapturePoints = p.CapturePoints,
                    IsTeamKiller = p.IsTeamKiller
                }
            });

        long lastId = -1;
        BattleReport? battle = null;
        foreach (var row in players)
        {
            if (row.BattleId != lastId)
            {
                lastId = row.BattleId;
                battle = byId.GetValueOrDefault(lastId);
            }

            if (battle is not null)
            {
                row.Player.Team = row.TeamNumber == 1 ? battle.Team1 : battle.Team2;
                row.Player.Team.Players.Add(row.Player);
            }
        }

        return byName;
    }
}
