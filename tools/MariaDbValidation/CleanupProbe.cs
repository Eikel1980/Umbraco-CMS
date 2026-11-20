using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Umbraco.Cms.Infrastructure.BackgroundJobs.Jobs.DistributedJobs;
using Umbraco.Cms.Persistence.EFCore.MariaDb;

/// <summary>Checks cleanup decisions against isolated MariaDB records.</summary>
internal static class CleanupProbe
{
    /// <summary>Executes the CMS job and verifies both removal and retention.</summary>
    internal static async Task RunAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(options => options.AddConsole().SetMinimumLevel(LogLevel.Error));
        services.AddDbContext<CleanupProbeContext>(options =>
        {
            new MariaDbMigrationProviderSetup().Setup(options, connectionString);
            options.UseOpenIddict();
        });
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore()
            .UseDbContext<CleanupProbeContext>().DisableBulkOperations());
        await using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CleanupProbeContext>();
        var creator = db.GetService<IRelationalDatabaseCreator>();
        string collationBefore = await GetDatabaseCollationAsync(connectionString);
        await creator.CreateTablesAsync();
        try
        {
            DateTime now = DateTime.UtcNow;
            var expired = new OpenIddictEntityFrameworkCoreToken
            {
                CreationDate = now.AddDays(-14), ExpirationDate = now.AddDays(-8),
                Status = OpenIddictConstants.Statuses.Valid, Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            };
            var valid = new OpenIddictEntityFrameworkCoreToken
            {
                CreationDate = now.AddDays(-14), ExpirationDate = now.AddDays(1),
                Status = OpenIddictConstants.Statuses.Valid, Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            };
            var revoked = new OpenIddictEntityFrameworkCoreAuthorization
            {
                CreationDate = now.AddDays(-14), Status = OpenIddictConstants.Statuses.Revoked,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            };
            var retained = new OpenIddictEntityFrameworkCoreAuthorization
            {
                CreationDate = now.AddDays(-14), Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            };
            db.AddRange(expired, valid, revoked, retained);
            await db.SaveChangesAsync();
            var job = ActivatorUtilities.CreateInstance<OpenIddictCleanupJob>(provider);
            await job.ExecuteAsync();
            var tokens = await db.Set<OpenIddictEntityFrameworkCoreToken>().Select(t => t.Id).ToListAsync();
            var authorizations = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Select(a => a.Id).ToListAsync();
            if (tokens is not [var tokenId] || tokenId != valid.Id ||
                authorizations is not [var authId] || authId != retained.Id)
            {
                throw new InvalidOperationException($"Unexpected cleanup result: {tokens.Count} tokens and {authorizations.Count} authorizations remain. See the cleanup log above.");
            }
        }
        finally
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            foreach (Type entity in new[] { typeof(OpenIddictEntityFrameworkCoreToken), typeof(OpenIddictEntityFrameworkCoreAuthorization), typeof(OpenIddictEntityFrameworkCoreScope), typeof(OpenIddictEntityFrameworkCoreApplication) })
            {
                string table = db.Model.FindEntityType(entity)!.GetTableName()!;
                if (!table.StartsWith(CleanupProbeContext.Prefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unexpected probe table name.");
                }
                using var command = connection.CreateCommand();
                command.CommandText = $"DROP TABLE `{table}`";
                await command.ExecuteNonQueryAsync();
            }

            // EF Core's CreateTables issues ALTER DATABASE CHARACTER SET for the model's character set, which
            // resets the database collation to that character set's default. Put the probe database back.
            string collationAfter = await GetDatabaseCollationAsync(connectionString);
            if (!string.Equals(collationBefore, collationAfter, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"NOTE: EF Core changed the database collation from {collationBefore} to {collationAfter}; restoring it.");
                using var restore = connection.CreateCommand();
                restore.CommandText = $"ALTER DATABASE COLLATE {collationBefore}";
                await restore.ExecuteNonQueryAsync();
            }
        }
        Console.WriteLine("PASS: expired token and revoked authorization pruned; valid token and permanent authorization retained.");
    }

    private static async Task<string> GetDatabaseCollationAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = DATABASE()";
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
