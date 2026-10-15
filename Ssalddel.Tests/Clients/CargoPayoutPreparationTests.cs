using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Settlement;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Settlement;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 운송대금 조회 VM·인증·typed client의 경계 검사. 단말·송금 증거가 아닙니다.</summary>
public sealed class CargoPayoutPreparationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task 최초인증복원뒤_선택월의본인운송대금을조회한다()
    {
        using var f = new Fixture(restore: false);
        await f.Model.InitializeAsync(2026, 10);
        Assert.True(f.Auth.IsAuthenticated);
        Assert.Equal((2026, 10), Assert.Single(f.Api.Calls).Period);
        Assert.Equal(11, Assert.Single(f.Model.항목목록).TransportId);
        Assert.False(f.Model.불러오는중);
        Assert.Null(f.Model.오류메시지);
    }

    [Fact]
    public async Task 미로그인상태는_조회하지않고개인금액을표시하지않는다()
    {
        using var f = new Fixture(authenticated: false);
        await f.Model.InitializeAsync(2026, 10);
        Assert.True(f.Model.로그인필요);
        Assert.False(f.Model.불러오는중);
        Assert.Empty(f.Api.Calls);
        Assert.Empty(f.Model.항목목록);
    }

    [Fact]
    public async Task 상세는같은월의선택운송만보여주고다른운송으로대체하지않는다()
    {
        using var f = new Fixture();
        await f.Model.InitializeAsync(2026, 10, 11);
        Assert.Equal(11, f.Model.선택항목!.TransportId);
        await f.Model.InitializeAsync(2026, 10, 22);
        Assert.Null(f.Model.선택항목);
        Assert.Null(f.Model.오류메시지);
        Assert.Equal(22, f.Model.SelectedTransportId);
    }

    [Fact]
    public async Task 월과운송선택이바뀐뒤_늦은이전응답을폐기한다()
    {
        using var f = new Fixture();
        var old = Pending();
        var current = Pending();
        f.Api.Query = (year, month, token) => month == 9 ? old.Task : current.Task;
        var first = f.Model.InitializeAsync(2026, 9, 9);
        var second = f.Model.InitializeAsync(2026, 10, 10);
        Assert.True(f.Api.Calls[0].Token.IsCancellationRequested);
        current.SetResult(Response(2026, 10, [Item(10)]));
        await second.WaitAsync(Timeout);
        old.SetResult(Response(2026, 9, [Item(9, month: 9)]));
        await first.WaitAsync(Timeout);
        Assert.Equal(10, f.Model.선택항목!.TransportId);
        Assert.Equal(10, f.Model.Month);
        Assert.False(f.Model.불러오는중);
    }

    [Fact]
    public async Task 이전요청의인증오류가_최신조회로딩을지우지않는다()
    {
        using var f = new Fixture();
        var old = Pending();
        var current = Pending();
        f.Api.Query = (_, _, _) => f.Api.Calls.Count == 1 ? old.Task : current.Task;
        var first = f.Model.InitializeAsync(2026, 10);
        var second = f.Model.RefreshAsync();
        old.SetException(new UnauthorizedAccessException());
        await first.WaitAsync(Timeout);
        Assert.True(f.Model.불러오는중);
        Assert.False(f.Model.로그인필요);
        current.SetResult(Response());
        await second.WaitAsync(Timeout);
        Assert.Single(f.Model.항목목록);
        Assert.Null(f.Model.오류메시지);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로그아웃과다른계정진입은_표시금액을즉시지우고늦은응답을버린다(bool logout)
    {
        using var f = new Fixture();
        await f.Model.InitializeAsync(2026, 10, 11);
        Assert.NotNull(f.Model.선택항목);
        var late = Pending();
        f.Api.Query = (_, _, _) => late.Task;
        var pending = f.Model.RefreshAsync();
        if (logout) await f.Auth.ClearAsync();
        else await f.Auth.ApplyAsync(Snapshot("driver-two"));
        Assert.Empty(f.Model.항목목록);
        Assert.Null(f.Model.선택항목);
        Assert.True(f.Api.Calls.Last().Token.IsCancellationRequested);
        late.SetResult(Response());
        await pending.WaitAsync(Timeout);
        Assert.Empty(f.Model.항목목록);
        Assert.Equal(logout, f.Model.로그인필요);
        if (!logout)
        {
            f.Api.Query = (_, _, _) => Task.FromResult<기사지급준비목록응답?>(new() { DriverId = "driver-two", Year = 2026, Month = 10, Items = [Item(22)] });
            await f.Model.RefreshAsync();
            Assert.Equal(22, Assert.Single(f.Model.항목목록).TransportId);
        }
    }

    [Fact]
    public async Task 같은계정토큰갱신은_운송대금을지우거나새조회하지않는다()
    {
        using var f = new Fixture();
        await f.Model.InitializeAsync(2026, 10);
        var session = f.Auth.SessionRevision;
        Assert.True(await f.Auth.TryApplyAsync(Snapshot(), f.Auth.Version));
        Assert.Equal(session, f.Auth.SessionRevision);
        Assert.Single(f.Model.항목목록);
        Assert.Single(f.Api.Calls);
    }

    [Fact]
    public async Task 화면이탈뒤_늦은조회가금액이나화면을복원하지않는다()
    {
        using var f = new Fixture();
        var pending = Pending();
        f.Api.Query = (_, _, _) => pending.Task;
        var operation = f.Model.InitializeAsync(2026, 10);
        var notifications = 0;
        f.Model.PropertyChanged += (_, _) => notifications++;
        f.Model.Dispose();
        var atDispose = notifications;
        pending.SetResult(Response());
        await operation.WaitAsync(Timeout);
        await f.Model.RefreshAsync();
        Assert.Empty(f.Model.항목목록);
        Assert.Equal(atDispose, notifications);
        Assert.Single(f.Api.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 조회실패는정상빈목록과구분하고_다시조회해회복한다(bool nullResponse)
    {
        using var f = new Fixture();
        f.Api.Query = (_, _, _) => nullResponse ? Task.FromResult<기사지급준비목록응답?>(null)
            : Task.FromException<기사지급준비목록응답?>(new InvalidOperationException("HTTP 403 private response"));
        await f.Model.InitializeAsync(2026, 10);
        Assert.NotNull(f.Model.오류메시지);
        Assert.DoesNotContain("private response", f.Model.오류메시지);
        Assert.Empty(f.Model.항목목록);
        f.Api.Query = (_, _, _) => Task.FromResult<기사지급준비목록응답?>(Response(items: []));
        await f.Model.RefreshAsync();
        Assert.Null(f.Model.오류메시지);
        Assert.Empty(f.Model.항목목록);
        Assert.False(f.Model.로그인필요);
    }

    [Fact]
    public async Task 인증거절은_로그인복귀를표시하고기존금액을남기지않는다()
    {
        using var f = new Fixture();
        await f.Model.InitializeAsync(2026, 10);
        f.Api.Query = (_, _, _) => Task.FromException<기사지급준비목록응답?>(new UnauthorizedAccessException());
        await f.Model.RefreshAsync();
        Assert.True(f.Model.로그인필요);
        Assert.Empty(f.Model.항목목록);
        Assert.Null(f.Model.오류메시지);
    }

    [Theory]
    [InlineData("month")]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("completion-month")]
    [InlineData("driver")]
    public async Task 잘못된응답은_다른운송금액이나지급성공으로표시하지않는다(string defect)
    {
        using var f = new Fixture();
        var data = Response();
        switch (defect)
        {
            case "month": data.Month = 9; break;
            case "driver": data.DriverId = "driver-two"; break;
            case "duplicate": data.Items = [Item(11), Item(11)]; break;
            case "negative": data.Items[0].ExpectedPayoutAmount = -1; break;
            case "completion-month": data.Items[0].CompletedAtUtc = new DateTime(2026, 9, 30, 23, 59, 0, DateTimeKind.Utc); break;
        }
        f.Api.Query = (_, _, _) => Task.FromResult<기사지급준비목록응답?>(data);
        await f.Model.InitializeAsync(2026, 10);
        Assert.NotNull(f.Model.오류메시지);
        Assert.Empty(f.Model.항목목록);
    }

    [Theory]
    [InlineData(1999, 10, 1)]
    [InlineData(2026, 13, 1)]
    [InlineData(2026, 10, 0)]
    public async Task 잘못된기간과운송식별자는_서버호출전에차단한다(int year, int month, long transportId)
    {
        using var f = new Fixture();
        await f.Model.InitializeAsync(year, month, transportId);
        Assert.Empty(f.Api.Calls);
        Assert.NotNull(f.Model.오류메시지);
        Assert.False(f.Model.불러오는중);
    }

    [Fact]
    public async Task 미확정금액과보류이유를보존하고_응답원본변경이표시금액을바꾸지않는다()
    {
        using var f = new Fixture();
        var data = Response(items: [Item(1), Item(2)]);
        data.Items[0].ExpectedPayoutAmount = null;
        data.Items[0].ReadinessCode = 기사지급준비상태코드.비정상운송검토보류;
        data.Items[0].ReadinessMessage = "현장 확인이 끝날 때까지 보류합니다.";
        f.Api.Query = (_, _, _) => Task.FromResult<기사지급준비목록응답?>(data);
        await f.Model.InitializeAsync(2026, 10, 1);
        Assert.Equal("금액 미확정", f.Model.선택항목!.금액표시);
        Assert.Equal("운영 검토 보류", f.Model.선택항목.상태표시);
        Assert.Equal(data.Items[0].ReadinessMessage, f.Model.선택항목.ReadinessMessage);
        data.Items[0].ExpectedPayoutAmount = 0;
        Assert.Null(f.Model.선택항목.ExpectedPayoutAmount);
    }

    [Theory]
    [InlineData("ReadyForPayoutPreparation", "지급 준비 가능")]
    [InlineData("OnSiteCollectionConfirmed", "현장수금 확인")]
    [InlineData("UnknownFutureState", "지급 상태 확인 필요")]
    public async Task 지급준비와현장수금을_입금완료로바꾸지않는다(string code, string text)
    {
        using var f = new Fixture();
        var data = Response();
        data.Items[0].ReadinessCode = code;
        f.Api.Query = (_, _, _) => Task.FromResult<기사지급준비목록응답?>(data);
        await f.Model.InitializeAsync(2026, 10, 11);
        Assert.Equal(text, f.Model.선택항목!.상태표시);
        Assert.DoesNotContain("입금 완료", f.Model.선택항목.상태표시);
        Assert.Equal("4,180원", f.Model.선택항목.금액표시);
    }

    [Fact]
    public async Task TypedClient는_기존본인조회GET만호출하고기간을전달한다()
    {
        var client = DispatchProxy.Create<IDriverApiClient, QueryClientStub>();
        var query = (QueryClientStub)(object)client;
        var api = new DriverPayoutPreparationApiService(client);
        using var token = new CancellationTokenSource();
        var response = await api.월별조회Async(2026, 9, token.Token);
        Assert.Equal("api/v1/driver/payout-preparations?year=2026&month=9", query.Path);
        Assert.Equal(token.Token, query.Token);
        Assert.NotNull(response);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.월별조회Async(2026, 13));
        Assert.Equal(1, query.Calls);
    }

    [Fact]
    public void 상세에서같은월목록과로그인복귀경로를보존한다()
    {
        Assert.Equal("/driver/payouts/11?year=2026&month=9", DriverRoutes.PayoutDetail(11, 2026, 9));
        Assert.Equal("/driver/payouts?year=2026&month=9", DriverRoutes.PayoutList(2026, 9));
        Assert.Contains(Uri.EscapeDataString(DriverRoutes.PayoutDetail(11, 2026, 9)),
            DriverRoutes.LoginFor(DriverRoutes.PayoutDetail(11, 2026, 9)));
    }

    private static TaskCompletionSource<기사지급준비목록응답?> Pending()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static 기사지급준비항목응답 Item(long id, int month = 10) => new()
    {
        TransportId = id, TransportNumber = $"cargo-{id}", RequestId = $"request-{id}",
        CompletedAtUtc = new DateTime(2026, month, 14, 0, 0, 0, DateTimeKind.Utc), ExpectedPayoutAmount = 4180,
        CurrencyCode = "KRW", ReadinessCode = 기사지급준비상태코드.화주수납대기,
        ReadinessMessage = "화주 수납을 기다리고 있습니다.", ShipperPaymentStatus = "결제대기", FreightSettlementStatus = "입금대기"
    };
    private static 기사지급준비목록응답 Response(int year = 2026, int month = 10, 기사지급준비항목응답[]? items = null)
        => new() { DriverId = "driver-one", Year = year, Month = month, Items = items ?? [Item(11, month)] };
    private static ClientAuthTokenSnapshot Snapshot(string id = "driver-one")
        => new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), id, "기사", ["Driver"]);

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; }
        public ApiStub Api { get; } = new();
        public 기사운송대금PageViewModel Model { get; }
        public Fixture(bool authenticated = true, bool restore = true)
        {
            Auth = new(new TokenStore(authenticated ? Snapshot() : null), new ClientSessionGuard());
            if (restore) Auth.RestoreAsync().GetAwaiter().GetResult();
            Model = new(Api, Auth);
        }
        public void Dispose() => Model.Dispose();
    }
    private sealed class ApiStub : IDriverPayoutPreparationApiService
    {
        public List<((int, int) Period, CancellationToken Token)> Calls { get; } = [];
        public Func<int, int, CancellationToken, Task<기사지급준비목록응답?>> Query { get; set; }
            = (year, month, _) => Task.FromResult<기사지급준비목록응답?>(Response(year, month, [Item(11, month)]));
        public Task<기사지급준비목록응답?> 월별조회Async(int year, int month, CancellationToken cancellationToken = default)
        { Calls.Add(((year, month), cancellationToken)); return Query(year, month, cancellationToken); }
    }
    public class QueryClientStub : DispatchProxy
    {
        public string? Path { get; private set; }
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != nameof(IDriverApiClient.GetAsync) || method.GetGenericArguments().Single() != typeof(기사지급준비목록응답))
                throw new InvalidOperationException("운송대금 조회에서 변경 API를 호출하면 안 됩니다.");
            Calls++; Path = (string)args![0]!; Token = (CancellationToken)args[2]!;
            return Task.FromResult<기사지급준비목록응답?>(Response());
        }
    }
    private sealed class TokenStore(ClientAuthTokenSnapshot? snapshot) : IClientSecureTokenStore
    {
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default) { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { snapshot = null; return Task.CompletedTask; }
    }
}
