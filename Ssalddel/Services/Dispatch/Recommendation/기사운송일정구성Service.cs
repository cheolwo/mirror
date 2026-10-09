using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public interface I기사운송일정구성Service
    {
        Task<기사운송일정계획> 구성Async(string 기사Id, 배차경로좌표? 시작좌표, CancellationToken cancellationToken = default);
    }

    public sealed class 기사운송일정구성Service : I기사운송일정구성Service
    {
        private static readonly string[] 픽업완료상태목록 = ["상차완료", "하차지도착", "운송중", "인수완료"];

        private readonly SsalddelContext _db;

        public 기사운송일정구성Service(SsalddelContext db)
        {
            _db = db;
        }

        public async Task<기사운송일정계획> 구성Async(string 기사Id, 배차경로좌표? 시작좌표, CancellationToken cancellationToken = default)
        {
            var transports = await _db.운송원장
                .AsNoTracking()
                .Where(x => x.기사_운송자 == 기사Id
                            && x.배차업무유형 == 상태값.배차업무유형.용달운송
                            && x.상태 != "인수완료")
                .OrderBy(x => x.출발_픽업 ?? x.UpdatedAt)
                .ThenBy(x => x.도착 ?? x.CreatedAt)
                .ToListAsync(cancellationToken);

            var requestIds = transports
                .Select(x => x.운송번호)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var requestMap = requestIds.Count == 0
                ? new Dictionary<string, 화주운송의뢰>(StringComparer.Ordinal)
                : await _db.화주운송의뢰
                    .AsNoTracking()
                    .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                    .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);

            var items = new List<기사운송일정항목>();
            var sequence = 0;
            foreach (var transport in transports)
            {
                requestMap.TryGetValue(transport.운송번호, out var request);

                if (상차지포함필요(transport.상태))
                {
                    items.Add(new 기사운송일정항목(
                        transport.운송번호,
                        "pickup",
                        request?.픽업_도로명주소 ?? transport.출발지,
                        CreatePoint(request?.픽업_위도, request?.픽업_경도),
                        request?.픽업_시간창_시작일시 ?? transport.출발_픽업,
                        request?.픽업_시간창_종료일시,
                        sequence++,
                        transport.Id,
                        true,
                        false));
                }

                items.Add(new 기사운송일정항목(
                    transport.운송번호,
                    "dropoff",
                    request?.하차_도로명주소 ?? transport.도착지,
                    CreatePoint(request?.하차_위도, request?.하차_경도),
                    request?.하차_시간창_시작일시 ?? transport.도착,
                    request?.하차_시간창_종료일시,
                    sequence++,
                    transport.Id,
                    true,
                    false));
            }

            return new 기사운송일정계획(
                기사Id,
                DateTime.UtcNow,
                시작좌표,
                items);
        }

        private static bool 상차지포함필요(string 상태)
        {
            return !픽업완료상태목록.Contains(상태, StringComparer.Ordinal);
        }

        private static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        {
            return latitude.HasValue && longitude.HasValue
                ? new 배차경로좌표(latitude.Value, longitude.Value)
                : null;
        }
    }
}
