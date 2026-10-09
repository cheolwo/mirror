using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class AutoFulfillmentViewModelTests
{
    [Fact]
    public void 내부도_정보제공동의가_필수이며_음식은_생활직불동의를_요구하지않는다()
    {
        var food = Conditions(food: true, provider: "internal");
        food.ProviderInformationSharingAgreed = false; Assert.False(food.IsComplete);
        food.ProviderInformationSharingAgreed = true; Assert.True(food.IsComplete); Assert.False(food.DirectPaymentAgreed);
        var living = Conditions(provider: "internal"); Assert.False(living.IsComplete);
        living.DirectPaymentAgreed = true; Assert.True(living.IsComplete);
    }

    [Fact]
    public void 범위_비용_기한_지원판본변경은_자동접수확인을_무효화한다()
    {
        var model = Conditions(); Assert.True(model.IsComplete);
        model.MaximumUserChargeKrw = 5000; Assert.False(model.AutomaticSubmissionAgreed);
        model.AutomaticSubmissionAgreed = true; model.SetAllowed("mock-local", true);
        Assert.False(model.ProviderInformationSharingAgreed); Assert.False(model.IsComplete);
        model.ProviderInformationSharingAgreed = true; model.AutomaticSubmissionAgreed = true;
        model.ConfigureDefault(2000, 3); Assert.False(model.IsComplete);
        model.ProviderInformationSharingAgreed = true; model.AutomaticSubmissionAgreed = true;
        model.DesiredCompletionLocal = DateTime.Now.AddMinutes(-1); model.AutomaticSubmissionAgreed = true; Assert.False(model.IsComplete);
        model.SetAllowed("unknown-provider", true); Assert.DoesNotContain("unknown-provider", model.AllowedProviderIds);
    }

    [Theory]
    [InlineData(AutoFulfillmentStates.ResultUnknown)]
    [InlineData(AutoFulfillmentStates.Submitting)]
    [InlineData(AutoFulfillmentStates.CancelPending)]
    public async Task 미확정상태는_서버플래그가있어도_새동의를_전송하지않는다(string state)
    {
        var calls = 0; var vm = Query((_, _, _) => { calls++; return Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()); });
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", Snapshot(state)); Complete(vm.Conditions);
        await vm.RenewAsync(); Assert.Equal(0, calls); Assert.False(vm.CanRenew);
    }

    [Fact]
    public async Task 취소응답유실과404는_원취소명령으로_복구하고_재선정을막는다()
    {
        var commands = new List<AutoFulfillmentActionRequest>(); var refreshCalls = 0;
        var vm = new AutoFulfillmentQueryViewModel((_, _, _) => throw new InvalidOperationException(),
            (_, _, _) => { refreshCalls++; return Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()); },
            (_, request, _) => { commands.Add(request); return commands.Count == 1
                ? Task.FromException<AutoFulfillmentSnapshot?>(new SsalddelApiException("absent", 404, "test", "", null))
                : Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot(AutoFulfillmentStates.Cancelled, 3)); });
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", Snapshot(AutoFulfillmentStates.Accepted));
        await vm.CancelAsync(); Assert.True(vm.HasPending); Assert.False(vm.CanRenew);
        await vm.RefreshAsync(); Assert.False(vm.HasPending); Assert.Equal(0, refreshCalls);
        Assert.Equal(commands[0].ClientRequestId, commands[1].ClientRequestId); Assert.Equal(2, commands[1].ExpectedRevision);
    }

    [Fact]
    public async Task 취소뒤_조건갱신과철회는_동의판본을_진행재확인은_수행판본을_사용한다()
    {
        var permissions = new List<AutoFulfillmentConsent>(); var actions = new List<AutoFulfillmentActionRequest>();
        var current = Snapshot(AutoFulfillmentStates.Cancelled, 9); current.ConsentRevision = 3;
        var vm = new AutoFulfillmentQueryViewModel((_, request, _) =>
            {
                permissions.Add(request);
                Assert.Equal(current.ConsentRevision, request.ExpectedRevision);
                current.Revision++; current.ConsentRevision++;
                return Task.FromResult<AutoFulfillmentSnapshot?>(current);
            }, (_, request, _) =>
            {
                actions.Add(request); Assert.Equal(current.Revision, request.ExpectedRevision);
                return Task.FromResult<AutoFulfillmentSnapshot?>(current);
            }, (_, _, _) => throw new InvalidOperationException());
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", current); Complete(vm.Conditions);
        await vm.RenewAsync(); Assert.False(vm.HasPending); Assert.Equal(3, permissions[0].ExpectedRevision);
        await vm.RevokeAsync(); Assert.False(vm.HasPending); Assert.True(permissions[1].Revoked); Assert.Equal(4, permissions[1].ExpectedRevision);
        await vm.RefreshAsync(); Assert.Equal(11, Assert.Single(actions).ExpectedRevision);
    }

    [Fact]
    public async Task 종료된_원주문은_새동의와철회를_전송하지않는다()
    {
        var calls = 0; var vm = Query((_, _, _) => { calls++; return Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()); });
        var closed = Snapshot(AutoFulfillmentStates.Cancelled); closed.SourceClosed = true;
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", closed); Complete(vm.Conditions);
        await vm.RenewAsync(); await vm.RevokeAsync(); Assert.False(vm.CanRenew); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task 기능중지는_기존자동요청을_조회만하고_새명령을_전송하지않는다()
    {
        var calls = 0;
        Task<AutoFulfillmentSnapshot?> Blocked() { calls++; return Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()); }
        var vm = new AutoFulfillmentQueryViewModel((_, _, _) => Blocked(), (_, _, _) => Blocked(), (_, _, _) => Blocked());
        var paused = Snapshot(); paused.ExecutionPaused = true;
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", paused); Complete(vm.Conditions);
        await vm.RenewAsync(); await vm.RevokeAsync(); await vm.CancelAsync(); await vm.RefreshAsync();
        Assert.False(vm.CanRenew); Assert.Equal(0, calls); Assert.True(vm.Value!.ExecutionPaused);
    }

    [Fact]
    public async Task 소유자전환은_늦은응답과_이전동의를_다른주문으로_넘기지않는다()
    {
        var completion = new TaskCompletionSource<AutoFulfillmentSnapshot?>();
        var vm = new AutoFulfillmentQueryViewModel((_, _, _) => completion.Task, (_, _, _) => completion.Task, (_, _, _) => completion.Task);
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", Snapshot()); Complete(vm.Conditions);
        var pending = vm.RenewAsync(); vm.Bind(FulfillmentSourceKinds.FoodOrder, "other", "new-owner", Snapshot(id: "other"));
        completion.SetResult(Snapshot(AutoFulfillmentStates.Accepted, 3)); await pending;
        Assert.Equal("other", vm.Value!.SourceId); Assert.False(vm.HasPending); Assert.False(vm.Conditions.AutomaticSubmissionAgreed);
    }

    [Fact]
    public async Task 다른원요청_낮은판본응답은_성공으로_인증하지않는다()
    {
        var vm = Query((_, _, _) => Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot(revision: 1, id: "other")));
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", Snapshot()); Complete(vm.Conditions);
        await vm.RenewAsync(); Assert.True(vm.HasPending); Assert.Equal(2, vm.Value!.Revision); Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task 생활자동조건은_견적입력변경후_다시확인하며_수동후보와_혼합하지않는다()
    {
        var client = new LivingClient(); using var vm = await Ready(client); vm.Draft.DispatchMode = NeighborhoodDispatchModes.PublicCall;
        await vm.QuoteAsync(); Complete(vm.AutoConditions); Assert.False(vm.HasCurrentQuote);
        await vm.QuoteAsync(); vm.DispatchRequested = true;
        Assert.Equal("source", await vm.SubmitAsync()); var request = Assert.Single(client.Requests);
        Assert.Null(request.FulfillmentSelection); Assert.Equal(NeighborhoodDispatchModes.PublicCall, request.DispatchMode);
        Assert.NotNull(request.AutoFulfillment); Assert.True(request.AutoFulfillment.AutomaticSubmissionAgreed);
        Assert.False(request.DirectPaymentAgreed);
    }

    [Fact]
    public async Task 생활응답유실은_조건을바꿔도_원자동요청_그대로_재확인한다()
    {
        var client = new LivingClient { Lose = true }; using var vm = await Ready(client);
        await vm.QuoteAsync(); Complete(vm.AutoConditions); await vm.QuoteAsync(); vm.DispatchRequested = true;
        Assert.Null(await vm.SubmitAsync()); Assert.True(vm.NeedsSubmissionReview);
        vm.AutoConditions.MaximumUserChargeKrw = 9999; client.Lose = false; Assert.Equal("source", await vm.SubmitAsync());
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(client.Requests[0].ClientRequestId, client.Requests[1].ClientRequestId);
        Assert.Equal(client.Requests[0].AutoFulfillment!.ClientRequestId, client.Requests[1].AutoFulfillment!.ClientRequestId);
        Assert.Equal(4000, client.Requests[1].AutoFulfillment!.MaxUserChargeKrw);
        Assert.False(client.Requests[1].DirectPaymentAgreed); Assert.False(client.Requests[1].AutoFulfillment!.DirectPaymentAgreed);
        Assert.Equal(JsonSerializer.Serialize(client.Requests[0]), JsonSerializer.Serialize(client.Requests[1]));
    }

    [Fact]
    public async Task 음식지원은_주문전체한번으로_등록하며_수령정보변경은_동의를_다시받는다()
    {
        var service = new FoodService(); var vm = FoodWriter(service); await vm.자동배송조건확인Async();
        Complete(vm.AutoConditions); Assert.True(vm.제출가능); Assert.True(await vm.등록Async());
        var consent = Assert.Single(service.Requests).AutoFulfillment!;
        Assert.Equal(2000, consent.ConfirmedRestaurantSupportKrw); Assert.Equal(7, consent.ConfirmedRestaurantSupportRevision);
        Assert.False(consent.DirectPaymentAgreed); Assert.Equal(8000, vm.주문금액);
        vm.주소 = "새 검토 장소"; Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed); Assert.False(vm.제출가능);
    }

    [Fact]
    public async Task 음식지원조회실패는_자동재시도하거나_미확인제출을하지않는다()
    {
        var service = new FoodService { QuoteFails = true }; var vm = FoodWriter(service);
        await vm.자동배송조건확인Async(); await vm.자동배송조건확인Async();
        Assert.Equal(1, service.QuoteCalls); Assert.False(vm.제출가능); Assert.False(await vm.등록Async()); Assert.Empty(service.Requests);
        service.QuoteFails = false; await vm.자동배송조건확인Async(force: true); Assert.True(vm.자동배송사용); Assert.Equal(2, service.QuoteCalls);
    }

    [Fact]
    public async Task 식당지원_변경후재조회는_이전동의를_해제하고_새지원판본으로_등록한다()
    {
        var service = new FoodService(); var vm = FoodWriter(service); await vm.자동배송조건확인Async(); Complete(vm.AutoConditions);
        service.Support = 3000; service.SupportRevision = 8; await vm.자동배송조건확인Async(force: true);
        Assert.False(vm.제출가능); Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed);
        Assert.Equal(3000, vm.AutoConditions.MerchantContributionKrw); Assert.Equal(8, vm.AutoConditions.MerchantSettingsRevision);
        Complete(vm.AutoConditions); Assert.True(await vm.등록Async());
        var consent = Assert.Single(service.Requests).AutoFulfillment!;
        Assert.Equal(3000, consent.ConfirmedRestaurantSupportKrw); Assert.Equal(8, consent.ConfirmedRestaurantSupportRevision);
    }

    [Fact]
    public async Task 지원설정_응답유실은_원요청번호와_판본을_재사용한다()
    {
        var requests = new List<RestaurantFulfillmentSupportRequest>();
        var vm = new FoodMerchantFulfillmentSettingsViewModel(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1, Revision = 4 }),
            (request, _) => { requests.Add(request); return requests.Count == 1 ? Task.FromException<RestaurantFulfillmentSupportResponse?>(new HttpRequestException())
                : Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1, Revision = 5, FixedContributionKrw = 2000, Enabled = true }); });
        await vm.LoadAsync(); vm.FixedContributionKrw = 2000; vm.Enabled = true; vm.Agreed = true;
        await vm.SaveAsync(); Assert.True(vm.HasPending); vm.FixedContributionKrw = 3000; await vm.SaveAsync();
        Assert.Equal(requests[0].ClientRequestId, requests[1].ClientRequestId); Assert.Equal(2000, requests[1].FixedContributionKrw);
        Assert.False(vm.HasPending); Assert.Equal(2000, vm.Value!.FixedContributionKrw);
    }

    [Theory]
    [InlineData("CustomerPays")]
    [InlineData("FixedContribution")]
    [InlineData("PercentageShare")]
    [InlineData("RestaurantPays")]
    public async Task 네정책은_자동사용설정과_분리되어_선택한필드만_저장한다(string mode)
    {
        RestaurantFulfillmentSupportRequest? saved = null;
        var vm = new FoodMerchantFulfillmentSettingsViewModel(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1 }),
            (request, _) => { saved = request; return Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1, Revision = 1,
                Enabled = request.Enabled, FixedContributionKrw = request.FixedContributionKrw, CostSharingPolicy = request.CostSharingPolicy }); });
        await vm.LoadAsync(); Assert.False(vm.CanSave); vm.SelectPolicy(mode); vm.Enabled = true;
        if (mode == "FixedContribution") vm.FixedContributionKrw = 2000;
        if (mode == "PercentageShare") vm.RestaurantSharePercent = 30;
        if (mode is "PercentageShare" or "RestaurantPays") vm.UsesRestaurantLimit = false;
        vm.Agreed = true; Assert.True(vm.CanSave); await vm.SaveAsync(); Assert.NotNull(saved);
        Assert.True(saved.Enabled); Assert.Equal(mode, saved.CostSharingPolicy!.Mode);
        Assert.Equal(mode == "FixedContribution" ? 2000m : (decimal?)null, saved.CostSharingPolicy.FixedAmountKrw);
        Assert.Equal(mode == "PercentageShare" ? 30m : (decimal?)null, saved.CostSharingPolicy.RestaurantSharePercent);
        Assert.Null(saved.CostSharingPolicy.MaximumRestaurantChargeKrw);
    }

    [Fact]
    public async Task 새정책의_빈입력과_미선택한도는_저장을막고_변경은_동의를해제한다()
    {
        var calls = 0; var vm = new FoodMerchantFulfillmentSettingsViewModel(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1 }),
            (_, _) => { calls++; return Task.FromResult<RestaurantFulfillmentSupportResponse?>(null); });
        await vm.LoadAsync(); vm.SelectPolicy("FixedContribution"); vm.Agreed = true; await vm.SaveAsync(); Assert.Equal(0, calls); Assert.Null(vm.FixedContributionKrw);
        vm.SelectPolicy("PercentageShare"); vm.RestaurantSharePercent = 30; vm.Agreed = true; Assert.False(vm.CanSave);
        vm.UsesRestaurantLimit = true; vm.Agreed = true; await vm.SaveAsync(); Assert.Equal(0, calls); Assert.Null(vm.MaximumRestaurantChargeKrw);
        vm.MaximumRestaurantChargeKrw = 2000; Assert.False(vm.Agreed); vm.Agreed = true; Assert.True(vm.CanSave);
        vm.RestaurantSharePercent = 40; Assert.False(vm.CanSave); Assert.False(vm.Agreed);
    }

    [Fact]
    public async Task 정책저장_응답유실은_새초안이나_가변객체와무관하게_원JSON을_재전송한다()
    {
        var requests = new List<RestaurantFulfillmentSupportRequest>();
        var vm = new FoodMerchantFulfillmentSettingsViewModel(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1 }),
            (request, _) => { requests.Add(request); return requests.Count == 1 ? Task.FromException<RestaurantFulfillmentSupportResponse?>(new HttpRequestException())
                : Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 1, Revision = 1, CostSharingPolicy = request.CostSharingPolicy, Enabled = request.Enabled }); });
        await vm.LoadAsync(); vm.SelectPolicy("PercentageShare"); vm.RestaurantSharePercent = 30; vm.UsesRestaurantLimit = false; vm.Enabled = true; vm.Agreed = true;
        await vm.SaveAsync(); Assert.True(vm.HasPending); var first = JsonSerializer.Serialize(requests[0]);
        vm.RestaurantSharePercent = 75; vm.DraftPolicy!.RestaurantSharePercent = 90;
        await vm.SaveAsync(); Assert.Equal(first, JsonSerializer.Serialize(requests[1])); Assert.False(vm.HasPending);
        Assert.Equal(30, vm.Value!.CostSharingPolicy!.RestaurantSharePercent);
        requests[1].CostSharingPolicy!.RestaurantSharePercent = 99; Assert.Equal(30, vm.Value.CostSharingPolicy.RestaurantSharePercent);
    }

    [Fact]
    public async Task 음식의_정책변경은_동의를새로받고_등록은_정책사본과판본을_보존한다()
    {
        var service = new FoodService { Policy = new() { Mode = "PercentageShare", RestaurantSharePercent = 30 } };
        var vm = FoodWriter(service); await vm.자동배송조건확인Async(); Complete(vm.AutoConditions);
        service.Policy.RestaurantSharePercent = 50; service.SupportRevision = 8;
        await vm.자동배송조건확인Async(force: true); Assert.False(vm.제출가능); Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed);
        Complete(vm.AutoConditions); Assert.True(await vm.등록Async()); var consent = Assert.Single(service.Requests).AutoFulfillment!;
        Assert.Equal(8, consent.ConfirmedRestaurantSupportRevision); Assert.Equal(50, consent.ConfirmedCostSharingPolicy!.RestaurantSharePercent);
        service.Policy.RestaurantSharePercent = 75; Assert.Equal(50, consent.ConfirmedCostSharingPolicy.RestaurantSharePercent);
        vm.AutoConditions.CostSharingPolicy!.RestaurantSharePercent = 90; Assert.Equal(50, consent.ConfirmedCostSharingPolicy.RestaurantSharePercent);
    }

    [Fact]
    public async Task 음식조건_복구는_식당현재설정이아닌_원주문정책과_동의판본을_사용한다()
    {
        AutoFulfillmentConsent? sent = null; var value = Snapshot(AutoFulfillmentStates.Cancelled, 9);
        value.ConsentRevision = 3; value.CostSharingPolicy = new() { Mode = "RestaurantPays", MaximumRestaurantChargeKrw = 5000 };
        var vm = Query((_, request, _) => { sent = request; return Task.FromResult<AutoFulfillmentSnapshot?>(value); });
        vm.Bind(FulfillmentSourceKinds.FoodOrder, "source", "owner", value); Complete(vm.Conditions);
        value.CostSharingPolicy.MaximumRestaurantChargeKrw = 9000; await vm.RenewAsync();
        Assert.NotNull(sent); Assert.Equal(3, sent.ExpectedRevision); Assert.Equal(7, sent.ConfirmedRestaurantSupportRevision);
        Assert.Equal(5000, sent.ConfirmedCostSharingPolicy!.MaximumRestaurantChargeKrw);
    }

    [Theory]
    [InlineData("waiting", "견적 대기", "견적 대기")]
    [InlineData("held", "견적 미확정", "확인 필요")]
    [InlineData("unknown", "확인 필요", "확인 필요")]
    [InlineData("paused", "확인 필요", "확인 필요")]
    public async Task 음식점업무카드는_미확정견적과_보류분담을_0원으로_표시하지않는다(string state, string total, string shares)
    {
        var value = Snapshot(state == "waiting" ? AutoFulfillmentStates.AwaitingRouting : AutoFulfillmentStates.NeedsReview);
        value.CostSharingPolicy = new() { Mode = "RestaurantPays", MaximumRestaurantChargeKrw = 5000 };
        value.TotalCostKrw = state is "waiting" or "held" ? 0 : null;
        value.UserChargeKrw = state == "waiting" ? 0 : null; value.RestaurantContributionKrw = 0;
        value.CostSharingAllowed = state == "held" ? false : null; value.ExecutionPaused = state == "paused";
        var order = new 음식주문응답 { 주문번호 = "policy-order", AutoFulfillment = value };
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString("미처리") + "&Page=1&PageSize=50"] = new 음식점주문수신함응답 { Items = [order] };
        api.Reads["api/v1/food-orders/restaurant/inbox/policy-order"] = order;
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(api).LoadAsync(null, default)).Items);
        var fields = Assert.Single(item.Sections!, section => section.Title == "자동 배송 처리").Fields;
        Assert.Equal(total, Assert.Single(fields, field => field.Label == "주문 전체 배송 비용").Value);
        Assert.Equal(shares, Assert.Single(fields, field => field.Label == "음식점 부담").Value);
        Assert.Equal(shares, Assert.Single(fields, field => field.Label == "주문자 부담").Value);
        Assert.DoesNotContain(fields, field => field.Value == "0원"); Assert.Empty(api.Writes);
        if (state == "held") Assert.Contains("초과분을 주문자에게 자동으로 넘기지", Assert.Single(fields, field => field.Label == "부담 조건 확인").Value);
        if (state != "paused") { Assert.Equal("7", Assert.Single(fields, field => field.Label == "조건 판본").Value); Assert.Contains("음식점 전액 부담", Assert.Single(fields, field => field.Label == "배송비 조건").Value); }
    }

    [Fact]
    public void 확정된_구형정액비용과_생활비용은_기존금액을_보존한다()
    {
        var food = Snapshot(AutoFulfillmentStates.Accepted); food.AttemptId = "confirmed"; food.TotalCostKrw = 6000;
        food.RestaurantContributionKrw = 2000; food.UserChargeKrw = 4000;
        Assert.Equal("6,000원", DeliveryCostSharingPresentation.Total(food)); Assert.Equal("2,000원", DeliveryCostSharingPresentation.Merchant(food));
        Assert.Equal("4,000원", DeliveryCostSharingPresentation.Customer(food)); Assert.Equal("음식점 정액 2,000원 지원", DeliveryCostSharingPresentation.Summary(null, food.FrozenRestaurantSupportKrw));
        var living = new AutoFulfillmentSnapshot { SourceKind = FulfillmentSourceKinds.NeighborhoodDelivery, StateCode = AutoFulfillmentStates.AwaitingRouting, TotalCostKrw = 0, UserChargeKrw = 0 };
        Assert.Equal("0원", DeliveryCostSharingPresentation.Total(living)); Assert.Equal("0원", DeliveryCostSharingPresentation.Customer(living));
    }

    private static AutoFulfillmentConditionsViewModel Conditions(bool food = false, string provider = "mock-quick")
    { var model = new AutoFulfillmentConditionsViewModel(); model.ConfigureDefault(2000, food ? 7 : null, !food); Complete(model, provider); return model; }
    private static void Complete(AutoFulfillmentConditionsViewModel model, string provider = "mock-quick")
    { model.MaximumUserChargeKrw = 4000; model.DesiredCompletionLocal = DateTime.Now.AddHours(4); model.SetAllowed(provider, true); model.ProviderInformationSharingAgreed = true; model.AutomaticSubmissionAgreed = true; }
    private static AutoFulfillmentSnapshot Snapshot(string state = AutoFulfillmentStates.NeedsReview, long revision = 2, string id = "source") => new()
    { SourceKind = FulfillmentSourceKinds.FoodOrder, SourceId = id, StateCode = state, Revision = revision, ConsentRevision = 1, CanCancel = true, CanRenewConsent = true, FrozenRestaurantSupportKrw = 2000, RestaurantSupportRevision = 7 };
    private static AutoFulfillmentQueryViewModel Query(Func<string, AutoFulfillmentConsent, CancellationToken, Task<AutoFulfillmentSnapshot?>> consent)
        => new(consent, (_, _, _) => Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()), (_, _, _) => Task.FromResult<AutoFulfillmentSnapshot?>(Snapshot()));
    private static 음식주문작성ViewModel FoodWriter(FoodService service)
    {
        var vm = new 음식주문작성ViewModel(service); vm.음식점설정(new() { 음식점 = new 음식점공개요약응답 { Id = 1, 주문가능여부 = true },
            메뉴목록 = [new 음식점메뉴공개응답 { Id = 2, 메뉴명 = "예시 메뉴", 판매가 = 4000 }] });
        vm.메뉴수량변경(2, 2); vm.수령인명 = "검토자"; vm.연락처 = "01000000000"; vm.주소 = "검토 장소"; return vm;
    }
    private static async Task<NeighborhoodDeliveryAuthoringViewModel> Ready(LivingClient client)
    {
        var vm = new NeighborhoodDeliveryAuthoringViewModel(client, new User()); vm.Initialize(null);
        vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync(); vm.Draft.CargoName = "검토 물품"; vm.Draft.WeightKg = 1;
        foreach (var place in new[] { vm.Draft.Pickup, vm.Draft.Dropoff }) { place.주소.도로명주소 = "검토 장소"; place.연락처.이름 = "검토자"; place.연락처.전화번호 = "01000000000"; } return vm;
    }
    private sealed class User : ISsalddel현재사용자Context { public 현재사용자Snapshot 현재사용자 => new("owner", "검토자", []); }
    private sealed class LivingClient : INeighborhoodDeliveryClient
    {
        public bool Lose; public List<NeighborhoodDeliveryRequest> Requests { get; } = [];
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000 }, FulfillmentRoutingEnabled = true, AutomaticProviders = AutoFulfillmentNotice.Providers });
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) { Requests.Add(JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!); return Lose ? Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "source" }); }
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
    }
    private sealed class FoodService : I주문자음식주문쓰기Service
    {
        public bool QuoteFails; public int QuoteCalls; public decimal Support = 2000; public long SupportRevision = 7; public DeliveryCostSharingPolicy? Policy; public List<음식주문등록요청> Requests { get; } = [];
        public Task<FoodAutoFulfillmentQuoteResponse> 자동배송조건Async(음식주문등록요청 request, CancellationToken ct = default) { QuoteCalls++; return QuoteFails ? Task.FromException<FoodAutoFulfillmentQuoteResponse>(new HttpRequestException()) : Task.FromResult(new FoodAutoFulfillmentQuoteResponse { Enabled = true, RestaurantId = 1, RestaurantSupportKrw = Support, RestaurantSupportRevision = SupportRevision, CostSharingPolicy = Policy, Providers = AutoFulfillmentNotice.Providers }); }
        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken ct = default) { Requests.Add(request); return Task.FromResult(new 음식주문응답 { 주문번호 = "source" }); }
    }
}
