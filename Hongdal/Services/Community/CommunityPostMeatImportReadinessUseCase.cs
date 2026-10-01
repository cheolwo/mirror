using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;

namespace Hongdal.Services.Community;

public sealed class CommunityPostMeatImportReadinessUseCase : ICommunityPostMeatImportReadinessUseCase
{
    private readonly ICommunityPostOpportunityStore _postStore;
    private readonly ICommunityPostOpportunityAnalyzer _analyzer;
    private readonly IMeatImportReadinessService _readinessService;

    public CommunityPostMeatImportReadinessUseCase(
        ICommunityPostOpportunityStore postStore,
        ICommunityPostOpportunityAnalyzer analyzer,
        IMeatImportReadinessService readinessService)
    {
        _postStore = postStore;
        _analyzer = analyzer;
        _readinessService = readinessService;
    }

    public async Task<StartCommunityMeatImportReadinessResponse> StartAsync(
        long postId,
        StartCommunityMeatImportReadinessRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Case);
        var actor = CommunityPostOpportunityGuard.RequireActor(actorUserId);
        if (!request.ConfirmExplicitStart || !request.ConfirmInformationOnly)
        {
            throw new InvalidOperationException("자동 전환은 하지 않습니다. 시작 의사와 정보 제공 전용 경계를 모두 명시적으로 확인해야 합니다.");
        }

        var source = await _postStore.GetAsync(postId, cancellationToken)
                     ?? throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        CommunityPostOpportunityGuard.EnsureCollectiveActionAllowed(source);
        if (string.IsNullOrWhiteSpace(source.AuthorUserId)
            || !string.Equals(source.AuthorUserId, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("게시글 작성자만 이 대화에서 준비도 원장을 시작할 수 있습니다.");
        }

        var analysis = _analyzer.Analyze(source.Title, source.Body);
        if (!analysis.SuggestMeatImportReadiness)
        {
            throw new InvalidOperationException("게시글에서 육류와 국경 간 거래 신호가 함께 확인되지 않아 이 정보 협업을 제안할 수 없습니다.");
        }

        var expectedLedgerId = MeatImportReadinessCaseIds.FromCommunityPost(postId);
        if (source.LinkedLedgerId is not null
            && !string.Equals(source.LinkedLedgerId, expectedLedgerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommunityPostOpportunityConflictException("게시글에 이미 다른 원장이 연결되어 있습니다.");
        }

        request.Case.CommunityId = string.IsNullOrWhiteSpace(request.Case.CommunityId)
            ? source.AppKey
            : request.Case.CommunityId;
        var readinessCase = await _readinessService.CreateCaseFromCommunityPostAsync(
            postId,
            request.Case,
            actor,
            actorDisplayName,
            cancellationToken);
        var linkResult = await _postStore.LinkLedgerAsync(postId, actor, readinessCase.CaseId, cancellationToken);
        if (linkResult is CommunityPostLedgerLinkResult.NotFound)
        {
            throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        }

        if (linkResult is CommunityPostLedgerLinkResult.NotOwner)
        {
            throw new UnauthorizedAccessException("게시글 작성자만 준비도 원장을 연결할 수 있습니다.");
        }

        if (linkResult is CommunityPostLedgerLinkResult.ConflictingLedger)
        {
            throw new CommunityPostOpportunityConflictException("동시에 다른 원장이 게시글에 연결되었습니다. 게시글을 다시 확인해 주세요.");
        }

        var linkedSource = source with { LinkedLedgerId = readinessCase.CaseId };
        var language = CommunityDisplayLanguageCodes.Normalize(request.DisplayLanguageCode);
        return new StartCommunityMeatImportReadinessResponse
        {
            PostId = postId,
            DisplayLanguageCode = language,
            LinkedToCommunityPost = true,
            Opportunity = CommunityPostOpportunityProjection.BuildOpportunity(linkedSource, analysis, language),
            Case = readinessCase
        };
    }
}

internal static class CommunityPostOpportunityGuard
{
    public static string RequireActor(string? actorUserId)
        => string.IsNullOrWhiteSpace(actorUserId)
            ? throw new UnauthorizedAccessException("로그인 사용자 식별자를 확인할 수 없습니다.")
            : actorUserId.Trim();

    public static void EnsureCollectiveActionAllowed(CommunityPostOpportunitySource source)
    {
        if (source.IsReportBoardPost)
        {
            throw new InvalidOperationException("신고·분쟁 게시글에서는 관심 모집, 가원장 또는 거래 역할 참여를 시작할 수 없습니다.");
        }
    }
}
