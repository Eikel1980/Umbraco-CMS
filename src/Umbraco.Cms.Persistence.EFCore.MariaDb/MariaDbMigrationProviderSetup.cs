using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using Umbraco.Cms.Persistence.EFCore.Migrations;

namespace Umbraco.Cms.Persistence.EFCore.MariaDb;

/// <summary>
/// Configures the Umbraco EF Core context for MariaDB.
/// </summary>
public class MariaDbMigrationProviderSetup : IMigrationProviderSetup
{
    /// <summary>The configured ADO.NET provider invariant name.</summary>
    internal const string DatabaseProviderName = "MySqlConnector";

    private static readonly MariaDbServerVersion _serverVersion = new(new Version(13, 0, 2));

    /// <inheritdoc />
    public string ProviderName => DatabaseProviderName;

    /// <inheritdoc />
    public void Setup(DbContextOptionsBuilder builder, string? connectionString) =>
        builder.UseMySql(connectionString, _serverVersion, options => options.MigrationsAssembly(GetType().Assembly.FullName));

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder builder)
    {
        // Keep the full foreign key and fit the lookup index within 768 utf8mb4 characters.
        builder.Entity<OpenIddictEntityFrameworkCoreToken>()
            .HasIndex("ApplicationId", "Status", "Subject", "Type")
            .HasPrefixLength(0, 0, 313, 0);
    }
}
