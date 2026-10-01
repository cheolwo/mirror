using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp.Services;

public interface IShipperWarehouseWorkflowService : IWarehouseWorkspaceService
{
    Task<화주운송의뢰응답?> CreateReconsignmentAsync(재고운송의뢰생성요청 payload, CancellationToken cancellationToken = default);
}
