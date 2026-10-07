using System.Globalization;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>음식 정산 내역과 화물 종료 내역은 각 기존 API에서 읽고 수행/지급을 실행하지 않습니다.</summary>
public sealed class DriverHistoryViewModel : IDisposable
{
    private readonly IRoleWorkspaceApi api;
    private readonly IRoleWorkspaceAccess access;
    private readonly TimeProvider clock;
    private FoodDeliveryCompletedDeliveryDetailDto? foodDetail;
    private long privateStarted;
    private TimeSpan privateRemaining;
    public DriverHistoryViewModel(string roleKey, IRoleWorkspaceApi api, IRoleWorkspaceAccess access, TimeProvider? clock = null)
    {
        if (roleKey is not (RoleWorkspaceCatalog.FoodDriver or RoleWorkspaceCatalog.CargoDriver))
            throw new ArgumentException("기사 내역 역할을 확인해 주세요.", nameof(roleKey));
        RoleKey = roleKey; this.api = api; this.access = access; this.clock = clock ?? TimeProvider.System;
        Lifetime = new(access, roleKey, Clear);
        Date = DateOnly.FromDateTime(this.clock.GetUtcNow().ToOffset(TimeSpan.FromHours(9)).DateTime);
    }
    public string RoleKey { get; }
    public bool IsFood => RoleKey == RoleWorkspaceCatalog.FoodDriver;
    public CargoInputLifetime Lifetime { get; }
    public DateOnly Date { get; set; }
    public string? RecordId { get; private set; }
    public bool Loaded { get; private set; }
    public IReadOnlyList<RoleWorkspaceItem> Items { get; private set; } = [];
    public RoleWorkspaceItem? Detail { get; private set; }
    public string DetailAccessNotice { get; private set; } = "";
    public FoodDeliveryCompletedOrderDetailsDto? OrderDetails => CanShowPrivateDetails ? foodDetail?.OrderDetails : null;
    public FoodDeliveryCompletedCustomerDetailsDto? CustomerDetails => CanShowPrivateDetails ? foodDetail?.CustomerDetails : null;
    public bool CanShowPrivateDetails => foodDetail?.DetailAccessStatusCode == FoodDeliveryCompletedDetailAccessStatusCodes.Allowed
        && clock.GetElapsedTime(privateStarted) < privateRemaining;

    public Task LoadAsync(string? recordId = null)
    {
        Lifetime.Reset(); RecordId = recordId;
        return Lifetime.RunAsync(async ct =>
        {
            if (recordId is not null && (RoleWorkspaceNavigation.StableId(recordId) != recordId || recordId.Length == 0))
                throw new ArgumentException("내역 식별자를 확인해 주세요.");
            var owner = access.GetIdentity(RoleKey).OwnerId;
            if (IsFood && recordId is null)
            {
                var date = Date;
                var value = await api.GetAsync<FoodDeliveryDailySettlementDto>(RoleKey,
                    "api/v1/driver/food-deliveries/settlements/daily?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);
                Lifetime.RequireCurrent(ct);
                if (value.DriverId != owner || value.CompletionDateKst != date
                    || value.OrderSettlements.Any(item => item.DriverId != owner || !ValidId(item.SettlementId))
                    || value.OrderSettlements.Select(item => item.SettlementId).Distinct(StringComparer.Ordinal).Count() != value.OrderSettlements.Count)
                    throw new InvalidOperationException("본인 완료 배달 내역을 확인하지 못했습니다.");
                Items = value.OrderSettlements.OrderByDescending(item => item.CompletedAtUtc).Select(MapFood).ToArray();
            }
            else if (IsFood)
            {
                var started = clock.GetTimestamp();
                var value = await api.GetAsync<FoodDeliveryCompletedDeliveryDetailDto>(RoleKey,
                    "api/v1/driver/food-deliveries/settlements/" + Uri.EscapeDataString(recordId!) + "/detail", ct);
                Lifetime.RequireCurrent(ct);
                if (value.Settlement.DriverId != owner || value.Settlement.SettlementId != recordId)
                    throw new InvalidOperationException("선택한 완료 배달의 응답이 일치하지 않습니다.");
                var remaining = value.DetailExpiresAtUtc is { Kind: DateTimeKind.Utc } expiry
                    && value.ServerNowUtc is { Kind: DateTimeKind.Utc } now && now != default
                    ? expiry - now - clock.GetElapsedTime(started) : TimeSpan.Zero;
                privateRemaining = remaining; privateStarted = clock.GetTimestamp();
                if (value.DetailAccessStatusCode != FoodDeliveryCompletedDetailAccessStatusCodes.Allowed || remaining <= TimeSpan.Zero)
                { value.OrderDetails = null; value.CustomerDetails = null; privateRemaining = TimeSpan.Zero; }
                foodDetail = value; Detail = MapFood(value.Settlement, true);
                DetailAccessNotice = CanShowPrivateDetails ? "주문·고객 정보는 열람 기한 안에서만 표시합니다."
                    : "주문·고객 정보의 열람 기한 또는 제공 조건이 충족되지 않아 정산 정보만 표시합니다.";
            }
            else if (recordId is null)
            {
                var value = await api.GetAsync<IReadOnlyList<기사운송요약응답>>(RoleKey, "api/v1/driver/transports", ct);
                Lifetime.RequireCurrent(ct);
                if (value.Any(item => item.Id <= 0 || !item.개인정보제공보류 && item.기사_운송자 != owner)
                    || value.Select(item => item.Id).Distinct().Count() != value.Count)
                    throw new InvalidOperationException("본인 운송 내역을 확인하지 못했습니다.");
                Items = value.Where(item => Historical(item.상태)).OrderByDescending(item => item.UpdatedAt).Select(MapCargo).ToArray();
            }
            else
            {
                var id = long.Parse(CargoWorkspaceRoutes.PositiveNumber(recordId!), CultureInfo.InvariantCulture);
                var value = await api.GetAsync<기사운송상세응답>(RoleKey, "api/v1/driver/transports/" + id, ct);
                Lifetime.RequireCurrent(ct);
                if (value.Id != id || !Historical(value.상태) || !value.개인정보제공보류 && value.기사_운송자 != owner)
                    throw new InvalidOperationException("선택한 종료 운송의 응답이 일치하지 않습니다.");
                Detail = MapCargo(value);
                DetailAccessNotice = "종료 운송의 연락처·상세 주소·증빙 원본은 이 내역 화면에 표시하지 않습니다.";
            }
            Loaded = true;
        });
    }

    public void ExpirePrivateDetails()
    {
        if (foodDetail is null || CanShowPrivateDetails || foodDetail.OrderDetails is null && foodDetail.CustomerDetails is null) return;
        foodDetail.OrderDetails = null; foodDetail.CustomerDetails = null;
        DetailAccessNotice = "주문·고객 정보 열람 기한이 지나 정산 정보만 표시합니다.";
        Lifetime.Notify();
    }
    public static bool Historical(string? state) => CargoWorkspacePresentation.IsClosed(state)
        || state is "배달중단" or "중단" or "실패" or "종료" or "배차취소";
    private static bool ValidId(string id) => id.Length > 0 && RoleWorkspaceNavigation.StableId(id) == id;
    private static string Money(decimal? amount) => amount is { } value ? value.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + "원" : "미확정";
    private static string Time(DateTime value) => value == default ? "시각 미확인" : value.ToUniversalTime().AddHours(9).ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) + " (한국)";
    private static RoleWorkspaceItem MapFood(FoodDeliveryOrderSettlementDto value) => MapFood(value, false);
    private static RoleWorkspaceItem MapFood(FoodDeliveryOrderSettlementDto value, bool detail)
    {
        var display = FoodDeliverySettlementDisplay.From(value);
        var sections = new List<RoleWorkspaceSection>
        {
            new("배달 정산", [new("주문", value.OrderNo), new("전달 완료", Time(value.CompletedAtUtc)), new("배달료", display.GrossText),
                new("공제", display.DeductionText), new("수령액", display.NetText), new("정산", display.ProgressText),
                new("실입금", value.IsActualTransferCompleted ? "입금 완료 기록 있음" : "입금 확인 전"), new("안내", display.NoticeText)])
        };
        if (detail)
        {
            var fee = value.PricingBreakdown;
            sections.Add(new("수락 당시 요금 구성", [new("픽업비", Money(fee.PickupAmount)), new("전달비", Money(fee.DropoffAmount)),
                new("거리비", Money(fee.DistanceAmount)), new("시간 할증", Money(fee.TimeSurchargeAmount)),
                new("날씨 할증", Money(fee.WeatherSurchargeAmount)), new("수요 할증", Money(fee.DemandSurchargeAmount)),
                new("거리", fee.DistanceKm is { } distance ? distance.ToString("0.###", CultureInfo.InvariantCulture) + " km" : "미확인")]));
        }
        return new(value.SettlementId, string.IsNullOrWhiteSpace(value.RestaurantName) ? "완료 배달" : value.RestaurantName,
            "전달 완료", Time(value.CompletedAtUtc), sections,
            Summary: new("배달료", display.GrossText, Request: new("정산", display.ProgressText)));
    }
    private static RoleWorkspaceItem MapCargo(기사운송요약응답 value)
    {
        var sections = new List<RoleWorkspaceSection>
        {
            new("운송 내역", [new("운송", string.IsNullOrWhiteSpace(value.운송번호) ? value.Id.ToString(CultureInfo.InvariantCulture) : value.운송번호),
                new("종료 상태", value.상태), new("최근 변경", Time(value.UpdatedAt)), new("운임", Money(value.운임)),
                new("거리", value.예상거리Km is { } km ? km.ToString("0.###", CultureInfo.InvariantCulture) + " km" : "미확인"),
                new("정산", value.정산보류 ? "정산 보류" : "실지급 확인 자료 없음"),
                new("증빙", value is 기사운송상세응답 detail && !string.IsNullOrWhiteSpace(detail.첨부Json) ? "등록된 증빙 있음" : "증빙 등록 확인 필요")])
        };
        return new(value.Id.ToString(CultureInfo.InvariantCulture), "운송 " + value.Id, value.상태, Time(value.UpdatedAt), sections,
            Summary: new("운임", Money(value.운임), Request: new("정산", value.정산보류 ? "정산 보류" : "실지급 확인 자료 없음")));
    }
    private void Clear()
    {
        if (foodDetail is not null) { foodDetail.OrderDetails = null; foodDetail.CustomerDetails = null; }
        foodDetail = null; privateRemaining = TimeSpan.Zero;
        Loaded = false; Items = []; Detail = null; DetailAccessNotice = "";
    }
    public void Dispose() => Lifetime.Dispose();
}
