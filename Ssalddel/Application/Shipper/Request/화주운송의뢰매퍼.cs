using ShipRequest = Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Shipper.Request;

internal static class 화주운송의뢰매퍼
{
    internal static ShipRequest.화주운송의뢰응답 To응답(
        화주운송의뢰 entity,
        운송원장? transportLedger = null,
        용달기사? driver = null,
        기사위치기록? driverLocation = null,
        운영체제업무인계Dto? operatingSystemHandoff = null,
        운영체제업무인계Dto? completionHandoff = null,
        IReadOnlyList<ShipRequest.비정상운송사건Dto>? abnormalTransportIncidents = null)
    {
        ShipRequest.정산시점? settlementTime = Enum.TryParse<ShipRequest.정산시점>(entity.정산시점, ignoreCase: false, out var parsedSettlementTime)
            ? parsedSettlementTime
            : (ShipRequest.정산시점?)null;
        ShipRequest.증빙방식? evidenceMethod = Enum.TryParse<ShipRequest.증빙방식>(entity.증빙방식, ignoreCase: false, out var parsedEvidenceMethod)
            ? parsedEvidenceMethod
            : (ShipRequest.증빙방식?)null;
        ShipRequest.수납주체? collector = Enum.TryParse<ShipRequest.수납주체>(entity.수납주체, ignoreCase: false, out var parsedCollector)
            ? parsedCollector
            : (ShipRequest.수납주체?)null;

        return new ShipRequest.화주운송의뢰응답
        {
            의뢰Id = entity.의뢰Id,
            주문자UserId = entity.주문자UserId,
            화주Id = entity.화주Id,
            의뢰상태 = entity.상태,
            결제상태 = entity.결제상태,
            정산상태 = entity.정산상태,
            배차상태 = entity.배차상태,
            운송상태 = transportLedger?.상태 ?? string.Empty,
            운영체제인계 = operatingSystemHandoff,
            운송완료화주인계 = completionHandoff,
            비정상운송검토보류중 = abnormalTransportIncidents?.Any(
                x => x.정산보류적용여부
                     || string.Equals(
                         x.상태Code,
                         비정상운송사건상태Codes.운영검토대기,
                         StringComparison.Ordinal)) == true,
            비정상운송사건목록 = abnormalTransportIncidents ?? Array.Empty<ShipRequest.비정상운송사건Dto>(),
            운송원장갱신일시Utc = transportLedger?.UpdatedAt,
            확정기사Id = transportLedger?.확정기사Id,
            확정기사명 = driver?.기사명,
            확정기사차량 = driver?.차량,
            기사최근위도 = driverLocation?.위도,
            기사최근경도 = driverLocation?.경도,
            기사최근위치시각Utc = driverLocation?.기록시각,
            운송방식 = entity.운송방식,
            차량종류 = entity.차량종류,
            결제수단 = entity.결제수단,
            결제예정금액 = entity.결제예정금액,
            정산시점 = settlementTime,
            증빙방식 = evidenceMethod,
            수납주체 = collector,
            세금계산서필요 = entity.세금계산서필요,
            현금영수증필요 = entity.현금영수증필요,
            정산메모 = entity.정산메모,
            인수증번호 = entity.인수증번호,
            인수증등록일시 = entity.인수증등록일시,
            현장수금확인일시 = entity.현장수금확인일시,
            현장지급메모 = entity.현장지급메모,
            생성일시 = entity.CreatedAt,
            화물길이Mm = entity.화물길이Mm,
            화물폭Mm = entity.화물폭Mm,
            화물높이Mm = entity.화물높이Mm,
            팔레트개수 = entity.화물팔레트개수,
            픽업지 = entity.픽업_도로명주소,
            픽업상세지 = entity.픽업_상세주소,
            픽업위도 = entity.픽업_위도,
            픽업경도 = entity.픽업_경도,
            하차지 = entity.하차_도로명주소,
            하차상세지 = entity.하차_상세주소,
            하차위도 = entity.하차_위도,
            하차경도 = entity.하차_경도,
            대기료 = entity.대기료,
            수작업비 = entity.수작업비,
            할증 = entity.할증,
            최종운임 = entity.최종운임,
            요약 = new ShipRequest.화주운송의뢰응답.요약DTO
            {
                화물종류 = entity.화물종류,
                픽업지 = entity.픽업_도로명주소,
                하차지 = entity.하차_도로명주소
            }
        };
    }

    internal static ShipRequest.공개화물요약응답 To공개화물요약응답(화주운송의뢰 entity)
    {
        return new ShipRequest.공개화물요약응답
        {
            의뢰Id = entity.의뢰Id,
            화물종류 = entity.화물종류,
            화물수량 = entity.화물수량,
            화물중량Kg = entity.화물중량Kg,
            운송방식 = entity.운송방식,
            차량종류 = entity.차량종류,
            의뢰상태 = entity.상태,
            배차상태 = entity.배차상태,
            생성일시 = entity.CreatedAt
        };
    }

    internal static async Task UpsertCargoRequirementAsync(SsalddelContext db, 살뜰.도메인.화주.화주운송의뢰 entity, CancellationToken cancellationToken)
    {
        var cargo = await db.화물요구조건.FirstOrDefaultAsync(x => x.의뢰Id == entity.의뢰Id, cancellationToken);
        if (cargo == null)
        {
            cargo = new 살뜰.도메인.화물.화물요구조건 { 의뢰Id = entity.의뢰Id };
            db.화물요구조건.Add(cargo);
        }

        var mergedText = string.Join(' ', new[] { entity.운송방식, entity.서비스레벨, entity.요청사항, entity.화물종류, entity.화물설명 }
            .Where(x => !string.IsNullOrWhiteSpace(x))!);

        cargo.화물무게Kg = entity.화물중량Kg.HasValue ? (int?)Math.Ceiling(entity.화물중량Kg.Value) : null;
        cargo.화물길이Mm = entity.화물길이Mm;
        cargo.화물폭Mm = entity.화물폭Mm;
        cargo.화물높이Mm = entity.화물높이Mm;
        cargo.팔레트개수 = entity.화물팔레트개수;
        cargo.비맞으면안됨 = mergedText.Contains("비", StringComparison.OrdinalIgnoreCase) || mergedText.Contains("방수", StringComparison.OrdinalIgnoreCase);
        cargo.냉장필요 = string.Equals(entity.화물온도조건, "냉장", StringComparison.OrdinalIgnoreCase);
        cargo.냉동필요 = string.Equals(entity.화물온도조건, "냉동", StringComparison.OrdinalIgnoreCase);
        cargo.리프트필요 = mergedText.Contains("리프트", StringComparison.OrdinalIgnoreCase);
        cargo.측면상하차필요 = mergedText.Contains("측면", StringComparison.OrdinalIgnoreCase);
        cargo.장재물 = mergedText.Contains("장재물", StringComparison.OrdinalIgnoreCase);
        cargo.혼적허용 = !mergedText.Contains("단독", StringComparison.OrdinalIgnoreCase);
        cargo.독차필수 = mergedText.Contains("단독", StringComparison.OrdinalIgnoreCase);
        cargo.주의사항 = entity.화물설명;
        cargo.UpdatedAt = DateTime.UtcNow;
        cargo.CreatedAt = cargo.CreatedAt == default ? DateTime.UtcNow : cargo.CreatedAt;

        entity.화물길이Mm = cargo.화물길이Mm;
        entity.화물폭Mm = cargo.화물폭Mm;
        entity.화물높이Mm = cargo.화물높이Mm;
        entity.화물팔레트개수 = cargo.팔레트개수;
    }
}
