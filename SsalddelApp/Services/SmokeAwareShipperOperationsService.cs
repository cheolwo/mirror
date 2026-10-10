using Ssalddel.Client.Infrastructure;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Microsoft.Extensions.Options;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services.Samples;

namespace SsalddelApp.Services;

public sealed class SmokeAwareShipperOperationsService : IShipperOperationsService
{
    private readonly ServerBackedShipperOperationsService _server;
    private readonly SampleShipperOperationsService _sample;
    private readonly IOptions<ClientDataModeOptions> _dataModeOptions;

    public SmokeAwareShipperOperationsService(
        ServerBackedShipperOperationsService server,
        SampleShipperOperationsService sample,
        IOptions<ClientDataModeOptions> dataModeOptions)
    {
        _server = server;
        _sample = sample;
        _dataModeOptions = dataModeOptions;
    }

    public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetRequestsAsync(token),
            token => _sample.GetRequestsAsync(token),
            cancellationToken);

    public Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetRequestAsync(requestId, token),
            token => _sample.GetRequestAsync(requestId, token),
            cancellationToken);

    public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetPublicCargoAsync(token),
            token => _sample.GetPublicCargoAsync(token),
            cancellationToken);

    // 불명확한 실제 접수는 샘플의 미존재/성공으로 대체하지 않습니다.
    public Task<ShipperRequestItem?> GetRequestByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken = default)
        => _server.GetRequestByClientRequestIdAsync(clientRequestId, cancellationToken);

    public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetWarehousesAsync(token),
            token => _sample.GetWarehousesAsync(token),
            cancellationToken);

    public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetInboundsAsync(token),
            token => _sample.GetInboundsAsync(token),
            cancellationToken);

    public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetInventoryAsync(token),
            token => _sample.GetInventoryAsync(token),
            cancellationToken);

    public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.GetVehicleTypesAsync(token),
            token => _sample.GetVehicleTypesAsync(token),
            cancellationToken);

    public Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.EstimateFareAsync(vehicleType, distanceKm, token),
            token => _sample.EstimateFareAsync(vehicleType, distanceKm, token),
            cancellationToken);

    public Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default)
        => request.의뢰Id.StartsWith("shipper-app-", StringComparison.Ordinal)
            ? _server.AddRequestAsync(request, cancellationToken)
            : WithSampleFallbackAsync(
            token => _server.AddRequestAsync(request, token),
            token => _sample.AddRequestAsync(request, token),
            cancellationToken);

    public Task<ShipperRequestItem> UpdateRequestAsync(
        ShipperRequestItem request,
        CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            token => _server.UpdateRequestAsync(request, token),
            token => _sample.UpdateRequestAsync(request, token),
            cancellationToken);

    public Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default)
        => WithSampleFallbackAsync(
            async token =>
            {
                await _server.DeleteRequestAsync(requestId, token);
                return true;
            },
            async token =>
            {
                await _sample.DeleteRequestAsync(requestId, token);
                return true;
            },
            cancellationToken);

    private async Task<T> WithSampleFallbackAsync<T>(
        Func<CancellationToken, Task<T>> serverCall,
        Func<CancellationToken, Task<T>> sampleCall,
        CancellationToken cancellationToken)
    {
        try
        {
            return await serverCall(cancellationToken);
        }
        catch when (CanUseSampleFallback())
        {
            return await sampleCall(cancellationToken);
        }
    }

    private bool CanUseSampleFallback()
        => _dataModeOptions.Value.CanUseSampleFallback;
}
