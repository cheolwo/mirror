using System.Net;
using System.Reflection;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 MAUI 상·하차 PageVM의 수명 시험. 기기·촬영·완료 UI 증거와 구별합니다.</summary>
public sealed class CargoTransportPageLifetimeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 다른ID로빠르게이동하면_취소를무시한이전응답과개인입력을남기지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        var entered = Signal();
        var oldResponse = Response();
        CancellationToken oldToken = default;
        fixture.Api.Detail = (id, token) =>
        {
            if (id == 11)
            {
                oldToken = token;
                entered.SetResult();
                return oldResponse.Task; // 서버가 취소를 무시하고 늦게 응답하는 상황입니다.
            }
            return Task.FromResult<기사운송상세응답?>(Detail(id));
        };

        var first = fixture.InitializeAsync(11);
        await entered.Task.WaitAsync(Timeout);
        await fixture.FillPrivateInputsAsync();
        var second = fixture.InitializeAsync(22);

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal(22, fixture.TransportId);
        Assert.Null(fixture.Transport);
        fixture.AssertPrivateInputsCleared();

        oldResponse.SetResult(Detail(11));
        await Task.WhenAll(first, second).WaitAsync(Timeout);

        Assert.Equal([11L, 22L], fixture.Api.RequestedIds);
        Assert.Equal(22, fixture.Transport!.Id);
        Assert.Equal("synthetic-cargo-22", fixture.Transport.의뢰Id);
        Assert.Null(fixture.Page.오류메시지);
        Assert.True(fixture.Page.초기화됨);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 페이지Dispose후_취소를무시한뒤늦은상세응답을표시하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        var entered = Signal();
        var pending = Response();
        CancellationToken requestToken = default;
        fixture.Api.Detail = (_, token) =>
        {
            requestToken = token;
            entered.SetResult();
            return pending.Task;
        };

        var loading = fixture.InitializeAsync(11);
        await entered.Task.WaitAsync(Timeout);
        fixture.Dispose();
        Assert.True(requestToken.IsCancellationRequested);

        pending.SetResult(Detail(11));
        await loading.WaitAsync(Timeout);

        Assert.Null(fixture.Transport);
        Assert.Equal([11L], fixture.Api.RequestedIds);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로그아웃은_운송과사진서명예외메모를지우고_이전갱신응답을버린다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        await fixture.FillPrivateInputsAsync();
        var entered = Signal();
        var pending = Response();
        fixture.Api.Detail = (_, _) => { entered.SetResult(); return pending.Task; };
        var refreshing = fixture.InitializeAsync(11);
        await entered.Task.WaitAsync(Timeout);

        await fixture.Auth.ClearAsync();

        Assert.True(fixture.LoginRequired);
        Assert.Null(fixture.Transport);
        fixture.AssertPrivateInputsCleared();

        pending.SetResult(Detail(11));
        await refreshing.WaitAsync(Timeout);
        Assert.Null(fixture.Transport);
        fixture.AssertPrivateInputsCleared();
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로그인된사용자가바뀌면_이전개인입력을지우고_새세션의상세만다시불러온다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        await fixture.FillPrivateInputsAsync();
        var entered = Signal();
        var pending = Response();
        fixture.Api.Detail = (_, _) => { entered.SetResult(); return pending.Task; };
        var refreshing = fixture.InitializeAsync(11);
        await entered.Task.WaitAsync(Timeout);

        await fixture.Auth.ApplyAsync(Snapshot("synthetic-driver-two"));

        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.False(fixture.LoginRequired);
        Assert.Null(fixture.Transport);
        fixture.AssertPrivateInputsCleared();
        pending.SetResult(Detail(11));
        await refreshing.WaitAsync(Timeout);
        Assert.Null(fixture.Transport);

        fixture.Api.Detail = (id, _) => Task.FromResult<기사운송상세응답?>(Detail(id, "새 세션 합성 수령자"));
        await fixture.InitializeAsync(22);
        Assert.Equal(22, fixture.Transport!.Id);
        Assert.Equal("새 세션 합성 수령자", fixture.Transport.수령자명);
        fixture.AssertPrivateInputsCleared();
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 서버503은_실패를표시하되인증을보존하고_같은ID재시도로회복한다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        fixture.Api.Detail = (_, _) => Task.FromException<기사운송상세응답?>(
            new HttpRequestException("synthetic 503", null, HttpStatusCode.ServiceUnavailable));

        await fixture.InitializeAsync(11);

        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.False(fixture.LoginRequired);
        Assert.Null(fixture.Transport);
        Assert.Contains("synthetic 503", fixture.Page.오류메시지);
        Assert.False(fixture.Page.처리중);

        fixture.Api.Detail = (id, _) => Task.FromResult<기사운송상세응답?>(Detail(id));
        await fixture.InitializeAsync(11);

        Assert.Equal([11L, 11L], fixture.Api.RequestedIds);
        Assert.Equal(11, fixture.Transport!.Id);
        Assert.Null(fixture.Page.오류메시지);
        Assert.True(fixture.Page.초기화됨);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<기사운송상세응답?> Response() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static 기사운송상세응답 Detail(long id, string recipient = "합성 수령자") => new()
    {
        Id = id, 운송번호 = $"synthetic-cargo-{id}", 상태 = "상차지도착",
        출발지 = "합성 상차지", 도착지 = "합성 하차지", 인수증필요 = true,
        인수증서명필수 = true, 수령자명 = recipient, 전달요청 = "합성 전달 요청", 운임 = 6500m
    };

    private static ClientAuthTokenSnapshot Snapshot(string userId = "synthetic-driver-one")
        => new("test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh",
            DateTime.UtcNow.AddDays(1), userId, "합성 기사", ["Driver"]);

    private sealed class Fixture : IDisposable
    {
        private readonly 기사상차PageViewModel? pickup;
        private readonly 기사하차PageViewModel? dropoff;
        private bool disposed;
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public TransportApiStub Api { get; }
        public CompletionService Completion { get; } = new();
        public 기사PageViewModelBase Page => (기사PageViewModelBase?)pickup ?? dropoff!;
        public 기사운송샘플항목? Transport => pickup?.운송 ?? dropoff?.운송;
        public long TransportId => pickup?.운송Id ?? dropoff!.운송Id;
        public bool LoginRequired => pickup?.로그인필요 ?? dropoff!.로그인필요;
        private 기사운송사진ViewModel Photo => pickup?.사진 ?? dropoff!.사진;
        private 기사운송예외신고ViewModel Issue => pickup?.예외 ?? dropoff!.예외;
        private 기사운송작업상태ViewModel Status => pickup?.작업상태 ?? dropoff!.작업상태;

        public Fixture(bool isPickup)
        {
            var api = DispatchProxy.Create<IDriverTransportApiService, TransportApiStub>();
            Api = (TransportApiStub)(object)api;
            var detail = new 기사운송상세조회Service(api, Auth);
            if (isPickup)
                pickup = new(detail, new 기사상차완료ViewModel(Completion), new ExceptionService(), new Navigation());
            else
                dropoff = new(detail, new 기사하차완료ViewModel(Completion), new ExceptionService());
        }

        public Task InitializeAsync(long id) => pickup is not null ? pickup.InitializeAsync(id) : dropoff!.InitializeAsync(id);

        public async Task FillPrivateInputsAsync()
        {
            using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture();
            await Photo.촬영Async();
            Assert.True(Photo.사진있음);
            Issue.열림 = true;
            Issue.유형 = "합성 개인 입력";
            Issue.메모 = "합성 수령자 연락 메모";
            Status.설정("합성 개인 입력 상태", Severity.Warning);
            if (pickup is not null)
            {
                pickup.화물.바코드입력 = "synthetic-private-cargo-code";
                pickup.인수증.인수자명 = "합성 인수자";
                pickup.인수증.인수자소속 = "합성 소속";
                pickup.인수증.인수자서명 = "합성 인수자 서명";
                pickup.인수증.기사서명 = "합성 기사 서명";
                pickup.인수증.서명생략사유 = "합성 입력";
                pickup.인수증.인수증확인 = true;
                pickup.현장.상차지확인 = true;
                pickup.현장.화물상태확인 = true;
                pickup.현장.화주연락확인 = true;
            }
            else
            {
                dropoff!.화물.바코드입력 = "synthetic-private-cargo-code";
                dropoff!.인계.하차지확인 = true;
                dropoff.인계.수령자확인 = true;
                dropoff.인계.결제증빙확인 = true;
                dropoff.인계.결제증빙방식 = "합성 개인 입력";
            }
        }

        public void AssertPrivateInputsCleared()
        {
            Assert.False(Photo.사진있음);
            Assert.Null(Photo.파일명);
            Assert.Null(Photo.미리보기Url);
            Assert.Null(Issue.메모);
            Assert.False(Issue.열림);
            Assert.Equal("수량 부족", Issue.유형);
            Assert.Null(Status.메시지);
            if (pickup is not null)
            {
                Assert.Null(pickup.화물.바코드입력);
                Assert.Null(pickup.인수증.인수자명);
                Assert.Null(pickup.인수증.인수자소속);
                Assert.Null(pickup.인수증.인수자서명);
                Assert.Null(pickup.인수증.기사서명);
                Assert.Null(pickup.인수증.서명생략사유);
                Assert.False(pickup.인수증.인수증확인);
                Assert.False(pickup.현장.완료);
            }
            else
            {
                Assert.Null(dropoff!.화물.바코드입력);
                Assert.False(dropoff!.인계.완료);
                Assert.Equal("인수증", dropoff.인계.결제증빙방식);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Page.Dispose();
        }
    }

    public class TransportApiStub : DispatchProxy
    {
        public List<long> RequestedIds { get; } = [];
        public Func<long, CancellationToken, Task<기사운송상세응답?>> Detail { get; set; }
            = (id, _) => Task.FromResult<기사운송상세응답?>(CargoTransportPageLifetimeTests.Detail(id));

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IDriverTransportApiService.상세조회Async)) throw new NotSupportedException();
            var id = (long)args![0]!;
            RequestedIds.Add(id);
            return Detail(id, (CancellationToken)args[1]!);
        }
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot = Snapshot();
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default)
        { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { snapshot = null; return Task.CompletedTask; }
    }

    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public int Calls { get; private set; }
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("수명 시험에서 운송 완료를 호출하면 안 됩니다."); }
    }

    private sealed class ExceptionService : IDriverTransportExceptionService
    {
        public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("수명 시험에서 예외 신고를 호출하면 안 됩니다.");
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/driver/transports/current");
        protected override void NavigateToCore(string uri, bool forceLoad)
            => throw new InvalidOperationException("수명 시험에서 완료 후 이동을 호출하면 안 됩니다.");
    }
}
