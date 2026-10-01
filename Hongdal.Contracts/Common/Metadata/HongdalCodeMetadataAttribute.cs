using System.Reflection;

namespace Hongdal.Contracts.Common.Metadata;

public static class HongdalCodeFeatureKeys
{
    public const string CommunityAuthoringImage = "community-authoring-image";
}

public enum HongdalCodeLayer
{
    Contract,
    Api,
    Application,
    Domain,
    Infrastructure,
    ExternalAdapter,
    ClientAdapter,
    ViewModel,
    View
}

[Flags]
public enum HongdalCodeEffect
{
    None = 0,
    NetworkCall = 1 << 0,
    ThirdPartyApiCall = 1 << 1,
    PersistentRead = 1 << 2,
    PersistentWrite = 1 << 3,
    ObjectStorageRead = 1 << 4,
    ObjectStorageWrite = 1 << 5,
    UiStateMutation = 1 << 6,
    MayIncurExternalCost = 1 << 7
}

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false)]
public sealed class HongdalCodeMetadataAttribute : Attribute
{
    public HongdalCodeMetadataAttribute(
        string featureKey,
        HongdalCodeLayer layer,
        string responsibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibility);

        FeatureKey = featureKey.Trim();
        Layer = layer;
        Responsibility = responsibility.Trim();
    }

    public string FeatureKey { get; }

    public HongdalCodeLayer Layer { get; }

    public string Responsibility { get; }

    public HongdalCodeEffect Effects { get; set; }

    public Type? ContractType { get; set; }

    public int FlowOrder { get; set; }

    public string Boundary { get; set; } = string.Empty;
}

public sealed record HongdalCodeMetadataDescriptor(
    Type ComponentType,
    string FeatureKey,
    HongdalCodeLayer Layer,
    string Responsibility,
    HongdalCodeEffect Effects,
    Type? ContractType,
    int FlowOrder,
    string Boundary);

public static class HongdalCodeMetadataReader
{
    public static IReadOnlyList<HongdalCodeMetadataDescriptor> Read(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .SelectMany(ReadType)
            .OrderBy(metadata => metadata.FeatureKey, StringComparer.Ordinal)
            .ThenBy(metadata => metadata.FlowOrder)
            .ThenBy(metadata => metadata.ComponentType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<HongdalCodeMetadataDescriptor> ReadFeature(
        string featureKey,
        params Assembly[] assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);

        return Read(assemblies)
            .Where(metadata => string.Equals(
                metadata.FeatureKey,
                featureKey.Trim(),
                StringComparison.Ordinal))
            .ToArray();
    }

    public static IReadOnlyList<HongdalCodeMetadataDescriptor> Read(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        return ReadType(componentType).ToArray();
    }

    private static IEnumerable<HongdalCodeMetadataDescriptor> ReadType(Type componentType)
        => componentType
            .GetCustomAttributes<HongdalCodeMetadataAttribute>(inherit: false)
            .Select(attribute => new HongdalCodeMetadataDescriptor(
                componentType,
                attribute.FeatureKey,
                attribute.Layer,
                attribute.Responsibility,
                attribute.Effects,
                attribute.ContractType,
                attribute.FlowOrder,
                attribute.Boundary));

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }
}
