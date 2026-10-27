using System.Reflection;
using MudBlazor;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed class PlatformCommunityLedgerStatePresentationTests
{
    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.CargoTransport, "기사 수락", "운송 의뢰", 0)]
    [InlineData(CommunityLedgerTemplateKeys.CargoTransport, "상차 완료", "상차", 1)]
    [InlineData(CommunityLedgerTemplateKeys.CargoTransport, "POD 확인", "하차", 2)]
    [InlineData(CommunityLedgerTemplateKeys.CargoTransport, "하차 정산 보류", "결제/정산", 3)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart, "주문 접수", "마트 주문", 0)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart, "창고 재고", "도심 재고", 1)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart, "피킹 중", "피킹/포장", 2)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart, "피킹 완료", "픽업/전달", 3)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseInbound, "요청", "입고 요청", 0)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseInbound, "배송 도착", "운송 하차", 1)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseInbound, "입고 대기", "창고 입고", 2)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseInbound, "검수 필요", "검수/이상", 3)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseInbound, "검수 완료", "보관/재고화", 4)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "요청", "출고 요청", 0)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "출고 피킹", "피킹/검수", 1)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "포장 중", "포장", 2)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "출고 완료", "창고 출고", 3)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "출고 완료 배송", "운송 상차", 4)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "배송 도착", "운송 하차", 5)]
    [InlineData(CommunityLedgerTemplateKeys.WarehouseOutbound, "입고 완료", "창고 입고", 6)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, "요청", "음식 주문", 0)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, "접수", "주문 수락", 1)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, "조리 중", "조리", 2)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, "조리 완료", "준비 완료", 3)]
    [InlineData(CommunityLedgerTemplateKeys.FoodDelivery, "요청", "배달 회차", 0)]
    [InlineData(CommunityLedgerTemplateKeys.FoodDelivery, "배차 추천", "배차", 1)]
    [InlineData(CommunityLedgerTemplateKeys.FoodDelivery, "픽업 이동", "픽업/이동", 2)]
    [InlineData(CommunityLedgerTemplateKeys.FoodDelivery, "픽업 실패", "전달/수령", 3)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase, "요청", "수요 모집", 0)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase, "구매 확정", "구매 확정", 1)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase, "창고 보관", "수령 거점", 2)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase, "분배 진행", "분배", 3)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase, "수령 확인", "정산/수령", 4)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "요청", "원천 공동구매", 0)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "ImportDecision", "수입 결정", 1)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "ImportInProgress", "해외 선적", 2)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "통관 검토", "통관/반출", 3)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "3PL 창고", "3PL 입고", 4)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "국내 운송", "세대 분배", 5)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport, "수령 확인", "정산/수령", 6)]
    [InlineData(CommunityLedgerTemplateKeys.LocalSale, "요청", "판매/예약", 0)]
    [InlineData(CommunityLedgerTemplateKeys.LocalSale, "보관 중", "보관/준비", 1)]
    [InlineData(CommunityLedgerTemplateKeys.LocalSale, "택배 배송", "전달", 2)]
    [InlineData(CommunityLedgerTemplateKeys.LocalSale, "배송 확인", "정산/확인", 3)]
    [InlineData(CommunityLedgerTemplateKeys.Errand, "요청", "요청", 0)]
    [InlineData(CommunityLedgerTemplateKeys.Errand, "진행 중", "수행", 1)]
    [InlineData(CommunityLedgerTemplateKeys.Errand, "작업 보류", "확인/마감", 2)]
    public void 전문원장별_기존활성노드와_우선순위를_유지한다(
        string templateKey, string stateLabel, string expectedTitle, int expectedIndex)
    {
        var nodes = BuildExistingFlowNodes(templateKey);

        var actual = PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(templateKey, stateLabel, nodes);

        Assert.Equal(expectedIndex, actual);
        Assert.Equal(expectedTitle, nodes[actual].Title);
    }

    [Theory]
    [InlineData("전체 완료")]
    [InlineData("종료")]
    [InlineData("닫힘")]
    [InlineData("정산 완료")]
    [InlineData("완료됨")]
    public void 전체완료문구는_표시용으로만_모든노드뒤를_반환한다(string token)
    {
        var nodes = BuildExistingFlowNodes(CommunityLedgerTemplateKeys.FoodOrder);

        Assert.Equal(nodes.Count, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            CommunityLedgerTemplateKeys.FoodOrder, $" 조리 {token} ", nodes));
    }

    [Fact]
    public void 일반완료문구는_전체완료로_확대하지않는다()
    {
        var nodes = BuildExistingFlowNodes(CommunityLedgerTemplateKeys.FoodOrder);

        Assert.Equal(3, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            CommunityLedgerTemplateKeys.FoodOrder, "완료", nodes));
    }

    [Theory]
    [InlineData("  ", 0)]
    [InlineData(" second ", 1)]
    [InlineData("GROUP-SECOND 작업", 1)]
    [InlineData("설명근거", 1)]
    [InlineData("없는상태", 0)]
    public void 미등록원장은_공백정규화와_제목그룹설명_첫일치_폴백을_유지한다(string stateLabel, int expectedIndex)
    {
        원장블록노드[] nodes =
        [
            new("first", "group-first", "시작 근거", "product", Color.Primary),
            new("second", "group-second", "설명근거", "work", Color.Warning),
            new("second", "group-second", "설명근거", "work", Color.Warning)
        ];

        Assert.Equal(expectedIndex, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            "unknown", stateLabel, nodes));
        Assert.Equal(1, PlatformCommunityLedgerStatePresentation.FindNodeIndex(nodes, "SECOND"));
        Assert.Equal(-1, PlatformCommunityLedgerStatePresentation.FindNodeIndex(nodes, "missing"));
    }

    [Fact]
    public void 매핑된제목이없으면_직접일치로_돌아가며_노드현재순서를_사용한다()
    {
        var nodes = BuildExistingFlowNodes(CommunityLedgerTemplateKeys.FoodOrder).Reverse().ToArray();
        Assert.Equal(1, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            CommunityLedgerTemplateKeys.FoodOrder, "조리", nodes));

        원장블록노드[] changedNodes =
        [
            new("별도 노드", "시작", "첫 번째", "product", Color.Primary),
            new("현장 작업", "조리 중", "연결 근거", "work", Color.Warning)
        ];
        Assert.Equal(1, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            CommunityLedgerTemplateKeys.FoodOrder, "조리 중", changedNodes));
    }

    [Theory]
    [InlineData("", -1)]
    [InlineData("  ", -1)]
    [InlineData("없는상태", 0)]
    [InlineData("전체 완료", 0)]
    public void 빈노드목록의_기존경계값을_유지한다(string stateLabel, int expected)
        => Assert.Equal(expected, PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex("unknown", stateLabel, []));

    [Fact]
    public void 다이어그램레이어는_키와_표시순서_충돌우선순위_색상_잠금을_유지한다()
    {
        var layers = PlatformCommunityDiagramCatalog.All;
        Assert.Equal(new[] { "structure", "procedure", "role", "state", "evidence", "risk", "api" }, layers.Select(layer => layer.Key));
        Assert.Equal(new[] { "구조", "절차", "역할", "상태", "증빙", "리스크", "API" }, layers.Select(layer => layer.Label));
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70 }, layers.Select(layer => layer.DisplayOrder));
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 35 }, layers.Select(layer => layer.ConflictPriority));
        Assert.Equal(new[] { Color.Primary, Color.Secondary, Color.Info, Color.Success, Color.Warning, Color.Error, Color.Info }, layers.Select(layer => layer.Color));
        Assert.Equal(new[]
        {
            Icons.Material.Filled.Hub, Icons.Material.Filled.AccountTree, Icons.Material.Filled.PushPin,
            Icons.Material.Filled.Schedule, Icons.Material.Filled.FactCheck, Icons.Material.Filled.HelpOutline,
            Icons.Material.Filled.OpenInNew
        }, layers.Select(layer => layer.Icon));
        Assert.All(layers, layer => Assert.True(layer.DefaultVisible));
        Assert.Equal("structure", Assert.Single(layers.Where(layer => layer.IsLocked)).Key);
    }

    private static IReadOnlyList<원장블록노드> BuildExistingFlowNodes(string templateKey)
    {
        var method = typeof(PlatformCommunityHome).GetMethod("원장블록흐름도생성", BindingFlags.Static | BindingFlags.NonPublic)!;
        var flow = (원장블록흐름도)method.Invoke(null, [CommunityLedgerTemplateCatalog.Find(templateKey)])!;
        return flow.Nodes;
    }
}
