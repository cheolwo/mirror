using System.Text.Json;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify;

public interface IApifyAmazonProductClient
{
    Task<ApifyAmazon상품상세응답?> 상품상세조회Async(
        Uri 상품Url,
        CancellationToken cancellationToken);
}

public sealed record ApifyAmazon가격응답(
    decimal? 금액,
    string? 통화코드);

public sealed record ApifyAmazon속성응답(
    string 항목명,
    string 값);

public sealed record ApifyAmazon상품상세응답(
    string Asin,
    string? OriginalAsin,
    string 상품명,
    string? 브랜드명,
    string? 원문Url,
    string? 국가코드,
    ApifyAmazon가격응답 현재가격,
    ApifyAmazon가격응답 정가,
    ApifyAmazon가격응답 배송비,
    bool? 재고여부,
    string? 재고표시문구,
    decimal? 평점,
    int? 리뷰수,
    string? 카테고리경로,
    string? 썸네일Url,
    IReadOnlyList<string> 이미지Url목록,
    IReadOnlyList<string> 특징목록,
    IReadOnlyList<ApifyAmazon속성응답> 속성목록);

public sealed class ApifyAmazonProductClient : IApifyAmazonProductClient
{
    private readonly IApifyActorGateway _gateway;
    private readonly ApifyAmazonOptions _options;

    public ApifyAmazonProductClient(
        IApifyActorGateway gateway,
        IOptions<ApifyAmazonOptions> options)
    {
        _gateway = gateway;
        _options = options.Value;
    }

    public async Task<ApifyAmazon상품상세응답?> 상품상세조회Async(
        Uri 상품Url,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var input = JsonSerializer.SerializeToElement(new
        {
            categoryOrProductUrls = new[] { new { url = 상품Url.AbsoluteUri } },
            maxItemsPerStartUrl = 1,
            proxyCountry = "AUTO_SELECT_PROXY_COUNTRY",
            maxSearchPagesPerStartUrl = 1,
            maxProductVariantsAsSeparateResults = 0,
            maxOffers = 0,
            scrapeSellers = false,
            scrapeProductVariantPrices = false,
            scrapeProductDetails = true,
            locationDeliverableRoutes = new[] { "PRODUCT" }
        });
        var result = await _gateway.RunSyncGetDatasetItemsAsync(
            new ApifyActorSyncRequest(
                _options.ActorId,
                input,
                _options.ActorTimeoutSeconds,
                _options.MemoryMegabytes,
                Math.Clamp(_options.MaxDatasetItems, 1, 10),
                _options.MaxTotalChargeUsd),
            cancellationToken);
        if (result.Items.Count == 0)
        {
            return null;
        }

        var item = result.Items[0];
        var asin = GetString(item, "asin");
        var title = GetString(item, "title");
        if (string.IsNullOrWhiteSpace(asin) || string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Apify 응답에 ASIN 또는 상품명이 없습니다.");
        }

        return new ApifyAmazon상품상세응답(
            asin.Trim().ToUpperInvariant(),
            Normalize(GetString(item, "originalAsin"), 20),
            Normalize(title, 500)!,
            Normalize(GetString(item, "brand"), 200),
            Normalize(GetString(item, "url"), 2_000),
            NormalizeCountryCode(GetString(item, "loadedCountryCode")),
            GetPrice(item, "price"),
            GetPrice(item, "listPrice"),
            GetPrice(item, "shippingPrice"),
            GetBoolean(item, "inStock"),
            Normalize(GetString(item, "inStockText"), 500),
            GetDecimal(item, "stars"),
            GetInt32(item, "reviewsCount"),
            Normalize(GetString(item, "breadCrumbs"), 1_000),
            Normalize(GetString(item, "thumbnailImage"), 2_000),
            GetImages(item),
            GetFeatures(item),
            GetAttributes(item));
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Apify Amazon 상품 참고자료 조회가 비활성화되어 있습니다.");
        }
    }

    private IReadOnlyList<string> GetImages(JsonElement item)
    {
        var max = Math.Clamp(_options.MaxImageCount, 1, 20);
        var images = new List<string>();
        AddString(images, GetString(item, "thumbnailImage"), 2_000);
        AddStringArray(images, item, "highResolutionImages", 2_000);
        AddStringArray(images, item, "galleryThumbnails", 2_000);
        return images
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();
    }

    private IReadOnlyList<string> GetFeatures(JsonElement item)
    {
        if (!item.TryGetProperty("features", out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => Normalize(element.GetString(), 700))
            .Where(text => text is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Clamp(_options.MaxFeatureCount, 1, 20))
            .ToArray();
    }

    private IReadOnlyList<ApifyAmazon속성응답> GetAttributes(JsonElement item)
    {
        var result = new List<ApifyAmazon속성응답>();
        AddAttributes(result, item, "productOverview");
        AddAttributes(result, item, "attributes");

        return result
            .GroupBy(
                attribute => $"{attribute.항목명}\u001f{attribute.값}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(Math.Clamp(_options.MaxAttributeCount, 1, 80))
            .ToArray();
    }

    private static void AddAttributes(
        ICollection<ApifyAmazon속성응답> target,
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = Normalize(GetString(element, "key"), 200);
            var text = Normalize(GetString(element, "value"), 1_000);
            if (key is not null && text is not null)
            {
                target.Add(new ApifyAmazon속성응답(key, text));
            }
        }
    }

    private static ApifyAmazon가격응답 GetPrice(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return new ApifyAmazon가격응답(null, null);
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return new ApifyAmazon가격응답(value.TryGetDecimal(out var number) ? number : null, null);
        }

        return value.ValueKind == JsonValueKind.Object
            ? new ApifyAmazon가격응답(
                GetDecimal(value, "value"),
                Normalize(GetString(value, "currency"), 10)?.ToUpperInvariant())
            : new ApifyAmazon가격응답(null, null);
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var number)
                ? number
                : null;

    private static int? GetInt32(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (value.TryGetInt32(out var integer))
        {
            return integer;
        }

        return value.TryGetDecimal(out var number)
            ? (int)Math.Clamp(number, 0, int.MaxValue)
            : null;
    }

    private static bool? GetBoolean(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }
            : null;

    private static void AddString(ICollection<string> target, string? value, int maxLength)
    {
        var normalized = Normalize(value, maxLength);
        if (normalized is not null)
        {
            target.Add(normalized);
        }
    }

    private static void AddStringArray(
        ICollection<string> target,
        JsonElement item,
        string propertyName,
        int maxLength)
    {
        if (!item.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                AddString(target, element.GetString(), maxLength);
            }
        }
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? NormalizeCountryCode(string? value)
    {
        var normalized = Normalize(value, 2)?.ToUpperInvariant();
        return normalized?.Length == 2 ? normalized : null;
    }

}
