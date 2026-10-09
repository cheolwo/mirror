using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261008190000_AddNeighborhoodSettlement")]
public sealed class AddNeighborhoodSettlement : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        var agreement = Table("neighborhood_settlement_agreements", "AgreementId");
        Strings(agreement, 64, "AgreementId"); Strings(agreement, 30, "SourceKind", "ActivityKind", "StatusCode"); Strings(agreement, 120, "SourceId"); Strings(agreement, 191, "CreatorUserId");
        Strings(agreement, null, "TermsJson", "SourceSnapshotJson", "ConsentsJson", "HistoryJson");
        Column(agreement, "TermsVersion", typeof(int), "int"); Column(agreement, "Revision", typeof(long), "bigint");
        Column(agreement, "CreatedAtUtc", typeof(DateTime), "datetime(6)"); Column(agreement, "UpdatedAtUtc", typeof(DateTime), "datetime(6)"); m.Operations.Add(agreement);
        Index(m, "IX_neighborhood_agreement_source", agreement.Name, ["SourceKind", "SourceId"], true);
        var version = Table("neighborhood_settlement_versions", "AgreementId", "TermsVersion");
        Strings(version, 64, "AgreementId"); Column(version, "TermsVersion", typeof(int), "int"); Strings(version, null, "TermsJson", "SourceSnapshotJson", "ConsentsJson");
        Column(version, "CreatedAtUtc", typeof(DateTime), "datetime(6)"); Foreign(version, "FK_neighborhood_version_agreement", ["AgreementId"], agreement.Name, ["AgreementId"]); m.Operations.Add(version);
        var entry = Table("neighborhood_settlement_entries", "EntryId");
        Strings(entry, 64, "EntryId", "AgreementId", "LineKey", "AccrualKey"); Strings(entry, 30, "Kind", "StatusCode"); Strings(entry, 40, "OriginKind"); Strings(entry, 120, "OriginId"); Strings(entry, null, "DetailsJson");
        Column(entry, "CanonicalDeliveryOrigin", typeof(string), "varchar(191)", true, 191); Column(entry, "OriginalEntryId", typeof(string), "varchar(64)", true, 64);
        Column(entry, "TermsVersion", typeof(int), "int"); Column(entry, "OriginRevision", typeof(long), "bigint"); Column(entry, "Revision", typeof(long), "bigint"); Column(entry, "RecordedAtUtc", typeof(DateTime), "datetime(6)");
        Foreign(entry, "FK_neighborhood_entry_version", ["AgreementId", "TermsVersion"], version.Name, ["AgreementId", "TermsVersion"]);
        Foreign(entry, "FK_neighborhood_entry_original", ["OriginalEntryId"], entry.Name, ["EntryId"]); m.Operations.Add(entry);
        Index(m, "IX_neighborhood_entry_accrual", entry.Name, ["AccrualKey"], true); Index(m, "IX_neighborhood_entry_delivery_origin", entry.Name, ["CanonicalDeliveryOrigin"], true);
        Index(m, "IX_neighborhood_entry_version", entry.Name, ["AgreementId", "TermsVersion"]); Index(m, "IX_neighborhood_entry_original", entry.Name, ["OriginalEntryId"]);
        var payment = Table("neighborhood_settlement_payments", "PaymentId"); Strings(payment, 64, "PaymentId", "AgreementId"); Strings(payment, null, "DetailsJson"); Strings(payment, 30, "StatusCode");
        Column(payment, "Revision", typeof(long), "bigint"); Column(payment, "ReportedAtUtc", typeof(DateTime), "datetime(6)"); Column(payment, "RecipientConfirmedAtUtc", typeof(DateTime), "datetime(6)", true);
        Foreign(payment, "FK_neighborhood_payment_agreement", ["AgreementId"], agreement.Name, ["AgreementId"]); m.Operations.Add(payment); Index(m, "IX_neighborhood_payment_agreement", payment.Name, ["AgreementId"]);
        var receipt = Table("neighborhood_settlement_receipts", "ActorUserId", "RequestId"); Strings(receipt, 191, "ActorUserId"); Strings(receipt, 64, "AgreementId", "InputDigest"); Strings(receipt, 40, "Kind"); Strings(receipt, null, "ResponseJson");
        Column(receipt, "RequestId", typeof(Guid), "char(36)"); Column(receipt, "Revision", typeof(long), "bigint"); Column(receipt, "RecordedAtUtc", typeof(DateTime), "datetime(6)");
        Foreign(receipt, "FK_neighborhood_receipt_agreement", ["AgreementId"], agreement.Name, ["AgreementId"]); m.Operations.Add(receipt); Index(m, "IX_neighborhood_receipt_agreement", receipt.Name, ["AgreementId"]);
    }
    private static CreateTableOperation Table(string name, params string[] primary)
        => new() { Name = name, PrimaryKey = new() { Name = "PK_" + name, Table = name, Columns = primary } };
    private static void Column(CreateTableOperation t, string name, Type type, string sql, bool nullable = false, int? length = null)
        => t.Columns.Add(new() { Name = name, Table = t.Name, ClrType = type, ColumnType = sql, IsNullable = nullable, MaxLength = length });
    private static void Strings(CreateTableOperation t, int? max, params string[] names)
    { foreach (var name in names) Column(t, name, typeof(string), max is null ? "longtext" : $"varchar({max})", length: max); }
    private static void Index(MigrationBuilder m, string name, string table, string[] columns, bool unique = false) => m.CreateIndex(name, table, columns, unique: unique);
    private static void Foreign(CreateTableOperation t, string name, string[] columns, string principal, string[] principalColumns)
        => t.ForeignKeys.Add(new() { Name = name, Table = t.Name, Columns = columns, PrincipalTable = principal, PrincipalColumns = principalColumns, OnDelete = ReferentialAction.Restrict });
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("약정·발생·정정·지급 신고의 정산 원장은 별도의 보존 정리 없이 삭제할 수 없습니다.");
}
