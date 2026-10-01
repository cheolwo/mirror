using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ssalddel.Simulation.Persistence;

internal static class 프랜차이즈공급망ProjectionModelBuilderExtensions
{
    public static void ApplyFranchiseSupplyChainSimulationFoundation(
        this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new 프랜차이즈공급망ProjectionRootConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈본부ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장소속ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈공급자ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈조달계약ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈조달계약품목ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장공급안ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장공급안품목ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장발주ProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new 프랜차이즈매장발주품목ProjectionConfiguration());
    }
}

internal static class 프랜차이즈공급망ProjectionConfiguration
{
    private const string StableIdCollation = "ascii_bin";
    private const string BusinessKeyCollation = "utf8mb4_bin";

    public static PropertyBuilder<string> Text(
        PropertyBuilder<string> property,
        string columnName,
        int maxLength) => property
            .HasColumnName(columnName)
            .HasMaxLength(maxLength)
            .IsRequired();

    public static PropertyBuilder<string> StableId(
        PropertyBuilder<string> property,
        string columnName,
        int maxLength = 200) => Text(property, columnName, maxLength)
            .HasCharSet("ascii")
            .UseCollation(StableIdCollation);

    public static PropertyBuilder<string> BusinessKey(
        PropertyBuilder<string> property,
        string columnName,
        int maxLength) => Text(property, columnName, maxLength)
            .UseCollation(BusinessKeyCollation);

    public static PropertyBuilder<T> Value<T>(
        PropertyBuilder<T> property,
        string columnName) => property.HasColumnName(columnName);
}

internal sealed class 프랜차이즈공급망ProjectionRootConfiguration
    : IEntityTypeConfiguration<프랜차이즈공급망ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈공급망ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈공급망Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈공급망Projection_읽기전용Simulation",
                "`실행모드코드` = 'Simulation' AND `상태권위코드` = 'SessionAggregate' AND `합성자료여부` = 1 AND `운영상태여부` = 0 AND `읽기전용Projection여부` = 1"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.ScenarioStableId,
        }).IsUnique();
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SessionStableId), "세션고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.NetworkStableId), "공급망고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ScenarioStableId), "Scenario고유식별자");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.SourceKindCode), "자료종류코드", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.ModeCode), "실행모드코드", 30);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StateAuthorityCode), "상태권위코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.IsSynthetic), "합성자료여부");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.IsOperationalState), "운영상태여부");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.IsReadOnlyProjection), "읽기전용Projection여부");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.DataRevision), "자료개정번호", 120);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.RuleRevision), "규칙개정번호", 120);
        builder.Property(value => value.SourceStableIdsJson)
            .HasColumnName("출처고유식별자목록JSON")
            .HasColumnType("longtext")
            .IsRequired();
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatePayloadHashSha256),
            "상태PayloadSHA256", 64);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.SourceSessionRevision), "원본세션개정번호");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.ProjectionRevision), "Projection개정번호");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.AsOfWorldTick), "기준WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.GeneratedAtUtc), "생성시각UTC");
    }
}

internal sealed class 프랜차이즈본부ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈본부ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈본부ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈본부Projection");
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersCode,
        }).IsUnique();
        builder.HasOne(value => value.Network)
            .WithMany(value => value.Headquarters)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.HeadquartersCode), "본부코드", 80);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.DisplayName), "표시명", 200);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }

    internal static void MapScope<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property<string>(nameof(프랜차이즈본부ProjectionEntity.SessionStableId)),
            "세션고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property<string>(nameof(프랜차이즈본부ProjectionEntity.NetworkStableId)),
            "공급망고유식별자");
    }
}

internal sealed class 프랜차이즈매장ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장Projection");
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.StoreStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.StoreCode,
        }).IsUnique();
        builder.HasOne(value => value.Network)
            .WithMany(value => value.Stores)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.StoreStableId), "매장고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.StoreCode), "매장코드", 80);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.DisplayName), "표시명", 200);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SemanticPlaceStableId), "의미장소고유식별자");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈매장소속ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장소속ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장소속ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장소속Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈매장소속Projection_유효기간",
                "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.MembershipStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.MembershipStableId,
            value.StoreStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.StoreStableId,
            value.EffectiveFromWorldTick,
        }).IsUnique();
        builder.HasOne(value => value.Headquarters)
            .WithMany(value => value.StoreMemberships)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Store)
            .WithMany(value => value.Memberships)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.StoreStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.MembershipStableId), "매장소속고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.StoreStableId), "매장고유식별자");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveFromWorldTick), "시작WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveUntilWorldTick), "종료WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈공급자ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈공급자ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈공급자ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈공급자Projection");
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.SupplierStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.SupplierKey,
        }).IsUnique();
        builder.HasOne(value => value.Network)
            .WithMany(value => value.Suppliers)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SupplierStableId), "공급자고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.SupplierKey), "공급자키", 160);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.DisplayName), "표시명", 200);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.SupplierKindCode), "공급자종류코드", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈조달계약ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈조달계약ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈조달계약ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈조달계약Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈조달계약Projection_유효기간",
                "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.ContractStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.ContractStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.ContractNumber,
        }).IsUnique();
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.SupplierStableId,
            value.StatusCode,
        });
        builder.HasOne(value => value.Headquarters)
            .WithMany(value => value.SourcingAgreements)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Supplier)
            .WithMany(value => value.SourcingAgreements)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.SupplierStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ContractStableId), "조달계약고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SupplierStableId), "공급자고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.ContractNumber), "계약번호", 100);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.ContractDocumentVersion), "계약문서판본", 100);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveFromWorldTick), "시작WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveUntilWorldTick), "종료WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CurrencyCode), "통화코드", 3);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈조달계약품목ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈조달계약품목ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈조달계약품목ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈조달계약품목Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈조달계약품목Projection_수량가격",
                "`포장내용수량` > 0 AND `단가` >= 0 AND `최소발주수량` > 0 AND (`최대발주수량` IS NULL OR `최대발주수량` >= `최소발주수량`)"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.ContractItemStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.ContractStableId,
            value.ContractItemStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.ContractStableId,
            value.SupplierSku,
        }).IsUnique();
        builder.HasOne(value => value.Contract)
            .WithMany(value => value.Items)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.ContractStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.ContractStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ContractItemStableId), "조달계약품목고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ContractStableId), "조달계약고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ProductStableId), "상품고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.SupplierSku), "공급자SKU", 120);
        MapItemTerms(builder.Property(value => value.ItemName),
            builder.Property(value => value.OrderUnitCode),
            builder.Property(value => value.PackageContentQuantity),
            builder.Property(value => value.PackageContentUnitCode),
            builder.Property(value => value.ConversionRuleRevision),
            builder.Property(value => value.UnitPrice),
            builder.Property(value => value.MinimumOrderQuantity),
            builder.Property(value => value.MaximumOrderQuantity),
            builder.Property(value => value.StorageConditionCode),
            builder.Property(value => value.StatusCode),
            builder.Property(value => value.Revision));
        builder.Property(value => value.SourceStableIdsJson)
            .HasColumnName("출처고유식별자목록JSON")
            .HasColumnType("longtext")
            .IsRequired();
    }

    internal static void MapItemTerms(
        PropertyBuilder<string> itemName,
        PropertyBuilder<string> orderUnit,
        PropertyBuilder<decimal> contentQuantity,
        PropertyBuilder<string> contentUnit,
        PropertyBuilder<string> conversionRevision,
        PropertyBuilder<decimal> unitPrice,
        PropertyBuilder<decimal> minimum,
        PropertyBuilder<decimal?> maximum,
        PropertyBuilder<string> storage,
        PropertyBuilder<string> status,
        PropertyBuilder<long> revision)
    {
        프랜차이즈공급망ProjectionConfiguration.Text(itemName, "품목명", 200);
        프랜차이즈공급망ProjectionConfiguration.Text(orderUnit, "발주단위코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            contentQuantity.HasPrecision(18, 4), "포장내용수량");
        프랜차이즈공급망ProjectionConfiguration.Text(contentUnit, "포장내용단위코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Text(conversionRevision, "단위변환규칙개정번호", 120);
        프랜차이즈공급망ProjectionConfiguration.Value(
            unitPrice.HasPrecision(18, 2), "단가");
        프랜차이즈공급망ProjectionConfiguration.Value(
            minimum.HasPrecision(18, 4), "최소발주수량");
        프랜차이즈공급망ProjectionConfiguration.Value(
            maximum.HasPrecision(18, 4), "최대발주수량");
        프랜차이즈공급망ProjectionConfiguration.Text(storage, "보관조건코드", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(status, "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(revision, "원본개정번호");
    }
}

internal sealed class 프랜차이즈매장공급안ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장공급안ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장공급안ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장공급안Projection", table =>
        {
            table.HasCheckConstraint(
                "CK_프랜차이즈매장공급안Projection_첫Slice",
                "`상업흐름모형코드` = 'HeadquartersResale' AND `이행모형코드` = 'HeadquartersFleet'");
            table.HasCheckConstraint(
                "CK_프랜차이즈매장공급안Projection_유효기간",
                "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`");
        });
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OfferStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.OfferStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.OfferNumber,
        }).IsUnique();
        builder.HasOne(value => value.Headquarters)
            .WithMany(value => value.StoreSupplyOffers)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferStableId), "매장공급안고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.OfferNumber), "공급안번호", 100);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.OfferDocumentVersion), "공급안문서판본", 100);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CommercialFlowModelCode), "상업흐름모형코드", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.FulfillmentModelCode), "이행모형코드", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveFromWorldTick), "시작WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.EffectiveUntilWorldTick), "종료WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CurrencyCode), "통화코드", 3);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈매장공급안품목ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장공급안품목ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장공급안품목ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장공급안품목Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈매장공급안품목Projection_수량가격",
                "`포장내용수량` > 0 AND `단가` >= 0 AND `최소발주수량` > 0 AND (`최대발주수량` IS NULL OR `최대발주수량` >= `최소발주수량`)"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OfferItemStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.OfferStableId,
            value.OfferItemStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OfferStableId,
            value.StoreSku,
        }).IsUnique();
        builder.HasOne(value => value.Offer)
            .WithMany(value => value.Items)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SourceContractItem)
            .WithMany(value => value.StoreOfferItems)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.SourceContractStableId,
                value.SourceContractItemStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.ContractStableId,
                value.ContractItemStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferItemStableId), "매장공급안품목고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferStableId), "매장공급안고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SourceContractStableId), "원본조달계약고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SourceContractItemStableId), "원본조달계약품목고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ProductStableId), "상품고유식별자");
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.StoreSku), "매장공급SKU", 120);
        프랜차이즈조달계약품목ProjectionConfiguration.MapItemTerms(
            builder.Property(value => value.ItemName),
            builder.Property(value => value.OrderUnitCode),
            builder.Property(value => value.PackageContentQuantity),
            builder.Property(value => value.PackageContentUnitCode),
            builder.Property(value => value.ConversionRuleRevision),
            builder.Property(value => value.UnitPrice),
            builder.Property(value => value.MinimumOrderQuantity),
            builder.Property(value => value.MaximumOrderQuantity),
            builder.Property(value => value.StorageConditionCode),
            builder.Property(value => value.StatusCode),
            builder.Property(value => value.Revision));
        builder.Property(value => value.SourceStableIdsJson)
            .HasColumnName("출처고유식별자목록JSON")
            .HasColumnType("longtext")
            .IsRequired();
    }
}

internal sealed class 프랜차이즈매장발주ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장발주ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장발주ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장발주Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈매장발주Projection_합계",
                "`발주합계금액사본` >= 0"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OrderStableId,
        });
        builder.HasAlternateKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.HeadquartersStableId,
            value.OfferStableId,
            value.OrderStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.StoreMembershipStableId,
            value.ClientRequestStableId,
        }).IsUnique();
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.StoreMembershipStableId,
            value.OrderNumber,
        }).IsUnique();
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OfferStableId,
            value.StatusCode,
            value.RequestedDeliveryWorldTick,
        });
        builder.HasOne(value => value.StoreMembership)
            .WithMany(value => value.Orders)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.StoreMembershipStableId,
                value.StoreStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.MembershipStableId,
                value.StoreStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Offer)
            .WithMany(value => value.Orders)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OrderStableId), "매장발주고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.StoreMembershipStableId), "매장소속고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.StoreStableId), "매장고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferStableId), "매장공급안고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ClientRequestStableId), "요청고유식별자");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.RequestPayloadHashSha256), "요청PayloadSHA256", 64);
        프랜차이즈공급망ProjectionConfiguration.BusinessKey(
            builder.Property(value => value.OrderNumber), "발주번호", 100);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.OfferDocumentVersionSnapshot), "공급안문서판본사본", 100);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.SellerHeadquartersStableIdSnapshot), "판매본부고유식별자사본");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CommercialFlowModelCodeSnapshot), "상업흐름모형코드사본", 60);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.FulfillmentModelCodeSnapshot), "이행모형코드사본", 60);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.DeliverySiteStableIdSnapshot), "납품지고유식별자사본");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CurrencyCodeSnapshot), "통화코드사본", 3);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StatusCode), "상태코드", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.RequestedDeliveryWorldTick), "요청납품WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.CreatedWorldTick), "생성WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.SubmittedWorldTick), "제출WorldTick");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.TotalAmountSnapshot)
                .HasPrecision(18, 2), "발주합계금액사본");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}

internal sealed class 프랜차이즈매장발주품목ProjectionConfiguration
    : IEntityTypeConfiguration<프랜차이즈매장발주품목ProjectionEntity>
{
    public void Configure(EntityTypeBuilder<프랜차이즈매장발주품목ProjectionEntity> builder)
    {
        builder.ToTable("시뮬레이션_프랜차이즈매장발주품목Projection", table =>
            table.HasCheckConstraint(
                "CK_프랜차이즈매장발주품목Projection_수량가격",
                "`요청발주단위수량` > 0 AND `요청내용수량사본` > 0 AND (`수락발주단위수량` IS NULL OR (`수락발주단위수량` >= 0 AND `수락발주단위수량` <= `요청발주단위수량`)) AND `품목금액사본` >= 0"));
        builder.HasKey(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OrderLineStableId,
        });
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.NetworkStableId,
            value.OrderStableId,
            value.OfferItemStableId,
        }).IsUnique();
        builder.HasOne(value => value.Order)
            .WithMany(value => value.Lines)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
                value.OrderStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
                value.OrderStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.OfferItem)
            .WithMany(value => value.OrderLines)
            .HasForeignKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
                value.OfferItemStableId,
            })
            .HasPrincipalKey(value => new
            {
                value.SessionStableId,
                value.NetworkStableId,
                value.HeadquartersStableId,
                value.OfferStableId,
                value.OfferItemStableId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        프랜차이즈본부ProjectionConfiguration.MapScope(builder);
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OrderLineStableId), "매장발주품목고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.HeadquartersStableId), "본부고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferStableId), "매장공급안고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OrderStableId), "매장발주고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.OfferItemStableId), "매장공급안품목고유식별자");
        프랜차이즈공급망ProjectionConfiguration.StableId(
            builder.Property(value => value.ProductStableIdSnapshot), "상품고유식별자사본");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.StoreSkuSnapshot), "매장공급SKU사본", 120);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.ItemNameSnapshot), "품목명사본", 200);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.OrderUnitCodeSnapshot), "발주단위코드사본", 40);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.PackageContentQuantitySnapshot)
                .HasPrecision(18, 4), "포장내용수량사본");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.PackageContentUnitCodeSnapshot), "포장내용단위코드사본", 40);
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.ConversionRuleRevisionSnapshot), "단위변환규칙개정번호사본", 120);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.UnitPriceSnapshot)
                .HasPrecision(18, 2), "단가사본");
        프랜차이즈공급망ProjectionConfiguration.Text(
            builder.Property(value => value.CurrencyCodeSnapshot), "통화코드사본", 3);
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.RequestedOrderUnitQuantity)
                .HasPrecision(18, 4), "요청발주단위수량");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.RequestedContentQuantitySnapshot)
                .HasPrecision(18, 4), "요청내용수량사본");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.AcceptedOrderUnitQuantity)
                .HasPrecision(18, 4), "수락발주단위수량");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.LineAmountSnapshot)
                .HasPrecision(18, 2), "품목금액사본");
        프랜차이즈공급망ProjectionConfiguration.Value(
            builder.Property(value => value.Revision), "원본개정번호");
    }
}
