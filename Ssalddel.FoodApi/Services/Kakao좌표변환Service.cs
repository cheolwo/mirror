using System.Net.Http.Headers;
using Ssalddel.SharedServices.Kakao;
using Ssalddel.FoodApi.Options;
using Microsoft.Extensions.Options;

namespace Ssalddel.FoodApi.Services;

public sealed class Kakao좌표변환Service : IKakao좌표변환Service
{
    private readonly HttpClient _httpClient;
    private readonly Kakao주소조회Client _client;

    public Kakao좌표변환Service(HttpClient httpClient, IOptions<KakaoLocalOptions> options)
    {
        _httpClient = httpClient;
        var setting = options.Value;

        _httpClient.BaseAddress = new Uri(setting.BaseUrl);
        if (!string.IsNullOrWhiteSpace(setting.RestApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("KakaoAK", setting.RestApiKey);
        }

        _client = new Kakao주소조회Client(_httpClient);
    }

    public async Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string 주소, CancellationToken cancellationToken = default)
    {
        var info = await 주소정보조회Async(주소, cancellationToken);
        if (info?.위도 is decimal latitude && info.경도 is decimal longitude)
        {
            return ((double)latitude, (double)longitude);
        }

        return null;
    }

    public async Task<Kakao주소정보?> 주소정보조회Async(string 주소, CancellationToken cancellationToken = default)
    {
        var info = await _client.주소정보조회Async(주소, cancellationToken);
        return info is null
            ? null
            : new Kakao주소정보(
                info.전체주소,
                info.도로명주소,
                info.Region1,
                info.Region2,
                info.Region3,
                info.위도,
                info.경도);
    }

    public async Task<Kakao지역정보?> 좌표지역정보조회Async(decimal 위도, decimal 경도, CancellationToken cancellationToken = default)
    {
        var info = await _client.좌표지역정보조회Async(위도, 경도, cancellationToken);
        return info is null
            ? null
            : new Kakao지역정보(
                info.Region1,
                info.Region2,
                info.Region3,
                info.AddressName);
    }
}
