using System.Security.Cryptography;
using System.Text;
using Hongdal.Application.Community;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using MediatR;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

public interface ICommunityLedgerCompletionPostService
{
    Task PublishIfCompletedAsync(
        커뮤니티원장Dto ledger,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public interface ICommunityLedgerCompletionPostStore
{
    Task<long?> PublishIfMissingAsync(
        CommunityLedgerCompletionPostDraft draft,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityLedgerCompletionPostDraft(
    string LedgerId,
    string AppKey,
    string Category,
    string WorkflowTag,
    string RoleTag,
    string Title,
    string Body,
    string Nickname,
    string SystemAuthorKey,
    DateTime CreatedAtUtc);

public static class CommunityLedgerCompletionPublication
{
    public const string SystemAuthorKey = "system:ledger-completion";
    public static string Category => CommunityBoardCatalog.CompletionReview.DisplayName;

    public static bool IsCompleted(커뮤니티원장Dto ledger)
        => string.Equals(ledger.상태, 커뮤니티원장상태.완료, StringComparison.OrdinalIgnoreCase);

    public static bool IsSystemPost(PlatformCommunityPost post)
        => string.Equals(post.AuthorUserId, SystemAuthorKey, StringComparison.Ordinal);

    public static CommunityLedgerCompletionPostDraft BuildDraft(
        커뮤니티원장Dto ledger,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var template = CommunityLedgerTemplateCatalog.Find(ledger.원장템플릿Key);
        var classification = CommunityWorkClassificationCatalog.FindByLedgerTemplate(ledger.원장템플릿Key);
        var timestamp = occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc.ToUniversalTime();

        return new CommunityLedgerCompletionPostDraft(
            LedgerId: ledger.원장Id,
            AppKey: "platform",
            Category: Category,
            WorkflowTag: classification?.WorkflowTag ?? template.WorkflowTag,
            RoleTag: "시스템 기록",
            Title: $"[성립 사례] {template.DisplayName} 절차가 완료되었습니다",
            Body: string.Join(
                Environment.NewLine + Environment.NewLine,
                $"{template.DisplayName} 1건이 정해진 절차를 거쳐 완료 상태로 성립했습니다.",
                template.BestLedgerPatternSummary,
                "공개되는 내용: 원장 종류, 완료 여부, 개인 식별정보가 제거된 비식별 절차 다이어그램",
                "공개하지 않는 내용: 이름, 연락처, 상세 주소, 금액, 상품·화물 세부값, 증빙과 메모 원문",
                "다이어그램의 단계와 연결 순서를 참고해 같은 유형의 일을 새로 시작할 수 있습니다."),
            Nickname: "홍달 시스템",
            SystemAuthorKey: SystemAuthorKey,
            CreatedAtUtc: timestamp);
    }

    public static PlatformCommunityPostLedgerContextResponse BuildPrivacySafeContext(
        커뮤니티원장Dto ledger,
        bool featureEnabled)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var template = CommunityLedgerTemplateCatalog.Find(ledger.원장템플릿Key);
        var classification = CommunityWorkClassificationCatalog.FindByLedgerTemplate(ledger.원장템플릿Key);

        return new PlatformCommunityPostLedgerContextResponse
        {
            원장Id = ledger.원장Id,
            Revision = ledger.Revision,
            원장템플릿Key = ledger.원장템플릿Key,
            원장템플릿명 = template.DisplayName,
            제목 = $"{template.DisplayName} 비식별 완료 사례",
            상태 = 커뮤니티원장상태.완료,
            현재단계 = string.Empty,
            처리체계명 = template.TargetOperatingSystemName,
            업무분류Code = classification?.Code ?? string.Empty,
            업무분류명 = classification?.DisplayName ?? template.WorkflowTag,
            기능설정Key = classification?.FeatureFlagKey ?? string.Empty,
            기능활성화여부 = featureEnabled,
            상세조회가능여부 = false,
            참여요청필요여부 = false,
            재사용허용여부 = false,
            재공유허용여부 = false,
            다이어그램 = BuildPrivacySafeDiagram(ledger, template),
            블록목록 = [],
            가능한행동목록 = ["완료 절차 보기", "같은 원장 유형으로 시작"],
            노드행동목록 = [],
            포함원장목록 = []
        };
    }

    public static DiagramSnapshotDto BuildPrivacySafeDiagram(
        커뮤니티원장Dto ledger,
        CommunityLedgerTemplateResponse? template = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        template ??= CommunityLedgerTemplateCatalog.Find(ledger.원장템플릿Key);

        var source = ledger.다이어그램스냅샷;
        var safeId = BuildSafeId(ledger.원장Id);
        if (source is not { Nodes.Count: > 0 })
        {
            return BuildTemplateDiagram(template, safeId);
        }

        var nodeIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nodes = new List<DiagramNodeDto>(source.Nodes.Count);
        for (var index = 0; index < source.Nodes.Count; index++)
        {
            var sourceNode = source.Nodes[index];
            var nodeId = $"case-node-{index + 1}";
            if (!string.IsNullOrWhiteSpace(sourceNode.NodeId))
            {
                nodeIdMap.TryAdd(sourceNode.NodeId, nodeId);
            }

            var kind = NormalizeNodeKind(sourceNode.Kind);
            nodes.Add(new DiagramNodeDto
            {
                NodeId = nodeId,
                Kind = kind,
                Title = $"{NodeKindLabel(kind)} {index + 1}",
                GroupLabel = "비식별 완료 절차",
                X = double.IsFinite(sourceNode.X) ? sourceNode.X : 0,
                Y = double.IsFinite(sourceNode.Y) ? sourceNode.Y : index * 140,
                Data = new Dictionary<string, string>()
            });
        }

        var edges = source.Edges
            .Select((edge, index) => new { edge, index })
            .Where(item => nodeIdMap.ContainsKey(item.edge.FromNodeId)
                           && nodeIdMap.ContainsKey(item.edge.ToNodeId))
            .Select(item => new DiagramEdgeDto
            {
                EdgeId = $"case-edge-{item.index + 1}",
                FromNodeId = nodeIdMap[item.edge.FromNodeId],
                ToNodeId = nodeIdMap[item.edge.ToNodeId],
                Label = "절차 연결",
                Data = new Dictionary<string, string>()
            })
            .ToArray();

        if (edges.Length == 0 && nodes.Count > 1)
        {
            edges = BuildSequentialEdges(nodes);
        }

        return new DiagramSnapshotDto
        {
            DiagramId = $"completion-case-{safeId}",
            DiagramName = $"{template.DisplayName} 비식별 완료 절차",
            LedgerTemplateKey = ledger.원장템플릿Key,
            WorkflowModeKey = "completion-case",
            Nodes = nodes,
            Edges = edges,
            Metadata = new Dictionary<string, string>
            {
                ["privacy"] = "de-identified",
                ["source"] = "completed-ledger-structure"
            }
        };
    }

    private static DiagramSnapshotDto BuildTemplateDiagram(
        CommunityLedgerTemplateResponse template,
        string safeId)
    {
        var steps = template.ActionHints.Count > 0
            ? template.ActionHints.Take(8).ToArray()
            : template.UiSectionHints.Count > 0
                ? template.UiSectionHints.Take(8).ToArray()
                : ["요청 확인", "조건 합의", "업무 진행", "완료 확인"];
        var nodes = steps
            .Select((step, index) => new DiagramNodeDto
            {
                NodeId = $"case-node-{index + 1}",
                Kind = index == steps.Length - 1 ? "state" : "handoff",
                Title = step,
                GroupLabel = "표준 완료 절차",
                X = (index % 3) * 260,
                Y = (index / 3) * 150,
                Data = new Dictionary<string, string>()
            })
            .ToArray();

        return new DiagramSnapshotDto
        {
            DiagramId = $"completion-case-{safeId}",
            DiagramName = $"{template.DisplayName} 표준 완료 절차",
            LedgerTemplateKey = template.Key,
            WorkflowModeKey = "completion-case",
            Nodes = nodes,
            Edges = BuildSequentialEdges(nodes),
            Metadata = new Dictionary<string, string>
            {
                ["privacy"] = "catalog-only",
                ["source"] = "ledger-template"
            }
        };
    }

    private static DiagramEdgeDto[] BuildSequentialEdges(IReadOnlyList<DiagramNodeDto> nodes)
        => nodes
            .Zip(nodes.Skip(1), (from, to) => new DiagramEdgeDto
            {
                EdgeId = $"case-edge-{from.NodeId}-{to.NodeId}",
                FromNodeId = from.NodeId,
                ToNodeId = to.NodeId,
                Label = "다음 단계",
                Data = new Dictionary<string, string>()
            })
            .ToArray();

    private static string NormalizeNodeKind(string? kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "participant" => "participant",
            "place" => "place",
            "item" => "item",
            "state" => "state",
            "settlement" => "settlement",
            "order" => "order",
            "handoff" => "handoff",
            _ => "generic"
        };

    private static string NodeKindLabel(string kind)
        => kind switch
        {
            "participant" => "참여 역할",
            "place" => "거점",
            "item" => "대상 품목",
            "state" => "진행 상태",
            "settlement" => "정산 단계",
            "order" => "주문 단계",
            "handoff" => "인계 단계",
            _ => "업무 단계"
        };

    private static string BuildSafeId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes.AsSpan(0, 6)).ToLowerInvariant();
    }
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ledger,
    HongdalModuleKind.Application,
    "완료 원장을 개인정보를 줄인 커뮤니티 완료 사례 글로 한 번만 환류",
    ReleaseStage = HongdalCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "완료 사례에는 상세 주소·연락처·계좌·비공개 증빙을 복제하지 않으며 동일 원장의 중복 발행을 막습니다.")]
public sealed class CommunityLedgerCompletionPostService : ICommunityLedgerCompletionPostService
{
    private readonly ICommunityLedgerCompletionPostStore _store;

    public CommunityLedgerCompletionPostService(ICommunityLedgerCompletionPostStore store)
    {
        _store = store;
    }

    public async Task PublishIfCompletedAsync(
        커뮤니티원장Dto ledger,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!CommunityLedgerCompletionPublication.IsCompleted(ledger))
        {
            return;
        }

        var draft = CommunityLedgerCompletionPublication.BuildDraft(ledger, occurredAtUtc);
        await _store.PublishIfMissingAsync(draft, cancellationToken);
    }
}

public sealed class EfCommunityLedgerCompletionPostStore : ICommunityLedgerCompletionPostStore
{
    private readonly HongdalContext _db;
    private readonly I커뮤니티게시글음성작업예약Service _audioQueue;
    private readonly ICommunityKeywordNotificationQueue _keywordQueue;
    private readonly IPublisher _publisher;
    private readonly ILogger<EfCommunityLedgerCompletionPostStore> _logger;

    public EfCommunityLedgerCompletionPostStore(
        HongdalContext db,
        I커뮤니티게시글음성작업예약Service audioQueue,
        ICommunityKeywordNotificationQueue keywordQueue,
        IPublisher publisher,
        ILogger<EfCommunityLedgerCompletionPostStore> logger)
    {
        _db = db;
        _audioQueue = audioQueue;
        _keywordQueue = keywordQueue;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<long?> PublishIfMissingAsync(
        CommunityLedgerCompletionPostDraft draft,
        CancellationToken cancellationToken = default)
    {
        var existingPostId = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && post.커뮤니티원장Id == draft.LedgerId
                           && post.AuthorUserId == draft.SystemAuthorKey)
            .Select(post => (long?)post.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPostId.HasValue)
        {
            return existingPostId;
        }

        var entity = new PlatformCommunityPost
        {
            AppKey = draft.AppKey,
            Category = draft.Category,
            WorkflowTag = draft.WorkflowTag,
            RoleTag = draft.RoleTag,
            Title = draft.Title,
            Body = draft.Body,
            커뮤니티원장Id = draft.LedgerId,
            AuthorUserId = draft.SystemAuthorKey,
            Nickname = draft.Nickname,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.CreatedAtUtc
        };

        _db.PlatformCommunityPosts.Add(entity);
        _audioQueue.예약(entity, draft.CreatedAtUtc);
        _keywordQueue.Enqueue(entity, draft.CreatedAtUtc);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _publisher.Publish(new 커뮤니티게시글등록됨Event(entity.Id), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "원장 성립 게시글 후속 작업 신호 발행에 실패했습니다. DB 대기열에서 복구합니다. PostId={PostId}, LedgerId={LedgerId}",
                entity.Id,
                draft.LedgerId);
        }

        return entity.Id;
    }
}
