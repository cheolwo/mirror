using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;

namespace Ssalddel.Tests.Ui.Common;

public sealed class AgreedRateTableViewModelTests
{
    [Theory]
    [InlineData("restaurant", "Restaurant", "FoodDriver", "FoodDelivery")]
    [InlineData("food-driver", "FoodDriver", "Restaurant", "FoodDelivery")]
    [InlineData("shipper", "Shipper", "CargoDriver", "CargoTransport")]
    [InlineData("cargo-driver", "CargoDriver", "Shipper", "CargoTransport")]
    public void 기존_역할키를_서버의_당사자와_업무종류로_명시매핑한다(string role, string party, string other, string business)
    {
        var client = new AgreedRateTableClient(new RateUiApi(), role);
        Assert.Equal(party, client.PartyKind); Assert.Equal(other, client.CounterpartyKind); Assert.Equal(business, client.BusinessKind);
        Assert.Throws<ArgumentException>(() => new AgreedRateTableClient(new RateUiApi(), "operator"));
    }
    [Fact]
    public async Task 익명은_조회와_제안을_실행하지_않는다()
    {
        var api = new RateUiApi(); var access = new RateUiAccess { Identity = new(null, 0, false) };
        using var model = new AgreedRateTableViewModel(api, access, "restaurant");
        await model.LoadAsync(); model.ProposalConfirmed = true; await model.ProposeAsync();
        Assert.True(model.RequiresLogin); Assert.Empty(api.Gets); Assert.Empty(api.Posts); Assert.Empty(model.Items);
    }
    [Fact]
    public async Task 본인제안과_받은제안은_서버_CurrentParty로_구분한다()
    {
        var api = new RateUiApi(); var sent = RateUiData.Table(); var received = RateUiData.Table("rate-2");
        (received.Proposer, received.Counterparty) = (received.Counterparty, received.Proposer);
        api.Get = (_, _, _) => Task.FromResult<object>(new[] { sent, received });
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync();
        Assert.Equal("rate-1", Assert.Single(model.Sent).AgreementId); Assert.Equal("rate-2", Assert.Single(model.Received).AgreementId);
    }
    [Theory]
    [InlineData("CurrentParty")]
    [InlineData("BusinessKind")]
    [InlineData("Pricing")]
    public async Task 다른당사자_업무종류_혼합단가는_정상목록으로_표시하지않는다(string mismatch)
    {
        var table = RateUiData.Table();
        if (mismatch == "CurrentParty") table.CurrentParty = new("FoodDriver", "driver-1");
        if (mismatch == "BusinessKind") table.BusinessKind = "CargoTransport";
        if (mismatch == "Pricing") table.CargoPricing = new(1, 1, 1);
        var api = new RateUiApi { Get = (_, _, _) => Task.FromResult<object>(new[] { table }) };
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync();
        Assert.NotNull(model.Error); Assert.False(model.IsLoaded); Assert.Empty(model.Items);
    }
    [Fact]
    public async Task 서버가_허용하지않은_행동은_상태문구로_추론하지않는다()
    {
        var table = RateUiData.Table(); table.StatusCode = "Proposed";
        var api = new RateUiApi { Table = table };
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Accept"); await model.ActAsync("Reject");
        Assert.Empty(api.Posts); Assert.False(model.CanAct("Accept"));
    }
    [Fact]
    public async Task 응답유실은_같은요청번호와_원래판본으로_재확인한다()
    {
        var api = new RateUiApi(); api.Table.CanAccept = true;
        api.Post = (_, _, body, _) => api.Posts.Count == 1 ? throw new HttpRequestException("lost") : Task.FromResult<object?>(api.DefaultPost(body));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Accept");
        Assert.True(model.HasPendingCommand); Assert.False(model.CanEdit); var requestId = model.RequestId;
        await model.RetryPendingAsync();
        Assert.False(model.HasPendingCommand); Assert.Equal(requestId, ((AgreedRateTableActionRequest)api.Posts[0].Body).RequestId);
        Assert.Same(api.Posts[0].Body, api.Posts[1].Body); Assert.Equal(4, ((AgreedRateTableActionRequest)api.Posts[1].Body).ExpectedRevision);
    }
    [Fact]
    public async Task 저장응답후_재조회실패는_저장을_재전송하지않는다()
    {
        var api = new RateUiApi(); api.Table.CanWithdraw = true;
        var detailFailures = 1;
        api.Get = (_, path, _) => path.Contains("/rate-1?") && api.Posts.Count > 0 && detailFailures-- > 0
            ? throw new HttpRequestException("read lost") : Task.FromResult<object>(path.Contains("/rate-1?") ? api.Table : new[] { api.Table });
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Withdraw"); Assert.True(model.HasPendingCommand);
        await model.RetryPendingAsync(); Assert.Single(api.Posts); Assert.False(model.HasPendingCommand); Assert.Null(model.Error);
    }
    [Fact]
    public async Task 판본충돌은_새판본으로_자동수락하지않고_다시확인을_요구한다()
    {
        var api = new RateUiApi(); api.Table.CanAccept = true;
        api.Post = (_, _, _, _) => throw new RoleWorkspaceAccessException(409, "conflict", "RevisionConflict");
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Accept"); await model.RetryPendingAsync();
        Assert.Single(api.Posts); Assert.False(model.HasPendingCommand); Assert.False(model.Confirmed); Assert.NotNull(model.Error);
        api.Table.Revision = 5; await model.LoadAsync(); await model.ActAsync("Accept"); Assert.Single(api.Posts);
    }
    [Theory]
    [InlineData("logout")]
    [InlineData("account")]
    [InlineData("revision")]
    [InlineData("dispose")]
    public async Task 계정변경과_폐기뒤_늦은조회는_비공개목록을_복원하지않는다(string change)
    {
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new RateUiApi { Get = (_, _, _) => { started.SetResult(); return completion.Task; } }; var access = new RateUiAccess();
        using var model = new AgreedRateTableViewModel(api, access, "restaurant"); var load = model.LoadAsync(); await started.Task;
        if (change == "dispose") model.Dispose();
        else access.Change(change switch { "logout" => new(null, 2, false), "account" => new("other", 2, true), _ => new("owner", 2, true) });
        completion.SetResult(new[] { RateUiData.Table() }); await load;
        Assert.Empty(model.Items); Assert.False(model.IsLoaded); Assert.Null(model.Selected);
    }
    [Fact]
    public async Task 요청중_계정변경은_이전요청과_초안을_폐기한다()
    {
        var api = new RateUiApi(); api.Table.CanAccept = true; var access = new RateUiAccess();
        api.Post = (_, _, _, _) => throw new HttpRequestException("lost");
        using var model = new AgreedRateTableViewModel(api, access, "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Accept"); Assert.NotNull(model.RequestId);
        access.Change(new("other", 2, true)); await model.RetryPendingAsync();
        Assert.Null(model.RequestId); Assert.Single(api.Posts); Assert.Empty(model.Items); Assert.Equal("", model.Draft.CounterpartyId);
    }
    [Fact]
    public async Task 조회와_계산은_업무에_자동선택하지않으며_명시확인과_재조회가_필요하다()
    {
        var api = new RateUiApi(); using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant");
        model.SetWork("order-1"); await model.LoadAsync("rate-1");
        await model.SelectAsync(); Assert.Empty(api.Posts);
        model.DistanceKm = 1.2m; model.DistanceBasisCode = "AppDisplayed"; await model.PreviewAsync();
        Assert.Equal("PreviewOnly", model.Preview!.StatusCode); Assert.Null(model.Selection); Assert.Single(api.Posts);
        model.SelectionConfirmed = true; await model.SelectAsync();
        Assert.NotNull(model.Selection); Assert.Contains(api.Gets, path => path.Contains("/selections/order-1?partyKind=Restaurant"));
        Assert.Equal(4, model.Selection!.Agreement.Revision); Assert.False(model.HasPendingCommand);
    }
    [Fact]
    public async Task 선택저장후_독립재조회실패는_선택완료를_인증하지않는다()
    {
        var api = new RateUiApi(); var failures = 1;
        api.Get = (_, path, _) => path.Contains("/selections/") && api.Posts.Count > 0 && failures-- > 0 ? throw new HttpRequestException("read lost") : Task.FromResult(api.DefaultGet(path));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        model.SelectionConfirmed = true; await model.SelectAsync(); Assert.Null(model.Selection); Assert.True(model.HasPendingCommand);
        await model.RetryPendingAsync(); Assert.NotNull(model.Selection); Assert.Single(api.Posts);
    }
    [Fact]
    public async Task 선택응답의_다른업무와_단가를_성공으로_표시하지않는다()
    {
        var api = new RateUiApi(); api.Post = (_, _, _, _) => Task.FromResult<object?>(new AgreedRateTableSelectionDto("FoodDelivery", "other-order", RateUiData.Snapshot(api.Table)));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        model.SelectionConfirmed = true; await model.SelectAsync(); Assert.Null(model.Selection); Assert.NotNull(model.Error);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("../bad")]
    public async Task 업무문맥이없는_관리는_특정업무_선택을_실행하지않는다(string? work)
    {
        var api = new RateUiApi(); using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant");
        model.SetWork(work); await model.LoadAsync("rate-1"); model.SelectionConfirmed = true; await model.SelectAsync();
        Assert.False(model.CanSelect); Assert.Empty(api.Posts);
    }
    [Fact]
    public async Task 미확인거리는_0으로_보내지않으며_명시적0은_계산할수있다()
    {
        var api = new RateUiApi(); using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.DistanceBasisCode = "AppDisplayed"; await model.PreviewAsync(); Assert.Empty(api.Posts); Assert.NotNull(model.Error);
        await model.LoadAsync(); model.DistanceKm = 0; await model.PreviewAsync(); Assert.Single(api.Posts);
        Assert.Equal(0, ((AgreedRateTablePreviewRequest)api.Posts[0].Body).DistanceKm);
    }
    [Fact]
    public async Task 음식기사의_계산권한과_업무선택권한은_분리한다()
    {
        var api = new RateUiApi(); api.Table.CurrentParty = api.Table.Counterparty; api.Table.CanSelect = false;
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "food-driver"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        Assert.True(model.CanPreview); Assert.False(model.CanSelect);
        model.DistanceKm = 1; model.DistanceBasisCode = "AppDisplayed"; await model.PreviewAsync(); Assert.NotNull(model.Preview);
    }
    [Fact]
    public async Task 초안에는_기본단가가없고_유효제안만_입력값으로_전송한다()
    {
        var api = new RateUiApi(); using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync();
        Assert.Null(model.Draft.PickupFeeKrw); model.ProposalConfirmed = true; await model.ProposeAsync(); Assert.Empty(api.Posts);
        RateUiData.Fill(model.Draft); await model.ProposeAsync();
        var body = Assert.IsType<AgreedRateTableProposalRequest>(Assert.Single(api.Posts).Body);
        Assert.Equal(700, body.FoodPricing!.PickupFeeKrw); Assert.Equal("Floor", body.FoodPricing.DistanceRoundingCode);
        Assert.Equal(DateTimeKind.Utc, body.EffectiveFromUtc.Kind); Assert.Equal("FoodDriver", body.CounterpartyPartyKind);
    }
    [Fact]
    public async Task 저장확인후_조회404도_동일저장요청을_유지하고_조회만_다시한다()
    {
        var api = new RateUiApi(); api.Table.CanWithdraw = true; var failures = 1;
        api.Get = (_, path, _) => api.Posts.Count > 0 && path.Contains("/rate-1?") && failures-- > 0
            ? throw new RoleWorkspaceAccessException(404, "read unavailable") : Task.FromResult(api.DefaultGet(path));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Withdraw"); Assert.True(model.HasPendingCommand);
        await model.RetryPendingAsync(); Assert.Single(api.Posts); Assert.False(model.HasPendingCommand); Assert.Null(model.Error);
    }
    [Fact]
    public async Task 판본이_증가하지않은_행동응답은_완료로_인증하지않는다()
    {
        var api = new RateUiApi(); api.Table.CanAccept = true; api.Post = (_, _, _, _) => Task.FromResult<object?>(api.Table);
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.Confirmed = true; await model.ActAsync("Accept"); Assert.True(model.HasPendingCommand); Assert.NotNull(model.Error); Assert.Null(model.Message);
    }
    [Fact]
    public async Task 다른단가로_저장된_제안은_완료로_인증하지않는다()
    {
        var api = new RateUiApi(); api.Post = (_, _, body, _) =>
        {
            var result = (AgreedRateTableDto)api.DefaultPost(body); result.FoodPricing = result.FoodPricing! with { PickupFeeKrw = 900 };
            return Task.FromResult<object?>(result);
        };
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync(); RateUiData.Fill(model.Draft);
        model.ProposalConfirmed = true; await model.ProposeAsync(); Assert.True(model.HasPendingCommand); Assert.NotNull(model.Error); Assert.Null(model.Message);
    }
    [Fact]
    public async Task 분담방식을_바꾸면_숨긴_이전금액을_새조건으로_보내지않는다()
    {
        var api = new RateUiApi(); using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "restaurant"); await model.LoadAsync("rate-1");
        model.IncludeCostSharing = true; model.CostSharingMode = "CustomerPays"; model.FixedAmountKrw = 300; model.RestaurantSharePercent = 50; model.MaximumRestaurantChargeKrw = 400;
        model.DistanceKm = 1; model.DistanceBasisCode = "AppDisplayed"; await model.PreviewAsync();
        var policy = ((AgreedRateTablePreviewRequest)Assert.Single(api.Posts).Body).CostSharingPolicy!;
        Assert.Null(policy.FixedAmountKrw); Assert.Null(policy.RestaurantSharePercent); Assert.Null(policy.MaximumRestaurantChargeKrw); Assert.NotNull(model.Preview);
    }
    [Fact]
    public void 한국시각_자정근처의_요금정책도_UTC적용기간을_포함한다()
    {
        var draft = new AgreedRateProposalDraft(); RateUiData.Fill(draft); draft.EffectiveFromLocal = new(2026,10,9,1,0,0);
        var request = draft.Build("Restaurant", "FoodDriver", "FoodDelivery");
        Assert.Equal(new DateTime(2026,10,8,16,0,0,DateTimeKind.Utc), request.EffectiveFromUtc);
        Assert.Equal(new DateOnly(2026,10,8), request.FoodPricing!.EffectiveFrom);
    }
    [Fact]
    public async Task 화물원장대기에서_적용됨으로_진행한_재조회는_동결근거를_보존한다()
    {
        var api = new RateUiApi(); api.Table.BusinessKind = "CargoTransport"; api.Table.Proposer = new("Shipper", "shipper-1");
        api.Table.Counterparty = new("CargoDriver", "driver-1"); api.Table.CurrentParty = api.Table.Proposer;
        api.Table.FoodPricing = null; api.Table.CargoPricing = new(20000, 1200, 20000);
        api.Post = (_, _, body, _) => Task.FromResult<object?>(new AgreedRateTableSelectionDto("CargoTransport", ((AgreedRateTableSelectionRequest)body).WorkId, RateUiData.Snapshot(api.Table))
            { SelectionRequestId = ((AgreedRateTableSelectionRequest)body).RequestId, StatusCode = "LedgerSyncPending", AgreedAmountKrw = 23600, DistanceKm = 3, DistanceBasisCode = "ConfirmedRoute" });
        api.Get = (_, path, _) => Task.FromResult(path.Contains("/selections/") && api.Posts.Count > 0
            ? (object)new AgreedRateTableSelectionDto("CargoTransport", "order-1", RateUiData.Snapshot(api.Table))
                { SelectionRequestId = ((AgreedRateTableSelectionRequest)api.Posts[0].Body).RequestId, StatusCode = "Applied", AgreedAmountKrw = 23600, DistanceKm = 3, DistanceBasisCode = "ConfirmedRoute" } : api.DefaultGet(path));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "shipper"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        model.SelectionConfirmed = true; await model.SelectAsync();
        Assert.Equal("Applied", model.Selection!.StatusCode); Assert.Equal(23600, model.Selection.AgreedAmountKrw);
        Assert.False(model.HasPendingCommand); Assert.Null(model.Error); Assert.Single(api.Posts);
    }
    [Fact]
    public async Task 화면재진입과_요금표철회뒤에도_같은요청으로_원장대기만_복구한다()
    {
        var api = new RateUiApi(); api.Table.BusinessKind = "CargoTransport"; api.Table.Proposer = new("Shipper", "shipper-1");
        api.Table.Counterparty = new("CargoDriver", "driver-1"); api.Table.CurrentParty = api.Table.Proposer;
        api.Table.FoodPricing = null; api.Table.CargoPricing = new(20000, 1200, 20000);
        var frozen = RateUiData.Snapshot(api.Table); var requestId = Guid.NewGuid();
        var result = new AgreedRateTableSelectionDto("CargoTransport", "order-1", frozen)
        { SelectionRequestId = requestId, CanRetrySynchronization = true, StatusCode = "LedgerSyncPending", AgreedAmountKrw = 23600, DistanceKm = 3, DistanceBasisCode = "ConfirmedRoute" };
        api.Table.Revision = 5; api.Table.StatusCode = "Withdrawn"; api.Table.EffectiveStatusCode = "Withdrawn"; api.Table.CanSelect = api.Table.CanPreview = false;
        api.Get = (_, path, _) => Task.FromResult(path.Contains("/selections/") ? (object)result : api.DefaultGet(path));
        api.Post = (_, _, body, _) =>
        {
            var retry = (AgreedRateTableSelectionRequest)body;
            Assert.Equal(requestId, retry.RequestId); Assert.Equal(4, retry.ExpectedRevision); Assert.Equal("order-1", retry.WorkId);
            result = result with { StatusCode = "Applied", CanRetrySynchronization = false }; return Task.FromResult<object?>(result);
        };
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "shipper"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        Assert.False(model.CanSelect); Assert.True(model.CanRetrySynchronization); Assert.Empty(api.Posts);
        await model.RetrySynchronizationAsync();
        Assert.Equal("Applied", model.Selection!.StatusCode); Assert.False(model.CanRetrySynchronization); Assert.Null(model.Error); Assert.Single(api.Posts);
    }
    [Fact]
    public async Task 원장대기라도_서버가_복구를_허용하지않으면_원선택을_재전송하지않는다()
    {
        var api = new RateUiApi(); api.Table.BusinessKind = "CargoTransport"; api.Table.Proposer = new("Shipper", "shipper-1");
        api.Table.Counterparty = new("CargoDriver", "driver-1"); api.Table.CurrentParty = api.Table.Counterparty;
        api.Table.FoodPricing = null; api.Table.CargoPricing = new(20000,1200,20000); api.Table.CanSelect = false;
        var result = new AgreedRateTableSelectionDto("CargoTransport", "order-1", RateUiData.Snapshot(api.Table))
        { SelectionRequestId = Guid.NewGuid(), StatusCode = "LedgerSyncPending", CanRetrySynchronization = false };
        api.Get = (_, path, _) => Task.FromResult(path.Contains("/selections/") ? (object)result : api.DefaultGet(path));
        using var model = new AgreedRateTableViewModel(api, new RateUiAccess(), "cargo-driver"); model.SetWork("order-1"); await model.LoadAsync("rate-1");
        Assert.False(model.CanRetrySynchronization); await model.RetrySynchronizationAsync(); Assert.Empty(api.Posts);
    }
}

internal sealed class RateUiAccess : IRoleWorkspaceAccess
{
    public RoleWorkspaceIdentity Identity { get; set; } = new("owner", 1, true);
    public event Action? Changed;
    public void Change(RoleWorkspaceIdentity value) { Identity = value; Changed?.Invoke(); }
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => Identity;
    public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
    public Task SignOutAsync(string roleKey, CancellationToken ct = default) { Change(new(null, Identity.Revision + 1, false)); return Task.CompletedTask; }
}
internal sealed class RateUiApi : IRoleWorkspaceApi
{
    public AgreedRateTableDto Table { get; set; } = RateUiData.Table();
    public List<string> Gets { get; } = [];
    public List<(string Path, object Body)> Posts { get; } = [];
    public Func<string, string, CancellationToken, Task<object>>? Get { get; set; }
    public Func<string, string, object, CancellationToken, Task<object?>>? Post { get; set; }
    public object DefaultGet(string path) => path.Contains("/selections/") ? Posts.Any(call => call.Body is AgreedRateTableSelectionRequest)
        ? new AgreedRateTableSelectionDto(Table.BusinessKind, "order-1", RateUiData.Snapshot(Table))
            { SelectionRequestId = ((AgreedRateTableSelectionRequest)Posts.First(call => call.Body is AgreedRateTableSelectionRequest).Body).RequestId } : throw new RoleWorkspaceAccessException(404, "No selection")
        : path.Split('?', 2)[0].Split('/').Length == 4 ? Table : new[] { Table };
    public async Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
    { Gets.Add(path); return (T)(Get is null ? DefaultGet(path) : await Get(role, path, ct)); }
    public async Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
    {
        Posts.Add((path, body));
        var result = Post is not null ? await Post(role, path, body, ct) : DefaultPost(body);
        return result is null ? default : (T)result;
    }
    public object DefaultPost(object body)
    {
        if (body is AgreedRateTableActionRequest action)
        {
            Table.Revision = action.ExpectedRevision + 1; Table.StatusCode = action.ActionCode switch { "Accept" => "Accepted", "Reject" => "Rejected", _ => "Withdrawn" };
            return Table;
        }
        if (body is AgreedRateTableProposalRequest proposal)
        {
            Table = new() { AgreementId = "rate-new", BusinessKind = proposal.BusinessKind, VehicleCode = proposal.VehicleCode, Revision = 1,
                StatusCode = "Proposed", Proposer = new(proposal.ActorPartyKind, "restaurant-1"), CurrentParty = new(proposal.ActorPartyKind, "restaurant-1"),
                Counterparty = new(proposal.CounterpartyPartyKind, proposal.CounterpartyPartyId), FoodPricing = proposal.FoodPricing, CargoPricing = proposal.CargoPricing,
                EffectiveFromUtc = proposal.EffectiveFromUtc, EffectiveUntilUtc = proposal.EffectiveUntilUtc };
            return Table;
        }
        return body switch
        {
            AgreedRateTablePreviewRequest => new AgreedRateTablePreviewDto(RateUiData.Snapshot(Table), 1400, null, null, null),
            AgreedRateTableSelectionRequest request => new AgreedRateTableSelectionDto(Table.BusinessKind, request.WorkId, RateUiData.Snapshot(Table)) { SelectionRequestId = request.RequestId },
            _ => (object)Table
        };
    }
    public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
    public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
}
internal static class RateUiData
{
    public static AgreedRateTableDto Table(string id = "rate-1") => new()
    {
        AgreementId = id, BusinessKind = "FoodDelivery", VehicleCode = "Motorcycle", Revision = 4,
        Proposer = new("Restaurant", "restaurant-1"), Counterparty = new("FoodDriver", "driver-1"), CurrentParty = new("Restaurant", "restaurant-1"),
        StatusCode = "Accepted", EffectiveStatusCode = "Active", CanPreview = true, CanSelect = true,
        EffectiveFromUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), EffectiveUntilUtc = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
        AcceptedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), FoodPricing = new("prices-1", new(2026, 10, 1), new(2026, 10, 31), 700, 700, 100, 80, "Floor")
    };
    public static AgreedRateTableSnapshotDto Snapshot(AgreedRateTableDto table) => new(table.AgreementId, table.Revision, table.BusinessKind,
        table.VehicleCode, table.Proposer, table.Counterparty, table.FoodPricing, table.CargoPricing, table.EffectiveFromUtc,
        table.EffectiveUntilUtc, table.AcceptedAtUtc!.Value, "terms-example");
    public static void Fill(AgreedRateProposalDraft draft)
    {
        draft.CounterpartyId = "driver-1"; draft.VehicleCode = "Motorcycle"; draft.PricingRevision = "prices-1";
        draft.EffectiveFromLocal = new(2026, 10, 9, 10, 0, 0); draft.EffectiveUntilLocal = new(2026, 10, 31, 10, 0, 0);
        draft.PickupFeeKrw = draft.DropoffFeeKrw = 700; draft.DistanceUnitMeters = 100; draft.DistanceUnitFeeKrw = 80;
        draft.DistanceRoundingCode = "Floor"; draft.IncludedDistanceMeters = 0; draft.MinimumFareKrw = 0;
    }
}
