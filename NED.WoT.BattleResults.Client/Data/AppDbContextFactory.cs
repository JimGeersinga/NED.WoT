using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NED.WoT.BattleResults.Client.Data;

/// <summary>Used by <c>dotnet ef</c> only (FileSystem.AppDataDirectory is MAUI-only).</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;

        return new AppDbContext(options);
    }
}
