using System.Reflection;
using Umbraco.Cms.Infrastructure.Persistence;

/// <summary>Exposes the probe connection through the ambient scope contract.</summary>
public class ProbeScope : DispatchProxy
{
    /// <summary>Gets or sets the database for this connection.</summary>
    public IUmbracoDatabase Database { get; set; } = null!;
    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method?.Name == "get_Database" ? Database : throw new NotSupportedException(method?.Name);
}
