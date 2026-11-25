namespace NED.WoT.BattleResults.Client.Data.Models;

/// <summary>One player of a stored battle, with the figures of the results block.</summary>
public sealed class StoredPlayer
{
    public long Id { get; set; }
    public long BattleId { get; set; }

    /// <summary>1 or 2.</summary>
    public int TeamNumber { get; set; }

    public string? Name { get; set; }
    public string? Clan { get; set; }
    public string? Vehicle { get; set; }

    public long? AccountId { get; set; }
    public long? PrebattleId { get; set; }
    public long? ClanId { get; set; }

    public int? DamageDealt { get; set; }
    public int? DamageReceived { get; set; }
    public int? DamageBlocked { get; set; }
    public int? Piercings { get; set; }
    public int? HitsReceived { get; set; }
    public int? ShotsReceived { get; set; }
    public int? ShotsBlocked { get; set; }
    public int? ExperienceEarned { get; set; }
    public int? CreditsEarned { get; set; }
    public int? Health { get; set; }
    public int? MaxHealth { get; set; }
    public int? LifeTime { get; set; }
    public int? Kills { get; set; }
    public int? Shots { get; set; }
    public int? DirectHits { get; set; }
    public int? DeathReason { get; set; }
    public int? Spotted { get; set; }
    public int? CapturePoints { get; set; }
    public bool? IsTeamKiller { get; set; }
}
