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
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Home;

public sealed class HomeParticipationAggregateR10Tests
{
    [Fact]
    public async Task 연결된비모의화물완료만한SQL로집계하고중복참여와관리자대리접수를구별한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        db.Users.AddRange(new() { Id = "shipper-1" }, new() { Id = "shipper-2" },
            new() { Id = "driver-1" }, new() { Id = "driver-2" }, new() { Id = "placing-admin" });
        Add(db, 1, "shipper-1", "driver-1", placedBy: "placing-admin");
        Add(db, 2, "shipper-1", "driver-1");
        Add(db, 3, "shipper-2", "driver-2");
        Add(db, 4, "shipper-1", "driver-1", mock: true);
        Add(db, 5, "shipper-1", "driver-1", business: 상태값.배차업무유형.음식배달);
        Add(db, 6, "shipper-1", "driver-1", state: "하차지도착");
        Add(db, 7, "shipper-1", "driver-1", state: "하차완료");
        db.운송원장.Add(new() { Id = 8, 의뢰Id = "orphan", 상태 = "인수완료", 배차업무유형 = 상태값.배차업무유형.용달운송 });
        await db.SaveChangesAsync();
        fixture.Reads.Reset();

        var summary = await fixture.UseCase(SsalddelExecutionMode.Simulation).조회Async(default);

        Assert.Equal(3, summary.CompletedFreightTransportCount);
        Assert.Equal(2, summary.FreightShipperAccountCount);
        Assert.Equal(2, summary.FreightDriverAccountCount);
        Assert.False(summary.HasIncompleteParticipantBindings);
        Assert.Equal("Simulation", summary.ExecutionMode);
        Assert.Equal("recorded", summary.StatusCode);
        Assert.Null(summary.ConfirmedPlatformProfitKrw);
        Assert.Null(summary.ConfirmedSharedPayoutKrw);
        Assert.Single(fixture.Reads.Commands);
        Assert.Contains("COUNT", fixture.Reads.Commands[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 도착시각없는완료와중복의뢰연결은완료건수를늘리지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        db.Users.AddRange(new() { Id = "shipper" }, new() { Id = "driver" });
        Add(db, 1, "shipper", "driver");
        Add(db, 2, "shipper", "driver").도착 = null;
        Add(db, 3, "shipper", "driver");
        db.화주운송의뢰.Add(new() { 의뢰Id = "request-3", 화주Id = "shipper" });
        Add(db, 4, "shipper", "driver");
        db.화주운송의뢰.Add(new() { 의뢰Id = "request-4", 화주Id = "shipper", 모의처리처수행 = true });
        await db.SaveChangesAsync();
        fixture.Reads.Reset();

        var summary = await fixture.UseCase(SsalddelExecutionMode.Simulation).조회Async(default);

        Assert.Equal(1, summary.CompletedFreightTransportCount);
        Assert.Equal(1, summary.FreightShipperAccountCount);
        Assert.Equal(1, summary.FreightDriverAccountCount);
        Assert.False(summary.HasIncompleteParticipantBindings);
        Assert.Single(fixture.Reads.Commands);
    }

    [Fact]
    public async Task 실재하지않거나결속이다른계정은대체추정하지않고완료기록과누락여부를별도집계한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        db.Users.AddRange(new() { Id = "both-roles" }, new() { Id = "placing-admin" }, new() { Id = "other-driver" });
        Add(db, 1, "both-roles", "both-roles");
        Add(db, 2, "missing-shipper", "missing-driver");
        Add(db, 3, "business-id-not-account", "both-roles", placedBy: "placing-admin");
        var mismatched = Add(db, 4, "both-roles", "both-roles");
        mismatched.화주Id = "placing-admin";
        mismatched.기사_운송자 = "other-driver";
        var missingConfirmation = Add(db, 5, "both-roles", "both-roles");
        missingConfirmation.확정기사Id = null;
        await db.SaveChangesAsync();
        fixture.Reads.Reset();

        var summary = await fixture.UseCase(SsalddelExecutionMode.Operational).조회Async(default);

        Assert.Equal(5, summary.CompletedFreightTransportCount);
        Assert.Equal(1, summary.FreightShipperAccountCount);
        Assert.Equal(1, summary.FreightDriverAccountCount);
        Assert.True(summary.HasIncompleteParticipantBindings);
        Assert.Equal("Operational", summary.ExecutionMode);
        Assert.Equal("recorded", summary.StatusCode);
        Assert.Null(summary.ConfirmedPlatformProfitKrw);
        Assert.Null(summary.ConfirmedSharedPayoutKrw);
        Assert.Single(fixture.Reads.Commands);
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("both-roles", json);
        Assert.DoesNotContain("placing-admin", json);
        Assert.DoesNotContain("private-address", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "asOfUtc", "completedFreightTransportCount", "confirmedPlatformProfitKrw", "confirmedSharedPayoutKrw",
            "executionMode", "freightDriverAccountCount", "freightShipperAccountCount", "hasIncompleteParticipantBindings", "periodCode", "statusCode" },
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(SsalddelExecutionMode.Simulation)]
    [InlineData(SsalddelExecutionMode.Operational)]
    public async Task 성공한빈집계의0과미연결금액의null을구별한다(SsalddelExecutionMode mode)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Reads.Reset();
        var summary = await fixture.UseCase(mode).조회Async(default);
        Assert.Equal(0, summary.CompletedFreightTransportCount);
        Assert.Equal(0, summary.FreightShipperAccountCount);
        Assert.Equal(0, summary.FreightDriverAccountCount);
        Assert.False(summary.HasIncompleteParticipantBindings);
        Assert.Null(summary.ConfirmedPlatformProfitKrw);
        Assert.Null(summary.ConfirmedSharedPayoutKrw);
        Assert.Equal("all-time", summary.PeriodCode);
        Assert.Equal(Fixture.Now, summary.AsOfUtc);
        Assert.Single(fixture.Reads.Commands);
    }

    [Fact]
    public async Task 취소된조회는가짜0응답으로바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.UseCase(SsalddelExecutionMode.Simulation).조회Async(cancel.Token));
    }

    [Fact]
    public async Task 공개GET는최소DTO와취소토큰을전달하며기능분류기본경계를명시한다()
    {
        var useCase = new Stub();
        using var cancellation = new CancellationTokenSource();
        var controller = new 플랫폼참여현황Controller(useCase);
        var result = await controller.조회(cancellation.Token);
        Assert.Same(useCase.Response, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(cancellation.Token, useCase.Token);
        Assert.NotNull(typeof(플랫폼참여현황Controller).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(SsalddelProductVersion.V0_0,
            typeof(플랫폼참여현황Controller).GetCustomAttribute<SsalddelApiVersionAttribute>()!.Version);
        Assert.Equal("api/v1/common/home/participation-summary",
            typeof(플랫폼참여현황Controller).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    private static 운송원장 Add(SsalddelContext db, long id, string shipper, string driver,
        bool mock = false, int business = 상태값.배차업무유형.용달운송, string state = "인수완료", string? placedBy = null)
    {
        var requestId = "request-" + id;
        db.화주운송의뢰.Add(new() { 의뢰Id = requestId, 화주Id = shipper, 주문자UserId = placedBy ?? shipper, 모의처리처수행 = mock,
            픽업_도로명주소 = "private-address" });
        var transport = new 운송원장 { Id = id, 의뢰Id = requestId, 화주Id = shipper, 확정기사Id = driver,
            기사_운송자 = driver, 배차업무유형 = business, 상태 = state, 도착 = Fixture.Now.UtcDateTime.AddDays(-1) };
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
        internal 플랫폼참여현황조회UseCase UseCase(SsalddelExecutionMode mode) => new(Db, new Execution(mode), new Clock());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Reads : DbCommandInterceptor
    {
        internal List<string> Commands { get; } = [];
        internal void Reset() => Commands.Clear();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Add(command.CommandText); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
    private sealed record Execution(SsalddelExecutionMode Mode) : ISsalddelExecutionModePolicy
    { public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation; public bool IsOperational => !IsSimulation; }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Fixture.Now; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Stub : I플랫폼참여현황조회UseCase
    {
        internal HomeParticipationSummaryResponse Response { get; } = new();
        internal CancellationToken Token { get; private set; }
        public Task<HomeParticipationSummaryResponse> 조회Async(CancellationToken cancellationToken)
        { Token = cancellationToken; return Task.FromResult(Response); }
    }
}
