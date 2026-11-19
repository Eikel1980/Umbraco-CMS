using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Web.Common.Hosting;
using Umbraco.Extensions;

namespace Umbraco.Cms.Api.Management.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IHtmlHelper"/> related to Umbraco back office functionality.
/// </summary>
public static class HtmlHelperBackOfficeExtensions
{
    /// <summary>
    ///     Outputs a script tag containing the import map for the BackOffice.
    /// </summary>
    /// <remarks>
    ///     It will replace the token %CACHE_BUSTER% with the cache buster hash.
    ///     It will also replace the /umbraco/backoffice path with the correct path for the BackOffice assets.
    ///     When a CSP nonce is available, it will be added to the script tag.
    /// </remarks>
    /// <param name="html">The HTML helper instance.</param>
    /// <param name="jsonSerializer">The JSON serializer used to serialize the package imports.</param>
    /// <param name="backOfficePathGenerator">The path generator for BackOffice assets and cache busting.</param>
    /// <param name="packageManifestService">The service to retrieve package manifest import maps.</param>
    /// <param name="cspNonceService">The service to retrieve the CSP nonce for the script tag.</param>
    /// <returns>A <see cref="Task"/> containing the html content for the BackOffice import map.</returns>
    public static async Task<IHtmlContent> BackOfficeImportMapScriptAsync(
        this IHtmlHelper html,
        IJsonSerializer jsonSerializer,
        IBackOfficePathGenerator backOfficePathGenerator,
        IPackageManifestService packageManifestService,
        ICspNonceService cspNonceService)
    {
        PackageManifestImportmap packageImports = await packageManifestService.GetPackageManifestImportmapAsync();

        var nonceAttribute = cspNonceService.GetNonceAttribute();

        var sb = new StringBuilder();
        sb.AppendLine($"<script type=\"importmap\"{nonceAttribute}>");
        sb.AppendLine(jsonSerializer.Serialize(ResolveBackOfficeImports(packageImports, backOfficePathGenerator)));
        sb.AppendLine("</script>");

        // Inject the BackOffice cache buster into the import string to handle BackOffice assets
        var importmapScript = sb.ToString()
            // TODO: Remove this when CacheBusterToken is gone. Scheduled for removal in Umbraco 20.
            .Replace(Constants.Web.CacheBusterToken, backOfficePathGenerator.BackOfficeCacheBustHash);

        return html.Raw(importmapScript);
    }

    internal static PackageManifestImportmap ResolveBackOfficeImports(PackageManifestImportmap map, IBackOfficePathGenerator paths)
    {
        const string packagedPath = "/" + Constants.System.UmbracoPathSegment + "/backoffice/";
        var publicPath = paths.BackOfficeVirtualDirectory + "/";
        var assetsPath = paths.BackOfficeAssetsPath + "/";

        string Resolve(string url)
        {
            if (url.StartsWith(assetsPath, StringComparison.Ordinal))
            {
                return url;
            }

            if (url.StartsWith(packagedPath, StringComparison.Ordinal))
            {
                return assetsPath + url[packagedPath.Length..];
            }

            return url.StartsWith(publicPath, StringComparison.Ordinal)
                ? assetsPath + url[publicPath.Length..]
                : url;
        }

        Dictionary<string, string> ResolveMappings(Dictionary<string, string> mappings)
        {
            var result = new Dictionary<string, string>();
            foreach (var (key, value) in mappings)
            {
                result[Resolve(key)] = Resolve(value);
            }

            return result;
        }

        var result = new PackageManifestImportmap { Imports = ResolveMappings(map.Imports) };
        if (map.Scopes is not null)
        {
            result.Scopes = new Dictionary<string, Dictionary<string, string>>();
            foreach (var (scope, mappings) in map.Scopes)
            {
                result.Scopes[Resolve(scope)] = ResolveMappings(mappings);
            }
        }

        return result;
    }
}
