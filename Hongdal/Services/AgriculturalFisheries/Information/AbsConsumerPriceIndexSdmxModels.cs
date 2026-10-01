using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal sealed record AbsConsumerPriceIndexSdmxReadResult(
    bool IsValid,
    IReadOnlyList<호주농수산식품가격항목> Items,
    DateTime? SourcePreparedAtUtc,
    string? ErrorMessage)
{
    public static AbsConsumerPriceIndexSdmxReadResult Valid(
        IReadOnlyList<호주농수산식품가격항목> items,
        DateTime? sourcePreparedAtUtc)
        => new(true, items, sourcePreparedAtUtc, null);

    public static AbsConsumerPriceIndexSdmxReadResult Invalid(string errorMessage)
        => new(false, [], null, errorMessage);
}

internal sealed class AbsSdmxResponse
{
    public AbsSdmxMeta Meta { get; init; } = new();

    public AbsSdmxData Data { get; init; } = new();

    public IReadOnlyList<JsonElement> Errors { get; init; } = [];
}

internal sealed class AbsSdmxMeta
{
    public DateTime? Prepared { get; init; }
}

internal sealed class AbsSdmxData
{
    public IReadOnlyList<AbsSdmxDataSet> DataSets { get; init; } = [];

    public IReadOnlyList<AbsSdmxStructure> Structures { get; init; } = [];
}

internal sealed class AbsSdmxDataSet
{
    public IReadOnlyList<int?> Attributes { get; init; } = [];

    public IReadOnlyDictionary<string, AbsSdmxSeries> Series { get; init; } =
        new Dictionary<string, AbsSdmxSeries>();
}

internal sealed class AbsSdmxSeries
{
    public IReadOnlyList<int?> Attributes { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement[]> Observations { get; init; } =
        new Dictionary<string, JsonElement[]>();
}

internal sealed class AbsSdmxStructure
{
    public AbsSdmxDimensions Dimensions { get; init; } = new();

    public AbsSdmxAttributes Attributes { get; init; } = new();
}

internal sealed class AbsSdmxDimensions
{
    public IReadOnlyList<AbsSdmxDimension> Series { get; init; } = [];

    public IReadOnlyList<AbsSdmxDimension> Observation { get; init; } = [];
}

internal sealed class AbsSdmxDimension
{
    public string Id { get; init; } = string.Empty;

    public IReadOnlyList<AbsSdmxCodeValue> Values { get; init; } = [];
}

internal sealed class AbsSdmxAttributes
{
    public IReadOnlyList<AbsSdmxAttribute> DataSet { get; init; } = [];

    public IReadOnlyList<AbsSdmxAttribute> Series { get; init; } = [];
}

internal sealed class AbsSdmxAttribute
{
    public string Id { get; init; } = string.Empty;

    public IReadOnlyList<AbsSdmxCodeValue> Values { get; init; } = [];
}

internal sealed class AbsSdmxCodeValue
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
