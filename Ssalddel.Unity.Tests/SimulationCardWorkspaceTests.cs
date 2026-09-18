using Ssalddel.Unity.Cards;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationCardWorkspaceTests
{
    [Fact]
    public async Task LoadAsync_MergesMeaningLayersWithoutGrantingAuthority()
    {
        var coordinator = new CardWorkspaceCoordinator(new ICardFamilySource[]
        {
            Source(CardFamilyCodes.Tarot, CardHierarchyTierCodes.Meta,
                CardAuthorityCodes.ServerMutable, "tarot:tower", "탑",
                CardMetaLayerCodes.ActiveMajorArcana),
            Source(CardFamilyCodes.TeamRole, CardHierarchyTierCodes.Action,
                CardAuthorityCodes.ServerMutable, "role:defend", "Farm Gate 방어"),
        });

        var result = await coordinator.LoadAsync();

        Assert.True(result.PresentationOnly);
        Assert.Equal(2, result.Items.Length);
        Assert.Equal(CardMetaLayerCodes.ActiveMajorArcana,
            result.Items[0].MetaLayerCode);
        Assert.Contains(result.Items, value => value.HierarchyTierCode == "Meta");
        Assert.Contains(result.Items, value => value.HierarchyTierCode == "Action");
        Assert.Equal([CardFamilyCodes.Tarot, CardFamilyCodes.TeamRole],
            result.SourceRevisions.Select(value => value.FamilyCode));
    }

    [Fact]
    public async Task LoadAsync_RejectsRelationThatChangesAvailability()
    {
        var source = Source(CardFamilyCodes.Tarot, CardHierarchyTierCodes.Meta,
            CardAuthorityCodes.ServerMutable, "tarot:tower", "탑", string.Empty,
            new CardWorkspaceRelation
            {
                SourceCardStableId = "tarot:tower",
                TargetCardStableId = "role:defend",
                RelationCode = CardContextRelationCodes.Recommended,
                ChangesAvailability = true,
            });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CardWorkspaceCoordinator(new[] { source }).LoadAsync());

        Assert.Equal("CardWorkspaceFamilyInvalid", error.Message);
    }

    private static ICardFamilySource Source(string family, string tier,
        string authority, string stableId, string title,
        string metaLayerCode = "",
        params CardWorkspaceRelation[] relations)
        => new FakeSource(new CardWorkspaceFamilySnapshot
        {
            FamilyCode = family,
            Items = new[]
            {
                new CardWorkspaceItem
                {
                    CardStableId = stableId,
                    Title = title,
                    FamilyCode = family,
                    HierarchyTierCode = tier,
                    MetaLayerCode = metaLayerCode,
                    AuthorityCode = authority,
                    IsAvailable = true,
                },
            },
            Relations = relations,
            SourceRevision = family == CardFamilyCodes.Tarot ? 3 : 7,
        });

    private sealed class FakeSource : ICardFamilySource
    {
        private readonly CardWorkspaceFamilySnapshot snapshot;
        public FakeSource(CardWorkspaceFamilySnapshot value) => snapshot = value;
        public string FamilyCode => snapshot.FamilyCode;
        public Task<CardWorkspaceFamilySnapshot> LoadAsync(
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }
}
