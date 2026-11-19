using Umbraco.Cms.Infrastructure.Persistence;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Inserts MariaDB records in a single transaction when the caller has no transaction.
/// </summary>
public class MariaDbBulkSqlInsertProvider : IBulkSqlInsertProvider
{
    /// <inheritdoc />
    public string ProviderName => MariaDbSyntaxProvider.InvariantName;

    /// <inheritdoc />
    public int BulkInsertRecords<T>(IUmbracoDatabase database, IEnumerable<T> records)
    {
        using var transaction = database.InTransaction ? null : database.GetTransaction();
        var count = 0;
        foreach (T record in records)
        {
            database.Insert(record);
            count++;
        }

        transaction?.Complete();
        return count;
    }
}
