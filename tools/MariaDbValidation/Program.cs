using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using NPoco;
using Umbraco.Cms.Core.DistributedLocking.Exceptions;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Persistence.MariaDb;

var connection = new MySqlConnectionStringBuilder
{
    Server = Environment.GetEnvironmentVariable("MARIADB_PROBE_SERVER") ?? "127.0.0.1",
    Database = Environment.GetEnvironmentVariable("MARIADB_PROBE_DATABASE") ?? "umbracomariadblab",
    UserID = Environment.GetEnvironmentVariable("MARIADB_PROBE_USER") ?? "umbraco_lab",
    Pooling = false,
};

if (Environment.GetEnvironmentVariable("MARIADB_PROBE_PASSWORD") is string probePassword)
{
    connection.Password = probePassword;
}
else
{
    Console.Write($"MariaDB password for {connection.UserID} on {connection.Database}: ");
    var password = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            break;
        }
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    connection.Password = password.ToString();
    password.Clear();
}

if (args is ["--cleanup"])
{
    await CleanupProbe.RunAsync(connection.ConnectionString);
    return;
}

if (args is ["--preflight"])
{
    // Read-only, so it is safe to point at a real Umbraco database with MARIADB_PROBE_DATABASE.
    Environment.ExitCode = await PreflightProbe.RunAsync(connection.ConnectionString);
    return;
}

var dialect = DatabaseType.Resolve("MySqlConnectorFactory", MariaDbSyntaxProvider.InvariantName);
using var metadata = new Database(connection.ConnectionString, dialect, MySqlConnectorFactory.Instance);
var sql = new SqlContext(new MariaDbSyntaxProvider(), dialect, metadata.PocoDataFactory);
UmbracoDatabase OpenSession()
{
    var db = new UmbracoDatabase(
        connection.ConnectionString,
        sql,
        MySqlConnectorFactory.Instance,
        NullLogger<UmbracoDatabase>.Instance,
        new MariaDbBulkSqlInsertProvider(),
        null!);
    db.OpenSharedConnection();
    return db;
}

using var first = OpenSession();
using var second = OpenSession();
var syntax = sql.SqlSyntax;
const string prefix = @"Umbraco!%_\";
string pattern = syntax.EscapeLikeValue(prefix) + syntax.GetWildcardPlaceholder();
string comparison = $"SELECT @0 LIKE @1 {syntax.GetLikeEscapeClause()}";
if (!first.ExecuteScalar<bool>(comparison, prefix + "State", pattern)
    || first.ExecuteScalar<bool>(comparison, @"Umbraco!other_\State", pattern)
    || first.ExecuteScalar<bool>(comparison, @"Umbraco!%X\State", pattern))
{
    throw new InvalidOperationException("Expected a literal key prefix followed by a wildcard.");
}

if (first.ExecuteScalar<long>("SELECT CONNECTION_ID()") == second.ExecuteScalar<long>("SELECT CONNECTION_ID()"))
{
    throw new InvalidOperationException("Expected independent MariaDB connections.");
}

// A plain CREATE must succeed before this probe owns the fixture. Never reuse an existing lock table.
first.Execute("CREATE TABLE umbracoLock (id INT PRIMARY KEY, value INT NOT NULL) ENGINE=InnoDB");
try
{
    first.Execute("INSERT INTO umbracoLock (id, value) VALUES (1, 1)");
    var reader = LockProvider(first);
    var writer = LockProvider(second);
    var timeout = TimeSpan.FromSeconds(1);
    using (var holding = first.GetTransaction())
    {
        using var shared = reader.ReadLock(1, timeout);
        using (var compatible = second.GetTransaction())
        {
            using var otherRead = writer.ReadLock(1, timeout);
            compatible.Complete();
        }
        using (var blocked = second.GetTransaction())
        {
            try
            {
                using var unexpected = writer.WriteLock(1, timeout);
                throw new InvalidOperationException("A writer acquired a row held by another reader.");
            }
            catch (DistributedWriteLockTimeoutException) { }
        }
        holding.Complete();
    }
    using (var retry = second.GetTransaction())
    {
        using var exclusive = writer.WriteLock(1, timeout);
        using (var blocked = first.GetTransaction())
        {
            try
            {
                using var unexpected = reader.ReadLock(1, timeout);
                throw new InvalidOperationException("A reader acquired a row held by another writer.");
            }
            catch (DistributedReadLockTimeoutException) { }
        }
        retry.Complete();
    }
    Console.WriteLine("PASS: concurrent readers, writer and reader timeouts, and acquisition after release.");
}
finally
{
    first.Execute("DROP TABLE umbracoLock");
}

// Use an ordinary fixture so an independent connection can verify committed state.
second.Execute("CREATE TABLE mariaDbBulkProbe (id INT PRIMARY KEY) ENGINE=InnoDB");
try
{
    try
    {
        first.BulkInsertRecords(new[] { new ProbeRow(1), new ProbeRow(1) });
        throw new InvalidOperationException("Expected the second insert to fail with a duplicate key.");
    }
    catch (MySqlException ex) when (ex.Number == 1062) { }
    AssertRows(second, 0);
    using (var outer = first.GetTransaction())
    {
        first.BulkInsertRecords(new[] { new ProbeRow(2), new ProbeRow(3) });
        AssertRows(first, 2);
        AssertRows(second, 0);
        // Disposing without Complete rolls the caller's transaction back.
    }
    AssertRows(second, 0);
    first.BulkInsertRecords(new[] { new ProbeRow(4), new ProbeRow(5) });
    AssertRows(second, 2);
}
finally
{
    second.Execute("DROP TABLE mariaDbBulkProbe");
}
Console.WriteLine("PASS: failed bulk insert rolled back, caller rollback respected, successful batch committed.");

static void AssertRows(IUmbracoDatabase database, int expected)
{
    int actual = database.ExecuteScalar<int>("SELECT COUNT(*) FROM mariaDbBulkProbe");
    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected {expected} probe rows, found {actual}.");
    }
}

static MariaDbDistributedLockingMechanism LockProvider(IUmbracoDatabase db)
{
    // Supply the connection as the ambient scope. SQL, transaction handling and exception translation run in the provider.
    var scope = DispatchProxy.Create<IScope, ProbeScope>();
    ((ProbeScope)(object)scope).Database = db;
    return new MariaDbDistributedLockingMechanism(new Lazy<IScopeAccessor>(() => new ProbeAccessor(scope)), null!, null!);
}
