using System.Globalization;

namespace Ssalddel.Contracts.Common.Inbound;

public enum InboundRequestScreenKind
{
    List,
    Create,
    Detail,
    Complete,
    WarehouseRegistration
}

/// <summary>Web과 모바일이 공유하는 입고 요청·창고 등록 page route입니다.</summary>
public static class InboundRequestPageRoutes
{
    public const string Root = "/shipper/inbound/requests";
    public const string Create = $"{Root}/new";
    public const string DetailTemplate = $"{Root}/{{InboundId:long}}";
    public const string CompleteTemplate = $"{Root}/{{InboundId:long}}/complete";
    public const string WarehouseRegistration = "/shipper/warehouses/new";

    public static string DetailFor(long inboundId)
        => $"{Root}/{RequireInboundId(inboundId).ToString(CultureInfo.InvariantCulture)}";

    public static string CompleteFor(long inboundId)
        => $"{DetailFor(inboundId)}/complete";

    private static long RequireInboundId(long inboundId)
        => inboundId > 0
            ? inboundId
            : throw new ArgumentOutOfRangeException(nameof(inboundId), "입고 요청 ID는 1 이상이어야 합니다.");
}

/// <summary>
/// 다이어그램 창고 후보와 입고 신청 초안을 route 사이에 보존합니다.
/// 복귀 위치는 현재 앱 안의 local path만 허용합니다.
/// </summary>
public sealed record InboundRequestNavigationContext
{
    public bool DetailedEntry { get; init; }
    public Guid? PhotoRequestId { get; init; }
    public bool Created { get; init; }
    public string? From { get; init; }
    public string? Source { get; init; }
    public string? SourceMarkerId { get; init; }
    public long? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? ProxyType { get; init; }
    public string? WarehouseAddress { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierName { get; init; }
    public string? OrderReference { get; init; }
    public DateTime? ExpectedArrivalDate { get; init; }
    public string? Notes { get; init; }
    public string? ContractNo { get; init; }
    public string? ContractType { get; init; }
    public string? ContractCounterpartyName { get; init; }
    public string? ContractSettlementType { get; init; }
    public decimal? ContractCommissionRate { get; init; }
    public decimal? ContractDailyStorageFee { get; init; }
    public string? NodeTitle { get; init; }
    public string? NodeGroup { get; init; }
    public string? NodeDescription { get; init; }
    public string? Scope { get; init; }

    public string ResolveReturnPath(string fallback)
        => IsSafeLocalPath(From) ? From! : fallback;

    public string PathFor(InboundRequestScreenKind screen, long? inboundId = null)
    {
        var path = screen switch
        {
            InboundRequestScreenKind.List => InboundRequestPageRoutes.Root,
            InboundRequestScreenKind.Create => InboundRequestPageRoutes.Create,
            InboundRequestScreenKind.Detail when inboundId is > 0 => InboundRequestPageRoutes.DetailFor(inboundId.Value),
            InboundRequestScreenKind.Complete when inboundId is > 0 => InboundRequestPageRoutes.CompleteFor(inboundId.Value),
            InboundRequestScreenKind.WarehouseRegistration => InboundRequestPageRoutes.WarehouseRegistration,
            _ => throw new ArgumentException("이 화면에는 유효한 입고 요청 ID가 필요합니다.", nameof(inboundId))
        };

        return AddQuery(
            path,
            includeCreated: screen == InboundRequestScreenKind.Detail,
            includeDraft: screen is InboundRequestScreenKind.Create or InboundRequestScreenKind.WarehouseRegistration);
    }

    public static InboundRequestNavigationContext Parse(string? uri)
    {
        var query = ParseQuery(uri);
        return new InboundRequestNavigationContext
        {
            DetailedEntry = ParseBool(query, "details"),
            PhotoRequestId = Guid.TryParse(Get(query, "photoRequestId"), out var photoId) && photoId != Guid.Empty ? photoId : null,
            Created = ParseBool(query, "created"),
            From = SafeLocalOrNull(Get(query, "from")),
            Source = Get(query, "source"),
            SourceMarkerId = Get(query, "sourceMarkerId"),
            WarehouseId = ParseLong(query, "warehouseId"),
            WarehouseName = Get(query, "warehouseName"),
            ProxyType = Get(query, "proxyType"),
            WarehouseAddress = Get(query, "warehouseAddress"),
            SupplierCode = Get(query, "supplierCode"),
            SupplierName = Get(query, "supplierName"),
            OrderReference = Get(query, "orderReference"),
            ExpectedArrivalDate = ParseDate(query, "expectedArrivalDate"),
            Notes = Get(query, "notes"),
            ContractNo = Get(query, "contractNo"),
            ContractType = Get(query, "contractType"),
            ContractCounterpartyName = Get(query, "contractCounterpartyName"),
            ContractSettlementType = Get(query, "contractSettlementType"),
            ContractCommissionRate = ParseDecimal(query, "contractCommissionRate"),
            ContractDailyStorageFee = ParseDecimal(query, "contractDailyStorageFee"),
            NodeTitle = Get(query, "nodeTitle"),
            NodeGroup = Get(query, "nodeGroup"),
            NodeDescription = Get(query, "nodeDescription"),
            Scope = Get(query, "scope")
        };
    }

    public static bool IsSafeLocalPath(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.StartsWith("/", StringComparison.Ordinal)
           && !value.StartsWith("//", StringComparison.Ordinal)
           && !value.Contains("\\", StringComparison.Ordinal)
           && !value.Contains('\r')
           && !value.Contains('\n');

    private string AddQuery(string path, bool includeCreated, bool includeDraft)
    {
        var values = new List<string>();
        Add(values, "created", includeCreated && Created ? "true" : null);
        Add(values, "from", SafeLocalOrNull(From));
        Add(values, "source", Source);
        Add(values, "sourceMarkerId", SourceMarkerId);
        if (!includeDraft)
        {
            return values.Count == 0 ? path : $"{path}?{string.Join('&', values)}";
        }

        Add(values, "warehouseId", WarehouseId?.ToString(CultureInfo.InvariantCulture));
        Add(values, "details", DetailedEntry ? "true" : null);
        Add(values, "photoRequestId", PhotoRequestId?.ToString("D"));
        Add(values, "warehouseName", WarehouseName);
        Add(values, "proxyType", ProxyType);
        Add(values, "warehouseAddress", WarehouseAddress);
        Add(values, "supplierCode", SupplierCode);
        Add(values, "supplierName", SupplierName);
        Add(values, "orderReference", OrderReference);
        Add(values, "expectedArrivalDate", ExpectedArrivalDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(values, "notes", Notes);
        Add(values, "contractNo", ContractNo);
        Add(values, "contractType", ContractType);
        Add(values, "contractCounterpartyName", ContractCounterpartyName);
        Add(values, "contractSettlementType", ContractSettlementType);
        Add(values, "contractCommissionRate", ContractCommissionRate?.ToString("0.####", CultureInfo.InvariantCulture));
        Add(values, "contractDailyStorageFee", ContractDailyStorageFee?.ToString("0.####", CultureInfo.InvariantCulture));
        Add(values, "nodeTitle", NodeTitle);
        Add(values, "nodeGroup", NodeGroup);
        Add(values, "nodeDescription", NodeDescription);
        Add(values, "scope", Scope);
        return values.Count == 0 ? path : $"{path}?{string.Join('&', values)}";
    }

    private static void Add(ICollection<string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string? uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(uri))
        {
            return result;
        }

        var queryStart = uri.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0 || queryStart == uri.Length - 1)
        {
            return result;
        }

        var query = uri[(queryStart + 1)..];
        var fragmentStart = query.IndexOf('#', StringComparison.Ordinal);
        if (fragmentStart >= 0)
        {
            query = query[..fragmentStart];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[Decode(key)] = Decode(value);
            }
        }

        return result;
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> query, string key)
        => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string? SafeLocalOrNull(string? value)
        => IsSafeLocalPath(value) ? value!.Trim() : null;

    private static bool ParseBool(IReadOnlyDictionary<string, string> query, string key)
        => bool.TryParse(Get(query, key), out var value) && value;

    private static long? ParseLong(IReadOnlyDictionary<string, string> query, string key)
        => long.TryParse(Get(query, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

    private static decimal? ParseDecimal(IReadOnlyDictionary<string, string> query, string key)
        => decimal.TryParse(Get(query, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static DateTime? ParseDate(IReadOnlyDictionary<string, string> query, string key)
        => DateTime.TryParseExact(
            Get(query, key),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : null;
}
