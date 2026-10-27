namespace DriverApp.Services;

public static class DriverMobileScreenCatalog
{
    public static readonly DriverMobileScreenDefinition Home = new(
        "04.01",
        "기사 홈 요약",
        "운행 준비와 제안, 진행 중 업무를 한눈에 확인합니다.",
        "오늘");

    public static DriverMobileScreenDefinition? Resolve(string? path)
    {
        var normalizedPath = Normalize(path);

        if (normalizedPath is DriverRoutes.Home or DriverRoutes.HomeSummary)
        {
            return Home;
        }

        return normalizedPath switch
        {
            DriverRoutes.Login => new(
                "04.00",
                "기사 로그인",
                "배차 추천과 운송 정보를 확인할 기사 세션을 연결합니다.",
                "계정"),
            DriverRoutes.WorkStart => new(
                "04.02",
                "운행 시작",
                "운행 전 차량·안전·위치 공유 동의를 명시적으로 확인합니다.",
                "준비"),
            DriverRoutes.CommunityInquiries => new(
                "04.03",
                "커뮤니티 개별 의뢰",
                "공동행동 현장에서 공개된 운송 문의를 조건과 함께 봅니다.",
                "문의"),
            DriverRoutes.Recommendations => new(
                "04.04",
                "운송 추천",
                "내가 설정한 지역·차량·시간 조건에 맞는 정보 후보를 비교합니다.",
                "추천"),
            DriverRoutes.ExplorationCampaigns => new(
                "04.07",
                "보낸 탐색 문의함",
                "공개 수요에 보낸 질문과 응답 상태를 확인합니다.",
                "탐색"),
            DriverRoutes.FoodDeliveries => new(
                "04.08A",
                "음식 배달 업무",
                "음식점 픽업 제안부터 고객 전달 완료까지 한 흐름으로 처리합니다.",
                "음식 배달"),
            DriverRoutes.CurrentTransport => new(
                "04.08",
                "현재 운송",
                string.Empty,
                "진행"),
            DriverRoutes.DeliveryHistory => new(
                "04.11",
                "배달 내역",
                "완료한 운송과 증빙·예외 기록을 기간별로 조회합니다.",
                "이력"),
            DriverRoutes.Reservations => new(
                "04.12",
                "운행 예약",
                "확정 일정과 내가 비워 둔 시간대를 구분해 관리합니다.",
                "일정"),
            DriverRoutes.CurrentMonthSettlement => new(
                "04.13",
                "월 정산",
                "완료 운송의 수익·공제·미확인 근거를 투명하게 확인합니다.",
                "정산"),
            DriverRoutes.BankAccount => new(
                "04.14",
                "계좌 정보",
                "정산 계좌의 확인 가능 여부와 운송대금 조회 경로를 안내합니다.",
                "보안"),
            DriverRoutes.Notifications => new(
                "04.15",
                "알림함",
                "참여 의사, 일정 변경, 예외, 보안 요청을 확인합니다.",
                "알림"),
            _ => ResolveDynamic(normalizedPath)
        };
    }

    private static DriverMobileScreenDefinition? ResolveDynamic(string path)
    {
        if (path.StartsWith($"{DriverRoutes.Recommendations}/", StringComparison.OrdinalIgnoreCase))
        {
            return path.EndsWith("/decision", StringComparison.OrdinalIgnoreCase)
                ? new(
                    "04.06",
                    "운송 참여 결정",
                    "확인한 조건을 바탕으로 참여 의사를 직접 제출합니다.",
                    "의사표시")
                : new(
                    "04.05",
                    "추천 상세",
                    "경로·화물·예상 비용을 근거로 확인한 뒤 참여 여부를 결정합니다.",
                    "후보");
        }

        if (path.StartsWith("/driver/transports/", StringComparison.OrdinalIgnoreCase))
        {
            if (path.EndsWith("/pickup", StringComparison.OrdinalIgnoreCase))
            {
                return new(
                    "04.09",
                    "상차",
                    string.Empty,
                    "상차");
            }

            if (path.EndsWith("/dropoff", StringComparison.OrdinalIgnoreCase))
            {
                return new(
                    "04.10",
                    "하차",
                    string.Empty,
                    "하차");
            }
        }

        return null;
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Split('?', '#')[0].TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}

public sealed record DriverMobileScreenDefinition(
    string ScreenCode,
    string Title,
    string Description,
    string Badge);
