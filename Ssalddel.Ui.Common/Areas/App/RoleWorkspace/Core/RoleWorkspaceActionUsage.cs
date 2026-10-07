using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>표시 위치만 구분하며 업무 권한이나 서버의 가능 행동을 판정하지 않습니다.</summary>
public enum RoleWorkspaceActionUsage
{
    Unclassified,
    Required,
    Conditional,
    OptionalTool
}

/// <summary>확인된 역할·행동 키만 분류합니다. 강조, 링크, 활성 여부는 필수 여부의 근거가 아닙니다.</summary>
public static class RoleWorkspaceActionPolicy
{
    public static RoleWorkspaceActionUsage GetUsage(string? roleKey, RoleWorkspaceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return (RoleWorkspaceCatalog.Normalize(roleKey), action.Key) switch
        {
            (RoleWorkspaceCatalog.Orderer, 음식배달가능행동Ids.주문수령확인) => RoleWorkspaceActionUsage.Required,
            (RoleWorkspaceCatalog.Orderer, 음식배달가능행동Ids.주문취소) => RoleWorkspaceActionUsage.Conditional,
            (RoleWorkspaceCatalog.Orderer, "food.new-order") => RoleWorkspaceActionUsage.OptionalTool,

            (RoleWorkspaceCatalog.Restaurant, 음식배달가능행동Ids.음식점주문수락
                or 음식배달가능행동Ids.음식점조리시작 or 음식배달가능행동Ids.음식점픽업준비완료) => RoleWorkspaceActionUsage.Required,
            (RoleWorkspaceCatalog.Restaurant, 음식배달가능행동Ids.음식점주문거절
                or 음식배달가능행동Ids.음식점조리시간변경) => RoleWorkspaceActionUsage.Conditional,

            (RoleWorkspaceCatalog.FoodDriver, 음식배달가능행동Ids.기사제안수락
                or 음식배달가능행동Ids.기사픽업확인
                or 음식배달가능행동Ids.기사전달완료) => RoleWorkspaceActionUsage.Required,
            // 서버 픽업 처리가 누락된 가게 도착 시각도 채우므로 별도 도착 기록은 절대 선행조건이 아니다.
            (RoleWorkspaceCatalog.FoodDriver, 음식배달가능행동Ids.기사제안거절
                or 음식배달가능행동Ids.기사가게도착 or 음식배달가능행동Ids.기사배달중단 or FoodDriverRoleWorkspaceAdapter.StartWork
                or FoodDriverRoleWorkspaceAdapter.StopWork or FoodDriverRoleWorkspaceAdapter.EnableDispatch
                or FoodDriverRoleWorkspaceAdapter.DisableDispatch or FoodDriverRoleWorkspaceAdapter.UpdateLocation) => RoleWorkspaceActionUsage.Conditional,

            (RoleWorkspaceCatalog.Shipper, "create" or "timeline" or "payment" or "proofs") => RoleWorkspaceActionUsage.OptionalTool,
            (RoleWorkspaceCatalog.CargoDriver, "arrive-pickup" or "pickup" or "arrive-dropoff" or "dropoff") => RoleWorkspaceActionUsage.Required,
            (RoleWorkspaceCatalog.CargoDriver, "offer" or "issue") => RoleWorkspaceActionUsage.Conditional,
            (RoleWorkspaceCatalog.Warehouse, "inbound" or "inspection" or "put-away" or "picking"
                or "packing" or "handoff" or "outbound") => RoleWorkspaceActionUsage.Conditional,
            (RoleWorkspaceCatalog.Operator, OperatorRoleWorkspaceAdapter.ReviewInterruption) => RoleWorkspaceActionUsage.Conditional,
            _ => RoleWorkspaceActionUsage.Unclassified
        };
    }

    /// <summary>현재 업무·복구 행동을 바로 표시하고, 미분류는 기존 상세 펼침 동작을 유지합니다.</summary>
    public static bool KeepVisible(string? roleKey, RoleWorkspaceAction action, bool detailsExpanded)
        => GetUsage(roleKey, action) switch
        {
            RoleWorkspaceActionUsage.Required or RoleWorkspaceActionUsage.Conditional => true,
            RoleWorkspaceActionUsage.OptionalTool => false,
            _ => action.IsPrimary || detailsExpanded
        };
}
