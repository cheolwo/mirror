using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

public sealed class 기사운송대금정산배치Configuration : IEntityTypeConfiguration<기사운송대금정산배치>
{
    public void Configure(EntityTypeBuilder<기사운송대금정산배치> builder)
    {
        builder.ToTable("기사운송대금정산배치");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.기사Id).HasColumnName("driver_id").HasMaxLength(450).IsRequired();
        builder.Property(x => x.통화코드).HasColumnName("currency_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.실행모드코드).HasColumnName("execution_mode_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.정책판본).HasColumnName("policy_revision").HasMaxLength(100).IsRequired();
        builder.Property(x => x.일정코드).HasColumnName("schedule_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.기간시작Utc).HasColumnName("period_start_utc");
        builder.Property(x => x.기간종료Utc).HasColumnName("period_end_utc");
        builder.Property(x => x.기간기준Code).HasColumnName("period_basis_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.PayoutScheduledAtUtc).HasColumnName("payout_scheduled_at_utc");
        builder.Property(x => x.멱등키).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ContentDigest).HasColumnName("content_digest").HasMaxLength(64).IsRequired();
        builder.Property(x => x.상태코드).HasColumnName("status_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.승인합계금액).HasColumnName("approved_total_amount").HasPrecision(18, 2);
        builder.Property(x => x.준비합계금액).HasColumnName("ready_total_amount").HasPrecision(18, 2);
        builder.Property(x => x.준비건수).HasColumnName("ready_count");
        builder.Property(x => x.보류건수).HasColumnName("held_count");
        builder.Property(x => x.생성관리자Id).HasColumnName("created_by").HasMaxLength(450).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.멱등키).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.기간시작Utc });
    }
}

public sealed class 기사운송대금정산배치행Configuration : IEntityTypeConfiguration<기사운송대금정산배치행>
{
    public void Configure(EntityTypeBuilder<기사운송대금정산배치행> builder)
    {
        builder.ToTable("기사운송대금정산배치행");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.정산배치Id).HasColumnName("batch_id");
        builder.Property(x => x.지급요청Id).HasColumnName("payout_request_id");
        builder.Property(x => x.운송Id).HasColumnName("transport_id");
        builder.Property(x => x.의뢰Id).HasColumnName("request_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.동결금액).HasColumnName("frozen_amount").HasPrecision(18, 2);
        builder.Property(x => x.상태코드).HasColumnName("status_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.보류사유코드).HasColumnName("hold_reason_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.보류메시지).HasColumnName("hold_message").HasMaxLength(500).IsRequired();
        builder.Property(x => x.최근검증시각Utc).HasColumnName("last_validated_at_utc");
        builder.HasOne(x => x.정산배치).WithMany(x => x.행목록).HasForeignKey(x => x.정산배치Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.지급요청).WithMany().HasForeignKey(x => x.지급요청Id).OnDelete(DeleteBehavior.Restrict);
        // 건별/주간 및 서로 다른 관리자 요청 사이에도 한 채무는 한 명세에만 속합니다.
        builder.HasIndex(x => x.지급요청Id).IsUnique();
    }
}
