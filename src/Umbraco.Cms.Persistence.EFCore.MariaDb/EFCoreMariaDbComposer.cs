using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Persistence.EFCore.Migrations;

namespace Umbraco.Cms.Persistence.EFCore.MariaDb;

/// <summary>
/// Registers MariaDB EF Core migration services.
/// </summary>
public class EFCoreMariaDbComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IMigrationProvider, MariaDbMigrationProvider>();
        builder.Services.AddSingleton<IMigrationProviderSetup, MariaDbMigrationProviderSetup>();
        builder.Services.AddOptions<OpenIddictEntityFrameworkCoreOptions>()
            .Configure<IOptionsMonitor<ConnectionStrings>>((options, connections) =>
            {
                if (string.Equals(connections.CurrentValue.ProviderName, MariaDbMigrationProviderSetup.DatabaseProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    // MariaDB rejects the limited IN subquery used by OpenIddict's bulk prune.
                    options.DisableBulkOperations = true;
                }
            });
    }
}
