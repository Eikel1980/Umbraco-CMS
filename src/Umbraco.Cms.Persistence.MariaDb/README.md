# Umbraco MariaDB provider

Adds MariaDB support to Umbraco through MySqlConnector. Set the connection string provider name to `MySqlConnector`.

## Requirements

- MariaDB 10.10 or later (validated on 11.8 LTS).
- A database created with `CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_as_ci`.
  The installer creates it this way when Umbraco is allowed to create a missing database.
  Tables inherit the database's character set and collation, so tables added by later upgrades match the existing ones.
- `GuidFormat=Char36` in the connection string.

Run `dotnet run --project tools/MariaDbValidation -- --preflight` with `MARIADB_PROBE_DATABASE`
pointing at a database to check its version, character set and collation. It only reads.

## Upgrades: always back up first

MariaDB commits automatically after every `CREATE`, `ALTER` and `DROP`. Umbraco's upgrade steps
expect the database to roll a failed step back completely, and on MariaDB it cannot: a step that
fails after changing the schema leaves the schema changed while the recorded upgrade state still
points at the step before it. Restoring a backup is the reliable way back.

Before every Umbraco upgrade:

1. Stop the site.
2. `mariadb-dump --single-transaction --routines --triggers <database> > before-upgrade.sql`
3. Upgrade, start the site and check the log.
4. If the upgrade fails, restore the dump into an empty database and start the old version again.

Test the upgrade on a restored copy of the live database first when you can.
