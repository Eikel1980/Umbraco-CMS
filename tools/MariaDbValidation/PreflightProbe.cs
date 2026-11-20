using MySqlConnector;

/// <summary>
/// Read-only checks of a MariaDB database's version, character set and collation.
/// Run it against a copy, or the live database, before an Umbraco install or upgrade.
/// </summary>
internal static class PreflightProbe
{
    /// <summary>Runs the checks and returns 0 when all pass, otherwise 1.</summary>
    public static async Task<int> RunAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var failed = false;

        var version = await ScalarAsync(connection, "SELECT VERSION()");
        Version? parsed = ParseVersion(version);
        Console.WriteLine($"Server version:      {version}");
        if (!version.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
        {
            failed |= Fail("The server is not MariaDB. This provider is only validated against MariaDB.");
        }
        else if (parsed is null || parsed < new Version(10, 10))
        {
            failed |= Fail("MariaDB 10.10 or later is needed for the utf8mb4_uca1400 collations.");
        }

        var database = await ScalarAsync(connection, "SELECT DATABASE()");
        var charset = await ScalarAsync(connection, "SELECT @@character_set_database");
        var collation = await ScalarAsync(connection, "SELECT @@collation_database");
        Console.WriteLine($"Database:            {database}");
        Console.WriteLine($"Default charset:     {charset}");
        Console.WriteLine($"Default collation:   {collation}");

        if (!string.Equals(charset, "utf8mb4", StringComparison.OrdinalIgnoreCase))
        {
            failed |= Fail("The database default character set should be utf8mb4.");
        }

        if (!collation.Contains("_as_", StringComparison.OrdinalIgnoreCase))
        {
            failed |= Fail($"'{collation}' treats accented and unaccented letters as equal, so values like 'cafe' and 'café' collide in unique indexes. Use utf8mb4_uca1400_as_ci.");
        }

        List<string> mixed = await ListAsync(
            connection,
            """
            SELECT CONCAT(TABLE_NAME, ' (', TABLE_COLLATION, ')') FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' AND TABLE_COLLATION <> @@collation_database
            ORDER BY TABLE_NAME
            """);
        if (mixed.Count > 0)
        {
            failed |= Fail($"{mixed.Count} table(s) differ from the database collation. Joining their text columns with newer tables can fail with 'Illegal mix of collations': {string.Join(", ", mixed.Take(10))}{(mixed.Count > 10 ? ", ..." : string.Empty)}");
        }
        else
        {
            Console.WriteLine("PASS: every table uses the database collation.");
        }

        List<string> narrow = await ListAsync(
            connection,
            """
            SELECT CONCAT(TABLE_NAME, '.', COLUMN_NAME, ' (', CHARACTER_SET_NAME, ')') FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND CHARACTER_SET_NAME IS NOT NULL AND CHARACTER_SET_NAME <> 'utf8mb4'
            ORDER BY TABLE_NAME, COLUMN_NAME
            """);
        if (narrow.Count > 0)
        {
            failed |= Fail($"{narrow.Count} text column(s) are not utf8mb4 and cannot store emoji: {string.Join(", ", narrow.Take(10))}{(narrow.Count > 10 ? ", ..." : string.Empty)}");
        }
        else
        {
            Console.WriteLine("PASS: every text column is utf8mb4.");
        }

        failed |= !await CheckAccentSensitivityAsync(connection);

        var hasKeyValue = await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'umbracoKeyValue'") != "0";
        Console.WriteLine(hasKeyValue
            ? $"Umbraco upgrade state: {await ScalarAsync(connection, "SELECT COALESCE(MAX(value), '(none)') FROM umbracoKeyValue WHERE `key` = 'Umbraco.Core.Upgrader.State+Umbraco.Core'")}"
            : "Umbraco upgrade state: not installed");

        Console.WriteLine(failed ? "PREFLIGHT FAILED: see the messages above." : "PREFLIGHT PASSED.");
        return failed ? 1 : 0;
    }

    private static async Task<bool> CheckAccentSensitivityAsync(MySqlConnection connection)
    {
        // A temporary table lives only in this session, so this leaves the database unchanged.
        try
        {
            await ExecuteAsync(connection, "CREATE TEMPORARY TABLE preflightAccentProbe (value VARCHAR(10) NOT NULL, UNIQUE KEY (value))");
        }
        catch (MySqlException exception)
        {
            Console.WriteLine($"SKIP: could not create a temporary table to test unique keys ({exception.Message}).");
            return true;
        }

        try
        {
            await ExecuteAsync(connection, "INSERT INTO preflightAccentProbe (value) VALUES ('cafe')");
            try
            {
                await ExecuteAsync(connection, "INSERT INTO preflightAccentProbe (value) VALUES ('café')");
            }
            catch (MySqlException exception) when (exception.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                Fail("A unique key rejected 'café' as a duplicate of 'cafe' under the database collation.");
                return false;
            }

            Console.WriteLine("PASS: unique keys keep 'cafe' and 'café' distinct.");
            return true;
        }
        finally
        {
            await ExecuteAsync(connection, "DROP TEMPORARY TABLE IF EXISTS preflightAccentProbe");
        }
    }

    private static bool Fail(string message)
    {
        Console.WriteLine($"FAIL: {message}");
        return true;
    }

    private static Version? ParseVersion(string version)
    {
        var numeric = new string(version.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(numeric, out Version? parsed) ? parsed : null;
    }

    private static async Task<string> ScalarAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> ListAsync(MySqlConnection connection, string sql)
    {
        var values = new List<string>();
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
