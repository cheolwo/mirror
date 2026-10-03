namespace Ssalddel.Contracts.Common.Inventory;

using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Privacy;

public sealed class 재고항목응답
{
    public long 입고상품Id { get; set; }
    public long 창고Id { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string? 커뮤니티원장템플릿Key { get; set; }
    public string? 커뮤니티원장상태 { get; set; }
    public string 창고명 { get; set; } = string.Empty;
    public string 소유자UserId { get; set; } = string.Empty;
    public string 판매자UserId { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string 옵션명 { get; set; } = string.Empty;
    public int 가용수량 { get; set; }
    public int 예약수량 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string 보관위치 { get; set; } = string.Empty;
    public 입고계약스냅샷 계약정보 { get; set; } = 입고계약스냅샷.Default();
}

public sealed class 재고목록응답
{
    public IReadOnlyList<재고항목응답> Items { get; set; } = [];
}

public sealed class 창고작업결과응답
{
    public long 입고상품Id { get; set; }
    public long 창고Id { get; set; }
    public string 작업유형 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string 보관위치 { get; set; } = string.Empty;
    public int 가용수량 { get; set; }
    public int 불량수량 { get; set; }
    public string 처리UserId { get; set; } = string.Empty;
    public DateTime 처리일시 { get; set; }
    public string 메모 { get; set; } = string.Empty;

    /// <summary>같은 검수 수량·불량 수량·메모가 이미 적용되어 추가 상태 변경이나 Event 발행 없이 반환되었는지 나타냅니다.</summary>
    public bool 멱등재시도여부 { get; set; }
}

public sealed class 입고검수요청
{
    public int 검수수량 { get; set; }
    public int 불량수량 { get; set; }
    public string 검수메모 { get; set; } = string.Empty;
}

public sealed class 적재위치배정요청
{
    public string 보관위치 { get; set; } = string.Empty;
    public string 적재메모 { get; set; } = string.Empty;
}

public sealed class 포장작업요청
{
    public int 포장수량 { get; set; }
    public string 포장유형 { get; set; } = string.Empty;
    public string 포장메모 { get; set; } = string.Empty;
}

public sealed class 재고조정요청
{
    public int 변경수량 { get; set; }
    public string 사유 { get; set; } = string.Empty;
}

public sealed class 재고운송의뢰생성요청
{
    public long? 출고예정Id { get; set; }
    public long 입고상품Id { get; set; }
    public int 요청수량 { get; set; }
    public string 하차지주소 { get; set; } = string.Empty;
    public string 하차지상세주소 { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName,
        "재위탁 화물의 실제 하차 담당자 식별",
        ProtectionNote = "입력된 담당자만 저장하며 사용자 ID나 상차 창고 담당자로 대체하지 않음")]
    public string? 하차담당자명 { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber,
        "재위탁 화물의 실제 하차 담당자 연락",
        ProtectionNote = "입력된 연락처만 저장하며 상차 창고 연락처로 대체하지 않음")]
    public string? 하차연락처 { get; set; }
    public string 화물종류 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public DateTime? 희망상차일시 { get; set; }
    public DateTime? 희망도착일시 { get; set; }
    public string 취급메모 { get; set; } = string.Empty;
}

public sealed class 재고이동항목응답
{
    public long Id { get; set; }
    public long 창고Id { get; set; }
    public long? 입고상품Id { get; set; }
    public long? 판매상품Id { get; set; }
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string 이동유형 { get; set; } = string.Empty;
    public int 수량 { get; set; }
    public long? 주문Id { get; set; }
    public string? 주문참조번호 { get; set; }
    public long? 출고예정Id { get; set; }
    public long? 입고요청Id { get; set; }
    public string? 운송의뢰Id { get; set; }
    public string 처리UserId { get; set; } = string.Empty;
    public string 메모 { get; set; } = string.Empty;
    public DateTime 발생일시 { get; set; }
}

public sealed class 재고이동목록응답
{
    public IReadOnlyList<재고이동항목응답> Items { get; set; } = [];
}
