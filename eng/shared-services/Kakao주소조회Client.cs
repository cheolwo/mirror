using System.Net.Http.Json;

namespace Ssalddel.SharedServices.Kakao;

/// <summary>두 API 호스트가 같은 HTTP 요청과 응답 해석을 사용하도록 연결합니다.</summary>
internal sealed class Kakao주소조회Client
{
    private readonly HttpClient _httpClient;

    internal Kakao주소조회Client(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Kakao주소조회Result?> 주소정보조회Async(string 주소, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(주소))
        {
            return null;
        }

        var requestUri = $"v2/local/search/address.json?query={Uri.EscapeDataString(주소)}";
        var response = await _httpClient.GetFromJsonAsync<KakaoAddressSearchResponse>(requestUri, cancellationToken);
        var first = response?.documents?.FirstOrDefault();
        return first?.To주소정보();
    }

    public async Task<Kakao지역조회Result?> 좌표지역정보조회Async(decimal 위도, decimal 경도, CancellationToken cancellationToken = default)
    {
        var requestUri = $"v2/local/geo/coord2regioncode.json?x={경도}&y={위도}";
        var response = await _httpClient.GetFromJsonAsync<KakaoRegionCodeResponse>(requestUri, cancellationToken);
        var first = response?.documents?
            .OrderByDescending(x => string.Equals(x.region_type, "H", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return first is null
            ? null
            : new Kakao지역조회Result(
                first.region_1depth_name,
                first.region_2depth_name,
                first.region_3depth_name,
                first.address_name);
    }

    private sealed class KakaoAddressSearchResponse
    {
        public List<KakaoAddressDocument>? documents { get; set; }
    }

    private sealed class KakaoRegionCodeResponse
    {
        public List<KakaoRegionCodeDocument>? documents { get; set; }
    }

    private sealed class KakaoRegionCodeDocument
    {
        public string region_type { get; set; } = string.Empty;
        public string address_name { get; set; } = string.Empty;
        public string region_1depth_name { get; set; } = string.Empty;
        public string region_2depth_name { get; set; } = string.Empty;
        public string region_3depth_name { get; set; } = string.Empty;
    }

    private sealed class KakaoAddressDocument
    {
        public string address_name { get; set; } = string.Empty;
        public string x { get; set; } = string.Empty;
        public string y { get; set; } = string.Empty;
        public KakaoAddress? address { get; set; }
        public KakaoRoadAddress? road_address { get; set; }

        public Kakao주소조회Result To주소정보()
        {
            var source = road_address as IKakaoRegionSource ?? address;
            _ = decimal.TryParse(y, out var latitude);
            _ = decimal.TryParse(x, out var longitude);

            return new Kakao주소조회Result(
                address_name,
                road_address?.address_name ?? string.Empty,
                source?.region_1depth_name ?? string.Empty,
                source?.region_2depth_name ?? string.Empty,
                source?.region_3depth_name ?? string.Empty,
                latitude == 0m ? null : latitude,
                longitude == 0m ? null : longitude);
        }
    }

    private interface IKakaoRegionSource
    {
        string region_1depth_name { get; }
        string region_2depth_name { get; }
        string region_3depth_name { get; }
    }

    private sealed class KakaoAddress : IKakaoRegionSource
    {
        public string address_name { get; set; } = string.Empty;
        public string region_1depth_name { get; set; } = string.Empty;
        public string region_2depth_name { get; set; } = string.Empty;
        public string region_3depth_name { get; set; } = string.Empty;
    }

    private sealed class KakaoRoadAddress : IKakaoRegionSource
    {
        public string address_name { get; set; } = string.Empty;
        public string region_1depth_name { get; set; } = string.Empty;
        public string region_2depth_name { get; set; } = string.Empty;
        public string region_3depth_name { get; set; } = string.Empty;
        public string road_name { get; set; } = string.Empty;
    }
}

internal sealed record Kakao주소조회Result(
    string 전체주소,
    string 도로명주소,
    string Region1,
    string Region2,
    string Region3,
    decimal? 위도,
    decimal? 경도);

internal sealed record Kakao지역조회Result(
    string Region1,
    string Region2,
    string Region3,
    string AddressName);
