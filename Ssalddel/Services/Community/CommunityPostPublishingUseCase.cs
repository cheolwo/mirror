using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;

namespace Ssalddel.Services.Community;

[SsalddelCommunityV0Module(
    SsalddelCommunityV0ModuleKeys.Content,
    SsalddelModuleKind.Application,
    "즉시 게시글 생성과 로그인 작성자·관리자·익명 비밀번호 권한에 의한 수정·삭제를 처리",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.Persistence,
    Boundary = "게시글 작성자의 명시적 요청만 반영하며 예약 발행 관리와 운영자 심의 상태는 변경하지 않습니다.")]
public sealed class 커뮤니티게시글발행UseCase : I커뮤니티게시글발행UseCase
{
    private readonly 커뮤니티게시글생성Service _creationService;
    private readonly SsalddelContext _db;
    private readonly I게시글원장선택조회Service _ledgerSelectionService;
    private readonly I게시글원장표시ContextService _ledgerContextService;
    private readonly ICommunityBoardWritePolicy _boardWritePolicy;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I생활교류공개지역Source? _publicRegions;
    private readonly Ssalddel.Services.Commerce.I통신판매거래Guard? _commerce;

    public 커뮤니티게시글발행UseCase(
        커뮤니티게시글생성Service creationService,
        SsalddelContext db,
        I게시글원장선택조회Service ledgerSelectionService,
        I게시글원장표시ContextService ledgerContextService,
        ICommunityBoardWritePolicy boardWritePolicy,
        ICurrentUserAccessor currentUserAccessor,
        I생활교류공개지역Source? publicRegions = null,
        Ssalddel.Services.Commerce.I통신판매거래Guard? commerce = null)
    {
        _creationService = creationService;
        _db = db;
        _ledgerSelectionService = ledgerSelectionService;
        _ledgerContextService = ledgerContextService;
        _boardWritePolicy = boardWritePolicy;
        _currentUserAccessor = currentUserAccessor;
        _publicRegions = publicRegions;
        _commerce = commerce;
    }

    public Task<Result<PlatformCommunityPostResponse>> 생성Async(
        PlatformCommunityPostCreateRequest? request,
        CancellationToken cancellationToken)
        => _creationService.CreateAsync(request, null, cancellationToken);

    public async Task<Result<PlatformCommunityPostResponse>> 수정Async(
        long id,
        PlatformCommunityPostUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest<PlatformCommunityPostResponse>("request body is required");
        }

        var category = CommunityPostWritePolicy.ResolvePostCategory(
            request.Category,
            request.SalesOffer);
        var requiresNickname = CommunityPostingIdentityPolicy.RequiresSuppliedNickname(
            category,
            _currentUserAccessor.UserId);
        var validation = CommunityPostWritePolicy.ValidatePost(
            request.Nickname,
            request.Password,
            request.Title,
            request.Body,
            request.SharedLinkUrl,
            request.SalesOffer,
            requiresNickname);
        if (validation is not null)
        {
            return BadRequest<PlatformCommunityPostResponse>(validation);
        }

        var countryValidation = CommunityPostWritePolicy.ValidateAuthorDisplayCountry(
            request.IsAuthorDisplayCountryPublic,
            request.AuthorDisplayCountryCode,
            request.AuthorDisplayCountryName);
        if (countryValidation is not null)
        {
            return BadRequest<PlatformCommunityPostResponse>(countryValidation);
        }

        var entity = await _db.PlatformCommunityPosts
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound<PlatformCommunityPostResponse>("게시글을 찾을 수 없습니다.");
        }

        if (CommunityLedgerCompletionPublication.IsSystemPost(entity))
        {
            return Forbidden<PlatformCommunityPostResponse>("원장 성립 시스템 기록은 수정할 수 없습니다.");
        }

        var mutationCapabilities = CommunityPostMutationAccessPolicy.Resolve(
            entity,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role);
        if (!mutationCapabilities.CanEdit)
        {
            return Forbidden<PlatformCommunityPostResponse>("작성자만 이 게시글을 수정할 수 있습니다.");
        }

        if (mutationCapabilities.EditRequiresPassword
            && !BCrypt.Net.BCrypt.Verify(request.Password.Trim(), entity.PasswordHash))
        {
            return Forbidden<PlatformCommunityPostResponse>("게시글 비밀번호가 일치하지 않습니다.");
        }

        var regionValidation = await NeighborhoodExchangeRegionSelectionPolicy.ValidateAsync(
            request.PublicNeighborhoodRegionKey, category, request.WorkflowTag, request.RoleTag,
            request.IsReportBoardPost, _publicRegions, cancellationToken);
        if (regionValidation is not null)
        {
            return BadRequest<PlatformCommunityPostResponse>(regionValidation);
        }

        커뮤니티원장Dto? linkedLedger = null;
        if (!string.IsNullOrWhiteSpace(request.커뮤니티원장Id))
        {
            var ledgerResult = await _ledgerSelectionService.연결가능원장조회Async(
                request.커뮤니티원장Id,
                _currentUserAccessor.UserId,
                request.WorkflowTag,
                cancellationToken);
            if (ledgerResult.IsFailed)
            {
                return Result.Fail<PlatformCommunityPostResponse>(ledgerResult.Errors);
            }

            linkedLedger = ledgerResult.Value;
        }

        if (!await _boardWritePolicy.CanWriteAsync(
                entity.AppKey,
                category,
                _currentUserAccessor.UserId,
                cancellationToken))
        {
            return CommunityPostWritePolicy.WriteRejected<PlatformCommunityPostResponse>(
                category,
                _currentUserAccessor.UserId);
        }

        // 기존 게시글의 판매 종료·철회는 새 거래 자격과 무관하게 처리합니다.
        if (request.SalesOffer is not null && request.SalesOffer.Status == PlatformCommunitySalesOfferStatuses.Open && _commerce is not null)
        {
            try { await _commerce.요구Async(_currentUserAccessor.UserId ?? string.Empty, entity.AuthorUserId,
                request.CommerceProtection, "sales-offer-update", Guid.NewGuid().ToString("N"), cancellationToken); }
            catch (Ssalddel.Services.Commerce.거래보호Exception ex)
            { return Result.Fail<PlatformCommunityPostResponse>(new Error(ex.Message).WithMetadata("errorCode", ex.Code).WithMetadata("statusCode", ex.Status)); }
        }
        entity.Category = category;
        var isReportPost = request.SalesOffer is null
                           && (request.IsReportBoardPost
                               || CommunityPostWritePolicy.IsReportCategory(category));
        entity.WorkflowTag = CommunityPostWritePolicy.Normalize(
            request.WorkflowTag,
            "국내 화물 운송",
            60);
        entity.RoleTag = CommunityPostWritePolicy.Normalize(request.RoleTag, "플랫폼 구성원", 40);
        entity.PublicNeighborhoodRegionKey = NeighborhoodExchangeRegionSelectionPolicy.Normalize(request.PublicNeighborhoodRegionKey);
        entity.Title = CommunityPostWritePolicy.Normalize(request.Title, string.Empty, 160);
        entity.Body = CommunityPostWritePolicy.Normalize(request.Body, string.Empty, 4000);
        entity.OriginalLanguageCode = CommunityPostLanguageResolver.Resolve(
            request.OriginalLanguageCode,
            entity.Title,
            entity.Body);
        entity.SharedLinkUrl = CommunityPostWritePolicy.NormalizeOptionalUrl(request.SharedLinkUrl);
        entity.SalesOfferJson = CommunityPostWritePolicy.SerializeSalesOffer(request.SalesOffer);
        entity.IsInterestGatheringEnabled = CommunityPostInterestGatheringPolicy.ResolveEnabled(
            category,
            request.IsInterestGatheringEnabled);
        if (request.커뮤니티원장Id is not null)
        {
            entity.커뮤니티원장Id = linkedLedger?.원장Id;
        }

        entity.Nickname = CommunityPostingIdentityPolicy.ResolveNickname(
            category,
            request.Nickname,
            entity.Nickname,
            _currentUserAccessor.UserId);
        entity.IsReportBoardPost = isReportPost;
        entity.IsAuthorDisplayCountryPublic = !isReportPost && request.IsAuthorDisplayCountryPublic;
        entity.AuthorDisplayCountryCode = entity.IsAuthorDisplayCountryPublic
            ? CommunityPostWritePolicy.NormalizeCountryCode(request.AuthorDisplayCountryCode)
            : null;
        entity.AuthorDisplayCountryName = entity.IsAuthorDisplayCountryPublic
            ? CommunityPostWritePolicy.Normalize(
                request.AuthorDisplayCountryName,
                string.Empty,
                80)
            : null;
        entity.ReporterDisplayName = isReportPost
            ? CommunityPostWritePolicy.Normalize(request.ReporterDisplayName, entity.Nickname, 40)
            : null;
        entity.ReportedDisplayName = isReportPost
            ? CommunityPostWritePolicy.Normalize(request.ReportedDisplayName, string.Empty, 40)
            : null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        var ledgerContext = await _ledgerContextService.조회Async(
            entity.커뮤니티원장Id,
            _currentUserAccessor.UserId,
            cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToResponse(
            entity,
            ledgerContext,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role));
    }

    public async Task<Result> 삭제Async(
        long id,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.PlatformCommunityPosts
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound("게시글을 찾을 수 없습니다.");
        }

        if (CommunityLedgerCompletionPublication.IsSystemPost(entity))
        {
            return Forbidden("원장 성립 시스템 기록은 삭제할 수 없습니다.");
        }

        var mutationCapabilities = CommunityPostMutationAccessPolicy.Resolve(
            entity,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role);
        if (!mutationCapabilities.CanDelete)
        {
            return Forbidden("관리자나 작성자만 이 게시글을 삭제할 수 있습니다.");
        }

        if (mutationCapabilities.DeleteRequiresPassword
            && string.IsNullOrWhiteSpace(request?.Password))
        {
            return BadRequest("익명 게시글을 삭제하려면 작성 시 비밀번호를 입력해야 합니다.");
        }

        if (mutationCapabilities.DeleteRequiresPassword
            && !BCrypt.Net.BCrypt.Verify(request!.Password.Trim(), entity.PasswordHash))
        {
            return Forbidden("게시글 비밀번호가 일치하지 않습니다.");
        }

        entity.IsDeleted = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static Result<T> BadRequest<T>(string message) => Result.Fail<T>(message);
    private static Result BadRequest(string message) => Result.Fail(message);

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result NotFound(string message)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result<T> Forbidden<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status403Forbidden));

    private static Result Forbidden(string message)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", StatusCodes.Status403Forbidden));
}
