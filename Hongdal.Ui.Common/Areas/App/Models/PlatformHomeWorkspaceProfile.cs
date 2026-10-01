using Hongdal.Contracts.Common.Community;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Models;

public sealed record PlatformHomeWorkspaceProfile(
    string Title,
    string Description,
    string LedgerTemplateKey,
    string OperatingSystemName,
    string EntryHref,
    string Icon,
    Color Color);

public static class PlatformHomeWorkspaceCatalog
{
    public static IReadOnlyList<PlatformHomeWorkspaceProfile> DefaultWorkspaces { get; } =
    [
        Create(
            "화물 운송",
            "상차, 하차, 증빙, 정산 표시가 필요한 이동 업무",
            CommunityLedgerTemplateKeys.CargoTransport,
            "/shipper/request",
            Icons.Material.Filled.LocalShipping,
            Color.Primary),
        Create(
            "음식 주문",
            "주문, 조리, 픽업, 수령 확인이 이어지는 음식 업무",
            CommunityLedgerTemplateKeys.FoodOrder,
            "/food",
            Icons.Material.Filled.Restaurant,
            Color.Error),
        Create(
            "음식 배달",
            "픽업지와 도착지를 기준으로 빠르게 처리하는 배달 업무",
            CommunityLedgerTemplateKeys.FoodDelivery,
            "/driver/recommendations",
            Icons.Material.Filled.DeliveryDining,
            Color.Warning),
        Create(
            "창고 출고",
            "피킹, 검수, 포장, 운송 인계를 연결하는 출고 업무",
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            "/work/outbound/start",
            Icons.Material.Filled.Outbox,
            Color.Info),
        Create(
            "창고 입고",
            "납품, 입고 검수, 보관 위치, 이상 여부를 남기는 입고 업무",
            CommunityLedgerTemplateKeys.WarehouseInbound,
            "/work/inbound/start",
            Icons.Material.Filled.MoveToInbox,
            Color.Success),
        Create(
            "생활 판매",
            "예약, 입금 표시, 전달 확인을 느슨하게 정리하는 거래 업무",
            CommunityLedgerTemplateKeys.LocalSale,
            "/community",
            Icons.Material.Filled.Storefront,
            Color.Secondary),
        Create(
            "공동구매",
            "모집, 구매, 분배, 정산 표시를 함께 굴리는 공동 업무",
            CommunityLedgerTemplateKeys.GroupPurchase,
            "/community/group-purchase",
            Icons.Material.Filled.Groups,
            Color.Primary),
        Create(
            "공동수입",
            "공동구매 수요를 이어 해외 선적, 통관, 3PL 입고와 국내 분배를 처리하는 업무",
            CommunityLedgerTemplateKeys.GroupImport,
            "/community/group-import",
            Icons.Material.Filled.Public,
            Color.Info),
        Create(
            "생활 요청",
            "심부름, 도움 요청, 동네 협업처럼 정형화되지 않은 업무",
            CommunityLedgerTemplateKeys.Errand,
            "/community",
            Icons.Material.Filled.Handshake,
            Color.Success)
    ];

    private static PlatformHomeWorkspaceProfile Create(
        string title,
        string description,
        string ledgerTemplateKey,
        string entryHref,
        string icon,
        Color color)
    {
        var template = CommunityLedgerTemplateCatalog.Find(ledgerTemplateKey);

        return new(
            title,
            description,
            template.Key,
            template.TargetOperatingSystemName,
            entryHref,
            icon,
            color);
    }
}
