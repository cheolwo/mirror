namespace Ssalddel.Services.PrivacyRetention;

public sealed class 개인정보보존Options
{
    public const string SectionName = "PrivacyRetention";
    public bool Enabled { get; set; }
    public bool InventoryVerified { get; set; }
    public bool ProvidersVerified { get; set; }
    public bool BackupRestoreBarrierVerified { get; set; }
    public int OperationalDays { get; set; } = 3;
    public int DriverLocationDays { get; set; } = 1;
    public bool EnhancedAuditRetentionRequired { get; set; }
    public int ScanIntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 8;

    public bool Ready => Enabled && InventoryVerified && ProvidersVerified && BackupRestoreBarrierVerified;
    public int AuditYears => EnhancedAuditRetentionRequired ? 2 : 1;
    public DateTime AuditActorScopeEffectiveAtUtc => new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);
    public string AuditReviewPeriodCode => "KoreaCalendarMonth";
}
