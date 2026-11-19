using Umbraco.Cms.Infrastructure.Scoping;

/// <summary>Provides an independent scope for a single probe session.</summary>
internal sealed record ProbeAccessor(IScope AmbientScope) : IScopeAccessor;
