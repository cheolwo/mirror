using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using FoodApiTypes = global::Ssalddel.FoodApi.Services;
using FoodApiOptions = Ssalddel.FoodApi.Options.KakaoLocalOptions;
using Server = Ssalddel.Services.Food;
using ServerOptions = 살뜰.Services.Options.KakaoLocalOptions;

namespace Ssalddel.Tests.Services.Food;

public sealed class Kakao좌표변환ParityTests
{
    private const string BaseUrl = "https://kakao.invalid/fixture/";
    private const string AddressFixture = """
        {"documents":[
          {"address_name":"first address","x":"127.08","y":"37.5",
           "address":{"region_1depth_name":"address-1","region_2depth_name":"address-2","region_3depth_name":"address-3"},
           "road_address":{"address_name":"first road","region_1depth_name":"road-1","region_2depth_name":"road-2","region_3depth_name":"road-3"}},
          {"address_name":"second address","x":"128","y":"38"}
        ]}
        """;

    [Theory]
    [InlineData(false, "fixture-key")]
    [InlineData(true, "fixture-key")]
    [InlineData(false, "")]
    [InlineData(true, " \t")]
    public void 생성자는호스트별옵션과기존인증헤더동작을유지한다(bool foodApi, string apiKey)
    {
        using var http = new HttpClient(new RecordingHandler());
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "existing-fixture");

        _ = Create(foodApi, http, apiKey);

        Assert.Equal(new Uri(BaseUrl), http.BaseAddress);
        Assert.Equal(string.IsNullOrWhiteSpace(apiKey) ? "Bearer" : "KakaoAK",
            http.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal(string.IsNullOrWhiteSpace(apiKey) ? "existing-fixture" : apiKey,
            http.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, " \t\r\n")]
    [InlineData(true, " \t\r\n")]
    public async Task 빈주소는취소된토큰이어도HTTP없이null을반환한다(bool foodApi, string? address)
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = Create(foodApi, http);

        Assert.Null(await sut.Address(address!, cancellation.Token));
        Assert.Null(await sut.Coordinates(address!, cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 첫주소와도로명우선순위및원문쿼리를유지한다(bool foodApi)
    {
        using var culture = new CultureScope("en-US");
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(AddressFixture)));
        using var http = new HttpClient(handler);
        var sut = Create(foodApi, http);
        const string address = " 서울 공개도로 &검토 #1? ";

        Assert.Equal(new AddressValue("first address", "first road", "road-1", "road-2", "road-3", 37.5m, 127.08m),
            await sut.Address(address, default));
        Assert.Equal(((double)37.5m, (double)127.08m), (await sut.Coordinates(address, default))!.Value);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/fixture/v2/local/search/address.json", handler.Uri!.AbsolutePath);
        Assert.StartsWith("?query=%20", handler.Uri.Query);
        Assert.Equal(address, Uri.UnescapeDataString(handler.Uri.Query[7..]));
        Assert.Equal("KakaoAK fixture-key", handler.Authorization);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task 빈도로명객체도지번보다우선하고누락된도로명만지번으로내린다(bool foodApi, bool roadPresent)
    {
        var body = JsonSerializer.Serialize(new
        {
            documents = new[]
            {
                new
                {
                    address_name = (string?)null,
                    address = new { region_1depth_name = "address-1", region_2depth_name = "address-2", region_3depth_name = "address-3" },
                    road_address = roadPresent ? (object)new { } : null
                }
            }
        });
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(body)));
        using var http = new HttpClient(handler);

        Assert.Equal(new AddressValue(null, "", roadPresent ? "" : "address-1",
                roadPresent ? "" : "address-2", roadPresent ? "" : "address-3", null, null),
            await Create(foodApi, http).Address("fixture", default));
    }

    [Theory]
    [InlineData(false, "0", "37.5", "37.5", null)]
    [InlineData(true, "0", "37.5", "37.5", null)]
    [InlineData(false, "127.08", "invalid", null, "127.08")]
    [InlineData(true, "127.08", "invalid", null, "127.08")]
    [InlineData(false, "", "0", null, null)]
    [InlineData(true, "", "0", null, null)]
    public async Task 영좌표와숫자변환실패는null이며불완전좌표는변환하지않는다(
        bool foodApi, string x, string y, string? latitude, string? longitude)
    {
        using var culture = new CultureScope("en-US");
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(NumericAddress(x, y))));
        using var http = new HttpClient(handler);
        var sut = Create(foodApi, http);

        Assert.Equal(new AddressValue("numeric", "", "", "", "", Decimal(latitude), Decimal(longitude)),
            await sut.Address("fixture", default));
        Assert.Null(await sut.Coordinates("fixture", default));
    }

    [Theory]
    [InlineData(false, "en-US", "37.5", "127.08", "37.5", "127.08", "?x=127.08&y=37.5")]
    [InlineData(true, "en-US", "37.5", "127.08", "37.5", "127.08", "?x=127.08&y=37.5")]
    [InlineData(false, "de-DE", "37,5", "127,08", "37.5", "127.08", "?x=127,08&y=37,5")]
    [InlineData(true, "de-DE", "37,5", "127,08", "37.5", "127.08", "?x=127,08&y=37,5")]
    [InlineData(false, "de-DE", "37.5", "127.08", "375", "12708", "?x=127,08&y=37,5")]
    [InlineData(true, "de-DE", "37.5", "127.08", "375", "12708", "?x=127,08&y=37,5")]
    public async Task 현재문화권의숫자파싱과좌표URI를그대로유지한다(
        bool foodApi, string cultureName, string y, string x, string latitude, string longitude, string query)
    {
        using var culture = new CultureScope(cultureName);
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(NumericAddress(x, y))));
        using var http = new HttpClient(handler);
        var sut = Create(foodApi, http);

        Assert.Equal(new AddressValue("numeric", "", "", "", "", Decimal(latitude), Decimal(longitude)),
            await sut.Address("fixture", default));
        Assert.Equal(((double)Decimal(latitude)!.Value, (double)Decimal(longitude)!.Value),
            (await sut.Coordinates("fixture", default))!.Value);
        _ = await sut.Region(37.5m, 127.08m, default);
        Assert.Equal("/fixture/v2/local/geo/coord2regioncode.json", handler.Uri!.AbsolutePath);
        Assert.Equal(query, handler.Uri.Query);
    }

    [Theory]
    [InlineData(false, "B,h,H", 1)]
    [InlineData(true, "B,h,H", 1)]
    [InlineData(false, "H,h,B", 0)]
    [InlineData(true, "H,h,B", 0)]
    [InlineData(false, "B,A,B", 0)]
    [InlineData(true, "B,A,B", 0)]
    public async Task 행정동H는대소문자없이우선하고동률과H없음은원래순서를유지한다(
        bool foodApi, string regionTypes, int selectedIndex)
    {
        var body = JsonSerializer.Serialize(new
        {
            documents = regionTypes.Split(',').Select((type, index) => new
            {
                region_type = type,
                address_name = $"address-{index}",
                region_1depth_name = $"region1-{index}",
                region_2depth_name = $"region2-{index}",
                region_3depth_name = $"region3-{index}"
            })
        });
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(body)));
        using var http = new HttpClient(handler);

        Assert.Equal(new RegionValue($"region1-{selectedIndex}", $"region2-{selectedIndex}",
                $"region3-{selectedIndex}", $"address-{selectedIndex}"),
            await Create(foodApi, http).Region(37.5m, 127.08m, default));
    }

    [Theory]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    [InlineData(false, "{}")]
    [InlineData(true, "{}")]
    [InlineData(false, "{\"documents\":null}")]
    [InlineData(true, "{\"documents\":null}")]
    [InlineData(false, "{\"documents\":[]}")]
    [InlineData(true, "{\"documents\":[]}")]
    public async Task 응답과문서가없으면모든조회가null이다(bool foodApi, string body)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(body)));
        using var http = new HttpClient(handler);
        var sut = Create(foodApi, http);

        Assert.Null(await sut.Address("fixture", default));
        Assert.Null(await sut.Coordinates("fixture", default));
        Assert.Null(await sut.Region(37.5m, 127.08m, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 첫주소문서가null이면뒤문서를대신선택하지않는다(bool foodApi)
    {
        const string body = "{\"documents\":[null,{\"address_name\":\"later\",\"x\":\"127\",\"y\":\"37\"}]}";
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(body)));
        using var http = new HttpClient(handler);

        Assert.Null(await Create(foodApi, http).Address("fixture", default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 지역문서의명시null문자열을새로보정하지않는다(bool foodApi)
    {
        const string body = "{\"documents\":[{\"region_type\":\"H\",\"address_name\":null,\"region_1depth_name\":null}]}";
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(body)));
        using var http = new HttpClient(handler);

        Assert.Equal(new RegionValue(null, "", "", null),
            await Create(foodApi, http).Region(37.5m, 127.08m, default));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HTTP실패상태는예외로전파하며다시호출하지않는다(bool foodApi, bool region)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw("{}", HttpStatusCode.ServiceUnavailable)));
        using var http = new HttpClient(handler);
        var sut = Create(foodApi, http);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Invoke(sut, region, default));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task 잘못된JSON은예외로전파하며다시호출하지않는다(bool foodApi, bool region)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw("{\"documents\":")));
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<JsonException>(() => Invoke(Create(foodApi, http), region, default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task 네트워크예외는전파하며다시호출하지않는다(bool foodApi, bool region)
    {
        var expected = new HttpRequestException("fixture network failure");
        var handler = new RecordingHandler((_, _) => Task.FromException<HttpResponseMessage>(expected));
        using var http = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Invoke(Create(foodApi, http), region, default));

        Assert.Same(expected, error);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task 호출자취소는실제HTTP토큰으로전파한다(bool foodApi, bool region)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Raw("{}");
        });
        using var http = new HttpClient(handler);
        var task = Invoke(Create(foodApi, http), region, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, handler.Calls);
        Assert.True(handler.Token.IsCancellationRequested);
    }

    private static async Task Invoke(Facade client, bool region, CancellationToken token)
    {
        if (region) _ = await client.Region(37.5m, 127.08m, token);
        else _ = await client.Address("fixture", token);
    }

    private static Facade Create(bool foodApi, HttpClient http, string apiKey = "fixture-key")
    {
        if (foodApi)
        {
            var client = new FoodApiTypes.Kakao좌표변환Service(http,
                Options.Create(new FoodApiOptions { BaseUrl = BaseUrl, RestApiKey = apiKey }));
            return new(
                async (address, token) => Address(await client.주소정보조회Async(address, token)),
                client.도로명주소좌표변환Async,
                async (latitude, longitude, token) => Region(await client.좌표지역정보조회Async(latitude, longitude, token)));
        }

        var server = new Server.Kakao좌표변환Service(http,
            Options.Create(new ServerOptions { BaseUrl = BaseUrl, RestApiKey = apiKey }));
        return new(
            async (address, token) => Address(await server.주소정보조회Async(address, token)),
            server.도로명주소좌표변환Async,
            async (latitude, longitude, token) => Region(await server.좌표지역정보조회Async(latitude, longitude, token)));
    }

    private static AddressValue? Address(Server.Kakao주소정보? value) => value is null ? null
        : new(value.전체주소, value.도로명주소, value.Region1, value.Region2, value.Region3, value.위도, value.경도);
    private static AddressValue? Address(FoodApiTypes.Kakao주소정보? value) => value is null ? null
        : new(value.전체주소, value.도로명주소, value.Region1, value.Region2, value.Region3, value.위도, value.경도);
    private static RegionValue? Region(Server.Kakao지역정보? value) => value is null ? null
        : new(value.Region1, value.Region2, value.Region3, value.AddressName);
    private static RegionValue? Region(FoodApiTypes.Kakao지역정보? value) => value is null ? null
        : new(value.Region1, value.Region2, value.Region3, value.AddressName);

    private static decimal? Decimal(string? value) => value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    private static string NumericAddress(string x, string y) => JsonSerializer.Serialize(new
    {
        documents = new[] { new { address_name = "numeric", x, y } }
    });
    private static HttpResponseMessage Raw(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record Facade(
        Func<string, CancellationToken, Task<AddressValue?>> Address,
        Func<string, CancellationToken, Task<(double 위도, double 경도)?>> Coordinates,
        Func<decimal, decimal, CancellationToken, Task<RegionValue?>> Region);
    private sealed record AddressValue(string? 전체주소, string 도로명주소, string Region1, string Region2,
        string Region3, decimal? 위도, decimal? 경도);
    private sealed record RegionValue(string? Region1, string? Region2, string? Region3, string? AddressName);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
        public CultureScope(string name) => CultureInfo.CurrentCulture = new CultureInfo(name);
        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? response = null)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? Uri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Authorization { get; private set; }
        public CancellationToken Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Uri = request.RequestUri;
            Method = request.Method;
            Authorization = request.Headers.Authorization?.ToString();
            Token = token;
            return response?.Invoke(request, token) ?? Task.FromResult(Raw("{}"));
        }
    }
}
