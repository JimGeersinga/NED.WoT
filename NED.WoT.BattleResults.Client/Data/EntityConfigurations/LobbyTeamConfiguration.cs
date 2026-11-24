using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Data.EntityConfigurations;

public sealed class LobbyTeamConfiguration : IEntityTypeConfiguration<LobbyTeam>
{
    public void Configure(EntityTypeBuilder<LobbyTeam> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LobbyTeams");
        builder.HasIndex(x => x.LobbyKey).IsUnique();
        builder.Property(x => x.LobbyKey).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TeamName).IsRequired().HasMaxLength(40);
    }
}
