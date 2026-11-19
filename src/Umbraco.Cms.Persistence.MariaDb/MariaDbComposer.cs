using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.DistributedLocking;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Registers MariaDB CMS persistence services.
/// </summary>
public class MariaDbComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlSyntaxProvider, MariaDbSyntaxProvider>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBulkSqlInsertProvider, MariaDbBulkSqlInsertProvider>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDistributedLockingMechanism, MariaDbDistributedLockingMechanism>());
        DbProviderFactories.RegisterFactory(MariaDbSyntaxProvider.InvariantName, MySqlConnectorFactory.Instance);
    }
}
