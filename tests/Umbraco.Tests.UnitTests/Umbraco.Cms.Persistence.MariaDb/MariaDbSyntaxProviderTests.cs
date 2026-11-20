using System.Text.RegularExpressions;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Migrations.Install;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Persistence.MariaDb;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Cms.Persistence.MariaDb;

[TestFixture]
public class MariaDbSyntaxProviderTests
{
    // InnoDB's index key limit, and the most bytes one utf8mb4 character can take.
    private const int MaxIndexBytes = 3072;
    private const int BytesPerCharacter = 4;

    private static readonly Regex _indexColumn = new(@"`(?<name>(?:[^`]|``)+)`(?:\((?<prefix>\d+)\))?", RegexOptions.Compiled);
    private static readonly Regex _sizedText = new(@"^(?:VAR)?CHAR\((?<size>\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static MariaDbSyntaxProvider CreateSut() => new();

    [TestCase("umbracoNode", ExpectedResult = "`umbracoNode`")]
    [TestCase("we`ird", ExpectedResult = "`we``ird`")]
    [TestCase(null, ExpectedResult = "``")]
    public string Can_Quote_Names(string? name) => CreateSut().GetQuotedName(name);

    [Test]
    public void Can_Escape_Like_Wildcards_With_The_Escape_Character()
    {
        MariaDbSyntaxProvider sut = CreateSut();

        Assert.Multiple(() =>
        {
            Assert.AreEqual("a!%b!_c!!d", sut.EscapeLikeValue("a%b_c!d"));
            Assert.AreEqual("ESCAPE '!'", sut.GetLikeEscapeClause());
        });
    }

    [Test]
    public void Keeps_Short_Constraint_Names()
        => Assert.AreEqual("FK_short", CreateSut().TruncateConstraintName<ForeignKeyDefinition>("FK_short"));

    [Test]
    public void Truncates_Long_Constraint_Names_To_A_Stable_Unique_Name()
    {
        MariaDbSyntaxProvider sut = CreateSut();
        var first = "FK_" + new string('a', 80) + "_one";
        var second = "FK_" + new string('a', 80) + "_two";

        var truncated = sut.TruncateConstraintName<ForeignKeyDefinition>(first);

        Assert.Multiple(() =>
        {
            Assert.That(truncated.Length, Is.LessThanOrEqualTo(64));
            Assert.AreEqual(truncated, sut.TruncateConstraintName<ForeignKeyDefinition>(first));
            Assert.AreNotEqual(truncated, sut.TruncateConstraintName<ForeignKeyDefinition>(second));
        });
    }

    [Test]
    public void Creates_InnoDB_Tables_That_Inherit_The_Database_Character_Set_And_Collation()
    {
        MariaDbSyntaxProvider sut = CreateSut();

        Assert.Multiple(() =>
        {
            foreach (Type type in DatabaseSchemaCreator._orderedTables)
            {
                var sql = sut.Format(DefinitionFactory.GetTableDefinition(type, sut));

                StringAssert.Contains("ENGINE=InnoDB", sql);

                // A character set without a collation would pick that character set's default collation
                // instead of the database's, so neither may be named.
                StringAssert.DoesNotContain("CHARSET", sql);
                StringAssert.DoesNotContain("COLLATE", sql);

                // MariaDB's NVARCHAR means utf8mb3, which cannot store emoji.
                StringAssert.DoesNotContain("NVARCHAR", sql);
            }
        });
    }

    [Test]
    public void Creates_Missing_Databases_With_The_Accent_Sensitive_Collation()
        => Assert.AreEqual(
            "CREATE DATABASE IF NOT EXISTS `umbraco``site` CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_as_ci",
            MariaDbDatabaseCreator.GetCreateDatabaseSql("umbraco`site"));

    /// <summary>
    /// Guards against new or longer upstream columns that would make a fresh install fail with
    /// "Specified key was too long". When this fails, add a prefix for the named index in
    /// <see cref="MariaDbSyntaxProvider.Format(IndexDefinition)"/>.
    /// </summary>
    [Test]
    public void Every_Umbraco_Key_And_Index_Fits_The_InnoDB_Key_Limit()
    {
        MariaDbSyntaxProvider sut = CreateSut();
        var failures = new List<string>();

        foreach (Type type in DatabaseSchemaCreator._orderedTables)
        {
            TableDefinition table = DefinitionFactory.GetTableDefinition(type, sut);
            var columnBytes = table.Columns.ToDictionary(
                column => column.Name,
                column => GetColumnBytes(sut.Format(column)),
                StringComparer.OrdinalIgnoreCase);

            foreach (IndexDefinition index in table.Indexes)
            {
                CheckKey($"{table.Name}.{index.Name}", sut.Format(index), columnBytes, failures);
            }

            var primaryKey = sut.FormatPrimaryKey(table);
            if (primaryKey.Length > 0)
            {
                CheckKey($"{table.Name} primary key", primaryKey, columnBytes, failures);
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    private static void CheckKey(string label, string statement, IReadOnlyDictionary<string, long> columnBytes, List<string> failures)
    {
        // Only the column list counts: skip the index and table names before it.
        var columnList = statement[statement.LastIndexOf(" (", StringComparison.Ordinal)..];
        long total = 0;
        foreach (Match match in _indexColumn.Matches(columnList))
        {
            var name = match.Groups["name"].Value.Replace("``", "`");
            if (match.Groups["prefix"].Success)
            {
                total += long.Parse(match.Groups["prefix"].Value) * BytesPerCharacter;
            }
            else if (columnBytes.TryGetValue(name, out var bytes))
            {
                total += bytes;
            }
            else
            {
                failures.Add($"{label}: unknown column {name} in: {statement}");
            }
        }

        if (total > MaxIndexBytes)
        {
            failures.Add($"{label}: {total} bytes exceeds {MaxIndexBytes}: {statement}");
        }
    }

    private static long GetColumnBytes(string columnSql)
    {
        // "`name` TYPE ..." - read the type that follows the quoted name.
        var type = columnSql[(columnSql.IndexOf("` ", StringComparison.Ordinal) + 2)..];
        Match sized = _sizedText.Match(type);
        if (sized.Success)
        {
            return long.Parse(sized.Groups["size"].Value) * BytesPerCharacter;
        }

        // TEXT and BLOB columns cannot be indexed without a prefix.
        return type.Contains("TEXT", StringComparison.OrdinalIgnoreCase) || type.Contains("BLOB", StringComparison.OrdinalIgnoreCase)
            ? long.MaxValue / 4
            : 16;
    }
}
