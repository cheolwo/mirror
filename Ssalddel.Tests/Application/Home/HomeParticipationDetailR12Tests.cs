using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Home;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Controllers.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Home;

public sealed class HomeParticipationDetailR12Tests
{
    [Fact]
    public async Task 누적과월별은같은완료근거를사용하고한국시간월경계와서로다른계정을구분한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        db.Users.AddRange(new() { Id = "shipper-A" }, new() { Id = "shipper-B" }, new() { Id = "driver-A" }, new() { Id = "driver-B" });
        Add(db, 1, Utc(2026, 9, 30, 14, 59));
        Add(db, 2, Utc(2026, 9, 30, 15));
        Add(db, 3, Utc(2026, 10, 2), "shipper-B", "driver-B");
        Add(db, 4, Utc(2026, 10, 3), "missing-shipper", "missing-driver");
        Add(db, 5, Utc(2026, 10, 4), mock: true);
        Add(db, 6, Utc(2026, 10, 4), business: 상태값.배차업무유형.음식배달);
        Add(db, 7, Utc(2026, 10, 4), state: "하차완료");
        Add(db, 8, null);
        Add(db, 9, Utc(2026, 10, 4));
        db.화주운송의뢰.Add(new() { 의뢰Id = "private-request-9", 화주Id = "shipper-A" });
        Add(db, 10, Utc(2026, 10, 4));
        db.화주운송의뢰.Add(new() { 의뢰Id = "private-request-10", 화주Id = "shipper-A", 모의처리처수행 = true });
        db.운송원장.Add(new() { Id = 11, 의뢰Id = "orphan", 상태 = "인수완료", 도착 = Utc(2026, 10, 4), 배차업무유형 = 상태값.배차업무유형.용달운송 });
        await db.SaveChangesAsync();
        fixture.Reads.Reset();

        var detail = await fixture.Details().상세조회Async(1, default);

        Assert.Equal(4, detail.Summary.CompletedFreightTransportCount);
        Assert.Equal(2, detail.Summary.FreightShipperAccountCount);
        Assert.Equal(2, detail.Summary.FreightDriverAccountCount);
        Assert.True(detail.Summary.HasIncompleteParticipantBindings);
        Assert.Equal(2, fixture.Reads.Commands.Count);
        Assert.All(fixture.Reads.InTransaction, value => Assert.True(value));
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal("arrival-month-kst", detail.MonthlyPeriodBasisCode);
        Assert.Collection(detail.MonthlyItems,
            october =>
            {
                Assert.Equal("2026-10", october.MonthCode);
                Assert.Equal(3, october.CompletedFreightTransportCount);
                Assert.Equal(2, october.FreightShipperAccountCount);
                Assert.Equal(2, october.FreightDriverAccountCount);
                Assert.True(october.HasIncompleteParticipantBindings);
            },
            september =>
            {
                Assert.Equal("2026-09", september.MonthCode);
                Assert.Equal(1, september.CompletedFreightTransportCount);
                Assert.Equal(1, september.FreightShipperAccountCount);
                Assert.Equal(1, september.FreightDriverAccountCount);
                Assert.False(september.HasIncompleteParticipantBindings);
            });
        Assert.True(detail.MonthlyItems.Sum(item => item.FreightShipperAccountCount) > detail.Summary.FreightShipperAccountCount);
        var summary = await fixture.Summary().조회Async(default);
        Assert.Equal(summary.CompletedFreightTransportCount, detail.Summary.CompletedFreightTransportCount);
        Assert.Equal(summary.FreightShipperAccountCount, detail.Summary.FreightShipperAccountCount);
        Assert.Equal(summary.FreightDriverAccountCount, detail.Summary.FreightDriverAccountCount);
    }

    [Fact]
    public async Task 월별참여계정도화주와의뢰일치및확정기사결속을검증하고대리접수계정으로대체하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        db.Users.AddRange(new() { Id = "shipper-A" }, new() { Id = "driver-A" }, new() { Id = "placing-admin" });
        Add(db, 1, Utc(2026, 10, 1), placedBy: "placing-admin");
        var mismatch = Add(db, 2, Utc(2026, 10, 2), placedBy: "placing-admin");
        mismatch.화주Id = "placing-admin";
        mismatch.기사_운송자 = "placing-admin";
        Add(db, 3, Utc(2026, 10, 3)).확정기사Id = null;
        await db.SaveChangesAsync();

        var detail = await fixture.Details().상세조회Async(1, default);

        var month = Assert.Single(detail.MonthlyItems);
        Assert.Equal(3, month.CompletedFreightTransportCount);
        Assert.Equal(1, month.FreightShipperAccountCount);
        Assert.Equal(1, month.FreightDriverAccountCount);
        Assert.True(month.HasIncompleteParticipantBindings);
    }

    [Fact]
    public async Task 기록있는월만고정12개씩최신순으로읽고다음페이지와전체누적을구분한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Users.AddRange(new() { Id = "shipper-A" }, new() { Id = "driver-A" });
        for (var index = 0; index < 14; index++)
            if (index != 7) Add(fixture.Db, index + 1, Utc(2024, 1, 15).AddMonths(index));
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.Details().상세조회Async(1, default);
        var second = await fixture.Details().상세조회Async(2, default);
        var beyond = await fixture.Details().상세조회Async(3, default);

        Assert.Equal(12, first.PageSize);
        Assert.Equal(12, first.MonthlyItems.Count);
        Assert.True(first.HasMore);
        Assert.Equal("2025-02", first.MonthlyItems[0].MonthCode);
        Assert.DoesNotContain(first.MonthlyItems, month => month.MonthCode == "2024-08");
        Assert.Equal("2024-01", Assert.Single(second.MonthlyItems).MonthCode);
        Assert.False(second.HasMore);
        Assert.Empty(beyond.MonthlyItems);
        Assert.False(beyond.HasMore);
        Assert.Equal(13, first.Summary.CompletedFreightTransportCount);
        Assert.Equal(first.Summary.CompletedFreightTransportCount, beyond.Summary.CompletedFreightTransportCount);
        Assert.Equal(1, first.Summary.FreightShipperAccountCount);
        Assert.Equal(1, first.Summary.FreightDriverAccountCount);
    }

    [Theory]
    [InlineData(SsalddelExecutionMode.Simulation)]
    [InlineData(SsalddelExecutionMode.Operational)]
    public async Task 성공한빈조회만0이고미연결재무값과미확인상태를만들지않는다(SsalddelExecutionMode mode)
    {
        await using var fixture = await Fixture.CreateAsync();
        var detail = await fixture.Details(mode).상세조회Async(1, default);
        Assert.Empty(detail.MonthlyItems);
        Assert.False(detail.HasMore);
        Assert.Equal("all-time", detail.Summary.PeriodCode);
        Assert.Equal(mode.ToString(), detail.Summary.ExecutionMode);
        Assert.Equal("recorded", detail.Summary.StatusCode);
        Assert.Equal(Fixture.Now, detail.Summary.AsOfUtc);
        Assert.Equal(0, detail.Summary.CompletedFreightTransportCount);
        Assert.Null(detail.Summary.ConfirmedPlatformProfitKrw);
        Assert.Null(detail.Summary.ConfirmedSharedPayoutKrw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    [InlineData(int.MaxValue)]
    public async Task 유효하지않은페이지는조회전에거절한다(int page)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Reads.Reset();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Details().상세조회Async(page, default));
        Assert.Empty(fixture.Reads.Commands);
        Assert.Null(fixture.Db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task 취소된조회는빈성공으로바꾸지않고소유한읽기트랜잭션을남기지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Details().상세조회Async(1, cancellation.Token));
        Assert.Null(fixture.Db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task 월집계조회오류는빈성공으로바꾸지않고읽기트랜잭션을정리한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Reads.Reset();
        fixture.Reads.FailOnRead = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Details().상세조회Async(1, default));
        Assert.Equal(2, fixture.Reads.Commands.Count);
        Assert.Null(fixture.Db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task 호출자의트랜잭션을재사용하되종료하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        await fixture.Details().상세조회Async(1, default);
        Assert.Same(transaction, fixture.Db.Database.CurrentTransaction);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task 공개상세는기간별숫자만직렬화하고개인식별값이나원문을반환하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Users.AddRange(new() { Id = "shipper-A" }, new() { Id = "driver-A" });
        Add(fixture.Db, 1, Utc(2026, 10, 1));
        await fixture.Db.SaveChangesAsync();
        var detail = await fixture.Details().상세조회Async(1, default);
        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("shipper-A", json);
        Assert.DoesNotContain("driver-A", json);
        Assert.DoesNotContain("private-request", json);
        Assert.DoesNotContain("private-address", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "hasMore", "monthlyItems", "monthlyPeriodBasisCode", "page", "pageSize", "summary" },
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "completedFreightTransportCount", "freightDriverAccountCount", "freightShipperAccountCount", "hasIncompleteParticipantBindings", "monthCode" },
            document.RootElement.GetProperty("monthlyItems")[0].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task 공개GET상세는기존요약생성자와권한을유지하고페이지취소토큰을전달한다()
    {
        var details = new DetailsStub();
        var controller = new 플랫폼참여현황Controller(new SummaryStub());
        using var cancellation = new CancellationTokenSource();
        var result = await controller.상세조회(details, 2, cancellation.Token);
        Assert.Same(details.Response, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, details.Page);
        Assert.Equal(cancellation.Token, details.Token);
        Assert.NotNull(typeof(플랫폼참여현황Controller).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.True(typeof(플랫폼참여현황Controller).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.Equal(SsalddelProductVersion.V0_0,
            typeof(플랫폼참여현황Controller).GetCustomAttribute<SsalddelApiVersionAttribute>()!.Version);
        Assert.Equal("~/api/v1/common/home/participation-details",
            typeof(플랫폼참여현황Controller).GetMethod(nameof(플랫폼참여현황Controller.상세조회))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        var invalid = await controller.상세조회(details, 0, default);
        Assert.IsType<BadRequestObjectResult>(invalid.Result);
        Assert.Equal(1, details.Calls);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static 운송원장 Add(SsalddelContext db, long id, DateTime? arrived, string shipper = "shipper-A", string driver = "driver-A",
        bool mock = false, int business = 상태값.배차업무유형.용달운송, string state = "인수완료", string? placedBy = null)
    {
        var requestId = "private-request-" + id;
        db.화주운송의뢰.Add(new() { 의뢰Id = requestId, 화주Id = shipper, 주문자UserId = placedBy ?? shipper,
            모의처리처수행 = mock, 픽업_도로명주소 = "private-address" });
        var transport = new 운송원장 { Id = id, 의뢰Id = requestId, 화주Id = shipper, 확정기사Id = driver,
            기사_운송자 = driver, 배차업무유형 = business, 상태 = state, 도착 = arrived };
        db.운송원장.Add(transport);
        return transport;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal static readonly DateTimeOffset Now = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero);
        private readonly SqliteConnection connection;
        internal SsalddelContext Db { get; }
        internal Reads Reads { get; } = new();
        private Fixture(SqliteConnection connection)
        {
            this.connection = connection;
            Db = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).AddInterceptors(Reads).Options, new Encryption());
        }
        internal static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var fixture = new Fixture(connection);
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }
        internal 플랫폼참여상세조회UseCase Details(SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation) => new(Db, new Execution(mode), new Clock());
        internal 플랫폼참여현황조회UseCase Summary() => new(Db, new Execution(SsalddelExecutionMode.Simulation), new Clock());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Reads : DbCommandInterceptor
    {
        internal List<string> Commands { get; } = [];
        internal List<bool> InTransaction { get; } = [];
        internal int? FailOnRead { get; set; }
        internal void Reset() { Commands.Clear(); InTransaction.Clear(); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText); InTransaction.Add(command.Transaction is not null);
            if (Commands.Count == FailOnRead) throw new InvalidOperationException("SyntheticReadFailure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
    private sealed record Execution(SsalddelExecutionMode Mode) : ISsalddelExecutionModePolicy
    { public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation; public bool IsOperational => !IsSimulation; }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Fixture.Now; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class SummaryStub : I플랫폼참여현황조회UseCase
    { public Task<HomeParticipationSummaryResponse> 조회Async(CancellationToken ct) => Task.FromResult(new HomeParticipationSummaryResponse()); }
    private sealed class DetailsStub : I플랫폼참여상세조회UseCase
    {
        internal HomeParticipationDetailsResponse Response { get; } = new();
        internal int Page { get; private set; }
        internal int Calls { get; private set; }
        internal CancellationToken Token { get; private set; }
        public Task<HomeParticipationDetailsResponse> 상세조회Async(int page, CancellationToken cancellationToken)
        { Calls++; Page = page; Token = cancellationToken; return Task.FromResult(Response); }
    }
}
