using Ssalddel.Application.Common.Transport;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Hubs;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Services.Dispatch.Compliance;

namespace 살뜰.Services.Dispatch.Request
{
    public sealed class NationalDispatchRequestService : INationalDispatchRequestService
    {
        private readonly SsalddelContext _db;
        private readonly IDriverRejectedRequestStore _rejectedRequestStore;
        private readonly I소형배송기사제안요금Service? _smallGoodsPricing;
        private readonly I신규화물업무자격Guard? _신규업무자격;

        public NationalDispatchRequestService(SsalddelContext db, IDriverRejectedRequestStore rejectedRequestStore,
            I소형배송기사제안요금Service? smallGoodsPricing = null,
            I신규화물업무자격Guard? 신규업무자격 = null)
        {
            _db = db;
            _rejectedRequestStore = rejectedRequestStore;
            _smallGoodsPricing = smallGoodsPricing;
            _신규업무자격 = 신규업무자격;
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetNationwideRequestsAsync(string driverId, CancellationToken cancellationToken = default)
        {
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId, cancellationToken)).CanReceiveNewWork) return [];
            var rejectedRequestIds = await _rejectedRequestStore.GetRejectedRequestIdsAsync(driverId, cancellationToken);
            var rejectedRequestIdSet = rejectedRequestIds.Count > 0
                ? new HashSet<string>(rejectedRequestIds, StringComparer.Ordinal)
                : null;

            var items = await _db.운송원장
                .AsNoTracking()
                .Where(q => q.배차업무유형 == 상태값.배차업무유형.용달운송 && q.상태 == 상태값.배차대기상태.대기)
                .OrderByDescending(q => q.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            var requestIds = items.Select(q => q.의뢰Id).Distinct().ToList();
            if (_smallGoodsPricing is not null)
                for (var index = items.Count - 1; index >= 0; index--)
                    if ((await _smallGoodsPricing.조회검증Async(items[index], cancellationToken)).IsFailed) items.RemoveAt(index);
            var directPickingIds = await 기사제안표시근거Service.직접피킹의뢰Async(_db, items.Select(x => x.의뢰Id).ToArray(), cancellationToken);
            var requestMap = requestIds.Count == 0
                ? new Dictionary<string, 화주운송의뢰>(StringComparer.Ordinal)
                : await _db.화주운송의뢰
                    .AsNoTracking()
                    .Where(r => requestIds.Contains(r.의뢰Id))
                    .ToDictionaryAsync(r => r.의뢰Id, StringComparer.Ordinal, cancellationToken);

            items.RemoveAll(item => requestMap.TryGetValue(item.의뢰Id, out var request)
                ? !신규화물업무자격검사.화물정보완료(_신규업무자격, request)
                : _신규업무자격?.RequiresCargoEvidence != false);

            var result = items
                .Where(q => rejectedRequestIdSet == null || !rejectedRequestIdSet.Contains(q.의뢰Id))
                .Select(q =>
                {
                    var recommendation = 운송콜제안Projector.CreateCore(q,
                        Ssalddel.Services.Dispatch.Participation.SharedDriverWorkAdmissionService.ResolveWorkKind(q),
                        기사제안표시근거Service.운송작업(directPickingIds.Contains(q.의뢰Id)),
                        기사제안표시근거Service.요금근거(q),
                        requestMap.TryGetValue(q.의뢰Id, out var request) ? request.화물종류 : q.픽업_도로명주소);
                    화물운송조건Projector.투영(recommendation, request);
                    recommendation.직선거리Km = null;
                    recommendation.주행거리Km = null;
                    recommendation.상태 = q.상태;
                    recommendation.배차상태 = 상태값.배차상태.대기;

                    운송콜제안Projector.ApplyClassificationAndPrivacy(recommendation, q, request?.클라이언트요청Id);
                    return recommendation;
                })
                .ToList();
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId, cancellationToken)).CanReceiveNewWork) return [];
            return result;
        }
    }
}
