using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Cards
{
    public static class CardHierarchyTierCodes
    {
        public const string Meta = "Meta";
        public const string Context = "Context";
        public const string Action = "Action";
        public const string Knowledge = "Knowledge";
        public const string Research = "Research";
    }

    public static class CardFamilyCodes
    {
        public const string Tarot = "Tarot";
        public const string TurnClosing = "TurnClosing";
        public const string Culture = "Culture";
        public const string TeamRole = "TeamRole";
        public const string BattleSnapshot = "BattleSnapshot";
        public const string ConceptInformation = "ConceptInformation";
        public const string RecipeKnowledge = "RecipeKnowledge";
        public const string ResearchSeedbed = "ResearchSeedbed";
    }

    public static class CardAuthorityCodes
    {
        public const string ServerMutable = "ServerMutable";
        public const string ServerFrozenSnapshot = "ServerFrozenSnapshot";
        public const string ProjectionReadOnly = "ProjectionReadOnly";
        public const string ResearchOnly = "ResearchOnly";
    }

    public static class CardContextRelationCodes
    {
        public const string Relevant = "Relevant";
        public const string Recommended = "Recommended";
        public const string Warned = "Warned";
        public const string Contrasted = "Contrasted";
        public const string AvailabilityExplained = "AvailabilityExplained";
        public const string BlockExplained = "BlockExplained";
    }

    public static class CardMetaLayerCodes
    {
        public const string JourneyRoot = "JourneyRoot";
        public const string ActiveMajorArcana = "ActiveMajorArcana";
    }

    public static class CardActionRouteCodes
    {
        public const string None = "None";
        public const string OpenTurnClosing = "OpenTurnClosing";
        public const string SetTeamRole = "SetTeamRole";
        public const string SetCombatLoadout = "SetCombatLoadout";
        public const string OpenBattle = "OpenBattle";
        public const string OpenInformation = "OpenInformation";
        public const string OpenResearchSeedbed = "OpenResearchSeedbed";
    }

    public sealed class CardWorkspaceRelation
    {
        public string SourceCardStableId { get; set; } = string.Empty;
        public string TargetCardStableId { get; set; } = string.Empty;
        public string RelationCode { get; set; } = string.Empty;
        public bool ChangesAvailability { get; set; }
    }

    public sealed class CardWorkspaceItem
    {
        public string CardStableId { get; set; } = string.Empty;
        public string CardCopyStableId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string FamilyCode { get; set; } = string.Empty;
        public string HierarchyTierCode { get; set; } = string.Empty;
        public string MetaLayerCode { get; set; } = string.Empty;
        public string AuthorityCode { get; set; } = string.Empty;
        public string ActionRouteCode { get; set; } = CardActionRouteCodes.None;
        public string ApplicableControlModeCode { get; set; } = string.Empty;
        public string SlotCode { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public bool IsLocked { get; set; }
    }

    public sealed class CardWorkspaceFamilySnapshot
    {
        public string FamilyCode { get; set; } = string.Empty;
        public CardWorkspaceItem[] Items { get; set; } = Array.Empty<CardWorkspaceItem>();
        public CardWorkspaceRelation[] Relations { get; set; }
            = Array.Empty<CardWorkspaceRelation>();
        public long SourceRevision { get; set; }
    }

    public sealed class CardWorkspaceSnapshot
    {
        public CardWorkspaceItem[] Items { get; set; } = Array.Empty<CardWorkspaceItem>();
        public CardWorkspaceRelation[] Relations { get; set; }
            = Array.Empty<CardWorkspaceRelation>();
        public string[] LoadedFamilyCodes { get; set; } = Array.Empty<string>();
        public CardWorkspaceFamilyRevision[] SourceRevisions { get; set; }
            = Array.Empty<CardWorkspaceFamilyRevision>();
        public bool PresentationOnly { get; set; }
    }

    public sealed class CardWorkspaceFamilyRevision
    {
        public string FamilyCode { get; set; } = string.Empty;
        public long SourceRevision { get; set; }
    }

    public interface ICardFamilySource
    {
        string FamilyCode { get; }
        Task<CardWorkspaceFamilySnapshot> LoadAsync(CancellationToken cancellationToken);
    }

    public sealed class DelegateCardFamilySource : ICardFamilySource
    {
        private readonly Func<CancellationToken, Task<CardWorkspaceFamilySnapshot>> loader;

        public DelegateCardFamilySource(string familyCode,
            Func<CancellationToken, Task<CardWorkspaceFamilySnapshot>> load)
        {
            FamilyCode = string.IsNullOrWhiteSpace(familyCode)
                ? throw new ArgumentException("CardFamilyCodeMissing", nameof(familyCode))
                : familyCode;
            loader = load ?? throw new ArgumentNullException(nameof(load));
        }

        public string FamilyCode { get; }

        public Task<CardWorkspaceFamilySnapshot> LoadAsync(
            CancellationToken cancellationToken) => loader(cancellationToken);
    }

    /// <summary>
    /// 여러 카드 원장의 조회 결과를 하나의 서랍으로 투영한다.
    /// 실제 실행은 각 항목의 ActionRouteCode가 가리키는 기존 소유자에게 위임한다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class CardWorkspaceCoordinator
    {
        private readonly ICardFamilySource[] sources;

        public CardWorkspaceCoordinator(IEnumerable<ICardFamilySource> familySources)
        {
            sources = (familySources ?? throw new ArgumentNullException(
                    nameof(familySources))).ToArray();
            if (sources.Length == 0
                || sources.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.FamilyCode))
                || sources.Select(value => value.FamilyCode)
                    .Distinct(StringComparer.Ordinal).Count() != sources.Length)
                throw new InvalidOperationException("CardWorkspaceSourcesInvalid");
        }

        public async Task<CardWorkspaceSnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            var families = await Task.WhenAll(sources.Select(source =>
                source.LoadAsync(cancellationToken)));
            foreach (var family in families) ValidateFamily(family);

            var items = families.SelectMany(value => value.Items).ToArray();
            if (items.GroupBy(value => (value.FamilyCode, value.CardStableId,
                        value.CardCopyStableId), StringTupleComparer.Instance)
                    .Any(group => group.Count() > 1))
                throw new InvalidOperationException("CardWorkspaceItemDuplicate");

            return new CardWorkspaceSnapshot
            {
                Items = items,
                Relations = families.SelectMany(value => value.Relations).ToArray(),
                LoadedFamilyCodes = families.Select(value => value.FamilyCode).ToArray(),
                SourceRevisions = families
                    .OrderBy(value => value.FamilyCode, StringComparer.Ordinal)
                    .Select(value => new CardWorkspaceFamilyRevision
                    {
                        FamilyCode = value.FamilyCode,
                        SourceRevision = value.SourceRevision,
                    }).ToArray(),
                PresentationOnly = true,
            };
        }

        private static void ValidateFamily(CardWorkspaceFamilySnapshot family)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.FamilyCode)
                || family.Items == null || family.Relations == null
                || family.Items.Any(item => item == null
                    || item.FamilyCode != family.FamilyCode
                    || string.IsNullOrWhiteSpace(item.CardStableId)
                    || string.IsNullOrWhiteSpace(item.Title)
                    || string.IsNullOrWhiteSpace(item.HierarchyTierCode)
                    || string.IsNullOrWhiteSpace(item.AuthorityCode)
                    || (item.IsLocked && item.IsAvailable))
                || family.Relations.Any(relation => relation == null
                    || relation.ChangesAvailability
                    || string.IsNullOrWhiteSpace(relation.SourceCardStableId)
                    || string.IsNullOrWhiteSpace(relation.TargetCardStableId)
                    || string.IsNullOrWhiteSpace(relation.RelationCode)))
                throw new InvalidOperationException("CardWorkspaceFamilyInvalid");
        }

        private sealed class StringTupleComparer
            : IEqualityComparer<(string Family, string StableId, string CopyId)>
        {
            public static readonly StringTupleComparer Instance = new();

            public bool Equals((string Family, string StableId, string CopyId) x,
                (string Family, string StableId, string CopyId) y)
                => string.Equals(x.Family, y.Family, StringComparison.Ordinal)
                   && string.Equals(x.StableId, y.StableId, StringComparison.Ordinal)
                   && string.Equals(x.CopyId, y.CopyId, StringComparison.Ordinal);

            public int GetHashCode((string Family, string StableId, string CopyId) value)
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value.Family);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value.StableId);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value.CopyId);
                    return hash;
                }
            }
        }
    }
}
