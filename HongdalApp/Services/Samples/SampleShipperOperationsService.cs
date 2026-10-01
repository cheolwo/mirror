using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using HongdalApp.Models.Shipper;
using HongdalApp.Services.Application;
using HongdalApp.Services.Samples.Commands;

namespace HongdalApp.Services.Samples;

public sealed class SampleShipperOperationsService : IShipperOperationsService
{
    private readonly InMemoryShipperStore _store;
    private readonly IAppCommandHandler<AddShipperRequestCommand, bool> _addRequestHandler;

    public SampleShipperOperationsService(InMemoryShipperStore store, IAppCommandHandler<AddShipperRequestCommand, bool> addRequestHandler)
    {
        _store = store;
        _addRequestHandler = addRequestHandler;
    }

    public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetRequests());
    }

    public Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _store.GetRequests().FirstOrDefault(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetPublicCargo());
    }

    public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetWarehouses());
    }

    public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetInbounds());
    }

    public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetInventory());
    }

    public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetVehicleTypes());
    }

    public Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.EstimateFare(vehicleType, distanceKm));
    }

    public async Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _addRequestHandler.HandleAsync(new AddShipperRequestCommand(request, "shipper-demo"), cancellationToken);
        return request;
    }

    public Task<ShipperRequestItem> UpdateRequestAsync(
        ShipperRequestItem request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.UpdateRequest(request);
        return Task.FromResult(request);
    }

    public Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.DeleteRequest(requestId);
        return Task.CompletedTask;
    }
}
