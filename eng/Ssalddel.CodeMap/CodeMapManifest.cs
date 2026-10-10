using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.CodeMap;

public sealed class WorkAreaManifest
{
    public string Key { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string[] Solutions { get; set; } = Array.Empty<string>();
    public string[] ReadFirst { get; set; } = Array.Empty<string>();
    public string[] SourceRoots { get; set; } = Array.Empty<string>();
    public string[] ExcludedRoots { get; set; } = Array.Empty<string>();
    public WorkAreaFeature[] Features { get; set; } = Array.Empty<WorkAreaFeature>();
}

public sealed class WorkAreaFeature
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string[] DependsOnFeatures { get; set; } = Array.Empty<string>();
    public string[] RequiredStepKeys { get; set; } = Array.Empty<string>();
    public CodeMapSourceBinding[] SourceBindings { get; set; } = Array.Empty<CodeMapSourceBinding>();
}

// A reviewed source navigation binding, not a reflected component or a runtime call edge.
public sealed class CodeMapSourceBinding
{
    public string Key { get; set; } = string.Empty;
    public string[] RelatedStepKeys { get; set; } = Array.Empty<string>();
    public string Responsibility { get; set; } = string.Empty;
    public string Boundary { get; set; } = string.Empty;
    public CodeMapSourceReference[] Sources { get; set; } = Array.Empty<CodeMapSourceReference>();
}

public sealed class CodeMapSourceReference
{
    public string Path { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
}

public sealed record CodeMapStepIdentity(string FeatureKey, string StepKey);

public sealed class ResolvedCodeMapSourceBinding
{
    public string FeatureKey { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string[] RelatedStepKeys { get; init; } = Array.Empty<string>();
    public string Responsibility { get; init; } = string.Empty;
    public string Boundary { get; init; } = string.Empty;
    public ResolvedCodeMapSourceReference[] Sources { get; init; } = Array.Empty<ResolvedCodeMapSourceReference>();
}

public sealed class ResolvedCodeMapSourceReference
{
    public string Path { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public int Line { get; init; }
}

public sealed class CodeMapSourceBindingValidationResult
{
    public ResolvedCodeMapSourceBinding[] Bindings { get; init; } = Array.Empty<ResolvedCodeMapSourceBinding>();
    public SsalddelCodeMetadataDiagnostic[] Diagnostics { get; init; } = Array.Empty<SsalddelCodeMetadataDiagnostic>();
}
