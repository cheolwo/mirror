using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodFulfillmentViewModelTests
{
    [Fact]
    public async Task 외부처리처선택은_누락된내부기사규격과작업시간을_임의로채우거나필수화하지않는다()
    {
        var client = new Client(); using var vm = await Ready(client);
        vm.Draft.PackageLengthMm = null; vm.Draft.PackageWidthMm = null; vm.Draft.PackageHeightMm = null;
        vm.Draft.PalletCount = null; vm.Draft.FloorRotationAllowed = null; vm.Draft.StackingAllowed = null;
        vm.Draft.Pickup.작업시간분 = null; vm.Draft.Dropoff.작업시간분 = null;
        await vm.QuoteAsync(); vm.SelectFulfillment("mock-quote");
        vm.DispatchRequested = vm.FulfillmentCostAgreed = vm.ProviderInformationSharingAgreed = true;
        Assert.Empty(vm.InternalCourierMissing); Assert.Equal("request", await vm.SubmitAsync());
        var sent = Assert.Single(client.Created);
        Assert.Null(sent.PackageLengthMm); Assert.Null(sent.VolumeCbm); Assert.Null(sent.PalletCount);
        Assert.Null(sent.FloorRotationAllowed); Assert.Null(sent.Pickup.작업시간분); Assert.Null(sent.Dropoff.작업시간분);
    }

    [Fact]
    public async Task 추천은_자동선택하지않고_처리처별_비용과_동의후에만_접수한다()
    {
        var client = new Client(); using var vm = await Ready(client);
        await vm.QuoteAsync(); vm.DispatchRequested = true;
        Assert.Null(vm.SelectedFulfillmentOption); Assert.Null(await vm.SubmitAsync());
        vm.SelectFulfillment("mock-quote"); vm.FulfillmentCostAgreed = true;
        Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created);
        vm.ProviderInformationSharingAgreed = true;
        Assert.Equal("request", await vm.SubmitAsync());
        var request = Assert.Single(client.Created);
        Assert.Equal("mock-quick", request.FulfillmentSelection!.ProviderId);
        Assert.Equal(3200, request.FulfillmentSelection.ConfirmedCostKrw);
        Assert.True(request.FulfillmentSelection.ProviderInformationSharingAgreed);
        Assert.False(request.DirectPaymentAgreed);
    }

    [Fact]
    public async Task 입력변경_만료_불명확견적은_선택하거나_기존동의로_접수하지않는다()
    {
        var client = new Client(); using var vm = await Ready(client);
        await vm.QuoteAsync(); vm.SelectFulfillment("mock-quote"); vm.FulfillmentCostAgreed = true;
        vm.ProviderInformationSharingAgreed = true; vm.DispatchRequested = true;
        vm.Draft.WeightKg = 2; Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created);
        await vm.QuoteAsync(); Assert.Null(vm.SelectedFulfillmentOption); Assert.False(vm.FulfillmentCostAgreed);
        vm.DesiredCompletionLocal = DateTime.Now.AddHours(4); Assert.False(vm.HasCurrentQuote);
        await vm.QuoteAsync();
        client.Options[0].ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        vm.SelectFulfillment("mock-quote"); Assert.Null(vm.SelectedFulfillmentOption);
        client.Options[0].ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5); client.Options[0].EstimatedCompletionUtc = null;
        vm.SelectFulfillment("mock-quote"); Assert.Null(vm.SelectedFulfillmentOption);
    }

    [Fact]
    public async Task 서버의_비용변경거절은_원초안을_보존하고_새견적을_허용한다()
    {
        var client = new Client { CreateError = new SsalddelApiException("diagnostics", 409, "test", "", null, errorCode: "FulfillmentCostChanged") };
        using var vm = await Ready(client); await vm.QuoteAsync(); vm.SelectFulfillment("mock-quote");
        vm.DispatchRequested = true; vm.FulfillmentCostAgreed = true; vm.ProviderInformationSharingAgreed = true;
        var id = vm.Draft.ClientRequestId;
        Assert.Null(await vm.SubmitAsync()); Assert.False(vm.NeedsSubmissionReview); Assert.Null(vm.Quote);
        Assert.Null(vm.Draft.FulfillmentSelection); Assert.Equal(id, vm.Draft.ClientRequestId);
        client.CreateError = null; await vm.QuoteAsync(); Assert.True(vm.HasCurrentQuote);
        Assert.Null(vm.SelectedFulfillmentOption); Assert.False(vm.FulfillmentCostAgreed);
    }

    [Fact]
    public async Task 외부접수_응답유실은_희망시각과_처리처를_유지한_원요청으로_복구한다()
    {
        var client = new Client { LoseCreate = true }; using var vm = await Ready(client);
        vm.DesiredCompletionLocal = DateTime.Now.AddHours(3);
        await vm.QuoteAsync(); vm.SelectFulfillment("mock-quote"); vm.DispatchRequested = true;
        vm.FulfillmentCostAgreed = true; vm.ProviderInformationSharingAgreed = true;
        Assert.Null(await vm.SubmitAsync()); Assert.True(vm.NeedsSubmissionReview);
        vm.SelectFulfillment("internal-quote"); Assert.Equal("mock-quick", vm.SelectedFulfillmentOption!.ProviderId);
        client.LoseCreate = false; Assert.Equal("request", await vm.SubmitAsync());
        Assert.Equal(2, client.Created.Count);
        Assert.Equal(JsonSerializer.Serialize(client.Created[0]), JsonSerializer.Serialize(client.Created[1]));
    }

    [Fact]
    public async Task 기능꺼짐_빈후보는_기존직접지급과_배차방식을_보존한다()
    {
        var client = new Client { Options = [] }; using var vm = await Ready(client);
        vm.Draft.DispatchMode = NeighborhoodDispatchModes.PublicCall; await vm.QuoteAsync();
        vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        Assert.Equal("request", await vm.SubmitAsync());
        var request = Assert.Single(client.Created); Assert.Null(request.FulfillmentSelection);
        Assert.True(request.DirectPaymentAgreed); Assert.Equal(NeighborhoodDispatchModes.PublicCall, request.DispatchMode);
    }

    [Fact]
    public async Task 취소확인중은_처리처변경을막고_새로고침후에만_새견적을받는다()
    {
        var client = new Client { Detail = Detail("CancelPending") };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User());
        await vm.LoadDetailAsync("request"); await vm.LoadFulfillmentOptionsAsync();
        Assert.False(vm.CanChangeFulfillment); Assert.Equal(0, client.OptionsCalls);
        client.Action = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(Detail("Cancelled", revision: 3));
        await vm.RefreshFulfillmentAsync(); Assert.True(vm.CanChangeFulfillment);
        await vm.LoadFulfillmentOptionsAsync(); Assert.Equal(1, client.OptionsCalls);
        vm.SelectFulfillment("mock-quote"); vm.FulfillmentCostAgreed = true; vm.ProviderInformationSharingAgreed = true;
        await vm.ChooseFulfillmentAsync();
        var choice = Assert.Single(client.Choices); Assert.Equal(3, choice.ExpectedRevision);
        Assert.Equal("mock-quick", choice.Selection.ProviderId);
    }

    [Fact]
    public async Task 취소응답유실은_다른명령대신_원취소번호와_판본만_재사용한다()
    {
        var client = new Client { Detail = Detail("Accepted"), Action = _ => Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("lost")) };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User()); await vm.LoadDetailAsync("request");
        await vm.CancelFulfillmentAsync(); Assert.True(vm.HasPendingFulfillmentOperation);
        await vm.RefreshFulfillmentAsync(); Assert.Equal(2, client.Actions.Count);
        Assert.All(client.Actions, item => Assert.Equal("cancel", item.Kind));
        Assert.Equal(client.Actions[0].Request.ClientRequestId, client.Actions[1].Request.ClientRequestId);
        Assert.Equal(client.Actions[0].Request.ExpectedRevision, client.Actions[1].Request.ExpectedRevision);
        Assert.False(vm.CanChangeFulfillment);
    }

    [Fact]
    public async Task 자체배차재선택은_새직접지급동의를_서버명령에_담는다()
    {
        var client = new Client { Detail = Detail("Cancelled") };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User()); await vm.LoadDetailAsync("request");
        await vm.LoadFulfillmentOptionsAsync(); vm.SelectFulfillment("internal-quote"); vm.FulfillmentCostAgreed = true;
        await vm.ChooseFulfillmentAsync(); Assert.Empty(client.Choices);
        vm.FulfillmentDirectPaymentAgreed = true; await vm.ChooseFulfillmentAsync();
        Assert.True(Assert.Single(client.Choices).Selection.DirectPaymentAgreed);
    }

    [Fact]
    public async Task 계정교체는_처리처견적과_미확정명령을_지운다()
    {
        var client = new Client { Detail = Detail("Cancelled") }; var user = new User();
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, user); await vm.LoadDetailAsync("request");
        await vm.LoadFulfillmentOptionsAsync(); vm.SelectFulfillment("mock-quote");
        user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        Assert.Null(vm.Detail); Assert.Null(vm.FulfillmentQuote); Assert.Null(vm.SelectedFulfillmentOption);
        await vm.RefreshFulfillmentAsync(); await vm.ChooseFulfillmentAsync(); Assert.Empty(client.Actions); Assert.Empty(client.Choices);
    }

    [Fact]
    public async Task 진행명령중_중복클릭과_늦은이전계정응답은_최신상태를_바꾸지않는다()
    {
        var pending = new TaskCompletionSource<NeighborhoodDeliveryResponse?>();
        var client = new Client { Detail = Detail("Accepted"), Action = _ => pending.Task }; var user = new User();
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, user); await vm.LoadDetailAsync("request");
        var first = vm.RefreshFulfillmentAsync(); await vm.RefreshFulfillmentAsync(); Assert.Single(client.Actions);
        user.Current = new("next", "다음 검토자", []); vm.SynchronizeOwner();
        client.Detail = Detail("Cancelled", 4); await vm.LoadDetailAsync("request");
        pending.SetResult(Detail("Completed", 3)); await first;
        Assert.Equal("Cancelled", vm.Detail!.Fulfillment!.StateCode); Assert.Equal(4, vm.Detail.Fulfillment.Revision);
    }

    [Fact]
    public void 미확정과_모의완료는_실제배차나_지급완료로_표현하지않는다()
    {
        Assert.Equal("접수 결과 확인 중", NeighborhoodDeliveryPresentation.Stage(Detail("ResultUnknown")));
        Assert.Contains("따로", NeighborhoodDeliveryPresentation.다음행동안내(Detail("Completed")));
        Assert.Equal("금액 확인 필요", NeighborhoodDeliveryPresentation.Money(null));
    }

    [Fact]
    public void 처리처취소확인과_의뢰전체취소는_다른_다음행동을_안내한다()
    {
        var cancelledProvider = Detail("Cancelled").Fulfillment!;
        Assert.Contains("새 견적", NeighborhoodDeliveryPresentation.FulfillmentNext(cancelledProvider));
        var closedRequest = Detail("Cancelled").Fulfillment!; closedRequest.CanChangeProvider = false;
        Assert.Contains("종료된 배송 기록", NeighborhoodDeliveryPresentation.FulfillmentNext(closedRequest));
        Assert.DoesNotContain("새 견적", NeighborhoodDeliveryPresentation.FulfillmentNext(closedRequest));
    }

    private static async Task<NeighborhoodDeliveryAuthoringViewModel> Ready(Client client)
    {
        var vm = new NeighborhoodDeliveryAuthoringViewModel(client, new User()); vm.Initialize(null);
        vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync();
        vm.Draft.CargoName = "시험 물품"; vm.Draft.WeightKg = 1;
        // 내부 기사 경로를 검증하는 시험의 명시 합성 포장·작업시간입니다.
        vm.Draft.PackageLengthMm = 300; vm.Draft.PackageWidthMm = 200; vm.Draft.PackageHeightMm = 200;
        vm.Draft.PalletCount = 0; vm.Draft.FloorRotationAllowed = true; vm.Draft.StackingAllowed = true;
        vm.Draft.Pickup.작업시간분 = 2m; vm.Draft.Dropoff.작업시간분 = 1m;
        foreach (var place in new[] { vm.Draft.Pickup, vm.Draft.Dropoff })
        { place.주소.도로명주소 = "검토용 장소"; place.연락처.이름 = "검토 이용자"; place.연락처.전화번호 = "01000000000"; }
        return vm;
    }
    private static NeighborhoodDeliveryResponse Detail(string state, long revision = 2) => new()
    {
        RequestId = "request", Fulfillment = new()
        {
            RequestId = "request", ProviderId = "mock-local", ProviderName = "모의 생활배송", StateCode = state, Revision = revision,
            CanCancel = state is "Accepted" or "InTransit", CanChangeProvider = state is "Cancelled" or "Failed" or "AwaitingSelection", IsSimulation = true
        }
    };
    private sealed class User : ISsalddel현재사용자Context
    { public 현재사용자Snapshot Current { get; set; } = new("owner", "검토 이용자", []); public 현재사용자Snapshot 현재사용자 => Current; }
    private sealed class Client : INeighborhoodDeliveryClient
    {
        public NeighborhoodFulfillmentOption[] Options { get; set; } = [Option("mock-quick", "mock-quote", 3200, recommended: true), Option("internal", "internal-quote", 4000)];
        public NeighborhoodDeliveryResponse Detail { get; set; } = NeighborhoodFulfillmentViewModelTests.Detail("Accepted");
        public Func<NeighborhoodFulfillmentActionRequest, Task<NeighborhoodDeliveryResponse?>> Action { get; set; }
            = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(NeighborhoodFulfillmentViewModelTests.Detail("Accepted", 3));
        public bool LoseCreate; public int OptionsCalls; public Exception? CreateError;
        public List<NeighborhoodDeliveryRequest> Created { get; } = [];
        public List<NeighborhoodFulfillmentChoiceRequest> Choices { get; } = [];
        public List<(string Kind, NeighborhoodFulfillmentActionRequest Request)> Actions { get; } = [];
        private static NeighborhoodFulfillmentOption Option(string id, string quote, decimal cost, bool recommended = false) => new()
        { ProviderId = id, ProviderName = id, QuoteId = quote, TotalCostKrw = cost, EstimatedCompletionUtc = DateTime.UtcNow.AddHours(1), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), IsAvailable = true, IsRecommended = recommended, IsSimulation = true };
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
            => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
            => Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000 }, FulfillmentOptions = Options });
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        { Created.Add(JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!); return CreateError is not null ? Task.FromException<NeighborhoodDeliveryResponse?>(CreateError) : LoseCreate ? Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("lost")) : Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "request" }); }
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(Detail);
        public Task<NeighborhoodDeliveryQuoteResponse?> FulfillmentOptionsAsync(string id, CancellationToken ct) { OptionsCalls++; return QuoteAsync(new(), ct); }
        public Task<NeighborhoodDeliveryResponse?> ChooseFulfillmentAsync(string id, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct)
        { Choices.Add(request); return Task.FromResult<NeighborhoodDeliveryResponse?>(NeighborhoodFulfillmentViewModelTests.Detail("Accepted", request.ExpectedRevision + 1)); }
        public Task<NeighborhoodDeliveryResponse?> RefreshFulfillmentAsync(string id, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        { Actions.Add(("refresh", request)); return Action(request); }
        public Task<NeighborhoodDeliveryResponse?> CancelFulfillmentAsync(string id, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        { Actions.Add(("cancel", request)); return Action(request); }
    }
}
