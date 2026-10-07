using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Application.Driver.Transport;

using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Services.Community;

public sealed class 운송상세조회QueryHandler : IRequestHandler<운송상세조회Query, 기사운송상세응답?>
{
    private readonly SsalddelContext _db;
    private readonly I생활배송기사정보제공동의Service? _disclosure;

    public 운송상세조회QueryHandler(SsalddelContext db, I생활배송기사정보제공동의Service? disclosure = null)
    {
        _db = db;
        _disclosure = disclosure;
    }

    public async Task<기사운송상세응답?> Handle(운송상세조회Query request, CancellationToken cancellationToken)
    {
        var entity = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.기사_운송자 == request.기사Id
                                     && x.배차업무유형 == 상태값.배차업무유형.용달운송, cancellationToken);

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
                x.하차_연락처_전화번호,
                x.픽업_위도,
                x.픽업_경도,
                x.하차_위도,
                x.하차_경도
            })
            .FirstOrDefaultAsync(cancellationToken);

        var response = new 기사운송상세응답
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
            UpdatedAt = entity.UpdatedAt,
            첨부Json = entity.첨부_json,
            메모 = entity.메모
        };
        기사운송업무상태Projector.투영(response, entity,
            await 기사운송업무상태Projector.사건조회Async(_db, [entity.Id], cancellationToken));
        if (생활배송기사정보공개Policy.생활배송인가(shipperRequest?.클라이언트요청Id))
            생활배송기사정보공개Policy.운송내부정보가림(response);
        if (!await 생활배송기사정보공개Policy.정보제공가능인가Async(shipperRequest?.클라이언트요청Id,
                requestId, shipperRequest?.주문자UserId ?? string.Empty, request.기사Id, _disclosure, cancellationToken))
            생활배송기사정보공개Policy.운송정보가림(response);

        // 구판 운송번호 fallback은 기존 상세 정보 호환에만 사용한다.
        // 지도 좌표는 기사 소유 원장의 명시 의뢰 관계와 정보 제공 조건을 모두 확인한 후 채운다.
        if (!response.개인정보제공보류 && !string.IsNullOrWhiteSpace(entity.의뢰Id)
            && shipperRequest is not null
            && string.Equals(entity.의뢰Id, shipperRequest.의뢰Id, StringComparison.Ordinal))
        {
            if (유효한좌표쌍(shipperRequest.픽업_위도, shipperRequest.픽업_경도))
            {
                response.픽업위도 = shipperRequest.픽업_위도;
                response.픽업경도 = shipperRequest.픽업_경도;
            }
            if (유효한좌표쌍(shipperRequest.하차_위도, shipperRequest.하차_경도))
            {
                response.하차위도 = shipperRequest.하차_위도;
                response.하차경도 = shipperRequest.하차_경도;
            }
        }
        return response;
    }

    private static bool 유효한좌표쌍(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}
