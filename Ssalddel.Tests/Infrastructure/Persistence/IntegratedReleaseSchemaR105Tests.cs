using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.판매;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Infrastructure.Persistence;

/// <summary>DB 연결 없이 MySQL 8.4 모델·마이그레이션 순서·생성 SQL을 함께 대조합니다.</summary>
public sealed class IntegratedReleaseSchemaR105Tests
{
    [Fact]
    public void 현재MySql모델과최종TargetModel과Snapshot사이에전체Schema차이가없다()
    {
        using var db = MySql();
        var current = db.GetService<IDesignTimeModel>().Model;
        var migrations = db.GetService<IMigrationsAssembly>();
        var snapshot = migrations.ModelSnapshot!.Model;
        var latest = migrations.Migrations.OrderBy(x => x.Key, StringComparer.Ordinal).Last();
        var final = migrations.CreateMigration(latest.Value, db.Database.ProviderName!).TargetModel;
        Assert.Empty(Differences(db, snapshot, current));
        Assert.Empty(Differences(db, final, current));
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void 배차근거Migration은_실차JSON판본과추천근거Nullable세열만추가한다()
    {
        using var db = MySql();
        var migration = new AddCargoDispatchEvidence();
        var generated = Differences(db, new AddCargoLoadingEvidenceFields().TargetModel, migration.TargetModel);
        Assert.Equal(Signatures(generated), Signatures(migration.UpOperations));
        var additions = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(3, additions.Length);
        Assert.Equal(additions.Length, migration.UpOperations.Count);
        Assert.All(additions, column =>
        {
            Assert.True(column.IsNullable);
            Assert.Null(column.DefaultValue);
            Assert.Null(column.DefaultValueSql);
        });
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void 적재입력근거Migration은_직전TargetModel에Nullable다섯열만추가한다()
    {
        using var db = MySql();
        var migration = new AddCargoLoadingEvidenceFields();
        var generated = Differences(db, new 운송자격검토준비().TargetModel, migration.TargetModel);
        Assert.Equal(Signatures(generated), Signatures(migration.UpOperations));
        var additions = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(5, additions.Length);
        Assert.Equal(additions.Length, migration.UpOperations.Count);
        Assert.Equal(new[]
        {
            "cargo_floor_rotation_allowed", "cargo_input_volume_cbm", "cargo_loading_calculation_revision",
            "cargo_stacking_allowed", "cargo_volume_evidence_code"
        }, additions.Select(x => x.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(additions, column =>
        {
            Assert.Equal("shipper_requests", column.Table);
            Assert.True(column.IsNullable);
            Assert.Null(column.DefaultValue);
            Assert.Null(column.DefaultValueSql);
        });
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void 운송자격Migration은이전최종Schema에서_실차Nullable두열과자격전문표만추가한다()
    {
        using var db = MySql();
        var migration = new 운송자격검토준비();
        var generated = Differences(db, new AddAutomaticStatementReview().TargetModel, migration.TargetModel);
        Assert.Equal(Signatures(generated), Signatures(migration.UpOperations));
        Assert.Equal(2, migration.UpOperations.OfType<AddColumnOperation>().Count());
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("driver_transport_qualification", table.Name);
        Assert.All(migration.UpOperations, operation => Assert.True(operation is AddColumnOperation or CreateTableOperation));
        Assert.All(migration.UpOperations.OfType<AddColumnOperation>(), column =>
        { Assert.True(column.IsNullable); Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql); });
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void 세Migration의각TargetModel차이가실제Up와동일하고기존Schema를건드리지않는다()
    {
        using var db = MySql();
        Migration previous = new AddTransportPrepaymentPayoutBatchR73();
        foreach (var next in NewMigrations())
        {
            var generated = Differences(db, previous.TargetModel, next.TargetModel);
            Assert.Equal(Signatures(generated), Signatures(next.UpOperations));
            Assert.All(next.UpOperations, operation =>
                Assert.True(operation is CreateTableOperation or CreateIndexOperation or AddColumnOperation,
                    $"예상하지 않은 schema 변경: {operation.GetType().Name}"));
            previous = next;
        }

        Assert.Null(new AddFoodCancellationAgreements().TargetModel.FindEntityType(typeof(판매채널계정).FullName!)!.FindProperty("동기화진단Json"));
        Assert.Null(new AddSalesChannelSyncDiagnostics().TargetModel.FindEntityType(typeof(기사운송대금지급요청).FullName!)!.FindProperty("자동명세검토코드"));
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void 취소합의의판본과유일식별자와주문참조및기존행호환Nullable열을보존한다()
    {
        using var db = MySql();
        var model = db.GetService<IDesignTimeModel>().Model;
        var agreement = model.FindEntityType(typeof(음식주문취소합의))!;
        Assert.True(agreement.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.Contains(agreement.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["합의StableId"]));
        var relation = Assert.Single(agreement.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, relation.DeleteBehavior);
        Assert.Equal(typeof(음식주문), relation.PrincipalEntityType.ClrType);
        Assert.Equal(18, agreement.FindProperty("주문자부담액")!.GetPrecision());
        Assert.Equal(2, agreement.FindProperty("음식점부담액")!.GetScale());

        var additions = NewMigrations().SelectMany(x => x.UpOperations).OfType<AddColumnOperation>().ToArray();
        Assert.Equal(3, additions.Length);
        Assert.All(additions, column => { Assert.True(column.IsNullable); Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql); });
        Assert.Equal("utf8mb4", additions.Single(x => x.Name == "동기화진단_json")["MySql:CharSet"]);
        Assert.Equal("utf8mb4", additions.Single(x => x.Name == "automatic_statement_review_code")["MySql:CharSet"]);
        Assert.Equal(80, additions.Single(x => x.Name == "automatic_statement_review_code").MaxLength);
    }

    [Fact]
    public void MySqlSql은새테이블한개와Nullable열세개만추가하며데이터수정삭제를하지않는다()
    {
        using var db = MySql();
        var sql = string.Join('\n', NewMigrations().SelectMany(migration =>
            db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations,
                db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true)))
            .Select(command => command.CommandText));
        Assert.Equal(1, Count(sql, "CREATE TABLE "));
        Assert.Equal(3, Count(sql, "ALTER TABLE "));
        Assert.Contains("CREATE TABLE `음식주문취소합의`", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
        Assert.Contains("CREATE UNIQUE INDEX `IX_음식주문취소합의_합의StableId`", sql);
        Assert.Contains("ADD `동기화진단_json` longtext CHARACTER SET utf8mb4 NULL", sql);
        Assert.Contains("ADD `automatic_statement_review_code` varchar(80) CHARACTER SET utf8mb4 NULL", sql);
        Assert.Contains("ADD `automatic_statement_reviewed_at_utc` datetime(6) NULL", sql);
        // FK의 ON DELETE RESTRICT는 데이터 삭제문이 아니다. 문장 시작의 DML/파괴 DDL만 차단한다.
        Assert.False(Regex.IsMatch(sql,
            @"(?:\A|;)\s*(?:DELETE\s+FROM\b|UPDATE\b|INSERT\s+INTO\b|DROP\b|TRUNCATE\b|ALTER\s+DATABASE\b|CREATE\s+DATABASE\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), sql);
        Assert.Throws<NotSupportedException>(() => new AddSalesChannelSyncDiagnostics().DownOperations);
        Assert.Throws<NotSupportedException>(() => new AddAutomaticStatementReview().DownOperations);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    private static Migration[] NewMigrations() =>
        [new AddFoodCancellationAgreements(), new AddSalesChannelSyncDiagnostics(), new AddAutomaticStatementReview()];

    private static IReadOnlyList<MigrationOperation> Differences(SsalddelContext db, IModel from, IModel to)
    {
        var initializer = db.GetService<IModelRuntimeInitializer>();
        return db.GetService<IMigrationsModelDiffer>().GetDifferences(
            initializer.Initialize(from, designTime: true).GetRelationalModel(),
            initializer.Initialize(to, designTime: true).GetRelationalModel());
    }

    private static string[] Signatures(IEnumerable<MigrationOperation> operations) =>
        operations.Select(Signature).Order(StringComparer.Ordinal).ToArray();

    private static string Signature(MigrationOperation operation) => operation switch
    {
        AddColumnOperation column => $"column|{column.Table}|{Column(column)}",
        CreateIndexOperation index => $"index|{index.Table}|{index.Name}|{index.IsUnique}|{string.Join(',', index.Columns)}|{index.Filter}|{Annotations(index)}",
        CreateTableOperation table => $"table|{table.Schema}|{table.Name}|{Annotations(table)}|"
            + string.Join(';', table.Columns.Select(Column).Order(StringComparer.Ordinal))
            + $"|pk:{table.PrimaryKey!.Name}:{string.Join(',', table.PrimaryKey.Columns)}|fk:"
            + string.Join(';', table.ForeignKeys.Select(fk => $"{fk.Name}:{string.Join(',', fk.Columns)}:{fk.PrincipalTable}:{string.Join(',', fk.PrincipalColumns!)}:{fk.OnDelete}:{fk.OnUpdate}").Order(StringComparer.Ordinal)),
        _ => $"unexpected|{operation.GetType().Name}"
    };

    private static string Column(ColumnOperation column) =>
        $"{column.Name}|{column.ClrType.FullName}|{column.ColumnType}|{column.IsNullable}|{column.MaxLength}|{column.Precision}|{column.Scale}|{column.DefaultValue}|{column.DefaultValueSql}|{column.ComputedColumnSql}|{Annotations(column)}";

    private static string Annotations(MigrationOperation operation) => string.Join(',', operation.GetAnnotations()
        .Select(annotation => $"{annotation.Name}:{Convert.ToString(annotation.Value, CultureInfo.InvariantCulture)}").Order(StringComparer.Ordinal));

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

    private static SsalddelContext MySql() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Port=1;Database=r105_schema_model_only;User=unused;Password=unused;Connection Timeout=1;",
            new MySqlServerVersion(new Version(8, 4, 0)), options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption());

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
