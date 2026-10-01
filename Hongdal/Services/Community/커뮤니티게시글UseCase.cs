using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "게시글 기능별 UseCase를 기존 통합 계약으로 조립하는 호환 façade",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "기존 소비자 호환만 제공하며 각 기능의 영속 처리와 권한 판단은 기능별 UseCase에 위임합니다.")]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalUseCase(
    "커뮤니티 게시글 운영",
    Summary = "기능별 게시글 UseCase를 기존 통합 계약으로 조립합니다.")]
[HongdalUseCaseActor(HongdalActor.CommunityMember)]
[HongdalUseCaseActor(HongdalActor.PlatformOperator, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "커뮤니티투표UseCase",
    Condition = "게시글 토론이 투표, 결의문, 전자서명 필요 상태로 발전하는 경우",
    Summary = "커뮤니티 게시글을 투표와 결의문 작성 흐름으로 확장합니다.")]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "인연스냅샷조회UseCase",
    Condition = "게시글 작성자 또는 참여자의 업무 관계 신뢰 신호를 함께 보여주는 경우",
    Summary = "게시글의 역할 태그와 활동 신호를 업무 인연 스냅샷 조회로 확장합니다.")]
public sealed partial class 커뮤니티게시글UseCase : I커뮤니티게시글UseCase
{
    private readonly I커뮤니티게시글조회UseCase _readUseCase;
    private readonly I커뮤니티게시글발행UseCase _publishingUseCase;
    private readonly I커뮤니티게시글예약발행UseCase _schedulingUseCase;
    private readonly I커뮤니티게시글첨부UseCase _attachmentUseCase;
    private readonly I커뮤니티게시글참여UseCase _participationUseCase;
    private readonly I커뮤니티게시글운영UseCase _moderationUseCase;

    public 커뮤니티게시글UseCase(
        I커뮤니티게시글조회UseCase readUseCase,
        I커뮤니티게시글발행UseCase publishingUseCase,
        I커뮤니티게시글예약발행UseCase schedulingUseCase,
        I커뮤니티게시글첨부UseCase attachmentUseCase,
        I커뮤니티게시글참여UseCase participationUseCase,
        I커뮤니티게시글운영UseCase moderationUseCase)
    {
        _readUseCase = readUseCase;
        _publishingUseCase = publishingUseCase;
        _schedulingUseCase = schedulingUseCase;
        _attachmentUseCase = attachmentUseCase;
        _participationUseCase = participationUseCase;
        _moderationUseCase = moderationUseCase;
    }
}
