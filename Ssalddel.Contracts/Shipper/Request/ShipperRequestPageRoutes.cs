using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Contracts.Shipper.Request;

public enum ShipperRequestAuthoringStep
{
    Cargo,
    Transport,
    Procedure,
    Review
}

/// <summary>
/// Web과 모바일 운송 의뢰 작성 Screen이 같은 단계 route와 출발 문맥을 공유하기 위한 계약입니다.
/// </summary>
public static class ShipperRequestPageRoutes
{
    public const string Root = "/shipper/request";
    public const string Cargo = "/shipper/request/cargo";
    public const string Package = "/shipper/request/package";
    public const string Pickup = "/shipper/request/pickup";
    public const string Dropoff = "/shipper/request/dropoff";
    public const string Schedule = "/shipper/request/schedule";
    public const string Vehicle = "/shipper/request/vehicle";
    public const string Transport = "/shipper/request/transport";
    public const string Procedure = "/shipper/request/procedure";
    public const string Review = "/shipper/request/review";
    public const string LegacySummary = "/shipper/request/summary";
    public const string Bulk = "/shipper/request/bulk";

    public static string PathFor(ShipperRequestAuthoringStep step)
        => step switch
        {
            ShipperRequestAuthoringStep.Cargo => Cargo,
            ShipperRequestAuthoringStep.Transport => Transport,
            ShipperRequestAuthoringStep.Procedure => Procedure,
            ShipperRequestAuthoringStep.Review => Review,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "지원하지 않는 운송 의뢰 작성 단계입니다.")
        };

    public static string PathFor(ShipperRequestMobileStep step)
        => ShipperRequestMobileStepCatalog.Get(step).Path;
}

/// <summary>
/// 작성 단계를 이동할 때 커뮤니티 글과 다이어그램 출발 문맥을 잃지 않도록 제한된 query만 전달합니다.
/// </summary>
public sealed record ShipperRequestNavigationContext
{
    public long? SourcePostId { get; init; }
    public string? Source { get; init; }
    public string? SourceMarkerId { get; init; }
    public string? LedgerTemplateKey { get; init; }
    public string? NodeTitle { get; init; }
    public string? NodeKind { get; init; }
    public string? CountryCode { get; init; }
    public string? ReturnPath { get; init; }

    public string RootPath => BuildPath(ShipperRequestPageRoutes.Root);

    public string PathFor(ShipperRequestAuthoringStep step)
        => BuildPath(ShipperRequestPageRoutes.PathFor(step));

    public string PathFor(ShipperRequestMobileStep step)
        => BuildPath(ShipperRequestPageRoutes.PathFor(step));

    public static ShipperRequestNavigationContext Parse(string? uri)
    {
        var query = ExtractQuery(uri);
        var values = ParseQuery(query);

        return new ShipperRequestNavigationContext
        {
            SourcePostId = TryPositiveLong(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.SourcePostId)),
            Source = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.Source), 80),
            SourceMarkerId = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.SourceMarkerId), 160),
            LedgerTemplateKey = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.LedgerTemplateKey), 100),
            NodeTitle = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.NodeTitle), 200),
            NodeKind = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.NodeKind), 80),
            CountryCode = NormalizeValue(values.GetValueOrDefault(ShipperRequestNavigationQueryNames.CountryCode), 8),
            ReturnPath = PageNavigationContext.NormalizeReturnPath(
                values.GetValueOrDefault(PageNavigationQueryNames.ReturnPath))
        };
    }

    private string BuildPath(string path)
    {
        var values = new (string Key, string? Value)[]
        {
            (ShipperRequestNavigationQueryNames.SourcePostId, SourcePostId is > 0 ? SourcePostId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null),
            (ShipperRequestNavigationQueryNames.Source, NormalizeValue(Source, 80)),
            (ShipperRequestNavigationQueryNames.SourceMarkerId, NormalizeValue(SourceMarkerId, 160)),
            (ShipperRequestNavigationQueryNames.LedgerTemplateKey, NormalizeValue(LedgerTemplateKey, 100)),
            (ShipperRequestNavigationQueryNames.NodeTitle, NormalizeValue(NodeTitle, 200)),
            (ShipperRequestNavigationQueryNames.NodeKind, NormalizeValue(NodeKind, 80)),
            (ShipperRequestNavigationQueryNames.CountryCode, NormalizeValue(CountryCode, 8)),
            (PageNavigationQueryNames.ReturnPath, PageNavigationContext.NormalizeReturnPath(ReturnPath))
        };

        var query = values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value!)}")
            .ToArray();

        return query.Length == 0 ? path : $"{path}?{string.Join("&", query)}";
    }

    private static string ExtractQuery(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
        {
            return absolute.Query;
        }

        var queryIndex = uri.IndexOf('?');
        if (queryIndex < 0)
        {
            return string.Empty;
        }

        var fragmentIndex = uri.IndexOf('#', queryIndex);
        return fragmentIndex < 0
            ? uri[queryIndex..]
            : uri[queryIndex..fragmentIndex];
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split('=', 2);
                var rawKey = parts[0].Replace('+', ' ');
                var rawValue = (parts.Length > 1 ? parts[1] : string.Empty).Replace('+', ' ');
                if (string.IsNullOrWhiteSpace(rawKey)
                    || !HasValidPercentEncoding(rawKey)
                    || !HasValidPercentEncoding(rawValue))
                {
                    continue;
                }

                values[Uri.UnescapeDataString(rawKey)] = Uri.UnescapeDataString(rawValue);
            }

            return values;
        }
        catch (UriFormatException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool HasValidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }

    private static long? TryPositiveLong(string? value)
        => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    private static string? NormalizeValue(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > maximumLength
            || normalized.Any(char.IsControl))
        {
            return null;
        }

        return normalized;
    }
}

public static class ShipperRequestNavigationQueryNames
{
    public const string SourcePostId = "sourcePostId";
    public const string Source = "source";
    public const string SourceMarkerId = "sourceMarkerId";
    public const string LedgerTemplateKey = "ledgerTemplateKey";
    public const string NodeTitle = "nodeTitle";
    public const string NodeKind = "nodeKind";
    public const string CountryCode = "country";
}
