using Hongdal.Contracts.Common.Content;

namespace Hongdal.Domain.Content;

public sealed class YouTube영상상품후보
{
    public long Id { get; set; }

    public long YouTube채널영상Id { get; set; }

    public YouTube채널영상? 영상 { get; set; }

    public string 상품키 { get; set; } = string.Empty;

    public string 상품명 { get; set; } = string.Empty;

    public string? 브랜드명 { get; set; }

    public string? 원산지국가코드 { get; set; }

    public string? HS코드후보 { get; set; }

    public string 온도코드 { get; set; } = "상온";

    public string 물류방식 { get; set; } = "LCL";

    public string 후보유형 { get; set; } = YouTube상품후보유형코드.포장상품;

    public int? 영상구간초 { get; set; }

    public string 발견근거 { get; set; } = string.Empty;

    public string 추출방식 { get; set; } = YouTube상품후보추출방식코드.수동검수;

    public decimal 신뢰도 { get; set; }

    public string 검수상태 { get; set; } = YouTube상품후보검수상태코드.대기;

    public string 협찬표시상태 { get; set; } = YouTube협찬표시상태코드.미확인;

    public string 허용의향유형 { get; set; } = YouTube상품구매의향유형코드.구매관심;

    public string? 공식구매Url { get; set; }

    public string? 검수메모 { get; set; }

    public string? 검수자UserId { get; set; }

    public DateTime? 검수일시Utc { get; set; }

    public DateTime 생성일시Utc { get; set; } = DateTime.UtcNow;

    public DateTime 수정일시Utc { get; set; } = DateTime.UtcNow;
}
