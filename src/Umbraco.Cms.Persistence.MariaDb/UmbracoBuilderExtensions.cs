using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.DistributedLocking;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Extension methods for adding MariaDB support to Umbraco.
/// </summary>
public static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds the services Umbraco needs to use MariaDB through MySqlConnector.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IUmbracoBuilder AddUmbracoMariaDbSupport(this IUmbracoBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlSyntaxProvider, MariaDbSyntaxProvider>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBulkSqlInsertProvider, MariaDbBulkSqlInsertProvider>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDatabaseCreator, MariaDbDatabaseCreator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDatabaseProviderMetadata, MariaDbDatabaseProviderMetadata>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDistributedLockingMechanism, MariaDbDistributedLockingMechanism>());

        DbProviderFactories.UnregisterFactory(MariaDbSyntaxProvider.InvariantName);
        DbProviderFactories.RegisterFactory(MariaDbSyntaxProvider.InvariantName, MySqlConnectorFactory.Instance);

        return builder;
    }
}
