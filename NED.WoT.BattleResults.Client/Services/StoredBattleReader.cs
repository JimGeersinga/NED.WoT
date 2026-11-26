using Dapper;

using Microsoft.EntityFrameworkCore;

using NED.WoT.BattleResults.Client.Data;
using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Reads the stored battles of a folder with two queries, straight into <see cref="BattleReport"/> and <see cref="Player"/> (Dapper fills them by column name;
/// there is no copy from an entity). EF materialising <c>StoredBattle</c> and <c>StoredPlayer</c> cost about twice as much on the warm load.
/// A new column needs no change here when <see cref="Player"/> has a property of that name; a battle column that is not on <see cref="BattleReport"/> goes on <see cref="Row"/>.
/// </summary>
public static class StoredBattleReader
{
    /// <summary>A stored file: what the cache check compares, and its report. The report still needs <see cref="BattleReportMapper.Complete"/> (unless unreadable).</summary>
    public sealed record Entry(long Id, long Length, long WriteTicks, BattleReport Report);

    /// <summary>The columns of Battles that are not a property of <see cref="BattleReport"/>: the file check, and the figures of the two teams.</summary>
    private sealed class Row : BattleReport
    {
        public Row()
        {
            Team1 = new Team { Number = 1 };
            Team2 = new Team { Number = 2 };
        }

        public long Id { get; set; }
        public long FileLength { get; set; }
        public long FileWriteTicks { get; set; }
        public bool Unreadable { get; set; }

        public int? Team1Health { get => Team1.Health; set => Team1.Health = value; }
        public int? Team2Health { get => Team2.Health; set => Team2.Health = value; }
        public bool? Team1IsWinner { get => Team1.IsWinner; set => Team1.IsWinner = value; }
        public bool? Team2IsWinner { get => Team2.IsWinner; set => Team2.IsWinner = value; }
        public long? Team1ClanId { get => Team1.ClanId; set => Team1.ClanId = value; }
        public long? Team2ClanId { get => Team2.ClanId; set => Team2.ClanId = value; }
    }

    /// <summary>Players have the columns of Players as properties, plus the battle they belong to (<see cref="Player.TeamNumber"/> is the team).</summary>
    private sealed class PlayerRow : Player
    {
        public PlayerRow() : base(null!)
        {
        }

        public long BattleId { get; set; }
    }

    /// <returns>The stored files of <paramref name="folder"/> by file name (case-insensitive).</returns>
    public static Dictionary<string, Entry> Read(AppDbContext db, string folder)
    {
        Dictionary<long, Row> byId = [];
        Dictionary<string, Entry> byName = new(StringComparer.OrdinalIgnoreCase);

        db.Database.OpenConnection();
        try
        {
            System.Data.Common.DbConnection connection = db.Database.GetDbConnection();

            foreach (Row row in connection.Query<Row>("SELECT * FROM Battles WHERE Folder = @folder", new { folder }))
            {
                if (row.Unreadable)
                {
                    row.Error = BattleReportService.UnreadableMessage(row.FileName!);
                }

                byId[row.Id] = row;
                byName[row.FileName!] = new Entry(row.Id, row.FileLength, row.FileWriteTicks, row);
            }

            // In insert order (= table order) per battle: Complete relies on it for Number.
            long lastId = -1;
            Row? battle = null;
            foreach (PlayerRow player in connection.Query<PlayerRow>("SELECT p.* FROM Players p JOIN Battles b ON b.Id = p.BattleId WHERE b.Folder = @folder ORDER BY p.BattleId, p.Id", new { folder }, buffered: false))
            {
                if (player.BattleId != lastId)
                {
                    lastId = player.BattleId;
                    battle = byId.GetValueOrDefault(lastId);
                }

                if (battle is not null)
                {
                    player.Team = player.TeamNumber == 1 ? battle.Team1 : battle.Team2;
                    player.Team.Players.Add(player);
                }
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }

        return byName;
    }
}
