namespace Hongdal.Services.Community;

public sealed class InMemoryCommunityVoteService : CommunityVoteService
{
    public InMemoryCommunityVoteService(
        ICommunityGroupPurchaseDemandHandoff? groupPurchaseDemandHandoff = null,
        string? operatingMarketCountryCode = null)
        : this(
            new InMemoryCommunityVoteStore(),
            groupPurchaseDemandHandoff ?? new NoOpCommunityGroupPurchaseDemandHandoff(),
            operatingMarketCountryCode)
    {
    }

    private InMemoryCommunityVoteService(
        InMemoryCommunityVoteStore store,
        ICommunityGroupPurchaseDemandHandoff handoff,
        string? operatingMarketCountryCode)
        : this(
            store,
            new CommunityGroupPurchaseDemandOutboxProcessor(
                store,
                handoff,
                retryBaseDelay: TimeSpan.Zero),
            operatingMarketCountryCode)
    {
    }

    private InMemoryCommunityVoteService(
        InMemoryCommunityVoteStore store,
        ICommunityGroupPurchaseDemandOutboxProcessor processor,
        string? operatingMarketCountryCode)
        : base(
            store,
            processor,
            operatingMarketCountryCode: operatingMarketCountryCode)
    {
        DemandOutboxProcessor = processor;
    }

    private ICommunityGroupPurchaseDemandOutboxProcessor DemandOutboxProcessor { get; }

    public Task<bool> ProcessPendingDemandHandoffAsync(CancellationToken cancellationToken = default) =>
        DemandOutboxProcessor.ProcessNextAsync(cancellationToken);
}
