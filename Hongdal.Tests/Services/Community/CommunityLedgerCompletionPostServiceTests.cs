using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerCompletionPostServiceTests
{
    [Fact]
    public void BuildPrivacySafeContext_RemovesPersonalAndBusinessDetailValues()
    {
        var ledger = CreateSensitiveLedger(커뮤니티원장상태.완료);

        var draft = CommunityLedgerCompletionPublication.BuildDraft(ledger, new DateTime(2026, 7, 15, 1, 2, 3, DateTimeKind.Utc));
        var context = CommunityLedgerCompletionPublication.BuildPrivacySafeContext(ledger, featureEnabled: true);
        var publicPayload = JsonSerializer.Serialize(new { draft, context });

        Assert.True(context.다이어그램 is { Nodes.Count: 3, Edges.Count: 2 });
        Assert.Empty(context.블록목록);
        Assert.All(context.다이어그램!.Nodes, node =>
        {
            Assert.StartsWith("case-node-", node.NodeId, StringComparison.Ordinal);
            Assert.Empty(node.Data);
            Assert.Null(node.Description);
            Assert.Null(node.RelatedRoute);
        });
        Assert.All(context.다이어그램.Edges, edge =>
        {
            Assert.StartsWith("case-edge-", edge.EdgeId, StringComparison.Ordinal);
            Assert.Empty(edge.Data);
            Assert.Equal("절차 연결", edge.Label);
        });

        Assert.DoesNotContain("홍길동", publicPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1234-5678", publicPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("면목동 123-45", publicPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("1,230,000원", publicPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("private-node", publicPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("상세 증빙 메모", publicPayload, StringComparison.Ordinal);
        Assert.Contains("화물 운송 원장", draft.Title, StringComparison.Ordinal);
        Assert.Contains("비식별", draft.Body, StringComparison.Ordinal);
        Assert.Equal(CommunityBoardCatalog.CompletionReview.DisplayName, draft.Category);
    }

    [Fact]
    public void BuildPrivacySafeDiagram_UsesCatalogFlowWhenLedgerHasNoDiagram()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "ledger-no-diagram",
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "공개하면 안 되는 실제 제목",
            상태 = 커뮤니티원장상태.완료
        };

        var diagram = CommunityLedgerCompletionPublication.BuildPrivacySafeDiagram(ledger);

        Assert.NotEmpty(diagram.Nodes);
        Assert.Equal(diagram.Nodes.Count - 1, diagram.Edges.Count);
        Assert.Equal("catalog-only", diagram.Metadata["privacy"]);
        Assert.DoesNotContain(diagram.Nodes, node => node.Title.Contains("실제 제목", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishIfCompletedAsync_OnlySendsCompletedLedgersToStore()
    {
        var store = new RecordingStore();
        var service = new CommunityLedgerCompletionPostService(store);

        await service.PublishIfCompletedAsync(CreateSensitiveLedger(커뮤니티원장상태.진행중), "event-1", DateTime.UtcNow);
        await service.PublishIfCompletedAsync(CreateSensitiveLedger(커뮤니티원장상태.완료), "event-2", DateTime.UtcNow);

        var published = Assert.Single(store.Drafts);
        Assert.Equal("ledger-completed-case", published.LedgerId);
        Assert.Equal(CommunityLedgerCompletionPublication.SystemAuthorKey, published.SystemAuthorKey);
        Assert.Equal(CommunityLedgerCompletionPublication.Category, published.Category);
    }

    private static 커뮤니티원장Dto CreateSensitiveLedger(string state)
        => new()
        {
            원장Id = "ledger-completed-case",
            Revision = 7,
            커뮤니티Id = "platform",
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "홍길동님의 면목동 운송",
            원함 = "010-1234-5678로 연락",
            상태 = state,
            현재단계Key = "상세 증빙 메모",
            생성자UserId = "user-hong",
            생성자표시명 = "홍길동",
            참여자목록 =
            [
                new 커뮤니티원장참여자Dto
                {
                    UserId = "driver-1",
                    DisplayName = "홍길동",
                    RoleLabel = "기사"
                }
            ],
            블록목록 =
            [
                new 커뮤니티원장블록Dto
                {
                    BlockId = "private-block",
                    BlockType = "transport",
                    Title = "서울 중랑구 면목동 123-45",
                    Data = new Dictionary<string, string>
                    {
                        ["연락처"] = "010-1234-5678",
                        ["운임"] = "1,230,000원"
                    }
                }
            ],
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = "private-diagram",
                DiagramName = "홍길동 운송",
                Nodes =
                [
                    SensitiveNode("private-node-requester", "participant", "홍길동", 0, 0),
                    SensitiveNode("private-node-place", "place", "면목동 123-45", 260, 0),
                    SensitiveNode("private-node-payment", "settlement", "1,230,000원", 520, 0)
                ],
                Edges =
                [
                    SensitiveEdge("private-edge-1", "private-node-requester", "private-node-place", "010-1234-5678"),
                    SensitiveEdge("private-edge-2", "private-node-place", "private-node-payment", "상세 증빙 메모")
                ]
            }
        };

    private static DiagramNodeDto SensitiveNode(string id, string kind, string title, double x, double y)
        => new()
        {
            NodeId = id,
            Kind = kind,
            Title = title,
            GroupLabel = "홍길동 그룹",
            Description = "상세 증빙 메모",
            RelatedRoute = "/private/hong-gildong",
            X = x,
            Y = y,
            Data = new Dictionary<string, string> { ["연락처"] = "010-1234-5678" }
        };

    private static DiagramEdgeDto SensitiveEdge(string id, string from, string to, string label)
        => new()
        {
            EdgeId = id,
            FromNodeId = from,
            ToNodeId = to,
            Label = label,
            MeaningCode = "private",
            Data = new Dictionary<string, string> { ["운임"] = "1,230,000원" }
        };

    private sealed class RecordingStore : ICommunityLedgerCompletionPostStore
    {
        public List<CommunityLedgerCompletionPostDraft> Drafts { get; } = [];

        public Task<long?> PublishIfMissingAsync(
            CommunityLedgerCompletionPostDraft draft,
            CancellationToken cancellationToken = default)
        {
            Drafts.Add(draft);
            return Task.FromResult<long?>(Drafts.Count);
        }
    }
}
