using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Services.Community;

public interface I커뮤니티투표UseCase
{
    Task<Result<CommunityVoteListResponse>> 목록Async(
        string? appKey,
        string? communityScope,
        string? hsCode,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResponse>> 상세Async(Guid voteId, CancellationToken cancellationToken);

    Task<Result<CommunityVoteResponse>> 생성Async(
        CommunityVoteCreateRequest? request,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResponse>> 투표Async(
        Guid voteId,
        CommunityVoteCastRequest? request,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResponse>> 마감Async(
        Guid voteId,
        CommunityVoteCloseRequest? request,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문초안생성Async(
        Guid voteId,
        CommunityVoteResolutionDraftRequest? request,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문서명Async(
        Guid voteId,
        CommunityVoteResolutionSignRequest? request,
        CancellationToken cancellationToken);

    Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문서명가능전환Async(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest? request,
        CancellationToken cancellationToken);
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Application,
    "비구속 수요 투표·참여 의사·결의문 초안과 서명 상태를 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.DomesticGroupPurchasePilot,
    Boundary = "0.0 투표는 참여 신호이며 주문·계약을 확정하지 않습니다. 자동 집단화와 수입 실행은 후속 기능 플래그 경계입니다.")]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalUseCase("커뮤니티 투표와 결의문", Summary = "커뮤니티 참여자가 투표를 만들고 참여하며, 필요한 경우 결의문 초안과 전자서명 흐름으로 연결합니다.")]
[HongdalUseCaseActor(HongdalActor.CommunityMember)]
[HongdalUseCaseActor(HongdalActor.PlatformOperator, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "문서관리UseCase",
    Condition = "투표 결과가 결의문, 전자서명, 제출 가능한 문서로 남아야 하는 경우",
    Summary = "커뮤니티 투표 결과를 문서 관리와 서명 증빙 흐름으로 확장합니다.")]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "파일업로드UseCase",
    Condition = "결의문 첨부, 서명 이미지, 증빙 파일이 함께 제출되는 경우",
    Summary = "투표와 결의문 흐름을 파일 업로드 증빙 흐름으로 확장합니다.")]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "공동구매자동집단화UseCase",
    Condition = "투표 유형이 GroupPurchaseDemand인 경우",
    Summary = "커뮤니티의 비구속 수요 투표를 상품과 배송권 기준 공동구매 자동 집단화 흐름으로 전달합니다.")]
public sealed class 커뮤니티투표UseCase : I커뮤니티투표UseCase
{
    private readonly ICommunityVoteService _voteService;

    public 커뮤니티투표UseCase(ICommunityVoteService voteService)
    {
        _voteService = voteService;
    }

    public async Task<Result<CommunityVoteListResponse>> 목록Async(
        string? appKey,
        string? communityScope,
        string? hsCode,
        CancellationToken cancellationToken)
    {
        string? normalizedHsCode;
        try
        {
            normalizedHsCode = CommunityVoteHsCode.NormalizeOptional(hsCode);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest<CommunityVoteListResponse>(ex.Message);
        }

        return Result.Ok(await _voteService.ListAsync(
            appKey,
            communityScope,
            normalizedHsCode,
            cancellationToken));
    }

    public async Task<Result<CommunityVoteResponse>> 상세Async(Guid voteId, CancellationToken cancellationToken)
    {
        var vote = await _voteService.GetAsync(voteId, cancellationToken);
        return vote is null
            ? NotFound<CommunityVoteResponse>("커뮤니티 투표를 찾을 수 없습니다.")
            : Result.Ok(vote);
    }

    public Task<Result<CommunityVoteResponse>> 생성Async(
        CommunityVoteCreateRequest? request,
        CancellationToken cancellationToken)
        => GuardRequest(request, () => _voteService.CreateAsync(request!, cancellationToken));

    public Task<Result<CommunityVoteResponse>> 투표Async(
        Guid voteId,
        CommunityVoteCastRequest? request,
        CancellationToken cancellationToken)
        => GuardNullableRequest(
            request,
            () => _voteService.CastVoteAsync(voteId, request!, cancellationToken),
            "커뮤니티 투표를 찾을 수 없습니다.");

    public Task<Result<CommunityVoteResponse>> 마감Async(
        Guid voteId,
        CommunityVoteCloseRequest? request,
        CancellationToken cancellationToken)
        => GuardNullableRequest(
            request,
            () => _voteService.CloseAsync(voteId, request!, cancellationToken),
            "커뮤니티 투표를 찾을 수 없습니다.");

    public Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문초안생성Async(
        Guid voteId,
        CommunityVoteResolutionDraftRequest? request,
        CancellationToken cancellationToken)
        => GuardNullableRequest(
            request,
            () => _voteService.CreateResolutionDraftAsync(voteId, request!, cancellationToken),
            "커뮤니티 투표를 찾을 수 없습니다.");

    public Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문서명Async(
        Guid voteId,
        CommunityVoteResolutionSignRequest? request,
        CancellationToken cancellationToken)
        => GuardNullableRequest(
            request,
            () => _voteService.SignResolutionAsync(voteId, request!, cancellationToken),
            "서명 가능한 결의문을 찾을 수 없습니다.");

    public Task<Result<CommunityVoteResolutionDocumentResponse>> 결의문서명가능전환Async(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest? request,
        CancellationToken cancellationToken)
        => GuardNullableRequest(
            request,
            () => _voteService.MarkResolutionReadyToSignAsync(voteId, request!, cancellationToken),
            "결의문을 찾을 수 없습니다.");

    private static async Task<Result<T>> GuardRequest<T>(
        object? request,
        Func<Task<T>> action)
    {
        if (request is null)
        {
            return BadRequest<T>("request body is required");
        }

        try
        {
            return Result.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest<T>(ex.Message);
        }
    }

    private static async Task<Result<T>> GuardNullableRequest<T>(
        object? request,
        Func<Task<T?>> action,
        string notFoundMessage)
        where T : class
    {
        if (request is null)
        {
            return BadRequest<T>("request body is required");
        }

        try
        {
            var result = await action();
            return result is null ? NotFound<T>(notFoundMessage) : Result.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest<T>(ex.Message);
        }
    }

    private static Result<T> BadRequest<T>(string message) => Result.Fail<T>(message);

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));
}
