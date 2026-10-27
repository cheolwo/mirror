using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

public sealed class 기사운송대금지급요청Configuration
    : IEntityTypeConfiguration<기사운송대금지급요청>
{
    public void Configure(EntityTypeBuilder<기사운송대금지급요청> builder)
    {
        builder.ToTable("기사운송대금지급요청");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.운송Id).HasColumnName("transport_id");
        builder.Property(x => x.운송번호).HasColumnName("transport_number").HasMaxLength(128).IsRequired();
        builder.Property(x => x.의뢰Id).HasColumnName("request_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.기사Id).HasColumnName("driver_id").HasMaxLength(450).IsRequired();
        builder.Property(x => x.지급예정금액).HasColumnName("expected_payout_amount").HasPrecision(18, 2);
        builder.Property(x => x.통화코드).HasColumnName("currency_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.멱등키).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        builder.Property(x => x.상태코드).HasColumnName("status_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.승인관리자Id).HasColumnName("approved_by").HasMaxLength(450).IsRequired();
        builder.Property(x => x.승인사유).HasColumnName("approval_reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.실행모드코드).HasColumnName("execution_mode_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.승인일시Utc).HasColumnName("approved_at_utc");
        builder.Property(x => x.Simulation검증일시Utc).HasColumnName("simulation_verified_at_utc");
        builder.Property(x => x.마지막처리코드).HasColumnName("last_result_code").HasMaxLength(80);
        builder.Property(x => x.마지막처리메시지).HasColumnName("last_result_message").HasMaxLength(500);
        builder.Property(x => x.자동명세검토시각Utc).HasColumnName("automatic_statement_reviewed_at_utc");
        builder.Property(x => x.자동명세검토코드).HasColumnName("automatic_statement_review_code").HasMaxLength(80);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.운송Id).IsUnique();
        builder.HasIndex(x => x.멱등키).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.승인일시Utc });
    }
}
