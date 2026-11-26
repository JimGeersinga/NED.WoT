using NED.WoT.BattleResults.Client.Data.Models;
#if DEBUG
using Microsoft.EntityFrameworkCore;

using NED.WoT.BattleResults.Client.Data;
#endif
using NED.WoT.BattleResults.Client.Models;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NED.WoT.BattleResults.Client.Services;

public class BattleReportMapper
{
    protected BattleReportMapper() { }

    private static CultureInfo CultureInfo => CultureInfo.InvariantCulture;

    /// <param name="me">The signed-in player (HQ's answer); null when unknown, then no team or player counts as yours.</param>
    public static BattleReport Map(ReplayHeader header, ReplayResults? results, HqIdentity? me)
    {
        if (!DateTime.TryParseExact(header.DateTime, "dd.MM.yyyy HH:mm:ss", CultureInfo, DateTimeStyles.None, out DateTime matchStart))
        {
            throw new FormatException($"Could not parse match datetime '{header.DateTime}' with culture '{CultureInfo.Name}'. Current culture is '{CultureInfo.CurrentCulture.Name}'.");
        }

        BattleReport game = new()
        {
            MapName = MapNameResolver.GetMapName(header.MapDisplayName),
            MapKey = header.MapName,
            RecorderName = header.PlayerName,
            ArenaUniqueId = results?.ArenaUniqueId,
            MatchStart = matchStart,
            Team1 = new Team { Number = 1 },
            Team2 = new Team { Number = 2 }
        };

        if (results != null)
        {
            ReplayCommon? common = results.Common;
            game.FinishReason = common?.FinishReason;
            game.MatchDuration = common?.Duration;
            game.Team1.Health = TeamHealth(common, game.Team1.Number);
            game.Team1.IsWinner = common?.WinnerTeam == game.Team1.Number;
            game.Team2.Health = TeamHealth(common, game.Team2.Number);
            game.Team2.IsWinner = common?.WinnerTeam == game.Team2.Number;

            if (results.Vehicles != null)
            {
                foreach (KeyValuePair<string, ReplayVehicleStats[]> vehicle in results.Vehicles)
                {
                    ReplayPlayer? player = null;
                    results.Players?.TryGetValue(vehicle.Key, out player);
                    MapPlayerData(game, player, vehicle.Value is { Length: > 0 } stats ? stats[0] : null, results);
                }
            }
        }
        else if (header.Vehicles != null)
        {
            foreach (ReplayPlayer? player in header.Vehicles.Values)
            {
                MapPlayerData(game, player, null, null);
            }
        }

        game.Team1.Abbreviation = GetMostMentionedClanAbbreviation(game.Team1);
        game.Team2.Abbreviation = GetMostMentionedClanAbbreviation(game.Team2);
        game.Team1.ClanId = ClanId(results, game.Team1);
        game.Team2.ClanId = ClanId(results, game.Team2);

        game.RecorderTeam = RecorderTeamOf(game, header.PlayerName);

        int playerCount = Math.Max(game.Team1.Players.Count, game.Team2.Players.Count);
        EnrichAndSortPlayers(game.Team1, playerCount);
        EnrichAndSortPlayers(game.Team2, playerCount);

        ApplyIdentity(game, me);

        return game;
    }

    private static Team? RecorderTeamOf(BattleReport game, string? recorderName) => recorderName is null ? null
        : game.Team1.Players.Any(x => x.Name == recorderName) ? game.Team1
        : game.Team2.Players.Any(x => x.Name == recorderName) ? game.Team2
        : null;

    /// <summary>The part of a report that depends on who is signed in: clan members, your team, the result of each team and of the battle. Needs the team abbreviations.</summary>
    public static void ApplyIdentity(BattleReport game, HqIdentity? me)
    {
        foreach (Player player in game.Team1.Players.Concat(game.Team2.Players))
        {
            player.IsClanMember = me?.ClanTag is not null && string.Equals(me.ClanTag, player.Clan, StringComparison.OrdinalIgnoreCase);
        }

        game.Team1.Result = GetResult(game.Team1.IsWinner, game.Team2.IsWinner);
        game.Team2.Result = GetResult(game.Team2.IsWinner, game.Team1.IsWinner);

        game.Team1.IsOwnTeam = IsMyTeam(game.Team1, me);
        game.Team2.IsOwnTeam = IsMyTeam(game.Team2, me);

        game.OwnTeam = game.Team1.IsOwnTeam ? game.Team1 : game.Team2.IsOwnTeam ? game.Team2 : null;

        if (game.OwnTeam?.IsWinner == true)
        {
            game.Result = Result.Win;
        }
        else if (game.Team1.IsWinner == false && game.Team2.IsWinner == false)
        {
            game.Result = Result.Draw;
        }
        else if (game.OwnTeam?.IsWinner == false)
        {
            game.Result = Result.Lose;
        }
        else
        {
            game.Result = Result.Unknown;
        }
    }

    /// <summary>The row of a replay for the database: only what the file says, nothing that depends on who is signed in. The filler players of an uneven match are left out.</summary>
    public static StoredBattle ToStored(BattleReport report, string folder, FileInfo file)
    {
        StoredBattle stored = new()
        {
            Folder = folder,
            FileName = file.Name,
            FileLength = file.Length,
            FileWriteTicks = file.LastWriteTimeUtc.Ticks,
            Unreadable = report.Error is not null,
            MatchStart = report.MatchStart
        };

        if (stored.Unreadable)
        {
            return stored;
        }

        stored.MapName = report.MapName;
        stored.MapKey = report.MapKey;
        stored.RecorderName = report.RecorderName;
        stored.ArenaUniqueId = report.ArenaUniqueId;
        stored.MatchDuration = report.MatchDuration;
        stored.FinishReason = report.FinishReason;
        stored.Team1Health = report.Team1.Health;
        stored.Team2Health = report.Team2.Health;
        stored.Team1IsWinner = report.Team1.IsWinner;
        stored.Team2IsWinner = report.Team2.IsWinner;
        stored.Team1ClanId = report.Team1.ClanId;
        stored.Team2ClanId = report.Team2.ClanId;

        foreach (Team team in new[] { report.Team1, report.Team2 })
        {
            // A filler has neither a name nor an account: it only pads the shorter team.
            foreach (Player x in team.Players.Where(x => x.Name is not null || x.AccountId is not null))
            {
                stored.Players.Add(new StoredPlayer
                {
                    TeamNumber = team.Number,
                    Name = x.Name,
                    Clan = x.Clan,
                    Vehicle = x.Vehicle,
                    AccountId = x.AccountId,
                    PrebattleId = x.PrebattleId,
                    ClanId = x.ClanId,
                    DamageDealt = x.DamageDealt,
                    DamageReceived = x.DamageReceived,
                    DamageBlocked = x.DamageBlocked,
                    Piercings = x.Piercings,
                    HitsReceived = x.HitsReceived,
                    ShotsReceived = x.ShotsReceived,
                    ShotsBlocked = x.ShotsBlocked,
                    ExperienceEarned = x.ExperienceEarned,
                    CreditsEarned = x.CreditsEarned,
                    Health = x.Health,
                    MaxHealth = x.MaxHealth,
                    LifeTime = x.LifeTime,
                    Kills = x.Kills,
                    Shots = x.Shots,
                    DirectHits = x.DirectHits,
                    DeathReason = x.DeathReason,
                    Spotted = x.Spotted,
                    CapturePoints = x.CapturePoints,
                    IsTeamKiller = x.IsTeamKiller
                });
            }
        }

        return stored;
    }

    /// <summary>The warning row of an unreadable file.</summary>
    public static BattleReport UnreadableRow(string fileName, DateTime matchStart) =>
        new() { FileName = fileName, MatchStart = matchStart, Error = BattleReportService.UnreadableMessage(fileName) };

    /// <summary>What a stored report still lacks once its fields and players are set (players in table order): abbreviations, recorder team, filler players, numbers, then the identity.</summary>
    public static void Complete(BattleReport game, HqIdentity? me)
    {
        if (game.Error is not null)
        {
            return;
        }

        game.Team1.Abbreviation = GetMostMentionedClanAbbreviation(game.Team1);
        game.Team2.Abbreviation = GetMostMentionedClanAbbreviation(game.Team2);
        game.RecorderTeam = RecorderTeamOf(game, game.RecorderName);

        int playerCount = Math.Max(game.Team1.Players.Count, game.Team2.Players.Count);
        EnrichAndSortPlayers(game.Team1, playerCount);
        EnrichAndSortPlayers(game.Team2, playerCount);

        ApplyIdentity(game, me);
    }

    /// <summary>Your team: its clan tag is yours, or one of its players has your name. Without a clan only the name matches.</summary>
    private static bool IsMyTeam(Team team, HqIdentity? me) =>
        me is not null
        && ((me.ClanTag is not null && string.Equals(team.Abbreviation, me.ClanTag, StringComparison.OrdinalIgnoreCase))
            || team.Players.Any(x => string.Equals(x.Name, me.Nickname, StringComparison.OrdinalIgnoreCase)));

    private static long? ClanId(ReplayResults? results, Team team) =>
        results?.AccountPlayers?.Values.FirstOrDefault(x => x.ClanDbid > 0 && x.Team == team.Number && x.ClanAbbrev == team.Abbreviation)?.ClanDbid;

    private static string GetMostMentionedClanAbbreviation(Team team)
    {
        if (team.Players == null || team.Players.Count == 0)
        {
            return "?";
        }

        Dictionary<string, int> clanCounts = [];
        string mostMentionedClan = "?";
        int maxMentions = 0;
        int mentionThreshold = Math.Max(team.Players.Count / 2, 4);

        foreach (var player in team.Players)
        {
            if (string.IsNullOrEmpty(player.Clan))
            {
                continue;
            }

            int count = clanCounts.GetValueOrDefault(player.Clan) + 1;
            clanCounts[player.Clan] = count;

            if (count > maxMentions)
            {
                mostMentionedClan = player.Clan;
                maxMentions = count;
            }
        }

        return maxMentions >= mentionThreshold ? mostMentionedClan : "?";
    }

    private static void EnrichAndSortPlayers(Team team, int playerCount)
    {
        // One new object per empty slot: a shared object would be renumbered by every slot and end up with the last number.
        for (int i = team.Players.Count; i < playerCount; i++)
        {
            team.Players.Add(new Player(team));
        }

        Span<Player> playersSpan = CollectionsMarshal.AsSpan(team.Players);
        playersSpan.Sort((p1, p2) => p2.ExperienceEarned.GetValueOrDefault().CompareTo(p1.ExperienceEarned.GetValueOrDefault()));

        for (int i = 0; i < playersSpan.Length; i++)
        {
            playersSpan[i].Number = i + 1;
        }
    }

    private static int? TeamHealth(ReplayCommon? common, int team) =>
        common?.TeamHealth is { } health && health.TryGetValue(team.ToString(CultureInfo), out int value) ? value : null;

    private static void MapPlayerData(BattleReport game, ReplayPlayer? playerData, ReplayVehicleStats? vehicleData, ReplayResults? results)
    {
        string? clan = playerData?.ClanAbbrev;

        Team team = playerData?.Team == game.Team1.Number ? game.Team1 : game.Team2;

        Player player = new(team)
        {
            Name = playerData?.Name,
            Clan = clan,
            Vehicle = TankNameResolver.GetTankName(playerData?.VehicleType)
        };

        if (vehicleData != null)
        {
            player.AccountId = vehicleData.AccountDbId;
            if (vehicleData.AccountDbId is { } accountId && results?.AccountPlayers?.TryGetValue(accountId.ToString(CultureInfo), out ReplayPlayer? account) == true)
            {
                player.PrebattleId = account.PrebattleId is > 0 ? account.PrebattleId : null;
                player.ClanId = account.ClanDbid is > 0 ? account.ClanDbid : null;
            }

            player.DamageDealt = vehicleData.DamageDealt;
            player.DamageReceived = vehicleData.DamageReceived;
            player.DamageBlocked = vehicleData.DamageBlockedByArmor;
            player.Piercings = vehicleData.Piercings;
            player.HitsReceived = vehicleData.PiercingEnemyHits;
            player.ShotsReceived = vehicleData.DirectHitsReceived;
            player.ExperienceEarned = vehicleData.Xp;
            player.CreditsEarned = vehicleData.Credits;
            player.Shots = vehicleData.Shots;
            player.Kills = vehicleData.Kills;
            player.IsTeamKiller = vehicleData.IsTeamKiller;
            player.CapturePoints = vehicleData.FlagCapture;
            player.Health = vehicleData.Health;
            player.DirectHits = vehicleData.DirectHits;
            player.Spotted = vehicleData.Spotted;
            player.LifeTime = vehicleData.LifeTime;
            player.MaxHealth = vehicleData.MaxHealth;
            player.DeathReason = vehicleData.DeathReason;
            player.ShotsBlocked = vehicleData.NoDamageDirectHitsReceived;
        }

        team.Players.Add(player);
    }

    private static Result GetResult(bool? team1IsWinner, bool? team2IsWinner)
    {
        if (team1IsWinner == true)
        {
            return Result.Win;
        }
        else if (team2IsWinner == true)
        {
            return Result.Lose;
        }
        else if (team1IsWinner == false && team2IsWinner == false)
        {
            return Result.Draw;
        }

        return Result.Unknown;
    }

#if DEBUG
    internal static HqIdentity SelfCheckIdentity(string tag) => new(1, "Ghost", null, tag, null, null, ClanRole.Unknown, true);

    /// <summary>Stores reports in a migrated temp database and reads them back with <see cref="StoredBattleReader"/>: each comes back equal, and the identity is applied on load.</summary>
    internal static async Task SelfCheckAsync(DbContextOptions<AppDbContext> options)
    {
        const string FOLDER = @"C:\self";
        FileInfo file = new(Path.GetTempFileName());
        try
        {
            // 13 against 15 with results (fillers), the same without results (header only), each with and without an identity.
            string Slots(int count, int firstId) => string.Join(",", Enumerable.Range(firstId, count).Select(x => $"\"{x}\":[{{\"accountDBID\":{x},\"xp\":{100 + x % 5},\"damageDealt\":{x * 10},\"deathReason\":{(x % 2 == 0 ? -1 : 0)},\"isTeamKiller\":false}}]"));
            string Named(int team, int count, int firstId) => string.Join(",", Enumerable.Range(firstId, count).Select(x => $"\"{x}\":{{\"name\":\"P{x}\",\"team\":{team},\"clanAbbrev\":\"{(team == 1 ? "NED" : "ABC")}\"}}"));
            byte[] json = System.Text.Encoding.UTF8.GetBytes($"[{{\"arenaUniqueID\":7,\"common\":{{\"winnerTeam\":1,\"duration\":420,\"teamHealth\":{{\"1\":3,\"2\":0}}}},\"vehicles\":{{{Slots(13, 1)},{Slots(15, 14)}}}}},{{{Named(1, 13, 1)},{Named(2, 15, 14)}}}]");
            ReplayResults? results = System.Text.Json.JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplayResults);
            ReplayHeader header = new()
            {
                DateTime = "08.12.2023 22:50:00",
                MapName = "01_karelia",
                PlayerName = "P14",
                Vehicles = Enumerable.Range(1, 3).ToDictionary(x => x.ToString(CultureInfo), x => new ReplayPlayer { Name = $"H{x}", Team = x % 2 + 1, ClanAbbrev = "NED" })
            };

            List<(string Name, BattleReport Before, HqIdentity? Me)> cases = [];
            foreach (ReplayResults? source in new[] { results, null })
            {
                foreach (HqIdentity? me in new[] { null, SelfCheckIdentity("NED"), SelfCheckIdentity("ABC") })
                {
                    cases.Add(($"case{cases.Count}.wotreplay", Map(header, source, me), me));
                }
            }

            cases.Add(("identity.wotreplay", Map(header, results, null), null));
            cases.Add(("broken.wotreplay", UnreadableRow("broken.wotreplay", new DateTime(2026, 4, 5, 21, 30, 0)), null));

            await using (AppDbContext db = new(options))
            {
                foreach ((string name, BattleReport before, _) in cases)
                {
                    StoredBattle stored = ToStored(before, FOLDER, file);
                    stored.FileName = name;
                    db.Battles.Add(stored);
                }

                await db.SaveChangesAsync();
            }

            await using (AppDbContext db = new(options))
            {
                Dictionary<string, StoredBattleReader.Entry> read = StoredBattleReader.Read(db, FOLDER);
                foreach ((string name, BattleReport before, HqIdentity? me) in cases)
                {
                    BattleReport after = read[name].Report;
                    Complete(after, me);
                    Debug.Assert(after.Error == before.Error && after.MatchStart == before.MatchStart && after.FileName == name, "an unreadable file comes back as its warning row, a readable one without error");
                    Debug.Assert(before.Error is not null || Same(before, after), "a stored report comes back equal");
                    Debug.Assert(after.Team1.Players.Count == before.Team1.Players.Count && after.Team2.Players.Count == before.Team2.Players.Count, "the fillers come back");
                }
            }

            // The identity is applied on load: the same stored row follows a changed clan tag, with nothing rebuilt.
            foreach ((HqIdentity? me, Result expected) in new (HqIdentity?, Result)[] { (SelfCheckIdentity("NED"), Result.Win), (SelfCheckIdentity("ABC"), Result.Lose), (null, Result.Unknown) })
            {
                await using AppDbContext db = new(options);
                BattleReport after = StoredBattleReader.Read(db, FOLDER)["identity.wotreplay"].Report;
                Complete(after, me);
                Debug.Assert(after.Result == expected && (me is not null || after.OwnTeam is null), "a changed clan tag changes own team and result");
            }
        }
        finally
        {
            file.Delete();
        }
    }

    internal static bool Same(BattleReport a, BattleReport b)
    {
        static bool SamePlayer(Player x, Player y) =>
            (x.Number, x.Name, x.Clan, x.Vehicle, x.AccountId, x.PrebattleId, x.ClanId, x.IsClanMember, x.DamageDealt, x.DamageReceived, x.DamageBlocked, x.Piercings, x.HitsReceived, x.ShotsReceived, x.ShotsBlocked)
            == (y.Number, y.Name, y.Clan, y.Vehicle, y.AccountId, y.PrebattleId, y.ClanId, y.IsClanMember, y.DamageDealt, y.DamageReceived, y.DamageBlocked, y.Piercings, y.HitsReceived, y.ShotsReceived, y.ShotsBlocked)
            && (x.ExperienceEarned, x.CreditsEarned, x.Health, x.MaxHealth, x.LifeTime, x.Kills, x.Shots, x.DirectHits, x.DeathReason, x.Spotted, x.CapturePoints, x.IsTeamKiller)
            == (y.ExperienceEarned, y.CreditsEarned, y.Health, y.MaxHealth, y.LifeTime, y.Kills, y.Shots, y.DirectHits, y.DeathReason, y.Spotted, y.CapturePoints, y.IsTeamKiller);

        static bool SameTeam(Team x, Team y) =>
            (x.Number, x.Abbreviation, x.ClanId, x.Health, x.IsWinner, x.IsOwnTeam, x.Result) == (y.Number, y.Abbreviation, y.ClanId, y.Health, y.IsWinner, y.IsOwnTeam, y.Result)
            && x.Players.Count == y.Players.Count && x.Players.Zip(y.Players).All(p => SamePlayer(p.First, p.Second));

        return (a.MapName, a.MapKey, a.RecorderName, a.ArenaUniqueId, a.MatchStart, a.MatchDuration, a.FinishReason, a.Result, a.RecorderTeam?.Number, a.OwnTeam?.Number)
            == (b.MapName, b.MapKey, b.RecorderName, b.ArenaUniqueId, b.MatchStart, b.MatchDuration, b.FinishReason, b.Result, b.RecorderTeam?.Number, b.OwnTeam?.Number)
            && SameTeam(a.Team1, b.Team1) && SameTeam(a.Team2, b.Team2);
    }
#endif
}
