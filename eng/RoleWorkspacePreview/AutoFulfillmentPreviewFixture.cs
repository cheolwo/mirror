using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

public static class AutoFulfillmentPreviewRegistration
{
    public static IServiceCollection AddAutoFulfillmentPreview(this IServiceCollection services)
    {
        services.AddScoped<AutoFulfillmentPreviewFixture>();
        services.AddScoped<ISsalddelJsonApiClient, AutoPreviewForbiddenApi>();
        var authoring = services.Last(d => d.ServiceType == typeof(NeighborhoodDeliveryAuthoringViewModel));
        var query = services.Last(d => d.ServiceType == typeof(NeighborhoodDeliveryQueryViewModel));
        services.AddTransient(sp => AutoFulfillmentPreviewFixture.IsPreview(sp.GetRequiredService<NavigationManager>())
            ? sp.GetRequiredService<AutoFulfillmentPreviewFixture>().Authoring : (NeighborhoodDeliveryAuthoringViewModel)Resolve(authoring, sp));
        services.AddTransient(sp => AutoFulfillmentPreviewFixture.IsPreview(sp.GetRequiredService<NavigationManager>())
            ? sp.GetRequiredService<AutoFulfillmentPreviewFixture>().Query : (NeighborhoodDeliveryQueryViewModel)Resolve(query, sp));
        return services;
    }
    private static object Resolve(ServiceDescriptor d, IServiceProvider sp) => d.ImplementationFactory is { } factory ? factory(sp)
        : d.ImplementationInstance ?? ActivatorUtilities.CreateInstance(sp, d.ImplementationType!);
}

// 실제 공통 화면을 실행하되 모든 접수·취소는 메모리의 합성 자료에만 적용합니다.
internal sealed class AutoFulfillmentPreviewFixture(NavigationManager navigation, I통신판매보호Client commerce) :
    INeighborhoodDeliveryClient, ISsalddel현재사용자Context, I주문자음식주문쓰기Service, I주문자음식주문읽기Service,
    I주문자음식주문수령확인Service, I주문자앱인증Service
{
    internal static string[] Scenarios = ["living-create", "living-lost", "living-feature-off", "living-accepted", "living-unknown", "living-review", "living-paused", "food-create", "food-feature-off", "food-unknown", "food-review", "food-paused", "merchant-support"];
    private string Scenario => new Uri(navigation.Uri).AbsolutePath.Split('/').Last();
    internal static bool IsPreview(NavigationManager n) => new Uri(n.Uri).AbsolutePath.StartsWith("/auto-fulfillment-preview/", StringComparison.Ordinal);
    public 현재사용자Snapshot 현재사용자 => new("preview-owner", "화면 검토자", []);
    public bool IsLivingCreate => Scenario is "living-create" or "living-lost" or "living-feature-off";
    public bool IsFoodCreate => Scenario is "food-create" or "food-feature-off";
    private NeighborhoodDeliveryAuthoringViewModel? _authoring;
    private NeighborhoodDeliveryQueryViewModel? _query;
    private 음식주문작성ViewModel? _writer;
    private 주문자음식주문상세ViewModel? _foodDetail;
    private 주문자앱인증ViewModel? _authentication;
    public NeighborhoodDeliveryAuthoringViewModel Authoring => _authoring ??= new(this, this);
    public NeighborhoodDeliveryQueryViewModel Query => _query ??= new(this, this);
    public 음식주문작성ViewModel Writer => _writer ??= new(this);
    public 주문자음식주문상세ViewModel FoodDetail => _foodDetail ??= new(this, this);
    public 주문자앱인증ViewModel Authentication => _authentication ??= new(this);
    private AutoFulfillmentSnapshot? _snapshot;
    private Guid? _lostActionId;
    private bool _createLost;
    private string? _prepared;
    public int CreateCalls { get; private set; }
    public int ConsentCalls { get; private set; }
    public int CancelCalls { get; private set; }
    public int RefreshCalls { get; private set; }
    public Guid? LastCreateId { get; private set; }
    public Guid? LastActionId { get; private set; }
    public AutoFulfillmentConsent? LastConsent { get; private set; }
    public event Action? Changed;
    public FoodMerchantFulfillmentSettingsViewModel Merchant { get; } = new(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 101, Revision = 4, FixedContributionKrw = 2000, Enabled = true }),
        (r, _) => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 101, Revision = r.ExpectedRevision + 1, FixedContributionKrw = r.FixedContributionKrw, Enabled = r.Enabled }));
    private void Watch(System.ComponentModel.INotifyPropertyChanged vm) => vm.PropertyChanged += (_, _) => Changed?.Invoke();
    public async Task PrepareAsync()
    {
        if (_prepared == Scenario) return; _prepared = Scenario;
        Watch(Writer); Watch(Authentication); Watch(FoodDetail); Watch(Merchant);
        if (IsLivingCreate)
        {
            var vm = Authoring; vm.Initialize(null); vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync();
            vm.Draft.CargoName = "책 한 상자"; vm.Draft.WeightKg = 1;
            foreach (var p in new[] { vm.Draft.Pickup, vm.Draft.Dropoff })
            { p.주소.도로명주소 = "검토용 장소"; p.연락처.이름 = "검토 담당자"; p.연락처.전화번호 = "010-0000-0000"; }
        }
        if (IsFoodCreate)
        {
            if (commerce is CommerceProtectionPreview.CommercePreviewClient preview) preview.State = "disclosure";
            await Authentication.복원Async();
            Writer.음식점설정(new() { 음식점 = new 음식점공개요약응답 { Id = 101, 상호명 = "검토 분식", 주문가능여부 = true },
                메뉴목록 = [new 음식점메뉴공개응답 { Id = 1001, 메뉴명 = "예시 김밥", 판매가 = 4000 }] });
            Writer.메뉴수량변경(1001, 2); Writer.수령인명 = "검토자"; Writer.연락처 = "010-0000-0000"; Writer.주소 = "검토용 전달 장소";
        }
        else if (Scenario.StartsWith("food-", StringComparison.Ordinal)) await FoodDetail.조회Async("preview-auto");
    }
    private bool Food => Scenario.StartsWith("food-", StringComparison.Ordinal);
    private bool Paused => Scenario.EndsWith("paused", StringComparison.Ordinal);
    private AutoFulfillmentSnapshot Current => _snapshot ??= new()
    {
        SourceKind = Food ? FulfillmentSourceKinds.FoodOrder : FulfillmentSourceKinds.NeighborhoodDelivery, SourceId = "preview-auto",
        StateCode = Scenario.EndsWith("unknown") ? AutoFulfillmentStates.ResultUnknown : Paused || Scenario.EndsWith("review") ? AutoFulfillmentStates.NeedsReview : AutoFulfillmentStates.Accepted,
        ProviderId = "mock-quick", ProviderName = "모의 퀵 배송", Revision = 2, ConsentRevision = 1,
        MaxUserChargeKrw = Paused ? 0 : Food ? 5000 : 7000, TotalCostKrw = Paused || Scenario.EndsWith("unknown") ? null : 6000, UserChargeKrw = Paused || Scenario.EndsWith("unknown") ? null : Food ? 4000 : 6000,
        RestaurantContributionKrw = !Paused && Food ? 2000 : 0, FrozenRestaurantSupportKrw = !Paused && Food ? 2000 : 0, RestaurantSupportRevision = !Paused && Food ? 7 : null,
        EstimatedCompletionUtc = Paused ? null : DateTime.UtcNow.AddHours(1), UpdatedAtUtc = DateTime.UtcNow, IsSimulation = true,
        CanCancel = !Paused && !Scenario.EndsWith("review"), CanRenewConsent = !Paused && Scenario.EndsWith("review"), ExecutionPaused = Paused,
        SelectionReason = Paused ? "자동 배송 처리 기능이 중지되어 원 요청만 조회합니다." : "시간 조건을 충족하는 처리처 중 총비용이 가장 낮습니다.", Notice = AutoFulfillmentNotice.Simulation
    };
    private NeighborhoodDeliveryResponse LivingDetail() => new() { RequestId = "preview-auto", CargoName = "책 한 상자", FareKrw = 4000,
        AutoFulfillment = Current, ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued, CreatedAtUtc = DateTime.UtcNow,
        Request = new() { 의뢰Id = "preview-auto", 주문자UserId = "preview-owner", 픽업지 = "검토 픽업 장소", 하차지 = "검토 전달 장소" } };
    private Task<AutoFulfillmentSnapshot?> Action(string kind, Guid id)
    {
        LastActionId = id;
        if (kind == "refresh") RefreshCalls++; if (kind == "cancel") CancelCalls++;
        Changed?.Invoke();
        if (Scenario.EndsWith("unknown") && _lostActionId is null) { _lostActionId = id; return Task.FromException<AutoFulfillmentSnapshot?>(new HttpRequestException("Synthetic response lost")); }
        if (_lostActionId is { } original && original != id) return Task.FromException<AutoFulfillmentSnapshot?>(new InvalidOperationException("Synthetic original operation mismatch"));
        if (kind == "cancel") { Current.StateCode = AutoFulfillmentStates.CancelPending; Current.CanCancel = false; Current.CanRenewConsent = false; }
        else if (Current.StateCode == AutoFulfillmentStates.CancelPending) { Current.StateCode = AutoFulfillmentStates.Cancelled; Current.CanRenewConsent = true; }
        else if (Current.StateCode == AutoFulfillmentStates.ResultUnknown) Current.StateCode = AutoFulfillmentStates.Accepted;
        Current.Revision++; Current.UpdatedAtUtc = DateTime.UtcNow;
        return Task.FromResult<AutoFulfillmentSnapshot?>(Current);
    }
    private Task<AutoFulfillmentSnapshot?> Consent(AutoFulfillmentConsent request)
    {
        if (request.ExpectedRevision != Current.ConsentRevision)
            return Task.FromException<AutoFulfillmentSnapshot?>(new SsalddelApiException("Synthetic consent revision conflict", 409, "preview", "", null));
        LastActionId = request.ClientRequestId; LastConsent = request; ConsentCalls++;
        Current.StateCode = request.Revoked ? AutoFulfillmentStates.NeedsReview : AutoFulfillmentStates.AwaitingRouting;
        Current.MaxUserChargeKrw = request.MaxUserChargeKrw; Current.CanRenewConsent = request.Revoked; Current.Revision++; Current.ConsentRevision++; Changed?.Invoke();
        return Task.FromResult<AutoFulfillmentSnapshot?>(Current);
    }
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request r, CancellationToken ct) => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = r.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest r, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000, 예상거리Km = 2 }, FulfillmentRoutingEnabled = Scenario != "living-feature-off" });
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest r, CancellationToken ct)
    {
        CreateCalls++; LastCreateId = r.ClientRequestId; LastConsent = r.AutoFulfillment; Changed?.Invoke();
        if (Scenario == "living-lost" && !_createLost) { _createLost = true; return Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("Synthetic create response lost")); }
        return Task.FromResult<NeighborhoodDeliveryResponse?>(null); // 원 요청 복구 화면을 유지하며 실제 페이지로 이동하지 않습니다.
    }
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(LivingDetail());
    public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([LivingDetail()]);
    public async Task<NeighborhoodDeliveryResponse?> AutoConsentAsync(string id, AutoFulfillmentConsent r, CancellationToken ct) { await Consent(r); return LivingDetail(); }
    public async Task<NeighborhoodDeliveryResponse?> AutoRefreshAsync(string id, AutoFulfillmentActionRequest r, CancellationToken ct) { await Action("refresh", r.ClientRequestId); return LivingDetail(); }
    public async Task<NeighborhoodDeliveryResponse?> AutoCancelAsync(string id, AutoFulfillmentActionRequest r, CancellationToken ct) { await Action("cancel", r.ClientRequestId); return LivingDetail(); }
    public Task<FoodAutoFulfillmentQuoteResponse> 자동배송조건Async(음식주문등록요청 r, CancellationToken ct = default) => Task.FromResult(new FoodAutoFulfillmentQuoteResponse { Enabled = Scenario != "food-feature-off", RestaurantId = r.음식점Id, RestaurantSupportKrw = 2000, RestaurantSupportRevision = 7, Providers = AutoFulfillmentNotice.Providers });
    public Task<음식주문응답> 등록Async(음식주문등록요청 r, CancellationToken ct = default) { CreateCalls++; LastCreateId = r.클라이언트요청Id; LastConsent = r.AutoFulfillment; Changed?.Invoke(); return Task.FromResult(new 음식주문응답 { 주문번호 = "preview-auto", AutoFulfillment = Current }); }
    public Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 r, CancellationToken ct = default) => Task.FromResult(new 주문자음식주문목록응답());
    public Task<주문자음식주문상세응답?> 상세Async(string id, CancellationToken ct = default) => Task.FromResult<주문자음식주문상세응답?>(new() { AutoFulfillment = Current,
        주문 = new() { 주문번호 = id, 음식점Id = 101, 음식점명 = "검토 분식", 상품요약 = "예시 김밥", 상태 = "주문확인", 총주문금액 = 8000 },
        상품목록 = [new() { 상품명 = "예시 김밥", 수량 = 2, 단가 = 4000 }] });
    public Task<AutoFulfillmentSnapshot?> 자동동의Async(string id, AutoFulfillmentConsent r, CancellationToken ct = default) => Consent(r);
    public Task<AutoFulfillmentSnapshot?> 자동진행확인Async(string id, AutoFulfillmentActionRequest r, CancellationToken ct = default) => Action("refresh", r.ClientRequestId);
    public Task<AutoFulfillmentSnapshot?> 자동수행취소Async(string id, AutoFulfillmentActionRequest r, CancellationToken ct = default) => Action("cancel", r.ClientRequestId);
    public Task<음식주문응답> 수령확인Async(string id, 주문자음식주문수령확인요청 r, CancellationToken ct = default) => throw new InvalidOperationException("Not a receipt fixture");
    public Task<주문자앱인증결과> 복원Async(CancellationToken ct = default) => Task.FromResult(new 주문자앱인증결과(new(true, "preview-owner", "화면 검토자")));
    public Task<주문자앱인증결과> 로그인Async(string username, string password, CancellationToken ct = default) => 복원Async(ct);
    public Task 로그아웃Async(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class AutoPreviewForbiddenApi : ISsalddelJsonApiClient
{
    private static InvalidOperationException Forbidden() => new("Preview business API is forbidden.");
    public Task<T?> GetAsync<T>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task<T?> SendAsync<T>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => throw Forbidden();
}
