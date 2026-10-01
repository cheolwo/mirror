namespace Hongdal.Contracts.Common.Content;

public static class YouTube상품후보유형코드
{
    public const string 포장상품 = "PackagedProduct";
    public const string 식재료 = "Ingredient";
    public const string 요리 = "Dish";
    public const string 생산자공급자 = "ProducerOrSupplier";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        포장상품,
        식재료,
        요리,
        생산자공급자
    };
}

public static class YouTube상품후보검수상태코드
{
    public const string 대기 = "Pending";
    public const string 승인 = "Approved";
    public const string 반려 = "Rejected";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        대기,
        승인,
        반려
    };
}

public static class YouTube상품후보추출방식코드
{
    public const string 수동검수 = "Manual";
    public const string 메타데이터검토 = "MetadataReview";
    public const string 메타데이터자동인지 = "MetadataAI";
    public const string 자막자동인지 = "TranscriptAI";
    public const string 영상프레임자동인지 = "VisionAI";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        수동검수,
        메타데이터검토,
        메타데이터자동인지,
        자막자동인지,
        영상프레임자동인지
    };
}

public static class YouTube재료인지근거유형코드
{
    public const string 메타데이터 = "Metadata";
    public const string 자막 = "Transcript";
    public const string 영상프레임 = "Frame";
    public const string 복합 = "Multimodal";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        메타데이터,
        자막,
        영상프레임,
        복합
    };
}

public static class YouTube협찬표시상태코드
{
    public const string 미확인 = "Unknown";
    public const string 표시됨 = "Disclosed";
    public const string 표시없음 = "NotDisclosed";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        미확인,
        표시됨,
        표시없음
    };
}

public static class YouTube상품구매의향유형코드
{
    public const string 구매관심 = "PurchaseInterest";
    public const string 공동구매 = "GroupPurchase";
    public const string 수입검토 = "ImportReview";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        구매관심,
        공동구매,
        수입검토
    };
}

public sealed record YouTube채널검색Dto(
    string ChannelId,
    string 채널명,
    string 설명,
    DateTime 게시일시Utc,
    string? 썸네일Url,
    string 공식채널Url,
    string 수집국가코드);

public sealed class YouTube음식채널프로필설정요청Dto
{
    public bool 음식채널여부 { get; set; } = true;
    public string? Handle { get; set; }
    public string? 국가코드 { get; set; }
    public string 기본언어코드 { get; set; } = "ko";
    public IReadOnlyList<string> 분류코드목록 { get; set; } = [];
    public int 구매발견점수 { get; set; }
    public int 수입발견점수 { get; set; }
    public string? 조사근거Url { get; set; }
    public string? 조사메모 { get; set; }
    public DateTime? 조사확인일시Utc { get; set; }
}

public sealed record YouTube음식채널Dto(
    string ChannelId,
    string 채널명,
    string? Handle,
    string? 썸네일Url,
    string 국가코드,
    string 기본언어코드,
    IReadOnlyList<string> 분류코드목록,
    int 구매발견점수,
    int 수입발견점수,
    string 공식채널Url,
    DateTime? 마지막영상게시일시Utc);

public sealed record YouTube음식채널국가집계Dto(
    string 국가코드,
    string 국가표시명,
    int 채널수,
    int 동기화완료채널수,
    DateTime? 마지막동기화일시Utc);

public sealed class YouTube상품후보등록요청Dto
{
    public string VideoId { get; set; } = string.Empty;
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
    public decimal 신뢰도 { get; set; } = 0.5m;
    public IReadOnlyList<string> 허용의향유형목록 { get; set; } =
        [YouTube상품구매의향유형코드.구매관심];
}

public sealed class YouTube상품후보검수요청Dto
{
    public string 검수상태 { get; set; } = YouTube상품후보검수상태코드.대기;
    public string 협찬표시상태 { get; set; } = YouTube협찬표시상태코드.미확인;
    public string? 공식구매Url { get; set; }
    public string? 검수메모 { get; set; }
    public string? 원산지국가코드 { get; set; }
    public string? HS코드후보 { get; set; }
    public string 온도코드 { get; set; } = "상온";
    public string 물류방식 { get; set; } = "LCL";
    public IReadOnlyList<string> 허용의향유형목록 { get; set; } = [];
}

public sealed record YouTube상품후보Dto(
    long 후보Id,
    string 상품키,
    string 상품명,
    string? 브랜드명,
    string? 원산지국가코드,
    string? HS코드후보,
    string 온도코드,
    string 물류방식,
    string 후보유형,
    int? 영상구간초,
    string 발견근거,
    string 추출방식,
    decimal 신뢰도,
    string 검수상태,
    string 협찬표시상태,
    IReadOnlyList<string> 허용의향유형목록,
    string? 공식구매Url,
    string? 검수메모,
    string VideoId,
    string 영상제목,
    string 영상설명,
    DateTime 영상게시일시Utc,
    string? 영상썸네일Url,
    string YouTube시청Url,
    string ChannelId,
    string 채널명,
    string 채널국가코드,
    DateTime 생성일시Utc,
    DateTime 수정일시Utc);

public sealed record YouTube영상재료인지항목Dto(
    string 재료명,
    string 표준재료명,
    int? 영상구간초,
    string 근거유형,
    string 발견근거,
    decimal 신뢰도,
    bool 상품후보추가여부);

public sealed record YouTube영상재료자동인지결과Dto(
    string VideoId,
    bool 실행됨,
    string? 분석모델,
    int 입력프레임수,
    bool 자막사용여부,
    int 인지재료수,
    int 추가상품후보수,
    int 중복상품후보수,
    IReadOnlyList<YouTube영상재료인지항목Dto> 인지항목목록,
    string 메시지);

public sealed record YouTube음식커뮤니티공유후보Dto(
    long 후보Id,
    string 상품명,
    string? 브랜드명,
    string? 원산지국가코드,
    string 후보유형,
    int? 영상구간초,
    string 발견근거,
    string VideoId,
    string 영상제목,
    DateTime 영상게시일시Utc,
    string? 영상썸네일Url,
    string YouTube시청Url,
    string ChannelId,
    string 채널명,
    string 채널국가코드);

public sealed class YouTube상품구매의향등록요청Dto
{
    public string 의향유형 { get; set; } = YouTube상품구매의향유형코드.구매관심;
    public string 배송권키 { get; set; } = string.Empty;
    public string 배송권명 { get; set; } = string.Empty;
    public decimal 희망수량 { get; set; } = 1;
    public string 수량단위 { get; set; } = "개";
    public long? 도착창고Id { get; set; }
    public string 도착창고유형 { get; set; } = string.Empty;
    public string 도착창고명 { get; set; } = string.Empty;
    public string 수령지주소참조키 { get; set; } = string.Empty;
    public string 수령지표시명 { get; set; } = string.Empty;
    public string 메모 { get; set; } = string.Empty;
    public int? 목표참여자수 { get; set; }
    public decimal? 목표수량 { get; set; }
}

public sealed record YouTube상품구매의향응답Dto(
    long 후보Id,
    string 의향유형,
    string 자동집단Id,
    string 현재상태,
    int 수요건수,
    decimal 총희망수량,
    string 수량단위,
    string 메시지);
