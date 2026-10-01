using FluentResults;
using Hongdal.Contracts.Common.Community;
using 홍달.Services.Versioning;

namespace Hongdal.Services.Community;

public sealed class 게시글원장ContextService : I게시글원장ContextService
{
    private readonly I게시글원장선택조회Service _선택조회Service;
    private readonly I게시글원장표시ContextService _표시ContextService;

    public 게시글원장ContextService(
        I게시글원장선택조회Service 선택조회Service,
        I게시글원장표시ContextService 표시ContextService)
    {
        _선택조회Service = 선택조회Service;
        _표시ContextService = 표시ContextService;
    }

    public 게시글원장ContextService(
        I커뮤니티원장저장소 원장저장소,
        IVersionFeatureFlagService featureFlagService,
        I커뮤니티원장공유Service 공유Service)
        : this(
            new 게시글원장선택조회Service(원장저장소, featureFlagService, 공유Service),
            new 게시글원장표시ContextService(원장저장소, featureFlagService, 공유Service))
    {
    }

    public 게시글원장ContextService(
        I커뮤니티원장저장소 원장저장소,
        IVersionFeatureFlagService featureFlagService,
        I커뮤니티원장공유Service 공유Service,
        ICommunityLedgerRoleAccessService? roleAccessService)
        : this(
            new 게시글원장선택조회Service(원장저장소, featureFlagService, 공유Service),
            new 게시글원장표시ContextService(
                원장저장소,
                featureFlagService,
                공유Service,
                roleAccessService))
    {
    }

    public Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 연결가능원장목록조회Async(
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken)
        => _선택조회Service.연결가능원장목록조회Async(
            사용자UserId,
            업무분류,
            cancellationToken);

    public Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 공유원장목록조회Async(
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken)
        => _선택조회Service.공유원장목록조회Async(
            사용자UserId,
            업무분류,
            cancellationToken);

    public Task<Result<커뮤니티원장Dto>> 연결가능원장조회Async(
        string 원장Id,
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken)
        => _선택조회Service.연결가능원장조회Async(
            원장Id,
            사용자UserId,
            업무분류,
            cancellationToken);

    public Task<PlatformCommunityPostLedgerContextResponse?> 조회Async(
        string? 원장Id,
        string? 사용자UserId,
        CancellationToken cancellationToken)
        => _표시ContextService.조회Async(원장Id, 사용자UserId, cancellationToken);

    public Task<PlatformCommunityPostLedgerContextResponse?> 비식별성립사례조회Async(
        string? 원장Id,
        CancellationToken cancellationToken)
        => _표시ContextService.비식별성립사례조회Async(원장Id, cancellationToken);
}
