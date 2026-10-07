using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace WarehouseManagerApp.Services;

public sealed record WarehousePageAvailability(bool IsEnabled, string Notice);

/// <summary>서버 capability 메타데이터에서 창고 앱 페이지의 노출 가능성만 해석합니다.</summary>
public sealed class WarehousePageAvailabilityService(ICommunityProcurementClient client)
{
    private const string WarehouseFeatureKey = "WarehouseFulfillmentWorkflow";
    private const string MartFeatureKey = "SsalddelMartWorkflow";

    public async Task<WarehousePageAvailability> GetHomeAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.Warehouse, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetExpectedInboundsAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.ExpectedInbounds, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetInboundProductReceivingAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.InboundProductScan, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetInboundInspectionAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.InboundInspection, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetPickingBatchAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.PickingBatch, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetGeneralInventoryAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.GeneralInventory, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetPutAwayTaskAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.PutAwayTask, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetPackingTaskAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.PackingTask, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetOutboundHandoffAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.GeneralTransportHandoff, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetOutboundPlanReviewAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.OutboundPlanReview, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetTransportRequestDraftAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.TransportRequestDraft, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetWorkBoardAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.WorkBoard, WarehouseFeatureKey, cancellationToken);

    public async Task<WarehousePageAvailability> GetMartPickingAsync(
        CancellationToken cancellationToken = default)
        => await GetAsync(WarehouseManagerRoutes.MartPickingPacking, MartFeatureKey, cancellationToken);

    private async Task<WarehousePageAvailability> GetAsync(
        string route,
        string fallbackFeatureKey,
        CancellationToken cancellationToken)
    {
        var metadata = await client.GetVersionWorkflowMetadataAsync(cancellationToken);
        var capability = metadata.PageCapabilities.FirstOrDefault(item =>
            string.Equals(item.AppCode, SsalddelPageAppCodes.Warehouse, StringComparison.Ordinal)
            && string.Equals(item.RoutePattern, route, StringComparison.OrdinalIgnoreCase));

        if (capability is not null)
        {
            return new WarehousePageAvailability(capability.IsFeatureEnabled, capability.Notice);
        }

        var enabled = metadata.Flags.TryGetValue(fallbackFeatureKey, out var flag) && flag;
        return new WarehousePageAvailability(
            enabled,
            enabled
                ? "요청한 창고 조회 기능이 활성화되어 있습니다."
                : "현재 환경에서는 요청한 창고 조회 기능이 비활성입니다.");
    }
}
