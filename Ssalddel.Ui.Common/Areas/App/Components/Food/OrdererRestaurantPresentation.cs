using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

public static class OrdererRestaurantPresentation
{
    public static string EmptyMessage(음식점공개목록ViewModel list)
        => list.검색조건사용중
            ? "검색어나 주문 가능 조건을 바꿔 다시 조회해 주세요."
            : "운영자가 공개 프로필과 메뉴를 등록하면 이 권역에 표시됩니다.";

    public static string DistanceLabel(decimal? distanceKm)
        => distanceKm.HasValue ? $"기준점 {distanceKm.Value:0.##}km" : "거리 기준 없음";

    public static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static string ErrorMessage(string? value)
        => string.IsNullOrWhiteSpace(value) ? "서버 응답을 확인할 수 없습니다." : value;

    public static string? SubmissionReadinessMessage(음식주문작성ViewModel writer)
    {
        if (writer.처리중) return "주문 접수 결과를 확인하고 있습니다.";
        if (writer.입력잠금) return "이전 주문의 접수 결과를 먼저 확인해 주세요.";
        if (writer.재로그인필요) return "다시 로그인한 뒤 주문을 이어가 주세요.";
        if (writer.음식점주문가능여부 is null) return "음식점과 메뉴를 먼저 선택해 주세요.";
        if (writer.음식점주문가능여부 is false) return "현재 주문을 받지 않는 음식점입니다. 다른 음식점을 선택해 주세요.";
        if (writer.메뉴확인중) return "최신 메뉴와 가격을 확인하고 있습니다.";
        if (writer.메뉴재확인필요) return "최신 메뉴를 다시 확인해 주세요.";
        if (!writer.메뉴선택됨) return "위 메뉴에서 주문 수량을 선택해 주세요.";
        if (!writer.최소주문충족) return $"최소 주문금액까지 {Math.Max(0, writer.최소주문금액 - writer.주문금액):N0}원 더 선택해 주세요.";

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(writer.수령인명)) missing.Add("수령인 이름");
        if (string.IsNullOrWhiteSpace(writer.연락처)) missing.Add("연락처");
        if (string.IsNullOrWhiteSpace(writer.주소)) missing.Add("배달 주소");
        if (missing.Count > 0) return $"{string.Join("·", missing)} 입력을 완료해 주세요.";

        if (writer.자동배송확인중) return "배송 지원 조건을 확인하고 있습니다.";
        if (writer.자동배송오류 is not null) return "배송 지원 조건을 다시 확인해 주세요.";
        if (!writer.자동배송제출가능) return "배송 지원 조건과 동의를 확인해 주세요.";
        if (writer.거래보호확인?.NoticeAccepted != true) return "거래 안내를 확인하고 동의해 주세요.";
        if (writer.가격변경확인대기) return "변경된 메뉴 가격을 확인한 뒤 아래 버튼으로 주문해 주세요.";
        return writer.제출가능 ? null : "주문 내용을 다시 확인해 주세요.";
    }
}
