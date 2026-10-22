namespace Ssalddel.Contracts.Common.Inbound;

public static class 입고흐름유형코드
{
    public const string 계약기반입고 = "ContractBased";
    public const string 현장임시입고 = "Unplanned";
    public const string 주문자동입고예정 = "OrderAutoExpected";

    public static string Normalize(string? value)
        => value?.Trim() switch
        {
            현장임시입고 => 현장임시입고,
            주문자동입고예정 => 주문자동입고예정,
            _ => 계약기반입고
        };

    public static string GetDisplayName(string? value)
        => Normalize(value) switch
        {
            현장임시입고 => "현장 임시 입고",
            주문자동입고예정 => "주문 자동 입고 예정",
            _ => "계약 기반 입고"
        };

    public static string GetDescription(string? value)
        => Normalize(value) switch
        {
            현장임시입고 => "계약이나 입고 예정이 아직 정리되지 않았지만, 현장에서 먼저 물건을 받아 임시로 시스템에 편입합니다.",
            주문자동입고예정 => "주문자 또는 판매자의 구매/판매 흐름에서 자동으로 입고 예정이 생성되어 창고 업무로 이어집니다.",
            _ => "계약서와 관리 정보가 먼저 등록되고, 그 계약을 기준으로 입고 예정과 검수가 진행됩니다."
        };

    public static bool RequiresExistingContract(string? value)
        => Normalize(value) == 계약기반입고;

    public static bool IsOrderGenerated(string? value)
        => Normalize(value) == 주문자동입고예정;
}

public static class 입고상태코드
{
    public const string 예정 = "입고예정";
    public const string 운송중 = "운송중";
    public const string 완료 = "입고완료";
    public const string 취소 = "입고취소";
}

public static class 현장입고보관조건
{
    public const string 상온 = "상온";
    public const string 냉장 = "냉장";
    public const string 냉동 = "냉동";
    public const string 미지정 = "미지정";

    public static IReadOnlyList<string> 전체 { get; } = [상온, 냉장, 냉동, 미지정];

    public static string Normalize(string? value)
        => 전체.Contains(value?.Trim() ?? string.Empty, StringComparer.Ordinal)
            ? value!.Trim()
            : 미지정;
}

public static class 현장입고요청안내
{
    public const string 현재버전 = "2026-07-20";

    public static IReadOnlyList<string> 문구 { get; } =
    [
        "이 요청은 입고 예정이나 계약 연결이 확인되지 않은 현장 반입 사실을 창고 원장에 기록합니다.",
        "저장만으로 실제 검수, 적재, 보관 책임, 계약, 정산과 재고 생성이 확정되지 않습니다.",
        "공급처 또는 반입자, 수량, 보관 조건과 현장 반입 사유는 담당자가 다시 확인해야 합니다.",
        "입고 완료와 재고 생성은 별도의 서버 상태 전이와 검수 기록을 거쳐야 합니다."
    ];

    public static bool 유효한확인(현장입고요청등록요청? request)
        => request is not null
           && request.임시입고안내확인
           && string.Equals(request.안내버전, 현재버전, StringComparison.Ordinal);
}

public sealed class 현장입고요청등록요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long 창고Id { get; set; }
    public string 상품바코드 { get; set; } = string.Empty;
    public string 입고묶음바코드 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 공급처명 { get; set; } = string.Empty;
    public int 입고수량 { get; set; } = 1;
    public string 보관조건 { get; set; } = 현장입고보관조건.미지정;
    public string 현장입고사유 { get; set; } = string.Empty;
    public bool 임시입고안내확인 { get; set; }
    public string 안내버전 { get; set; } = string.Empty;
}

public sealed class 입고요청항목응답
{
    public long Id { get; set; }
    public Guid? 가입고사진신청Id { get; set; }
    public Guid? ClientRequestId { get; set; }
    public bool 멱등재시도여부 { get; set; }
    public long 창고Id { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string? 커뮤니티원장템플릿Key { get; set; }
    public string? 커뮤니티원장상태 { get; set; }
    public string 입고흐름유형 { get; set; } = 입고흐름유형코드.계약기반입고;
    public string 입고생성경로 { get; set; } = string.Empty;
    public bool 계약선행여부 { get; set; } = true;
    public bool 자동생성여부 { get; set; }
    public long? 주문Id { get; set; }
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 주문자UserId { get; set; } = string.Empty;
    public string 판매자UserId { get; set; } = string.Empty;
    public long? 출고예정Id { get; set; }
    public string? 운송의뢰Id { get; set; }
    public string 공급처코드 { get; set; } = string.Empty;
    public string 공급처명 { get; set; } = string.Empty;
    public string 예정상품명 { get; set; } = string.Empty;
    public string 예정SKU { get; set; } = string.Empty;
    public int? 예정수량 { get; set; }
    public string 입고묶음바코드 { get; set; } = string.Empty;
    public string 보관조건 { get; set; } = string.Empty;
    public string 현장입고사유 { get; set; } = string.Empty;
    public string 안내버전 { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string 원주문참조번호 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public DateTime? 예정도착일 { get; set; }
    public DateTime? 입고완료일시 { get; set; }
    public 입고계약스냅샷 계약정보 { get; set; } = 입고계약스냅샷.Default();
}

public sealed class 입고요청목록응답
{
    public IReadOnlyList<입고요청항목응답> Items { get; set; } = [];
}

/// <summary>입고 업무 목록의 서버 정렬·검색·페이지 조회 조건입니다. Page는 0부터 시작합니다.</summary>
public sealed class 입고요청목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public long? WarehouseId { get; set; }
    public string? Status { get; set; }
    public string? FlowType { get; set; }
    public string? Sku { get; set; }
}

public sealed class 입고요청페이지응답
{
    public IReadOnlyList<입고요청항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class 입고요청저장요청
{
    public Guid? 가입고사진신청Id { get; set; }
    public Guid? ClientRequestId { get; set; }
    public Guid? 신청개인정보동의증적Id { get; set; }
    public string 신청출처Code { get; set; } = string.Empty;
    public long 창고Id { get; set; }
    public string 입고흐름유형 { get; set; } = 입고흐름유형코드.계약기반입고;
    public string 입고생성경로 { get; set; } = string.Empty;
    public bool 계약선행여부 { get; set; } = true;
    public bool 자동생성여부 { get; set; }
    public long? 주문Id { get; set; }
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 판매자UserId { get; set; } = string.Empty;
    public long? 출고예정Id { get; set; }
    public string? 운송의뢰Id { get; set; }
    public string 공급처코드 { get; set; } = string.Empty;
    public string 공급처명 { get; set; } = string.Empty;
    public string 원주문참조번호 { get; set; } = string.Empty;
    public DateTime? 예정도착일 { get; set; }
    public string 비고 { get; set; } = string.Empty;
    public 입고계약스냅샷 계약정보 { get; set; } = 입고계약스냅샷.Default();
}

public sealed class 입고완료요청
{
    public IReadOnlyList<입고상품저장요청> Items { get; set; } = [];
}

public sealed class 입고상품항목응답
{
    public long Id { get; set; }
    public long 입고요청Id { get; set; }
    public long 창고Id { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string? 커뮤니티원장템플릿Key { get; set; }
    public string? 커뮤니티원장상태 { get; set; }
    public string 소유자UserId { get; set; } = string.Empty;
    public string 판매자UserId { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string 옵션명 { get; set; } = string.Empty;
    public int 입고수량 { get; set; }
    public int 가용수량 { get; set; }
    public int 불량수량 { get; set; }
    public string 보관위치 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public DateTime? 입고완료일시 { get; set; }
    public 입고계약스냅샷 계약정보 { get; set; } = 입고계약스냅샷.Default();
}

public sealed class 입고상품목록응답
{
    public IReadOnlyList<입고상품항목응답> Items { get; set; } = [];
    public bool 멱등재시도여부 { get; set; }
}

public sealed class 입고상품저장요청
{
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string 옵션명 { get; set; } = string.Empty;
    public int 입고수량 { get; set; }
    public int 불량수량 { get; set; }
    public string 보관위치 { get; set; } = string.Empty;
}
