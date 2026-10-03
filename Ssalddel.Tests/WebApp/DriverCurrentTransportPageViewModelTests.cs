using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.WebApp.Pages.DriverCurrentTransport;
using Ssalddel.WebApp.ViewModels;

namespace Ssalddel.Tests.WebApp;

public sealed class DriverCurrentTransportPageViewModelTests
{
    [Fact]
    public async Task InitializeAsync_수락_운송이_생성되면_자동_재조회로_연결한다()
    {
        var loadCount = 0;
        using var viewModel = CreateViewModel(
            loadCurrentTransport: _ => Task.FromResult(++loadCount == 1
                ? Transport("REQ-OLD", "운송중")
                : Transport("REQ-ACCEPTED", "배차확정")));

        await viewModel.InitializeAsync(" REQ-ACCEPTED ");

        Assert.Equal(2, loadCount);
        Assert.Equal("REQ-ACCEPTED", viewModel.AcceptedRequestId);
        Assert.True(viewModel.IsAcceptedRequestLoaded);
        Assert.False(viewModel.IsWaitingForAcceptedTransport);
        Assert.Equal(DriverCurrentTransportMessageTone.Success, viewModel.StatusTone);
        Assert.Contains("생성된 현재 운송", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_현재_운송만_다시_조회한다()
    {
        var loadCount = 0;
        using var viewModel = CreateViewModel(
            loadCurrentTransport: _ => Task.FromResult(++loadCount == 1
                ? Transport("REQ-10", "배차확정", id: 10)
                : Transport("REQ-10", "상차지 도착", id: 10)));
        await viewModel.InitializeAsync(null);

        await viewModel.Refresh.RefreshAsync();

        Assert.Equal(2, loadCount);
        Assert.Equal("상차지 도착", viewModel.CurrentTransport?.상태);
        Assert.False(viewModel.Refresh.IsBusy);
        Assert.Equal(DriverCurrentTransportMessageTone.Success, viewModel.StatusTone);
    }

    [Fact]
    public async Task RefreshAsync_서버_오류를_화면_상태로_격리한다()
    {
        var loadCount = 0;
        using var viewModel = CreateViewModel(
            loadCurrentTransport: _ => ++loadCount == 1
                ? Task.FromResult(Transport("REQ-20", "운송중", id: 20))
                : throw new InvalidOperationException("현재 운송 조회 실패"));
        await viewModel.InitializeAsync(null);

        await viewModel.Refresh.RefreshAsync();

        Assert.False(viewModel.Refresh.IsBusy);
        Assert.Equal(DriverCurrentTransportMessageTone.Error, viewModel.StatusTone);
        Assert.Equal("현재 운송 조회 실패", viewModel.StatusMessage);
    }

    [Fact]
    public void RefreshSession은_현재_운송_event만_전달하고_dispose에서_구독을_해제한다()
    {
        var observer = new TestTransportRequestLedgerObserver();
        var refreshCount = 0;
        var session = new DriverCurrentTransportRefreshSession(
            observer,
            requestId => requestId == "REQ-40",
            () => false);
        session.RefreshRequested += () => refreshCount++;
        session.Start();

        observer.RaiseRefreshRequested(new TransportRequestLedgerRefreshRequest(
            "REQ-OTHER",
            "test",
            DateTimeOffset.UtcNow));
        observer.RaiseRefreshRequested(new TransportRequestLedgerRefreshRequest(
            "REQ-40",
            "test",
            DateTimeOffset.UtcNow));
        var previous = new TransportRequestLedgerSnapshot(
            "REQ-40", "배차확정", null, "배차확정", null, DateTimeOffset.UtcNow, "test");
        var current = previous with { RequestStatus = "운송중", DispatchStatus = "운송중" };
        observer.RaiseChanged(new TransportRequestLedgerChange(
            "REQ-40",
            previous,
            current,
            "test"));

        Assert.Equal(2, refreshCount);

        session.Dispose();
        observer.RaiseRefreshRequested(new TransportRequestLedgerRefreshRequest(
            "REQ-40",
            "after-dispose",
            DateTimeOffset.UtcNow));
        Assert.Equal(2, refreshCount);
    }

    [Theory]
    [InlineData("확정", 1, "상차 증빙으로")]
    [InlineData("배차확정", 1, "상차 증빙으로")]
    [InlineData("상차지 도착", 2, "상차 증빙으로")]
    [InlineData("상차완료", 3, "하차 증빙으로")]
    [InlineData("운송중", 3, "하차 증빙으로")]
    [InlineData("하차지 도착", 4, "하차 증빙으로")]
    [InlineData("인수완료", 5, "하차 증빙으로")]
    public void Presentation은_상태별_단계와_다음_증빙을_일관되게_계산한다(
        string status,
        int expectedOrder,
        string expectedButtonLabel)
    {
        var transport = Transport("REQ-30", status, id: 30);

        var nextAction = DriverCurrentTransportPresentation.ResolveNextAction(transport);
        var timeline = DriverCurrentTransportPresentation.BuildTimeline(status);

        Assert.Equal(expectedOrder, DriverCurrentTransportPresentation.ResolveStageOrder(status));
        Assert.Equal(expectedButtonLabel, nextAction.ButtonLabel);
        Assert.Equal($"/driver/transports/30/{(expectedOrder <= 2 ? "pickup" : "dropoff")}", nextAction.Href);
        Assert.Equal("현재", timeline[expectedOrder].Status);
        Assert.All(timeline.Take(expectedOrder), step => Assert.Equal("완료", step.Status));
        Assert.All(timeline.Skip(expectedOrder + 1), step => Assert.Equal("대기", step.Status));
    }

    private static DriverCurrentTransportPageViewModel CreateViewModel(
        Func<CancellationToken, Task<기사운송요약응답>>? loadCurrentTransport = null)
        => new(
            loadCurrentTransport ?? (_ => Task.FromResult(Transport("REQ-1", "배차확정"))),
            new TestTransportRequestLedgerObserver());

    private static 기사운송요약응답 Transport(string requestId, string status, long id = 1)
        => new()
        {
            Id = id,
            운송번호 = requestId,
            상태 = status,
            출발지 = "서울 공동창고",
            도착지 = "인천 수령지",
            UpdatedAt = new DateTime(2026, 7, 21, 9, 30, 0, DateTimeKind.Utc)
        };

    private sealed class TestTransportRequestLedgerObserver : ITransportRequestLedgerObserver
    {
        public event Action<TransportRequestLedgerChange>? Changed;
        public event Action<TransportRequestLedgerRefreshRequest>? RefreshRequested;

        public TransportRequestLedgerSnapshot? GetSnapshot(string requestId) => null;

        public bool Observe(TransportRequestLedgerSnapshot snapshot, string reason) => false;

        public bool ObserveServerEvent(TransportRequestLedgerServerEvent serverEvent) => false;

        public void RequestRefresh(string requestId, string reason)
        {
        }

        public void RaiseChanged(TransportRequestLedgerChange change) => Changed?.Invoke(change);

        public void RaiseRefreshRequested(TransportRequestLedgerRefreshRequest request)
            => RefreshRequested?.Invoke(request);
    }
}
