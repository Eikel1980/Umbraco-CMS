using MySqlConnector;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Creates a missing MariaDB database with the character set and collation Umbraco expects.
/// </summary>
public class MariaDbDatabaseCreator : IDatabaseCreator
{
    /// <inheritdoc />
    public string ProviderName => MariaDbSyntaxProvider.InvariantName;

    /// <inheritdoc />
    public void Create(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("The MariaDB connection string does not name a database.");
        }

        builder.Database = string.Empty;
        using var connection = new MySqlConnection(builder.ConnectionString);
        connection.Open();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = GetCreateDatabaseSql(database);
        command.ExecuteNonQuery();
    }

    /// <summary>Builds the statement that creates <paramref name="database"/> if it does not exist.</summary>
    internal static string GetCreateDatabaseSql(string database)
        => $"CREATE DATABASE IF NOT EXISTS {MariaDbSyntaxProvider.QuoteIdentifier(database)} " +
           $"CHARACTER SET utf8mb4 COLLATE {MariaDbSyntaxProvider.DefaultCollation}";
}
