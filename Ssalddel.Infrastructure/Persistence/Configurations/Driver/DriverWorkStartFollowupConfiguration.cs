using Ssalddel.Contracts.Common.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Infrastructure,
    "근무별 후속 작업의 SQL 키와 재시도 조회 인덱스를 정의한다.",
    ContractType = typeof(기사운행시작후속),
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 41,
    StepKey = "infrastructure.work-start-followup-mapping",
    DependsOnStepKeys = ["domain.work-start-followup"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "EF 모델 매핑만 정의한다. 실제 DB 적용과 작업 처리 완료는 별도이며 근무 외래 키 결속을 보존한다.")]
public sealed class 기사운행시작후속Configuration : IEntityTypeConfiguration<기사운행시작후속>
{
    public void Configure(EntityTypeBuilder<기사운행시작후속> builder)
    {
        builder.ToTable("driver_work_start_followups");
        builder.HasKey(x => x.ShiftId);
        builder.Property(x => x.ShiftId).ValueGeneratedNever();
        builder.Property(x => x.DriverId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LastFailureCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasOne<기사근무>().WithOne().HasForeignKey<기사운행시작후속>(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
    }
}
