using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface I게시글원장선택조회Service
{
    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 연결가능원장목록조회Async(
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 공유원장목록조회Async(
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken);

    Task<Result<커뮤니티원장Dto>> 연결가능원장조회Async(
        string 원장Id,
        string? 사용자UserId,
        string? 업무분류,
        CancellationToken cancellationToken);
}

public interface I게시글원장표시ContextService
{
    Task<PlatformCommunityPostLedgerContextResponse?> 조회Async(
        string? 원장Id,
        string? 사용자UserId,
        CancellationToken cancellationToken);

    Task<PlatformCommunityPostLedgerContextResponse?> 비식별성립사례조회Async(
        string? 원장Id,
        CancellationToken cancellationToken);
}

public interface I게시글원장ContextService :
    I게시글원장선택조회Service,
    I게시글원장표시ContextService
{
}
