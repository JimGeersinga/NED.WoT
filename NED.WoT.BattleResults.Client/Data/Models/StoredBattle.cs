namespace NED.WoT.BattleResults.Client.Data.Models;

/// <summary>One replay file of a folder, read once: its battle (identity-free, see <c>BattleReportMapper.ToStored</c>), or a mark that the file can't be read. The history of the app: a deleted replay keeps its row.</summary>
public sealed class StoredBattle
{
    public long Id { get; set; }

    /// <summary>The full path of the replay folder; compared case-insensitively.</summary>
    public string Folder { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>Length and last-write time (UTC ticks) of the file as it was read: a file that differs is read again.</summary>
    public long FileLength { get; set; }
    public long FileWriteTicks { get; set; }

    /// <summary>The file could not be read: no battle, only the warning row.</summary>
    public bool Unreadable { get; set; }

    public DateTime MatchStart { get; set; }
    public string? MapName { get; set; }
    public string? MapKey { get; set; }
    public string? RecorderName { get; set; }
    public long? ArenaUniqueId { get; set; }
    public int? MatchDuration { get; set; }
    public int? FinishReason { get; set; }

    public int? Team1Health { get; set; }
    public int? Team2Health { get; set; }
    public bool? Team1IsWinner { get; set; }
    public bool? Team2IsWinner { get; set; }
    public long? Team1ClanId { get; set; }
    public long? Team2ClanId { get; set; }

    /// <summary>In the order of the table (by experience, per team); the filler rows of an uneven match are not stored.</summary>
    public List<StoredPlayer> Players { get; set; } = [];
}
