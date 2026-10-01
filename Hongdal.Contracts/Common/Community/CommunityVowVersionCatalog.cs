namespace Hongdal.Contracts.Common.Community;

public sealed record CommunityVowVersionDefinition(
    string Code,
    string DisplayName,
    string WorkflowTag,
    string Focus,
    string InheritedFoundation,
    string OperationalBoundary,
    bool IsCurrentFocus = false,
    bool IsFutureExploration = false);

public static class CommunityVowVersionCatalog
{
    public const string CurrentVersionCode = "0.0";

    public const string FutureVersionCode = "future";

    public static IReadOnlyList<CommunityVowVersionDefinition> All { get; } =
    [
        new(
            "0.0",
            "홍달 0.0",
            "홍달 0.0 · 커뮤니티 기반",
            "글에서 시작한 마음이 참여 의사, 가원장, 역할 합의와 완료 사례로 이어지는 공동체 기반",
            "첫 기반이므로 앞선 제품 버전 없이 독립적으로 실행되고 검증되어야 합니다.",
            "운송 주선·자동 배차·계약·결제는 확정하지 않고 당사자의 대화와 합의를 기록합니다.",
            IsCurrentFocus: true),
        new(
            "1.0",
            "홍달 1.0",
            "홍달 1.0 · 국내 화물·용달",
            "커뮤니티에서 확인된 국내 운송 필요를 화주, 기사와 기존 운송 사업자가 안전하게 실행하는 흐름",
            "0.0의 참여 동의, 공동 원장, 신고와 신뢰 기록을 이어받습니다.",
            "허가·제휴·운영 준비 전 플랫폼이 유상 배차나 운송 주선을 확정하지 않습니다."),
        new(
            "1.5",
            "홍달 1.5",
            "홍달 1.5 · 창고·판매 물류",
            "입고, 적재, 출고와 판매 물류가 여러 창고 역할 사이에서 이어지는 흐름",
            "0.0의 공동 원장과 1.0의 운송 인계 구조를 이어받습니다.",
            "실제 보관·재위탁·출고 책임은 계약된 창고와 운송 주체가 확인합니다."),
        new(
            "2.0",
            "홍달 2.0",
            "홍달 2.0 · 국제 물류·통관",
            "HS 코드, 수출입 자료, 관세사 보정과 국제 물류 정보를 함께 알아차리는 흐름",
            "앞선 커뮤니티, 운송과 창고 원장을 참고 정보로 연결합니다.",
            "통관 판단과 신고 대행은 자격 있는 관세사·관계 기관의 확인을 대신하지 않습니다."),
        new(
            "2.5",
            "홍달 2.5",
            "홍달 2.5 · 공동주문·공동수입",
            "여러 구매자의 수요가 공동주문, 해외 선적, 국내 입고와 배분으로 이어지는 흐름",
            "0.0의 마음 모으기와 1.5·2.0의 물류·통관 정보를 조립합니다.",
            "플랫폼은 판매자·수입자·운송사·관세사를 자동 선정하거나 계약 당사자가 되지 않습니다."),
        new(
            "3.0",
            "홍달 3.0",
            "홍달 3.0 · 음식점 배달",
            "음식점 주문이 조리, 픽업과 고객 배송까지 짧은 시간 안에 이어지는 흐름",
            "0.0의 참여 기반과 앞선 운송·역할 인계 원칙을 이어받습니다.",
            "영업 신고, 식품 안전과 배달 운영 책임은 실제 음식점과 수행 주체가 확인합니다."),
        new(
            "3.5",
            "홍달 3.5",
            "홍달 3.5 · 마트·도심 물류",
            "도심 재고, 피킹, 포장과 즉시배송이 동네 마트·시장과 연결되는 흐름",
            "0.0의 공동체 기반과 1.5 창고, 3.0 배달의 인계 원칙을 조립합니다.",
            "재고 판매·보관·배송의 책임 주체와 허용 범위를 실제 참여자가 확인합니다."),
        new(
            FutureVersionCode,
            "홍달 다음 서원",
            "홍달 이후 · 다음 서원",
            "3.5 이후에도 사람들이 새 필요를 글로 발견하고 다음 제품 방향을 함께 정하는 흐름",
            "완료된 버전의 사례와 아직 풀리지 않은 문제를 근거로 삼습니다.",
            "버전 번호와 운영 약속을 미리 확정하지 않고 탐색 서원으로 기록합니다.",
            IsFutureExploration: true)
    ];

    public static CommunityVowVersionDefinition Current
        => Find(CurrentVersionCode);

    public static CommunityVowVersionDefinition Find(string? code)
        => All.FirstOrDefault(version => string.Equals(
               version.Code,
               code?.Trim(),
               StringComparison.OrdinalIgnoreCase))
           ?? Current;

    public static CommunityVowVersionDefinition? FindByWorkflowTag(string? workflowTag)
        => All.FirstOrDefault(version => string.Equals(
            version.WorkflowTag,
            workflowTag?.Trim(),
            StringComparison.OrdinalIgnoreCase));
}
