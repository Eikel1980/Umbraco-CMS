using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Extensions;
using ColumnInfo = Umbraco.Cms.Infrastructure.Persistence.SqlSyntax.ColumnInfo;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Translates Umbraco's CMS schema and queries to MariaDB SQL.
/// </summary>
public class MariaDbSyntaxProvider : SqlSyntaxProviderBase<MariaDbSyntaxProvider>
{
    /// <summary>The configured ADO.NET provider invariant name.</summary>
    public const string InvariantName = "MySqlConnector";

    private const char IdentifierQuote = '\u0060';

    /// <summary>Initializes the MariaDB column type mappings.</summary>
    public MariaDbSyntaxProvider()
    {
        GuidColumnDefinition = "CHAR(36)";
        DateTimeColumnDefinition = "DATETIME(6)";
        DateTimeOffsetColumnDefinition = "DATETIME(6)";
        TimeColumnDefinition = "TIME(6)";
        BlobColumnDefinition = "LONGBLOB";
    }

    /// <inheritdoc />
    public override string ProviderName => InvariantName;

    /// <inheritdoc />
    public override string DbProvider => InvariantName;

    /// <inheritdoc />
    public override IsolationLevel DefaultIsolationLevel => IsolationLevel.ReadCommitted;

    /// <inheritdoc />
    public override string StringLengthUnicodeColumnDefinitionFormat => "VARCHAR({0})";

    /// <inheritdoc />
    public override string Length => "CHAR_LENGTH";

    /// <inheritdoc />
    public override string GetLikeEscapeClause() => "ESCAPE '!'";

    /// <inheritdoc />
    public override string EscapeLikeValue(string value)
        => value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

    /// <inheritdoc />
    public override string AlterColumn => "ALTER TABLE {0} MODIFY COLUMN {1}";

    /// <inheritdoc />
    public override string DropIndex => "DROP INDEX {0} ON {1}";

    /// <inheritdoc />
    public override string ConvertIntegerToOrderableString => "LPAD(CAST(({0} + 2147483648) AS CHAR), 10, '0')";

    /// <inheritdoc />
    public override string ConvertDecimalToOrderableString => "LPAD(CAST(CAST(({0} + 100000000000) AS DECIMAL(21,9)) AS CHAR), 22, '0')";

    /// <inheritdoc />
    public override string ConvertDateToOrderableString => "DATE_FORMAT({0}, '%Y-%m-%d %H:%i:%s.%f')";

    /// <inheritdoc />
    public override string ConvertUniqueIdentifierToString => "CAST({0} AS CHAR(36))";

    /// <inheritdoc />
    public override bool SupportsIdentityInsert() => false;

    /// <inheritdoc />
    public override bool SupportsClustered() => false;

    /// <inheritdoc />
    public override bool SupportsTransactionalDdl => false;

    /// <inheritdoc />
    public override string TruncateConstraintName<T>(string constraintName)
    {
        if (constraintName.Length <= 64)
        {
            return constraintName;
        }

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(constraintName)));
        return $"{constraintName[..51]}_{hash[..12]}";
    }

    /// <inheritdoc />
    public override string GetQuotedName(string? name)
    {
        string escaped = (name ?? string.Empty).Replace(IdentifierQuote.ToString(), new string(IdentifierQuote, 2));
        return $"{IdentifierQuote}{escaped}{IdentifierQuote}";
    }

    /// <inheritdoc />
    public override string GetQuotedColumnName(string? columnName) => GetQuotedName(columnName);

    /// <inheritdoc />
    public override string GetQuotedTableName(string? tableName) => GetQuotedName(tableName);

    /// <inheritdoc />
    public override string GetColumn(DatabaseType dbType, string tableName, string columnName, string? columnAlias, string? referenceName = null, bool forInsert = false)
        => forInsert ? GetQuotedColumnName(columnName) : base.GetColumn(dbType, tableName, columnName, columnAlias, referenceName);

    /// <inheritdoc />
    public override string GetIndexType(IndexTypes indexTypes)
        => indexTypes is IndexTypes.UniqueClustered or IndexTypes.UniqueNonClustered ? "UNIQUE" : string.Empty;

    /// <inheritdoc />
    public override string Format(IndexDefinition index)
    {
        // The CMS allows long login identifiers. Keep their full stored values while
        // fitting the non-unique lookup indexes within InnoDB's 3072-byte utf8mb4 key limit.
        if (index.IndexType != IndexTypes.NonClustered)
        {
            return base.Format(index);
        }

        string? columns = (index.TableName, index.Name) switch
        {
            ("cmsMember", "IX_cmsMember_LoginName") => "`LoginName`(768)",
            ("umbracoExternalMember", "IX_umbracoExternalMember_Email") => "`email`(768)",
            ("umbracoExternalMember", "IX_umbracoExternalMember_UserName") => "`userName`(768)",
            ("umbracoExternalLogin", "IX_umbracoExternalLogin_ProviderKey") => "`loginProvider`,`providerKey`(368)",
            _ => null,
        };
        return columns is null
            ? base.Format(index)
            : $"CREATE INDEX {GetQuotedName(index.Name)} ON {GetQuotedTableName(index.TableName)} ({columns})";
    }

    /// <inheritdoc />
    public override string GetSpecialDbType(SpecialDbType dbType)
        => dbType.ToString() switch
        {
            "NCHAR" => "CHAR(1)",
            "NTEXT" or "NVARCHARMAX" => "LONGTEXT",
            _ => StringColumnDefinition,
        };

    /// <inheritdoc />
    public override string GetSpecialDbType(SpecialDbType dbType, int customSize)
        => dbType.ToString() switch
        {
            "NCHAR" => FormattableString.Invariant($"CHAR({customSize})"),
            "NTEXT" or "NVARCHARMAX" => "LONGTEXT",
            _ => string.Format(CultureInfo.InvariantCulture, StringLengthColumnDefinitionFormat, customSize),
        };

    /// <inheritdoc />
    protected override string FormatConstraint(ColumnDefinition column) => string.Empty;

    /// <inheritdoc />
    protected override string FormatDefaultValue(ColumnDefinition column)
        => column.DefaultValue is bool value ? value ? "DEFAULT 1" : "DEFAULT 0" : base.FormatDefaultValue(column);

    /// <inheritdoc />
    protected override string FormatIdentity(ColumnDefinition column) => column.IsIdentity ? "AUTO_INCREMENT" : string.Empty;

    /// <inheritdoc />
    protected override string? FormatSystemMethods(SystemMethods systemMethod) =>
        systemMethod switch
        {
            SystemMethods.NewGuid => "UUID()",
            SystemMethods.CurrentDateTime => "CURRENT_TIMESTAMP(6)",
            SystemMethods.CurrentUTCDateTime => "UTC_TIMESTAMP(6)",
            _ => null,
        };

    /// <inheritdoc />
    public override string FormatPrimaryKey(TableDefinition table)
    {
        ColumnDefinition? key = table.Columns.FirstOrDefault(column => column.IsPrimaryKey);
        if (key is null)
        {
            return string.Empty;
        }

        string[] columns = string.IsNullOrEmpty(key.PrimaryKeyColumns)
            ? [key.Name]
            : key.PrimaryKeyColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return $"PRIMARY KEY ({string.Join(", ", columns.Select(GetQuotedColumnName))})";
    }

    /// <inheritdoc />
    public override string Format(TableDefinition table)
    {
        var columns = Format(table.Columns);
        var primaryKey = FormatPrimaryKey(table);
        if (primaryKey.Length > 0)
        {
            columns += $", {primaryKey}";
        }

        int seed = table.Columns.FirstOrDefault(column => column.IsIdentity)?.Seeding ?? 0;
        var options = seed > 1 ? FormattableString.Invariant($" AUTO_INCREMENT={seed}") : string.Empty;
        return $"CREATE TABLE {GetQuotedTableName(table.Name)} ({columns}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{options}";
    }

    /// <inheritdoc />
    public override void HandleCreateTable(IDatabase database, TableDefinition tableDefinition, bool skipKeysAndIndexes = false)
    {
        database.Execute(Format(tableDefinition));

        if (skipKeysAndIndexes)
        {
            return;
        }

        foreach (string statement in Format(tableDefinition.Indexes))
        {
            database.Execute(statement);
        }

        foreach (string statement in Format(tableDefinition.ForeignKeys))
        {
            database.Execute(statement);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<string> GetTablesInSchema(IDatabase db)
        => db.Fetch<string>("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'");

    /// <inheritdoc />
    public override IEnumerable<ColumnInfo> GetColumnsInSchema(IDatabase db) =>
        db.Fetch<SchemaColumn>(
            """
            SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, ORDINAL_POSITION AS Ordinal,
                   COLUMN_DEFAULT AS DefaultValue, IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
            """)
            .Select(column => new ColumnInfo(column.TableName, column.ColumnName, column.Ordinal, column.IsNullable, column.DataType)
            {
                ColumnDefault = column.DefaultValue,
            });

    /// <inheritdoc />
    public override IEnumerable<Tuple<string, string, string, bool>> GetDefinedIndexes(IDatabase db) =>
        db.Fetch<SchemaIndex>(
            """
            SELECT TABLE_NAME AS TableName, INDEX_NAME AS IndexName, COLUMN_NAME AS ColumnName, NON_UNIQUE AS NonUnique
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME <> 'PRIMARY'
            ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX
            """)
            .Select(index => Tuple.Create(index.TableName, index.IndexName, index.ColumnName, index.NonUnique == 0));

    /// <inheritdoc />
    public override IEnumerable<Tuple<string, string, string>> GetConstraintsPerColumn(IDatabase db) =>
        db.Fetch<SchemaConstraint>(
            """
            SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, CONSTRAINT_NAME AS ConstraintName
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
            """)
            .Select(constraint => Tuple.Create(constraint.TableName, constraint.ColumnName, constraint.ConstraintName));

    /// <inheritdoc />
    public override IEnumerable<Tuple<string, string>> GetConstraintsPerTable(IDatabase db) =>
        GetConstraintsPerColumn(db).Select(constraint => Tuple.Create(constraint.Item1, constraint.Item3)).Distinct();

    /// <inheritdoc />
    public override bool DoesPrimaryKeyExist(IDatabase db, string tableName, string primaryKeyName) =>
        db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @0 AND CONSTRAINT_TYPE = 'PRIMARY KEY'",
            tableName) > 0;

    /// <inheritdoc />
    public override bool TryGetDefaultConstraint(IDatabase db, string? tableName, string columnName, [MaybeNullWhen(false)] out string constraintName)
    {
        // MariaDB column defaults have no independently named constraint.
        constraintName = null;
        return false;
    }

    /// <inheritdoc />
    public override Sql<ISqlContext> SelectTop(Sql<ISqlContext> sql, int top)
        => sql.Append(FormattableString.Invariant($"LIMIT {top}"));

    /// <inheritdoc />
    public override Sql<ISqlContext>.SqlJoinClause<ISqlContext> LeftJoinWithNestedJoin<TDto>(
        Sql<ISqlContext> sql,
        Func<Sql<ISqlContext>, Sql<ISqlContext>> nestedJoin,
        string? alias = null)
    {
        var tableName = GetQuotedTableName(typeof(TDto).GetTableName());
        var table = alias is null ? tableName : $"{tableName} {GetQuotedName(alias)}";
        Sql<ISqlContext>.SqlJoinClause<ISqlContext> clause = sql.LeftJoin($"({table}");
        sql.Append(nestedJoin(new Sql<ISqlContext>(sql.SqlContext)));
        sql.Append(")");
        return clause;
    }

    /// <inheritdoc />
    public override Sql<ISqlContext> AppendForUpdateHint(Sql<ISqlContext> sql) => sql.Append("FOR UPDATE");

    private sealed class SchemaColumn
    {
        public string TableName { get; set; } = string.Empty;

        public string ColumnName { get; set; } = string.Empty;

        public int Ordinal { get; set; }

        public string? DefaultValue { get; set; }

        public string IsNullable { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;
    }

    private sealed class SchemaIndex
    {
        public string TableName { get; set; } = string.Empty;

        public string IndexName { get; set; } = string.Empty;

        public string ColumnName { get; set; } = string.Empty;

        public int NonUnique { get; set; }
    }

    private sealed class SchemaConstraint
    {
        public string TableName { get; set; } = string.Empty;

        public string ColumnName { get; set; } = string.Empty;

        public string ConstraintName { get; set; } = string.Empty;
    }
}
