using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.배달권;

namespace 살뜰.Infrastructure.Persistence.Configurations.DeliveryZones;

public sealed class 배달운영권역Configuration : IEntityTypeConfiguration<배달운영권역>
{
    public void Configure(EntityTypeBuilder<배달운영권역> builder)
    {
        builder.ToTable("delivery_operating_territories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.권역고유식별자)
            .HasColumnName("territory_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.표시명)
            .HasColumnName("display_name")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.SourceScopeStableId)
            .HasColumnName("source_scope_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.상태Code)
            .HasColumnName("status_code")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.권역고유식별자)
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territories_stable_id");
    }
}

public sealed class 배달운영권역행정동MembershipConfiguration
    : IEntityTypeConfiguration<배달운영권역행정동Membership>
{
    public void Configure(EntityTypeBuilder<배달운영권역행정동Membership> builder)
    {
        builder.ToTable("delivery_operating_territory_admin_dongs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.배달운영권역Id).HasColumnName("territory_id");
        builder.Property(x => x.행정동고유식별자)
            .HasColumnName("administrative_area_stable_id")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.행정동표시명)
            .HasColumnName("administrative_area_display_name")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.법정동목록Json)
            .HasColumnName("legal_areas_json")
            .HasColumnType("json")
            .IsRequired();
        builder.Property(x => x.상태Code)
            .HasColumnName("membership_state_code")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.현행행정동유일성Key)
            .HasColumnName("active_administrative_area_key")
            .HasMaxLength(80);
        builder.Property(x => x.관할SourceId)
            .HasColumnName("jurisdiction_source_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.관할DataRevision)
            .HasColumnName("jurisdiction_data_revision")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.IncludedAtUtc).HasColumnName("included_at_utc");
        builder.Property(x => x.ExcludedAtUtc).HasColumnName("excluded_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasOne(x => x.배달운영권역)
            .WithMany(x => x.행정동Memberships)
            .HasForeignKey(x => x.배달운영권역Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.배달운영권역Id, x.행정동고유식별자 })
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territory_admin_dong_history");
        builder.HasIndex(x => x.현행행정동유일성Key)
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territory_active_admin_dong");
    }
}

public sealed class 배달운영권역CommandReceiptConfiguration
    : IEntityTypeConfiguration<배달운영권역CommandReceipt>
{
    public void Configure(EntityTypeBuilder<배달운영권역CommandReceipt> builder)
    {
        builder.ToTable("delivery_operating_territory_command_receipts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ClientRequestId)
            .HasColumnName("client_request_id")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(x => x.OperationCode)
            .HasColumnName("operation_code")
            .HasMaxLength(60)
            .IsRequired();
        builder.Property(x => x.RequestHashSha256)
            .HasColumnName("request_hash_sha256")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.ActorUserStableId)
            .HasColumnName("actor_user_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.배달운영권역고유식별자)
            .HasColumnName("territory_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.ResultRevision).HasColumnName("result_revision");
        builder.Property(x => x.ResultJson)
            .HasColumnName("result_json")
            .HasColumnType("json")
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => x.ClientRequestId)
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territory_client_request");
    }
}

public sealed class 배달운영권역변경OutboxConfiguration
    : IEntityTypeConfiguration<배달운영권역변경Outbox>
{
    public void Configure(EntityTypeBuilder<배달운영권역변경Outbox> builder)
    {
        builder.ToTable("delivery_operating_territory_change_outbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventStableId)
            .HasColumnName("event_stable_id")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.AggregateStableId)
            .HasColumnName("aggregate_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.AggregateRevision).HasColumnName("aggregate_revision");
        builder.Property(x => x.EventTypeCode)
            .HasColumnName("event_type_code")
            .HasMaxLength(60)
            .IsRequired();
        builder.Property(x => x.ActorUserStableId)
            .HasColumnName("actor_user_stable_id")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("json")
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");

        builder.HasIndex(x => x.EventStableId)
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territory_event");
        builder.HasIndex(x => new { x.AggregateStableId, x.AggregateRevision })
            .IsUnique()
            .HasDatabaseName("ux_delivery_operating_territory_revision_event");
    }
}
