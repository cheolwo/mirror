using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Engine;

namespace Ssalddel.Application.Warehouse;

internal sealed class 창고재위탁출고UseCase
{
    private readonly SsalddelContext _db;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;

    internal 창고재위탁출고UseCase(
        SsalddelContext db,
        I운송원장Mongo동기화Service transportLedgerSync)
    {
        _db = db;
        _transportLedgerSync = transportLedgerSync;
    }

    internal async Task<Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답> ExecuteAsync(
        재고운송의뢰생성요청 request,
        string userId,
        Func<IQueryable<입고상품>> accessibleInventory,
        Func<IQueryable<출고예정>> accessibleOutboundPlans,
        CancellationToken cancellationToken)
    {
        var dropoffAddress = (request.하차지주소 ?? string.Empty).Trim();
        var dropoffAddressDetail = (request.하차지상세주소 ?? string.Empty).Trim();
        var dropoffContactName = (request.하차담당자명 ?? string.Empty).Trim();
        var dropoffContactPhone = (request.하차연락처 ?? string.Empty).Trim();
        var vehicleType = (request.차량종류 ?? string.Empty).Trim();
        var handlingNote = (request.취급메모 ?? string.Empty).Trim();
        var requestedPickupAtUtc = ReconsignmentTimeUtc(request.희망상차일시);
        var requestedArrivalAtUtc = ReconsignmentTimeUtc(request.희망도착일시);

        if (dropoffAddress.Length < 5
            || dropoffAddress.StartsWith("주문자:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("운송의뢰에는 실제 하차지 도로명 주소가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(vehicleType))
        {
            throw new InvalidOperationException("운송 차량 종류를 선택해 주세요.");
        }

        if (handlingNote.Length > 300)
        {
            throw new InvalidOperationException("취급 메모는 300자 이하로 입력해 주세요.");
        }

        if (dropoffContactName.Length > 100 || dropoffContactPhone.Length > 50)
        {
            throw new InvalidOperationException("하차 담당자명은 100자, 연락처는 50자 이하로 입력해 주세요.");
        }

        if (requestedPickupAtUtc.HasValue != requestedArrivalAtUtc.HasValue
            || requestedPickupAtUtc.HasValue
            && requestedArrivalAtUtc <= requestedPickupAtUtc)
        {
            throw new InvalidOperationException("희망 상차·도착 일시를 모두 입력하고 도착 일시를 상차 일시보다 뒤로 지정해 주세요.");
        }

        var item = await accessibleInventory()
            .FirstOrDefaultAsync(x => x.Id == request.입고상품Id, cancellationToken)
            ?? throw new InvalidOperationException("입고상품을 찾을 수 없거나 접근할 수 없습니다.");

        if (item.보관위치.StartsWith("DLOC-", StringComparison.Ordinal))
            throw new InvalidOperationException("분산 적재 재고는 시간대 주문의 예약별 운송 연결을 이용해 주세요.");

        출고예정? outboundPlan = null;
        if (request.출고예정Id is > 0)
        {
            outboundPlan = await accessibleOutboundPlans()
                .FirstOrDefaultAsync(x => x.Id == request.출고예정Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("출고예정 원장을 찾을 수 없거나 접근할 수 없습니다.");

            if (outboundPlan.입고상품Id != item.Id)
            {
                throw new InvalidOperationException("출고예정 원장과 운송에 인계할 입고상품이 일치하지 않습니다.");
            }

            if (outboundPlan.출고창고Id != item.창고Id)
            {
                throw new InvalidOperationException("출고예정 원장의 출발 창고와 입고상품의 창고가 일치하지 않습니다.");
            }

            if (!string.IsNullOrWhiteSpace(outboundPlan.운송의뢰Id))
            {
                var existing = await _db.화주운송의뢰.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.의뢰Id == outboundPlan.운송의뢰Id, cancellationToken)
                    ?? throw new InvalidOperationException("출고예정 원장에 연결된 운송의뢰를 찾을 수 없습니다.");
                var existingLink = await _db.운송의뢰상품연결.AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.운송의뢰Id == existing.의뢰Id && x.입고상품Id == item.Id,
                        cancellationToken)
                    ?? throw new InvalidOperationException("출고예정 원장과 운송의뢰의 상품 연결을 확인할 수 없습니다.");

                if (existingLink.할당수량 != request.요청수량
                    || !string.Equals(existing.하차_도로명주소, dropoffAddress, StringComparison.Ordinal)
                    || !string.Equals(existing.하차_상세주소, dropoffAddressDetail, StringComparison.Ordinal)
                    || request.하차담당자명 is not null
                       && !string.Equals(existing.하차_연락처_이름, dropoffContactName, StringComparison.Ordinal)
                    || request.하차연락처 is not null
                       && !string.Equals(existing.하차_연락처_전화번호, dropoffContactPhone, StringComparison.Ordinal)
                    || !string.Equals(existing.차량종류, vehicleType, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("이미 운송의뢰가 연결된 출고예정입니다. 기존 의뢰와 다른 내용으로 다시 생성할 수 없습니다.");
                }

                var existingTransport = await _db.운송원장.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.의뢰Id == existing.의뢰Id, cancellationToken);
                var retried = Ssalddel.Application.Shipper.Request.화주운송의뢰매퍼.To응답(existing, existingTransport);
                retried.멱등재시도여부 = true;
                return retried;
            }

            if (outboundPlan.상태 != 출고상태.준비중)
            {
                throw new InvalidOperationException("운송의뢰는 준비 중인 출고예정에서만 생성할 수 있습니다.");
            }

            if (outboundPlan.수량 != request.요청수량)
            {
                throw new InvalidOperationException("운송 요청 수량은 출고예정 원장의 수량과 일치해야 합니다.");
            }
        }

        if (item.가용수량 < request.요청수량 || request.요청수량 <= 0)
        {
            throw new InvalidOperationException("가용수량보다 많은 수량을 재위탁할 수 없습니다.");
        }

        var warehouse = await _db.창고.FirstOrDefaultAsync(x => x.Id == item.창고Id, cancellationToken)
            ?? throw new InvalidOperationException("창고 정보를 찾을 수 없습니다.");
        if (!warehouse.IsActive || string.IsNullOrWhiteSpace(warehouse.주소))
        {
            throw new InvalidOperationException("활성 출고 창고와 실제 상차 주소가 필요합니다.");
        }

        var now = DateTime.UtcNow;
        var pickupAt = requestedPickupAtUtc ?? now;
        var arrivalAt = requestedArrivalAtUtc ?? pickupAt.AddDays(1);
        var storageCondition = await _db.입고요청.AsNoTracking()
            .Where(x => x.Id == item.입고요청Id)
            .Select(x => x.보관조건)
            .SingleOrDefaultAsync(cancellationToken);
        var requestId = outboundPlan is null
            ? Guid.NewGuid().ToString()
            : $"warehouse-outbound-{outboundPlan.Id}";
        var clientRequestId = outboundPlan is null
            ? $"reconsignment-{item.Id}-{now:yyyyMMddHHmmss}"
            : $"reconsignment-outbound-{outboundPlan.Id}";
        var requestText = $"재위탁 출고 상품 SKU: {item.SKU}";
        if (!string.IsNullOrWhiteSpace(handlingNote))
        {
            requestText = $"{requestText}\n취급 메모: {handlingNote}";
        }

        var shipRequest = new 화주운송의뢰
        {
            의뢰Id = requestId,
            화주Id = item.판매자UserId,
            주문자UserId = userId,
            화물종류 = string.IsNullOrWhiteSpace(request.화물종류) ? item.상품명 : request.화물종류.Trim(),
            화물설명 = $"입고상품 재위탁: {item.상품명}",
            화물수량 = request.요청수량,
            화물중량Kg = null,
            화물부피Cbm = null,
            화물파손주의여부 = false,
            화물온도조건 = string.IsNullOrWhiteSpace(storageCondition) ? "상온" : storageCondition,
            운송방식 = "재위탁",
            차량종류 = vehicleType,
            결제수단 = Ssalddel.Contracts.Shipper.Request.결제수단.별도정산.ToString(),
            정산시점 = Ssalddel.Contracts.Shipper.Request.정산시점.운송완료후정산.ToString(),
            증빙방식 = Ssalddel.Contracts.Shipper.Request.증빙방식.없음.ToString(),
            수납주체 = Ssalddel.Contracts.Shipper.Request.수납주체.플랫폼.ToString(),
            정산상태 = Ssalddel.Contracts.Shipper.Request.운임정산상태.청구대기.ToString(),
            정산메모 = "입고상품 재위탁 운송",
            결제예정금액 = null,
            픽업_도로명주소 = warehouse.주소,
            픽업_상세주소 = item.보관위치,
            픽업_연락처_이름 = warehouse.담당자명,
            픽업_연락처_전화번호 = warehouse.연락처,
            // 저장된 출고 창고의 위치만 인계합니다. 하차 위치·화물 규격·운임은 화주 검토에서 확인합니다.
            픽업_위도 = warehouse.위도,
            픽업_경도 = warehouse.경도,
            픽업_시간창_시작일시 = pickupAt,
            픽업_시간창_종료일시 = pickupAt.AddHours(1),
            하차_도로명주소 = dropoffAddress,
            하차_상세주소 = dropoffAddressDetail,
            하차_연락처_이름 = dropoffContactName,
            하차_연락처_전화번호 = dropoffContactPhone,
            하차_시간창_시작일시 = arrivalAt,
            하차_시간창_종료일시 = arrivalAt.AddHours(1),
            서비스레벨 = "일반",
            요청사항 = requestText,
            클라이언트요청Id = clientRequestId,
            상태 = 상태값.의뢰상태.생성됨,
            결제상태 = 상태값.결제상태.결제대기,
            배차상태 = 상태값.배차상태.미시작,
            CreatedAt = now,
            UpdatedAt = now
        };

        item.가용수량 -= request.요청수량;
        item.예약수량 += request.요청수량;
        item.상태 = item.가용수량 == 0 ? "재위탁대기" : item.상태;
        item.UpdatedAt = now;
        if (outboundPlan is not null)
        {
            outboundPlan.운송의뢰Id = shipRequest.의뢰Id;
            outboundPlan.UpdatedAt = now;
        }

        _db.화주운송의뢰.Add(shipRequest);
        _db.운송의뢰상품연결.Add(new 운송의뢰상품연결
        {
            운송의뢰Id = shipRequest.의뢰Id,
            입고상품Id = item.Id,
            할당수량 = request.요청수량,
            CreatedAt = now
        });

        var transportProjection = new 운송원장
        {
            운송번호 = shipRequest.의뢰Id,
            의뢰Id = shipRequest.의뢰Id,
            화주Id = shipRequest.화주Id,
            배차업무유형 = 상태값.배차업무유형.용달운송,
            원본의뢰유형 = 운송의뢰배차원천유형.창고출고연계운송,
            원본의뢰Id = (outboundPlan?.Id ?? item.Id).ToString(System.Globalization.CultureInfo.InvariantCulture),
            픽업_도로명주소 = shipRequest.픽업_도로명주소,
            픽업_상세주소 = shipRequest.픽업_상세주소,
            픽업_위도 = shipRequest.픽업_위도,
            픽업_경도 = shipRequest.픽업_경도,
            하차_도로명주소 = shipRequest.하차_도로명주소,
            하차_상세주소 = shipRequest.하차_상세주소,
            하차_위도 = shipRequest.하차_위도,
            하차_경도 = shipRequest.하차_경도,
            상태 = 상태값.배차대기상태.대기,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.운송원장.Add(transportProjection);

        _db.재고이력.Add(new 재고이력
        {
            입고상품Id = item.Id,
            이력유형 = 재고이동유형.예약,
            변경수량 = -request.요청수량,
            변경후수량 = item.가용수량,
            원인유형 = "재위탁운송생성",
            처리UserId = userId,
            메모 = $"재위탁 운송의뢰 생성: {shipRequest.의뢰Id}",
            처리일시 = now
        });

        _db.재고이동.Add(new 재고이동
        {
            창고Id = item.창고Id,
            입고상품Id = item.Id,
            상품명 = item.상품명,
            SKU = item.SKU,
            이동유형 = 재고이동유형.예약,
            수량 = request.요청수량,
            출고예정Id = outboundPlan?.Id,
            운송의뢰Id = shipRequest.의뢰Id,
            처리UserId = userId,
            메모 = $"재위탁 운송의뢰 생성: {shipRequest.의뢰Id}",
            발생일시 = now
        });

        await Ssalddel.Application.Shipper.Request.화주운송의뢰매퍼.UpsertCargoRequirementAsync(_db, shipRequest, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _transportLedgerSync.화주운송의뢰동기화Async(shipRequest, userId, cancellationToken);

        return Ssalddel.Application.Shipper.Request.화주운송의뢰매퍼.To응답(shipRequest, transportProjection);
    }

    // 오프셋 JSON은 Local DateTime으로 역직렬화될 수 있으므로 저장 전에 UTC 시각으로 복원한다.
    // 오프셋 없는 기존 입력은 시계값을 유지하며 서버의 UTC 시간창 계약으로 해석한다.
    private static DateTime? ReconsignmentTimeUtc(DateTime? value)
        => value is { } time
            ? time.Kind == DateTimeKind.Local
                ? time.ToUniversalTime()
                : DateTime.SpecifyKind(time, DateTimeKind.Utc)
            : null;
}
