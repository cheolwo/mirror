using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "게시글 언어 판정과 음성 본문 분할처럼 저장소와 무관한 커뮤니티 콘텐츠 규칙을 제공",
    ReleaseStage = HongdalCommunityV0ReleaseStages.IndependentExecution,
    Boundary = "DB·외부 음성 API·background worker를 참조하지 않고 입력을 판정하거나 변환한 결과만 반환합니다.")]
public static class CommunityContentApplicationModule
{
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Application,
    "기사 공개 참여와 비구속적 문의 상태를 커뮤니티 참여 규칙으로 관리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.IndependentExecution,
    Boundary = "공개 정보와 의향만 관리하며 배차·운송계약·운임 수취 또는 화물 주선을 확정하지 않습니다.")]
public static class CommunityParticipationApplicationModule
{
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Application,
    "게시글의 문맥에서 공동행동 기회를 판정하고 가원장 연결 포트를 정의",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "분석기는 후보 신호만 반환하며, 게시글·원장 저장과 상태 확정은 서버 UseCase가 수행합니다.")]
public static class CommunityOpportunityApplicationModule
{
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ledger,
    HongdalModuleKind.Application,
    "주문 원장 구성과 원장 노드에서 허용할 행동을 영속 계층과 분리해 판정",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "정책은 후보와 검증 결과만 반환하고 실제 원장 저장·상태 전이·업무 API 호출은 서버 UseCase가 수행합니다.")]
public static class CommunityLedgerApplicationModule
{
}
