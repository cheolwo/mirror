namespace Ssalddel.Contracts.Common.PrivacyRetention;

public static class 개인정보파기원천Codes
{
    public const string FoodOrder = "FoodOrder";
    public const string NeighborhoodDelivery = "NeighborhoodDelivery";
    public const string AccessAudit = "AccessAudit";
    public const string DriverLocation = "DriverLocation";
}

public static class 개인정보파기상태Codes
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Retry = "Retry";
    public const string Blocked = "Blocked";
    public const string Completed = "Completed";
}

public sealed record 개인정보파기결과Dto(string JobId, string SourceCode, string RecordId,
    string StatusCode, DateTime? CompletedAtUtc, string FailureCode, IReadOnlyList<string> VerifiedStorageCodes);

public sealed record 개인정보보존항목Dto(string DataClassCode, string Purpose, string LegalBasis,
    string RetentionRule, IReadOnlyList<string> StorageCodes, bool Implemented);

public static class 개인정보보존항목Catalog
{
    public static readonly IReadOnlyList<개인정보보존항목Dto> Items =
    [
        new("Advertising", "표시·광고 근거", "전자상거래법 시행령 제6조", "6 months", ["ApprovedAdvertisingArchive"], false),
        new("ContractAndSupply", "계약·철회·대금·공급 증빙", "전자상거래법 시행령 제6조", "5 years", ["privacy_retained_evidence"], true),
        new("ConsumerDispute", "불만·분쟁 처리 증빙", "전자상거래법 시행령 제6조", "3 years", ["SupportCaseEncryptedStore"], false),
        new("FoodOperationalCopy", "배송 수행", "개인정보 보호법 제21조", "terminal + 3 days", ["RDB.Food", "RDB.Transport", "RDB.LinkedHistory", "RDB.FoodOutbox", "Mongo.community_ledgers"], true),
        new("NeighborhoodOperationalCopy", "생활 배송 수행", "개인정보 보호법 제21조", "terminal + 3 days", ["RDB.Request", "RDB.Transport", "RDB.LinkedHistory", "Mongo.community_ledgers"], true),
        new("DriverLocation", "현재 운행 위치", "목적 달성 후 파기", "1 day by default; active location preserved", ["RDB.DriverLocation"], true),
        new("AccessAudit", "개인정보 접속 감사", "개인정보 안전성 확보조치 기준", "1 year; applicable enhanced class 2 years", ["RDB.UserActivityAudit"], true),
        new("Attachments", "업무 증빙 첨부", "항목별 목적·보존 근거 확인", "purpose-specific", ["ObjectStorage", "VersionCopies", "Backup"], false),
        new("SellerVerification", "판매자 확인·검증 이력", "거래 유형별 법적 근거·분쟁 보존 검토", "approved identity record policy required", ["CommerceSellerEncryptedStore"], false),
        new("ConsentEvidence", "동의·철회와 고지 증빙", "정보 제공 및 거래 근거", "purpose-specific evidence policy required", ["ConsentEvidenceStore"], false),
        new("SavedAddressBook", "사용자 선택 주소 저장", "별도 계속 이용 목적", "until withdrawal or account closure", ["RDB.AddressBook"], false),
        new("Backups", "복구", "복원 시 파기 대상 재노출 방지", "approved backup expiry; restore manifest required", ["Backup", "privacy_deletion_manifest"], false)
    ];
}
