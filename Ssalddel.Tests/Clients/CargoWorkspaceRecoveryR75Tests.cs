using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using DriverApp.Services.Samples;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Reservation;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients;

public sealed class CargoWorkspaceRecoveryR75Tests
{
    [Theory]
    [InlineData("recommendations", "추천 운송")]
    [InlineData("settlements/current-month", "월 정산")]
    [InlineData("reservations", "예약")]
    public async Task AuxiliaryFailure_PreservesVerifiedCurrentTransportAndReportsSliceFailure(string failingPath, string label)
    {
        using var f = new Fixture { FailingPath = failingPath };
        await f.Service.RefreshAsync(force: true);
        Assert.True(f.Service.ReadState.CurrentTransport.IsCurrent);
        Assert.Equal(75, f.Service.현재운송조회()!.Id);
        Assert.Equal("상차 완료", f.Service.현재운송조회()!.다음행동);
        Assert.Contains(f.Service.ReadState.Warnings, message => message.StartsWith(label));
        Assert.Single(f.Service.운송목록); Assert.Equal("운행중", f.Service.근무상태.운행상태);
        Assert.DoesNotContain(f.Service.추천의뢰목록, x => x.의뢰Id != "request-r75");
    }

    [Theory]
    [InlineData("recommendations")]
    [InlineData("reservations")]
    [InlineData("transports/workspace")]
    public async Task MissingListOrWorkspaceResponse_IsNotSuccessfulZeroWork(string path)
    {
        using var f = new Fixture { FailingPath = path, FailureStatus = HttpStatusCode.NotFound };
        await f.Service.RefreshAsync(force: true);
        var state = path switch
        {
            "recommendations" => f.Service.ReadState.Recommendations,
            "reservations" => f.Service.ReadState.Reservations,
            _ => f.Service.ReadState.CurrentTransport
        };
        Assert.Equal(DriverWorkspaceReadStatus.Failed, state.StateCode);
        Assert.False(state.IsCurrent); Assert.Contains("조회 실패", state.Message);
        if (path == "transports/workspace") { Assert.Null(f.Service.현재운송조회()); Assert.Empty(f.Service.운송목록); }
        else Assert.Equal(75, f.Service.현재운송조회()!.Id);
    }

    [Fact]
    public async Task VerifiedEmptyResponses_RemainSuccessfulEmptyWork()
    {
        using var f = new Fixture { EmptyWorkspace = true, EmptyRecommendations = true };
        await f.Service.RefreshAsync(force: true);
        Assert.True(f.Service.ReadState.CurrentTransport.IsCurrent);
        Assert.True(f.Service.ReadState.Recommendations.IsCurrent);
        Assert.True(f.Service.ReadState.Reservations.IsCurrent);
        Assert.Empty(f.Service.운송목록); Assert.Empty(f.Service.추천의뢰목록); Assert.Empty(f.Service.ReadState.Warnings);
    }

    [Fact]
    public async Task DetailFailure_KeepsOnlyVerifiedSummaryAndDoesNotInventCoordinates()
    {
        using var f = new Fixture { FailingPath = "transports/75" };
        await f.Service.RefreshAsync(force: true);
        var current = f.Service.현재운송조회()!;
        Assert.True(f.Service.ReadState.CurrentTransport.IsCurrent);
        Assert.Equal(DriverWorkspaceReadStatus.Failed, f.Service.ReadState.TransportDetail.StateCode);
        Assert.Equal("상차지도착", current.현재단계); Assert.Equal("상차 완료", current.다음행동);
        Assert.Null(current.픽업위도); Assert.Null(current.하차위도);
    }

    [Fact]
    public async Task CurrentTransportFailure_DoesNotKeepOldTransportOrUseAllowedSampleFallback()
    {
        using var f = new Fixture();
        await f.Service.RefreshAsync(force: true); Assert.Equal(75, f.Service.현재운송조회()!.Id);
        f.FailingPath = "transports/workspace";
        await f.Service.RefreshAsync(force: true);
        Assert.Null(f.Service.현재운송조회()); Assert.Empty(f.Service.운송목록);
        Assert.Equal(DriverWorkspaceReadStatus.Failed, f.Service.ReadState.CurrentTransport.StateCode);
        Assert.NotEqual(DriverWorkspaceReadStatus.Sample, f.Service.ReadState.CurrentTransport.StateCode);
    }

    [Fact]
    public async Task LateReplyAfterAccountSwitch_CannotPopulateAnotherAccountWorkspace()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.BeforeWorkspace = async () => { entered.SetResult(); await release.Task; };
        var load = f.Service.RefreshAsync(force: true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Auth.Login("other-driver"); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.Null(f.Service.현재운송조회()); Assert.Empty(f.Service.추천의뢰목록);
        Assert.Equal(DriverWorkspaceReadStatus.Unknown, f.Service.ReadState.CurrentTransport.StateCode);
    }

    [Fact]
    public async Task SameUserNewSession_ClearsExistingConfirmedWorkspaceImmediately()
    {
        using var f = new Fixture(); await f.Service.RefreshAsync(force: true);
        Assert.NotNull(f.Service.현재운송조회());
        await f.Auth.Login("driver-r75");
        Assert.Null(f.Service.현재운송조회()); Assert.Empty(f.Service.추천의뢰목록);
        Assert.Equal(DriverWorkspaceReadStatus.Unknown, f.Service.ReadState.Settlement.StateCode);
    }

    [Fact]
    public async Task CallerCancellation_DoesNotPublishEvenWhenProviderIgnoresToken()
    {
        using var f = new Fixture(); using var cancellation = new CancellationTokenSource();
        f.BeforeWorkspace = () => { cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.RefreshAsync(cancellation.Token, force: true));
        Assert.Null(f.Service.현재운송조회()); Assert.Equal(DriverWorkspaceReadStatus.Unknown, f.Service.ReadState.CurrentTransport.StateCode);
    }

    [Fact]
    public async Task WrongDriverInProtectedSlices_IsNotPublishedAsCurrentData()
    {
        using var f = new Fixture { WrongDriver = true };
        await f.Service.RefreshAsync(force: true);
        Assert.Null(f.Service.현재운송조회());
        Assert.Equal(DriverWorkspaceReadStatus.Failed, f.Service.ReadState.CurrentTransport.StateCode);
        Assert.Equal(DriverWorkspaceReadStatus.Failed, f.Service.ReadState.Work.StateCode);
        Assert.Equal(DriverWorkspaceReadStatus.Failed, f.Service.ReadState.Settlement.StateCode);
    }

    [Fact]
    public async Task RecommendationFailureAfterPriorSuccess_ClearsActionableOffersUntilVerifiedAgain()
    {
        using var f = new Fixture(); await f.Service.RefreshAsync(force: true);
        Assert.Single(f.Service.추천의뢰목록);
        f.FailingPath = "recommendations"; await f.Service.RefreshAsync(force: true);
        Assert.Empty(f.Service.추천의뢰목록); Assert.False(f.Service.ReadState.Recommendations.IsCurrent);
        Assert.Null(f.Service.추천의뢰조회("request-r75"));
        f.FailingPath = null; await f.Service.RefreshAsync(force: true);
        Assert.Single(f.Service.추천의뢰목록); Assert.True(f.Service.ReadState.Recommendations.IsCurrent);
    }

    [Fact]
    public async Task InFlightRefresh_InvalidatesActionableOldOfferUntilItsNewReadIsConfirmed()
    {
        using var f = new Fixture(); await f.Service.RefreshAsync(force: true);
        Assert.NotNull(f.Service.추천의뢰조회("request-r75"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.BeforeWorkspace = async () => { entered.SetResult(); await release.Task; };
        var refresh = f.Service.RefreshAsync(force: true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.Service.ReadState.Recommendations.IsCurrent);
        Assert.Null(f.Service.추천의뢰조회("request-r75"));
        release.SetResult(); await refresh;
        Assert.NotNull(f.Service.추천의뢰조회("request-r75"));
    }

    [Fact]
    public void ExplicitStaticSample_IsLabeledAndCanDisplayValues()
    {
        var samples = new 기사샘플데이터Service();
        Assert.Equal(DriverWorkspaceReadStatus.Sample, samples.ReadState.Recommendations.StateCode);
        Assert.True(samples.ReadState.Recommendations.CanDisplayValues);
        Assert.False(samples.ReadState.Recommendations.IsCurrent);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public readonly CargoWorkRecoveryR75Tests.RecoveryAuth Auth = new();
        public readonly ServerBackedDriverSampleDataService Service;
        public string? FailingPath;
        public HttpStatusCode FailureStatus = HttpStatusCode.ServiceUnavailable;
        public bool EmptyWorkspace, EmptyRecommendations, WrongDriver;
        public Func<Task>? BeforeWorkspace;
        private string Owner => WrongDriver ? "other-driver" : "driver-r75";
        public Fixture()
        {
            _http = new(new Handler(Handle)) { BaseAddress = new Uri("https://test.invalid/") };
            var api = new DriverApiClient(_http, Auth, new AuthApiService(_http, Auth), new DriverOperatingProfileService());
            var transports = new DriverTransportApiService(api);
            Service = new(Auth, new DriverFreightWorkspaceStore(transports), new DriverRecommendationApiService(api),
                new DriverSettlementApiService(api), new DriverReservationApiService(api), new DriverWorkApiService(api), transports,
                new 기사샘플데이터Service(), Options.Create(new ClientDataModeOptions { AllowSampleFallback = true }),
                null, new TransportRequestLedgerObserver());
        }
        private async Task<HttpResponseMessage> Handle(HttpRequestMessage request, CancellationToken _)
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/api/v1/driver/", string.Empty, StringComparison.Ordinal);
            Assert.Equal(HttpMethod.Get, request.Method);
            if (path == FailingPath) return new(FailureStatus);
            if (path == "transports/workspace" && BeforeWorkspace is not null) await BeforeWorkspace();
            object body = path switch
            {
                "transports/workspace" => new 기사화물운송작업공간응답
                { 활성운송목록 = EmptyWorkspace ? [] : [Summary()], 다음행동운송 = EmptyWorkspace ? null : Summary() },
                "transports/75" => new 기사운송상세응답
                { Id = 75, 기사_운송자 = Owner, 운송번호 = "request-r75", 상태 = "상차지도착", 출발지 = "시험 상차지", 도착지 = "시험 하차지", 운임 = 6500m, 픽업위도 = 37.57m, 픽업경도 = 127.07m },
                "work/status" => new 기사운행상태응답 { DriverId = Owner, ShiftId = 75, Status = "운행중" },
                "work/current" => new 기사현재근무응답 { DriverId = Owner, ShiftId = 75, 운행상태 = "운행중", 시작위치 = "시험 위치" },
                "recommendations" => EmptyRecommendations ? Array.Empty<기사배차추천항목응답>() : new[]
                { new 기사배차추천항목응답 { 의뢰Id = "request-r75", 화물종류 = "시험 소포", 픽업지 = "시험 상차지", 하차지 = "시험 하차지" } },
                "settlements/current-month" => new 기사정산응답 { DriverId = Owner, Year = 2026, Month = 10, DispatchCount = 1, UsageFee = 100m },
                "reservations" => Array.Empty<기사예약목록응답>(),
                _ => throw new InvalidOperationException("Unexpected test request: " + path)
            };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) };
        }
        private 기사운송요약응답 Summary() => new()
        { Id = 75, 기사_운송자 = Owner, 운송번호 = "request-r75", 상태 = "상차지도착", 출발지 = "시험 상차지", 도착지 = "시험 하차지", 운임 = 6500m };
        public void Dispose() => _http.Dispose();
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
