using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodCollaborationClient
{
    Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct);
    Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct);
    Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct);
    Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct);
    Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long postId, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long postId, CancellationToken ct);
}

public sealed class NeighborhoodCollaborationClient(ISsalddelJsonApiClient api) : INeighborhoodCollaborationClient
{
    public async Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct)
        => await api.GetAsync<NeighborhoodCollaborationListResponse>($"{NeighborhoodCollaborationRoutes.Api}/mine?scope={(scope == "undertaken" ? "undertaken" : "requested")}&page={Math.Clamp(page, 1, 10000)}",
            "내 협업 목록", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException();
    public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct)
        => api.GetAsync<NeighborhoodCollaborationResponse>($"{NeighborhoodCollaborationRoutes.Api}/{Uri.EscapeDataString(id)}", "협업 진행", cancellationToken: ct);
    public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodCollaborationCreateRequest, NeighborhoodCollaborationResponse>(HttpMethod.Post,
            NeighborhoodCollaborationRoutes.Api, request, "협업 신청", cancellationToken: ct);
    public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodCollaborationCommandRequest, NeighborhoodCollaborationResponse>(HttpMethod.Post,
            $"{NeighborhoodCollaborationRoutes.Api}/{Uri.EscapeDataString(id)}/commands", request, "협업 확인", cancellationToken: ct);
    public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct)
        => api.GetAsync<NeighborhoodCollaborationResponse>($"{NeighborhoodCollaborationRoutes.Api}/requests/{requestId:D}"
            + (stableId is null ? "" : "?stableId=" + Uri.EscapeDataString(stableId)), "협업 처리 결과", cancellationToken: ct);
    public async Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long postId, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>($"{NeighborhoodCollaborationRoutes.Api}/opportunities?sourcePostId={postId}",
            "공개 협업 참여", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException();
    public async Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long postId, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>($"{NeighborhoodCollaborationRoutes.Api}/public-history?sourcePostId={postId}",
            "동의한 완료 기록", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException();
}
