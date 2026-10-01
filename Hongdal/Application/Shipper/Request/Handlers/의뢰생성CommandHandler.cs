using FluentResults;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Application.CommandProcessing;
using Hongdal.Services.Community;
using Hongdal.Services.Operations;
using 홍달.Services.External.Google;

namespace Hongdal.Application.Shipper.Request;

public sealed class 의뢰생성CommandHandler : IRequestHandler<의뢰생성Command, Result<화주운송의뢰응답>>
{
    private static readonly string[] AllowedPaymentStatuses = 상태값.결제상태.허용값;

    private readonly HongdalContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IGeocodingService _geocodingService;
    private readonly I운송원장Mongo동기화Service _원장동기화Service;
    private readonly IOperatingMarketFreightWorkflowPolicy _freightWorkflowPolicy;

    public 의뢰생성CommandHandler(
        HongdalContext db,
        IGeocodingService geocodingService,
        ICurrentUserAccessor currentUserAccessor,
        I운송원장Mongo동기화Service 원장동기화Service,
        IOperatingMarketFreightWorkflowPolicy freightWorkflowPolicy)
    {
        _db = db;
        _geocodingService = geocodingService;
        _currentUserAccessor = currentUserAccessor;
        _원장동기화Service = 원장동기화Service;
        _freightWorkflowPolicy = freightWorkflowPolicy;
    }

    public async Task<Result<화주운송의뢰응답>> Handle(의뢰생성Command request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserAccessor.UserId;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result.Fail<화주운송의뢰응답>("로그인 사용자 정보를 확인할 수 없습니다.");
        }

        var freightWorkflowDecision = _freightWorkflowPolicy.Evaluate(
            new OperatingMarketFreightWorkflowRequest
            {
                ActivityCode = FreightWorkflowActivityCodes.QualifiedProviderParticipationRequest
            });
        if (!freightWorkflowDecision.CanProceed)
        {
            return Result.Fail<화주운송의뢰응답>(
                $"OperatingMarketFreightPolicy:{freightWorkflowDecision.DecisionCode}");
        }

        if (string.IsNullOrWhiteSpace(request.화물종류))
        {
            return Result.Fail<화주운송의뢰응답>("화물종류 is required");
        }

        if (string.IsNullOrWhiteSpace(request.픽업도로명주소))
        {
            return Result.Fail<화주운송의뢰응답>("픽업 주소 도로명주소 is required");
        }

        if (string.IsNullOrWhiteSpace(request.픽업연락처전화번호))
        {
            return Result.Fail<화주운송의뢰응답>("픽업 연락처 전화번호 is required");
        }

        if (request.픽업시간창시작일시 >= request.픽업시간창종료일시)
        {
            return Result.Fail<화주운송의뢰응답>("pickup.window.startAt must be before endAt");
        }

        if (string.IsNullOrWhiteSpace(request.하차도로명주소))
        {
            return Result.Fail<화주운송의뢰응답>("하차 주소 도로명주소 is required");
        }

        if (request.하차시간창시작일시.HasValue
            && request.하차시간창종료일시.HasValue
            && request.하차시간창시작일시 >= request.하차시간창종료일시)
        {
            return Result.Fail<화주운송의뢰응답>("dropoff.window.startAt must be before endAt");
        }

        var shipperId = string.IsNullOrWhiteSpace(request.화주Id) ? currentUserId : request.화주Id.Trim();
        var clientRequestId = request.클라이언트요청Id?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            var duplicate = await _db.화주운송의뢰
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => (r.화주Id == shipperId || (r.화주Id == string.Empty && r.주문자UserId == currentUserId))
                         && r.클라이언트요청Id == clientRequestId,
                    cancellationToken);
            if (duplicate != null)
            {
                return Result.Fail<화주운송의뢰응답>("동일한 클라이언트요청Id로 이미 생성된 의뢰가 있습니다.");
            }
        }

        var settlementCondition = request.정산조건;
        var paymentStatus = string.IsNullOrWhiteSpace(request.결제상태) ? 상태값.결제상태.결제대기 : request.결제상태.Trim();
        if (!AllowedPaymentStatuses.Contains(paymentStatus))
        {
            return Result.Fail<화주운송의뢰응답>($"결제상태 must be one of: {string.Join(", ", AllowedPaymentStatuses)}");
        }

        var settlementTime = settlementCondition?.정산시점 ?? 정산시점.선결제;
        var paymentMethod = settlementCondition?.결제수단.ToString() ?? request.결제수단 ?? "카드";
        var evidenceMethod = settlementCondition?.증빙방식.ToString() ?? 증빙방식.없음.ToString();
        var collector = settlementCondition?.수납주체.ToString() ?? 수납주체.플랫폼.ToString();
        var settlementStatus = GetSettlementStatus(settlementTime, settlementCondition?.증빙방식);
        var finalFare = request.최종운임 ?? request.결제예정금액;
        var paymentAmount = request.결제예정금액 ?? (finalFare.HasValue ? decimal.ToInt32(finalFare.Value) : null);

        var (pickupLat, pickupLng) = await ResolveCoordinatesAsync(request.픽업도로명주소, request.픽업상세주소, cancellationToken);
        var (dropoffLat, dropoffLng) = await ResolveCoordinatesAsync(request.하차도로명주소, request.하차상세주소, cancellationToken);

        var entity = new 화주운송의뢰
        {
            의뢰Id = Guid.NewGuid().ToString(),
            화주Id = shipperId,
            주문자UserId = currentUserId,
            화물종류 = request.화물종류,
            화물설명 = request.화물설명 ?? string.Empty,
            화물수량 = request.화물수량,
            화물중량Kg = request.화물중량Kg,
            화물부피Cbm = request.화물부피Cbm,
            화물파손주의여부 = request.화물파손주의여부,
            화물온도조건 = request.화물온도조건 ?? "상온",
            운송방식 = request.운송방식 ?? "혼적",
            차량종류 = request.차량종류 ?? string.Empty,
            결제수단 = paymentMethod,
            정산시점 = settlementTime.ToString(),
            증빙방식 = evidenceMethod,
            수납주체 = collector,
            정산상태 = settlementStatus,
            정산메모 = settlementCondition?.정산메모 ?? string.Empty,
            세금계산서필요 = settlementCondition?.세금계산서필요 ?? false,
            현금영수증필요 = settlementCondition?.현금영수증필요 ?? false,
            결제예정금액 = paymentAmount,
            픽업_도로명주소 = request.픽업도로명주소,
            픽업_상세주소 = request.픽업상세주소 ?? string.Empty,
            픽업_위도 = pickupLat,
            픽업_경도 = pickupLng,
            픽업_연락처_이름 = request.픽업연락처이름,
            픽업_연락처_전화번호 = request.픽업연락처전화번호,
            픽업_시간창_시작일시 = request.픽업시간창시작일시,
            픽업_시간창_종료일시 = request.픽업시간창종료일시,
            하차_도로명주소 = request.하차도로명주소,
            하차_상세주소 = request.하차상세주소 ?? string.Empty,
            하차_위도 = dropoffLat,
            하차_경도 = dropoffLng,
            하차_연락처_이름 = request.하차연락처이름,
            하차_연락처_전화번호 = request.하차연락처전화번호,
            하차_시간창_시작일시 = request.하차시간창시작일시,
            하차_시간창_종료일시 = request.하차시간창종료일시,
            서비스레벨 = request.서비스레벨 ?? string.Empty,
            요청사항 = request.요청사항 ?? string.Empty,
            대기료 = request.대기료,
            수작업비 = request.수작업비,
            할증 = request.할증,
            최종운임 = finalFare,
            클라이언트요청Id = clientRequestId,
            상태 = 상태값.의뢰상태.생성됨,
            결제상태 = paymentStatus,
            배차상태 = 상태값.배차상태.미시작,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ledger = await _원장동기화Service.화주운송의뢰동기화Async(entity, currentUserId, cancellationToken);
        if (ledger is null)
        {
            return Result.Fail<화주운송의뢰응답>("운송 의뢰 원장을 저장하지 못했습니다.");
        }

        var projectedEntity = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == entity.의뢰Id, cancellationToken);
        if (projectedEntity is null)
        {
            return Result.Fail<화주운송의뢰응답>(
                $"운송 의뢰 원장은 저장됐지만 RDB 업무 투영이 아직 완료되지 않았습니다. 원장Id={ledger.원장Id}");
        }

        entity = projectedEntity;
        await 화주운송의뢰매퍼.UpsertCargoRequirementAsync(_db, entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (ShouldCreateFareComposition(request))
        {
            var fareComposition = new 운임구성
            {
                의뢰Id = entity.의뢰Id,
                기본운임 = request.기본운임 ?? 0m,
                거리운임 = request.거리운임 ?? 0m,
                할증 = request.할증 ?? 0m,
                대기료 = request.대기료 ?? 0m,
                수작업비 = request.수작업비 ?? 0m,
                최종운임 = entity.최종운임 ?? 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _db.운임구성.AddAsync(fareComposition, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            entity.운임구성Id = fareComposition.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok(화주운송의뢰매퍼.To응답(entity));
    }

    private static bool ShouldCreateFareComposition(의뢰생성Command request)
        => request.최종운임.HasValue
           || request.기본운임.HasValue
           || request.거리운임.HasValue
           || request.최소운임.HasValue
           || request.Km당단가.HasValue
           || request.예상거리Km.HasValue;

    private static string GetSettlementStatus(정산시점 settlementTime, 증빙방식? evidenceMethod)
    {
        return settlementTime switch
        {
            정산시점.현장지급 => 운임정산상태.현장수금예정.ToString(),
            정산시점.운송완료후정산 when evidenceMethod == 증빙방식.인수증 => 운임정산상태.인수증대기.ToString(),
            정산시점.운송완료후정산 => 운임정산상태.청구대기.ToString(),
            정산시점.월말정산 => 운임정산상태.후불승인대기.ToString(),
            _ => 운임정산상태.결제대기.ToString()
        };
    }

    private async Task<(decimal? lat, decimal? lng)> ResolveCoordinatesAsync(string? roadAddress, string? detailAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roadAddress))
        {
            return (null, null);
        }

        var query = roadAddress;
        if (!string.IsNullOrWhiteSpace(detailAddress))
        {
            query += " " + detailAddress;
        }

        var location = await _geocodingService.GeocodeAsync(query);
        if (!location.HasValue)
        {
            return (null, null);
        }

        return (location.Value.lat, location.Value.lng);
    }
}
