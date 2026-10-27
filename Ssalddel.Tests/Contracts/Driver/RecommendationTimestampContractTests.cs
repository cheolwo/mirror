using System.Text.Json;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Hubs;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Contracts.Driver;

public sealed class RecommendationTimestampContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime StartedUtc = new DateTime(2026, 10, 19, 0, 14, 30, DateTimeKind.Utc).AddTicks(6823340);

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void 서버_추천시각은_순간과_정밀도를_유지하고_UTC_응답으로_전달한다(DateTimeKind sourceKind)
    {
        var expiresUtc = StartedUtc.AddMinutes(5);
        var server = new DispatchRecommendationDto
        {
            의뢰Id = "quick-utc", 추천시작시각 = WithKind(StartedUtc, sourceKind), 추천만료시각 = WithKind(expiresUtc, sourceKind),
            기사지급예정운임 = 6200m, 기사지급예정운임근거 = "운임구성.기사지급예정운임",
            픽업_위도 = 37m, 픽업_경도 = 127m, 하차_위도 = 37.01m, 하차_경도 = 127.01m
        };
        Assert.Equal(DateTimeKind.Utc, server.추천시작시각!.Value.Kind);
        Assert.Equal(StartedUtc.Ticks, server.추천시작시각.Value.Ticks);
        Assert.Equal(DateTimeKind.Utc, server.추천만료시각!.Value.Kind);
        Assert.Equal(expiresUtc.Ticks, server.추천만료시각.Value.Ticks);

        var wire = JsonSerializer.Serialize(server, Json);
        using var document = JsonDocument.Parse(wire);
        Assert.EndsWith("Z", document.RootElement.GetProperty("추천시작시각").GetString());
        Assert.EndsWith("Z", document.RootElement.GetProperty("추천만료시각").GetString());
        var client = JsonSerializer.Deserialize<기사배차추천항목응답>(wire, Json)!;
        Assert.Equal(DateTimeKind.Utc, client.추천시작시각!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, client.추천만료시각!.Value.Kind);
        Assert.Equal(StartedUtc, client.추천시작시각);
        Assert.Equal(expiresUtc, client.추천만료시각);
        Assert.Equal(6200m, client.기사지급예정운임);
        Assert.Equal("운임구성.기사지급예정운임", client.기사지급예정운임근거);

        var card = CargoDriverWorkspaceAdapter.MapOffer(client);
        Assert.Equal(2, card.Markers!.Count);
        Assert.All(card.Markers, marker =>
        {
            Assert.Equal(TimeSpan.Zero, marker.ExpiresAt!.Value.Offset);
            Assert.Equal(expiresUtc, marker.ExpiresAt.Value.UtcDateTime);
        });
        Assert.Equal(new DateTimeOffset(expiresUtc), Assert.Single(card.Routes!).ExpiresAt);
    }

    [Theory]
    [InlineData("2026-10-19T00:14:30.682334", "2026-10-19T00:19:30.682334")]
    [InlineData("2026-10-19T00:14:30.682334Z", "2026-10-19T00:19:30.682334Z")]
    [InlineData("2026-10-19T09:14:30.682334+09:00", "2026-10-19T09:19:30.682334+09:00")]
    public void 구버전_무시간대와_명시적_오프셋_응답은_동일한_UTC로_읽는다(string start, string end)
    {
        var wire = JsonSerializer.Serialize(new Dictionary<string, string?> { ["추천시작시각"] = start, ["추천만료시각"] = end }, Json);
        var client = JsonSerializer.Deserialize<기사배차추천항목응답>(wire, Json)!;
        Assert.Equal(DateTimeKind.Utc, client.추천시작시각!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, client.추천만료시각!.Value.Kind);
        Assert.Equal(StartedUtc.Ticks, client.추천시작시각.Value.Ticks);
        Assert.Equal(StartedUtc.AddMinutes(5).Ticks, client.추천만료시각.Value.Ticks);
        Assert.Null(client.기사지급예정운임);
        Assert.Null(client.기사지급예정운임근거);
    }

    [Fact]
    public void 추천_시각과_보수_누락은_null을_유지하고_자동_보완하지_않는다()
    {
        var server = new DispatchRecommendationDto { 의뢰Id = "legacy" };
        var wire = JsonSerializer.Serialize(server, Json);
        var client = JsonSerializer.Deserialize<기사배차추천항목응답>(wire, Json)!;
        Assert.Null(server.추천시작시각); Assert.Null(server.추천만료시각);
        Assert.Null(client.추천시작시각); Assert.Null(client.추천만료시각);
        Assert.Null(server.기사지급예정운임); Assert.Null(server.기사지급예정운임근거);
        Assert.Null(client.기사지급예정운임); Assert.Null(client.기사지급예정운임근거);
        Assert.Null(기사추천시각Policy.Utc로정규화(null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 무시간대_추천의_공통앱은_원래_유효시간과_확인_조건을_유지한다(bool future)
    {
        var expiry = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(future ? 5 : -5), DateTimeKind.Unspecified);
        var oldWire = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["의뢰Id"] = "quick-utc", ["차량적합여부"] = true, ["추천라운드"] = 1,
            ["기사지급예정운임"] = 6200m, ["기사지급예정운임근거"] = "운임구성.기사지급예정운임",
            ["추천시작시각"] = DateTime.SpecifyKind(expiry.AddMinutes(-10), DateTimeKind.Unspecified),
            ["추천만료시각"] = expiry
        }, Json);
        var offer = JsonSerializer.Deserialize<기사배차추천항목응답>(oldWire, Json)!;
        var api = new ReadApi(offer);
        using var model = new CargoOfferActionViewModel(new CargoDriverWorkspaceApiClient(api), new Access());
        await model.LoadAsync("quick-utc");
        Assert.False(model.Lifetime.HasError);
        Assert.Equal(future, model.ServerCandidateAvailable);
        Assert.False(model.CanSubmit);
        model.ConditionsConfirmed = true;
        Assert.False(model.CanSubmit);
        model.AcceptConfirmed = true;
        Assert.Equal(future, model.CanSubmit);
        Assert.Equal(0, api.Writes);
        Assert.Equal(2, api.Reads);
    }

    private static DateTime WithKind(DateTime utc, DateTimeKind kind) => kind == DateTimeKind.Local
        ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, kind);

    private sealed class ReadApi(기사배차추천항목응답 offer) : IRoleWorkspaceApi
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Reads++;
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, roleKey);
            object result = path.EndsWith("/recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)[offer]
                : new 기사운송의뢰상세응답 { 의뢰Id = "quick-utc", 차량적합여부 = true, 배차상태 = "대기" };
            return Task.FromResult((T)result);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Writes++; throw new InvalidOperationException("시각 회귀 검증은 수락 요청을 보내지 않습니다."); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("quick-driver", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
