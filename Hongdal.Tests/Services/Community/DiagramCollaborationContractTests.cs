using Hongdal.Contracts.Common.Community;
using Hongdal.Hubs;

namespace Hongdal.Tests.Services.Community;

public sealed class DiagramCollaborationContractTests
{
    [Theory]
    [InlineData("community:market/ledger 1", "diagram-room-community-market-ledger-1")]
    [InlineData(" cargo-v1:SHP-1001 ", "diagram-room-cargo-v1-SHP-1001")]
    public void RoomGroup_normalizes_room_id_for_signalr_group(string roomId, string expected)
    {
        Assert.Equal(expected, DiagramCollaborationHub.BuildRoomGroup(roomId));
    }

    [Fact]
    public void Snapshot_contract_carries_diagram_and_work_context()
    {
        var request = new DiagramSnapshotShareRequest
        {
            RoomId = "community:market:ledger:SHP-1001",
            Message = "이 흐름으로 물류 대행 신청을 이어가요.",
            Snapshot = new DiagramSnapshotDto
            {
                DiagramId = "diagram-1",
                DiagramName = "마트 주문-창고-배송 흐름",
                LedgerId = "SHP-1001",
                LedgerTemplateKey = CommunityLedgerTemplateKeys.HongdalMart,
                WorkflowModeKey = "mart-instant",
                Nodes =
                [
                    new()
                    {
                        NodeId = "warehouse-1",
                        Kind = "warehouse",
                        Title = "창고",
                        RelatedRoute = "/shipper/inbound/requests",
                        OrganizationReferences =
                        [
                            new DiagramOrganizationReferenceDto
                            {
                                ReferenceId = "organization-1",
                                OrganizationKey = "provider-1",
                                DisplayName = "Example 3PL",
                                RoleLabel = "3PL 물류 후보",
                                CountryCode = "US",
                                OfficialWebsiteUrl = "https://example.com/"
                            }
                        ]
                    }
                ],
                Edges =
                [
                    new()
                    {
                        EdgeId = "edge-1",
                        FromNodeId = "order-1",
                        ToNodeId = "warehouse-1",
                        Label = "출고 요청"
                    }
                ]
            }
        };

        Assert.Equal("마트 주문-창고-배송 흐름", request.Snapshot.DiagramName);
        Assert.Equal(CommunityLedgerTemplateKeys.HongdalMart, request.Snapshot.LedgerTemplateKey);
        Assert.Equal("/shipper/inbound/requests", request.Snapshot.Nodes.Single().RelatedRoute);
        Assert.Equal("Example 3PL", request.Snapshot.Nodes.Single().OrganizationReferences.Single().DisplayName);
        Assert.Equal("출고 요청", request.Snapshot.Edges.Single().Label);
    }

    [Fact]
    public void Room_joined_contract_carries_current_user_identity()
    {
        var response = new DiagramRoomJoinedResponse
        {
            RoomId = "community:cargo-transport:diagram",
            ConnectionId = "connection-1",
            UserId = "user-1",
            DisplayName = "익명 참여자",
            DiagramName = "화물 운송 원장",
            JoinedAtUtc = DateTime.UtcNow
        };

        Assert.Equal("user-1", response.UserId);
        Assert.Equal("익명 참여자", response.DisplayName);
    }

    [Fact]
    public void Work_action_contract_links_room_to_business_screen()
    {
        var request = new DiagramWorkActionRequest
        {
            RoomId = "community:warehouse:proxy",
            ActionCode = DiagramWorkActionCodes.RequestWarehouseProxy,
            ActionLabel = "물류 대행 신청 열기",
            TargetRoute = "/shipper/inbound/requests?source=diagram-warehouse-proxy",
            LedgerId = "LEDGER-1",
            DiagramId = "diagram-1",
            NodeId = "warehouse-1",
            WorkContext = new DiagramWorkContextDto
            {
                WorkType = "WarehouseProxy",
                WorkLabel = "창고 물류 대행",
                AppKey = "HongdalApp",
                PrimaryRoute = "/shipper/inbound/requests",
                PrimaryActionLabel = "신청서 작성"
            }
        };

        Assert.Equal(DiagramWorkActionCodes.RequestWarehouseProxy, request.ActionCode);
        Assert.Equal("/shipper/inbound/requests?source=diagram-warehouse-proxy", request.TargetRoute);
        Assert.Equal("창고 물류 대행", request.WorkContext!.WorkLabel);
    }

    [Fact]
    public void Ledger_change_contract_uses_the_same_ledger_room_id()
    {
        var roomId = DiagramLedgerRoomIds.Build("ledger-101");
        var response = new DiagramLedgerChangedResponse
        {
            LedgerId = "ledger-101",
            Revision = 12,
            State = "진행중",
            CurrentStep = "상차지도착",
            NodeId = "pickup",
            ChangedAtUtc = DateTime.UtcNow
        };

        Assert.Equal("community:ledger:ledger-101:diagram", roomId);
        Assert.Equal("pickup", response.NodeId);
        Assert.Equal("ReceiveDiagramLedgerChanged", DiagramCollaborationClientMethods.ReceiveLedgerChanged);
    }
}
