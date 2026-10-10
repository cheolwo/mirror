using Ssalddel.Contracts.Common.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Contracts.Common.Transport;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Infrastructure,
    "근무 시작 식별자와 활성 기사 슬롯의 SQL 매핑·고유 제약을 정의한다.",
    ContractType = typeof(기사근무),
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 40,
    StepKey = "infrastructure.shift-mapping",
    DependsOnStepKeys = ["domain.driver-shift"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "EF 모델 매핑이며 migration 실행이나 기존 근무 값 재작성은 수행하지 않는다. nullable 고유 슬롯과 시작 요청 키의 의미를 보존한다.")]
public sealed class 기사근무Configuration : IEntityTypeConfiguration<기사근무>
{
    public void Configure(EntityTypeBuilder<기사근무> builder)
    {
        builder.ToTable("driver_shifts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkStartClientRequestId).HasColumnName("work_start_client_request_id");
        builder.Property(x => x.WorkStartRequestKey).HasColumnName("work_start_request_key").HasMaxLength(64);
        builder.Property(x => x.WorkStartInputFingerprint).HasColumnName("work_start_input_fingerprint").HasMaxLength(64);
        builder.Property(x => x.ActiveDriverId).HasColumnName("active_driver_id").HasMaxLength(450);
        builder.Property(x => x.StoppedAtUtc).HasColumnName("stopped_at_utc");
        builder.Property(x => x.StartReturnPreference).HasColumnName("start_return_preference").HasMaxLength(32);
        builder.HasIndex(x => x.WorkStartRequestKey).IsUnique().HasDatabaseName("IX_driver_shifts_WorkStartRequestKey");
        builder.HasIndex(x => x.ActiveDriverId).IsUnique().HasDatabaseName("IX_driver_shifts_ActiveDriverId");
        builder.Property(x => x.기사Id).HasColumnName("driver_id").IsRequired();
        builder.Property(x => x.시작모드).HasColumnName("start_mode").IsRequired();
        builder.Property(x => x.시작시각).HasColumnName("started_at");
        builder.Property(x => x.시작위치).HasColumnName("start_location").IsRequired();
        builder.Property(x => x.운송실행유형)
            .HasColumnName("transport_execution_type")
            .HasMaxLength(32)
            .HasDefaultValue(운송실행유형코드.화물운송)
            .IsRequired();
        builder.Property(x => x.복귀지).HasColumnName("return_destination");
        builder.Property(x => x.오늘의복귀지주소).HasColumnName("today_return_destination");
        builder.Property(x => x.오늘의복귀지위도).HasColumnName("today_return_latitude");
        builder.Property(x => x.오늘의복귀지경도).HasColumnName("today_return_longitude");
        builder.Property(x => x.복귀지출처).HasColumnName("return_destination_source").IsRequired();
        builder.Property(x => x.복귀지입력일시).HasColumnName("return_destination_recorded_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
