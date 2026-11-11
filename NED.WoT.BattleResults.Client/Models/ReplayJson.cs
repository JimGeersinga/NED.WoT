using System.Text.Json;
using System.Text.Json.Serialization;

namespace NED.WoT.BattleResults.Client.Models;

// The JSON of a .wotreplay, with only the fields the app reads. Unknown fields are skipped; names are matched exactly (camelCase).
// A field that is absent stays null, so a replay with fewer fields maps to a battle with fewer stats, as before.

/// <summary>Block 0 of a replay.</summary>
public sealed record ReplayHeader
{
    /// <summary>"dd.MM.yyyy HH:mm:ss".</summary>
    public string? DateTime { get; init; }
    public string? MapDisplayName { get; init; }

    /// <summary>The players by vehicle id.</summary>
    public Dictionary<string, ReplayPlayer>? Vehicles { get; init; }
}

/// <summary>A player in the header (block 0) or in results[1].</summary>
public sealed record ReplayPlayer
{
    public string? Name { get; init; }
    public string? ClanAbbrev { get; init; }

    /// <summary>Only in results[1]; 0 for a player without a clan.</summary>
    public long? ClanDbid { get; init; }
    public int? Team { get; init; }
    public string? VehicleType { get; init; }
}

/// <summary>Block 1 of a replay: a JSON array, of which only element 0 (common and vehicles) and element 1 (the players) are read.</summary>
[JsonConverter(typeof(ReplayResultsConverter))]
public sealed record ReplayResults
{
    public ReplayCommon? Common { get; init; }

    /// <summary>The stats by vehicle id. Every value is an array; the first element is the one that counts.</summary>
    public Dictionary<string, ReplayVehicleStats[]>? Vehicles { get; init; }

    /// <summary>The players by vehicle id (results[1]).</summary>
    public Dictionary<string, ReplayPlayer>? Players { get; init; }
}

public sealed record ReplayCommon
{
    public int? FinishReason { get; init; }
    public int? Duration { get; init; }

    /// <summary>The health by team number ("1" and "2"; some modes have more keys).</summary>
    public Dictionary<string, int>? TeamHealth { get; init; }
    public int? WinnerTeam { get; init; }
}

public sealed record ReplayVehicleStats
{
    public int? DamageDealt { get; init; }
    public int? DamageReceived { get; init; }
    public int? DamageBlockedByArmor { get; init; }
    public int? Piercings { get; init; }
    public int? PiercingEnemyHits { get; init; }
    public int? DirectHitsReceived { get; init; }
    public int? Xp { get; init; }
    public int? Credits { get; init; }
    public int? Shots { get; init; }
    public int? Kills { get; init; }

    /// <summary>A JSON boolean.</summary>
    public bool IsTeamKiller { get; init; }
    public int? FlagCapture { get; init; }
    public int? Health { get; init; }
    public int? DirectHits { get; init; }
    public int? Spotted { get; init; }
    public int? LifeTime { get; init; }
    public int? MaxHealth { get; init; }
    public int? DeathReason { get; init; }
    public int? NoDamageDirectHitsReceived { get; init; }
}

/// <summary>Reads results[0] and results[1] and skips every other element. Anything but an array is a <see cref="JsonException"/>.</summary>
public sealed class ReplayResultsConverter : JsonConverter<ReplayResults>
{
    public override ReplayResults? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("The results block is not a JSON array.");
        }

        ReplayCommon? common = null;
        Dictionary<string, ReplayVehicleStats[]>? vehicles = null;
        Dictionary<string, ReplayPlayer>? players = null;

        for (int index = 0; reader.Read() && reader.TokenType != JsonTokenType.EndArray; index++)
        {
            if (index == 0 && reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("common"u8))
                    {
                        reader.Read();
                        common = JsonSerializer.Deserialize(ref reader, ReplayJsonContext.Default.ReplayCommon);
                    }
                    else if (reader.ValueTextEquals("vehicles"u8))
                    {
                        reader.Read();
                        vehicles = JsonSerializer.Deserialize(ref reader, ReplayJsonContext.Default.DictionaryStringReplayVehicleStatsArray);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
            }
            else if (index == 1 && reader.TokenType == JsonTokenType.StartObject)
            {
                players = JsonSerializer.Deserialize(ref reader, ReplayJsonContext.Default.DictionaryStringReplayPlayer);
            }
            else
            {
                reader.Skip();
            }
        }

        return new ReplayResults { Common = common, Vehicles = vehicles, Players = players };
    }

    public override void Write(Utf8JsonWriter writer, ReplayResults value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Replays are only read.");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReplayHeader))]
[JsonSerializable(typeof(ReplayResults))]
[JsonSerializable(typeof(ReplayCommon))]
[JsonSerializable(typeof(Dictionary<string, ReplayVehicleStats[]>))]
[JsonSerializable(typeof(Dictionary<string, ReplayPlayer>))]
public sealed partial class ReplayJsonContext : JsonSerializerContext;
