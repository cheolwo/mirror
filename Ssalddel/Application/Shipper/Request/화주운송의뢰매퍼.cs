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
        IReadOnlyList<ShipRequest.비정상운송사건Dto>? abnormalTransportIncidents = null,
        운임구성? fareComposition = null)
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
            화물 = new ShipRequest.CargoDTO
            {
                화물종류 = entity.화물종류,
                설명 = entity.화물설명,
                수량 = entity.화물수량,
                길이Mm = entity.화물길이Mm,
                폭Mm = entity.화물폭Mm,
                높이Mm = entity.화물높이Mm,
                중량Kg = entity.화물중량Kg,
                부피Cbm = entity.화물부피Cbm,
                팔레트개수 = entity.화물팔레트개수,
                화물파손주의여부 = entity.화물파손주의여부,
                온도조건 = entity.화물온도조건,
                바닥회전가능여부 = entity.화물바닥회전가능여부,
                적층가능여부 = entity.화물적층가능여부
            },
            적재조건 = ReadLoadingEvidence(entity),
            화물길이Mm = entity.화물길이Mm,
            화물폭Mm = entity.화물폭Mm,
            화물높이Mm = entity.화물높이Mm,
            팔레트개수 = entity.화물팔레트개수,
            픽업지 = entity.픽업_도로명주소,
            픽업상세지 = entity.픽업_상세주소,
            픽업 = To위치연락처(
                entity.픽업_도로명주소, entity.픽업_상세주소, entity.픽업_위도, entity.픽업_경도,
                entity.픽업_연락처_이름, entity.픽업_연락처_전화번호,
                entity.픽업_시간창_시작일시, entity.픽업_시간창_종료일시, entity.픽업_작업시간분),
            픽업위도 = entity.픽업_위도,
            픽업경도 = entity.픽업_경도,
            하차지 = entity.하차_도로명주소,
            하차상세지 = entity.하차_상세주소,
            하차 = To위치연락처(
                entity.하차_도로명주소, entity.하차_상세주소, entity.하차_위도, entity.하차_경도,
                entity.하차_연락처_이름, entity.하차_연락처_전화번호,
                entity.하차_시간창_시작일시, entity.하차_시간창_종료일시, entity.하차_작업시간분),
            하차위도 = entity.하차_위도,
            하차경도 = entity.하차_경도,
            대기료 = entity.대기료,
            수작업비 = entity.수작업비,
            할증 = entity.할증,
            최종운임 = entity.최종운임,
            요금옵션 = fareComposition is null ? null : new ShipRequest.PricingDTO
            {
                AgreedRateTable = ReadAgreement(fareComposition.합의요금표SnapshotJson),
                AgreedRateTableEvidenceStatusCode = string.IsNullOrEmpty(fareComposition.합의요금표SnapshotJson) ? "NotSelected"
                    : ReadAgreement(fareComposition.합의요금표SnapshotJson) is null ? "InvalidEvidence" : "StoredAcceptedAgreement",
                예상거리Km = fareComposition.예상거리Km, Km당단가 = fareComposition.Km당단가,
                기본운임 = fareComposition.기본운임, 거리운임 = fareComposition.거리운임,
                최소운임 = fareComposition.최소운임, 최종운임 = fareComposition.최종운임,
                대기료 = fareComposition.대기료, 수작업비 = fareComposition.수작업비, 할증 = fareComposition.할증,
                기사지급예정운임 = fareComposition.기사지급예정운임,
                거리계산방식 = fareComposition.거리계산방식, 단가출처 = fareComposition.단가출처
            },
            요약 = new ShipRequest.화주운송의뢰응답.요약DTO
            {
                화물종류 = entity.화물종류,
                픽업지 = entity.픽업_도로명주소,
                하차지 = entity.하차_도로명주소
            }
        };
    }

    private static ShipRequest.LocationContactDTO? To위치연락처(
        string roadAddress, string detailedAddress, decimal? latitude, decimal? longitude,
        string contactName, string contactPhone, DateTime? windowStart, DateTime? windowEnd, decimal? handlingMinutes)
    {
        var hasWindow = windowStart.HasValue && windowStart.Value != default
                        && windowEnd.HasValue && windowEnd.Value != default;
        if (string.IsNullOrWhiteSpace(roadAddress) && string.IsNullOrWhiteSpace(detailedAddress)
            && !latitude.HasValue && !longitude.HasValue
            && string.IsNullOrWhiteSpace(contactName) && string.IsNullOrWhiteSpace(contactPhone)
            && !hasWindow && !handlingMinutes.HasValue)
        {
            return null;
        }

        return new ShipRequest.LocationContactDTO
        {
            작업시간분 = handlingMinutes,
            주소 = new ShipRequest.AddressDTO
            {
                도로명주소 = roadAddress,
                상세주소 = detailedAddress,
                위도 = latitude,
                경도 = longitude
            },
            연락처 = new ShipRequest.ContactDTO { 이름 = contactName, 전화번호 = contactPhone },
            시간창 = hasWindow
                ? new ShipRequest.TimeWindowDTO { 시작일시 = windowStart!.Value, 종료일시 = windowEnd!.Value }
                : null
        };
    }

    internal static 살뜰.Services.Dispatch.Recommendation.화물상하차작업시간 ReadHandlingTime(화주운송의뢰 entity)
        => new(entity.픽업_작업시간분, entity.하차_작업시간분);

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

    internal static ShipRequest.화물적재입력결과 CalculateLoadingEvidence(의뢰생성Command request)
        => ShipRequest.화물적재입력Policy.계산(new ShipRequest.화물적재입력
        {
            화물종류 = request.화물종류, 설명 = request.화물설명, 수량 = request.화물수량,
            길이Mm = request.화물길이Mm, 폭Mm = request.화물폭Mm, 높이Mm = request.화물높이Mm,
            총중량Kg = request.화물중량Kg, 입력부피Cbm = request.화물부피Cbm, 팔레트개수 = request.팔레트개수,
            온도조건 = request.화물온도조건, 파손주의여부 = request.화물파손주의여부,
            바닥회전가능여부 = request.화물바닥회전가능여부, 적층가능여부 = request.화물적층가능여부,
            운송방식 = request.운송방식, 서비스레벨 = request.서비스레벨, 요청사항 = request.요청사항
        });

    internal static ShipRequest.화물적재입력결과 ReadLoadingEvidence(화주운송의뢰 entity)
    {
        var result = ShipRequest.화물적재입력Policy.계산(LoadingInput(entity));
        // 조회는 과거 원입력/판본을 소급 확인하거나 현재 계산으로 저장값을 바꾸는 명령이 아닙니다.
        return ShipRequest.화물적재입력Policy.저장값적용(result, entity.화물부피Cbm, entity.화물부피근거Code, entity.화물적재계산판본);
    }

    internal static bool VolumeInputChanged(화물정보입력값 cargo, 화주운송의뢰 entity)
        => cargo.부피입력초기화
            ? entity.화물입력부피Cbm.HasValue || entity.화물적재계산판본 != ShipRequest.화물적재입력Policy.CurrentRevision && entity.화물부피Cbm.HasValue
            : cargo.화물부피Cbm.HasValue && cargo.화물부피Cbm != entity.화물부피Cbm && cargo.화물부피Cbm != entity.화물입력부피Cbm;

    internal static ShipRequest.화물적재입력결과 CalculateUpdatedLoadingEvidence(화주운송의뢰 entity, 의뢰수정Command request)
    {
        var cargo = request.화물정보;
        var echoedVolume = cargo.화물부피Cbm.HasValue && cargo.화물부피Cbm == entity.화물부피Cbm;
        var rawVolume = cargo.부피입력초기화 ? null
            : cargo.화물부피Cbm.HasValue && !echoedVolume ? cargo.화물부피Cbm : entity.화물입력부피Cbm;
        var input = LoadingInput(entity) with
        {
            화물종류 = cargo.화물종류 ?? entity.화물종류, 설명 = cargo.화물설명 ?? entity.화물설명,
            수량 = cargo.화물수량 ?? entity.화물수량,
            길이Mm = cargo.화물길이Mm ?? entity.화물길이Mm, 폭Mm = cargo.화물폭Mm ?? entity.화물폭Mm,
            높이Mm = cargo.화물높이Mm ?? entity.화물높이Mm, 총중량Kg = cargo.화물중량Kg ?? entity.화물중량Kg,
            입력부피Cbm = rawVolume, 팔레트개수 = cargo.팔레트개수 ?? entity.화물팔레트개수,
            온도조건 = cargo.화물온도조건 ?? entity.화물온도조건, 파손주의여부 = cargo.화물파손주의여부 ?? entity.화물파손주의여부,
            바닥회전가능여부 = cargo.화물바닥회전가능여부 ?? entity.화물바닥회전가능여부,
            적층가능여부 = cargo.화물적층가능여부 ?? entity.화물적층가능여부,
            운송방식 = request.운송조건.운송방식 ?? entity.운송방식,
            서비스레벨 = request.운송조건.서비스레벨 ?? entity.서비스레벨,
            요청사항 = request.요청조건.요청사항 ?? entity.요청사항
        };
        var result = ShipRequest.화물적재입력Policy.계산(input);
        if (entity.화물적재계산판본 != ShipRequest.화물적재입력Policy.CurrentRevision && entity.화물부피Cbm.HasValue
            && !cargo.부피입력초기화 && (!cargo.화물부피Cbm.HasValue || echoedVolume))
        {
            result = result with
            {
                유효부피Cbm = Math.Max(entity.화물부피Cbm.Value, result.유효부피Cbm ?? 0m),
                부피근거Code = entity.화물부피근거Code ?? "LegacyUnverified", 계산판본 = entity.화물적재계산판본 ?? string.Empty,
                경고 = result.경고.Concat(["구형 유효 부피를 원입력으로 승격하지 않고 보수적으로 보존했습니다."]).ToArray()
            };
        }
        return result;
    }

    internal static void ApplyLoadingEvidence(화주운송의뢰 entity, ShipRequest.화물적재입력결과 result)
    {
        entity.화물부피Cbm = result.유효부피Cbm;
        entity.화물입력부피Cbm = result.입력.입력부피Cbm;
        entity.화물부피근거Code = result.부피근거Code;
        entity.화물적재계산판본 = string.IsNullOrEmpty(result.계산판본) ? null : result.계산판본;
        entity.화물바닥회전가능여부 = result.입력.바닥회전가능여부;
        entity.화물적층가능여부 = result.입력.적층가능여부;
    }

    private static ShipRequest.화물적재입력 LoadingInput(화주운송의뢰 entity) => new()
    {
        화물종류 = entity.화물종류, 설명 = entity.화물설명, 수량 = entity.화물수량,
        길이Mm = entity.화물길이Mm, 폭Mm = entity.화물폭Mm, 높이Mm = entity.화물높이Mm,
        총중량Kg = entity.화물중량Kg, 입력부피Cbm = entity.화물입력부피Cbm, 팔레트개수 = entity.화물팔레트개수,
        온도조건 = entity.화물온도조건, 파손주의여부 = entity.화물파손주의여부,
        바닥회전가능여부 = entity.화물바닥회전가능여부, 적층가능여부 = entity.화물적층가능여부,
        운송방식 = entity.운송방식, 서비스레벨 = entity.서비스레벨, 요청사항 = entity.요청사항
    };

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

        var roundedWeight = entity.화물중량Kg.HasValue ? Math.Ceiling(entity.화물중량Kg.Value) : (decimal?)null;
        // 정수 투영의 범위를 넘더라도 접수된 decimal 원중량을 손상하거나 쓰기 후 예외를 만들지 않습니다.
        cargo.화물무게Kg = roundedWeight.HasValue && roundedWeight.Value >= int.MinValue && roundedWeight.Value <= int.MaxValue
            ? decimal.ToInt32(roundedWeight.Value) : null;
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
    private static Ssalddel.Contracts.Common.Pricing.AgreedRateTableSnapshotDto? ReadAgreement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<Ssalddel.Contracts.Common.Pricing.AgreedRateTableSnapshotDto>(json,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            return Ssalddel.Domain.Pricing.AgreedRateTableEvidencePolicy.IsValid(snapshot)
                && snapshot!.BusinessKind == Ssalddel.Contracts.Common.Pricing.AgreedRateBusinessKinds.CargoTransport ? snapshot : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
