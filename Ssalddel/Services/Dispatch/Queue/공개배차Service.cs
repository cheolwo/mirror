using Ssalddel.Application.Common.Transport;
using Microsoft.EntityFrameworkCore;
using Ssalddel;
using Ssalddel.Hubs;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Services.Dispatch.Compliance;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed class 공개배차Service : I공개배차Service
    {
        private readonly SsalddelContext _db;
        private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;
        private readonly I소형배송기사제안요금Service? _smallGoodsPricing;
        private readonly I신규화물업무자격Guard? _신규업무자격;

        public 공개배차Service(SsalddelContext db, ISharedDriverWorkAdmissionService? sharedAdmission = null,
            I소형배송기사제안요금Service? smallGoodsPricing = null,
            I신규화물업무자격Guard? 신규업무자격 = null)
        {
            _db = db;
            _sharedAdmission = sharedAdmission;
            _smallGoodsPricing = smallGoodsPricing;
            _신규업무자격 = 신규업무자격;
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetPublicDispatchesAsync(string driverId, CancellationToken cancellationToken = default)
        {
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId, cancellationToken)).CanReceiveNewWork) return [];
            var items = await _db.운송원장
                .AsNoTracking()
                .Where(q => q.배차업무유형 == 상태값.배차업무유형.용달운송
                            && q.상태 == 상태값.배차대기상태.대기 && q.생활배송수락준비완료
                            && ((q.배차큐단계 == 상태값.배차큐단계.공개배차 && q.배차노출상태 == 상태값.배차노출상태.공개중)
                                || q.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Hybrid && q.배차큐단계 != 상태값.배차큐단계.종료)
                            && (q.생활배송배차방식 == null || q.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.PublicCall || q.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Hybrid)
                            && q.확정기사Id == null)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync(cancellationToken);

            if (_sharedAdmission is not null)
            {
                for (var index = items.Count - 1; index >= 0; index--)
                {
                    var participation = await _sharedAdmission.CheckRecommendationAsync(driverId, items[index], cancellationToken);
                    if (participation.IsFailed) items.RemoveAt(index);
                }
            }

            if (_smallGoodsPricing is not null)
            {
                for (var index = items.Count - 1; index >= 0; index--)
                    if ((await _smallGoodsPricing.조회검증Async(items[index], cancellationToken)).IsFailed) items.RemoveAt(index);
            }

            if (items.Count == 0)
            {
                return Array.Empty<DispatchRecommendationDto>();
            }

            var requestIds = items.Select(x => x.의뢰Id).Distinct().ToArray();
            var directPickingIds = await 기사제안표시근거Service.직접피킹의뢰Async(_db, requestIds, cancellationToken);
            var requestMap = await _db.화주운송의뢰
                .AsNoTracking()
                .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, cancellationToken);

            items.RemoveAll(item => requestMap.TryGetValue(item.의뢰Id, out var request)
                ? !신규화물업무자격검사.화물정보완료(_신규업무자격, request)
                : _신규업무자격?.RequiresCargoEvidence != false);

            var cargoMap = await _db.화물요구조건
                .AsNoTracking()
                .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, cancellationToken);

            var result = items.Select(item =>
            {
                requestMap.TryGetValue(item.의뢰Id, out var request);
                cargoMap.TryGetValue(item.의뢰Id, out var cargo);

                var recommendation = 운송콜제안Projector.CreateCore(item,
                    SharedDriverWorkAdmissionService.ResolveWorkKind(item),
                    기사제안표시근거Service.운송작업(directPickingIds.Contains(item.의뢰Id)),
                    소형배송기사제안요금Service.TryReadPricing(item),
                    request?.화물종류 ?? cargo?.주의사항 ?? item.픽업_도로명주소);
                화물운송조건Projector.투영(recommendation, request);
                recommendation.추천라운드 = item.추천라운드;
                recommendation.추천유형 = "public";
                recommendation.추천사유 = "공개배차";
                recommendation.추천점수 = 0m;
                recommendation.상태 = 상태값.배차큐단계.공개배차.ToString();
                recommendation.배차상태 = 상태값.배차노출상태.공개중.ToString();
                recommendation.배지 = ["공개배차"];
                recommendation.경고 = Array.Empty<string>();
                recommendation.차량적합여부 = true;
                recommendation.차량부적합사유 = Array.Empty<string>();
                recommendation.차량경고 = Array.Empty<string>();

                운송콜제안Projector.ApplyClassificationAndPrivacy(recommendation, item, request?.클라이언트요청Id);
                return recommendation;
            }).ToArray();
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId, cancellationToken)).CanReceiveNewWork) return [];
            return result;
        }
    }
}
