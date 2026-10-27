using System.Net;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Ui.Admin;

public sealed class 운송의뢰상세수명Tests
{
    [Theory]
    [InlineData(true, "transient")]
    [InlineData(false, "transient")]
    [InlineData(true, "auth")]
    [InlineData(false, "auth")]
    [InlineData(true, "not-found")]
    [InlineData(false, "not-found")]
    [InlineData(true, "feature-off")]
    [InlineData(false, "feature-off")]
    [InlineData(true, "canceled")]
    [InlineData(false, "canceled")]
    public async Task 조회실패는_수동과자동조회의_기존정보보존과오류표시를유지한다(bool manual, string failure)
    {
        using var fixture = new Fixture();
        var previous = Request();
        fixture.Session.Request = previous;
        fixture.Read = _ => Task.FromException<화주운송의뢰응답?>(failure switch
        {
            "auth" => new HttpRequestException("auth", null, HttpStatusCode.Unauthorized),
            "not-found" => new HttpRequestException("missing", null, HttpStatusCode.NotFound),
            "feature-off" => new InvalidOperationException("FeatureDisabled"),
            "canceled" => new OperationCanceledException(),
            _ => new HttpRequestException("temporary")
        });

        await fixture.Session.LoadAsync(manual);

        Assert.False(fixture.Session.IsLoading);
        Assert.Equal(failure == "auth", fixture.Session.RequiresLogin);
        Assert.Equal(failure == "not-found", fixture.Session.IsNotFound);
        Assert.Equal(failure != "feature-off", fixture.Session.IsTransportWorkflowEnabled);
        if (manual || failure == "not-found") Assert.Null(fixture.Session.Request);
        else Assert.Same(previous, fixture.Session.Request);
        Assert.Equal(manual ? 1 : 0, fixture.MessagesCleared);
        Assert.Equal(manual && (failure is "transient" or "canceled")
            ? "운송 의뢰 상세를 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요."
            : null, fixture.Session.ErrorMessage);
    }

    [Theory]
    [InlineData("loading")]
    [InlineData("saving")]
    [InlineData("login")]
    [InlineData("feature-off")]
    [InlineData("feature-unknown")]
    [InlineData("empty-id")]
    public async Task 보완조회는_현재공용잠금과접근상태를그대로따른다(string block)
    {
        using var fixture = new Fixture();
        fixture.Session.IsLoading = block == "loading";
        fixture.IsSaving = block == "saving";
        fixture.Session.RequiresLogin = block == "login";
        if (block == "feature-off") fixture.Session.IsTransportWorkflowEnabled = false;
        if (block == "feature-unknown") fixture.Session.IsTransportWorkflowEnabled = null;
        if (block == "empty-id") fixture.RequestId = " ";

        await fixture.Session.PollOnceAsync();

        Assert.Empty(fixture.ReadIds);
        Assert.Equal(0, fixture.Dispatches);
    }

    [Fact]
    public async Task 정상보완조회는_UI호출안에서_조회후화면갱신하고_메시지를지우지않는다()
    {
        using var fixture = new Fixture();
        await fixture.Session.PollOnceAsync();

        Assert.Equal(new[] { "request" }, fixture.ReadIds);
        Assert.Equal(1, fixture.Dispatches);
        Assert.Equal(1, fixture.Renders);
        Assert.Equal(0, fixture.MessagesCleared);
        Assert.NotNull(fixture.Session.Request);
    }

    [Fact]
    public async Task 저장잠금중후속조회호출도_기존대로생략되고_잠금해제후조회한다()
    {
        using var fixture = new Fixture { IsSaving = true };
        await fixture.Session.LoadAsync(showMessage: false);
        Assert.Empty(fixture.ReadIds);

        fixture.IsSaving = false;
        await fixture.Session.LoadAsync(showMessage: false);
        Assert.Single(fixture.ReadIds);
    }

    [Fact]
    public async Task 이미시작한조회는_선택변경이나Dispose로_새로운응답차단을추가하지않는다()
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<화주운송의뢰응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Read = _ => pending.Task;
        var loading = fixture.Session.LoadAsync(showMessage: false);
        Assert.True(fixture.Session.IsLoading);
        await fixture.Session.LoadAsync(showMessage: true);
        Assert.Single(fixture.ReadIds);
        fixture.RequestId = "another-request";
        fixture.Dispose();
        var originalResponse = Request();
        pending.SetResult(originalResponse);
        await loading;

        Assert.Same(originalResponse, fixture.Session.Request);
        Assert.False(fixture.Session.IsLoading);
    }

    [Fact]
    public void 관찰구독은_한번만시작하고_ID를Trim과대소문자무시로맞추며_Dispose로해제한다()
    {
        var fixture = new Fixture { RequestId = " request " };
        fixture.Session.EnsureObserverStarted();
        fixture.Session.EnsureObserverStarted();
        Assert.Equal(1, fixture.Observer.ChangeSubscriptions);
        Assert.Equal(1, fixture.Observer.RefreshSubscriptions);
        fixture.Observer.Refresh("other");
        Assert.Empty(fixture.ReadIds);
        fixture.Observer.Refresh("REQUEST");
        fixture.Observer.Change("request");
        Assert.Equal(2, fixture.ReadIds.Count);
        Assert.All(fixture.ReadIds, id => Assert.Equal(" request ", id));
        fixture.Dispose();
        fixture.Observer.Refresh("request");

        Assert.Equal(0, fixture.Observer.ChangeSubscriptions);
        Assert.Equal(0, fixture.Observer.RefreshSubscriptions);
        Assert.Equal(2, fixture.ReadIds.Count);
        Assert.Equal(2, fixture.Renders);
    }

    [Fact]
    public void 관찰이벤트는_보완조회와달리_requiresLogin을추가차단조건으로쓰지않는다()
    {
        using var fixture = new Fixture();
        fixture.Session.RequiresLogin = true;
        fixture.Session.EnsureObserverStarted();
        fixture.Observer.Refresh("request");

        Assert.Single(fixture.ReadIds);
        Assert.True(fixture.Session.RequiresLogin);
    }

    [Fact]
    public async Task 메모리모드는_기능조회없이열고_기능조회실패는미확인과기존문구를유지한다()
    {
        using var fixture = new Fixture();
        fixture.Session.IsTransportWorkflowEnabled = false;
        fixture.Session.IsMemoryMode = true;
        Assert.True(await fixture.Session.EnsureTransportWorkflowEnabledAsync());
        Assert.Equal(0, fixture.FeatureReads);

        fixture.Session.IsMemoryMode = false;
        fixture.Session.IsTransportWorkflowEnabled = null;
        fixture.Feature = () => throw new HttpRequestException("temporary");
        Assert.False(await fixture.Session.EnsureTransportWorkflowEnabledAsync());
        Assert.Null(fixture.Session.IsTransportWorkflowEnabled);
        Assert.Equal("국내 운송 의뢰 상세 조회 가능 여부를 확인하지 못했습니다. 잠시 뒤 다시 시도해 주세요.", fixture.Session.ErrorMessage);
        fixture.Feature = () => Task.FromResult(false);
        Assert.False(await fixture.Session.EnsureTransportWorkflowEnabledAsync());
        Assert.False(await fixture.Session.EnsureTransportWorkflowEnabledAsync());
        Assert.Equal(2, fixture.FeatureReads);
    }

    [Fact]
    public void 취소입력의열기닫기는_대상을보존하고_명시확인과저장잠금을기존정책으로판정한다()
    {
        var request = Request();
        var review = new 지도신청가원장Response { 원장Id = "ledger", 운영원본Id = "request", Revision = 17 };
        var state = new 운송의뢰취소검토DecisionState
        {
            Review = review, Reason = "old reason", ConfirmationRequestId = "old"
        };
        state.Open();
        Assert.True(state.ShowConfirmation);
        Assert.False(state.ConfirmationStarted);
        Assert.False(state.CanSubmitCancellation(request, false));
        state.ConfirmationRequestId = " REQUEST ";
        state.Reason = new string('a', 300);
        Assert.True(state.ConfirmationStarted);
        Assert.Null(state.ConfirmationError(request.의뢰Id));
        Assert.True(state.CanSubmitCancellation(request, false));
        Assert.True(state.CanSubmitReviewDecision(request, false));
        Assert.False(state.CanSubmitCancellation(request, true));
        Assert.False(state.CanSubmitReviewDecision(request, true));
        state.Reason += "a";
        Assert.Equal("취소 또는 환불 사유는 300자 이하여야 합니다.", state.ConfirmationError(request.의뢰Id));
        state.Close();
        Assert.False(state.ShowConfirmation);
        Assert.Null(state.Reason);
        Assert.Null(state.ConfirmationRequestId);
        Assert.Same(review, state.Review);
    }

    [Fact]
    public void 검토대상은_원장과운영원본을모두정확히대조하고_원래Revision을보존한다()
    {
        var expected = new 지도신청가원장Response { 원장Id = "ledger", 운영원본Id = "request", Revision = 17 };
        var state = new 운송의뢰취소검토DecisionState();
        state.SelectReview([
            new() { 원장Id = "Ledger", 운영원본Id = "request", Revision = 1 },
            new() { 원장Id = "ledger", 운영원본Id = "Request", Revision = 2 },
            expected
        ], "ledger", "request");
        Assert.Same(expected, state.Review);
        Assert.Equal(17, state.Review!.Revision);
        state.SelectReview([expected], "ledger", "other");
        Assert.Null(state.Review);
    }

    [Fact]
    public void 취소검토거절가능여부는_일반취소정책과다른현재조건을보존한다()
    {
        var request = Request();
        request.의뢰상태 = "운송중";
        var state = new 운송의뢰취소검토DecisionState
        {
            Review = new(), ConfirmationRequestId = "request", Reason = "검토 사유"
        };
        Assert.False(state.CanSubmitCancellation(request, false));
        Assert.True(state.CanSubmitReviewDecision(request, false));
        Assert.False(state.CanSubmitReviewDecision(null, false));
        Assert.Equal("의뢰를 먼저 조회해 주세요.", state.Decision(null).안내문구);
    }

    private static 화주운송의뢰응답 Request() => new()
    {
        의뢰Id = "request", 의뢰상태 = "접수", 결제상태 = "미결제", 정산상태 = "대기", 배차상태 = "대기"
    };

    private sealed class Fixture : IDisposable
    {
        public string RequestId { get; set; } = "request";
        public bool IsSaving { get; set; }
        public Func<string, Task<화주운송의뢰응답?>> Read { get; set; } = _ => Task.FromResult<화주운송의뢰응답?>(Request());
        public Func<Task<bool>> Feature { get; set; } = () => Task.FromResult(true);
        public List<string> ReadIds { get; } = [];
        public Observer Observer { get; } = new();
        public int FeatureReads { get; private set; }
        public int Dispatches { get; private set; }
        public int Renders { get; private set; }
        public int MessagesCleared { get; private set; }
        public 운송의뢰상세ReadSession Session { get; }

        public Fixture()
        {
            Session = new(id => { ReadIds.Add(id); return Read(id); },
                () => { FeatureReads++; return Feature(); }, Observer,
                () => RequestId, () => IsSaving,
                async action => { Dispatches++; await action(); },
                () => Renders++, () => MessagesCleared++)
            { IsTransportWorkflowEnabled = true };
        }

        public void Dispose() => Session.Dispose();
    }

    private sealed class Observer : ITransportRequestLedgerObserver
    {
        private Action<TransportRequestLedgerChange>? changed;
        private Action<TransportRequestLedgerRefreshRequest>? refreshRequested;
        public int ChangeSubscriptions { get; private set; }
        public int RefreshSubscriptions { get; private set; }
        public event Action<TransportRequestLedgerChange>? Changed
        {
            add { changed += value; ChangeSubscriptions++; }
            remove { changed -= value; ChangeSubscriptions--; }
        }
        public event Action<TransportRequestLedgerRefreshRequest>? RefreshRequested
        {
            add { refreshRequested += value; RefreshSubscriptions++; }
            remove { refreshRequested -= value; RefreshSubscriptions--; }
        }
        public void Refresh(string id) => refreshRequested?.Invoke(new(id, "test", DateTimeOffset.UnixEpoch));
        public void Change(string id)
        {
            var snapshot = new TransportRequestLedgerSnapshot(id, null, null, null, null, DateTimeOffset.UnixEpoch, "test");
            changed?.Invoke(new(id, snapshot, snapshot, "test"));
        }
        public TransportRequestLedgerSnapshot? GetSnapshot(string id) => null;
        public bool Observe(TransportRequestLedgerSnapshot snapshot, string reason) => throw new NotSupportedException();
        public bool ObserveServerEvent(TransportRequestLedgerServerEvent serverEvent) => throw new NotSupportedException();
        public void RequestRefresh(string id, string reason) => throw new NotSupportedException();
    }
}
