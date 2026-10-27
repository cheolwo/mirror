using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Operations;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Workflow;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Admin.Operations;

public sealed class CargoAcceptanceAdminRecoveryTests
{
    [Theory]
    [InlineData("Pending", "AutoRetryPending", false)]
    [InlineData("Retry", "AutoRetryPending", false)]
    [InlineData("Processing", "Processing", false)]
    [InlineData("Failed", "OperatorReviewRequired", true)]
    [InlineData("Blocked", "OperatorReviewRequired", false)]
    [InlineData("Done", "Completed", false)]
    public async Task 기존관리자복구목록은수락후속상태와판본만보여주고원문개인정보는노출하지않는다(string state, string mapped, bool retry)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, state);
        var result = await UseCase(db).목록조회Async(완료포함: true);
        var item = Assert.Single(result.항목);
        Assert.Equal(운영후속처리복구원천Codes.화물수락후속, item.원천Code);
        Assert.Equal(state, item.원천상태Code); Assert.Equal(mapped, item.상태Code); Assert.Equal(7, item.원천판본);
        Assert.Equal(retry, item.재시도예약가능); Assert.Equal(state is "Failed" or "Blocked", item.운영자확인필요);
        if (retry) Assert.Contains(업무복구행동Ids.운영자안전재시도예약, item.복구가능행동);
        if (state is "Pending" or "Retry" or "Processing") Assert.NotNull(item.다음처리예정시각Utc);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-driver", json); Assert.DoesNotContain("private-request", json);
        Assert.DoesNotContain("private-address", json); Assert.DoesNotContain("private-secret", json);
        if (state == "Done") Assert.Empty((await UseCase(db).목록조회Async()).항목);
    }

    [Fact]
    public async Task 관리자명시재시도는같은판본과현재수락만재예약하고발송이나원업무를변경하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, "Failed");
        var useCase = UseCase(db); var item = Assert.Single((await useCase.목록조회Async()).항목);
        var request = new 운영후속처리재시도요청Dto { 예상처리시도수 = item.처리시도수, 예상원천판본 = item.원천판본 };
        var result = await useCase.재시도예약Async(item.원천Code, item.원천항목Id, request);
        Assert.NotNull(result); Assert.Equal(0, result.처리시도수); Assert.True(result.원천판본 > item.원천판본);
        var marker = await db.운송이벤트.AsNoTracking().SingleAsync(); var job = Job(marker);
        Assert.Equal("Retry", job["state"]!.GetValue<string>());
        Assert.Equal(1, job["manualRetries"]!.GetValue<int>()); Assert.Equal(5, job["totalAttempts"]!.GetValue<int>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.재시도예약Async(item.원천Code, item.원천항목Id, request));
        Assert.Empty(db.Command알림Outbox); Assert.Empty(db.화물운송시간약속); Assert.Empty(db.결제);
        var transport = await db.운송원장.AsNoTracking().SingleAsync(); Assert.Equal("private-driver", transport.확정기사Id);
        Assert.Equal(상태값.배차대기상태.확정, transport.상태);
    }

    [Fact]
    public async Task 현재수락이달라져차단된의도는목록확인만가능하고재시도버튼이나명령을허용하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, "Blocked");
        var useCase = UseCase(db); var item = Assert.Single((await useCase.목록조회Async()).항목);
        Assert.False(item.재시도예약가능); Assert.Empty(item.복구가능행동);
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.재시도예약Async(item.원천Code, item.원천항목Id,
            new() { 예상처리시도수 = item.처리시도수, 예상원천판본 = item.원천판본 }));
        Assert.Equal("Blocked", Job(await db.운송이벤트.AsNoTracking().SingleAsync())["state"]!.GetValue<string>());
        Assert.Empty(db.Command알림Outbox);
    }

    [Fact]
    public async Task 최신대기목록이많아도오래된실패를조회상한앞에노출한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, "Failed");
        var original = await db.운송이벤트.SingleAsync();
        var root = JsonNode.Parse(original.메타데이터)!; root["cargoAcceptanceFollowups"]!["state"] = "Pending";
        for (var i = 0; i < 60; i++) db.운송이벤트.Add(new()
        { 의뢰Id = "pending-" + i, 이벤트타입 = CargoAcceptanceRecoveryMarker.EventType, 메타데이터 = root.ToJsonString() });
        await db.SaveChangesAsync();
        var item = Assert.Single((await UseCase(db).목록조회Async(take: 1)).항목);
        Assert.Equal(original.Id, item.원천항목Id); Assert.Equal("Failed", item.원천상태Code);
    }

    [Theory]
    [InlineData("missing-revision")]
    [InlineData("stale-revision")]
    [InlineData("stale-attempts")]
    [InlineData("cancelled")]
    [InlineData("other-driver")]
    [InlineData("new-round")]
    [InlineData("same-driver-new-generation")]
    public async Task 구판본이나달라진현재수락은관리자재시도로승격할수없다(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, "Failed");
        var useCase = UseCase(db); var item = Assert.Single((await useCase.목록조회Async()).항목);
        var transport = await db.운송원장.SingleAsync();
        if (scenario == "cancelled") (await db.화주운송의뢰.SingleAsync()).배차상태 = 상태값.배차상태.취소;
        if (scenario == "other-driver") { transport.기사_운송자 = "another"; transport.확정기사Id = "another"; }
        if (scenario == "new-round") transport.추천라운드++;
        if (scenario == "same-driver-new-generation") db.운송이벤트.Add(CargoAcceptanceRecoveryMarker.Create(transport, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var before = (await db.운송이벤트.AsNoTracking().SingleAsync(x => x.Id == item.원천항목Id)).메타데이터;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.재시도예약Async(item.원천Code, item.원천항목Id, new()
        {
            예상처리시도수 = scenario == "stale-attempts" ? 4 : 5,
            예상원천판본 = scenario == "missing-revision" ? null : scenario == "stale-revision" ? 6 : 7
        }));
        Assert.Equal(before, (await db.운송이벤트.AsNoTracking().SingleAsync(x => x.Id == item.원천항목Id)).메타데이터);
        Assert.Empty(db.Command알림Outbox); Assert.Empty(db.화물운송시간약속); Assert.Empty(db.결제);
    }

    private static 운영후속처리복구UseCase UseCase(SsalddelContext db)
        => new(db, null!, null!, new CargoAcceptanceRecoveryService(db, null!, Options.Create(new CargoAcceptanceRecoveryOptions()),
            TimeProvider.System, NullLogger<CargoAcceptanceRecoveryService>.Instance));
    private static async Task Seed(SsalddelContext db, string state)
    {
        var transport = new 운송원장 { 의뢰Id = "private-request", 상태 = 상태값.배차대기상태.확정,
            배차큐단계 = 상태값.배차큐단계.확정, 확정기사Id = "private-driver", 기사_운송자 = "private-driver", 추천라운드 = 2 };
        db.Add(transport); db.Add(new 화주운송의뢰 { 의뢰Id = "private-request", 화주Id = "private-shipper",
            배차상태 = 상태값.배차상태.배차확정, 픽업_도로명주소 = "private-address" });
        await db.SaveChangesAsync();
        var row = CargoAcceptanceRecoveryMarker.Create(transport, DateTime.UtcNow);
        var root = JsonNode.Parse(row.메타데이터)!; var job = root["cargoAcceptanceFollowups"]!;
        job["state"] = state; job["attempts"] = 5; job["totalAttempts"] = 5; job["revision"] = 7;
        job["reasonCode"] = "private-secret error text";
        if (state == "Processing") job["leaseUntilUtc"] = DateTime.UtcNow.AddMinutes(2);
        row.메타데이터 = root.ToJsonString(); db.운송이벤트.Add(row); await db.SaveChangesAsync();
    }
    private static JsonNode Job(운송이벤트 row) => JsonNode.Parse(row.메타데이터)!["cargoAcceptanceFollowups"]!;
    private static SsalddelContext Db(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
            .UseInternalServiceProvider(Ssalddel.Tests.Application.Driver.DispatchAction.CargoAcceptanceRecoveryTests.OrdinaryProvider)
            .Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
