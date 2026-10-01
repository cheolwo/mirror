using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Models;

public sealed record BaguaRoleTransitionStep(
    int Number,
    string Title,
    string Description);

public sealed record BaguaRoleTransitionAnimationModel(
    string AssetSlotKey,
    string MotionKind,
    string Title,
    string Storyboard,
    string PayloadSymbol,
    string AccentColor,
    int DurationMilliseconds,
    string CreativeBrief,
    IReadOnlyList<string> ContributorDisciplines);

public sealed record BaguaRoleTransitionPageModel(
    BaguaActorRoleDefinition Role,
    BaguaBusinessAreaDefinition SourceArea,
    BaguaBusinessAreaDefinition TargetArea,
    BaguaTransitionDefinition Transition,
    BaguaRoleTransitionPerspectiveDefinition Perspective,
    BaguaRoleTransitionAnimationModel Animation,
    IReadOnlyList<BaguaRoleTransitionStep> Steps,
    string TargetWorkspaceName,
    string TargetWorkspaceHref,
    string PermissionNotice);

public static class BaguaRoleTransitionRoutes
{
    public const string BasePath = "/community/bagua";

    public static string Build(
        string roleCode,
        string sourceTrigramKey,
        string targetTrigramKey)
    {
        var perspective = BaguaTransitionCatalog.FindPerspective(
            roleCode,
            sourceTrigramKey,
            targetTrigramKey);

        return $"{BasePath}/{perspective.RoleCode}/{perspective.SourceTrigramKey}/{perspective.TargetTrigramKey}";
    }

    public static string BuildRolePicker(string sourceTrigramKey, string targetTrigramKey)
    {
        var transition = BaguaTransitionCatalog.Find(sourceTrigramKey, targetTrigramKey);
        return $"{BasePath}/{transition.SourceTrigramKey}/{transition.TargetTrigramKey}";
    }
}

public static class BaguaRoleTransitionPageCatalog
{
    public const string PermissionNotice =
        "이 역할은 화면을 해석하는 관점입니다. 조회·수정·투표·이의 제기·전자서명 권한은 로그인 사용자, 원장 참여 여부, 원장 역할, 현재 절차 상태와 서버 권한을 다시 확인해 결정합니다.";

    public static BaguaRoleTransitionPageModel Build(
        string roleCode,
        string sourceTrigramKey,
        string targetTrigramKey)
    {
        var role = BaguaTransitionCatalog.FindRole(roleCode);
        var source = BaguaTransitionCatalog.FindArea(sourceTrigramKey);
        var target = BaguaTransitionCatalog.FindArea(targetTrigramKey);
        var transition = BaguaTransitionCatalog.Find(sourceTrigramKey, targetTrigramKey);
        var perspective = BaguaTransitionCatalog.FindPerspective(
            roleCode,
            sourceTrigramKey,
            targetTrigramKey);
        var workspace = ResolveDefaultTargetWorkspace(transition.TargetBusinessCode);

        return new BaguaRoleTransitionPageModel(
            role,
            source,
            target,
            transition,
            perspective,
            BuildAnimation(role, source, target, transition, perspective),
            BuildSteps(transition, perspective),
            workspace.Name,
            workspace.Href,
            PermissionNotice);
    }

    private static BaguaRoleTransitionAnimationModel BuildAnimation(
        BaguaActorRoleDefinition role,
        BaguaBusinessAreaDefinition source,
        BaguaBusinessAreaDefinition target,
        BaguaTransitionDefinition transition,
        BaguaRoleTransitionPerspectiveDefinition perspective)
    {
        var motionKind = transition.WorkflowKind switch
        {
            BaguaTransitionWorkflowKinds.Home => "organize",
            BaguaTransitionWorkflowKinds.Conversion => "transform",
            BaguaTransitionWorkflowKinds.Handoff => "relay",
            BaguaTransitionWorkflowKinds.Governance => "gather",
            BaguaTransitionWorkflowKinds.Execution => "launch",
            BaguaTransitionWorkflowKinds.Result => "result",
            BaguaTransitionWorkflowKinds.Return => "return",
            _ => "journey"
        };
        var payloadSymbol = transition.WorkflowKind switch
        {
            BaguaTransitionWorkflowKinds.Governance => "✍",
            BaguaTransitionWorkflowKinds.Execution => "✓",
            BaguaTransitionWorkflowKinds.Return => "↩",
            _ => target.BusinessCode switch
            {
                BaguaBusinessCodes.Order => "▤",
                BaguaBusinessCodes.Sales => "₩",
                BaguaBusinessCodes.Warehouse => "□",
                BaguaBusinessCodes.Transport => "➜",
                _ => "●"
            }
        };
        var accentColor = role.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => "#2563eb",
            BaguaActorRoleCodes.Seller => "#ea580c",
            BaguaActorRoleCodes.WarehouseManager => "#15803d",
            BaguaActorRoleCodes.TransportOperator => "#7c3aed",
            _ => "#b7791f"
        };

        return new BaguaRoleTransitionAnimationModel(
            $"bagua-motion:{role.RoleCode}:{transition.TransitionKey}",
            motionKind,
            $"{source.BusinessName}에서 {target.BusinessName}으로 움직이는 {role.RoleName}",
            $"{role.RoleName} 캐릭터가 {source.TrigramName}에서 출발해 {perspective.PrimaryAction}는 뜻을 담아 {target.TrigramName}에 도착합니다.",
            payloadSymbol,
            accentColor,
            5200,
            $"{transition.PageTitle}의 {role.RoleName} 관점을 6~10초 분량의 작은 반복 장면으로 표현합니다. 캐릭터의 이동, 전달 물품, 도착 반응은 업무 의미를 해치지 않는 범위에서 교체할 수 있습니다.",
            ["캐릭터 애니메이션", "모션 그래픽", "SVG·Lottie 제작", "선택형 효과음 디자인"]);
    }

    private static IReadOnlyList<BaguaRoleTransitionStep> BuildSteps(
        BaguaTransitionDefinition transition,
        BaguaRoleTransitionPerspectiveDefinition perspective)
        => transition.WorkflowKind switch
        {
            BaguaTransitionWorkflowKinds.Governance =>
            [
                new(1, "안건 제안", "공동으로 결정할 품목, 수량, 가격, 일정과 장소를 제안합니다."),
                new(2, "참여·수요 확인", "참여자 범위와 수요를 모으고 의결 가능한 상태인지 확인합니다."),
                new(3, "이의 검토", "단계별 이의와 변경 요청을 기록하고 수정안을 비교합니다."),
                new(4, "확정안 작성", "검토 결과를 반영해 실행 가능한 최종 결의안을 작성합니다."),
                new(5, "전자서명", "필수 구성원의 본인 확인과 전자서명 완료 여부를 확인합니다."),
                new(6, "실행 연결", $"확정된 결의를 다음 업무로 넘깁니다. {perspective.PrimaryAction}")
            ],
            BaguaTransitionWorkflowKinds.Execution =>
            [
                new(1, "확정 기록 확인", "투표, 이의 검토와 전자서명이 끝난 결의인지 확인합니다."),
                new(2, "실행 항목 구성", perspective.PrimaryAction),
                new(3, "담당 업무 배정", "확정된 조건을 대상 업무의 담당자와 처리 일정에 연결합니다."),
                new(4, "원장 연결", "실행 결과가 원래 합의 원장에 돌아오도록 추적 관계를 남깁니다.")
            ],
            BaguaTransitionWorkflowKinds.Handoff =>
            [
                new(1, "대상 선택", "넘길 주문, 판매 건, 재고 또는 운송 건을 선택합니다."),
                new(2, "인계 조건 확인", perspective.PrimaryAction),
                new(3, "담당자 인수", "대상 업무 담당자가 수량, 일정, 장소와 증빙을 확인합니다."),
                new(4, "상태 추적", "인수 이후 처리 상태와 예외를 출발 업무에 다시 표시합니다.")
            ],
            BaguaTransitionWorkflowKinds.Conversion =>
            [
                new(1, "원본 선택", "변환할 업무 기록과 적용할 범위를 선택합니다."),
                new(2, "조건 변환", perspective.PrimaryAction),
                new(3, "변환안 확인", "누락된 수량, 가격, 재고, 일정과 장소가 없는지 검토합니다."),
                new(4, "대상 업무 생성", "확인된 내용을 대상 업무의 새 기록으로 만들고 원본과 연결합니다.")
            ],
            BaguaTransitionWorkflowKinds.Result =>
            [
                new(1, "결과·증빙 확인", "완료 상태와 인수, 비용 또는 예외 증빙을 확인합니다."),
                new(2, "업무 결과 반영", perspective.PrimaryAction),
                new(3, "예외 처리", "실패, 지연, 변경 건을 재요청·취소·정산 후보로 분류합니다."),
                new(4, "당사자 통지", "반영된 결과와 다음 행동을 관련 참여자에게 알립니다.")
            ],
            BaguaTransitionWorkflowKinds.Return =>
            [
                new(1, "회수 요청", "반품 사유, 물품 상태, 회수 장소와 시간을 확인합니다."),
                new(2, "회수·운송", perspective.PrimaryAction),
                new(3, "창고 검수", "회수 물품의 수량과 상태를 확인해 재입고 가능 여부를 판정합니다."),
                new(4, "재고·정산 반영", "검수 결과를 재고, 교환, 환불과 비용 기록에 반영합니다.")
            ],
            _ =>
            [
                new(1, "현황 모으기", "내가 참여하거나 담당하는 업무와 최근 변경을 모아 봅니다."),
                new(2, "역할별 우선순위", perspective.PrimaryAction),
                new(3, "다음 작업 선택", "지금 처리할 항목 하나를 선택해 해당 업무 공간으로 이동합니다."),
                new(4, "결과 확인", "완료·대기·이의·예외 상태를 확인하고 후속 행동을 정합니다.")
            ]
        };

    internal static (string Name, string Href) ResolveDefaultTargetWorkspace(string businessCode)
        => businessCode switch
        {
            BaguaBusinessCodes.Order => ("주문 업무", "/shipper/sales/orders"),
            BaguaBusinessCodes.Sales => ("판매 업무", "/shipper/sales/listings"),
            BaguaBusinessCodes.Warehouse => ("창고 업무", "/shipper/warehouse/workspace"),
            BaguaBusinessCodes.Transport => ("운송 업무", "/shipper/transport"),
            _ => ("공동구매 합의", "/community/group-purchase")
        };
}
