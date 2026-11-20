using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Cms.Persistence.MariaDb;

/// <summary>
/// Registers MariaDB CMS persistence services when this assembly is referenced.
/// </summary>
public class MariaDbComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.AddUmbracoMariaDbSupport();
}
