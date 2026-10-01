using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using 홍달.도메인.공통;
using 홍달.도메인.운송;
using 홍달.도메인.화주;
using 홍달.도메인.창고;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerBusinessProjectionTests
{
    [Fact]
    public void Transport_snapshot_maps_coordination_data_without_confirming_dispatch()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "transport:REQ-200",
            커뮤니티Id = "platform",
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "상차완료",
            원함 = "생활용품 운송",
            외부참조 = new Dictionary<string, string>
            {
                ["화주운송의뢰Id"] = "REQ-200",
                ["원천유형"] = "CargoTransport",
                ["원천Id"] = "REQ-200"
            },
            블록목록 =
            [
                new()
                {
                    BlockId = "transport-request",
                    BlockType = CommunityLedgerBlockTypes.Order,
                    Title = "운송 의뢰",
                    Data = new Dictionary<string, string>
                    {
                        ["화주Id"] = "shipper-2",
                        ["주문자UserId"] = "orderer-2",
                        ["화물종류"] = "생활용품",
                        ["화물설명"] = "박스 4개"
                    }
                },
                new()
                {
                    BlockId = "pickup",
                    BlockType = CommunityLedgerBlockTypes.Place,
                    Title = "상차",
                    Data = new Dictionary<string, string>
                    {
                        ["주소"] = "서울 중구 세종대로",
                        ["상세주소"] = "1층",
                        ["위도"] = "37.5665",
                        ["경도"] = "126.9780"
                    }
                },
                new()
                {
                    BlockId = "dropoff",
                    BlockType = CommunityLedgerBlockTypes.Place,
                    Title = "하차",
                    Data = new Dictionary<string, string>
                    {
                        ["주소"] = "서울 용산구 한강대로",
                        ["상세주소"] = "2층"
                    }
                },
                new()
                {
                    BlockId = "settlement",
                    BlockType = CommunityLedgerBlockTypes.Settlement,
                    Title = "결제 정산",
                    Data = new Dictionary<string, string>
                    {
                        ["최종운임"] = "55000"
                    }
                }
            ],
            참여자목록 =
            [
                new() { UserId = "driver-2", DisplayName = "기사", RoleLabel = "운반자" }
            ]
        };

        var snapshot = 운송원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal("REQ-200", snapshot!.RequestId);
        Assert.Equal("transport:REQ-200", snapshot.LedgerId);
        Assert.Equal("shipper-2", snapshot.ShipperId);
        Assert.Equal("orderer-2", snapshot.OrdererUserId);
        Assert.Equal("생활용품", snapshot.CargoType);
        Assert.Equal("서울 중구 세종대로", snapshot.PickupAddress);
        Assert.Equal(37.5665m, snapshot.PickupLatitude);
        Assert.Equal(55000m, snapshot.Fare);
        Assert.Equal(상태값.배차업무유형.용달운송, snapshot.DispatchBusinessType);
        Assert.True(snapshot.ContainsParticipantExecutionObservation);
        Assert.False(snapshot.CanCreateCoordinationTransport);

        Assert.Throws<InvalidOperationException>(() =>
            운송원장업무투영Handler.CreateCoordinationTransport(
                snapshot,
                new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Coordination_only_transport_ledger_creates_planning_state()
    {
        var snapshot = new 운송원장업무투영Snapshot
        {
            LedgerId = "transport:REQ-COORDINATION",
            LedgerTemplateKey = CommunityLedgerTemplateKeys.CargoTransport,
            LedgerState = 커뮤니티원장상태.진행중,
            RequestId = "REQ-COORDINATION",
            ShipperId = "shipper-1"
        };

        var transport = 운송원장업무투영Handler.CreateCoordinationTransport(
            snapshot,
            new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(상태값.배차대기상태.대기, transport.상태);
        Assert.Equal(상태값.배차큐단계.계획배차, transport.배차큐단계);
        Assert.Equal(상태값.배차노출상태.계획대기, transport.배차노출상태);
        Assert.Null(transport.확정기사Id);
    }

    [Fact]
    public void Transport_projection_preserves_participant_execution_state()
    {
        var snapshot = new 운송원장업무투영Snapshot
        {
            LedgerId = "transport:REQ-201",
            LedgerTemplateKey = CommunityLedgerTemplateKeys.CargoTransport,
            LedgerState = 커뮤니티원장상태.완료,
            RequestId = "REQ-201",
            ShipperId = "shipper-updated"
        };
        var transport = new 운송원장
        {
            의뢰Id = "REQ-201",
            상태 = 상태값.배차대기상태.확정,
            배차큐단계 = 상태값.배차큐단계.확정,
            배차노출상태 = 상태값.배차노출상태.확정,
            확정기사Id = "driver-accepted",
            기사_운송자 = "driver-accepted"
        };
        var shipperRequest = new 화주운송의뢰
        {
            의뢰Id = "REQ-201",
            배차상태 = 상태값.배차상태.배차확정
        };

        운송원장업무투영Handler.ApplyTransportProjection(
            transport,
            snapshot,
            isNew: false);
        운송원장업무투영Handler.ApplyShipperRequest(shipperRequest, snapshot);

        Assert.Equal(상태값.배차대기상태.확정, transport.상태);
        Assert.Equal(상태값.배차큐단계.확정, transport.배차큐단계);
        Assert.Equal(상태값.배차노출상태.확정, transport.배차노출상태);
        Assert.Equal("driver-accepted", transport.확정기사Id);
        Assert.Equal("driver-accepted", transport.기사_운송자);
        Assert.Equal(상태값.배차상태.배차확정, shipperRequest.배차상태);
    }

    [Fact]
    public void Transport_snapshot_does_not_capture_plain_food_order_without_transport_blocks()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "food-order:ORDER-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder,
            블록목록 =
            [
                new()
                {
                    BlockId = "menu",
                    BlockType = CommunityLedgerBlockTypes.Order,
                    Title = "메뉴",
                    Data = new Dictionary<string, string>
                    {
                        ["주문번호"] = "ORDER-1"
                    }
                }
            ]
        };

        Assert.False(운송원장업무투영Snapshot.처리대상인가(ledger));
        Assert.Null(운송원장업무투영Snapshot.생성(ledger));
    }

    [Fact]
    public void Transport_snapshot_treats_food_delivery_with_dispatch_reference_as_delivery_projection()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "transport:FOOD-200",
            원장템플릿Key = CommunityLedgerTemplateKeys.FoodDelivery,
            외부참조 = new Dictionary<string, string>
            {
                ["RdbTransportProjectionType"] = "운송원장",
                ["운송번호"] = "FOOD-200"
            },
            블록목록 =
            [
                new() { BlockId = "pickup", BlockType = CommunityLedgerBlockTypes.Place, Title = "픽업지" },
                new() { BlockId = "dropoff", BlockType = CommunityLedgerBlockTypes.Place, Title = "도착지" }
            ]
        };

        var snapshot = 운송원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal("FOOD-200", snapshot!.RequestId);
        Assert.Equal(상태값.배차업무유형.음식배달, snapshot.DispatchBusinessType);
    }

    [Fact]
    public void Warehouse_snapshot_maps_outbound_and_bundle_references()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "warehouse-outbound:310",
            원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseOutbound,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "포장 완료",
            외부참조 = new Dictionary<string, string>
            {
                ["출고예정Id"] = "310",
                ["출고묶음번호"] = "OB-20260713-1",
                ["주문참조번호"] = "ORDER-77",
                ["운송의뢰Id"] = "REQ-77"
            },
            블록목록 =
            [
                new()
                {
                    BlockId = "picking-packing",
                    BlockType = CommunityLedgerBlockTypes.State,
                    Title = "피킹/포장",
                    Data = new Dictionary<string, string>
                    {
                        ["업무엔티티"] = "출고묶음"
                    }
                }
            ]
        };

        var snapshot = 입출고원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal(310, snapshot!.출고예정Id);
        Assert.Equal("OB-20260713-1", snapshot.출고묶음번호);
        Assert.Equal("ORDER-77", snapshot.주문참조번호);
        Assert.Equal("REQ-77", snapshot.운송의뢰Id);
        Assert.Equal(출고상태.준비중, snapshot.ResolveOutboundState());
    }

    [Fact]
    public void Warehouse_snapshot_maps_inbound_completion_state()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "warehouse-inbound:21",
            원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseInbound,
            상태 = 커뮤니티원장상태.완료,
            현재단계Key = "입고 완료",
            외부참조 = new Dictionary<string, string>
            {
                ["입고요청Id"] = "21",
                ["입고상품Id"] = "901"
            }
        };

        var snapshot = 입출고원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal(21, snapshot!.입고요청Id);
        Assert.Equal(901, snapshot.입고상품Id);
        Assert.Equal(입고상태.입고완료, snapshot.ResolveInboundState());
    }

    [Fact]
    public void Warehouse_snapshot_treats_hongdal_mart_as_outbound_projection_by_order_reference()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "hongdal-mart:ORDER-9",
            원장템플릿Key = CommunityLedgerTemplateKeys.HongdalMart,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "피킹 시작",
            외부참조 = new Dictionary<string, string>
            {
                ["주문참조번호"] = "ORDER-9"
            }
        };

        var snapshot = 입출고원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal("ORDER-9", snapshot!.주문참조번호);
        Assert.Equal(출고상태.준비중, snapshot.ResolveOutboundState());
    }

    [Fact]
    public void Food_snapshot_maps_food_order_reference_without_transport_projection()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "food-order:FOOD-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder,
            상태 = 커뮤니티원장상태.진행중,
            블록목록 =
            [
                new()
                {
                    BlockId = "menu",
                    BlockType = CommunityLedgerBlockTypes.Order,
                    Title = "메뉴",
                    Data = new Dictionary<string, string>
                    {
                        ["주문번호"] = "FOOD-1"
                    }
                }
            ]
        };

        var snapshot = 음식주문원장업무투영Snapshot.생성(ledger);

        Assert.NotNull(snapshot);
        Assert.Equal("FOOD-1", snapshot!.주문번호);
        Assert.Equal("food-order:FOOD-1", snapshot.LedgerId);
    }
}
