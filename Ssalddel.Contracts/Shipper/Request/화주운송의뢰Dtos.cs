using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.Operations;

namespace Ssalddel.Contracts.Shipper.Request;

public enum 정산시점
{
    선결제,
    현장지급,
    운송완료후정산,
    월말정산
}

public enum 결제수단
{
    카드,
    가상계좌,
    계좌이체,
    현금,
    별도정산
}

public enum 증빙방식
{
    없음,
    인수증,
    현금영수증,
    세금계산서
}

public enum 수납주체
{
    플랫폼,
    기사,
    화주직접
}

public enum 운임정산상태
{
    정산조건작성됨,
    결제대기,
    결제완료,
    현장수금예정,
    현장수금완료,
    후불승인대기,
    후불승인완료,
    인수증대기,
    인수증등록완료,
    청구대기,
    입금대기,
    입금확인완료,
    비정상운송검토보류,
    정산완료,
    정산취소,
    미수발생
}

public sealed class 화주운송정산조건DTO
{
    public 정산시점 정산시점 { get; set; } = 정산시점.선결제;
    public 결제수단 결제수단 { get; set; } = 결제수단.카드;
    public 증빙방식 증빙방식 { get; set; } = 증빙방식.없음;
    public 수납주체 수납주체 { get; set; } = 수납주체.플랫폼;
    public bool 세금계산서필요 { get; set; }
    public bool 현금영수증필요 { get; set; }
    public string? 정산메모 { get; set; }
}

public sealed class 화주운송의뢰생성요청
{
    public Guid? 신청개인정보동의증적Id { get; set; }
    public string 신청출처Code { get; set; } = string.Empty;
    public string? 화주Id { get; set; }
    public string? 운송방식 { get; set; }
    public string? 차량종류 { get; set; }
    public string? 결제수단 { get; set; }
    public int? 결제예정금액 { get; set; }
    public 화주운송정산조건DTO? 정산조건 { get; set; }
    public CargoDTO 화물 { get; set; } = new();
    public LocationContactDTO? 픽업 { get; set; }
    public LocationContactDTO? 하차 { get; set; }
    public PricingDTO? 요금옵션 { get; set; }
    public string? 클라이언트요청Id { get; set; }
    public string? 결제상태 { get; set; }
}

public sealed class 차량추천요청
{
    public string? 화물종류 { get; set; }
    public int? 화물수량 { get; set; }
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public decimal? 화물부피Cbm { get; set; }
    public int? 팔레트개수 { get; set; }
    public string? 화물온도조건 { get; set; }
    public bool 화물파손주의여부 { get; set; }
    public bool 화물바닥회전가능여부 { get; set; } = true;
    public bool 화물적층가능여부 { get; set; } = true;
}

public sealed class 차량추천응답
{
    public string 추천차량종류 { get; set; } = string.Empty;
    public decimal? 추정화물부피Cbm { get; set; }
    public IReadOnlyList<string> 추천사유 { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> 경고목록 { get; set; } = Array.Empty<string>();
    public IReadOnlyList<차량추천후보응답> 후보목록 { get; set; } = Array.Empty<차량추천후보응답>();
}

public sealed class 차량추천후보응답
{
    public string 차량코드 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public int 우선순위 { get; set; }
    public decimal 적재가능중량Kg { get; set; }
    public decimal? 적재가능부피Cbm { get; set; }
    public int? 적재가능팔레트개수 { get; set; }
    public bool 단일운송가능여부 { get; set; } = true;
    public int 권장운행횟수 { get; set; } = 1;
    public decimal? 중량사용률Percent { get; set; }
    public decimal? 부피사용률Percent { get; set; }
    public decimal? 팔레트사용률Percent { get; set; }
    public string 설명 { get; set; } = string.Empty;
}

public sealed class 화주운송의뢰수정요청
{
    public string? 운송방식 { get; set; }
    public string? 차량종류 { get; set; }
    public string? 결제수단 { get; set; }
    public int? 결제예정금액 { get; set; }
    public 화주운송정산조건DTO? 정산조건 { get; set; }
    public CargoDTO? 화물 { get; set; }
    public LocationContactDTO? 픽업 { get; set; }
    public LocationContactDTO? 하차 { get; set; }
    public PricingDTO? 요금옵션 { get; set; }
    public string? 결제상태 { get; set; }
    public string? 상태 { get; set; }
    public string? 배차상태 { get; set; }
}

public sealed class 화주운송의뢰후불승인요청
{
    public string? 승인메모 { get; set; }
}

public sealed class 화주운송의뢰인수증등록요청
{
    public string 인수증번호 { get; set; } = string.Empty;
    public string? 등록메모 { get; set; }
}

public sealed class 화주운송의뢰현장지급처리요청
{
    public string? 현장지급메모 { get; set; }
}

public sealed class CargoDTO
{
    public string 화물종류 { get; set; } = string.Empty;
    public string? 설명 { get; set; }
    public int? 수량 { get; set; }
    public int? 길이Mm { get; set; }
    public int? 폭Mm { get; set; }
    public int? 높이Mm { get; set; }
    public decimal? 중량Kg { get; set; }
    public decimal? 부피Cbm { get; set; }
    public int? 팔레트개수 { get; set; }
    public bool 화물파손주의여부 { get; set; }
    public string? 온도조건 { get; set; }
}

public sealed class LocationContactDTO
{
    public AddressDTO 주소 { get; set; } = new();
    public ContactDTO 연락처 { get; set; } = new();
    public TimeWindowDTO? 시간창 { get; set; }
}

public sealed class AddressDTO
{
    [IsmsPProtectedData(
        PersonalDataFieldKey.RoadAddressLevel2,
        "상차/하차 지역 확인",
        ProtectionNote = "목록과 추천 단계에서는 지역 단위 또는 마스킹 주소 우선 표시")]
    public string 도로명주소 { get; set; } = string.Empty;
    [IsmsPProtectedData(
        PersonalDataFieldKey.DetailedAddress,
        "상차/하차 상세 위치 확인",
        ProtectionNote = "상세주소는 배차 수락 후 필요한 단계에서만 노출")]
    public string? 상세주소 { get; set; }
    [IsmsPProtectedData(
        PersonalDataFieldKey.LocationCoordinate,
        "지도 기반 상차/하차 위치 표시",
        DomainCode = IsmsPDomainCode.ProtectionSafeguards,
        ProtectionNote = "정밀 좌표는 배차/운행 목적 범위로 제한")]
    public decimal? 위도 { get; set; }
    [IsmsPProtectedData(
        PersonalDataFieldKey.LocationCoordinate,
        "지도 기반 상차/하차 위치 표시",
        DomainCode = IsmsPDomainCode.ProtectionSafeguards,
        ProtectionNote = "정밀 좌표는 배차/운행 목적 범위로 제한")]
    public decimal? 경도 { get; set; }
}

public sealed class ContactDTO
{
    [IsmsPProtectedData(
        PersonalDataFieldKey.DisplayName,
        "상차/하차 담당자 식별",
        ProtectionNote = "운송 진행에 필요한 담당자명만 단계적으로 노출")]
    public string 이름 { get; set; } = string.Empty;
    [IsmsPProtectedData(
        PersonalDataFieldKey.PhoneNumber,
        "상차/하차 담당자 연락",
        ProtectionNote = "연락처는 업무 단계와 역할에 따라 마스킹/해제")]
    public string 전화번호 { get; set; } = string.Empty;
}

public sealed class TimeWindowDTO
{
    public DateTime 시작일시 { get; set; }
    public DateTime 종료일시 { get; set; }
}

public sealed class PricingDTO
{
    public string? 서비스레벨 { get; set; }
    public string? 요청사항 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public decimal? 기본운임 { get; set; }
    public decimal? Km당단가 { get; set; }
    public decimal? 거리운임 { get; set; }
    public decimal? 최소운임 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
    public decimal? 최종운임 { get; set; }
    public decimal? 플랫폼수수료 { get; set; }
    public decimal? 기사지급예정운임 { get; set; }
    public 화주운송알선정책DTO? 알선정책 { get; set; }
}

public sealed class 화주운송기준운임견적요청
{
    public string? 차량종류 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public decimal? 상차위도 { get; set; }
    public decimal? 상차경도 { get; set; }
    public decimal? 하차위도 { get; set; }
    public decimal? 하차경도 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
}

public sealed class 화주운송기준운임견적응답
{
    public string 차량종류 { get; set; } = string.Empty;
    public decimal 예상거리Km { get; set; }
    public decimal 기본운임 { get; set; }
    public decimal Km당단가 { get; set; }
    public decimal 거리운임 { get; set; }
    public decimal 최소운임 { get; set; }
    public decimal 대기료 { get; set; }
    public decimal 수작업비 { get; set; }
    public decimal 할증 { get; set; }
    public decimal 최종운임 { get; set; }
    public bool 직선거리기준 { get; set; } = true;
    public string 거리계산방식 { get; set; } = "직선거리";
    public string? 단가출처 { get; set; }
    public IReadOnlyList<string> 경고목록 { get; set; } = Array.Empty<string>();
}

public sealed class 화주운송알선정책DTO
{
    public string? 원의뢰Id { get; set; }
    public int 알선단계 { get; set; } = 1;
    public bool 재알선금지 { get; set; } = true;
    public string? 알선소Id { get; set; }
    public string? 요금정책버전 { get; set; }
    public string? 정책메모 { get; set; }
}

public sealed class 화주운송요금정책검토결과
{
    public bool 정책위반 { get; set; }
    public bool 재알선의심 { get; set; }
    public decimal? 기준운임 { get; set; }
    public decimal? 화주기사운임차액 { get; set; }
    public IReadOnlyList<string> 이벤트코드목록 { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> 경고목록 { get; set; } = Array.Empty<string>();
}

public static class 화주운송요금정책이벤트코드
{
    public const string 재알선차단필요 = nameof(재알선차단필요);
    public const string 재알선의심 = nameof(재알선의심);
    public const string 기준운임미달 = nameof(기준운임미달);
    public const string 기사지급운임누락 = nameof(기사지급운임누락);
}

public sealed class 화주운송의뢰응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public bool 멱등재시도여부 { get; set; }
    public string 주문자UserId { get; set; } = string.Empty;
    public string 화주Id { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public string 정산상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 운송상태 { get; set; } = string.Empty;
    public 운영체제업무인계Dto? 운영체제인계 { get; set; }
    public 운영체제업무인계Dto? 운송완료화주인계 { get; set; }
    public bool 비정상운송검토보류중 { get; set; }
    public IReadOnlyList<비정상운송사건Dto> 비정상운송사건목록 { get; set; } = Array.Empty<비정상운송사건Dto>();
    public DateTime? 운송원장갱신일시Utc { get; set; }
    public string? 확정기사Id { get; set; }
    public string? 확정기사명 { get; set; }
    public string? 확정기사차량 { get; set; }
    [IsmsPProtectedData(
        PersonalDataFieldKey.LocationCoordinate,
        "배차 확정 후 진행 중인 운송 위치 확인",
        DomainCode = IsmsPDomainCode.ProtectionSafeguards,
        ProtectionNote = "운송 종료 전 화주 본인 또는 관리자 조회에서만 노출")]
    public decimal? 기사최근위도 { get; set; }
    [IsmsPProtectedData(
        PersonalDataFieldKey.LocationCoordinate,
        "배차 확정 후 진행 중인 운송 위치 확인",
        DomainCode = IsmsPDomainCode.ProtectionSafeguards,
        ProtectionNote = "운송 종료 전 화주 본인 또는 관리자 조회에서만 노출")]
    public decimal? 기사최근경도 { get; set; }
    public DateTime? 기사최근위치시각Utc { get; set; }
    public string 운송방식 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public string 결제수단 { get; set; } = string.Empty;
    public int? 결제예정금액 { get; set; }
    public 정산시점? 정산시점 { get; set; }
    public 증빙방식? 증빙방식 { get; set; }
    public 수납주체? 수납주체 { get; set; }
    public bool 세금계산서필요 { get; set; }
    public bool 현금영수증필요 { get; set; }
    public string? 정산메모 { get; set; }
    public string? 인수증번호 { get; set; }
    public DateTime? 인수증등록일시 { get; set; }
    public DateTime? 현장수금확인일시 { get; set; }
    public string? 현장지급메모 { get; set; }
    public DateTime 생성일시 { get; set; }
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public int? 팔레트개수 { get; set; }
    public string 픽업지 { get; set; } = string.Empty;
    public string 픽업상세지 { get; set; } = string.Empty;
    public decimal? 픽업위도 { get; set; }
    public decimal? 픽업경도 { get; set; }
    public string 하차지 { get; set; } = string.Empty;
    public string 하차상세지 { get; set; } = string.Empty;
    public decimal? 하차위도 { get; set; }
    public decimal? 하차경도 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
    public decimal? 최종운임 { get; set; }
    public 요약DTO? 요약 { get; set; }

    public sealed class 요약DTO
    {
        public string 화물종류 { get; set; } = string.Empty;
        public string 픽업지 { get; set; } = string.Empty;
        public string 하차지 { get; set; } = string.Empty;
    }
}

public sealed class 공개화물요약응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    public int? 화물수량 { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public string 운송방식 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public DateTime 생성일시 { get; set; }
}

public sealed class 비정상운송사건Dto
{
    public string 사건StableId { get; set; } = string.Empty;
    public string 사건유형Code { get; set; } = string.Empty;
    public string 사건유형표시명 { get; set; } = string.Empty;
    public string 원본예외Code { get; set; } = string.Empty;
    public string 단계Code { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public string 현재담당Code { get; set; } = string.Empty;
    public bool 정산보류적용여부 { get; set; }
    public int? 전체수량 { get; set; }
    public int? 정상확인수량 { get; set; }
    public int? 영향수량 { get; set; }
    public string 업무통제상태Code { get; set; } = string.Empty;
    public string 보류범위Code { get; set; } = string.Empty;
    public bool 현장진행불가 { get; set; }
    public string 보험검토상태Code { get; set; } = string.Empty;
    public string 해결결과Code { get; set; } = string.Empty;
    public bool 증빙참조있음 { get; set; }
    public DateTime 최초신고시각Utc { get; set; }
    public DateTime 최근신고시각Utc { get; set; }
    public DateTime? 최근검토시각Utc { get; set; }
    public long Revision { get; set; }
}
