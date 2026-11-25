using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Common.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.Google;
using 살뜰.Services.Operations;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송시간창UtcR30Tests
{
    private static readonly DateTime Start = new(2026, 10, 28, 22, 32, 13, DateTimeKind.Utc);
    private static readonly ServiceProvider SqliteServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

    [Theory]
    [InlineData("Z")]
    [InlineData("+00:00")]
    [InlineData("+09:00")]
    public async Task 생성은_JSON의동일순간을UTC로원장에넘기고_관계형재조회뒤한국시각이한번만더해진다(string offset)
    {
        await using var f = await Fixture.Open();
        var window = Window(offset);
        var command = Command(window);

        var result = await f.Create.Handle(command, default);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(window.시작일시, command.픽업시간창시작일시);
        Assert.Equal(window.시작일시.Kind, command.픽업시간창시작일시.Kind);
        var block = f.Sync.LastLedger!.블록목록.Single(item => item.Title == "상차");
        Assert.EndsWith("Z", block.Data["시간창시작"]);
        f.Db.ChangeTracker.Clear();
        AssertStored(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("+00:00")]
    [InlineData("+09:00")]
    public async Task 수정은_검증과저장전에동일순간을정규화하고_SQL재조회로동일UTC를보존한다(string offset)
    {
        await using var f = await Fixture.Open();
        var created = await f.Create.Handle(Command(Window("Z", Start.AddDays(-1))), default);
        Assert.True(created.IsSuccess);
        f.Db.ChangeTracker.Clear();

        var update = Update(created.Value.의뢰Id, Window(offset));
        var original = update.픽업지.시간창시작일시;
        var result = await f.Update.Handle(update, default);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(original, update.픽업지.시간창시작일시);
        Assert.Equal(original!.Value.Kind, update.픽업지.시간창시작일시!.Value.Kind);
        f.Db.ChangeTracker.Clear();
        AssertStored(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void UTC와Local동일순간과_Kind없는기존UTC입력은_같은UTC명세가된다(DateTimeKind kind)
    {
        var start = kind == DateTimeKind.Local ? Start.ToLocalTime() : DateTime.SpecifyKind(Start, kind);
        var end = kind == DateTimeKind.Local ? Start.AddHours(2).ToLocalTime() : DateTime.SpecifyKind(Start.AddHours(2), kind);
        var original = Command(new() { 시작일시 = start, 종료일시 = end });

        var normalized = 화주운송시간창UtcPolicy.정규화(original);

        Assert.Equal(Start, normalized.픽업시간창시작일시);
        Assert.Equal(Start.AddHours(2), normalized.픽업시간창종료일시);
        Assert.Equal(Start, normalized.하차시간창시작일시);
        Assert.Equal(Start.AddHours(2), normalized.하차시간창종료일시);
        Assert.Equal(DateTimeKind.Utc, normalized.픽업시간창시작일시.Kind);
        Assert.Equal(DateTimeKind.Utc, normalized.하차시간창시작일시!.Value.Kind);
        Assert.Equal(kind, original.픽업시간창시작일시.Kind);
    }

    [Fact]
    public async Task Local시작과UTC종료의유효한범위는_현지벽시계가아닌동일시각으로검증한다()
    {
        await using var f = await Fixture.Open();
        var command = Command(new() { 시작일시 = Start.ToLocalTime(), 종료일시 = Start.AddHours(2) });

        var result = await f.Create.Handle(command, default);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        f.Db.ChangeTracker.Clear();
        AssertStored(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
    }

    [Fact]
    public async Task 실제로역전된범위는_정규화후에도거절하고저장하지않는다()
    {
        await using var f = await Fixture.Open();
        var command = Command(new() { 시작일시 = Start.ToLocalTime(), 종료일시 = Start.AddMinutes(-1) });

        var result = await f.Create.Handle(command, default);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Message == "pickup.window.startAt must be before endAt");
        Assert.Empty(await f.Db.화주운송의뢰.ToArrayAsync());
        Assert.Null(f.Sync.LastLedger);
    }

    [Fact]
    public void 누락과기본날짜와부분하차시간창은_새로운시각이나완전한범위로채우지않는다()
    {
        var command = Command(Window("Z")) with
        {
            픽업시간창시작일시 = default, 하차시간창시작일시 = Start.ToLocalTime(), 하차시간창종료일시 = null
        };
        var normalized = 화주운송시간창UtcPolicy.정규화(command);
        Assert.Equal(default(DateTime), normalized.픽업시간창시작일시);
        Assert.Equal(DateTimeKind.Unspecified, normalized.픽업시간창시작일시.Kind);
        Assert.Equal(Start, normalized.하차시간창시작일시);
        Assert.Null(normalized.하차시간창종료일시);

        var update = Update("missing", Window("Z")) with
        {
            픽업지 = new(null, null, null, null, null, null, null, default(DateTime)),
            하차지 = new(null, null, null, null, null, null, null, null)
        };
        var normalizedUpdate = 화주운송시간창UtcPolicy.정규화(update);
        Assert.Null(normalizedUpdate.픽업지.시간창시작일시);
        Assert.Equal(default(DateTime), normalizedUpdate.픽업지.시간창종료일시);
        Assert.Null(normalizedUpdate.하차지.시간창시작일시);
        Assert.Null(normalizedUpdate.하차지.시간창종료일시);
    }

    [Fact]
    public async Task 수락뒤동일순간의다른offset재전송은_핵심조건변경으로오인하지않는다()
    {
        await using var f = await Fixture.Open();
        var created = await f.Create.Handle(Command(Window("Z")), default);
        Assert.True(created.IsSuccess);
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.배차상태 = "확정";
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var result = await f.Update.Handle(Update(created.Value.의뢰Id, Window("+09:00")), default);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        f.Db.ChangeTracker.Clear();
        AssertStored(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
    }

    private static void AssertStored(화주운송의뢰 stored)
    {
        Assert.Equal(Start, stored.픽업_시간창_시작일시);
        Assert.Equal(Start.AddHours(2), stored.픽업_시간창_종료일시);
        Assert.Equal(Start, stored.하차_시간창_시작일시);
        Assert.Equal(Start.AddHours(2), stored.하차_시간창_종료일시);
        // SQLite가 실제 저장·재조회하면서 Kind를 제거해도 UTC ticks는 그대로여야 합니다.
        Assert.Equal(DateTimeKind.Unspecified, stored.픽업_시간창_시작일시.Kind);
        var projected = 화물운송조건Projector.시각(stored.픽업_시간창_시작일시);
        Assert.Equal(DateTimeKind.Utc, projected!.Value.Kind);
        Assert.Equal("2026-10-29 07:32 ~ 09:32 (한국)", 화물운송조건표시.시간창(projected,
            화물운송조건Projector.시각(stored.픽업_시간창_종료일시)));
    }

    private static TimeWindowDTO Window(string offset, DateTime? start = null)
    {
        string Format(DateTime value) => offset == "Z"
            ? value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : new DateTimeOffset(value).ToOffset(offset == "+09:00" ? TimeSpan.FromHours(9) : TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
        var from = start ?? Start;
        return JsonSerializer.Deserialize<TimeWindowDTO>(JsonSerializer.Serialize(new Dictionary<string, string>
        { ["시작일시"] = Format(from), ["종료일시"] = Format(from.AddHours(2)) }))!;
    }

    private static 의뢰생성Command Command(TimeWindowDTO window)
        => 화주운송의뢰일괄등록지원.To의뢰생성Command(new()
        {
            화물종류 = "시간창 시험 상자", 화물수량 = 1, 화물길이Mm = 200, 화물폭Mm = 200,
            화물높이Mm = 100, 화물중량Kg = 1m, 팔레트개수 = 0,
            픽업도로명주소 = "시험 상차지", 하차도로명주소 = "시험 하차지",
            픽업연락처이름 = "담당자", 픽업연락처전화번호 = "010-0000-0000",
            하차연락처이름 = "담당자", 하차연락처전화번호 = "010-0000-0000",
            픽업시간창시작일시 = window.시작일시, 픽업시간창종료일시 = window.종료일시,
            하차시간창시작일시 = window.시작일시, 하차시간창종료일시 = window.종료일시
        }, new() { 운송방식 = "혼적", 차량종류 = "시험 차량", 결제수단 = "계좌이체", 정산시점 = "선결제" });

    private static 의뢰수정Command Update(string id, TimeWindowDTO window)
        => new(id, new(null, null, null), new(null, null, null, null, null, null, null),
            new(null, null, null, null, null, null, window.시작일시, window.종료일시),
            new(null, null, null, null, null, null, window.시작일시, window.종료일시), new(null), null);

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        private static readonly Actor User = new();
        public SsalddelContext Db { get; } = db;
        public 화물적재검증Fixture.LoadingSync Sync { get; } = new(db);
        public 의뢰생성CommandHandler Create => new(Db, new Geo(), User, Sync, new FreightPolicy(), new Handoff());
        public 의뢰수정CommandHandler Update => new(Db, new 화주운송업무담당자UseCase(Db, User, TimeProvider.System),
            ledgerSync: Sync, currentUserAccessor: User);
        public static async Task<Fixture> Open()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
                .UseInternalServiceProvider(SqliteServices).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed record Actor : ICurrentUserAccessor { public string? UserId => "r30-window-shipper"; public string? Role => "화주"; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Geo : IGeocodingService
    { public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address) => Task.FromResult<(decimal lat, decimal lng)?>((37m, 127m)); }
    private sealed class FreightPolicy : IOperatingMarketFreightWorkflowPolicy
    {
        public string MarketCode => "KR";
        public OperatingMarketFreightWorkflowDecision Evaluate(OperatingMarketFreightWorkflowRequest request) => new() { CanProceed = true };
    }
    private sealed class Handoff : I화주운송의뢰화물운송인계Service
    { public Task<운영체제업무인계Dto> 인계Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(new 운영체제업무인계Dto()); }
}
