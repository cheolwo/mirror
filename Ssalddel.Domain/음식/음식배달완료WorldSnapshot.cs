using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식배달완료_WorldSnapshot")]
public sealed class 음식배달완료WorldSnapshot
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_outbox_id")]
    public long 원천OutboxId { get; set; }

    [Column("snapshot_stable_id")]
    [MaxLength(120)]
    public string SnapshotStableId { get; set; } = string.Empty;

    [Column("area_stable_id")]
    [MaxLength(120)]
    public string AreaStableId { get; set; } = string.Empty;

    [Column("lifecycle_revision")]
    public long LifecycleRevision { get; set; }

    [Column("outcome_code")]
    [MaxLength(40)]
    public string OutcomeCode { get; set; } = string.Empty;

    [Column("completed_at_utc")]
    public DateTime CompletedAtUtc { get; set; }

    [Column("published_at_utc")]
    public DateTime PublishedAtUtc { get; set; }

    [Column("expires_at_utc")]
    public DateTime ExpiresAtUtc { get; set; }

    [Column("orderer_actor_stable_id")]
    [MaxLength(160)]
    public string OrdererActorStableId { get; set; } = string.Empty;

    [Column("restaurant_actor_stable_id")]
    [MaxLength(160)]
    public string RestaurantActorStableId { get; set; } = string.Empty;

    [Column("driver_actor_stable_id")]
    [MaxLength(160)]
    public string DriverActorStableId { get; set; } = string.Empty;

    [Column("milestones_json")]
    public string MilestonesJson { get; set; } = "[]";
}
