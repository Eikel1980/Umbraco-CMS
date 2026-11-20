using System.Runtime.Serialization;
using MySqlConnector;
using Umbraco.Cms.Core.Install.Models;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Describes MariaDB to the Umbraco installer.
/// </summary>
[DataContract]
public class MariaDbDatabaseProviderMetadata : IDatabaseProviderMetadata
{
    /// <inheritdoc />
    public Guid Id => new("7d0c1f36-3b0e-4f2a-9b51-8a3e6c2d4f10");

    /// <inheritdoc />
    public int SortOrder => 3;

    /// <inheritdoc />
    public string DisplayName => "MariaDB";

    /// <inheritdoc />
    public string DefaultDatabaseName => string.Empty;

    /// <inheritdoc />
    public string ProviderName => MariaDbSyntaxProvider.InvariantName;

    /// <inheritdoc />
    public bool SupportsQuickInstall => false;

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public bool RequiresServer => true;

    /// <inheritdoc />
    public string ServerPlaceholder => "localhost";

    /// <inheritdoc />
    public bool RequiresCredentials => true;

    /// <inheritdoc />
    public bool SupportsIntegratedAuthentication => false;

    /// <inheritdoc />
    public bool SupportsTrustServerCertificate => false;

    /// <inheritdoc />
    public bool RequiresConnectionTest => true;

    /// <inheritdoc />
    /// <remarks>
    /// Not forced: if a production server is briefly unreachable, Umbraco should report that it
    /// cannot connect rather than switch to installing a missing database.
    /// </remarks>
    public bool ForceCreateDatabase => false;

    /// <inheritdoc />
    public bool CanRecognizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            return !string.IsNullOrEmpty(new MySqlConnectionStringBuilder(connectionString).Server);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string GenerateConnectionString(DatabaseModel databaseModel)
        => new MySqlConnectionStringBuilder
        {
            Server = databaseModel.Server,
            Database = databaseModel.DatabaseName,
            UserID = databaseModel.Login ?? string.Empty,
            Password = databaseModel.Password ?? string.Empty,
            GuidFormat = MySqlGuidFormat.Char36,
        }.ConnectionString;
}
