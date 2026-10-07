using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Application.Driver.Transport;

using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Services.Community;

public sealed class 운송목록조회QueryHandler : IRequestHandler<운송목록조회Query, IReadOnlyList<기사운송요약응답>>
{
    private readonly SsalddelContext _db;
    private readonly I생활배송기사정보제공동의Service? _disclosure;

    public 운송목록조회QueryHandler(SsalddelContext db, I생활배송기사정보제공동의Service? disclosure = null)
    {
        _db = db;
        _disclosure = disclosure;
    }

    public async Task<IReadOnlyList<기사운송요약응답>> Handle(운송목록조회Query request, CancellationToken cancellationToken)
    {
        var transports = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.기사_운송자 == request.기사Id
                        && x.배차업무유형 == 상태값.배차업무유형.용달운송)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var requestIds = transports
            .Select(x => string.IsNullOrWhiteSpace(x.의뢰Id) ? x.운송번호 : x.의뢰Id)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var requestMap = await _db.화주운송의뢰
                .AsNoTracking()
                .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
                .Select(x => new
                {
                    x.의뢰Id,
                    x.클라이언트요청Id,
                    x.주문자UserId,
                    x.결제수단,
                    예상거리Km = _db.운임구성.Where(f => f.Id == x.운임구성Id && f.의뢰Id == x.의뢰Id).Select(f => f.예상거리Km).FirstOrDefault(),
                    거리계산방식 = _db.운임구성.Where(f => f.Id == x.운임구성Id && f.의뢰Id == x.의뢰Id).Select(f => f.거리계산방식).FirstOrDefault(),
                    x.증빙방식,
                    x.요청사항,
                    x.정산메모,
                    x.픽업_연락처_이름,
                    x.픽업_연락처_전화번호,
                    x.픽업_시간창_시작일시,
                    x.픽업_시간창_종료일시,
                    x.하차_시간창_시작일시,
                    x.하차_시간창_종료일시,
                    x.하차_연락처_이름,
                    x.하차_연락처_전화번호
                })
                .ToDictionaryAsync(
                    x => x.의뢰Id,
                    x => x,
                    StringComparer.Ordinal,
                    cancellationToken);

        var responses = new List<기사운송요약응답>(transports.Count);
        var incidents = await 기사운송업무상태Projector.사건조회Async(_db, transports.Select(item => item.Id).ToArray(), cancellationToken);
        foreach (var x in transports)
        {
            var requestId = string.IsNullOrWhiteSpace(x.의뢰Id) ? x.운송번호 : x.의뢰Id;
            requestMap.TryGetValue(requestId, out var shipperRequest);

            var response = new 기사운송요약응답
            {
                Id = x.Id,
                운송번호 = x.운송번호,
                상태 = x.상태,
                출발지 = x.출발지,
                도착지 = x.도착지,
                기사_운송자 = x.기사_운송자,
                출발_픽업 = x.출발_픽업,
                도착 = x.도착,
                운임 = x.운임,
                예상거리Km = shipperRequest?.예상거리Km,
                거리계산방식 = shipperRequest?.거리계산방식,
                결제방식 = shipperRequest?.결제수단 ?? string.Empty,
                상차담당자명 = shipperRequest?.픽업_연락처_이름 ?? string.Empty,
                상차연락처 = shipperRequest?.픽업_연락처_전화번호 ?? string.Empty,
                상차시간창시작일시 = shipperRequest?.픽업_시간창_시작일시 is { } pickupStart && pickupStart != default ? pickupStart : null,
                상차시간창종료일시 = shipperRequest?.픽업_시간창_종료일시 is { } pickupEnd && pickupEnd != default ? pickupEnd : null,
                하차시간창시작일시 = shipperRequest?.하차_시간창_시작일시 is { } dropoffStart && dropoffStart != default ? dropoffStart : null,
                하차시간창종료일시 = shipperRequest?.하차_시간창_종료일시 is { } dropoffEnd && dropoffEnd != default ? dropoffEnd : null,
                수령자명 = shipperRequest?.하차_연락처_이름 ?? string.Empty,
                수령자연락처 = shipperRequest?.하차_연락처_전화번호 ?? string.Empty,
                전달요청 = shipperRequest?.요청사항 ?? string.Empty,
                인수증필요 = 기사운송증빙조건정책.인수증필요(shipperRequest?.증빙방식, shipperRequest?.결제수단),
                인수증서명필수 = 기사운송증빙조건정책.인수증서명필수(shipperRequest?.요청사항, shipperRequest?.정산메모),
                UpdatedAt = x.UpdatedAt
            };
            기사운송업무상태Projector.투영(response, x, incidents);
            if (!await 생활배송기사정보공개Policy.정보제공가능인가Async(shipperRequest?.클라이언트요청Id,
                    requestId, shipperRequest?.주문자UserId ?? string.Empty, request.기사Id, _disclosure, cancellationToken))
                생활배송기사정보공개Policy.운송정보가림(response);
            responses.Add(response);
        }
        return responses;
    }
}
