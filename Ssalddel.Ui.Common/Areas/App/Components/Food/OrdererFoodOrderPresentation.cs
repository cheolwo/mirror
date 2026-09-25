using MudBlazor;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

/// <summary>음식 주문 내역 하위 컴포넌트가 공유하는 표시 형식만 제공합니다.</summary>
internal static class OrdererFoodOrderPresentation
{
    public static string RestaurantLabel(주문자음식주문요약응답 order)
        => string.IsNullOrWhiteSpace(order.음식점명)
            ? $"음식점 #{order.음식점Id}"
            : order.음식점명.Trim();

    public static string DispatchLabel(string? value)
        => string.IsNullOrWhiteSpace(value) || value == 음식주문배차상태코드.미요청
            ? "배차 전"
            : $"배차 {value.Trim()}";

    public static string DeliveryStatusLabel(주문자음식배달진행응답 progress)
        => progress.배차요청됨
            ? ValueOrDash(progress.현재운송상태)
            : "배차 전";

    public static Severity DeliverySeverity(주문자음식배달진행응답 progress)
        => progress.현재운송상태 switch
        {
            "인수완료" => Severity.Success,
            "상차완료" or "운송중" or "하차지도착" => Severity.Info,
            "배차확정" or "이동중" or "상차지도착" => Severity.Normal,
            음식주문배차상태코드.배차불가 => Severity.Error,
            _ => Severity.Warning
        };

    public static bool NeedsDeliveryRecovery(주문자음식배달진행응답 progress)
        => progress.현재운송상태 is 음식주문배차상태코드.배차불가
            or "추천만료"
            or "수락취소"
            or "배차취소";

    public static string DeliveryRecoveryGuide(주문자음식배달진행응답 progress)
        => progress.현재운송상태 == 음식주문배차상태코드.배차불가
            ? "기사 배정을 완료하지 못한 상태입니다. 주문 취소나 환불이 자동 확정되는 것은 아니며, 음식점과 운영 확인 후 안내됩니다."
            : "기존 기사 제안이 종료되었습니다. 다른 기사 제안 가능 여부를 다시 확인하며, 주문 취소나 환불은 별도 확인 후 안내됩니다.";

    public static Severity DriverLocationSeverity(주문자음식배달위치추적응답 location)
        => location.상태 switch
        {
            음식배달위치추적상태코드.추적중 => Severity.Info,
            음식배달위치추적상태코드.갱신지연 => Severity.Warning,
            음식배달위치추적상태코드.종료 => Severity.Success,
            _ => Severity.Normal
        };

    public static string DriverLocationCoordinates(주문자음식배달위치추적응답 location)
        => location.위도.HasValue && location.경도.HasValue
            ? $"{location.위도.Value:F6}, {location.경도.Value:F6}"
            : "—";

    public static Color StatusColor(string? status)
        => 음식주문상태코드.Normalize(status) switch
        {
            음식주문상태코드.전달완료 or 음식주문상태코드.수령확인 => Color.Success,
            음식주문상태코드.거절 or 음식주문상태코드.취소 => Color.Error,
            음식주문상태코드.조리중 or 음식주문상태코드.픽업대기 => Color.Info,
            음식주문상태코드.기사배정 or 음식주문상태코드.픽업완료 => Color.Primary,
            _ => Color.Warning
        };

    public static string Address(string? address, string? detailAddress)
        => string.Join(" ", new[] { address, detailAddress }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())) is { Length: > 0 } value
            ? value
            : "—";

    public static string FormatDate(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime().ToString("yyyy.MM.dd HH:mm");

    public static string FormatOptionalDate(DateTime? value)
        => value.HasValue ? FormatDate(value.Value) : "—";

    public static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static string ErrorMessage(string? value)
        => string.IsNullOrWhiteSpace(value) ? "서버 응답을 확인할 수 없습니다." : value;
}
