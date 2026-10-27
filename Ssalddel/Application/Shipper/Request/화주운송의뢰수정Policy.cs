using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>수정 입력과 현재 스냅샷을 비교한다. 권한 조회·저장·견적 재계산은 수행하지 않는다.</summary>
internal static class 화주운송의뢰수정Policy
{
    internal static bool HasNumericPricingChanges(PricingDTO? p, 화주운송의뢰 e, 운임구성? f)
        => p is not null && (Diff(p.예상거리Km, f?.예상거리Km) || Diff(p.기본운임, f?.기본운임)
            || Diff(p.Km당단가, f?.Km당단가) || Diff(p.거리운임, f?.거리운임) || Diff(p.최소운임, f?.최소운임)
            || Diff(p.대기료, e.대기료 ?? 0m) || Diff(p.수작업비, e.수작업비 ?? 0m) || Diff(p.할증, e.할증 ?? 0m)
            || Diff(p.최종운임, e.최종운임) || Diff(p.기사지급예정운임, f?.기사지급예정운임 ?? e.최종운임)
            || p.플랫폼수수료 is not null and not 0m
            || p.알선정책 is { } policy && (policy.알선단계 > 1 || !policy.재알선금지));

    private static bool Diff(decimal? input, decimal? current) => input.HasValue && input != current;

    internal static bool HasSettlementChanges(정산조건입력값? input, 화주운송의뢰 e)
        => input is not null && (!string.IsNullOrWhiteSpace(input.결제수단) && input.결제수단 != e.결제수단
            || input.정산조건 is { } s && (s.정산시점.ToString() != e.정산시점 || s.증빙방식.ToString() != e.증빙방식
                || s.수납주체.ToString() != e.수납주체 || (s.정산메모 ?? string.Empty) != e.정산메모
                || s.세금계산서필요 != e.세금계산서필요 || s.현금영수증필요 != e.현금영수증필요));

    internal static bool CanChangeCoreConditions(화주운송의뢰 entity, 운송원장? transport)
        => entity.상태 == 상태값.의뢰상태.생성됨
            && entity.결제상태 == 상태값.결제상태.결제대기
            && entity.배차상태 == 상태값.배차상태.미시작
            && (entity.정산상태 is "결제대기" or "청구대기" or "후불승인대기" or "인수증대기" or "현장수금예정")
            && (transport is null || transport.상태 == "대기"
                && string.IsNullOrWhiteSpace(transport.확정기사Id) && string.IsNullOrWhiteSpace(transport.기사_운송자));

    internal static bool HasCoreConditionChanges(의뢰수정Command r, 화주운송의뢰 e)
        => HasRouteChanges(r, e)
            || r.픽업지.작업시간입력초기화 && e.픽업_작업시간분.HasValue
            || r.하차지.작업시간입력초기화 && e.하차_작업시간분.HasValue
            || r.픽업지.작업시간분.HasValue && r.픽업지.작업시간분 != e.픽업_작업시간분
            || r.하차지.작업시간분.HasValue && r.하차지.작업시간분 != e.하차_작업시간분
            || r.운송조건.운송방식 is not null && r.운송조건.운송방식 != e.운송방식
            || r.운송조건.서비스레벨 is not null && r.운송조건.서비스레벨 != e.서비스레벨
            || r.요청조건.요청사항 is not null && r.요청조건.요청사항 != e.요청사항
            || HasCargoConditionChanges(r.화물정보, e)
            || r.픽업지.상세주소 is not null && r.픽업지.상세주소 != e.픽업_상세주소
            || r.하차지.상세주소 is not null && r.하차지.상세주소 != e.하차_상세주소
            || r.픽업지.시간창시작일시.HasValue && r.픽업지.시간창시작일시 != e.픽업_시간창_시작일시
            || r.픽업지.시간창종료일시.HasValue && r.픽업지.시간창종료일시 != e.픽업_시간창_종료일시
            || r.하차지.시간창시작일시.HasValue && r.하차지.시간창시작일시 != e.하차_시간창_시작일시
            || r.하차지.시간창종료일시.HasValue && r.하차지.시간창종료일시 != e.하차_시간창_종료일시
            || HasSettlementChanges(r.정산조건, e);

    internal static bool HasCargoConditionChanges(화물정보입력값 cargo, 화주운송의뢰 e)
        => cargo.화물종류 is not null && cargo.화물종류 != e.화물종류
            || cargo.화물설명 is not null && cargo.화물설명 != e.화물설명
            || cargo.화물수량.HasValue && cargo.화물수량 != e.화물수량
            || cargo.화물길이Mm.HasValue && cargo.화물길이Mm != e.화물길이Mm
            || cargo.화물폭Mm.HasValue && cargo.화물폭Mm != e.화물폭Mm
            || cargo.화물높이Mm.HasValue && cargo.화물높이Mm != e.화물높이Mm
            || cargo.팔레트개수.HasValue && cargo.팔레트개수 != e.화물팔레트개수
            || cargo.화물중량Kg.HasValue && cargo.화물중량Kg != e.화물중량Kg
            || 화주운송의뢰매퍼.VolumeInputChanged(cargo, e)
            || cargo.화물바닥회전가능여부.HasValue && cargo.화물바닥회전가능여부 != e.화물바닥회전가능여부
            || cargo.화물적층가능여부.HasValue && cargo.화물적층가능여부 != e.화물적층가능여부
            || cargo.화물파손주의여부.HasValue && cargo.화물파손주의여부 != e.화물파손주의여부
            || cargo.화물온도조건 is not null && cargo.화물온도조건 != e.화물온도조건;

    internal static bool HasRouteChanges(의뢰수정Command r, 화주운송의뢰 e)
        => r.운송조건.차량종류 is not null && r.운송조건.차량종류 != e.차량종류
            || r.픽업지.도로명주소 is not null && r.픽업지.도로명주소 != e.픽업_도로명주소
            || r.하차지.도로명주소 is not null && r.하차지.도로명주소 != e.하차_도로명주소
            || r.픽업지.위도.HasValue && r.픽업지.위도 != e.픽업_위도
            || r.픽업지.경도.HasValue && r.픽업지.경도 != e.픽업_경도
            || r.하차지.위도.HasValue && r.하차지.위도 != e.하차_위도
            || r.하차지.경도.HasValue && r.하차지.경도 != e.하차_경도;

    internal static bool HasChanges(화물정보입력값 value)
        => value.화물종류 is not null
           || value.화물설명 is not null
           || value.화물수량.HasValue
           || value.화물길이Mm.HasValue
           || value.화물폭Mm.HasValue
           || value.화물높이Mm.HasValue
           || value.팔레트개수.HasValue
           || value.화물중량Kg.HasValue
           || value.화물부피Cbm.HasValue
           || value.부피입력초기화
           || value.화물바닥회전가능여부.HasValue
           || value.화물적층가능여부.HasValue
           || value.화물파손주의여부.HasValue
           || value.화물온도조건 is not null;

    internal static bool HasChanges(위치정보입력값 value)
        => value.도로명주소 is not null
           || value.상세주소 is not null
           || value.위도.HasValue
           || value.경도.HasValue
           || value.연락처이름 is not null
           || value.연락처전화번호 is not null
           || value.시간창시작일시.HasValue
           || value.시간창종료일시.HasValue
           || value.작업시간분.HasValue
           || value.작업시간입력초기화;

    internal static bool HasChanges(운송조건입력값 value)
        => value.운송방식 is not null
           || value.차량종류 is not null
           || value.서비스레벨 is not null;
}
