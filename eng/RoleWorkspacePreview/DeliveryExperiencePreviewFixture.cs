using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace RoleWorkspacePreview;

// 표시와 철회 재조회만 검증하는 루프백 예시입니다. 운영 DB·배차·위치·알림 발송을 실행하지 않습니다.
internal sealed class DeliveryExperiencePreviewFixture(NavigationManager navigation, IRoleWorkspaceAccess access, RoleWorkspaceState state) : IRoleWorkspaceApi
{
    private string[] Parts => new Uri(navigation.Uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    public string Role => Parts.ElementAtOrDefault(1) ?? "orderer";
    public string Scenario => Parts.ElementAtOrDefault(2) ?? "known";
    private bool _withdrawn;
    private RoleWorkspaceViewModel? _model;
    public RoleWorkspaceViewModel Model => _model ??= new([Role switch
    { "restaurant" => (IRoleWorkspaceAdapter)new RestaurantRoleWorkspaceAdapter(this), "food-driver" => new FoodDriverRoleWorkspaceAdapter(this, new NoLocation()),
      _ => new OrdererRoleWorkspaceAdapter(this) }], access, state);
    public async Task PrepareAsync() { await Model.InitializeAsync(Role); Model.SetListOnly(true); }
    public async Task WithdrawAsync() { _withdrawn = true; await Model.RefreshAsync(); }
    private IReadOnlyList<DeliveryExperienceSummaryDto> Values => _withdrawn || Scenario == "revoked" ? [] :
        [Summary("PickupCompleted", "Restaurant", "음식점 1", 4), Summary("DeliveryCompleted", "Road", "도로 1", 12),
         Summary("DeliveryCompleted", "ApartmentComplex", "단지 1", 8)];
    private DeliveryExperienceSummaryDto Summary(string metric, string place, string name, long count) => new()
    { MetricCode = metric, PlaceKind = place, PlaceDisplayName = name, CompletedCount = Scenario == "unknown" ? null : count,
      EvidenceStatusCode = Scenario == "unknown" ? "Unknown" : Scenario == "partial" ? "Partial" : "Confirmed",
      AsOfUtc = DateTime.UtcNow, ExecutionModeCode = Scenario == "simulation" ? "Simulation" : "Operational" };
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (roleKey != Role) throw new InvalidOperationException("Preview role mismatch.");
        var ended = Scenario == "ended";
        var order = new 음식주문응답 { 주문번호 = "배달-1", 음식점명 = "음식점 1", 상태 = ended ? 음식주문상태코드.수령확인 : 음식주문상태코드.기사배정,
            배차상태 = ended ? 음식주문배차상태코드.배달완료 : 음식주문배차상태코드.기사배정, 조리시작가능 = !ended,
            CurrentPreparationRound = 1, 총주문금액 = 19000, 상품목록 = [new() { 상품명 = "메뉴 1", 수량 = 1, 단가 = 19000 }],
            ExperienceSummaries = Values, AvailableActions = ended ? [] : [new() { ActionId = 음식배달가능행동Ids.음식점조리시작 }] };
        var detail = new 주문자음식주문상세응답 { 주문 = new() { 주문번호 = "배달-1", 음식점명 = "음식점 1", 상태 = ended ? 음식주문상태코드.수령확인 : 음식주문상태코드.픽업완료,
                총주문금액 = 19000, 상품요약 = "메뉴 1 × 1" }, 상품목록 = order.상품목록,
            배달진행 = new() { 안내 = ended ? "수령을 확인했습니다." : "기사가 음식을 전달하고 있습니다.", 기사전달완료 = ended, 주문자수령확인됨 = ended },
            기사위치 = new() { 상태 = 음식배달위치추적상태코드.추적전, 안내 = "현재 위치를 확인하고 있습니다." }, ExperienceSummaries = Values };
        object result = typeof(T) == typeof(음식점주문수신함응답) ? new 음식점주문수신함응답 { Items = [order], TotalCount = 1 }
            : typeof(T) == typeof(음식주문응답) ? order
            : typeof(T) == typeof(주문자음식주문목록응답) ? new 주문자음식주문목록응답 { Items = [detail.주문], TotalCount = 1 }
            : typeof(T) == typeof(주문자음식주문상세응답) ? detail
            : typeof(T) == typeof(기사운행상태응답) ? new 기사운행상태응답 { Status = "운행중" }
            : typeof(T) == typeof(운영배차수신상태Dto) ? new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On }
            : typeof(T) == typeof(FoodDeliveryDriverCurrentWorkDto) ? new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = ended ? [] : [new() {
                OfferId = "배달-1", RestaurantName = "음식점 1", WorkStatus = Scenario == "pickup" ? DriverWorkOfferStatus.Accepted : DriverWorkOfferStatus.MovingToDropoff,
                Pickup = new() { Address = "픽업 장소 1" }, Dropoff = new() { Address = "전달 장소 1" }, DriverPayout = 4000, OrderSummary = "메뉴 1 × 1",
                ExperienceSummaries = Values, Recipient = new() { DeliveryInstructions = "도착하면 알려 주세요." } }] }
            : throw new InvalidOperationException("Preview supports canonical reads only.");
        return Task.FromResult((T)result);
    }
    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException("Preview writes blocked.");
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException("Preview writes blocked.");
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new InvalidOperationException("Preview uploads blocked.");
    private sealed class NoLocation : IRoleWorkspaceLocationProvider
    { public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) => Task.FromResult<RoleWorkspaceLocation?>(null); }
}
