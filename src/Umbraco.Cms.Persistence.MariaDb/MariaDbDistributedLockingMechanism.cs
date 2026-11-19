using Microsoft.Extensions.Options;
using MySqlConnector;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DistributedLocking;
using Umbraco.Cms.Core.DistributedLocking.Exceptions;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Holds shared and exclusive MariaDB row locks for the lifetime of the ambient transaction.
/// </summary>
public class MariaDbDistributedLockingMechanism(
    Lazy<IScopeAccessor> scopeAccessor,
    IOptionsMonitor<ConnectionStrings> connectionStrings,
    IOptionsMonitor<GlobalSettings> globalSettings) : IDistributedLockingMechanism
{
    /// <inheritdoc />
    public bool Enabled => connectionStrings.CurrentValue.IsConnectionStringConfigured()
        && string.Equals(connectionStrings.CurrentValue.ProviderName, MariaDbSyntaxProvider.InvariantName, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IDistributedLock ReadLock(int lockId, TimeSpan? obtainLockTimeout = null)
        => Acquire(lockId, DistributedLockType.ReadLock, obtainLockTimeout ?? globalSettings.CurrentValue.DistributedLockingReadLockDefaultTimeout);

    /// <inheritdoc />
    public IDistributedLock WriteLock(int lockId, TimeSpan? obtainLockTimeout = null)
        => Acquire(lockId, DistributedLockType.WriteLock, obtainLockTimeout ?? globalSettings.CurrentValue.DistributedLockingWriteLockDefaultTimeout);

    private IDistributedLock Acquire(int lockId, DistributedLockType lockType, TimeSpan timeout)
    {
        IUmbracoDatabase? database = scopeAccessor.Value.AmbientScope?.Database;
        if (database is null || !database.InTransaction)
        {
            throw new InvalidOperationException("MariaDB locking requires an ambient database transaction.");
        }

        bool readLock = lockType == DistributedLockType.ReadLock;
        var lockClause = readLock ? "LOCK IN SHARE MODE" : "FOR UPDATE";
        var waitSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
        database.OneTimeCommandTimeout = timeout.ToLockCommandTimeoutSeconds();

        try
        {
            int? value = database.ExecuteScalar<int?>(
                FormattableString.Invariant($"SELECT value FROM umbracoLock WHERE id = @0 {lockClause} WAIT {waitSeconds}"), lockId);
            if (value is null)
            {
                throw new InvalidOperationException($"Database lock {lockId} does not exist.");
            }
        }
        catch (MySqlException exception) when (exception.ErrorCode is MySqlErrorCode.LockWaitTimeout or MySqlErrorCode.CommandTimeoutExpired)
        {
            if (readLock)
            {
                throw new DistributedReadLockTimeoutException(lockId);
            }

            throw new DistributedWriteLockTimeoutException(lockId);
        }

        return new MariaDbDistributedLock(lockId, lockType);
    }

    private sealed class MariaDbDistributedLock(int lockId, DistributedLockType lockType) : IDistributedLock
    {
        public int LockId { get; } = lockId;

        public DistributedLockType LockType { get; } = lockType;

        public void Dispose()
        {
            // InnoDB releases the row lock when the ambient transaction ends.
        }
    }
}
