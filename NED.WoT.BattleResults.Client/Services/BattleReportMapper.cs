using NED.WoT.BattleResults.Client.Models;

using System.Globalization;
using System.Runtime.InteropServices;

namespace NED.WoT.BattleResults.Client.Services;

public class BattleReportMapper
{
    protected BattleReportMapper() { }

    private static CultureInfo CultureInfo => CultureInfo.InvariantCulture;

    public static BattleReport Map(ReplayHeader header, ReplayResults? results, Settings settings)
    {
        if (!DateTime.TryParseExact(header.DateTime, "dd.MM.yyyy HH:mm:ss", CultureInfo, DateTimeStyles.None, out DateTime matchStart))
        {
            throw new FormatException($"Could not parse match datetime '{header.DateTime}' with culture '{CultureInfo.Name}'. Current culture is '{CultureInfo.CurrentCulture.Name}'.");
        }

        BattleReport game = new()
        {
            MapName = MapNameResolver.GetMapName(header.MapDisplayName),
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
                    MapPlayerData(game, player, vehicle.Value is { Length: > 0 } stats ? stats[0] : null, settings);
                }
            }
        }
        else if (header.Vehicles != null)
        {
            foreach (ReplayPlayer? player in header.Vehicles.Values)
            {
                MapPlayerData(game, player, null, settings);
            }
        }

        game.Team1.Abbreviation = GetMostMentionedClanAbbreviation(game.Team1);
        game.Team2.Abbreviation = GetMostMentionedClanAbbreviation(game.Team2);
        game.Team1.ClanId = game.Team1.Players.FirstOrDefault(x => x.Clan == game.Team1.Abbreviation && x.ClanId > 0)?.ClanId;
        game.Team2.ClanId = game.Team2.Players.FirstOrDefault(x => x.Clan == game.Team2.Abbreviation && x.ClanId > 0)?.ClanId;

        game.Team1.Result = GetResult(game.Team1.IsWinner, game.Team2.IsWinner);
        game.Team2.Result = GetResult(game.Team2.IsWinner, game.Team1.IsWinner);

        game.Team1.IsOwnTeam = string.Equals(game.Team1.Abbreviation, settings.ClanAbbreviation, StringComparison.OrdinalIgnoreCase) || game.Team1.Players.Any(x => string.Equals(x.Name, settings.PlayerName, StringComparison.OrdinalIgnoreCase));
        game.Team2.IsOwnTeam = string.Equals(game.Team2.Abbreviation, settings.ClanAbbreviation, StringComparison.OrdinalIgnoreCase) || game.Team2.Players.Any(x => string.Equals(x.Name, settings.PlayerName, StringComparison.OrdinalIgnoreCase));

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

        int playerCount = Math.Max(game.Team1.Players.Count, game.Team2.Players.Count);
        EnrichAndSortPlayers(game.Team1, playerCount);
        EnrichAndSortPlayers(game.Team2, playerCount);

        return game;
    }

    internal static string GetMostMentionedClanAbbreviation(Team team)
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

    private static void MapPlayerData(BattleReport game, ReplayPlayer? playerData, ReplayVehicleStats? vehicleData, Settings settings)
    {
        string? clan = playerData?.ClanAbbrev;

        Team team = playerData?.Team == game.Team1.Number ? game.Team1 : game.Team2;

        Player player = new(team)
        {
            Name = playerData?.Name,
            Clan = clan,
            ClanId = playerData?.ClanDbid,
            Vehicle = TankNameResolver.GetTankName(playerData?.VehicleType),
            IsClanMember = string.Equals(settings.ClanAbbreviation, clan, StringComparison.OrdinalIgnoreCase)
        };

        if (vehicleData != null)
        {
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
}
