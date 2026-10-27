using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Management;
using Ssalddel.Contracts.Admin.Management;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;

namespace Ssalddel.Tests.Application.Admin.Management;

public sealed class 기사실차적재제원Tests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 구형차종자료를_실차확인으로승격하지않는다()
    {
        var result = 기사실차적재근거조회Service.해석(new 용달기사 { 차량 = "fixture" }, ClassSpec(), Now);
        Assert.False(result.실차확인됨); Assert.NotNull(result.유효제원); Assert.Equal("ActualVehicle.ClassOnly", result.사유Code);
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("expired")]
    [InlineData("withdrawn")]
    [InlineData("missing")]
    public void 선택실차의확인이무효면_공통제원으로우회하지않는다(string reason)
    {
        var actual = Actual();
        actual = reason switch { "changed" => actual with { ActualVehicleReference = "vehicle:other" },
            "expired" => actual with { ValidUntilUtc = Now.AddSeconds(-1) }, "withdrawn" => actual with { Withdrawn = true }, _ => actual };
        var driver = new 용달기사 { 차량 = "fixture", 현재실차참조 = "vehicle:test",
            실차적재제원Json = reason == "missing" ? null : JsonSerializer.Serialize(actual), 실차적재판본 = 2 };
        var result = 기사실차적재근거조회Service.해석(driver, ClassSpec(), Now);
        Assert.Null(result.유효제원); Assert.False(result.실차확인됨);
    }

    [Fact]
    public void 실측과차종상한중작은값을쓰고_미확인장비는만들지않는다()
    {
        var driver = new 용달기사 { 차량 = "fixture", 현재실차참조 = "vehicle:test", 실차적재판본 = 1,
            실차적재제원Json = JsonSerializer.Serialize(Actual() with { 적재함높이Mm = null, 리프트가능 = null }) };
        var result = 기사실차적재근거조회Service.해석(driver, ClassSpec(), Now);
        Assert.True(result.실차확인됨);
        Assert.Equal(50, result.유효제원!.최대적재중량Kg); Assert.Equal(500, result.유효제원.적재함길이Mm);
        Assert.Null(result.유효제원.적재함높이Mm); Assert.False(result.유효제원.리프트가능);
    }

    [Fact]
    public void 실차운영부피를생략해도_기존차종운영상한이사라지지않는다()
    {
        var spec = ClassSpec(); spec.권장최대CBM = .02m;
        var driver = new 용달기사 { 차량 = "fixture", 현재실차참조 = "vehicle:test", 실차적재판본 = 1,
            실차적재제원Json = JsonSerializer.Serialize(Actual()) };
        var result = 기사실차적재근거조회Service.해석(driver, spec, Now);
        Assert.Equal(.02m, result.유효제원!.권장최대CBM);
    }

    [Fact]
    public async Task 관리자기록은_본인선택실차와판본을확인하며_자격원장을쓰지않는다()
    {
        await using var db = Context();
        db.용달기사.Add(new() { 기사Id = "driver", 차량 = "fixture", 현재실차참조 = "vehicle:test" }); await db.SaveChangesAsync();
        var service = new 기사실차적재제원UseCase(db, new Clock());
        var saved = await service.Handle(new 기사실차적재제원수정Command("driver", "admin", new(null, Actual())), default);
        Assert.Equal(1, saved!.판본); Assert.Equal("admin", saved.제원!.ReviewerId);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.Handle(new 기사실차적재제원수정Command("driver", "admin", new(null, Actual())), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Handle(new 기사실차적재제원수정Command("driver", "admin", new(1, Actual() with { ActualVehicleReference = "vehicle:other" })), default));
        Assert.Empty(await db.운송자격.ToListAsync());
        var type = db.Model.FindEntityType(typeof(용달기사))!;
        Assert.True(type.FindProperty(nameof(용달기사.실차적재판본))!.IsConcurrencyToken);
        Assert.NotNull(type.FindProperty(nameof(용달기사.실차적재제원Json))!.GetValueConverter());
    }

    [Fact]
    public async Task 장비가능확인은_실제장비근거참조가필요하다()
    {
        await using var db = Context();
        db.용달기사.Add(new() { 기사Id = "driver", 차량 = "fixture", 현재실차참조 = "vehicle:test" }); await db.SaveChangesAsync();
        var service = new 기사실차적재제원UseCase(db, new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.Handle(new 기사실차적재제원수정Command("driver", "admin", new(null, Actual() with { 리프트가능 = true })), default));
        Assert.Null((await db.용달기사.SingleAsync()).실차적재판본);
    }

    private static 기사실차적재제원 Actual() => new() { ActualVehicleReference = "vehicle:test", VehicleProfileValue = "fixture",
        PrivateEvidenceReference = "loading-evidence:test", ReviewerId = "admin", ConfirmedAtUtc = Now.AddDays(-1), ValidUntilUtc = Now.AddDays(7),
        적재함길이Mm = 500, 적재함폭Mm = 500, 적재함높이Mm = 500, 최대적재중량Kg = 50, EntranceKind = "Enclosed" };
    private static 차량제원 ClassSpec() => new() { 차량코드 = "fixture", 차량명 = "fixture", 최대적재중량Kg = 100,
        적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 리프트가능 = true };
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase($"actual-loading-{Guid.NewGuid():N}").Options, new Encryption());
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
}
