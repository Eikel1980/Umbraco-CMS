using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using Umbraco.Cms.Persistence.EFCore.MariaDb.Migrations;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Cms.Persistence.MariaDb;

[TestFixture]
public class MariaDbMigrationTests
{
    /// <summary>
    /// "ALTER DATABASE CHARACTER SET utf8mb4", or a table or column naming only utf8mb4, picks utf8mb4's
    /// default collation (accent-insensitive on MariaDB 11.5+) instead of the database's collation.
    /// </summary>
    [Test]
    public void Initial_Migration_Leaves_Character_Set_And_Collation_To_The_Database()
    {
        IReadOnlyList<MigrationOperation> operations = new InitialCreate().UpOperations;

        var offending = operations
            .Concat(operations.OfType<CreateTableOperation>().SelectMany(table => table.Columns))
            .Where(operation => operation is AlterDatabaseOperation
                || operation.GetAnnotations().Any(a => a.Name is "MySql:CharSet" or "Relational:Collation" or "MySql:Collation"))
            .Select(operation => operation.ToString())
            .ToList();

        Assert.That(offending, Is.Empty);
    }
}
