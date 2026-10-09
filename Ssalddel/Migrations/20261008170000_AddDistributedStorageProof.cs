using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261008170000_AddDistributedStorageProof")]
public sealed class AddDistributedStorageProof : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        var agreement = Table("distributed_storage_agreements", "AgreementId");
        Strings(agreement, 64, "AgreementId", "SiteId", "LocationId");
        Strings(agreement, 191, "SpaceOwnerUserId", "GoodsOwnerUserId", "CreatorUserId");
        Strings(agreement, null, "TermsJson", "HistoryJson"); Strings(agreement, 40, "StatusCode");
        Column(agreement, "TermsVersion", typeof(int), "int"); Column(agreement, "Revision", typeof(long), "bigint");
        Column(agreement, "SpaceOwnerConsented", typeof(bool), "tinyint(1)"); Column(agreement, "GoodsOwnerConsented", typeof(bool), "tinyint(1)");
        Column(agreement, "StartsAtUtc", typeof(DateTime), "datetime(6)"); Column(agreement, "EndsAtUtc", typeof(DateTime), "datetime(6)");
        agreement.ForeignKeys.Add(new() { Name = "FK_distributed_storage_agreements_locations", Table = agreement.Name, Columns = ["LocationId"], PrincipalTable = "distributed_locations", PrincipalColumns = ["LocationId"], OnDelete = ReferentialAction.Restrict });
        m.Operations.Add(agreement); Index(m, agreement.Name, ["LocationId", "GoodsOwnerUserId"], true);
        m.AddColumn<string>("StorageAgreementId", "distributed_lots", type: "varchar(64)", maxLength: 64, nullable: true);
        m.AddColumn<string>("StorageAgreementSnapshotJson", "distributed_lots", type: "longtext", nullable: false, defaultValue: "[]");
        var photo = Table("distributed_photo_evidence", "EvidenceId");
        Strings(photo, 64, "EvidenceId", "AggregateId", "BindingDigest", "InputSha256", "StoredSha256");
        Strings(photo, 191, "ActorUserId"); Strings(photo, 20, "Kind"); Strings(photo, 30, "ContentType", "StatusCode");
        Strings(photo, 100, "DeletionLimitationCode"); Strings(photo, null, "PrivateDetailsJson");
        Column(photo, "UploadRequestId", typeof(Guid), "char(36)"); Column(photo, "CompletionRequestId", typeof(Guid), "char(36)");
        Column(photo, "Bytes", typeof(long), "bigint"); Column(photo, "Revision", typeof(long), "bigint");
        Column(photo, "CreatedAtUtc", typeof(DateTime), "datetime(6)");
        foreach (var name in new[] { "UploadedAtUtc", "BoundAtUtc", "CustodyEndedAtUtc", "DeleteAfterUtc", "DeletedAtUtc" }) Column(photo, name, typeof(DateTime), "datetime(6)", true);
        Column(photo, "ActiveObjectAbsent", typeof(bool), "tinyint(1)"); Column(photo, "HistoricalCopiesVerifiedAbsent", typeof(bool), "tinyint(1)");
        m.Operations.Add(photo); Index(m, photo.Name, ["ActorUserId", "UploadRequestId"], true); Index(m, photo.Name, ["ActorUserId", "CompletionRequestId"]);
        Index(m, photo.Name, ["StatusCode", "CreatedAtUtc"]); Index(m, photo.Name, ["DeleteAfterUtc"]);
        var hold = Table("distributed_photo_holds", "CaseId"); Strings(hold, 100, "CaseId"); Strings(hold, 64, "SourceId"); Strings(hold, 40, "SourceKind"); Strings(hold, null, "LegalBasisJson");
        Column(hold, "Sequence", typeof(long), "bigint"); Column(hold, "Revision", typeof(long), "bigint"); Column(hold, "Active", typeof(bool), "tinyint(1)");
        Column(hold, "ReviewAtUtc", typeof(DateTime), "datetime(6)", true); m.Operations.Add(hold); Index(m, hold.Name, ["SourceKind", "SourceId", "Active"]);
    }
    private static CreateTableOperation Table(string name, string primary)
        => new() { Name = name, PrimaryKey = new() { Name = "PK_" + name, Table = name, Columns = [primary] } };
    private static void Column(CreateTableOperation t, string name, Type type, string sql, bool nullable = false, int? length = null)
        => t.Columns.Add(new() { Name = name, Table = t.Name, ClrType = type, ColumnType = sql, IsNullable = nullable, MaxLength = length });
    private static void Strings(CreateTableOperation t, int? max, params string[] names)
    { foreach (var name in names) Column(t, name, typeof(string), max is null ? "longtext" : $"varchar({max})", length: max); }
    private static void Index(MigrationBuilder m, string table, string[] columns, bool unique = false)
        => m.CreateIndex("IX_" + table + "_" + string.Join("_", columns), table, columns, unique: unique);
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("보관 합의와 사진 증빙을 삭제하기 전에 재고·사건·보존 의무를 명시적으로 정리해야 합니다.");
}
