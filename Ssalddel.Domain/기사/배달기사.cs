using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using 살뜰.도메인.공통;

namespace 살뜰.도메인.기사
{
    [Table("배달기사")]
    public class 배달기사
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("기사명")]
        public string 기사명 { get; set; } = string.Empty;

        [Column("기사Id")]
        public string 기사Id { get; set; } = string.Empty;

        [Column("상태")]
        public string 상태 { get; set; } = "활동중";

        [Column("연락처")]
        public string 연락처 { get; set; } = string.Empty;

        [Column("차량")]
        public string 차량 { get; set; } = "오토바이";

        /// <summary>선택한 실차의 비공개 내부 참조. 차량번호 원문을 저장하지 않는다.</summary>
        public string? 현재실차참조 { get; set; }

        [Column("운행상태")]
        public string 운행상태 { get; set; } = 상태값.기사운행상태.대기;

        [Column("주_활동지역")]
        public string 주_활동지역 { get; set; } = string.Empty;

        [Column("메모")]
        public string 메모 { get; set; } = string.Empty;

        [Column("등록일")]
        public DateTime? 등록일 { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
