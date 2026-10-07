using Microsoft.EntityFrameworkCore;
using Ssalddel.Hubs;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using Ssalddel.Application.Driver.Recommendation;

namespace 살뜰.Services.Dispatch.Request
{
    public sealed class NationalDispatchRequestService : INationalDispatchRequestService
    {
        private readonly SsalddelContext _db;
        private readonly IDriverRejectedRequestStore _rejectedRequestStore;

        public NationalDispatchRequestService(SsalddelContext db, IDriverRejectedRequestStore rejectedRequestStore)
        {
            _db = db;
            _rejectedRequestStore = rejectedRequestStore;
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetNationwideRequestsAsync(string driverId, CancellationToken cancellationToken = default)
        {
            var rejectedRequestIds = await _rejectedRequestStore.GetRejectedRequestIdsAsync(driverId, cancellationToken);
            var rejectedRequestIdSet = rejectedRequestIds.Count > 0
                ? new HashSet<string>(rejectedRequestIds, StringComparer.Ordinal)
                : null;

            var items = await _db.운송원장
                .AsNoTracking()
                .Where(q => q.상태 == 상태값.배차대기상태.대기)
                .OrderByDescending(q => q.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            var requestIds = items.Select(q => q.의뢰Id).Distinct().ToList();
            var requestMap = requestIds.Count == 0
                ? new Dictionary<string, 화주운송의뢰>(StringComparer.Ordinal)
                : await _db.화주운송의뢰
                    .AsNoTracking()
                    .Where(r => requestIds.Contains(r.의뢰Id))
                    .ToDictionaryAsync(r => r.의뢰Id, StringComparer.Ordinal, cancellationToken);

            return items
                .Where(q => rejectedRequestIdSet == null || !rejectedRequestIdSet.Contains(q.의뢰Id))
                .Select(q =>
                {
                    var recommendation = new DispatchRecommendationDto
                    {
                        의뢰Id = q.의뢰Id,
                        화물종류 = requestMap.TryGetValue(q.의뢰Id, out var request) ? request.화물종류 : q.픽업_도로명주소,
                        픽업지 = q.픽업_도로명주소,
                        하차지 = q.하차_도로명주소,
                        픽업_위도 = q.픽업_위도,
                        픽업_경도 = q.픽업_경도,
                        하차_위도 = q.하차_위도,
                        하차_경도 = q.하차_경도,
                        직선거리Km = null,
                        주행거리Km = null,
                        상태 = q.상태,
                        배차상태 = 상태값.배차상태.대기
                    };

                    DispatchRecommendationRequestTypeClassifier.ApplyTo(
                        recommendation,
                        q.원본의뢰유형,
                        q.공동구매도착지유형코드,
                        q.공동구매기사세대배송여부,
                        q.공동구매세대배송건수);
                    if (생활배송기사정보공개Policy.생활배송인가(request?.클라이언트요청Id))
                        생활배송기사정보공개Policy.추천정보가림(recommendation);
                    return recommendation;
                })
                .ToList();
        }
    }
}
