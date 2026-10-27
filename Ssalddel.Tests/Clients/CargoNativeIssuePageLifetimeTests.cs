using System.Reflection;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>신고 하위 모델을 실제 상·하차 페이지에 조립한 상태의 전환 수명 회귀입니다.</summary>
public sealed class CargoNativeIssuePageLifetimeTests
{
    [Theory]
    [InlineData(true, "transport")]
    [InlineData(false, "transport")]
    [InlineData(true, "logout")]
    [InlineData(false, "logout")]
    [InlineData(true, "dispose")]
    [InlineData(false, "dispose")]
    public async Task 상하차페이지전환은_진행중신고를취소하고_늦은성공을새업무에표시하지않는다(bool pickup, string transition)
    {
        using var f = new Fixture(pickup);
        await f.InitializeAsync(11);
        Assert.True(f.Issue.신고가능);
        f.Issue.열림 = true;
        f.Issue.메모 = "이전 운송의 합성 메모";
        var reporting = f.Issue.신고Command.ExecuteAsync(null);
        await f.Reporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(f.Issue.신고중);
        Assert.Equal(11, f.Reporter.Report!.TransportId);
        Assert.Equal(pickup ? "상차" : "하차", f.Reporter.Report.Stage);

        if (transition == "transport") await f.InitializeAsync(22);
        else if (transition == "logout") await f.Auth.ClearAsync();
        else f.Dispose();

        Assert.True(f.Reporter.Token.IsCancellationRequested);
        Assert.False(f.Issue.신고중);
        Assert.False(f.Issue.결과확인필요);
        Assert.False(f.Issue.열림);
        Assert.Null(f.Issue.메모);
        var afterTransition = f.Status.메시지;
        var afterSeverity = f.Status.심각도;
        var afterTransport = f.Transport;
        // 서버가 취소를 무시한 경우에도 부모 페이지가 초기화한 결과를 덮으면 안 됩니다.
        f.Reporter.Response.SetResult(new(true, "이전 신고 결과가 뒤늦게 도착했습니다", new 기사운송요약응답
        {
            Id = 11, 예외신고됨 = true, 관리자확인필요 = true,
            최근예외단계 = pickup ? "상차" : "하차", 최근예외메시지 = "수량 부족"
        }));
        await reporting.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(afterTransition, f.Status.메시지);
        Assert.Equal(afterSeverity, f.Status.심각도);
        Assert.Same(afterTransport, f.Transport);
        Assert.DoesNotContain("이전 신고 결과", f.Status.메시지 ?? string.Empty);
        Assert.DoesNotContain("이전 운송의 합성 메모", f.Status.메시지 ?? string.Empty);
        Assert.False(f.Issue.신고중);
        Assert.False(f.Issue.결과확인필요);
        Assert.Equal(1, f.Reporter.Calls);
        Assert.Equal(0, f.Completion.Calls);
        if (transition == "transport")
        {
            Assert.Equal(22, f.Transport!.Id);
            Assert.False(f.LoginRequired);
            Assert.True(f.Issue.신고가능);
        }
        else
        {
            Assert.False(f.Issue.신고가능);
            await f.Issue.신고Async();
            Assert.Equal(1, f.Reporter.Calls);
            if (transition == "logout") { Assert.True(f.LoginRequired); Assert.Null(f.Transport); }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly 기사상차PageViewModel? pickup;
        private readonly 기사하차PageViewModel? dropoff;
        private bool disposed;
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public Reporter Reporter { get; } = new();
        public CompletionService Completion { get; } = new();
        public 기사PageViewModelBase Page => (기사PageViewModelBase?)pickup ?? dropoff!;
        public 기사운송예외신고ViewModel Issue => pickup?.예외 ?? dropoff!.예외;
        public 기사운송작업상태ViewModel Status => pickup?.작업상태 ?? dropoff!.작업상태;
        public 기사운송샘플항목? Transport => pickup?.운송 ?? dropoff?.운송;
        public bool LoginRequired => pickup?.로그인필요 ?? dropoff!.로그인필요;
        public Fixture(bool isPickup)
        {
            var api = DispatchProxy.Create<IDriverTransportApiService, TransportApi>();
            var detail = new 기사운송상세조회Service(api, Auth);
            if (isPickup) pickup = new(detail, new 기사상차완료ViewModel(Completion), Reporter, new Navigation());
            else dropoff = new(detail, new 기사하차완료ViewModel(Completion), Reporter);
        }
        public Task InitializeAsync(long id) => pickup is not null ? pickup.InitializeAsync(id) : dropoff!.InitializeAsync(id);
        public void Dispose() { if (disposed) return; disposed = true; Page.Dispose(); }
    }

    public class TransportApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IDriverTransportApiService.상세조회Async)) throw new NotSupportedException();
            var id = (long)args![0]!;
            return Task.FromResult<기사운송상세응답?>(new()
            {
                Id = id, 기사_운송자 = "synthetic-driver", 운송번호 = $"synthetic-cargo-{id}", 상태 = "상차지도착",
                출발지 = "합성 상차지", 도착지 = "합성 하차지", 운임 = 6500m
            });
        }
    }

    private sealed class Reporter : IDriverTransportExceptionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DriverTransportExceptionReportResult> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DriverTransportExceptionReport? Report { get; private set; }
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }
        public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken cancellationToken = default)
        {
            Calls++; Report = report; Token = cancellationToken; Started.TrySetResult();
            return Response.Task;
        }
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot = new("test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh",
            DateTime.UtcNow.AddDays(1), "synthetic-driver", "합성 기사", ["Driver"]);
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default)
        { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { snapshot = null; return Task.CompletedTask; }
    }

    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public int Calls { get; private set; }
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("신고 수명 시험에서 상하차 완료를 호출하면 안 됩니다."); }
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/driver/transports/current");
        protected override void NavigateToCore(string uri, bool forceLoad)
            => throw new InvalidOperationException("신고 수명 시험에서 화면 이동을 자동 실행하면 안 됩니다.");
    }
}
