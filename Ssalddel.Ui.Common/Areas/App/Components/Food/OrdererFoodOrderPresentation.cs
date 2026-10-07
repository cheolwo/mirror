using MudBlazor;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

/// <summary>음식 주문 내역 하위 컴포넌트가 공유하는 표시 형식만 제공합니다.</summary>
internal static class OrdererFoodOrderPresentation
{
    public static bool IsStopped(string? status)
        => 음식주문상태코드.Normalize(status) is 음식주문상태코드.취소 or 음식주문상태코드.거절;

    public static string RestaurantLabel(주문자음식주문요약응답 order)
        => string.IsNullOrWhiteSpace(order.음식점명)
            ? "음식점 이름 확인 필요"
            : order.음식점명.Trim();

    public static string DispatchLabel(string? value)
        => value?.Trim() switch
        {
            음식주문배차상태코드.배차대기 or 음식주문배차상태코드.추천중 => "기사를 찾는 중",
            음식주문배차상태코드.기사배정 => "기사 배정됨",
            음식주문배차상태코드.배달중 => "배달 중",
            음식주문배차상태코드.배달완료 => "배달 완료",
            음식주문배차상태코드.배차불가 or "추천만료" or "수락취소" or "배차취소" => "기사 배정 확인 필요",
            null or "" or 음식주문배차상태코드.미요청 => "기사 배정 전",
            _ => "배달 상태 확인 필요"
        };

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

    public static string ErrorMessage(Api작업오류? error)
        => error switch
        {
            { Http상태코드: 401 } => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
            { Http상태코드: 403 } => "이 계정에서는 확인할 수 없습니다. 로그인한 계정을 확인해 주세요.",
            { Http상태코드: 400 } or { 코드: "validation" } => "입력한 내용을 확인한 뒤 다시 시도해 주세요.",
            { Http상태코드: 409 } => "주문 상태가 변경되었습니다. 새로고침 후 다시 시도해 주세요.",
            { Http상태코드: 429 } => "요청이 많습니다. 잠시 후 다시 시도해 주세요.",
            { 코드: "transport-failure" or "timeout" } => "인터넷 연결을 확인하고 다시 시도해 주세요.",
            _ => "잠시 후 다시 시도해 주세요. 문제가 계속되면 운영자에게 문의해 주세요."
        };
}
