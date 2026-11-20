using NUnit.Framework;
using Umbraco.Cms.Persistence.EFCore.MariaDb;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Cms.Persistence.MariaDb;

[TestFixture]
public class MariaDbMigrationProviderSetupTests
{
    [TestCase(null)]
    [TestCase("")]
    public void Uses_The_Fallback_Version_Without_A_Connection_String(string? connectionString)
        => Assert.AreSame(MariaDbMigrationProviderSetup.FallbackServerVersion, MariaDbMigrationProviderSetup.GetServerVersion(connectionString));

    [Test]
    public void Uses_The_Fallback_Version_When_The_Server_Cannot_Be_Reached()
    {
        // Port 1 refuses connections immediately, so detection fails fast.
        const string unreachable = "Server=127.0.0.1;Port=1;Database=umbraco;User ID=u;Connection Timeout=2";

        Assert.AreSame(MariaDbMigrationProviderSetup.FallbackServerVersion, MariaDbMigrationProviderSetup.GetServerVersion(unreachable));
    }

    [Test]
    public void Falls_Back_To_The_Oldest_Validated_LTS_Release()
        => Assert.AreEqual(new Version(11, 8, 0), MariaDbMigrationProviderSetup.FallbackServerVersion.Version);
}
