using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>검수·출고 전문 화면의 기존 ViewModel을 창고 역할과 선택 원장에만 연결합니다.</summary>
public sealed class WarehouseSpecialistTaskViewModel : IDisposable
{
    private readonly IRoleWorkspaceApi _api;
    private readonly IRoleWorkspaceAccess _access;
    private WarehouseRoleJsonApiBridge? _bridge;
    public WarehouseSpecialistTaskViewModel(IRoleWorkspaceApi api, IRoleWorkspaceAccess access)
    { _api = api; _access = access; Lifetime = new(access, RoleWorkspaceCatalog.Warehouse, Clear); }
    public CargoInputLifetime Lifetime { get; }
    public string Kind { get; private set; } = "";
    public string ItemId { get; private set; } = "";
    public bool Loaded { get; private set; }
    public int? DeniedStatus { get; private set; }
    public bool RequiresLogin => Lifetime.RequiresLogin || DeniedStatus == 401;
    public string Title => Kind switch { "inspection" => "수령한 물품 검수", "outbound" => "출고예정 검토", _ => "출고 운송·기사 인계" };
    public 입고검수실행ViewModel? Inspection { get; private set; }
    public 출고예정검토PageViewModel? Review { get; private set; }
    public 운송의뢰초안PageViewModel? Draft { get; private set; }
    public string ReturnHref => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Warehouse,
        (Kind == "transport-draft" ? "outbound" : Kind) + ":" + ItemId);
    public string ReviewHref => CargoWorkspaceRoutes.WarehouseInput("outbound", ItemId);
    public string DraftHref => CargoWorkspaceRoutes.WarehouseInput("transport-draft", ItemId);
    public string? PutAwayHref => Inspection?.상세.항목 is { CanInspect: false, InspectedAtUtc: not null, AvailableQuantity: > 0 }
        ? CargoWorkspaceRoutes.WarehouseInput("put-away", ItemId) : null;

    public async Task LoadAsync(string kind, string id)
    {
        if (kind != Kind || id != ItemId) { Lifetime.Reset(); Kind = kind; ItemId = id; }
        await Lifetime.RunAsync(async ct =>
        {
            if (kind is not ("inspection" or "outbound" or "transport-draft")) throw new ArgumentException("지원하지 않는 창고 업무입니다.");
            var target = long.Parse(CargoWorkspaceRoutes.PositiveNumber(id), System.Globalization.CultureInfo.InvariantCulture);
            Clear();
            _bridge = new(_api, _access, kind, target, Denied);
            switch (kind)
            {
                case "inspection":
                    var inspectionService = new 입고검수페이지Service(_bridge);
                    var inspection = new 입고검수실행ViewModel(new(inspectionService), new(inspectionService));
                    Inspection = inspection;
                    if (!await inspection.초기화Async(target, ct)) throw new InvalidOperationException("검수 원장을 확인하지 못했습니다.");
                    break;
                case "outbound":
                    var reviewService = new 출고예정검토페이지Service(_bridge);
                    var review = new 출고예정검토PageViewModel(new(reviewService), new(reviewService));
                    Review = review;
                    if (!await review.초기화Async(target, ct)) throw new InvalidOperationException("출고예정 원장을 확인하지 못했습니다.");
                    break;
                case "transport-draft":
                    var ledgerService = new 출고예정검토페이지Service(_bridge);
                    var inventoryService = new 입출고작업Service(_bridge);
                    var draft = new 운송의뢰초안PageViewModel(new(ledgerService), new(), new(inventoryService));
                    Draft = draft;
                    if (!await draft.초기화Async(target, ct)) throw new InvalidOperationException("출고 운송 원장을 확인하지 못했습니다.");
                    break;
            }
            Lifetime.RequireCurrent(ct); Loaded = true;
        });
    }
    private void Denied(int status)
    { Lifetime.Reset(); DeniedStatus = status; Lifetime.Notify(); }
    private void Clear()
    {
        Loaded = false; DeniedStatus = null;
        _bridge?.Dispose(); _bridge = null;
        Inspection?.Dispose(); Inspection = null;
        Review?.Dispose(); Review = null;
        Draft?.Dispose(); Draft = null;
    }
    public void Dispose() => Lifetime.Dispose();
}
