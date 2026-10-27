using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Management;
using Ssalddel.Contracts.Admin.Management;
using Ssalddel.Controllers.Admin.Master06;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.차량;

namespace Ssalddel.Tests.Application.Admin.Management;

public sealed class 차량추천기준수정Tests
{
    [Theory]
    [InlineData("오토바이")]
    [InlineData("다마스")]
    public async Task 미확인제원을_켜려하면_저장과추적객체를변경하지않는다(string code)
    {
        await using var db = Context();
        var vehicle = new 차량제원 { 차량코드 = code, 차량명 = code, 추천사용여부 = false };
        db.차량제원.Add(vehicle);
        await db.SaveChangesAsync();
        var original = vehicle.UpdatedAt;
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new(code, null, 50, true, null, null), default));
        Assert.False(vehicle.추천사용여부);
        Assert.Equal(original, vehicle.UpdatedAt);
        Assert.Equal(EntityState.Unchanged, db.Entry(vehicle).State);
        db.ChangeTracker.Clear();
        Assert.False((await db.차량제원.SingleAsync()).추천사용여부);
    }

    [Fact]
    public async Task 합성실측값등록후_같은DB의추천에만반영된다()
    {
        await using var db = Context();
        db.차량제원.Add(new 차량제원 { 차량코드 = "오토바이", 차량명 = "시험 장비", 추천사용여부 = false });
        await db.SaveChangesAsync();
        var engine = new 차량적재추천Engine();
        var service = new 차량적재추천Service(db, engine);
        var cargo = new 차량적재추천요구사항
        {
            총중량Kg = 2, 총부피Cbm = 0.008m,
            포장목록 = [new() { 포장길이Mm = 200, 포장폭Mm = 200, 포장높이Mm = 200 }]
        };
        Assert.Empty((await service.추천Async(cargo)).추천후보);
        var result = await new 차량추천기준수정CommandHandler(db).Handle(
            new("오토바이", 0.020m, 20, true, 3, null, 400, 300, 250, 4), default);
        Assert.NotNull(result);
        Assert.Equal(0.030m, result.계산CBM);
        db.ChangeTracker.Clear();
        var stored = await db.차량제원.SingleAsync();
        Assert.Equal(400, stored.적재함길이Mm);
        Assert.Equal(4, stored.최대적재중량Kg);
        Assert.Equal("오토바이", Assert.Single((await service.추천Async(cargo)).추천후보).차량.차량코드);
    }

    [Fact]
    public async Task 기존호출은_생략한물리제원을보존한다()
    {
        await using var db = Context();
        db.차량제원.Add(Known());
        await db.SaveChangesAsync();
        var result = await new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", 0.5m, 25, true, 90, 1), default);
        Assert.NotNull(result);
        Assert.Equal(1000, result.적재함길이Mm);
        Assert.Equal(1000, result.적재함폭Mm);
        Assert.Equal(1000, result.적재함높이Mm);
        Assert.Equal(100, result.최대적재중량Kg);
    }

    [Theory]
    [InlineData(101, 1.0)]
    [InlineData(100, 1.001)]
    public async Task 운영권장값이물리상한을넘으면_기존제원을보존한다(int weight, double cbm)
    {
        await using var db = Context();
        var vehicle = Known();
        db.차량제원.Add(vehicle);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", (decimal)cbm, 30, true, weight, 1), default));
        Assert.Equal(80, vehicle.운영권장중량Kg);
        Assert.Equal(0.8m, vehicle.권장최대CBM);
        Assert.Equal(EntityState.Unchanged, db.Entry(vehicle).State);
    }

    [Fact]
    public async Task 양수권장부피가_반올림후0이되면서_상한이사라지지않는다()
    {
        await using var db = Context();
        var vehicle = Known();
        db.차량제원.Add(vehicle);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", 0.0004m, 30, true, 80, null), default));
        Assert.Equal(0.8m, vehicle.권장최대CBM);
        Assert.Equal(EntityState.Unchanged, db.Entry(vehicle).State);
    }

    [Fact]
    public async Task 물리상한을줄일때에도_병합한최종권장값을검사한다()
    {
        await using var db = Context();
        var vehicle = Known();
        db.차량제원.Add(vehicle);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", 0.8m, 30, true, 80, 1, 최대적재중량Kg: 50), default));
        Assert.Equal(100, vehicle.최대적재중량Kg);
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", 0.8m, 30, true, 80, 1, 적재함길이Mm: 500), default));
        Assert.Equal(1000, vehicle.적재함길이Mm);
    }

    [Theory]
    [InlineData(-1, 1000, 1000, 100)]
    [InlineData(1000, -1, 1000, 100)]
    [InlineData(1000, 1000, -1, 100)]
    [InlineData(1000, 1000, 1000, -1)]
    [InlineData(1000, 1000, 0, 100)]
    public async Task 음수나소형차높이미확인은_활성화를거절한다(int length, int width, int height, int weight)
    {
        await using var db = Context();
        db.차량제원.Add(Known());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", null, 30, true, null, null, length, width, height, weight), default));
        Assert.Equal(EntityState.Unchanged, db.Entry(await db.차량제원.SingleAsync()).State);
    }

    [Fact]
    public async Task 비활성자료는_미확인값을보관할수있다()
    {
        await using var db = Context();
        db.차량제원.Add(Known());
        await db.SaveChangesAsync();
        var result = await new 차량추천기준수정CommandHandler(db)
            .Handle(new("다마스", null, 30, false, null, null, 0, 0, 0, 0), default);
        Assert.NotNull(result);
        Assert.False(result.추천사용여부);
        Assert.Null(result.적재함높이Mm);
        Assert.Equal(0, result.최대적재중량Kg);
    }

    [Fact]
    public async Task 기존개방형차량의_높이생략은_중량설정에서유지한다()
    {
        await using var db = Context();
        var vehicle = Known();
        vehicle.차량코드 = "시험카고";
        vehicle.적재함높이Mm = null;
        db.차량제원.Add(vehicle);
        await db.SaveChangesAsync();
        var result = await new 차량추천기준수정CommandHandler(db)
            .Handle(new("시험카고", null, 30, true, 80, null), default);
        Assert.NotNull(result);
        Assert.Null(result.적재함높이Mm);
    }

    [Fact]
    public async Task 기존관리자API가_실측필드를전달하고_검증거절을400으로반환한다()
    {
        var sender = DispatchProxy.Create<ISender, Sender>();
        var stub = (Sender)(object)sender;
        var controller = new 차량추천관리Controller(sender)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var response = await controller.추천기준수정("오토바이", new()
        { 적재함길이Mm = 400, 적재함폭Mm = 300, 적재함높이Mm = 250, 최대적재중량Kg = 4 });
        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(400, stub.Command!.적재함길이Mm);
        Assert.Equal(300, stub.Command.적재함폭Mm);
        Assert.Equal(250, stub.Command.적재함높이Mm);
        Assert.Equal(4, stub.Command.최대적재중량Kg);
        stub.Reject = true;
        var failure = Assert.IsType<ObjectResult>(await controller.추천기준수정("오토바이", new()));
        Assert.Equal(400, failure.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(failure.Value);
        Assert.Equal("시험 검증 거절", problem.Title);
        Assert.Equal("BadRequest", problem.Extensions["errorCode"]);
        Assert.Equal("서버관리자전용", Assert.Single(typeof(차량추천관리Controller)
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
    }

    // 이 값은 흐름 검증용 합성 수치이며 다마스 제조사 제원이 아니다.
    private static 차량제원 Known() => new()
    {
        차량코드 = "다마스", 차량명 = "시험 차량", 적재함길이Mm = 1000, 적재함폭Mm = 1000,
        적재함높이Mm = 1000, 최대적재중량Kg = 100, 운영권장중량Kg = 80,
        권장최대CBM = 0.8m, 추천사용여부 = true
    };

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("vehicle-spec-" + Guid.NewGuid()).Options, new Encryption());

    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }

    public class Sender : DispatchProxy
    {
        public 차량추천기준수정Command? Command;
        public bool Reject;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Command = (차량추천기준수정Command)args![0]!;
            return Reject ? Task.FromException<차량추천기준응답?>(new ArgumentException("시험 검증 거절"))
                : Task.FromResult<차량추천기준응답?>(new() { 차량코드 = Command.차량코드 });
        }
    }
}
