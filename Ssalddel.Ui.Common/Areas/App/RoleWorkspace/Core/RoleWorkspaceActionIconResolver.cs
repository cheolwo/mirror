using MudBlazor;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>기존 행동 키의 보조 그림만 선택합니다. 권한·상태·문구·실행을 판정하지 않습니다.</summary>
public static class RoleWorkspaceActionIconResolver
{
    public static string? Resolve(string? actionKey) => actionKey switch
    {
        음식배달가능행동Ids.주문수령확인 => Icons.Material.Filled.Verified,
        음식배달가능행동Ids.주문취소 => Icons.Material.Filled.Cancel,
        음식배달가능행동Ids.음식점주문수락 or 음식배달가능행동Ids.기사제안수락 => Icons.Material.Filled.Check,
        음식배달가능행동Ids.음식점주문거절 or 음식배달가능행동Ids.기사제안거절 => Icons.Material.Filled.Block,
        음식배달가능행동Ids.음식점조리시작 => Icons.Material.Filled.Restaurant,
        음식배달가능행동Ids.음식점조리시간변경 => Icons.Material.Filled.Timer,
        음식배달가능행동Ids.음식점픽업준비완료 or 음식배달가능행동Ids.기사픽업확인 => Icons.Material.Filled.TakeoutDining,
        음식배달가능행동Ids.기사가게도착 => Icons.Material.Filled.Store,
        음식배달가능행동Ids.기사전달완료 => Icons.Material.Filled.LocalShipping,
        음식배달가능행동Ids.기사배달중단 => Icons.Material.Filled.Stop,
        FoodDriverRoleWorkspaceAdapter.StartWork => Icons.Material.Filled.PlayArrow,
        FoodDriverRoleWorkspaceAdapter.StopWork => Icons.Material.Filled.Stop,
        FoodDriverRoleWorkspaceAdapter.EnableDispatch => Icons.Material.Filled.Notifications,
        FoodDriverRoleWorkspaceAdapter.DisableDispatch => Icons.Material.Filled.NotificationsOff,
        FoodDriverRoleWorkspaceAdapter.UpdateLocation => Icons.Material.Filled.MyLocation,
        CargoDriverWorkSession.StartWork => Icons.Material.Filled.PlayArrow,
        CargoDriverWorkSession.StopWork => Icons.Material.Filled.Stop,
        CargoDriverWorkSession.EnableDispatch => Icons.Material.Filled.Notifications,
        CargoDriverWorkSession.DisableDispatch => Icons.Material.Filled.NotificationsOff,
        CargoDriverWorkSession.CheckLocation => Icons.Material.Filled.MyLocation,
        OperatorRoleWorkspaceAdapter.ReviewInterruption => Icons.Material.Filled.RateReview,
        CargoOperatorWorkspaceAdapter.ReviewIncidents => Icons.Material.Filled.ReportProblem,
        "food.new-order" => Icons.Material.Filled.Restaurant,
        "create" => Icons.Material.Filled.Add,
        "timeline" => Icons.Material.Filled.ListAlt,
        "history" => Icons.Material.Filled.History,
        "payment" => Icons.Material.Filled.ReceiptLong,
        "proofs" => Icons.Material.Filled.Description,
        "offer" => Icons.Material.Filled.LocalShipping,
        "arrive-pickup" or "arrive-dropoff" => Icons.Material.Filled.LocationOn,
        "pickup" => Icons.Material.Filled.Upload,
        "dropoff" => Icons.Material.Filled.Download,
        "issue" or "cargo-incidents" => Icons.Material.Filled.ReportProblem,
        "inbound" => Icons.Material.Filled.MoveToInbox,
        "inspection" => Icons.Material.Filled.FactCheck,
        "put-away" or "packing" => Icons.Material.Filled.Inventory2,
        "picking" => Icons.Material.Filled.ListAlt,
        "handoff" or "outbound" => Icons.Material.Filled.LocalShipping,
        _ => null
    };
}
