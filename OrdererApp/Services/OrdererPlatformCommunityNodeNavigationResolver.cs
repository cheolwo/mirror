using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace OrdererApp.Services;

public sealed class OrdererPlatformCommunityNodeNavigationResolver
    : IPlatformCommunityNodeNavigationResolver
{
    public PlatformCommunityNodeNavigationTarget? Resolve(
        PlatformCommunityNodeNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsLedgerTemplate(CommunityLedgerTemplateKeys.FoodOrder))
        {
            return Food(OrdererRoutes.Food, "음식 주문 화면");
        }

        if (request.IsLedgerTemplate(CommunityLedgerTemplateKeys.SsalddelMart))
        {
            return Food(OrdererRoutes.Mart, "마트 상품 화면");
        }

        if (request.IsLedgerTemplate(CommunityLedgerTemplateKeys.CargoTransport))
        {
            return Food(OrdererRoutes.Cargo, "화물 주문 화면");
        }

        return null;
    }

    private static PlatformCommunityNodeNavigationTarget Food(string path, string label)
        => new(path, label, PlatformCommunityNodeNavigationArea.Food);
}
