using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

// 실제 API/원장과 분리된 화면 확인용 DTO입니다. 결정 저장과 외부 지급은 실행하지 않습니다.
public static class PreviewHandoffFixtures
{
    public static object? Read(string role, string path, IRoleWorkspaceAccess access, string uri)
    {
        var related = role == "operator" && path == "api/v1/admin/transports/incidents"
            || role == "food-driver" && path.StartsWith("api/v1/driver/food-deliveries/settlements/", StringComparison.Ordinal)
            || role == "cargo-driver" && (path == "api/v1/driver/transports" || path == "api/v1/driver/transports/201");
        if (!related) return null;
        if (!access.GetIdentity(role).IsAuthenticated) throw new RoleWorkspaceAccessException(401, "로그인 필요");
        if (uri.Contains("scenario=permission", StringComparison.Ordinal)) throw new RoleWorkspaceAccessException(403, "권한 확인 필요");
        if (uri.Contains("scenario=feature-off", StringComparison.Ordinal)) throw new RoleWorkspaceAccessException(404, "기능 미활성");
        var empty = uri.Contains("scenario=empty", StringComparison.Ordinal);
        var owner = access.GetIdentity(role).OwnerId!;
        var now = DateTime.UtcNow;
        if (role == "operator") return empty ? Array.Empty<비정상운송사건운영응답>() : new[] { new 비정상운송사건운영응답
        {
            사건StableId = "preview-incident", 운송Id = 201, 운송의뢰Id = "preview-request-1", 사건유형Code = "QuantityMismatch",
            상태Code = "OperationsReviewPending", 현재담당Code = "PlatformOperationsReview", 업무통제상태Code = "PartiallyHeld",
            보류범위Code = "AffectedQuantity", 정산보류적용여부 = true, 전체수량 = 12, 정상확인수량 = 10, 영향수량 = 2,
            최초신고시각Utc = now, 최근신고시각Utc = now, Revision = 3, 증빙참조있음 = true
        } };
        if (role == "food-driver")
        {
            var completionDate = DateOnly.FromDateTime(now.AddHours(9));
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(uri).Query);
            if (query.TryGetValue("date", out var dateText) && DateOnly.TryParseExact(dateText, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var selectedDate))
                completionDate = selectedDate;
            if (path.StartsWith("api/v1/driver/food-deliveries/settlements/daily?date=", StringComparison.Ordinal))
                completionDate = DateOnly.Parse(path.Split("date=")[1], System.Globalization.CultureInfo.InvariantCulture);
            var settlement = new FoodDeliveryOrderSettlementDto
            {
                SettlementId = "preview-settlement", DriverId = owner, OrderNo = "preview-order-1", RestaurantName = "음식점 1", GrossAmount = 4180,
                CompletedAtUtc = completionDate.ToDateTime(new TimeOnly(3, 0), DateTimeKind.Utc), SettlementStatusCode = "AwaitingDeductions", PricingBreakdown = new()
                { PickupAmount = 700, DropoffAmount = 700, DistanceAmount = 2080, TimeSurchargeAmount = 700, DistanceKm = 2.6m }
            };
            if (path.StartsWith("api/v1/driver/food-deliveries/settlements/daily?date=", StringComparison.Ordinal))
                return new FoodDeliveryDailySettlementDto { DriverId = owner, CompletionDateKst = DateOnly.Parse(path.Split("date=")[1], System.Globalization.CultureInfo.InvariantCulture),
                    OrderSettlements = empty ? [] : [settlement] };
            if (path != "api/v1/driver/food-deliveries/settlements/preview-settlement/detail") throw new RoleWorkspaceAccessException(404, "내역 없음");
            var expired = uri.Contains("scenario=expired", StringComparison.Ordinal);
            return new FoodDeliveryCompletedDeliveryDetailDto { Settlement = settlement, ServerNowUtc = now, DetailExpiresAtUtc = expired ? now.AddMinutes(-1) : now.AddMinutes(3),
                DetailAccessStatusCode = expired ? "Expired" : "Allowed", OrderDetails = expired ? null : new() { RestaurantAddress = "픽업 장소 1", TotalOrderAmount = 14000,
                    Items = [new() { MenuName = "메뉴 1", Quantity = 1, UnitPrice = 14000 }] }, CustomerDetails = expired ? null : new()
                    { DisplayName = "수령인 1", ContactPhone = "연락처 비공개", Address = "전달 장소 1", DeliveryInstructions = "문 앞에 놓아 주세요." } };
        }
        var cargo = new 기사운송상세응답 { Id = 201, 운송번호 = "운송 1", 기사_운송자 = owner, 상태 = "인수완료", 운임 = 34000, 예상거리Km = 3.2m, UpdatedAt = now };
        return path == "api/v1/driver/transports" ? empty ? Array.Empty<기사운송요약응답>() : new 기사운송요약응답[] { cargo } : cargo;
    }
}
