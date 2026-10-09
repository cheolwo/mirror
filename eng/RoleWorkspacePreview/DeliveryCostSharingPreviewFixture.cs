using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using System.Text.Json;

namespace RoleWorkspacePreview;

// 실제 정책 화면/VM을 연결하며 데이터와 명령은 이 루프백 호스트 메모리에만 존재합니다.
internal sealed class DeliveryCostSharingPreviewFixture(NavigationManager navigation, I통신판매보호Client commerce, IRoleWorkspaceAccess access, RoleWorkspaceState roleState) :
    I주문자음식주문쓰기Service, I주문자앱인증Service, IRoleWorkspaceApi
{
    internal static string[] Scenarios = ["merchant-new", "merchant-fixed", "merchant-percentage", "merchant-restaurant", "merchant-lost", "merchant-legacy",
        "food-customer", "food-fixed", "food-percentage", "food-restaurant", "food-policy-change",
        "detail-percentage", "detail-restaurant-held", "detail-waiting", "detail-unknown", "detail-lost", "role-percentage", "role-held", "role-waiting", "role-paused"];
    private string Scenario => new Uri(navigation.Uri).AbsolutePath.Split('/').Last();
    public bool MerchantScene => Scenario.StartsWith("merchant-", StringComparison.Ordinal);
    public bool FoodScene => Scenario.StartsWith("food-", StringComparison.Ordinal);
    public bool RoleScene => Scenario.StartsWith("role-", StringComparison.Ordinal);
    public event Action? Changed;
    public int SaveCalls { get; private set; }
    public int SubmitCalls { get; private set; }
    public int ConsentCalls { get; private set; }
    public RestaurantFulfillmentSupportRequest? LastSave { get; private set; }
    public AutoFulfillmentConsent? LastConsent { get; private set; }
    public Guid? LastSubmitId { get; private set; }
    private string? _prepared;
    private string? _lostSaveJson, _lostConsentJson;
    private long _supportRevision = 7;
    private DeliveryCostSharingPolicy? _policy;
    private FoodMerchantFulfillmentSettingsViewModel? _merchant;
    private 음식주문작성ViewModel? _writer;
    private 주문자앱인증ViewModel? _authentication;
    private AutoFulfillmentQueryViewModel? _query;
    private AutoFulfillmentSnapshot? _snapshot;
    private RoleWorkspaceViewModel? _roleModel;
    public RoleWorkspaceViewModel RoleModel => _roleModel ??= new([new RestaurantRoleWorkspaceAdapter(this)], access, roleState);
    public FoodMerchantFulfillmentSettingsViewModel Merchant => _merchant ??= new(_ => Task.FromResult<RestaurantFulfillmentSupportResponse?>(new()
        { RestaurantId = 101, Revision = Scenario == "merchant-legacy" ? 4 : 0, FixedContributionKrw = Scenario == "merchant-legacy" ? 2000 : 0,
          Enabled = Scenario == "merchant-legacy" }), SaveAsync);
    public 음식주문작성ViewModel Writer => _writer ??= new(this);
    public 주문자앱인증ViewModel Authentication => _authentication ??= new(this);
    public AutoFulfillmentQueryViewModel Query => _query ??= new(ConsentAsync, (_, _, _) => Task.FromResult<AutoFulfillmentSnapshot?>(Current), (_, _, _) => throw new InvalidOperationException("No cancellation fixture"));
    private DeliveryCostSharingPolicy Policy => _policy ??= Scenario switch
    {
        "food-customer" => new() { Mode = "CustomerPays" },
        "food-restaurant" or "detail-restaurant-held" or "role-held" => new() { Mode = "RestaurantPays", MaximumRestaurantChargeKrw = 5000 },
        "food-fixed" => new() { Mode = "FixedContribution", FixedAmountKrw = 2000 },
        _ => new() { Mode = "PercentageShare", RestaurantSharePercent = 33.33m, MaximumRestaurantChargeKrw = 2000 }
    };
    public AutoFulfillmentSnapshot Current
    {
        get
        {
            if (_snapshot is not null) return _snapshot;
            var calculation = DeliveryCostSharingCalculator.Calculate(6000, Policy);
            var waiting = Scenario is "detail-waiting" or "role-waiting";
            var held = Scenario is "detail-restaurant-held" or "role-held";
            var paused = Scenario == "role-paused";
            var unknown = Scenario == "detail-unknown";
            return _snapshot = new()
            {
                SourceKind = FulfillmentSourceKinds.FoodOrder, SourceId = "preview-cost-sharing", Revision = 9, ConsentRevision = 3,
                CostSharingPolicy = DeliveryCostSharingCalculator.Clone(Policy), CostSharingAllowed = waiting || paused || unknown ? null : calculation.IsAllowed, ExecutionPaused = paused,
                StateCode = paused || unknown ? AutoFulfillmentStates.NeedsReview : waiting ? AutoFulfillmentStates.AwaitingSource : Scenario == "detail-lost" || !calculation.IsAllowed ? AutoFulfillmentStates.NeedsReview : AutoFulfillmentStates.Accepted,
                AttemptId = waiting || held || paused || unknown || Scenario == "detail-lost" ? null : "synthetic-attempt", ProviderId = waiting || held || paused || unknown ? null : "mock-quick", ProviderName = waiting || held || paused || unknown ? null : "모의 퀵 배송",
                TotalCostKrw = paused || unknown ? null : waiting || held ? 0 : 6000, RestaurantContributionKrw = waiting || paused || unknown ? 0 : calculation.RestaurantChargeKrw ?? 0,
                UserChargeKrw = paused || unknown ? null : waiting ? 0 : calculation.CustomerChargeKrw, MaxUserChargeKrw = paused ? null : 5000, RestaurantSupportRevision = 7,
                FrozenRestaurantSupportKrw = Policy.Mode == "FixedContribution" ? Policy.FixedAmountKrw!.Value : 0,
                CanRenewConsent = Scenario == "detail-lost" || !calculation.IsAllowed, CanCancel = false, UpdatedAtUtc = DateTime.UtcNow,
                ReviewCode = calculation.ReasonCode, SelectionReason = waiting ? "음식점 확인 후 처리처 견적을 조회합니다." : !calculation.IsAllowed ? "음식점 전액 부담 한도를 충족하지 못했습니다." : "시간 조건을 충족하는 처리처 중 총비용이 가장 낮습니다.",
                Notice = AutoFulfillmentNotice.Simulation, IsSimulation = true
            };
        }
    }
    public async Task PrepareAsync()
    {
        if (_prepared == Scenario) return; _prepared = Scenario;
        foreach (var vm in new System.ComponentModel.INotifyPropertyChanged[] { Merchant, Writer, Authentication, Query, Query.Conditions }) vm.PropertyChanged += (_, _) => Changed?.Invoke();
        if (FoodScene)
        {
            if (commerce is CommerceProtectionPreview.CommercePreviewClient preview) preview.State = "disclosure";
            await Authentication.복원Async();
            Writer.음식점설정(new() { 음식점 = new 음식점공개요약응답 { Id = 101, 상호명 = "정책 검토 분식", 주문가능여부 = true },
                메뉴목록 = [new 음식점메뉴공개응답 { Id = 1001, 메뉴명 = "예시 김밥", 판매가 = 4000 }] });
            Writer.메뉴수량변경(1001, 2); Writer.수령인명 = "검토자"; Writer.연락처 = "010-0000-0000"; Writer.주소 = "검토용 전달 장소";
        }
        else if (RoleScene) { await RoleModel.InitializeAsync("restaurant"); RoleModel.SetListOnly(true); RoleModel.SetDetailsExpanded(true); }
        else if (!MerchantScene) Query.Bind(FulfillmentSourceKinds.FoodOrder, "preview-cost-sharing", "preview-owner", Current);
    }
    private Task<RestaurantFulfillmentSupportResponse?> SaveAsync(RestaurantFulfillmentSupportRequest request, CancellationToken ct)
    {
        SaveCalls++; LastSave = Clone(request); Changed?.Invoke();
        var json = JsonSerializer.Serialize(request);
        if (Scenario == "merchant-lost" && _lostSaveJson is null) { _lostSaveJson = json; return Task.FromException<RestaurantFulfillmentSupportResponse?>(new HttpRequestException("Synthetic response lost")); }
        if (_lostSaveJson is not null && _lostSaveJson != json) throw new InvalidOperationException("Original policy request changed.");
        return Task.FromResult<RestaurantFulfillmentSupportResponse?>(new() { RestaurantId = 101, Revision = request.ExpectedRevision + 1,
            FixedContributionKrw = request.FixedContributionKrw, CostSharingPolicy = DeliveryCostSharingCalculator.Clone(request.CostSharingPolicy!), Enabled = request.Enabled });
    }
    private Task<AutoFulfillmentSnapshot?> ConsentAsync(string id, AutoFulfillmentConsent request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request); ConsentCalls++; LastConsent = Clone(request); Changed?.Invoke();
        if (Scenario == "detail-lost" && _lostConsentJson is null) { _lostConsentJson = json; return Task.FromException<AutoFulfillmentSnapshot?>(new HttpRequestException("Synthetic consent lost")); }
        if (_lostConsentJson is not null && _lostConsentJson != json) throw new InvalidOperationException("Original consent changed.");
        if (request.ExpectedRevision != Current.ConsentRevision || !DeliveryCostSharingCalculator.Equivalent(request.ConfirmedCostSharingPolicy!, Policy)) throw new InvalidOperationException("Frozen consent identity changed.");
        Current.Revision++; Current.ConsentRevision++; Current.StateCode = AutoFulfillmentStates.AwaitingRouting; Current.CanRenewConsent = false;
        return Task.FromResult<AutoFulfillmentSnapshot?>(Current);
    }
    public async Task ChangePolicyAsync()
    {
        _policy = new() { Mode = "RestaurantPays", MaximumRestaurantChargeKrw = 5000 }; _supportRevision++;
        await Writer.자동배송조건확인Async(force: true); Changed?.Invoke();
    }
    public Task<FoodAutoFulfillmentQuoteResponse> 자동배송조건Async(음식주문등록요청 request, CancellationToken ct = default) => Task.FromResult(new FoodAutoFulfillmentQuoteResponse
        { Enabled = true, RestaurantId = request.음식점Id, RestaurantSupportKrw = Policy.Mode == "FixedContribution" ? Policy.FixedAmountKrw!.Value : 0,
          CostSharingPolicy = DeliveryCostSharingCalculator.Clone(Policy), RestaurantSupportRevision = _supportRevision, Providers = AutoFulfillmentNotice.Providers });
    public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken ct = default)
    {
        SubmitCalls++; LastSubmitId = request.클라이언트요청Id; LastConsent = Clone(request.AutoFulfillment); Changed?.Invoke();
        return Task.FromResult(new 음식주문응답 { 주문번호 = "preview-cost-sharing" });
    }
    public Task<주문자앱인증결과> 복원Async(CancellationToken ct = default) => Task.FromResult(new 주문자앱인증결과(new(true, "preview-owner", "화면 검토자")));
    public Task<주문자앱인증결과> 로그인Async(string username, string password, CancellationToken ct = default) => 복원Async(ct);
    public Task 로그아웃Async(CancellationToken ct = default) => Task.CompletedTask;
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (roleKey != "restaurant") throw new InvalidOperationException("Unexpected synthetic role.");
        var order = new 음식주문응답 { 주문번호 = "preview-cost-sharing", 음식점명 = "정책 검토 분식", 상태 = 음식주문상태코드.주문확인, AutoFulfillment = Current };
        if (typeof(T) == typeof(음식점주문수신함응답)) return Task.FromResult((T)(object)new 음식점주문수신함응답 { Items = [order], TotalCount = 1 });
        if (typeof(T) == typeof(음식주문응답)) return Task.FromResult((T)(object)order);
        throw new InvalidOperationException("Unexpected synthetic API read.");
    }
    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException("Role writes blocked.");
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException("Role writes blocked.");
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new InvalidOperationException("Role uploads blocked.");
    private static T? Clone<T>(T? value) => value is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
}
