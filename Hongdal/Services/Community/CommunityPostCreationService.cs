using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Application.Community;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using MediatR;
using 홍달.Data;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "즉시·예약 게시글이 공유하는 검증, 원장 연결, 영속 저장과 후속 작업 예약 파이프라인",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "호출자가 지정한 즉시 또는 예약 발행 시점만 반영하며 예약 취소와 기존 게시글 수정은 수행하지 않습니다.")]
public sealed class 커뮤니티게시글생성Service
{
    private readonly HongdalContext _db;
    private readonly I커뮤니티게시글음성작업예약Service _audioQueue;
    private readonly ICommunityKeywordNotificationQueue _keywordQueue;
    private readonly I게시글원장선택조회Service _ledgerSelectionService;
    private readonly I게시글원장표시ContextService _ledgerContextService;
    private readonly ICommunityBoardWritePolicy _boardWritePolicy;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPublisher _publisher;
    private readonly ILogger<커뮤니티게시글생성Service> _logger;

    public 커뮤니티게시글생성Service(
        HongdalContext db,
        I커뮤니티게시글음성작업예약Service audioQueue,
        ICommunityKeywordNotificationQueue keywordQueue,
        I게시글원장선택조회Service ledgerSelectionService,
        I게시글원장표시ContextService ledgerContextService,
        ICommunityBoardWritePolicy boardWritePolicy,
        ICurrentUserAccessor currentUserAccessor,
        IPublisher publisher,
        ILogger<커뮤니티게시글생성Service> logger)
    {
        _db = db;
        _audioQueue = audioQueue;
        _keywordQueue = keywordQueue;
        _ledgerSelectionService = ledgerSelectionService;
        _ledgerContextService = ledgerContextService;
        _boardWritePolicy = boardWritePolicy;
        _currentUserAccessor = currentUserAccessor;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Result<PlatformCommunityPostResponse>> CreateAsync(
        PlatformCommunityPostCreateRequest? request,
        DateTime? scheduledPublishAtUtc,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail<PlatformCommunityPostResponse>("request body is required");
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
            return Result.Fail<PlatformCommunityPostResponse>(validation);
        }

        var countryValidation = CommunityPostWritePolicy.ValidateAuthorDisplayCountry(
            request.IsAuthorDisplayCountryPublic,
            request.AuthorDisplayCountryCode,
            request.AuthorDisplayCountryName);
        if (countryValidation is not null)
        {
            return Result.Fail<PlatformCommunityPostResponse>(countryValidation);
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

        var appKey = CommunityPostWritePolicy.Normalize(request.AppKey, "platform", 80);
        if (!await _boardWritePolicy.CanWriteAsync(
                appKey,
                category,
                _currentUserAccessor.UserId,
                cancellationToken))
        {
            return CommunityPostWritePolicy.WriteRejected<PlatformCommunityPostResponse>(
                category,
                _currentUserAccessor.UserId);
        }

        var now = DateTime.UtcNow;
        var isReportPost = request.SalesOffer is null
                           && (request.IsReportBoardPost
                               || CommunityPostWritePolicy.IsReportCategory(category));
        var nickname = CommunityPostingIdentityPolicy.ResolveNickname(
            category,
            request.Nickname,
            null,
            _currentUserAccessor.UserId);
        var title = CommunityPostWritePolicy.Normalize(request.Title, string.Empty, 160);
        var body = CommunityPostWritePolicy.Normalize(request.Body, string.Empty, 4000);
        var entity = new PlatformCommunityPost
        {
            AppKey = appKey,
            Category = category,
            WorkflowTag = CommunityPostWritePolicy.Normalize(
                request.WorkflowTag,
                "국내 화물 운송",
                60),
            RoleTag = CommunityPostWritePolicy.Normalize(request.RoleTag, "플랫폼 구성원", 40),
            Title = title,
            Body = body,
            OriginalLanguageCode = CommunityPostLanguageResolver.Resolve(
                request.OriginalLanguageCode,
                title,
                body),
            SharedLinkUrl = CommunityPostWritePolicy.NormalizeOptionalUrl(request.SharedLinkUrl),
            SalesOfferJson = CommunityPostWritePolicy.SerializeSalesOffer(request.SalesOffer),
            커뮤니티원장Id = linkedLedger?.원장Id,
            AuthorUserId = CommunityPostWritePolicy.NormalizeOptional(
                _currentUserAccessor.UserId,
                450),
            Nickname = nickname,
            IsAuthorDisplayCountryPublic = !isReportPost && request.IsAuthorDisplayCountryPublic,
            AuthorDisplayCountryCode = !isReportPost && request.IsAuthorDisplayCountryPublic
                ? CommunityPostWritePolicy.NormalizeCountryCode(request.AuthorDisplayCountryCode)
                : null,
            AuthorDisplayCountryName = !isReportPost && request.IsAuthorDisplayCountryPublic
                ? CommunityPostWritePolicy.Normalize(
                    request.AuthorDisplayCountryName,
                    string.Empty,
                    80)
                : null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            IsReportBoardPost = isReportPost,
            ReporterDisplayName = isReportPost
                ? CommunityPostWritePolicy.Normalize(request.ReporterDisplayName, nickname, 40)
                : null,
            ReportedDisplayName = isReportPost
                ? CommunityPostWritePolicy.Normalize(request.ReportedDisplayName, string.Empty, 40)
                : null,
            PublicationStatusCode = scheduledPublishAtUtc.HasValue
                ? PlatformCommunityPostPublicationStatusCodes.Scheduled
                : PlatformCommunityPostPublicationStatusCodes.Published,
            ScheduledPublishAtUtc = scheduledPublishAtUtc,
            PublishedAtUtc = scheduledPublishAtUtc.HasValue ? null : now,
            PublicationNextAttemptAtUtc = scheduledPublishAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PlatformCommunityPosts.Add(entity);
        if (!scheduledPublishAtUtc.HasValue)
        {
            _audioQueue.예약(entity, now);
            _keywordQueue.Enqueue(entity, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (!scheduledPublishAtUtc.HasValue)
        {
            try
            {
                await _publisher.Publish(new 커뮤니티게시글등록됨Event(entity.Id), cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "게시글 등록 후속 이벤트 발행에 실패했습니다. 음성 및 키워드 작업은 DB 대기열에서 복구됩니다. PostId={PostId}",
                    entity.Id);
            }
        }

        var ledgerContext = linkedLedger is null
            ? null
            : await _ledgerContextService.조회Async(
                linkedLedger.원장Id,
                _currentUserAccessor.UserId,
                cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToResponse(entity, ledgerContext));
    }
}
