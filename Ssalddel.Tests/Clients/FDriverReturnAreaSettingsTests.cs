using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverReturnAreaSettingsTests
{
    [Fact]
    public async Task 저장은고정code와확인한판본을보내고서버재조회뒤적용한다()
    {
        var session = new FDriverTestSession(); var current = Settings(); var reads = 0;
        기사권역복귀설정수정요청? written = null;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            Assert.Equal("/api/v1/driver/return-area/settings", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Put)
            {
                written = await request.Content!.ReadFromJsonAsync<기사권역복귀설정수정요청>(token);
                current.설정 = Copy(written!.설정!); current.설정.Revision++;
            }
            else reads++;
            return Ok(current);
        });
        var model = Model(handler, session);
        await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        model.ReturnAreaModeIndex = 2; model.ReturnAreaEntryRadiusText = "8";
        await model.SaveReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.Equal(4, written!.ExpectedRevision); Assert.Equal(4, written.설정!.Revision);
        Assert.Equal("on", written.설정.Mode); Assert.Equal(8, written.설정.진입반경Km); Assert.Equal(6, written.설정.해제반경Km);
        Assert.Equal(2, reads); Assert.True(model.ReturnAreaSettingsLoaded); Assert.Contains("확인했습니다", model.ReturnAreaSettingsNotice);
        Assert.False(model.IsOnDuty); Assert.False(model.HasCurrentLocation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장응답손실또는확인조회실패는재조회전에다시저장하지않는다(bool loseWriteReply)
    {
        var session = new FDriverTestSession(); var reads = 0; var writes = 0; var current = Settings();
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                writes++; current.설정.Revision++;
                if (loseWriteReply) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            else if (++reads > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(Ok(current));
        });
        var model = Model(handler, session); await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        await model.SaveReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.False(model.ReturnAreaSettingsLoaded); Assert.Contains("다시 조회", model.ReturnAreaSettingsNotice);
        await model.SaveReturnAreaSettingsCommand.ExecuteAsync(null); Assert.Equal(1, writes);
    }

    [Fact]
    public async Task 현재위치선택은명시적기준점만바꾸고현재GPS나운행API를갱신하지않는다()
    {
        var session = new FDriverTestSession(); var current = Settings(); 기사권역복귀설정수정요청? written = null;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            Assert.Equal("/api/v1/driver/return-area/settings", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Put)
            { written = await request.Content!.ReadFromJsonAsync<기사권역복귀설정수정요청>(token); current.설정 = Copy(written!.설정!); current.설정.Revision++; }
            return Ok(current);
        });
        var model = Model(handler, session, new FDriverFixedTestLocationService());
        await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        await model.UseCurrentLocationForReturnAreaCommand.ExecuteAsync(null);
        Assert.False(model.HasCurrentLocation);
        Assert.Contains("저장", model.ReturnAreaAnchorText);
        await model.SaveReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.Equal(37.49m, written!.설정!.기준위도); Assert.Equal(127m, written.설정.기준경도);
        Assert.DoesNotContain("37.49", model.ReturnAreaAnchorText);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("missing-accuracy")]
    [InlineData("negative-accuracy")]
    [InlineData("inaccurate")]
    public async Task 오래되거나정확도없는위치는선택하지않고기존기준점과사용방식을보존한다(string kind)
    {
        var session = new FDriverTestSession(); var current = Settings(); 기사권역복귀설정수정요청? written = null;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            { written = await request.Content!.ReadFromJsonAsync<기사권역복귀설정수정요청>(token); current.설정 = Copy(written!.설정!); current.설정.Revision++; }
            return Ok(current);
        });
        var reading = new FDriverLocationSnapshot(38m, 128m,
            kind switch { "missing-accuracy" => null, "negative-accuracy" => -1m, "inaccurate" => 101m, _ => 5m },
            kind == "stale" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow);
        var model = Model(handler, session, new Location(reading));
        await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        await model.UseCurrentLocationForReturnAreaCommand.ExecuteAsync(null);
        Assert.Contains("다시 시도", model.ReturnAreaSettingsNotice);
        await model.SaveReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.Equal(37.5m, written!.설정!.기준위도); Assert.Equal("automatic", written.설정.Mode);
    }

    [Fact]
    public async Task 계정이바뀌면이전기준점은즉시숨기고늦은응답을적용하지않는다()
    {
        var session = new FDriverTestSession(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler((_, _) => { entered.SetResult(); return delayed.Task; });
        var model = Model(handler, session); var load = model.LoadReturnAreaSettingsCommand.ExecuteAsync(null); await entered.Task;
        await session.ApplyAsync(new ClientAuthTokenSnapshot("other-access", DateTime.UtcNow.AddMinutes(30), "other-refresh", DateTime.UtcNow.AddHours(1), "other", "다른 기사", ["Driver"]));
        delayed.SetResult(Ok(Settings())); await load;
        Assert.False(model.ReturnAreaSettingsLoaded); Assert.False(model.ReturnAreaSettingsBusy);
        Assert.Equal("복귀 기준점 미설정", model.ReturnAreaAnchorText); Assert.Equal(0, model.ReturnAreaModeIndex);
    }

    [Fact]
    public async Task 다른기사설정응답은저장할수없고로그아웃은기준점을초기화한다()
    {
        var session = new FDriverTestSession(); var current = Settings(); current.DriverId = "other";
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(Ok(current)));
        var model = Model(handler, session); await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.False(model.CanEditReturnAreaSettings);
        current.DriverId = "test-driver";
        await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null); Assert.True(model.ReturnAreaSettingsLoaded);
        await session.ClearAsync(); Assert.False(model.ReturnAreaSettingsLoaded);
        Assert.Equal("복귀 기준점 미설정", model.ReturnAreaAnchorText);
    }

    [Theory]
    [InlineData("radius")]
    [InlineData("name")]
    public async Task 서버제한을넘은기준점응답을저장가능한설정으로사용하지않는다(string field)
    {
        var current = Settings();
        if (field == "radius") current.설정.진입반경Km = 101;
        else current.설정.기준위치명 = new string('가', 81);
        var model = Model(new FDriverTestHttpHandler((_, _) => Task.FromResult(Ok(current))), new());
        await model.LoadReturnAreaSettingsCommand.ExecuteAsync(null);
        Assert.False(model.CanEditReturnAreaSettings); Assert.False(model.ReturnAreaSettingsLoaded);
    }

    private static HttpResponseMessage Ok(기사권역복귀설정응답 settings) => new(HttpStatusCode.OK) { Content = JsonContent.Create(settings) };
    private static 기사권역복귀설정 Copy(기사권역복귀설정 value) => JsonSerializer.Deserialize<기사권역복귀설정>(JsonSerializer.Serialize(value))!;
    private static 기사권역복귀설정응답 Settings() => new()
    {
        DriverId = "test-driver", 설정 = new() { Revision = 4, Mode = "automatic", 기준위치명 = "활동 중심", 기준위도 = 37.5m, 기준경도 = 127m }
    };
    private static MainPageModel Model(FDriverTestHttpHandler handler, FDriverTestSession session, IFDriverLocationService? gps = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") };
        var auth = new FDriverAuthApiService(http, session);
        return new(new(), session, auth, new FoodDeliveryDriverApiService(http, session, auth), gps ?? new FDriverTestLocationService(), new());
    }
    private sealed class Location(FDriverLocationSnapshot reading) : IFDriverLocationService
    {
        public bool IsListening => false;
        public FDriverLocationSnapshot? LatestLocation => reading;
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged { add { } remove { } }
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<FDriverLocationSnapshot?>(reading);
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false) => Task.FromResult(false);
        public void StopListening() { }
    }
}
