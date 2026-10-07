using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleHandoffUiR21Tests
{
    [Theory]
    [InlineData("EntireTransport", "FullyHeld", true, "전체 운송", true)]
    [InlineData("AffectedQuantity", "PartiallyHeld", true, "영향 수량만", true)]
    [InlineData("None", "ContinueWithCaution", true, "보류 범위", true)]
    [InlineData("None", "Resumed", false, "", false)]
    public void 기본_보류_안내는_일부와_전체와_정산을_구별하고_원래_운송상태를_유지한다(string scope, string control, bool held, string expected, bool notice)
    {
        var request = new 화주운송의뢰응답 { 의뢰Id = "request-a", 운송상태 = "운송중", 비정상운송검토보류중 = held,
            비정상운송사건목록 = [new() { 상태Code = "ActionDecided", 업무통제상태Code = control, 보류범위Code = scope, 정산보류적용여부 = held }] };
        var field = CargoIncidentPresentation.Notice(request);
        Assert.Equal(notice, field is not null);
        Assert.Equal("운송중", ShipperWorkspaceAdapter.Map(request).Status);
        if (notice)
        {
            Assert.Contains(expected, field!.Value); Assert.Contains("정산도 보류", field.Value);
            Assert.Equal(field, ShipperWorkspaceAdapter.Map(request).Summary!.Request);
        }
        else Assert.Equal("기사 위치", ShipperWorkspaceAdapter.Map(request).Summary!.Request!.Label);
    }

    [Fact]
    public void 구판_보류_응답의_범위는_임의로_전체중단이나_정산보류로_채우지_않는다()
    {
        var value = CargoIncidentPresentation.Notice(new() { 비정상운송검토보류중 = true });
        Assert.Contains("보류 범위", value!.Value); Assert.DoesNotContain("전체 운송이", value.Value); Assert.DoesNotContain("정산도", value.Value);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task 사건_권한과_기능_거절은_빈_정상목록으로_숨기지_않는다(int code)
    {
        var api = IncidentApi(); using var model = new CargoIncidentReviewViewModel(api, new Access());
        await model.LoadAsync(); Assert.True(model.Loaded);
        api.Get = _ => throw new RoleWorkspaceAccessException(code, "거절");
        await model.LoadAsync();
        Assert.False(model.Loaded); Assert.Empty(model.Items); Assert.True(model.Lifetime.HasError);
        Assert.Equal(code == 401, model.Lifetime.RequiresLogin);
    }

    [Fact]
    public async Task 사건_빈_목록은_권한을_확인한_조회완료로_남는다()
    {
        var api = new Api { Get = _ => Task.FromResult<object>(Array.Empty<비정상운송사건운영응답>()) };
        using var model = new CargoIncidentReviewViewModel(api, new Access()); await model.LoadAsync();
        Assert.True(model.Loaded); Assert.Empty(model.Items); Assert.False(model.Lifetime.HasError);
        Assert.Equal("operator", Assert.Single(api.Reads).Role);
    }

    [Fact]
    public async Task 사건_결정은_확인한_내용을_고정하고_같은_사건을_재조회한_뒤_완료한다()
    {
        var api = IncidentApi(); using var model = await PreparedIncident(api);
        var id = model.RequestId; model.Decision = "TransportResumed"; model.Reason = "다른 입력";
        await model.SubmitAsync();
        var write = Assert.Single(api.Writes); var body = Assert.IsType<비정상운송사건검토요청>(write.Body);
        Assert.Equal("operator", write.Role); Assert.Equal(CargoIncidentReviewViewModel.Root + "/incident-a/decision", write.Path);
        Assert.Equal(id, body.클라이언트요청Id); Assert.Equal("EntireTransportHeld", body.결정Code); Assert.Equal("현장 확인", body.검토사유);
        Assert.True(model.Saved); Assert.Equal(2, api.Reads.Count);
    }

    [Fact]
    public async Task 사건_명령응답_유실은_같은_ID와_내용으로만_재시도한다()
    {
        var api = IncidentApi(); var put = api.Put;
        api.Put = (_, body) => api.Writes.Count == 1 ? throw new HttpRequestException("응답 유실") : put!("", body);
        using var model = await PreparedIncident(api); var id = model.RequestId;
        await model.SubmitAsync(); Assert.False(model.Saved); Assert.True(model.IsConfirming);
        model.Reason = "수정된 입력"; await model.SubmitAsync();
        Assert.True(model.Saved); Assert.Equal(2, api.Writes.Count);
        Assert.All(api.Writes, write => Assert.Equal(id, ((비정상운송사건검토요청)write.Body).클라이언트요청Id));
        Assert.All(api.Writes, write => Assert.Equal("현장 확인", ((비정상운송사건검토요청)write.Body).검토사유));
    }

    [Fact]
    public async Task 저장응답_후_조회실패는_명령을_다시_보내지_않는다()
    {
        var api = IncidentApi(); var get = api.Get;
        api.Get = path => api.Reads.Count == 2 ? throw new HttpRequestException("조회 실패") : get(path);
        using var model = await PreparedIncident(api); await model.SubmitAsync();
        Assert.True(model.AwaitingResult); Assert.False(model.Saved);
        await model.CheckResultAsync();
        Assert.True(model.Saved); Assert.Single(api.Writes); Assert.Equal(3, api.Reads.Count);
    }

    [Fact]
    public async Task Revision_충돌은_자동_덮어쓰기_대신_새_조회와_재확인을_요구한다()
    {
        var api = IncidentApi(); api.Put = (_, _) => throw new RoleWorkspaceAccessException(409, "충돌");
        using var model = await PreparedIncident(api); await model.SubmitAsync();
        Assert.False(model.Saved); Assert.False(model.Loaded); Assert.Null(model.Source); Assert.Null(model.RequestId);
        Assert.Contains("다시 조회", model.Message); await model.SubmitAsync(); Assert.Single(api.Writes);
    }

    [Fact]
    public async Task 잘못된_사건의_결정응답은_완료로_인증하지_않는다()
    {
        var api = IncidentApi(); api.Put = (_, _) => Task.FromResult<object?>(Incident(2, "other-incident"));
        using var model = await PreparedIncident(api); await model.SubmitAsync();
        Assert.False(model.Saved); Assert.False(model.AwaitingResult); Assert.True(model.Lifetime.HasError);
    }

    [Fact]
    public async Task 일부_보류_수량_합계가_다르면_명령을_만들지_않는다()
    {
        var api = IncidentApi(); using var model = new CargoIncidentReviewViewModel(api, new Access());
        await model.LoadAsync("incident-a"); model.Decision = "NormalAcceptedAffectedHeld"; model.Reason = "확인";
        model.NormalQuantity = 9; model.AffectedQuantity = 2; model.Prepare();
        Assert.Null(model.RequestId); Assert.Contains("수량", model.Message); Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task 사건_조회_중_계정_변경은_늦은_응답과_초안을_제거한다()
    {
        var reply = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new Api { Get = _ => reply.Task }; var access = new Access();
        using var model = new CargoIncidentReviewViewModel(api, access);
        var load = model.LoadAsync("incident-a"); access.Change(new("other", 2, true));
        reply.SetResult(new[] { Incident() }); await load;
        Assert.Empty(model.Items); Assert.Null(model.Source); Assert.False(model.Loaded);
    }

    [Fact]
    public async Task 음식_내역은_본인_한국_완료일_전체_원장만_읽는다()
    {
        var access = new Access(); var clock = new Clock();
        var api = new Api { Get = _ => Task.FromResult<object>(new FoodDeliveryDailySettlementDto
            { DriverId = "owner", CompletionDateKst = new(2026, 10, 6), OrderSettlements = [Settlement()] }) };
        using var model = new DriverHistoryViewModel("food-driver", api, access, clock); await model.LoadAsync();
        var read = Assert.Single(api.Reads); Assert.Equal("food-driver", read.Role);
        Assert.EndsWith("daily?date=2026-10-06", read.Path); Assert.Single(model.Items); Assert.Empty(api.Writes);
        Assert.Empty(model.Items.Single().Actions ?? []); Assert.Contains("입금 확인 전", model.Items.Single().Sections!.SelectMany(section => section.Fields).Select(field => field.Value));
    }

    [Theory]
    [InlineData("other", "settlement-a")]
    [InlineData("owner", "other-settlement")]
    public async Task 음식_상세의_계정이나_선택_ID가_다르면_정보를_표시하지_않는다(string owner, string id)
    {
        var value = FoodDetail(); value.Settlement.DriverId = owner; value.Settlement.SettlementId = id;
        var api = new Api { Get = _ => Task.FromResult<object>(value) };
        using var model = new DriverHistoryViewModel("food-driver", api, new Access()); await model.LoadAsync("settlement-a");
        Assert.Null(model.Detail); Assert.Null(model.CustomerDetails); Assert.True(model.Lifetime.HasError);
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("PolicyNotConfigured")]
    public async Task 열람_차단_응답에_개인정보가_들어와도_제거한다(string status)
    {
        var value = FoodDetail(); value.DetailAccessStatusCode = status;
        var api = new Api { Get = _ => Task.FromResult<object>(value) };
        using var model = new DriverHistoryViewModel("food-driver", api, new Access(), new Clock()); await model.LoadAsync("settlement-a");
        Assert.NotNull(model.Detail); Assert.Null(model.CustomerDetails); Assert.Null(model.OrderDetails);
        Assert.Null(value.CustomerDetails); Assert.Null(value.OrderDetails); Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task 화면을_열어둔_채_열람기한이_지나면_개인정보를_지우고_금액만_유지한다()
    {
        var clock = new Clock(); var value = FoodDetail();
        var api = new Api { Get = _ => Task.FromResult<object>(value) };
        using var model = new DriverHistoryViewModel("food-driver", api, new Access(), clock); await model.LoadAsync("settlement-a");
        Assert.NotNull(model.CustomerDetails); clock.Advance(61); model.ExpirePrivateDetails();
        Assert.Null(model.CustomerDetails); Assert.Null(model.OrderDetails); Assert.Null(value.CustomerDetails);
        Assert.NotNull(model.Detail); Assert.Single(api.Reads);
    }

    [Fact]
    public async Task 열람기한은_서버시각과_조회_소요시간으로_계산하고_기기시계에_의존하지_않는다()
    {
        var clock = new Clock(); var value = FoodDetail(); value.ServerNowUtc = value.ServerNowUtc.AddYears(-1);
        value.DetailExpiresAtUtc = value.ServerNowUtc.AddSeconds(60);
        var api = new Api { Get = _ => { clock.Advance(61); return Task.FromResult<object>(value); } };
        using var model = new DriverHistoryViewModel("food-driver", api, new Access(), clock); await model.LoadAsync("settlement-a");
        Assert.False(model.CanShowPrivateDetails); Assert.Null(model.CustomerDetails); Assert.Null(value.CustomerDetails);
    }

    [Fact]
    public async Task 내역의_계정_변경은_정산과_고객_정보도_함께_지운다()
    {
        var access = new Access(); var value = FoodDetail();
        using var model = new DriverHistoryViewModel("food-driver", new Api { Get = _ => Task.FromResult<object>(value) }, access, new Clock());
        await model.LoadAsync("settlement-a"); access.Change(new(null, 2, false));
        Assert.Null(model.Detail); Assert.Null(model.CustomerDetails); Assert.Null(value.CustomerDetails); Assert.True(model.Lifetime.RequiresLogin);
    }

    [Fact]
    public async Task 화물_내역은_종료건만_보이며_고객주소와_증빙원본을_표시하지_않는다()
    {
        var detail = new 기사운송상세응답 { Id = 11, 기사_운송자 = "owner", 상태 = "인수완료", 운임 = 34000,
            출발지 = "비공개 상차주소", 도착지 = "비공개 하차주소", 수령자연락처 = "비공개 번호", 첨부Json = "비공개 원본", UpdatedAt = DateTime.UtcNow };
        var api = new Api { Get = path => Task.FromResult<object>(path == "api/v1/driver/transports"
            ? new 기사운송요약응답[] { detail, new() { Id = 12, 기사_운송자 = "owner", 상태 = "운송중" }, new() { Id = 13, 기사_운송자 = "owner", 상태 = "중단" } } : detail) };
        using var model = new DriverHistoryViewModel("cargo-driver", api, new Access()); await model.LoadAsync();
        Assert.Equal(2, model.Items.Count); await model.LoadAsync("11");
        Assert.DoesNotContain(model.Detail!.Sections!.SelectMany(section => section.Fields), field => field.Value.Contains("비공개"));
        Assert.All(api.Reads, read => Assert.Equal("cargo-driver", read.Role)); Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task 내역_늦은_응답은_다른_계정_화면에_들어가지_않는다()
    {
        var reply = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously); var access = new Access();
        using var model = new DriverHistoryViewModel("cargo-driver", new Api { Get = _ => reply.Task }, access);
        var load = model.LoadAsync(); access.Change(new("other", 2, true));
        reply.SetResult(new 기사운송요약응답[] { new() { Id = 11, 기사_운송자 = "owner", 상태 = "인수완료" } });
        await load; Assert.Empty(model.Items); Assert.False(model.Loaded);
    }

    private static Api IncidentApi()
    {
        var source = Incident();
        return new Api { Get = _ => Task.FromResult<object>(new[] { source }), Put = (_, body) =>
        {
            var request = (비정상운송사건검토요청)body;
            source = Incident(2); source.해결결과Code = request.결정Code;
            return Task.FromResult<object?>(source);
        } };
    }
    private static async Task<CargoIncidentReviewViewModel> PreparedIncident(Api api)
    {
        var model = new CargoIncidentReviewViewModel(api, new Access()); await model.LoadAsync("incident-a");
        model.Decision = "EntireTransportHeld"; model.Reason = "현장 확인"; model.Prepare(); model.Confirmed = true; return model;
    }
    private static 비정상운송사건운영응답 Incident(long revision = 1, string id = "incident-a") => new()
    { 사건StableId = id, 운송Id = 11, 운송의뢰Id = "request-a", Revision = revision, 상태Code = "OperationsReviewPending", 전체수량 = 10 };
    private static FoodDeliveryOrderSettlementDto Settlement() => new()
    { SettlementId = "settlement-a", DriverId = "owner", OrderNo = "order-a", RestaurantName = "음식점 1", GrossAmount = 4180,
        CompletedAtUtc = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc), SettlementStatusCode = "AwaitingDeductions" };
    private static FoodDeliveryCompletedDeliveryDetailDto FoodDetail() => new()
    {
        Settlement = Settlement(), DetailAccessStatusCode = "Allowed", ServerNowUtc = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc),
        DetailExpiresAtUtc = new(2026, 10, 6, 1, 1, 0, DateTimeKind.Utc),
        CustomerDetails = new() { Address = "비공개 주소", ContactPhone = "비공개 번호" }, OrderDetails = new() { RestaurantAddress = "비공개 음식점 주소" }
    };
    private sealed class Clock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 6, 1, 0, 0, TimeSpan.Zero);
        public void Advance(int seconds) => timestamp += TimeSpan.FromSeconds(seconds).Ticks;
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        public Func<string, Task<object>> Get { get; set; } = _ => throw new NotSupportedException();
        public Func<string, object, Task<object?>>? Put { get; set; }
        public List<(string Role, string Path)> Reads { get; } = [];
        public List<(string Role, string Path, object Body)> Writes { get; } = [];
        public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        { Reads.Add((roleKey, path)); return (T)await Get(path); }
        public async Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        { Writes.Add((roleKey, path, body)); var value = await Put!(path, body); return value is null ? default : (T)value; }
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("owner", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => identity;
        public void Change(RoleWorkspaceIdentity value) { identity = value; Changed?.Invoke(); }
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
    }
}
