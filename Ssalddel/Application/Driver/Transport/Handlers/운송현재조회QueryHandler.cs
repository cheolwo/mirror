using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송현재조회QueryHandler : IRequestHandler<운송현재조회Query, 기사운송요약응답?>
{
    private readonly SsalddelContext _db;

    public 운송현재조회QueryHandler(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<기사운송요약응답?> Handle(운송현재조회Query request, CancellationToken cancellationToken)
    {
        var entity = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.기사_운송자 == request.기사Id
                        && x.배차업무유형 == 상태값.배차업무유형.용달운송
                        && x.상태 != "인수완료")
            .OrderBy(x => x.상태 == "하차지도착" ? 0
                : x.상태 == "상차완료" || x.상태 == "운송중" ? 1
                : x.상태 == "상차지도착" ? 2
                : x.상태 == "배차확정" || x.상태 == "확정" || x.상태 == "매칭중" ? 3
                : 9)
            .ThenBy(x => x.출발_픽업 ?? x.도착 ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var requestId = string.IsNullOrWhiteSpace(entity.의뢰Id) ? entity.운송번호 : entity.의뢰Id;
        var shipperRequest = await _db.화주운송의뢰
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId)
            .Select(x => new
            {
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
            .FirstOrDefaultAsync(cancellationToken);

        return new 기사운송요약응답
        {
            Id = entity.Id,
            운송번호 = entity.운송번호,
            상태 = entity.상태,
            출발지 = entity.출발지,
            도착지 = entity.도착지,
            기사_운송자 = entity.기사_운송자,
            출발_픽업 = entity.출발_픽업,
            도착 = entity.도착,
            운임 = entity.운임,
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
            UpdatedAt = entity.UpdatedAt
        };
    }
}
