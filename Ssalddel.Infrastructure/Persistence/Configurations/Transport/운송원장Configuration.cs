using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.운송;

namespace 살뜰.Infrastructure.Persistence.Configurations.Transport;

public sealed class 운송원장Configuration : IEntityTypeConfiguration<운송원장>
{
    public void Configure(EntityTypeBuilder<운송원장> builder)
    {
        builder.ToTable("운송실행투영");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.운송번호).HasColumnName("운송번호").IsRequired();
        builder.Property(x => x.의뢰Id).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.화주Id).HasColumnName("shipper_id").IsRequired();
        builder.Property(x => x.배차업무유형).HasColumnName("business_type");
        builder.Property(x => x.원본의뢰유형).HasColumnName("source_type").IsRequired();
        builder.Property(x => x.원본의뢰Id).HasColumnName("source_request_id").IsRequired();
        builder.Property(x => x.커뮤니티원장Id).HasColumnName("community_ledger_id").HasMaxLength(120);
        builder.Property(x => x.커뮤니티원장템플릿Key).HasColumnName("community_ledger_template_key").HasMaxLength(120);
        builder.Property(x => x.커뮤니티원장상태).HasColumnName("community_ledger_state").HasMaxLength(80);
        builder.Property(x => x.커뮤니티원장동기화시각Utc).HasColumnName("community_ledger_synced_at_utc");
        builder.Property(x => x.공동구매도착지유형코드).HasColumnName("group_purchase_destination_type_code");
        builder.Property(x => x.공동구매기사세대배송여부).HasColumnName("group_purchase_driver_unit_distribution");
        builder.Property(x => x.공동구매세대배송방식코드).HasColumnName("group_purchase_unit_distribution_mode_code");
        builder.Property(x => x.공동구매세대배송건수).HasColumnName("group_purchase_unit_delivery_count");
        builder.Property(x => x.공동구매분배책임코드).HasColumnName("group_purchase_distribution_responsibility_code");
        builder.Property(x => x.상태).HasColumnName("상태").IsRequired();
        builder.Property(x => x.배차큐단계).HasColumnName("queue_stage");
        builder.Property(x => x.배차노출상태).HasColumnName("exposure_state");
        builder.Property(x => x.현재추천대상기사Id).HasColumnName("current_recommended_driver_id");
        builder.Property(x => x.추천시작시각).HasColumnName("recommendation_started_at");
        builder.Property(x => x.추천만료시각).HasColumnName("recommendation_expires_at");
        builder.Property(x => x.추천라운드).HasColumnName("recommendation_round");
        builder.Property(x => x.계획배차시도횟수).HasColumnName("plan_attempts");
        builder.Property(x => x.마지막거절기사Id).HasColumnName("last_rejected_driver_id");
        builder.Property(x => x.공개전환시각).HasColumnName("public_transition_at");
        builder.Property(x => x.확정기사Id).HasColumnName("confirmed_driver_id");
        builder.Property(x => x.픽업_도로명주소).HasColumnName("pickup_address").IsRequired();
        builder.Property(x => x.픽업_상세주소).HasColumnName("pickup_address_detail").IsRequired();
        builder.Property(x => x.픽업_위도).HasColumnName("pickup_latitude");
        builder.Property(x => x.픽업_경도).HasColumnName("pickup_longitude");
        builder.Property(x => x.하차_도로명주소).HasColumnName("dropoff_address").IsRequired();
        builder.Property(x => x.하차_상세주소).HasColumnName("dropoff_address_detail").IsRequired();
        builder.Property(x => x.하차_위도).HasColumnName("dropoff_latitude");
        builder.Property(x => x.하차_경도).HasColumnName("dropoff_longitude");
        builder.Property(x => x.출발_픽업).HasColumnName("출발_픽업");
        builder.Property(x => x.도착).HasColumnName("도착");
        builder.Property(x => x.기사_운송자).HasColumnName("기사_운송자").IsRequired();
        builder.Property(x => x.출발지).HasColumnName("출발지").IsRequired();
        builder.Property(x => x.도착지).HasColumnName("도착지").IsRequired();
        builder.Property(x => x.운임).HasColumnName("운임");
        builder.Property(x => x.기사기본거리지급액).HasColumnName("driver_base_distance_payout").HasPrecision(18, 2);
        builder.Property(x => x.기사기상할증액).HasColumnName("driver_weather_surcharge").HasPrecision(18, 2);
        builder.Property(x => x.기사지급예정액).HasColumnName("driver_expected_payout").HasPrecision(18, 2);
        builder.Property(x => x.기사기상할증적용여부).HasColumnName("driver_weather_surcharge_applied");
        builder.Property(x => x.픽업지기상자료상태).HasColumnName("pickup_weather_evidence_status").HasMaxLength(80);
        builder.Property(x => x.픽업지기상코드).HasColumnName("pickup_weather_code").HasMaxLength(40);
        builder.Property(x => x.픽업지기상기준시각Utc).HasColumnName("pickup_weather_observed_at_utc");
        builder.Property(x => x.픽업지기상자료출처).HasColumnName("pickup_weather_source").HasMaxLength(300);
        builder.Property(x => x.픽업지기상자료Hash).HasColumnName("pickup_weather_payload_hash").HasMaxLength(64);
        builder.Property(x => x.기사제안요금정책판본).HasColumnName("driver_offer_pricing_revision").HasMaxLength(180);
        builder.Property(x => x.기사제안요금판정시각Utc).HasColumnName("driver_offer_priced_at_utc");
        builder.Property(x => x.첨부_json).HasColumnName("첨부_json").IsRequired();
        builder.Property(x => x.메모).HasColumnName("메모").IsRequired();
        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("timestamp(6)")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.의뢰Id)
            .IsUnique()
            .HasDatabaseName("ux_운송실행투영_request_id");
        builder.HasIndex(x => x.커뮤니티원장Id);
    }
}
