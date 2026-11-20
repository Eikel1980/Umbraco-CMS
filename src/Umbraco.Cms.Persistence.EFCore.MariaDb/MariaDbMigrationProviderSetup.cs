using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
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

    /// <summary>
    /// The version assumed when the server cannot be asked, such as at design time.
    /// MariaDB 11.8 is the oldest LTS release this provider is validated against.
    /// </summary>
    internal static readonly ServerVersion FallbackServerVersion = new MariaDbServerVersion(new Version(11, 8, 0));

    private static readonly ConcurrentDictionary<string, ServerVersion> _detectedVersions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string ProviderName => DatabaseProviderName;

    /// <inheritdoc />
    public void Setup(DbContextOptionsBuilder builder, string? connectionString) =>
        builder.UseMySql(connectionString, GetServerVersion(connectionString), options => options.MigrationsAssembly(GetType().Assembly.FullName));

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder builder)
    {
        // Keep the full foreign key and fit the lookup index within 768 utf8mb4 characters.
        builder.Entity<OpenIddictEntityFrameworkCoreToken>()
            .HasIndex("ApplicationId", "Status", "Subject", "Type")
            .HasPrefixLength(0, 0, 313, 0);
    }

    /// <summary>
    /// Gets the version of the server behind <paramref name="connectionString"/>, asking the server once per connection string.
    /// </summary>
    /// <remarks>
    /// EF Core chooses the SQL it generates from this version, so it must not claim a newer server than the real one.
    /// Detection connects without selecting the database, so it also works before the installer creates it.
    /// A failed detection is not cached: the fallback is used for that context and detection is retried next time.
    /// </remarks>
    internal static ServerVersion GetServerVersion(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return FallbackServerVersion;
        }

        if (_detectedVersions.TryGetValue(connectionString, out ServerVersion? cached))
        {
            return cached;
        }

        try
        {
            var serverOnly = new MySqlConnectionStringBuilder(connectionString) { Database = string.Empty };
            ServerVersion detected = ServerVersion.AutoDetect(serverOnly.ConnectionString);
            return _detectedVersions.GetOrAdd(connectionString, detected);
        }
        catch (Exception exception) when (exception is MySqlException or ArgumentException or InvalidOperationException)
        {
            return FallbackServerVersion;
        }
    }
}
