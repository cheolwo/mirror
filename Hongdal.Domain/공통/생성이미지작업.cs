using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 홍달.도메인.공통;

[Table("생성이미지작업")]
public class 생성이미지작업
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("작업코드")]
    [MaxLength(100)]
    public string 작업코드 { get; set; } = Guid.NewGuid().ToString("N");

    [Column("이미지용도")]
    [MaxLength(100)]
    public string 이미지용도 { get; set; } = 생성이미지용도.화주상품사진;

    [Column("대상타입")]
    [MaxLength(100)]
    public string 대상타입 { get; set; } = string.Empty;

    [Column("대상식별자")]
    [MaxLength(200)]
    public string 대상식별자 { get; set; } = string.Empty;

    [Column("샘플데이터여부")]
    public bool 샘플데이터여부 { get; set; }

    [Column("중복방지키")]
    [MaxLength(300)]
    public string 중복방지키 { get; set; } = string.Empty;

    [Column("프롬프트", TypeName = "longtext")]
    public string 프롬프트 { get; set; } = string.Empty;

    [Column("종횡비")]
    [MaxLength(20)]
    public string 종횡비 { get; set; } = "auto";

    [Column("해상도")]
    [MaxLength(20)]
    public string 해상도 { get; set; } = "1K";

    [Column("외부모델명")]
    [MaxLength(100)]
    public string 외부모델명 { get; set; } = string.Empty;

    [Column("외부TaskId")]
    [MaxLength(200)]
    public string? 외부TaskId { get; set; }

    [Column("상태")]
    [MaxLength(50)]
    public string 상태 { get; set; } = 생성이미지작업상태.생성대기;

    [Column("외부원본이미지Url", TypeName = "varchar(1000)")]
    public string? 외부원본이미지Url { get; set; }

    [Column("저장Bucket")]
    [MaxLength(200)]
    public string? 저장Bucket { get; set; }

    [Column("저장ObjectName", TypeName = "varchar(500)")]
    public string? 저장ObjectName { get; set; }

    [Column("저장Url", TypeName = "varchar(1000)")]
    public string? 저장Url { get; set; }

    [Column("콜백Url", TypeName = "varchar(1000)")]
    public string? 콜백Url { get; set; }

    [Column("실패사유", TypeName = "longtext")]
    public string? 실패사유 { get; set; }

    [Column("재시도횟수")]
    public int 재시도횟수 { get; set; }

    [Column("최종실패시각")]
    public DateTime? 최종실패시각 { get; set; }

    [Column("최근응답원문", TypeName = "longtext")]
    public string? 최근응답원문 { get; set; }

    [Column("AI생성이미지여부")]
    public bool AI생성이미지여부 { get; set; } = true;

    [Column("생성시각")]
    public DateTime 생성시각 { get; set; } = DateTime.UtcNow;

    [Column("완료시각")]
    public DateTime? 완료시각 { get; set; }

    [Column("수정시각")]
    public DateTime 수정시각 { get; set; } = DateTime.UtcNow;
}

public static class 생성이미지용도
{
    public const string 화주상품사진 = "화주상품사진";
    public const string 기사상차인증사진 = "기사상차인증사진";
    public const string 기사배차완료인증사진 = "기사배차완료인증사진";
    public const string 음식상품썸네일 = "음식상품썸네일";
    public const string 주문후기사진 = "주문후기사진";
    public const string 상품상세페이지생성이미지 = "상품상세페이지생성이미지";
    public const string 커뮤니티글쓰기이미지 = "커뮤니티글쓰기이미지";
}

public static class 생성이미지작업상태
{
    public const string 생성대기 = "생성대기";
    public const string 생성요청됨 = "생성요청됨";
    public const string 생성중 = "생성중";
    public const string 업로드중 = "업로드중";
    public const string 완료 = "완료";
    public const string 실패 = "실패";
}
