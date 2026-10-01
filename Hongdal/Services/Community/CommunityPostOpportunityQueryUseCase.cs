using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed class CommunityPostOpportunityQueryUseCase : ICommunityPostOpportunityQueryUseCase
{
    private readonly ICommunityPostOpportunityStore _postStore;
    private readonly ICommunityPostOpportunityAnalyzer _analyzer;
    private readonly ICommunityVoteService _voteService;
    private readonly I커뮤니티원장저장소 _ledgerStore;
    private readonly ICommunityActionJourneyService _journeyService;
    private readonly ICommunityDynamicDiscoveryService _dynamicDiscoveryService;

    public CommunityPostOpportunityQueryUseCase(
        ICommunityPostOpportunityStore postStore,
        ICommunityPostOpportunityAnalyzer analyzer,
        ICommunityVoteService voteService,
        I커뮤니티원장저장소 ledgerStore,
        ICommunityActionJourneyService journeyService,
        ICommunityDynamicDiscoveryService dynamicDiscoveryService)
    {
        _postStore = postStore;
        _analyzer = analyzer;
        _voteService = voteService;
        _ledgerStore = ledgerStore;
        _journeyService = journeyService;
        _dynamicDiscoveryService = dynamicDiscoveryService;
    }

    public async Task<CommunityPostOpportunityListResponse?> GetAsync(
        long postId,
        string? displayLanguageCode,
        CancellationToken cancellationToken = default)
    {
        var source = await _postStore.GetAsync(postId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var language = CommunityDisplayLanguageCodes.Normalize(displayLanguageCode);
        var analysis = _analyzer.Analyze(source.Title, source.Body);
        var expectedLedgerId = MeatImportReadinessCaseIds.FromCommunityPost(postId);
        var isActive = string.Equals(source.LinkedLedgerId, expectedLedgerId, StringComparison.OrdinalIgnoreCase);
        var items = !source.IsReportBoardPost && (analysis.SuggestMeatImportReadiness || isActive)
            ? new[] { CommunityPostOpportunityProjection.BuildOpportunity(source, analysis, language) }
            : [];
        var participationVote = await CommunityPostOpportunityProjection.FindParticipationVoteAsync(
            _voteService,
            postId,
            cancellationToken);
        커뮤니티원장Dto? provisionalLedger = null;
        if (!string.IsNullOrWhiteSpace(source.LinkedLedgerId))
        {
            provisionalLedger = await _ledgerStore.원장조회Async(source.LinkedLedgerId, cancellationToken);
        }

        var participation = CommunityPostOpportunityProjection.BuildParticipationEntry(
            source,
            language,
            participationVote,
            provisionalLedger);
        var journey = await _journeyService.BuildAsync(
            source,
            participation,
            participationVote,
            provisionalLedger,
            language,
            cancellationToken);
        var contextDiscovery = await _dynamicDiscoveryService.DiscoverAsync(source, null, cancellationToken);

        return new CommunityPostOpportunityListResponse
        {
            PostId = source.PostId,
            DisplayLanguageCode = language,
            ExperiencePolicy = new CommunitySharedExperiencePolicyResponse(),
            Participation = participation,
            Journey = journey,
            Items = items,
            DynamicTopics = contextDiscovery.DynamicTopics,
            ContextDiscovery = contextDiscovery
        };
    }

    public async Task<CommunityPostContextDiscoveryResponse?> GetContextDiscoveryAsync(
        long postId,
        CommunityPostContextDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await _postStore.GetAsync(postId, cancellationToken);
        return source is null
            ? null
            : await _dynamicDiscoveryService.DiscoverAsync(source, request, cancellationToken);
    }
}
