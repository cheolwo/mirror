using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodStorageClient
{
    Task<NeighborhoodStorageListResponse> ListAsync(string? region, int page, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> MineAsync(int page, CancellationToken ct);
    Task<NeighborhoodStoragePublicDto?> PublicAsync(string id, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> PrivateAsync(string id, string? collaborationId, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> SaveAsync(string? id, NeighborhoodStorageSpaceRequest request, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> StateAsync(string id, string action, NeighborhoodStorageMutationRequest request, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> ReserveAsync(string id, NeighborhoodStorageReservationRequest request, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> HandoverAsync(string id, string collaborationId, NeighborhoodStorageReservationActionRequest request, CancellationToken ct);
    Task<NeighborhoodStorageSpaceDto?> ReceiptAsync(Guid requestId, string? spaceId, CancellationToken ct, string? collaborationId = null);
}

public sealed class NeighborhoodStorageClient(ISsalddelJsonApiClient api) : INeighborhoodStorageClient
{
    public async Task<NeighborhoodStorageListResponse> ListAsync(string? region, int page, CancellationToken ct)
        => await api.GetAsync<NeighborhoodStorageListResponse>($"{NeighborhoodStorageRoutes.Api}?page={Math.Clamp(page, 1, 10000)}"
            + (string.IsNullOrWhiteSpace(region) ? "" : "&regionKey=" + Uri.EscapeDataString(region)), "동네 보관공간", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException();
    public async Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> MineAsync(int page, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodStorageSpaceDto>>($"{NeighborhoodStorageRoutes.MineApi}?page={Math.Clamp(page, 1, 10000)}",
            "내 보관공간", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException();
    public Task<NeighborhoodStoragePublicDto?> PublicAsync(string id, CancellationToken ct)
        => api.GetAsync<NeighborhoodStoragePublicDto>(NeighborhoodStorageRoutes.DetailApi(id), "보관공간 조건", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> PrivateAsync(string id, string? collaborationId, CancellationToken ct)
        => api.GetAsync<NeighborhoodStorageSpaceDto>(NeighborhoodStorageRoutes.DetailApi(id) + "/private"
            + (string.IsNullOrWhiteSpace(collaborationId) ? "" : "?collaborationId=" + Uri.EscapeDataString(collaborationId)), "보관공간 인계 정보", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> SaveAsync(string? id, NeighborhoodStorageSpaceRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodStorageSpaceRequest, NeighborhoodStorageSpaceDto>(HttpMethod.Post,
            id is null ? NeighborhoodStorageRoutes.Api : NeighborhoodStorageRoutes.DetailApi(id) + "/update", request, "보관공간 저장", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> StateAsync(string id, string action, NeighborhoodStorageMutationRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodStorageMutationRequest, NeighborhoodStorageSpaceDto>(HttpMethod.Post,
            NeighborhoodStorageRoutes.DetailApi(id) + "/" + action, request, "공간 제공 상태", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> ReserveAsync(string id, NeighborhoodStorageReservationRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodStorageReservationRequest, NeighborhoodStorageSpaceDto>(HttpMethod.Post,
            NeighborhoodStorageRoutes.DetailApi(id) + "/reservations", request, "보관 예약", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> HandoverAsync(string id, string collaborationId, NeighborhoodStorageReservationActionRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodStorageReservationActionRequest, NeighborhoodStorageSpaceDto>(HttpMethod.Post,
            NeighborhoodStorageRoutes.DetailApi(id) + "/reservations/" + Uri.EscapeDataString(collaborationId) + "/action", request, "보관 물품 인계", cancellationToken: ct);
    public Task<NeighborhoodStorageSpaceDto?> ReceiptAsync(Guid requestId, string? spaceId, CancellationToken ct, string? collaborationId = null)
        => api.GetAsync<NeighborhoodStorageSpaceDto>($"{NeighborhoodStorageRoutes.Api}/requests/{requestId:D}"
            + (spaceId is null ? "" : "?spaceId=" + Uri.EscapeDataString(spaceId)
                + (collaborationId is null ? "" : "&collaborationId=" + Uri.EscapeDataString(collaborationId))), "보관 처리 결과", cancellationToken: ct);
}
