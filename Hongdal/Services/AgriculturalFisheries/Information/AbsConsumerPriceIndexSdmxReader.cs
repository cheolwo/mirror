using System.Globalization;
using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class AbsConsumerPriceIndexSdmxReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<AbsConsumerPriceIndexSdmxReadResult> ReadAsync(
        Stream stream,
        호주농수산식품가격조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var payload = await JsonSerializer.DeserializeAsync<AbsSdmxResponse>(
            stream,
            JsonOptions,
            cancellationToken);
        if (payload is null || payload.Errors.Count > 0)
        {
            return AbsConsumerPriceIndexSdmxReadResult.Invalid(
                "ABS가 현재 식품 가격지수 조회 조건을 처리하지 못했습니다.");
        }

        var dataSet = payload.Data.DataSets.FirstOrDefault();
        var structure = payload.Data.Structures.FirstOrDefault();
        var series = dataSet?.Series.Values.FirstOrDefault();
        var timeDimension = structure?.Dimensions.Observation.FirstOrDefault(dimension =>
            string.Equals(dimension.Id, "TIME_PERIOD", StringComparison.OrdinalIgnoreCase));
        if (dataSet is null
            || structure is null
            || series is null
            || timeDimension is null
            || series.Observations.Count == 0)
        {
            return AbsConsumerPriceIndexSdmxReadResult.Valid([], payload.Meta.Prepared);
        }

        var officialIndexLabel = ResolveDimensionLabel(
            structure.Dimensions.Series,
            "INDEX",
            request.IndexCode,
            호주농수산식품가격Catalog.OfficialIndexLabel(request.IndexCode));
        var unitCode = ResolveAttributeValue(
            series.Attributes,
            structure.Attributes.Series,
            "UNIT_MEASURE",
            value => value.Id);
        var unitLabel = ResolveAttributeValue(
            series.Attributes,
            structure.Attributes.Series,
            "UNIT_MEASURE",
            value => value.Name);
        var basePeriod = ResolveAttributeValue(
            dataSet.Attributes,
            structure.Attributes.DataSet,
            "BASE_PERIOD",
            value => value.Name);
        var items = series.Observations
            .Select(observation => MapItem(
                observation,
                timeDimension,
                request,
                officialIndexLabel,
                unitCode,
                unitLabel,
                basePeriod))
            .Where(item => item is not null)
            .Cast<호주농수산식품가격항목>()
            .OrderBy(item => item.ReferencePeriod, StringComparer.Ordinal)
            .ToArray();

        return AbsConsumerPriceIndexSdmxReadResult.Valid(items, payload.Meta.Prepared);
    }

    private static 호주농수산식품가격항목? MapItem(
        KeyValuePair<string, JsonElement[]> observation,
        AbsSdmxDimension timeDimension,
        호주농수산식품가격조회요청 request,
        string officialIndexLabel,
        string unitCode,
        string unitLabel,
        string basePeriod)
    {
        if (!int.TryParse(
                observation.Key,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var periodIndex)
            || periodIndex < 0
            || periodIndex >= timeDimension.Values.Count
            || observation.Value.Length == 0)
        {
            return null;
        }

        var value = observation.Value[0];
        var rawValue = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString() ?? string.Empty,
            _ => string.Empty
        };
        decimal? numericValue = value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var parsed)
                ? parsed
                : decimal.TryParse(
                    rawValue,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out parsed)
                    ? parsed
                    : null;

        return new 호주농수산식품가격항목
        {
            IndexCode = request.IndexCode,
            IndexLabel = 호주농수산식품가격Catalog.IndexLabel(request.IndexCode),
            OfficialIndexLabel = officialIndexLabel,
            MeasureCode = request.MeasureCode,
            MeasureLabel = 호주농수산식품가격Catalog.MeasureLabel(request.MeasureCode),
            RegionCode = request.RegionCode,
            RegionLabel = 호주농수산식품가격Catalog.RegionLabel(request.RegionCode),
            ReferencePeriod = timeDimension.Values[periodIndex].Id,
            RawValue = rawValue,
            NumericValue = numericValue,
            UnitCode = unitCode,
            UnitLabel = unitLabel,
            BasePeriod = basePeriod
        };
    }

    private static string ResolveDimensionLabel(
        IReadOnlyList<AbsSdmxDimension> dimensions,
        string dimensionId,
        string valueId,
        string fallback)
        => dimensions
               .FirstOrDefault(dimension => string.Equals(
                   dimension.Id,
                   dimensionId,
                   StringComparison.OrdinalIgnoreCase))
               ?.Values
               .FirstOrDefault(value => string.Equals(
                   value.Id,
                   valueId,
                   StringComparison.Ordinal))
               ?.Name
           ?? fallback;

    private static string ResolveAttributeValue(
        IReadOnlyList<int?> selectedIndexes,
        IReadOnlyList<AbsSdmxAttribute> definitions,
        string attributeId,
        Func<AbsSdmxCodeValue, string> selector)
    {
        var position = definitions
            .Select((definition, index) => new { definition, index })
            .FirstOrDefault(candidate => string.Equals(
                candidate.definition.Id,
                attributeId,
                StringComparison.OrdinalIgnoreCase))
            ?.index;
        if (position is null
            || position.Value >= selectedIndexes.Count
            || selectedIndexes[position.Value] is not int valueIndex)
        {
            return string.Empty;
        }

        var values = definitions[position.Value].Values;
        return valueIndex >= 0 && valueIndex < values.Count
            ? selector(values[valueIndex])
            : string.Empty;
    }
}
