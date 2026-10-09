using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

// 공통 제품 Razor와 VM을 합성 자료로 실행합니다. 업무 HTTP·DB·외부 처리처·정산을 호출하지 않습니다.
public static class NeighborhoodFulfillmentPreviewRegistration
{
    public static IServiceCollection AddNeighborhoodFulfillmentPreview(this IServiceCollection services)
    {
        services.AddScoped<NeighborhoodFulfillmentPreviewFixture>();
        var authoring = services.Last(value => value.ServiceType == typeof(NeighborhoodDeliveryAuthoringViewModel));
        var query = services.Last(value => value.ServiceType == typeof(NeighborhoodDeliveryQueryViewModel));
        services.AddTransient(sp => NeighborhoodFulfillmentPreviewFixture.IsPreview(sp.GetRequiredService<NavigationManager>())
            ? sp.GetRequiredService<NeighborhoodFulfillmentPreviewFixture>().Authoring
            : (NeighborhoodDeliveryAuthoringViewModel)Resolve(authoring, sp));
        services.AddTransient(sp => NeighborhoodFulfillmentPreviewFixture.IsPreview(sp.GetRequiredService<NavigationManager>())
            ? sp.GetRequiredService<NeighborhoodFulfillmentPreviewFixture>().Query
            : (NeighborhoodDeliveryQueryViewModel)Resolve(query, sp));
        return services;
    }
    private static object Resolve(ServiceDescriptor descriptor, IServiceProvider provider)
        => descriptor.ImplementationFactory is { } factory ? factory(provider)
            : descriptor.ImplementationInstance ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}

internal sealed class NeighborhoodFulfillmentPreviewFixture(NavigationManager navigation) : INeighborhoodDeliveryClient, ISsalddel현재사용자Context
{
    internal static readonly string[] Scenarios = ["create", "create-lost", "feature-off", "expired", "accepted", "unknown", "awaiting-selection", "cancelled", "completed", "error", "anonymous"];
    private string Scenario => new Uri(navigation.Uri).AbsolutePath.Split('/').Last();
    internal static bool IsPreview(NavigationManager navigation) => new Uri(navigation.Uri).AbsolutePath.StartsWith("/neighborhood-fulfillment-preview/", StringComparison.Ordinal);
    public 현재사용자Snapshot 현재사용자 => Scenario == "anonymous" ? 현재사용자Snapshot.익명 : new("preview-owner", "화면 검토자", []);
    private NeighborhoodDeliveryAuthoringViewModel? _authoring;
    private NeighborhoodDeliveryQueryViewModel? _query;
    public NeighborhoodDeliveryAuthoringViewModel Authoring => _authoring ??= new(this, this);
    public NeighborhoodDeliveryQueryViewModel Query => _query ??= new(this, this);
    private NeighborhoodDeliveryResponse? _detail;
    private bool _createLost;
    private Guid? _refreshRecovery;
    private string? _scenario;
    public int CreateCalls { get; private set; }
    public int ChoiceCalls { get; private set; }
    public int CancelCalls { get; private set; }
    public Guid? LastCreateId { get; private set; }
    public Guid? LastActionId { get; private set; }
    public event Action? Changed;
    public bool IsAuthoring => Scenario is "create" or "create-lost" or "feature-off" or "expired";
    public async Task PrepareAsync()
    {
        if (_scenario == Scenario) return;
        _scenario = Scenario;
        if (!IsAuthoring) return;
        var vm = Authoring; vm.Initialize(null); vm.CollectionConsent = true; vm.AgeConfirmed = true;
        await vm.AcceptConsentAsync(); vm.Draft.CargoName = "책 한 상자"; vm.Draft.WeightKg = 1;
        foreach (var place in new[] { vm.Draft.Pickup, vm.Draft.Dropoff })
        { place.주소.도로명주소 = "검토용 장소"; place.연락처.이름 = "예시 담당자"; place.연락처.전화번호 = "010-0000-0000"; }
        // The real notice component obtains its current policy and the reviewer acknowledges it before quoting.
    }
    private NeighborhoodFulfillmentOption[] Options() => Scenario == "feature-off" ? [] : [
        new() { ProviderId = "mock-quick", ProviderName = "모의 퀵배송", QuoteId = "preview-quick", TotalCostKrw = 3200,
            EstimatedCompletionUtc = DateTime.UtcNow.AddHours(1), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), IsAvailable = true, IsRecommended = true, IsSimulation = true },
        new() { ProviderId = "internal", ProviderName = "자체 배차", QuoteId = "preview-internal", TotalCostKrw = 4000,
            EstimatedCompletionUtc = DateTime.UtcNow.AddHours(2), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), IsAvailable = true, IsSimulation = true },
        new() { ProviderId = "mock-local", ProviderName = "모의 생활배송", QuoteId = "preview-local", TotalCostKrw = null,
            ExpiresAtUtc = Scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            IsAvailable = false, UnavailableReason = "완료 시각과 비용 확인 필요", IsSimulation = true }
    ];
    private NeighborhoodDeliveryResponse Current => _detail ??= Detail(Scenario switch
    { "unknown" => "ResultUnknown", "awaiting-selection" => "AwaitingSelection", "cancelled" => "Cancelled", "completed" => "Completed", _ => "Accepted" });
    private static NeighborhoodDeliveryResponse Detail(string state) => new()
    {
        RequestId = "preview-fulfillment", CargoName = "책 한 상자", CreatedAtUtc = DateTime.UtcNow,
        FareKrw = 4000, DistanceKm = 2, SettlementStatusCode = "미확인", ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued,
        Fulfillment = new()
        {
            RequestId = "preview-fulfillment", ProviderId = state == "AwaitingSelection" ? "" : "mock-quick", ProviderName = state == "AwaitingSelection" ? "처리처 미선정" : "모의 퀵배송", IsSimulation = true,
            StateCode = state, CostKrw = state == "AwaitingSelection" ? null : 3200, EstimatedCompletionUtc = state == "AwaitingSelection" ? null : DateTime.UtcNow.AddHours(1), UpdatedAtUtc = DateTime.UtcNow, Revision = 2,
            CanCancel = state is "Accepted" or "InTransit", CanChangeProvider = state is "Cancelled" or "Failed" or "AwaitingSelection",
            Notice = "합성 자료로 연결 흐름을 확인합니다. 실제 업체와 연결되지 않았습니다."
        },
        Request = new() { 의뢰Id = "preview-fulfillment", 주문자UserId = "preview-owner", 픽업지 = "픽업 장소 1", 하차지 = "전달 장소 1" }
    };
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
        => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000, 예상거리Km = 2 }, FulfillmentOptions = Options() });
    public Task<NeighborhoodDeliveryQuoteResponse?> FulfillmentOptionsAsync(string id, CancellationToken ct) => QuoteAsync(new(), ct);
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct)
        => Scenario == "error" ? Task.FromException<NeighborhoodDeliveryResponse?>(new InvalidOperationException("Synthetic fixture read failure")) : Task.FromResult<NeighborhoodDeliveryResponse?>(Current);
    public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([Current]);
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        CreateCalls++; LastCreateId = request.ClientRequestId; Changed?.Invoke();
        if (Scenario == "create-lost" && !_createLost) { _createLost = true; return Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("Synthetic response lost")); }
        // Avoid navigation to a real product route. Preserve unresolved source request in this preview only.
        return Task.FromResult<NeighborhoodDeliveryResponse?>(null);
    }
    public Task<NeighborhoodDeliveryResponse?> ChooseFulfillmentAsync(string id, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct)
    {
        ChoiceCalls++; LastActionId = request.ClientRequestId; Changed?.Invoke();
        if (Current.Fulfillment is not { CanChangeProvider: true }) return Task.FromException<NeighborhoodDeliveryResponse?>(new InvalidOperationException("Synthetic invalid transition"));
        Current.Fulfillment.ProviderId = request.Selection.ProviderId;
        Current.Fulfillment.ProviderName = request.Selection.ProviderId == "internal" ? "자체 배차" : "모의 퀵배송";
        Current.Fulfillment.CostKrw = request.Selection.ConfirmedCostKrw; Move("Accepted");
        return Task.FromResult<NeighborhoodDeliveryResponse?>(Current);
    }
    public Task<NeighborhoodDeliveryResponse?> CancelFulfillmentAsync(string id, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
    { CancelCalls++; LastActionId = request.ClientRequestId; Changed?.Invoke(); Move("CancelPending"); return Task.FromResult<NeighborhoodDeliveryResponse?>(Current); }
    public Task<NeighborhoodDeliveryResponse?> RefreshFulfillmentAsync(string id, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
    {
        LastActionId = request.ClientRequestId; Changed?.Invoke();
        if (Scenario == "unknown" && _refreshRecovery is null) { _refreshRecovery = request.ClientRequestId; return Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("Synthetic refresh response lost")); }
        if (Scenario == "unknown" && _refreshRecovery != request.ClientRequestId) return Task.FromException<NeighborhoodDeliveryResponse?>(new InvalidOperationException("Synthetic recovery ID mismatch"));
        if (Current.Fulfillment?.StateCode == "CancelPending") Move("Cancelled");
        else if (Current.Fulfillment?.StateCode == "ResultUnknown") Move("Accepted");
        return Task.FromResult<NeighborhoodDeliveryResponse?>(Current);
    }
    private void Move(string state)
    {
        var value = Current.Fulfillment!; value.StateCode = state; value.Revision++; value.UpdatedAtUtc = DateTime.UtcNow;
        value.CanCancel = state is "Accepted" or "InTransit"; value.CanChangeProvider = state is "Cancelled" or "Failed";
    }
}
