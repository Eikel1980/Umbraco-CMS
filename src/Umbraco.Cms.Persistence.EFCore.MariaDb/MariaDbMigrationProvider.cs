using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Persistence.EFCore.Migrations;

namespace Umbraco.Cms.Persistence.EFCore.MariaDb;

/// <summary>
/// Applies MariaDB migrations for the Umbraco EF Core context.
/// </summary>
public class MariaDbMigrationProvider : IMigrationProvider
{
    private readonly IDbContextFactory<UmbracoDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MariaDbMigrationProvider"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The factory for creating Umbraco database contexts.</param>
    public MariaDbMigrationProvider(IDbContextFactory<UmbracoDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    /// <inheritdoc />
    public string ProviderName => MariaDbMigrationProviderSetup.DatabaseProviderName;

    /// <inheritdoc />
    /// <remarks>
    /// MariaDB support starts after every historical Umbraco EF Core migration, so its migration set
    /// is cumulative. Any requested migration is satisfied by applying all pending MariaDB migrations.
    /// </remarks>
    public Task MigrateAsync(EFCoreMigration migration) => MigrateAllAsync();

    /// <inheritdoc />
    public async Task MigrateAllAsync()
    {
        await using UmbracoDbContext context = await _dbContextFactory.CreateDbContextAsync();

        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Cannot migrate MariaDB while a transaction is active.");
        }

        await context.Database.MigrateAsync();
    }
}
