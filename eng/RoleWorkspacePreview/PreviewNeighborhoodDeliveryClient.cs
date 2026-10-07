using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RoleWorkspacePreview;

// 실제 업무와 분리된 보류 안내 렌더 검토입니다. 모든 쓰기는 차단합니다.
internal sealed class PreviewNeighborhoodUser : ISsalddel현재사용자Context
{
    public 현재사용자Snapshot 현재사용자 => new("preview-owner", "화면 검토", []);
}

internal sealed class PreviewNeighborhoodDeliveryClient : INeighborhoodDeliveryClient
{
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct)
        => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
        {
            RequestId = "preview-request-1", CargoName = "물품 1", FareKrw = 34000, DistanceKm = 3.2m,
            ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
            Request = new()
            {
                의뢰Id = "preview-request-1", 주문자UserId = "preview-owner", 운송상태 = "운송중", 비정상운송검토보류중 = true,
                픽업지 = "픽업 장소 1", 하차지 = "전달 장소 1", 차량종류 = "화물차",
                비정상운송사건목록 = [new()
                {
                    사건StableId = "preview-incident", 상태Code = "OperationsReviewPending",
                    업무통제상태Code = "PartiallyHeld", 보류범위Code = "AffectedQuantity", 정산보류적용여부 = true
                }]
            }
        });
    public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct)
        => throw new NotSupportedException();
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
        => throw new NotSupportedException();
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => throw new NotSupportedException();
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => throw new NotSupportedException();
}
