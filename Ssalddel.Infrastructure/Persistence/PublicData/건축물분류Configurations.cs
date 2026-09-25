using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.PublicData.Korea;

namespace Ssalddel.Infrastructure.Persistence.PublicData;

internal sealed class 건축물용도CategoryDefinitionConfiguration
    : IEntityTypeConfiguration<건축물용도CategoryDefinition>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물용도CategoryDefinition> builder)
    {
        builder.ToTable("public_building_category_catalog");
        builder.HasKey(item => item.CategoryCode);
        builder.Property(item => item.CategoryCode).HasMaxLength(64);
        builder.Property(item => item.DisplayNameKo).HasMaxLength(80).IsRequired();
        builder.Property(item => item.DescriptionKo).HasMaxLength(500).IsRequired();
        builder.Property(item => item.WorldRoleCode).HasMaxLength(64).IsRequired();
        builder.HasData(CategorySeed());
    }

    private static 건축물용도CategoryDefinition[] CategorySeed() =>
    [
        Define(건축물용도CategoryCodes.Residential, "주거", "단독·공동주택 등 사람이 거주하는 건축물", "settlement", 10, true),
        Define(건축물용도CategoryCodes.Agriculture, "농업", "동물·식물 관련 시설 등 농업 생산을 지원하는 건축물", "farm", 20, true),
        Define(건축물용도CategoryCodes.LogisticsStorage, "물류·창고", "창고시설 등 보관·적재와 관계되는 건축물", "hub", 30, true),
        Define(건축물용도CategoryCodes.Commercial, "상업·생활", "근린생활·판매시설 등 생활권 상업 건축물", "town", 40, true),
        Define(건축물용도CategoryCodes.BusinessOffice, "업무", "업무시설 등 사무 기능의 건축물", "town", 50, true),
        Define(건축물용도CategoryCodes.PublicCommunity, "공공·공동체", "공공·안전·공동체 기능으로 검토할 건축물", "civic", 60, true),
        Define(건축물용도CategoryCodes.Industrial, "산업", "공장 등 제조·산업 기능의 건축물", "industrial", 70, true),
        Define(건축물용도CategoryCodes.EducationResearch, "교육·연구", "교육연구시설", "civic", 80, true),
        Define(건축물용도CategoryCodes.MedicalWelfare, "의료·복지", "의료시설과 노유자시설", "civic", 90, true),
        Define(건축물용도CategoryCodes.CultureTourism, "문화·관광", "문화·집회·숙박·관광·운동 관련 건축물", "town", 100, true),
        Define(건축물용도CategoryCodes.Transport, "교통", "운수시설과 자동차 관련 시설", "transport", 110, true),
        Define(건축물용도CategoryCodes.UtilityInfrastructure, "기반시설", "발전·방송통신·자원순환·위험물 처리 관련 건축물", "infrastructure", 120, true),
        Define(건축물용도CategoryCodes.Religious, "종교", "종교시설", "settlement", 130, true),
        Define(건축물용도CategoryCodes.Other, "기타", "공식 주용도는 있으나 현재 규칙에 대응하지 않는 건축물", "generic", 900, false),
        Define(건축물용도CategoryCodes.Unresolved, "미분류", "공식 주용도가 없거나 행정동 배정·분류가 해결되지 않은 건축물", "unresolved", 999, false),
    ];

    private static 건축물용도CategoryDefinition Define(
        string code,
        string name,
        string description,
        string worldRole,
        int sortOrder,
        bool presentationEligible) => new()
        {
            CategoryCode = code,
            DisplayNameKo = name,
            DescriptionKo = description,
            WorldRoleCode = worldRole,
            SortOrder = sortOrder,
            PresentationEligible = presentationEligible,
        };
}

internal sealed class 건축물대장표제부RecordConfiguration
    : IEntityTypeConfiguration<건축물대장표제부Record>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물대장표제부Record> builder)
    {
        builder.ToTable("public_building_register_titles");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.RegisterManagementPk, item.SourceRevision }).IsUnique();
        builder.HasIndex(item => new { item.SigunguCode, item.LegalDongCode, item.ValidToUtc });
        builder.Property(item => item.RegisterManagementPk).HasMaxLength(100).IsRequired();
        builder.Property(item => item.RegisterKindCode).HasMaxLength(32).IsRequired();
        builder.Property(item => item.RegisterTypeCode).HasMaxLength(32);
        builder.Property(item => item.SigunguCode).HasMaxLength(5).IsRequired();
        builder.Property(item => item.LegalDongCode).HasMaxLength(10).IsRequired();
        builder.Property(item => item.LandLot).HasMaxLength(80);
        builder.Property(item => item.RoadAddress).HasMaxLength(500);
        builder.Property(item => item.NormalizedRoadAddressKey).HasMaxLength(500);
        builder.Property(item => item.BuildingName).HasMaxLength(300);
        builder.Property(item => item.DongName).HasMaxLength(200);
        builder.Property(item => item.MainPurposeCode).HasMaxLength(32);
        builder.Property(item => item.MainPurposeName).HasMaxLength(200);
        builder.Property(item => item.StructureCode).HasMaxLength(32);
        builder.Property(item => item.StructureName).HasMaxLength(200);
        builder.Property(item => item.BuildingAreaSquareMeters).HasPrecision(20, 4);
        builder.Property(item => item.TotalFloorAreaSquareMeters).HasPrecision(20, 4);
        builder.Property(item => item.SiteAreaSquareMeters).HasPrecision(20, 4);
        builder.Property(item => item.OfficialBuildingCoveragePercent).HasPrecision(12, 4);
        builder.Property(item => item.OfficialFloorAreaRatioPercent).HasPrecision(12, 4);
        builder.Property(item => item.HeightMeters).HasPrecision(12, 4);
        builder.Property(item => item.SourceRevision).HasMaxLength(200).IsRequired();
    }
}

internal sealed class 공개인허가사업장RecordConfiguration
    : IEntityTypeConfiguration<공개인허가사업장Record>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<공개인허가사업장Record> builder)
    {
        builder.ToTable("public_licensed_business_records");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new
        {
            item.SourceId,
            item.OpenServiceId,
            item.ManagementNumber,
            item.SourceRevision,
        }).IsUnique();
        builder.HasIndex(item => new { item.NormalizedRoadAddressKey, item.SourceRevision });
        builder.HasIndex(item => new { item.BusinessStatusCode, item.SourceRevision });
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceDatasetId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.OpenServiceId).HasMaxLength(80).IsRequired();
        builder.Property(item => item.OpenServiceName).HasMaxLength(200);
        builder.Property(item => item.ManagementNumber).HasMaxLength(160).IsRequired();
        builder.Property(item => item.BusinessName).HasMaxLength(300).IsRequired();
        builder.Property(item => item.BusinessTypeName).HasMaxLength(200);
        builder.Property(item => item.LicenseCategoryName).HasMaxLength(200);
        builder.Property(item => item.BusinessStatusCode).HasMaxLength(40);
        builder.Property(item => item.BusinessStatusName).HasMaxLength(100);
        builder.Property(item => item.DetailedStatusCode).HasMaxLength(40);
        builder.Property(item => item.DetailedStatusName).HasMaxLength(100);
        builder.Property(item => item.LotAddress).HasMaxLength(600);
        builder.Property(item => item.RoadAddress).HasMaxLength(600);
        builder.Property(item => item.NormalizedRoadAddressKey).HasMaxLength(500);
        builder.Property(item => item.SourceCoordinateX).HasPrecision(20, 8);
        builder.Property(item => item.SourceCoordinateY).HasPrecision(20, 8);
        builder.Property(item => item.SourceCoordinateReferenceSystem).HasMaxLength(40);
        builder.Property(item => item.SourceRevision).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SourceHashSha256).HasMaxLength(64).IsRequired();
    }
}

internal sealed class 공개사업장건축물AssignmentConfiguration
    : IEntityTypeConfiguration<공개사업장건축물Assignment>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<공개사업장건축물Assignment> builder)
    {
        builder.ToTable("public_business_building_assignments");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.BusinessRecordId, item.RuleRevision }).IsUnique();
        builder.HasIndex(item => new { item.BuildingRecordId, item.AssignmentStatusCode });
        builder.Property(item => item.AssignmentStatusCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.AssignmentMethodCode).HasMaxLength(80);
        builder.Property(item => item.ConfidenceCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RuleRevision).HasMaxLength(200).IsRequired();
        builder.HasOne(item => item.BusinessRecord)
            .WithMany()
            .HasForeignKey(item => item.BusinessRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.BuildingRecord)
            .WithMany()
            .HasForeignKey(item => item.BuildingRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class 건축물공개사업장AggregateConfiguration
    : IEntityTypeConfiguration<건축물공개사업장Aggregate>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물공개사업장Aggregate> builder)
    {
        builder.ToTable("public_building_business_aggregates");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new
        {
            item.BuildingRecordId,
            item.SourceRevision,
            item.RuleRevision,
        }).IsUnique();
        builder.Property(item => item.SourceRevision).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EvidenceKindCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RuleRevision).HasMaxLength(200).IsRequired();
        builder.Property(item => item.AggregateHashSha256).HasMaxLength(64).IsRequired();
        builder.HasOne(item => item.BuildingRecord)
            .WithMany()
            .HasForeignKey(item => item.BuildingRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class 건축물형태ProfileConfiguration
    : IEntityTypeConfiguration<건축물형태Profile>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물형태Profile> builder)
    {
        builder.ToTable("public_building_massing_profiles");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.건축물RecordId, item.규칙개정번호 }).IsUnique();
        builder.Property(item => item.건축물RecordId).HasColumnName("BuildingRecordId");
        builder.Property(item => item.관측지상층수).HasColumnName("ObservedAboveGroundFloorCount");
        builder.Property(item => item.추정지상층수).HasColumnName("EstimatedAboveGroundFloorCount");
        builder.Property(item => item.표현지상층수).HasColumnName("PresentationAboveGroundFloorCount");
        builder.Property(item => item.공식건폐율Percent).HasColumnName("OfficialBuildingCoveragePercent").HasPrecision(12, 4);
        builder.Property(item => item.공식용적률Percent).HasColumnName("OfficialFloorAreaRatioPercent").HasPrecision(12, 4);
        builder.Property(item => item.단순건폐비율Percent).HasColumnName("SimpleBuildingToSiteRatioPercent").HasPrecision(12, 4);
        builder.Property(item => item.단순연면적대지비율Percent).HasColumnName("SimpleGrossFloorToSiteRatioPercent").HasPrecision(12, 4);
        builder.Property(item => item.대지면적SquareMeters).HasColumnName("SiteAreaSquareMeters").HasPrecision(20, 4);
        builder.Property(item => item.건축면적SquareMeters).HasColumnName("BuildingAreaSquareMeters").HasPrecision(20, 4);
        builder.Property(item => item.연면적SquareMeters).HasColumnName("TotalFloorAreaSquareMeters").HasPrecision(20, 4);
        builder.Property(item => item.높이Meters).HasColumnName("HeightMeters").HasPrecision(12, 4);
        builder.Property(item => item.추정층고Meters).HasColumnName("EstimatedFloorHeightMeters").HasPrecision(8, 4);
        builder.Property(item => item.건물바닥면적등급Code).HasColumnName("FootprintTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.높이등급Code).HasColumnName("HeightTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.밀도등급Code).HasColumnName("DensityTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.근거종류Code).HasColumnName("EvidenceKindCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.규칙개정번호).HasColumnName("RuleRevision").HasMaxLength(200).IsRequired();
        builder.Property(item => item.생성시각Utc).HasColumnName("GeneratedAtUtc");
        builder.Property(item => item.ProfileHashSha256).HasMaxLength(64).IsRequired();
        builder.HasOne(item => item.건축물Record)
            .WithMany()
            .HasForeignKey(item => item.건축물RecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class 건축물시각구성계획Configuration
    : IEntityTypeConfiguration<건축물시각구성계획>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물시각구성계획> builder)
    {
        builder.ToTable("public_building_visual_composition_plans");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.건축물형태ProfileId, item.규칙개정번호 }).IsUnique();
        builder.Property(item => item.건축물형태ProfileId).HasColumnName("BuildingMassingProfileId");
        builder.Property(item => item.시각FamilyCode).HasColumnName("VisualFamilyCode").HasMaxLength(80).IsRequired();
        builder.Property(item => item.기준층수).HasColumnName("PresentationFloorCount");
        builder.Property(item => item.중간층반복수).HasColumnName("MiddleFloorRepeatCount");
        builder.Property(item => item.대지점유등급Code).HasColumnName("SiteCoverageTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.주변여백등급Code).HasColumnName("SurroundingSpaceTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.LOD등급Code).HasColumnName("LodTierCode").HasMaxLength(40).IsRequired();
        builder.Property(item => item.표현전용).HasColumnName("PresentationOnly");
        builder.Property(item => item.규칙개정번호).HasColumnName("RuleRevision").HasMaxLength(200).IsRequired();
        builder.Property(item => item.계획HashSha256).HasColumnName("PlanHashSha256").HasMaxLength(64).IsRequired();
        builder.Property(item => item.생성시각Utc).HasColumnName("GeneratedAtUtc");
        builder.HasOne(item => item.건축물형태Profile)
            .WithMany()
            .HasForeignKey(item => item.건축물형태ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class 건축물행정구역AssignmentConfiguration
    : IEntityTypeConfiguration<건축물행정구역Assignment>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물행정구역Assignment> builder)
    {
        builder.ToTable("public_building_region_assignments");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.BuildingRecordId, item.RuleRevision }).IsUnique();
        builder.HasIndex(item => new { item.AdministrativeRegionStableId, item.SourceVintage });
        builder.Property(item => item.LegalRegionStableId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.AdministrativeRegionStableId).HasMaxLength(240);
        builder.Property(item => item.AssignmentMethodCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ConfidenceCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SourceVintage).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RuleRevision).HasMaxLength(200).IsRequired();
        builder.HasOne(item => item.BuildingRecord)
            .WithMany()
            .HasForeignKey(item => item.BuildingRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class 건축물용도CategoryAssignmentConfiguration
    : IEntityTypeConfiguration<건축물용도CategoryAssignment>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<건축물용도CategoryAssignment> builder)
    {
        builder.ToTable("public_building_category_assignments");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.BuildingRecordId, item.RuleRevision, item.IsPrimary }).IsUnique();
        builder.HasIndex(item => new { item.CategoryCode, item.RuleRevision });
        builder.Property(item => item.CategoryCode).HasMaxLength(64).IsRequired();
        builder.Property(item => item.AssignmentMethodCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.EvidenceKindCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RuleRevision).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SourceMainPurposeCode).HasMaxLength(32);
        builder.Property(item => item.SourceMainPurposeName).HasMaxLength(200);
        builder.HasOne(item => item.BuildingRecord)
            .WithMany()
            .HasForeignKey(item => item.BuildingRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Category)
            .WithMany()
            .HasForeignKey(item => item.CategoryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class 행정동건축물CategoryAggregateConfiguration
    : IEntityTypeConfiguration<행정동건축물CategoryAggregate>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<행정동건축물CategoryAggregate> builder)
    {
        builder.ToTable("public_administrative_building_category_aggregates");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new
        {
            item.AdministrativeRegionStableId,
            item.SourceVintage,
            item.CategoryCode,
            item.RuleRevision,
        }).IsUnique();
        builder.Property(item => item.AdministrativeRegionStableId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SourceVintage).HasMaxLength(200).IsRequired();
        builder.Property(item => item.CategoryCode).HasMaxLength(64).IsRequired();
        builder.Property(item => item.BuildingAreaSquareMeters).HasPrecision(24, 4);
        builder.Property(item => item.TotalFloorAreaSquareMeters).HasPrecision(24, 4);
        builder.Property(item => item.EvidenceKindCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RuleRevision).HasMaxLength(200).IsRequired();
        builder.Property(item => item.AggregateHashSha256).HasMaxLength(64).IsRequired();
        builder.HasOne(item => item.Category)
            .WithMany()
            .HasForeignKey(item => item.CategoryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
