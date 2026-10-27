using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkflowCompletionFoodTests
{
    [Theory]
    [InlineData(DriverWorkKinds.FoodDelivery, false)]
    [InlineData(DriverWorkKinds.QuickDelivery, true)]
    public async Task 픽업응답과_같은시도_정본을확인하면_업무에맞는_안내를보인다(string kind, bool quick)
    {
        var api = new Api(false) { WorkKind = kind }; var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        Assert.Null(adapter.CompletionFeedback.Current);
        await adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사픽업확인, Guid.NewGuid(), default);
        var notice = adapter.CompletionFeedback.Current!;
        Assert.False(notice.IsFinal); Assert.Contains(quick ? "물품" : "음식", notice.Title);
        Assert.Equal(quick ? WorkflowCompletionVisuals.CargoIconPath : WorkflowCompletionVisuals.FoodIconPath, notice.IconPath);
        Assert.Equal(1, api.ActionPosts);
        adapter.CompletionFeedback.Dismiss();
        await adapter.LoadAsync(null, default);
        Assert.Null(adapter.CompletionFeedback.Current);
        Assert.Single(adapter.CompletionFeedback.History);
    }

    [Fact]
    public async Task 전달은_현재목록에서사라져도_같은시도_완료원장이없으면_성공으로표시하지않는다()
    {
        var api = new Api(true) { IncludeSettlement = false }; var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사전달완료, Guid.NewGuid(), default));
        Assert.Null(adapter.CompletionFeedback.Current);
        Assert.True(adapter.BlocksAction(음식배달가능행동Ids.기사전달완료));
        await adapter.LoadAsync(null, default);
        Assert.Null(adapter.CompletionFeedback.Current); Assert.Equal(1, api.ActionPosts);
        api.IncludeSettlement = true;
        await adapter.LoadAsync(null, default);
        Assert.True(adapter.CompletionFeedback.Current!.IsFinal);
        Assert.Contains("수령 확인·정산·입금", adapter.CompletionFeedback.Current.Message);
        Assert.Equal(1, api.ActionPosts); Assert.True(api.DetailReads > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 응답유실은_재전송을막고_같은시도_정본조회로만_완료안내를복구한다(bool final)
    {
        var api = new Api(final) { LoseResponse = true }; var adapter = Adapter(api);
        var action = final ? 음식배달가능행동Ids.기사전달완료 : 음식배달가능행동Ids.기사픽업확인;
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.PerformAsync("offer-1", action, Guid.NewGuid(), default));
        Assert.Null(adapter.CompletionFeedback.Current); Assert.True(adapter.BlocksAction(action));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-1", action, Guid.NewGuid(), default));
        await adapter.LoadAsync(null, default);
        Assert.Equal(final, adapter.CompletionFeedback.Current!.IsFinal);
        Assert.False(adapter.BlocksAction(action)); Assert.Equal(1, api.ActionPosts);
        Assert.True(adapter.ResolveCanonicalActions().OutcomeConfirmed);
    }

    [Theory]
    [InlineData("attempt")]
    [InlineData("owner")]
    [InlineData("order")]
    [InlineData("time")]
    public async Task 다른완료원장_상세를_현재전달의_완료로받아들이지않는다(string mismatch)
    {
        var api = new Api(true) { DetailMismatch = mismatch }; var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사전달완료, Guid.NewGuid(), default));
        Assert.Null(adapter.CompletionFeedback.Current); Assert.Equal(1, api.ActionPosts);
    }

    [Theory]
    [InlineData("evidence")]
    [InlineData("daily-kind")]
    [InlineData("daily-date")]
    [InlineData("detail-kind")]
    public async Task 완료근거없음과_모호한시계와_다른한국날짜는_전달성공으로표시하지않는다(string mismatch)
    {
        var api = new Api(true) { CompletionEvidenceMismatch = mismatch }; var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사전달완료, Guid.NewGuid(), default));
        Assert.Null(adapter.CompletionFeedback.Current);
        Assert.True(adapter.BlocksAction(음식배달가능행동Ids.기사전달완료));
        Assert.Equal(1, api.ActionPosts);
    }

    [Fact]
    public async Task 새화면의_완료조회는_안내를만들지않고_일시숨김과_계정초기화를_구분한다()
    {
        var api = new Api(false); var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        await adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사픽업확인, Guid.NewGuid(), default);
        adapter.ClearTransientSnapshot();
        Assert.Null(adapter.CompletionFeedback.Current); Assert.Single(adapter.CompletionFeedback.History);
        await adapter.LoadAsync(null, default);
        Assert.Null(adapter.CompletionFeedback.Current);
        adapter.Clear(); Assert.Empty(adapter.CompletionFeedback.History);
        await adapter.LoadAsync(null, default);
        Assert.Null(adapter.CompletionFeedback.Current);
    }

    [Fact]
    public async Task 알수없는업무유형은_음식그림으로_추정하지않는다()
    {
        var api = new Api(false) { WorkKind = "unknown" }; var adapter = Adapter(api);
        await adapter.LoadAsync(null, default);
        await adapter.PerformAsync("offer-1", 음식배달가능행동Ids.기사픽업확인, Guid.NewGuid(), default);
        Assert.Null(adapter.CompletionFeedback.Current);
        Assert.Equal(1, api.ActionPosts);
    }

    private static FoodDriverRoleWorkspaceAdapter Adapter(Api api) => new(api, new Location());
    private sealed class Location : IRoleWorkspaceLocationProvider
    {
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct)
            => Task.FromResult<RoleWorkspaceLocation?>(new(37.56, 127.07, 5, DateTimeOffset.UtcNow));
    }
    private sealed class Api(bool final) : IRoleWorkspaceApi
    {
        private readonly DateTime completedAt = DateTime.UtcNow;
        private bool completed;
        public string WorkKind { get; set; } = DriverWorkKinds.FoodDelivery;
        public bool IncludeSettlement { get; set; } = true;
        public bool LoseResponse { get; set; }
        public string? DetailMismatch { get; set; }
        public string? CompletionEvidenceMismatch { get; set; }
        public int ActionPosts { get; private set; }
        public int DetailReads { get; private set; }
        public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            object result;
            if (path.EndsWith("workspace/current", StringComparison.Ordinal)) result = new FoodDeliveryDriverCurrentWorkDto
            {
                DriverId = "driver-1", ActiveDeliveries = completed && final ? [] : [new()
                {
                    OfferId = "offer-1", OrderNo = "order-1", DeliveryAttemptId = "attempt-1", AttemptRevision = completed ? 6 : 5,
                    WorkKind = WorkKind, WorkStatus = completed || final ? DriverWorkOfferStatus.MovingToDropoff : DriverWorkOfferStatus.Accepted,
                    UpdatedAtUtc = completedAt, AvailableActions = completed ? [] : [new()
                    { ActionId = final ? 음식배달가능행동Ids.기사전달완료 : 음식배달가능행동Ids.기사픽업확인, ExpectedRevision = 5 }]
                }]
            };
            else if (path.EndsWith("work/status", StringComparison.Ordinal)) result = new 기사운행상태응답 { Status = "운행중" };
            else if (path.EndsWith("availability", StringComparison.Ordinal)) result = new 운영배차수신상태Dto
            { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
            else if (path.Contains("settlements/daily?date=", StringComparison.Ordinal)) result = new FoodDeliveryDailySettlementDto
            { DriverId = "driver-1", CompletionDateKst = DateOnly.Parse(path.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture),
                OrderSettlements = IncludeSettlement ? [Settlement(CompletionEvidenceMismatch == "daily-kind" ? "kind" : CompletionEvidenceMismatch == "daily-date" ? "date" : null)] : [] };
            else if (path.EndsWith("settlements/settlement-1/detail", StringComparison.Ordinal))
            {
                DetailReads++; result = new FoodDeliveryCompletedDeliveryDetailDto
                {
                    Settlement = Settlement(CompletionEvidenceMismatch == "detail-kind" ? "kind" : DetailMismatch),
                    DetailAccessStatusCode = CompletionEvidenceMismatch == "evidence" ? FoodDeliveryCompletedDetailAccessStatusCodes.CompletionEvidenceUnavailable
                        : FoodDeliveryCompletedDetailAccessStatusCodes.PolicyNotConfigured
                };
            }
            else throw new InvalidOperationException("Unexpected test read: " + path);
            return Task.FromResult((T)result);
        }
        private FoodDeliveryOrderSettlementDto Settlement(string? mismatch = null) => new()
        { SettlementId = "settlement-1", DriverId = mismatch == "owner" ? "other" : "driver-1",
            DeliveryAttemptId = mismatch == "attempt" ? "other" : "attempt-1", OrderNo = mismatch == "order" ? "other" : "order-1",
            CompletedAtUtc = mismatch == "time" ? completedAt.AddSeconds(1) : mismatch == "kind" ? DateTime.SpecifyKind(completedAt, DateTimeKind.Unspecified)
                : mismatch == "date" ? completedAt.AddDays(1) : completedAt };
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            if (path.EndsWith("work/location", StringComparison.Ordinal))
                return Task.FromResult((T?)(object)new 기사위치갱신응답 { 현재위도 = 37.56m, 현재경도 = 127.07m });
            ActionPosts++; completed = true;
            if (LoseResponse) throw new HttpRequestException("Synthetic response loss");
            return Task.FromResult((T?)(object)new FoodDeliveryDriverActionResponse
            { OfferId = "offer-1", DeliveryAttemptId = "attempt-1", AttemptRevision = 6, OccurredAtUtc = completedAt,
                Status = final ? DriverWorkOfferStatus.Completed : DriverWorkOfferStatus.MovingToDropoff });
        }
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
}
