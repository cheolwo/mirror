using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodMemberDeliveryClient
{
    Task<NeighborhoodMemberParticipationResponse?> ParticipationAsync(CancellationToken ct);
    Task<NeighborhoodMemberParticipationResponse?> SaveParticipationAsync(NeighborhoodMemberParticipationSaveRequest request, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodMemberOfferResponse>> OffersAsync(CancellationToken ct);
    Task<NeighborhoodMemberOfferResponse?> OfferAsync(string id, CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> AcceptAsync(string id, NeighborhoodMemberAcceptRequest request, CancellationToken ct);
    Task<NeighborhoodMemberOfferResponse?> DeclineAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodMemberWorkResponse>> WorkAsync(CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> WorkAsync(string id, CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> EvidenceAsync(string id, NeighborhoodMemberEvidenceRequest request, CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> PickupAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> DeliverAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct);
    Task<NeighborhoodMemberWorkResponse?> IssueAsync(string id, NeighborhoodMemberIssueRequest request, CancellationToken ct);
    Task<NeighborhoodMemberEvidenceReceiptResponse?> EvidenceReceiptAsync(string id, Guid requestId, CancellationToken ct)
        => Task.FromException<NeighborhoodMemberEvidenceReceiptResponse?>(new NotSupportedException());
    Task CancelRequestAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => Task.FromException(new NotSupportedException());
}

/// <summary>회원 본인의 생활 배송 API만 사용하며, 기사 역할 등록을 수행하지 않습니다.</summary>
public sealed class NeighborhoodMemberDeliveryClient(ISsalddelJsonApiClient api) : INeighborhoodMemberDeliveryClient
{
    private static string Path(string suffix) => NeighborhoodMemberDeliveryRoutes.Api + "/" + suffix;
    private static string Id(string value) => Uri.EscapeDataString(value);
    public Task<NeighborhoodMemberParticipationResponse?> ParticipationAsync(CancellationToken ct)
        => api.GetAsync<NeighborhoodMemberParticipationResponse>(Path("participation"), "배송 참여 설정", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodMemberParticipationResponse?> SaveParticipationAsync(NeighborhoodMemberParticipationSaveRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberParticipationSaveRequest, NeighborhoodMemberParticipationResponse>(HttpMethod.Put, Path("participation"), request, "배송 참여 저장", cancellationToken: ct);
    public async Task<IReadOnlyList<NeighborhoodMemberOfferResponse>> OffersAsync(CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodMemberOfferResponse>>(Path("offers"), "받은 배송 제안", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidOperationException("배송 제안 목록을 확인하지 못했습니다.");
    public Task<NeighborhoodMemberOfferResponse?> OfferAsync(string id, CancellationToken ct)
        => api.GetAsync<NeighborhoodMemberOfferResponse>(Path("offers/" + Id(id)), "배송 제안 상세", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodMemberWorkResponse?> AcceptAsync(string id, NeighborhoodMemberAcceptRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberAcceptRequest, NeighborhoodMemberWorkResponse>(HttpMethod.Post, Path("offers/" + Id(id) + "/accept"), request, "배송 제안 수락", cancellationToken: ct);
    public Task<NeighborhoodMemberOfferResponse?> DeclineAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberCommandRequest, NeighborhoodMemberOfferResponse>(HttpMethod.Post, Path("offers/" + Id(id) + "/decline"), request, "배송 제안 거절", cancellationToken: ct);
    public async Task<IReadOnlyList<NeighborhoodMemberWorkResponse>> WorkAsync(CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodMemberWorkResponse>>(Path("work"), "내 생활 배송", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidOperationException("생활 배송 목록을 확인하지 못했습니다.");
    public Task<NeighborhoodMemberWorkResponse?> WorkAsync(string id, CancellationToken ct)
        => api.GetAsync<NeighborhoodMemberWorkResponse>(Path("work/" + Id(id)), "생활 배송 진행", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodMemberWorkResponse?> EvidenceAsync(string id, NeighborhoodMemberEvidenceRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberEvidenceRequest, NeighborhoodMemberWorkResponse>(HttpMethod.Post, Path("work/" + Id(id) + "/evidence"), request, "생활 배송 사진 저장", cancellationToken: ct);
    public Task<NeighborhoodMemberWorkResponse?> PickupAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberCommandRequest, NeighborhoodMemberWorkResponse>(HttpMethod.Post, Path("work/" + Id(id) + "/pickup"), request, "물품 픽업 완료", cancellationToken: ct);
    public Task<NeighborhoodMemberWorkResponse?> DeliverAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberCommandRequest, NeighborhoodMemberWorkResponse>(HttpMethod.Post, Path("work/" + Id(id) + "/deliver"), request, "물품 전달 완료", cancellationToken: ct);
    public Task<NeighborhoodMemberWorkResponse?> IssueAsync(string id, NeighborhoodMemberIssueRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodMemberIssueRequest, NeighborhoodMemberWorkResponse>(HttpMethod.Post, Path("work/" + Id(id) + "/issue"), request, "배송 문제 접수", cancellationToken: ct);
    public async Task<NeighborhoodMemberEvidenceReceiptResponse?> EvidenceReceiptAsync(string id, Guid requestId, CancellationToken ct)
    {
        try { return await api.GetAsync<NeighborhoodMemberEvidenceReceiptResponse>(Path("work/" + Id(id) + "/evidence-receipts/" + requestId.ToString("D")), "사진 전송 결과 확인", allowNotFound: false, cancellationToken: ct); }
        catch (SsalddelApiException ex) when (ex.StatusCode == 404 && ex.ErrorCode is not ("MemberDeliveryDisabled" or "MemberWorkNotFound")) { return null; }
    }
    public async Task CancelRequestAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => _ = await api.SendAsync<NeighborhoodMemberCommandRequest, Dictionary<string, string>>(HttpMethod.Post, Path("requests/" + Id(id) + "/cancel"), request, "생활 배송 의뢰 취소", cancellationToken: ct);
}
