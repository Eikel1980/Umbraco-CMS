using MySqlConnector;
using NUnit.Framework;
using Umbraco.Cms.Core.Install.Models;
using Umbraco.Cms.Persistence.MariaDb;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Cms.Persistence.MariaDb;

[TestFixture]
public class MariaDbDatabaseProviderMetadataTests
{
    [TestCase("Server=localhost;Database=umbraco;User ID=u;GuidFormat=Char36", ExpectedResult = true)]
    [TestCase("Database=umbraco", ExpectedResult = false)]
    [TestCase("not a connection string", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool Recognizes_Connection_Strings_With_A_Server(string? connectionString)
        => new MariaDbDatabaseProviderMetadata().CanRecognizeConnectionString(connectionString);

    [Test]
    public void Generates_A_Char36_Guid_Connection_String()
    {
        var connectionString = new MariaDbDatabaseProviderMetadata().GenerateConnectionString(new DatabaseModel
        {
            Server = "db.example",
            DatabaseName = "umbraco",
            Login = "umbraco_user",
            Password = "secret;value",
        });

        var parsed = new MySqlConnectionStringBuilder(connectionString);
        Assert.Multiple(() =>
        {
            Assert.AreEqual("db.example", parsed.Server);
            Assert.AreEqual("umbraco", parsed.Database);
            Assert.AreEqual("umbraco_user", parsed.UserID);
            Assert.AreEqual("secret;value", parsed.Password);
            Assert.AreEqual(MySqlGuidFormat.Char36, parsed.GuidFormat);
        });
    }

    [Test]
    public void Does_Not_Force_Database_Creation()
        => Assert.IsFalse(new MariaDbDatabaseProviderMetadata().ForceCreateDatabase);
}
