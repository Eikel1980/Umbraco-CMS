using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Persistence.EFCore.MariaDb;

/// <summary>Isolates each validation run in its own set of OpenIddict tables.</summary>
internal sealed class CleanupProbeContext(DbContextOptions<CleanupProbeContext> options) : DbContext(options)
{
    /// <summary>Gets the table prefix unique to this process.</summary>
    internal static string Prefix { get; } = $"probe{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName(Prefix + entity.GetTableName());
        }

        new MariaDbMigrationProviderSetup().ConfigureModel(builder);
    }
}
