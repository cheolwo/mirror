using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using 살뜰.도메인.공통;

namespace 살뜰.도메인.결제
{
    [Table("결제")]
    public class 결제
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("payment_id")]
        public string 결제Id { get; set; } = string.Empty;

        [Column("request_id")]
        public string 의뢰Id { get; set; } = string.Empty;

        [Column("shipper_id")]
        public string 화주Id { get; set; } = string.Empty;

        [Column("target_type")]
        public int 결제대상유형 { get; set; } = 결제공통정의.결제대상유형.용달운송의뢰;

        [Column("target_id")]
        public string 대상Id { get; set; } = string.Empty;

        [Column("pg_provider")]
        public string PG사 { get; set; } = "TossPayments";

        [Column("provider_type")]
        public int 결제제공자 { get; set; } = 결제공통정의.결제제공자.TossPayments;

        [Column("payment_method")]
        public string 결제수단 { get; set; } = "미정";

        [Column("payment_status")]
        public string 결제상태 { get; set; } = 상태값.결제상태.결제대기;

        [Column("common_status")]
        public int 공통결제상태 { get; set; } = 결제공통정의.결제상태.요청생성;

        [Column("amount")]
        public int 결제금액 { get; set; }

        [Column("currency")]
        public string 통화 { get; set; } = "KRW";

        [Column("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [Column("order_name")]
        public string 주문명 { get; set; } = string.Empty;

        [Column("payment_key")]
        public string? PaymentKey { get; set; }

        [Column("external_transaction_no")]
        public string? 외부거래번호 { get; set; }

        [Column("toss_response_json")]
        public string? Toss응답Json { get; set; }

        [Column("raw_response_json")]
        public string? 원본응답Json { get; set; }

        [Column("preparation_evidence_json")]
        public string? 준비근거Json { get; set; }

        [Column("deposit_webhook_verification_hash")]
        public string? PG입금알림검증Hash { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("approved_at")]
        public DateTime? 승인일시 { get; set; }

        [Column("canceled_at")]
        public DateTime? 취소일시 { get; set; }
    }
}
