using NED.WoT.BattleResults.Client.Models;
using NED.WoT.BattleResults.Client.Services;

using System.Text.Json.Nodes;

namespace NED.WoT.BattleResults.Client.Data
{
    public class BattleReportMapper
    {
        protected BattleReportMapper() { }

        public static BattleReport Map(JsonObject replay, JsonArray stats)
        {
            var datetime = replay["dateTime"].GetValue<string>();
            var game = new BattleReport
            {
                MapName = replay["mapDisplayName"].GetValue<string>(),
                MatchStart = DateTime.Parse(datetime.Replace('.', '/')),
                Team1 = new Team()
                {
                    Number = 1
                },
                Team2 = new Team()
                {
                    Number = 2
                }
            };

            if (stats != null)
            {
                game.FinishReason = stats[0]["common"]["finishReason"].GetValue<int>();
                game.MatchDuration = stats[0]["common"]["duration"].GetValue<int>();
                game.Team1.Health = stats[0]["common"]["teamHealth"]["1"].GetValue<int>();
                game.Team1.IsWinner = stats[0]["common"]["winnerTeam"].GetValue<int>() == game.Team1.Number;
                game.Team2.Health = stats[0]["common"]["teamHealth"]["2"].GetValue<int>();
                game.Team2.IsWinner = stats[0]["common"]["winnerTeam"].GetValue<int>() == game.Team2.Number;

                foreach (var vehicle in stats[0]["vehicles"].AsObject())
                {
                    var vehicleData = vehicle.Value.AsArray()[0].AsObject();
                    var playerData = stats[1][vehicle.Key].AsObject();
                    MapPlayerData(game, playerData, vehicleData);
                }
            }
            else
            {
                foreach (var vehicle in replay["vehicles"].AsObject())
                {
                    var playerData = vehicle.Value.AsObject();
                    var vehicleData = stats?[0]?["vehicles"]?.AsObject()?[vehicle.Key]?[0]?.AsObject();
                    MapPlayerData(game, playerData, vehicleData);
                }
            }

            game.Team1.Result = GetResult(game.Team1.IsWinner, game.Team2.IsWinner);
            game.Team2.Result = GetResult(game.Team2.IsWinner, game.Team1.IsWinner);

            int playerCount = Math.Max(game.Team1.Players.Count, game.Team2.Players.Count);
            EnrichAndSortPlayers(game.Team1, playerCount);
            EnrichAndSortPlayers(game.Team2, playerCount);

            return game;
        }

        private static void EnrichAndSortPlayers(Team team, int playerCount)
        {
            var currentTeamPlayerCount = team.Players.Count;
            for (int i = 0; i < playerCount - currentTeamPlayerCount; i++)
            {
                team.Players.Add(new Player());
            }

            team.Players = team.Players.OrderByDescending(p => p.ExperienceEarned).ToList();
            for (int i = 0; i < team.Players.Count; i++)
            {
                team.Players[i].Number = i + 1;
            }
        }

        private static void MapPlayerData(BattleReport game, JsonObject playerData, JsonObject vehicleData)
        {
            var clan = playerData["clanAbbrev"].GetValue<string>();

            var team = playerData["team"].GetValue<int>() == game.Team1.Number ? game.Team1 : game.Team2;
            team.Abbreviation = team.Abbreviation == null || team.Abbreviation == clan ? clan : "?";

            var player = new Player()
            {
                Name = playerData["name"].GetValue<string>(),
                Clan = clan,
                Vehicle = TankNameResolver.GetTankName(playerData["vehicleType"].GetValue<string>()),
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
}
