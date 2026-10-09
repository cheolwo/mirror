namespace Ssalddel.Services.Community;

/// <summary>관계 편집으로 진행·완료된 업무 사실을 주문에서 지우지 않도록 제한합니다.</summary>
public static class 주문원장연결변경Policy
{
    public static string? 잠금사유(커뮤니티원장Dto root, 커뮤니티원장Dto? child)
        => 진행사실이있는가(root)
            ? "주문이 이미 진행되었으므로 연결 변경은 해당 업무의 취소·변경 절차에서 확인해야 합니다."
            : child is null
                ? "연결 원장의 현재 상태를 확인할 수 없어 분리하거나 필수 조건을 해제할 수 없습니다."
                : 진행사실이있는가(child)
                    ? "연결 업무가 이미 진행되었으므로 수행 기록을 분리하거나 필수 조건을 해제할 수 없습니다."
                    : null;

    // 상태를 초안으로 정정해도 이미 기록한 진행·완료·종료 사실은 관계 삭제의
    // 허가가 되지 않습니다. 업무 취소·정정은 해당 업무의 별도 절차를 따릅니다.
    private static bool 진행사실이있는가(커뮤니티원장Dto ledger)
        => ledger.상태 != 커뮤니티원장상태.초안
           || ledger.상태이력.Any(item => item.상태 != 커뮤니티원장상태.초안);
}
