using Hongdal.Contracts.Common.Community;
using Hongdal.Extensions;
using Hongdal.Services.Community;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerStoreContractTests
{
    [Fact]
    public void Ledger_save_request_can_hold_blocks_participants_and_diagram_snapshot()
    {
        var request = new 커뮤니티원장저장요청
        {
            커뮤니티Id = "platform",
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "동네 책장 운송 원장",
            원함 = "책장을 옮기고 하차 완료 후 정산 표시를 남기고 싶어요.",
            대상OsCode = CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
            대상OsName = "국내 화물 운송 OS",
            블록목록 =
            [
                new()
                {
                    BlockId = "request",
                    BlockType = CommunityLedgerBlockTypes.Order,
                    Title = "운송 의뢰",
                    Data = new Dictionary<string, string>
                    {
                        ["상차지"] = "파주시",
                        ["하차지"] = "은평구"
                    }
                },
                new()
                {
                    BlockId = "settlement",
                    BlockType = CommunityLedgerBlockTypes.Settlement,
                    Title = "결제/정산",
                    State = "대기"
                }
            ],
            참여자목록 =
            [
                new()
                {
                    UserId = "user-1",
                    DisplayName = "익명 화주",
                    RoleLabel = "요청자"
                }
            ],
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = "diagram-1",
                DiagramName = "운송 의뢰-상차-하차-정산",
                LedgerTemplateKey = CommunityLedgerTemplateKeys.CargoTransport,
                Nodes =
                [
                    new() { NodeId = "request", Kind = "order", Title = "운송 의뢰", X = 10, Y = 80 },
                    new() { NodeId = "settlement", Kind = "confirm", Title = "결제/정산", X = 80, Y = 80 }
                ],
                Edges =
                [
                    new() { EdgeId = "edge-1", FromNodeId = "request", ToNodeId = "settlement", Label = "완료 후 정산 표시" }
                ]
            }
        };

        Assert.Equal("platform", request.커뮤니티Id);
        Assert.Equal(2, request.블록목록.Count);
        Assert.Equal("익명 화주", request.참여자목록.Single().DisplayName);
        Assert.Equal("완료 후 정산 표시", request.다이어그램스냅샷.Edges.Single().Label);
    }

    [Fact]
    public void Ledger_state_change_request_keeps_state_history_boundary_explicit()
    {
        var request = new 커뮤니티원장상태변경요청
        {
            원장Id = "ledger-1",
            이전상태 = 커뮤니티원장상태.초안,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "pickup",
            메모 = "상차 단계로 이동"
        };

        Assert.Equal("ledger-1", request.원장Id);
        Assert.Equal(커뮤니티원장상태.진행중, request.상태);
        Assert.Equal("pickup", request.현재단계Key);
    }

    [Fact]
    public void Ledger_write_contract_exposes_optimistic_revision_and_projection_state()
    {
        var save = new 커뮤니티원장저장요청
        {
            원장Id = "ledger-1",
            기대Revision = 4,
            제목 = "운송 원장"
        };
        var stateChange = new 커뮤니티원장상태변경요청
        {
            원장Id = "ledger-1",
            기대Revision = 5,
            상태 = 커뮤니티원장상태.완료
        };
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "ledger-1",
            Revision = 6,
            투영완료Revision = 5,
            투영상태 = 커뮤니티원장투영상태.재시도대기,
            투영EventId = "event-6"
        };

        Assert.Equal(4, save.기대Revision);
        Assert.Equal(5, stateChange.기대Revision);
        Assert.True(ledger.Revision > ledger.투영완료Revision);
        Assert.Equal(커뮤니티원장투영상태.재시도대기, ledger.투영상태);
    }

    [Fact]
    public void Order_ledger_can_reference_independent_fulfillment_ledgers_without_copying_their_state()
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "order-ledger-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.Order,
            제목 = "생활 주문 원장",
            포함원장목록 =
            [
                new()
                {
                    원장Id = "transport-ledger-1",
                    원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
                    역할 = 주문원장포함역할.운송,
                    필수여부 = true,
                    표시순서 = 0
                }
            ]
        };

        var reference = Assert.Single(request.포함원장목록!);
        Assert.Equal("transport-ledger-1", reference.원장Id);
        Assert.Equal(주문원장포함역할.운송, reference.역할);
        Assert.DoesNotContain(
            typeof(커뮤니티포함원장참조Dto).GetProperties(),
            property => property.Name.Contains("상태", StringComparison.Ordinal));
    }

    [Fact]
    public void Ledger_source_store_and_projection_work_store_have_separate_responsibilities()
    {
        Assert.True(typeof(I커뮤니티원장저장소).IsAssignableFrom(typeof(Mongo커뮤니티원장저장소)));
        Assert.False(typeof(I커뮤니티원장투영작업저장소).IsAssignableFrom(typeof(Mongo커뮤니티원장저장소)));
        Assert.True(typeof(I커뮤니티원장투영작업저장소).IsAssignableFrom(typeof(Mongo커뮤니티원장투영작업저장소)));
        Assert.False(typeof(I커뮤니티원장저장소).IsAssignableFrom(typeof(Mongo커뮤니티원장투영작업저장소)));

        var services = new ServiceCollection();
        services.AddHongdalDomainServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(I커뮤니티원장투영작업저장소)
            && descriptor.ImplementationType == typeof(Mongo커뮤니티원장투영작업저장소));

        var eventStoreDependencies = typeof(이벤트발행커뮤니티원장저장소)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Assert.Contains(typeof(I커뮤니티원장투영작업저장소), eventStoreDependencies);
    }
}
