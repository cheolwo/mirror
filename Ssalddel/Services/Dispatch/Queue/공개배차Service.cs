using Microsoft.EntityFrameworkCore;
using Ssalddel;
using Ssalddel.Hubs;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using Ssalddel.Application.Driver.Recommendation;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed class 공개배차Service : I공개배차Service
    {
        private readonly SsalddelContext _db;

        public 공개배차Service(SsalddelContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetPublicDispatchesAsync(string driverId, CancellationToken cancellationToken = default)
        {
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

            if (items.Count == 0)
            {
                return Array.Empty<DispatchRecommendationDto>();
            }

            var requestIds = items.Select(x => x.의뢰Id).Distinct().ToArray();
            var requestMap = await _db.화주운송의뢰
                .AsNoTracking()
                .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, cancellationToken);

            var cargoMap = await _db.화물요구조건
                .AsNoTracking()
                .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, cancellationToken);

            return items.Select(item =>
            {
                requestMap.TryGetValue(item.의뢰Id, out var request);
                cargoMap.TryGetValue(item.의뢰Id, out var cargo);

                var recommendation = new DispatchRecommendationDto
                {
                    의뢰Id = item.의뢰Id,
                    화물종류 = request?.화물종류 ?? cargo?.주의사항 ?? item.픽업_도로명주소,
                    픽업지 = item.픽업_도로명주소,
                    하차지 = item.하차_도로명주소,
                    픽업_위도 = item.픽업_위도,
                    픽업_경도 = item.픽업_경도,
                    하차_위도 = item.하차_위도,
                    하차_경도 = item.하차_경도,
                    추천라운드 = item.추천라운드,
                    추천유형 = "public",
                    추천사유 = "공개배차",
                    추천점수 = 0m,
                    상태 = 상태값.배차큐단계.공개배차.ToString(),
                    배차상태 = 상태값.배차노출상태.공개중.ToString(),
                    배지 = ["공개배차"],
                    경고 = Array.Empty<string>(),
                    차량적합여부 = true,
                    차량부적합사유 = Array.Empty<string>(),
                    차량경고 = Array.Empty<string>()
                };

                DispatchRecommendationRequestTypeClassifier.ApplyTo(
                    recommendation,
                    item.원본의뢰유형,
                    item.공동구매도착지유형코드,
                    item.공동구매기사세대배송여부,
                    item.공동구매세대배송건수);
                if (생활배송기사정보공개Policy.생활배송인가(request?.클라이언트요청Id))
                    생활배송기사정보공개Policy.추천정보가림(recommendation);
                return recommendation;
            }).ToArray();
        }
    }
}
