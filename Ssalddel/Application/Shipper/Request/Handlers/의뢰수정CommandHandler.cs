using FluentResults;
using Ssalddel.Application.CommandProcessing;
using System.Data;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰수정CommandHandler : IRequestHandler<의뢰수정Command, Result<화주운송의뢰응답>>
{
    private readonly SsalddelContext _db;
    private readonly I화주운송업무담당자UseCase _operatorUseCase;
    private readonly I화주운송기준운임Service? _fareEstimateService;
    private readonly I운송원장Mongo동기화Service? _ledgerSync;
    private readonly ICurrentUserAccessor? _currentUserAccessor;

    public 의뢰수정CommandHandler(
        SsalddelContext db,
        I화주운송업무담당자UseCase operatorUseCase,
        I화주운송기준운임Service? fareEstimateService = null,
        I운송원장Mongo동기화Service? ledgerSync = null,
        ICurrentUserAccessor? currentUserAccessor = null)
    {
        _db = db;
        _operatorUseCase = operatorUseCase;
        _fareEstimateService = fareEstimateService;
        _ledgerSync = ledgerSync;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<화주운송의뢰응답>> Handle(의뢰수정Command request, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var result = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) _db.ChangeTracker.Clear();
            await using var tx = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var saved = await PersistAsync(request, cancellationToken);
            if (saved.IsSuccess && tx is not null) await tx.CommitAsync(cancellationToken);
            return saved;
        });
        if (result.IsSuccess && result.Value.요금옵션 is not null && _ledgerSync is not null)
        {
            // 승인/수락이 먼저 진행되었을 수 있으므로 저장 전의 tracked 객체를 발행하지 않습니다.
            var current = await _db.화주운송의뢰.AsNoTracking()
                .SingleAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
            if (await _ledgerSync.화주운송의뢰동기화Async(current, _currentUserAccessor?.UserId ?? "system", cancellationToken) is null)
                return Result.Fail<화주운송의뢰응답>("견적은 저장됐지만 운송 원장 동기화를 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.");
        }
        return result;
    }

    private async Task<Result<화주운송의뢰응답>> PersistAsync(의뢰수정Command request, CancellationToken cancellationToken)
    {
        // 수락과 같은 운송원장 → 의뢰 순서로 Serializable 읽기를 수행합니다.
        var transport = await _db.운송원장
            .FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId || x.운송번호 == request.RequestId, cancellationToken);
        var entity = await _db.화주운송의뢰.FirstOrDefaultAsync(r => r.의뢰Id == request.RequestId, cancellationToken);
        if (entity == null)
        {
            return Result.Fail<화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }

        var cargoChanged = HasChanges(request.화물정보);
        var pickupChanged = HasChanges(request.픽업지);
        var dropoffChanged = HasChanges(request.하차지);
        var transportChanged = HasChanges(request.운송조건) || request.요청조건.요청사항 is not null;
        var settlementChanged = HasSettlementChanges(request.정산조건, entity);
        var pickupAddressChanged = request.픽업지.도로명주소 is not null && request.픽업지.도로명주소 != entity.픽업_도로명주소;
        var dropoffAddressChanged = request.하차지.도로명주소 is not null && request.하차지.도로명주소 != entity.하차_도로명주소;
        var routeChanged = HasRouteChanges(request, entity);
        var fareComposition = entity.운임구성Id.HasValue
            ? await _db.운임구성.SingleOrDefaultAsync(x => x.Id == entity.운임구성Id.Value && x.의뢰Id == entity.의뢰Id, cancellationToken)
            : null;
        var pricingChanged = HasNumericPricingChanges(request.요금옵션, entity, fareComposition)
                             || request.결제예정금액.HasValue && request.결제예정금액 != entity.결제예정금액
                             || routeChanged && entity.최종운임.HasValue;
        var requiredPermissions = new List<string>(4);
        if (cargoChanged) requiredPermissions.Add(운송업무권한Codes.화물조건수정);
        if (pickupChanged || dropoffChanged) requiredPermissions.Add(운송업무권한Codes.주소시간연락처수정);
        if (transportChanged) requiredPermissions.Add(운송업무권한Codes.배차조건수정);
        if (settlementChanged || pricingChanged) requiredPermissions.Add(운송업무권한Codes.운임정산조건수정);

        if (!await _operatorUseCase.모든권한보유Async(entity, requiredPermissions, cancellationToken))
        {
            return Result.Fail<화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }
        if (request.픽업지.위도.HasValue != request.픽업지.경도.HasValue
            || request.하차지.위도.HasValue != request.하차지.경도.HasValue)
            return Result.Fail<화주운송의뢰응답>("좌표는 위도와 경도를 함께 제공해야 합니다.");

        화주운송기준운임견적응답? estimate = null;
        if (pricingChanged)
        {
            var unapproved = entity.정산상태 is "결제대기" or "청구대기" or "후불승인대기" or "인수증대기";
            if (entity.상태 != 상태값.의뢰상태.생성됨 || entity.결제상태 != 상태값.결제상태.결제대기
                || entity.배차상태 != 상태값.배차상태.미시작 || !unapproved
                || transport is not null && (transport.상태 != "대기"
                    || !string.IsNullOrWhiteSpace(transport.확정기사Id) || !string.IsNullOrWhiteSpace(transport.기사_운송자)))
                return Conflict("FreightPricingLocked", "승인·배차 전 의뢰에서만 견적을 변경할 수 있습니다.");
            if (_fareEstimateService is null || _ledgerSync is null)
                return Result.Fail<화주운송의뢰응답>("운임 견적 저장 서비스를 사용할 수 없습니다.");
            if (request.요금옵션 is { } p && new decimal?[] { p.예상거리Km, p.기본운임, p.Km당단가, p.거리운임,
                    p.최소운임, p.대기료, p.수작업비, p.할증, p.최종운임, p.기사지급예정운임, p.플랫폼수수료 }.Any(x => x < 0m)
                || request.결제예정금액 < 0)
                return Result.Fail<화주운송의뢰응답>("거리와 운임은 음수일 수 없습니다.");
            if (await _db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == entity.의뢰Id
                    && (x.정산보류적용여부 || x.상태Code == 비정상운송사건상태Codes.운영검토대기), cancellationToken))
                return Conflict("FreightPricingHeld", "운영 검토 중에는 견적을 변경할 수 없습니다.");
            if (entity.운임구성Id.HasValue)
            {
                if (fareComposition is null) return Conflict("FreightPricingLinkMismatch", "의뢰의 견적 연결을 확인할 수 없습니다.");
            }
            if ((pickupAddressChanged || dropoffAddressChanged) && request.요금옵션?.예상거리Km is not > 0m
                && (pickupAddressChanged && (!request.픽업지.위도.HasValue || !request.픽업지.경도.HasValue)
                    || dropoffAddressChanged && (!request.하차지.위도.HasValue || !request.하차지.경도.HasValue)))
                return Result.Fail<화주운송의뢰응답>("변경한 주소의 좌표 또는 새 거리 근거가 필요합니다.");
            var quote = await _fareEstimateService.견적Async(new()
            {
                차량종류 = request.운송조건.차량종류 ?? entity.차량종류,
                예상거리Km = request.요금옵션?.예상거리Km ?? (routeChanged ? null : fareComposition?.예상거리Km),
                상차위도 = request.픽업지.위도 ?? (pickupAddressChanged ? null : entity.픽업_위도),
                상차경도 = request.픽업지.경도 ?? (pickupAddressChanged ? null : entity.픽업_경도),
                하차위도 = request.하차지.위도 ?? (dropoffAddressChanged ? null : entity.하차_위도),
                하차경도 = request.하차지.경도 ?? (dropoffAddressChanged ? null : entity.하차_경도),
                대기료 = request.요금옵션?.대기료 ?? entity.대기료,
                수작업비 = request.요금옵션?.수작업비 ?? entity.수작업비,
                할증 = request.요금옵션?.할증 ?? entity.할증
            }, cancellationToken);
            if (quote.IsFailed) return Result.Fail<화주운송의뢰응답>(quote.Errors);
            estimate = quote.Value;
            if (estimate.최종운임 < 0m || estimate.최종운임 > int.MaxValue || estimate.예상거리Km <= 0m)
                return Result.Fail<화주운송의뢰응답>("견적 금액 또는 거리의 저장 범위를 확인해 주세요.");
            var payment = request.결제예정금액 ?? decimal.ToInt32(estimate.최종운임);
            var review = new 화주운송요금정책검토Service().검토(new PricingDTO
            {
                최종운임 = estimate.최종운임, 기사지급예정운임 = request.요금옵션?.기사지급예정운임,
                알선정책 = request.요금옵션?.알선정책, 플랫폼수수료 = request.요금옵션?.플랫폼수수료
            }, payment);
            if (review.정책위반) return Result.Fail<화주운송의뢰응답>(review.경고목록);
        }
        var updated = false;

        if (cargoChanged)
        {
            if (request.화물정보.화물종류 != null) entity.화물종류 = request.화물정보.화물종류;
            if (request.화물정보.화물설명 != null) entity.화물설명 = request.화물정보.화물설명;
            if (request.화물정보.화물수량.HasValue) entity.화물수량 = request.화물정보.화물수량;
            if (request.화물정보.화물중량Kg.HasValue) entity.화물중량Kg = request.화물정보.화물중량Kg;
            if (request.화물정보.화물부피Cbm.HasValue) entity.화물부피Cbm = request.화물정보.화물부피Cbm;
            if (request.화물정보.화물파손주의여부.HasValue) entity.화물파손주의여부 = request.화물정보.화물파손주의여부.Value;
            if (request.화물정보.화물온도조건 != null) entity.화물온도조건 = request.화물정보.화물온도조건;
            updated = true;
        }

        if (pickupChanged)
        {
            if (pickupAddressChanged) { entity.픽업_위도 = null; entity.픽업_경도 = null; }
            if (request.픽업지.도로명주소 != null) entity.픽업_도로명주소 = request.픽업지.도로명주소;
            if (request.픽업지.상세주소 != null) entity.픽업_상세주소 = request.픽업지.상세주소;
            if (request.픽업지.위도.HasValue) entity.픽업_위도 = request.픽업지.위도;
            if (request.픽업지.경도.HasValue) entity.픽업_경도 = request.픽업지.경도;
            if (request.픽업지.연락처이름 != null) entity.픽업_연락처_이름 = request.픽업지.연락처이름;
            if (request.픽업지.연락처전화번호 != null) entity.픽업_연락처_전화번호 = request.픽업지.연락처전화번호;
            if (request.픽업지.시간창시작일시.HasValue && request.픽업지.시간창종료일시.HasValue)
            {
                if (request.픽업지.시간창시작일시 >= request.픽업지.시간창종료일시)
                {
                    return Result.Fail<화주운송의뢰응답>("pickup.window.startAt must be before endAt");
                }

                entity.픽업_시간창_시작일시 = request.픽업지.시간창시작일시.Value;
                entity.픽업_시간창_종료일시 = request.픽업지.시간창종료일시.Value;
            }

            updated = true;
        }

        if (dropoffChanged)
        {
            if (dropoffAddressChanged) { entity.하차_위도 = null; entity.하차_경도 = null; }
            if (request.하차지.도로명주소 != null) entity.하차_도로명주소 = request.하차지.도로명주소;
            if (request.하차지.상세주소 != null) entity.하차_상세주소 = request.하차지.상세주소;
            if (request.하차지.위도.HasValue) entity.하차_위도 = request.하차지.위도;
            if (request.하차지.경도.HasValue) entity.하차_경도 = request.하차지.경도;
            if (request.하차지.연락처이름 != null) entity.하차_연락처_이름 = request.하차지.연락처이름;
            if (request.하차지.연락처전화번호 != null) entity.하차_연락처_전화번호 = request.하차지.연락처전화번호;
            if (request.하차지.시간창시작일시.HasValue && request.하차지.시간창종료일시.HasValue)
            {
                if (request.하차지.시간창시작일시 >= request.하차지.시간창종료일시)
                {
                    return Result.Fail<화주운송의뢰응답>("dropoff.window.startAt must be before endAt");
                }

                entity.하차_시간창_시작일시 = request.하차지.시간창시작일시.Value;
                entity.하차_시간창_종료일시 = request.하차지.시간창종료일시.Value;
            }

            updated = true;
        }

        if (transportChanged)
        {
            if (request.운송조건.운송방식 != null) entity.운송방식 = request.운송조건.운송방식;
            if (request.운송조건.차량종류 != null) entity.차량종류 = request.운송조건.차량종류;
            if (request.운송조건.서비스레벨 != null) entity.서비스레벨 = request.운송조건.서비스레벨;
            if (request.요청조건.요청사항 != null) entity.요청사항 = request.요청조건.요청사항;
            updated = true;
        }

        if (settlementChanged)
        {
            var settlementInput = request.정산조건!;
            var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
                .AsNoTracking()
                .AnyAsync(
                    x => x.운송의뢰Id == entity.의뢰Id
                         && (x.정산보류적용여부
                             || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                    cancellationToken);
            if (hasOpenAbnormalTransportIncident)
            {
                return Result.Fail<화주운송의뢰응답>(
                    "비정상 운송 사건의 운영 검토 중에는 정산 조건을 변경할 수 없습니다.");
            }

            if (!string.IsNullOrWhiteSpace(settlementInput.결제수단))
            {
                entity.결제수단 = settlementInput.결제수단;
            }

            if (settlementInput.정산조건 != null)
            {
                entity.정산시점 = settlementInput.정산조건.정산시점.ToString();
                entity.증빙방식 = settlementInput.정산조건.증빙방식.ToString();
                entity.수납주체 = settlementInput.정산조건.수납주체.ToString();
                entity.정산메모 = settlementInput.정산조건.정산메모 ?? string.Empty;
                entity.세금계산서필요 = settlementInput.정산조건.세금계산서필요;
                entity.현금영수증필요 = settlementInput.정산조건.현금영수증필요;
                entity.정산상태 = GetSettlementStatus(settlementInput.정산조건.정산시점, settlementInput.정산조건.증빙방식);
            }

            updated = true;
        }

        if (!updated)
        {
            if (!pricingChanged) return Result.Fail<화주운송의뢰응답>("수정할 필드를 하나 이상 제공해야 합니다.");
        }

        entity.UpdatedAt = DateTime.UtcNow;
        if (estimate is not null)
        {
            entity.최종운임 = estimate.최종운임;
            entity.결제예정금액 = request.결제예정금액 ?? decimal.ToInt32(estimate.최종운임);
            entity.대기료 = estimate.대기료;
            entity.수작업비 = estimate.수작업비;
            entity.할증 = estimate.할증;
            fareComposition ??= new 운임구성 { 의뢰Id = entity.의뢰Id, CreatedAt = entity.UpdatedAt };
            fareComposition.기본운임 = estimate.기본운임;
            fareComposition.거리운임 = estimate.거리운임;
            fareComposition.대기료 = estimate.대기료;
            fareComposition.수작업비 = estimate.수작업비;
            fareComposition.할증 = estimate.할증;
            fareComposition.최종운임 = estimate.최종운임;
            fareComposition.예상거리Km = estimate.예상거리Km;
            fareComposition.Km당단가 = estimate.Km당단가;
            fareComposition.최소운임 = estimate.최소운임;
            fareComposition.거리계산방식 = estimate.거리계산방식;
            fareComposition.단가출처 = estimate.단가출처;
            if (request.요금옵션?.기사지급예정운임 is { } driverFare) fareComposition.기사지급예정운임 = driverFare;
            fareComposition.UpdatedAt = entity.UpdatedAt;
            if (transport is not null)
            {
                transport.운임 = estimate.최종운임;
                transport.픽업_도로명주소 = entity.픽업_도로명주소;
                transport.픽업_상세주소 = entity.픽업_상세주소;
                transport.픽업_위도 = entity.픽업_위도;
                transport.픽업_경도 = entity.픽업_경도;
                transport.하차_도로명주소 = entity.하차_도로명주소;
                transport.하차_상세주소 = entity.하차_상세주소;
                transport.하차_위도 = entity.하차_위도;
                transport.하차_경도 = entity.하차_경도;
                transport.출발지 = entity.픽업_도로명주소;
                transport.도착지 = entity.하차_도로명주소;
                transport.UpdatedAt = entity.UpdatedAt;
            }
            if (fareComposition.Id == 0) _db.운임구성.Add(fareComposition);
            await _db.SaveChangesAsync(cancellationToken);
            entity.운임구성Id = fareComposition.Id;
        }
        await 화주운송의뢰매퍼.UpsertCargoRequirementAsync(_db, entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(화주운송의뢰매퍼.To응답(entity, fareComposition: fareComposition));
    }

    private static bool HasNumericPricingChanges(PricingDTO? p, 화주운송의뢰 e, 운임구성? f)
        => p is not null && (Diff(p.예상거리Km, f?.예상거리Km) || Diff(p.기본운임, f?.기본운임)
            || Diff(p.Km당단가, f?.Km당단가) || Diff(p.거리운임, f?.거리운임) || Diff(p.최소운임, f?.최소운임)
            || Diff(p.대기료, e.대기료 ?? 0m) || Diff(p.수작업비, e.수작업비 ?? 0m) || Diff(p.할증, e.할증 ?? 0m)
            || Diff(p.최종운임, e.최종운임) || Diff(p.기사지급예정운임, f?.기사지급예정운임 ?? e.최종운임)
            || p.플랫폼수수료 is not null and not 0m
            || p.알선정책 is { } policy && (policy.알선단계 > 1 || !policy.재알선금지));

    private static bool Diff(decimal? input, decimal? current) => input.HasValue && input != current;

    private static bool HasSettlementChanges(정산조건입력값? input, 화주운송의뢰 e)
        => input is not null && (!string.IsNullOrWhiteSpace(input.결제수단) && input.결제수단 != e.결제수단
            || input.정산조건 is { } s && (s.정산시점.ToString() != e.정산시점 || s.증빙방식.ToString() != e.증빙방식
                || s.수납주체.ToString() != e.수납주체 || (s.정산메모 ?? string.Empty) != e.정산메모
                || s.세금계산서필요 != e.세금계산서필요 || s.현금영수증필요 != e.현금영수증필요));

    private static bool HasRouteChanges(의뢰수정Command r, 화주운송의뢰 e)
        => r.운송조건.차량종류 is not null && r.운송조건.차량종류 != e.차량종류
            || r.픽업지.도로명주소 is not null && r.픽업지.도로명주소 != e.픽업_도로명주소
            || r.하차지.도로명주소 is not null && r.하차지.도로명주소 != e.하차_도로명주소
            || r.픽업지.위도.HasValue && r.픽업지.위도 != e.픽업_위도
            || r.픽업지.경도.HasValue && r.픽업지.경도 != e.픽업_경도
            || r.하차지.위도.HasValue && r.하차지.위도 != e.하차_위도
            || r.하차지.경도.HasValue && r.하차지.경도 != e.하차_경도;

    private static Result<화주운송의뢰응답> Conflict(string code, string message)
        => Result.Fail<화주운송의뢰응답>(new Error(message).WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", code));

    private static string GetSettlementStatus(정산시점 settlementTime, 증빙방식 evidenceMethod)
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

    private static bool HasChanges(화물정보입력값 value)
        => value.화물종류 is not null
           || value.화물설명 is not null
           || value.화물수량.HasValue
           || value.화물중량Kg.HasValue
           || value.화물부피Cbm.HasValue
           || value.화물파손주의여부.HasValue
           || value.화물온도조건 is not null;

    private static bool HasChanges(위치정보입력값 value)
        => value.도로명주소 is not null
           || value.상세주소 is not null
           || value.위도.HasValue
           || value.경도.HasValue
           || value.연락처이름 is not null
           || value.연락처전화번호 is not null
           || value.시간창시작일시.HasValue
           || value.시간창종료일시.HasValue;

    private static bool HasChanges(운송조건입력값 value)
        => value.운송방식 is not null
           || value.차량종류 is not null
           || value.서비스레벨 is not null;
}
