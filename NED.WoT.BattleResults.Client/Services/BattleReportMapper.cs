using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

using NED.WoT.BattleResults.Client.Models;
using NED.WoT.BattleResults.Client.Services;

namespace NED.WoT.BattleResults.Client.Data;

public class BattleReportMapper
{
    protected BattleReportMapper() { }

    private static CultureInfo CultureInfo => CultureInfo.InvariantCulture;

    public static BattleReport Map(JsonObject replay, JsonArray stats, Settings settings)
    {
        if (!DateTime.TryParseExact(replay["dateTime"].GetValue<string>(), "dd.MM.yyyy HH:mm:ss", CultureInfo, DateTimeStyles.None, out DateTime matchStart))
        {
            throw new Exception($"Could not parse match datetime '{replay["dateTime"].GetValue<string>()}' with culture '{CultureInfo.Name}'. Current culture is '{CultureInfo.CurrentCulture.Name}'.");
        }

        BattleReport game = new()
        {
            MapName = MapNameResolver.GetMapName(replay["mapDisplayName"].GetValue<string>()),
            MatchStart = matchStart,
            Team1 = new Team { Number = 1 },
            Team2 = new Team { Number = 2 }
        };

        if (stats != null)
        {
            JsonNode commonStats = stats[0]["common"];
            game.FinishReason = commonStats["finishReason"].GetValue<int>();
            game.MatchDuration = commonStats["duration"].GetValue<int>();
            game.Team1.Health = commonStats["teamHealth"]["1"].GetValue<int>();
            game.Team1.IsWinner = commonStats["winnerTeam"].GetValue<int>() == game.Team1.Number;
            game.Team2.Health = commonStats["teamHealth"]["2"].GetValue<int>();
            game.Team2.IsWinner = commonStats["winnerTeam"].GetValue<int>() == game.Team2.Number;

            foreach (KeyValuePair<string, JsonNode> vehicle in stats[0]["vehicles"].AsObject())
            {
                MapPlayerData(game, stats[1][vehicle.Key].AsObject(), vehicle.Value.AsArray()[0].AsObject(), settings);
            }
        }
        else
        {
            foreach (KeyValuePair<string, JsonNode> vehicle in replay["vehicles"].AsObject())
            {
                MapPlayerData(game, vehicle.Value.AsObject(), stats?[0]?["vehicles"]?.AsObject()?[vehicle.Key]?[0]?.AsObject(), settings);
            }
        }

        game.Team1.Abbreviation = GetMostMentionedClanAbbreviation(game.Team1);
        game.Team2.Abbreviation = GetMostMentionedClanAbbreviation(game.Team2);

        game.Team1.Result = GetResult(game.Team1.IsWinner, game.Team2.IsWinner);
        game.Team2.Result = GetResult(game.Team2.IsWinner, game.Team1.IsWinner);

        int playerCount = Math.Max(game.Team1.Players.Count, game.Team2.Players.Count);
        EnrichAndSortPlayers(game.Team1, playerCount);
        EnrichAndSortPlayers(game.Team2, playerCount);

        return game;
    }

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
        int playersToAdd = playerCount - team.Players.Count;
        if (playersToAdd > 0)
        {
            team.Players.AddRange(Enumerable.Repeat(new Player(), playersToAdd));
        }

        Span<Player> playersSpan = CollectionsMarshal.AsSpan(team.Players);
        playersSpan.Sort((p1, p2) => p2.ExperienceEarned.GetValueOrDefault().CompareTo(p1.ExperienceEarned.GetValueOrDefault()));

        for (int i = 0; i < playersSpan.Length; i++)
        {
            playersSpan[i].Number = i + 1;
        }
    }

    private static void MapPlayerData(BattleReport game, JsonObject playerData, JsonObject vehicleData, Settings settings)
    {
        string clan = playerData["clanAbbrev"].GetValue<string>();

        Team team = playerData["team"].GetValue<int>() == game.Team1.Number ? game.Team1 : game.Team2;

        Player player = new()
        {
            Name = playerData["name"].GetValue<string>(),
            Clan = clan,
            Vehicle = TankNameResolver.GetTankName(playerData["vehicleType"].GetValue<string>()),
            IsClanMember = settings.ClanAbbreviation?.ToLower() == clan?.ToLower()
        };

        if (vehicleData != null)
        {
            player.DamageDealt = vehicleData["damageDealt"]?.GetValue<int>();
            player.DamageReceived = vehicleData["damageReceived"]?.GetValue<int>();
            player.DamageBlocked = vehicleData["damageBlockedByArmor"]?.GetValue<int>();
            player.Piercings = vehicleData["piercings"]?.GetValue<int>();
            player.ExperienceEarned = vehicleData["xp"]?.GetValue<int>();
            player.CreditsEarned = vehicleData["credits"]?.GetValue<int>();
            player.Shots = vehicleData["shots"]?.GetValue<int>();
            player.Kills = vehicleData["kills"]?.GetValue<int>();
            player.IsTeamKiller = vehicleData["isTeamKiller"]?.ToString() == "1";
            player.CapturePoints = vehicleData["flagCapture"]?.GetValue<int>();
            player.Health = vehicleData["health"]?.GetValue<int>();
            player.DirectHits = vehicleData["directHits"]?.GetValue<int>();
            player.Spotted = vehicleData["spotted"]?.GetValue<int>();
            player.LifeTime = vehicleData["lifeTime"]?.GetValue<int>();
            player.MaxHealth = vehicleData["maxHealth"]?.GetValue<int>();
            player.DeathReason = vehicleData["deathReason"]?.GetValue<int>();
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

        return Result.Unkown;
    }
}
