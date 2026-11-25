using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NED.WoT.BattleResults.Client.Data.Models;

namespace NED.WoT.BattleResults.Client.Data.EntityConfigurations;

public sealed class StoredBattleConfiguration : IEntityTypeConfiguration<StoredBattle>
{
    public void Configure(EntityTypeBuilder<StoredBattle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Battles");
        // Windows paths and file names don't differ by case.
        builder.Property(x => x.Folder).IsRequired().UseCollation("NOCASE");
        builder.Property(x => x.FileName).IsRequired().UseCollation("NOCASE");
        builder.HasIndex(x => new { x.Folder, x.FileName }).IsUnique();
        builder.HasIndex(x => x.MatchStart);
        builder.HasMany(x => x.Players).WithOne().HasForeignKey(x => x.BattleId).OnDelete(DeleteBehavior.Cascade);
    }
}
