using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

/// <summary>
/// 기존 API와 client 계약을 유지하기 위한 호환 façade입니다.
/// 새로운 소비자는 조회·참여·전문 역할·수입 준비의 좁은 UseCase를 직접 사용합니다.
/// </summary>
public sealed class CommunityPostOpportunityService : ICommunityPostOpportunityService
{
    private readonly ICommunityPostOpportunityQueryUseCase _queryUseCase;
    private readonly ICommunityPostParticipationUseCase _participationUseCase;
    private readonly ICommunityPostProfessionalParticipationService _professionalParticipationService;
    private readonly ICommunityPostMeatImportReadinessUseCase _meatImportReadinessUseCase;

    public CommunityPostOpportunityService(
        ICommunityPostOpportunityQueryUseCase queryUseCase,
        ICommunityPostParticipationUseCase participationUseCase,
        ICommunityPostProfessionalParticipationService professionalParticipationService,
        ICommunityPostMeatImportReadinessUseCase meatImportReadinessUseCase)
    {
        _queryUseCase = queryUseCase;
        _participationUseCase = participationUseCase;
        _professionalParticipationService = professionalParticipationService;
        _meatImportReadinessUseCase = meatImportReadinessUseCase;
    }

    public Task<CommunityPostOpportunityListResponse?> GetAsync(
        long postId,
        string? displayLanguageCode,
        CancellationToken cancellationToken = default)
        => _queryUseCase.GetAsync(postId, displayLanguageCode, cancellationToken);

    public Task<CommunityPostContextDiscoveryResponse?> GetContextDiscoveryAsync(
        long postId,
        CommunityPostContextDiscoveryRequest request,
        CancellationToken cancellationToken = default)
        => _queryUseCase.GetContextDiscoveryAsync(postId, request, cancellationToken);

    public Task<StartCommunityPostParticipationResponse> StartParticipationAsync(
        long postId,
        StartCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => _participationUseCase.StartParticipationAsync(
            postId,
            request,
            actorUserId,
            actorDisplayName,
            cancellationToken);

    public Task<PromoteCommunityPostParticipationResponse> PromoteParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => _participationUseCase.PromoteParticipationAsync(
            postId,
            request,
            actorUserId,
            actorDisplayName,
            cancellationToken);

    public Task<JoinCommunityPostProfessionalResponse> JoinProfessionalAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => _professionalParticipationService.JoinAsync(
            postId,
            request,
            actorUserId,
            actorDisplayName,
            cancellationToken);

    public Task<JoinCommunityPostPartyRoleResponse> JoinPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => _professionalParticipationService.JoinPartyRoleAsync(
            postId,
            request,
            actorUserId,
            actorDisplayName,
            cancellationToken);

    public Task<StartCommunityMeatImportReadinessResponse> StartMeatImportReadinessAsync(
        long postId,
        StartCommunityMeatImportReadinessRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => _meatImportReadinessUseCase.StartAsync(
            postId,
            request,
            actorUserId,
            actorDisplayName,
            cancellationToken);
}
