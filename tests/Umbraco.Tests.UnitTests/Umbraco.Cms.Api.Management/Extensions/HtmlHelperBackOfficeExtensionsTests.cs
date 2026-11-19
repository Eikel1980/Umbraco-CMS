using Moq;
using NUnit.Framework;
using Umbraco.Cms.Api.Management.Extensions;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Web.Common.Hosting;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Cms.Api.Management.Extensions;

[TestFixture]
public class HtmlHelperBackOfficeExtensionsTests
{
    [TestCase("")]
    [TestCase("/staging")]
    public void Backoffice_Imports_Use_The_Public_Asset_Path(string pathBase)
    {
        const string packaged = "/umbraco/backoffice/";
        const string external = "https://cdn.example/umbraco/backoffice/app.js";
        var publicPath = pathBase + packaged;
        var assets = publicPath + "hash/";
        var paths = Mock.Of<IBackOfficePathGenerator>(p =>
            p.BackOfficeVirtualDirectory == publicPath.TrimEnd('/') &&
            p.BackOfficeAssetsPath == assets.TrimEnd('/'));
        var map = new PackageManifestImportmap
        {
            Imports = new() { ["app"] = packaged + "app.js", ["external"] = external },
            Scopes = new() { [publicPath] = new() { ["lib"] = publicPath + "lib.js" } },
        };

        var result = HtmlHelperBackOfficeExtensions.ResolveBackOfficeImports(map, paths);

        Assert.Multiple(() =>
        {
            Assert.AreEqual(assets + "app.js", result.Imports["app"]);
            Assert.AreEqual(external, result.Imports["external"]);
            Assert.AreEqual(assets + "lib.js", result.Scopes![assets]["lib"]);
            Assert.AreEqual(packaged + "app.js", map.Imports["app"]);
        });
    }
}
