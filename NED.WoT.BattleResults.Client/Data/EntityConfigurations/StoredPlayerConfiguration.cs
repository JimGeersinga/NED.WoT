using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NED.WoT.BattleResults.Client.Data.Models;

namespace NED.WoT.BattleResults.Client.Data.EntityConfigurations;

public sealed class StoredPlayerConfiguration : IEntityTypeConfiguration<StoredPlayer>
{
    public void Configure(EntityTypeBuilder<StoredPlayer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Players");
    }
}
