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

/// <summary>실제 MAUI PageVM의 서버 도착 명령과 같은 ID 재조회. 기기/현장 실행 증거는 아닙니다.</summary>
public sealed class CargoTransportArrivalPageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 도착은_해당명령뒤_같은운송을재조회하고_그상태로만표시한다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        Assert.True(fixture.CanArrive);

        await fixture.ArriveAsync();

        Assert.Equal([11L], fixture.Api.ArrivalIds);
        Assert.Equal(pickup ? nameof(IDriverTransportApiService.상차지도착Async) : nameof(IDriverTransportApiService.하차지도착Async), fixture.Api.ArrivalMethod);
        Assert.Equal([11L, 11L], fixture.Api.DetailIds);
        Assert.Equal(fixture.ArrivedState, fixture.Transport!.현재단계);
        Assert.Equal(Severity.Success, fixture.Status.심각도);
        Assert.False(fixture.CanArrive);
        Assert.False(fixture.Arriving);
        Assert.False(fixture.PlaceConfirmed); // 현장 주소 확인은 서버 도착 기록과 별도입니다.
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 도착명령실패는_원래상태와인증을보존하고_같은ID재시도로회복한다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        fixture.Api.Arrive = (_, _) => throw new HttpRequestException("synthetic 503", null, HttpStatusCode.ServiceUnavailable);

        await fixture.ArriveAsync();

        Assert.Equal(fixture.BeforeState, fixture.Transport!.현재단계);
        Assert.Equal([11L], fixture.Api.DetailIds);
        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.True(fixture.CanArrive);
        Assert.False(fixture.Arriving);
        Assert.Equal(Severity.Error, fixture.Status.심각도);
        Assert.Contains("synthetic 503", fixture.Status.메시지);

        fixture.Api.Arrive = fixture.SuccessfulArrival;
        await fixture.ArriveAsync();
        Assert.Equal([11L, 11L], fixture.Api.ArrivalIds);
        Assert.Equal([11L, 11L], fixture.Api.DetailIds);
        Assert.Equal(fixture.ArrivedState, fixture.Transport!.현재단계);
        Assert.Equal(Severity.Success, fixture.Status.심각도);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 도착응답뒤_재조회가실패하면_도착상태나완료를합성하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        fixture.Api.Detail = (_, _) => throw new HttpRequestException("synthetic requery 503", null, HttpStatusCode.ServiceUnavailable);

        await fixture.ArriveAsync();

        Assert.Equal(fixture.BeforeState, fixture.Transport!.현재단계);
        Assert.Equal(Severity.Warning, fixture.Status.심각도);
        Assert.Contains("synthetic requery 503", fixture.Page.오류메시지);
        Assert.False(fixture.CanComplete);
        Assert.False(fixture.Arriving);
        Assert.Equal(0, fixture.Completion.Calls);

        fixture.Api.Detail = fixture.CanonicalDetail;
        await fixture.InitializeAsync(11);
        Assert.Equal(fixture.ArrivedState, fixture.Transport!.현재단계);
        Assert.Null(fixture.Page.오류메시지);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 도착응답과다른ID는_적용하거나재조회하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        fixture.Api.Arrive = (_, _) => Task.FromResult<기사운송상태변경응답?>(new() { Id = 99, 상태 = fixture.ArrivedState });

        await fixture.ArriveAsync();

        Assert.Equal([11L], fixture.Api.DetailIds);
        Assert.Equal(fixture.BeforeState, fixture.Transport!.현재단계);
        Assert.Equal(Severity.Error, fixture.Status.심각도);
        Assert.Contains("도착 처리 결과", fixture.Status.메시지);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 도착요청중_중복요청과완료를차단한다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        var pending = Response();
        fixture.Api.Arrive = (_, _) => pending.Task;
        var first = fixture.ArriveAsync();
        Assert.True(fixture.Arriving);
        Assert.False(fixture.CanArrive);
        Assert.False(fixture.CanComplete);

        await fixture.ArriveAsync();
        Assert.Equal([11L], fixture.Api.ArrivalIds);
        fixture.Api.State = fixture.ArrivedState;
        pending.SetResult(new() { Id = 11, 상태 = fixture.ArrivedState });
        await first.WaitAsync(Timeout);

        Assert.Equal([11L, 11L], fixture.Api.DetailIds);
        Assert.False(fixture.Arriving);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 다른운송선택은_이전도착을취소하고_늦은응답을새운송에적용하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        var pending = Response();
        CancellationToken oldToken = default;
        fixture.Api.Arrive = (_, token) => { oldToken = token; return pending.Task; };
        var arriving = fixture.ArriveAsync();

        await fixture.InitializeAsync(22);
        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal(22, fixture.Transport!.Id);
        pending.SetResult(new() { Id = 11, 상태 = fixture.ArrivedState });
        await arriving.WaitAsync(Timeout);

        Assert.Equal([11L, 22L], fixture.Api.DetailIds);
        Assert.Equal(22, fixture.Transport!.Id);
        Assert.Equal(fixture.BeforeState, fixture.Transport.현재단계);
        Assert.Null(fixture.Status.메시지);
        Assert.False(fixture.Arriving);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 취소를무시한이전도착이끝나도_새운송도착의처리중상태를해제하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        var oldPending = Response();
        var currentPending = Response();
        CancellationToken oldToken = default;
        CancellationToken currentToken = default;
        fixture.Api.Arrive = (id, token) =>
        {
            if (id == 11) { oldToken = token; return oldPending.Task; }
            currentToken = token;
            return currentPending.Task;
        };
        var previous = fixture.ExecuteArrivalCommandAsync();

        await fixture.InitializeAsync(22);
        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(fixture.Arriving);
        Assert.True(fixture.CanArrive);
        var current = fixture.ExecuteArrivalCommandAsync();
        Assert.Equal([11L, 22L], fixture.Api.ArrivalIds);
        Assert.True(fixture.Arriving);

        oldPending.SetResult(new() { Id = 11, 상태 = fixture.ArrivedState });
        await previous.WaitAsync(Timeout);

        Assert.True(fixture.Arriving);
        Assert.False(fixture.CanArrive);
        Assert.False(fixture.CanComplete);
        Assert.False(currentToken.IsCancellationRequested);
        Assert.Equal([11L, 22L], fixture.Api.DetailIds);
        Assert.Equal(22, fixture.Transport!.Id);
        Assert.Equal(Severity.Info, fixture.Status.심각도);

        fixture.Api.State = fixture.ArrivedState;
        currentPending.SetResult(new() { Id = 22, 상태 = fixture.ArrivedState });
        await current.WaitAsync(Timeout);
        Assert.False(fixture.Arriving);
        Assert.Equal([11L, 22L, 22L], fixture.Api.DetailIds);
        Assert.Equal(fixture.ArrivedState, fixture.Transport!.현재단계);
        Assert.Equal(Severity.Success, fixture.Status.심각도);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task 인증종료나새세션은_이전도착응답과재조회를차단한다(bool pickup, bool relogin)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        var pending = Response();
        CancellationToken oldToken = default;
        fixture.Api.Arrive = (_, token) => { oldToken = token; return pending.Task; };
        var arriving = fixture.ArriveAsync();

        if (relogin) await fixture.Auth.ApplyAsync(Snapshot("synthetic-driver-two"));
        else await fixture.Auth.ClearAsync();
        Assert.True(oldToken.IsCancellationRequested);
        pending.SetResult(new() { Id = 11, 상태 = fixture.ArrivedState });
        await arriving.WaitAsync(Timeout);

        Assert.Equal([11L], fixture.Api.DetailIds);
        Assert.Null(fixture.Transport);
        Assert.Null(fixture.Status.메시지);
        Assert.False(fixture.CanArrive);
        Assert.False(fixture.Arriving);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 페이지이탈은_이전도착요청을취소하고_늦은결과뒤재조회하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        var pending = Response();
        CancellationToken oldToken = default;
        fixture.Api.Arrive = (_, token) => { oldToken = token; return pending.Task; };
        var arriving = fixture.ArriveAsync();

        fixture.Dispose();
        Assert.True(oldToken.IsCancellationRequested);
        pending.SetResult(new() { Id = 11, 상태 = fixture.ArrivedState });
        await arriving.WaitAsync(Timeout);

        Assert.Equal([11L], fixture.Api.DetailIds);
        Assert.Equal(fixture.BeforeState, fixture.Transport!.현재단계);
        Assert.False(fixture.CanArrive);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로컬현장체크와사진만으로_서버도착전에완료하지않는다(bool pickup)
    {
        using var fixture = new Fixture(pickup);
        await fixture.InitializeAsync(11);
        await fixture.FillFieldConfirmationAsync();
        Assert.False(fixture.CanComplete);

        await fixture.ArriveAsync();
        await fixture.FillFieldConfirmationAsync();
        Assert.True(fixture.CanComplete);
        Assert.Equal(0, fixture.Completion.Calls);
    }

    private static TaskCompletionSource<기사운송상태변경응답?> Response()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ClientAuthTokenSnapshot Snapshot(string userId = "synthetic-driver-one")
        => new("test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh",
            DateTime.UtcNow.AddDays(1), userId, "합성 기사", ["Driver"]);

    private sealed class Fixture : IDisposable
    {
        private readonly 기사상차PageViewModel? pickup;
        private readonly 기사하차PageViewModel? dropoff;
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public TransportApiStub Api { get; }
        public CompletionService Completion { get; } = new();
        public 기사PageViewModelBase Page => (기사PageViewModelBase?)pickup ?? dropoff!;
        public 기사운송샘플항목? Transport => pickup?.운송 ?? dropoff?.운송;
        public 기사운송작업상태ViewModel Status => pickup?.작업상태 ?? dropoff!.작업상태;
        public bool CanArrive => pickup?.도착가능 ?? dropoff!.도착가능;
        public bool CanComplete => pickup?.완료가능 ?? dropoff!.완료가능;
        public bool Arriving => pickup?.도착처리중 ?? dropoff!.도착처리중;
        public bool PlaceConfirmed => pickup?.현장.상차지확인 ?? dropoff!.인계.하차지확인;
        public string BeforeState { get; }
        public string ArrivedState { get; }

        public Fixture(bool isPickup)
        {
            BeforeState = isPickup ? "배차확정" : "상차완료";
            ArrivedState = isPickup ? "상차지도착" : "하차지도착";
            var api = DispatchProxy.Create<IDriverTransportApiService, TransportApiStub>();
            Api = (TransportApiStub)(object)api;
            Api.State = BeforeState;
            Api.Detail = CanonicalDetail;
            Api.Arrive = SuccessfulArrival;
            var detail = new 기사운송상세조회Service(api, Auth);
            if (isPickup) pickup = new(detail, new 기사상차완료ViewModel(Completion), new ExceptionService(), new Navigation());
            else dropoff = new(detail, new 기사하차완료ViewModel(Completion), new ExceptionService());
        }

        public Task<기사운송상세응답?> CanonicalDetail(long id, CancellationToken token)
            => Task.FromResult<기사운송상세응답?>(new() { Id = id, 운송번호 = $"synthetic-cargo-{id}", 상태 = Api.State });

        public Task<기사운송상태변경응답?> SuccessfulArrival(long id, CancellationToken token)
        {
            Api.State = ArrivedState;
            return Task.FromResult<기사운송상태변경응답?>(new() { Id = id, 상태 = ArrivedState });
        }

        public Task InitializeAsync(long id) => pickup is not null ? pickup.InitializeAsync(id) : dropoff!.InitializeAsync(id);
        public Task ArriveAsync() => pickup is not null ? pickup.도착Async() : dropoff!.도착Async();
        public Task ExecuteArrivalCommandAsync() => pickup is not null
            ? pickup.도착Command.ExecuteAsync(null)
            : dropoff!.도착Command.ExecuteAsync(null);

        public async Task FillFieldConfirmationAsync()
        {
            using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture();
            if (pickup is not null)
            {
                await pickup.사진.촬영Async();
                pickup.현장.상차지확인 = true;
                pickup.현장.화물상태확인 = true;
                pickup.현장.화주연락확인 = true;
            }
            else
            {
                await dropoff!.사진.촬영Async();
                dropoff.인계.하차지확인 = true;
                dropoff.인계.수령자확인 = true;
                dropoff.인계.결제증빙확인 = true;
            }
        }

        public void Dispose() => Page.Dispose();
    }

    public class TransportApiStub : DispatchProxy
    {
        public string State { get; set; } = string.Empty;
        public List<long> DetailIds { get; } = [];
        public List<long> ArrivalIds { get; } = [];
        public string? ArrivalMethod { get; private set; }
        public Func<long, CancellationToken, Task<기사운송상세응답?>> Detail { get; set; } = null!;
        public Func<long, CancellationToken, Task<기사운송상태변경응답?>> Arrive { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var id = (long)args![0]!;
            var token = (CancellationToken)args[1]!;
            if (targetMethod?.Name == nameof(IDriverTransportApiService.상세조회Async))
            {
                DetailIds.Add(id);
                return Detail(id, token);
            }
            if (targetMethod?.Name is nameof(IDriverTransportApiService.상차지도착Async) or nameof(IDriverTransportApiService.하차지도착Async))
            {
                ArrivalIds.Add(id);
                ArrivalMethod = targetMethod.Name;
                return Arrive(id, token);
            }
            throw new NotSupportedException();
        }
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot = Snapshot();
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default) { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { snapshot = null; return Task.CompletedTask; }
    }

    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public int Calls { get; private set; }
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("도착 시험에서 운송 완료를 호출하면 안 됩니다."); }
    }

    private sealed class ExceptionService : IDriverTransportExceptionService
    {
        public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("도착 시험에서 예외 신고를 호출하면 안 됩니다.");
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/driver/transports/current");
        protected override void NavigateToCore(string uri, bool forceLoad)
            => throw new InvalidOperationException("도착 시험에서 완료 후 이동을 호출하면 안 됩니다.");
    }
}
