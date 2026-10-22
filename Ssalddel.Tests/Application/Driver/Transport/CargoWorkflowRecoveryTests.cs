using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoWorkflowRecoveryTests
{
    [Theory]
    [InlineData(true, "CargoPickupPhotoRequired", "cargo.pickup")]
    [InlineData(false, "CargoDropoffPhotoRequired", "cargo.dropoff")]
    public async Task 사진누락은해당단계의미적용으로확정하고완료원장을건드리지않는다(bool pickup, string code, string stage)
    {
        await using var db = Context(); var row = await Seed(db, pickup ? "상차지도착" : "하차지도착");
        var executor = new RejectExecutor();
        var result = pickup
            ? await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter())
                .Handle(new("driver-1", row.Id), default)
            : await new 운송인수완료CommandHandler(executor, new 운송증빙첨부JsonWriter(), db)
                .Handle(new("driver-1", row.Id), default);
        Verify(result.Errors.Single(), code, stage);
        Assert.Equal(0, executor.Calls);
        Assert.Equal(pickup ? "상차지도착" : "하차지도착", row.상태);
        Assert.Equal("[]", row.첨부_json); Assert.Empty(db.운송이벤트);
    }

    [Theory]
    [InlineData("pickup-complete-photo", "cargo.pickup")]
    [InlineData("dropoff-complete-photo", "cargo.dropoff")]
    public async Task 사진저장조회장애는완료미적용이며사진업로드실패로단정하지않는다(string kind, string stage)
    {
        await using var db = Context(); var row = await Seed(db, "상차지도착");
        var result = await 화물운송사진증빙Policy.VerifyAsync(db,
            new CargoPhotoTestStorage { Configured = false }, row.Id, "driver-1", kind, "proof/photo.jpg", default);
        Verify(result.Errors.Single(), "CargoPhotoStorageUnavailable", stage);
        Assert.Equal(503, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal("상차지도착", row.상태); Assert.Empty(db.운송이벤트);
    }

    [Fact]
    public async Task 창고인계대기는창고담당과상차단계를연결하고재고나운송을완료하지않는다()
    {
        await using var db = Context(); var row = await Seed(db, "상차지도착");
        db.출고예정.Add(new 출고예정 { 운송의뢰Id = row.의뢰Id, 출고창고Id = 1, 상태 = 출고상태.예정 });
        await db.SaveChangesAsync(); var executor = new RejectExecutor();
        var result = await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter())
            .Handle(new("driver-1", row.Id) { 상차사진ObjectName = "proof/photo.jpg" }, default);
        Verify(result.Errors.Single(), "CargoWarehouseHandoffPending", "cargo.pickup");
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, result.Errors.Single().Metadata["ResponsibilityRoleCode"]);
        Assert.Equal(0, executor.Calls); Assert.Equal(출고상태.예정, (await db.출고예정.SingleAsync()).상태);
    }

    [Fact]
    public async Task 서명조건미충족은인수증사유로남기고사진검증이나완료명령을실행하지않는다()
    {
        await using var db = Context(); var row = await Seed(db, "상차지도착");
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = row.의뢰Id, 증빙방식 = "인수증", 요청사항 = "서명필수" });
        await db.SaveChangesAsync(); var executor = new RejectExecutor();
        var result = await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter())
            .Handle(new("driver-1", row.Id) { 상차사진ObjectName = "proof/photo.jpg" }, default);
        Verify(result.Errors.Single(), "CargoReceiptEvidenceRequired", "cargo.pickup");
        Assert.Equal(0, executor.Calls); Assert.Equal("상차지도착", row.상태);
    }

    [Theory]
    [InlineData("pickup-complete-photo", "cargo.pickup")]
    [InlineData("dropoff-complete-photo", "cargo.dropoff")]
    public async Task 분산사진차단은공통실패의기존코드를중복추가하지않고구형별칭과맥락을함께보존한다(string kind, string stage)
    {
        await using var db = Context(); var row = await Seed(db, "상차지도착");
        var item = new 입고상품 { 보관위치 = "DLOC-fixture", 입고수량 = 2, 가용수량 = 2 };
        db.입고상품.Add(item); await db.SaveChangesAsync();
        db.출고예정.Add(new 출고예정
        {
            운송의뢰Id = row.의뢰Id, 입고상품Id = item.Id, 상태 = 출고상태.출고완료
        });
        await db.SaveChangesAsync();

        var result = await 화물운송사진증빙Policy.VerifyAsync(db, new CargoPhotoTestStorage(), row.Id,
            "driver-1", kind, "proof/photo.jpg", default);

        var error = Assert.Single(result.Errors);
        Verify(error, "DistributedCargoCustodyRequired", stage);
        Assert.Equal("DistributedCargoCustodyRequired", error.Metadata["Code"]);
        Assert.Equal(409, error.Metadata["StatusCode"]);
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, error.Metadata["ResponsibilityRoleCode"]);
        Assert.Equal(2, item.가용수량); Assert.Equal("상차지도착", row.상태);
        Assert.Equal("[]", row.첨부_json); Assert.Empty(db.운송이벤트);
    }

    [Theory]
    [InlineData(true, "CargoTransportHeld", "cargo.interruption-recovery")]
    [InlineData(false, "CargoTransportStateChanged", "cargo.dropoff")]
    public async Task 현재원장의보류와단계불일치를별도사유로반환하고상태를보존한다(bool held, string code, string stage)
    {
        await using var db = Context(); var row = await Seed(db, "상차지도착");
        if (held)
            db.비정상운송사건.Add(new 비정상운송사건
            {
                사건StableId = "case-1", 운송Id = row.Id, 운송의뢰Id = row.의뢰Id!,
                업무통제상태Code = 비정상운송업무통제상태Codes.전체보류
            });
        await db.SaveChangesAsync();
        var result = await Executor(db).실행Async(new("driver-1", row.Id, "driver-1", 살뜰역할유형.기사,
            "인수완료", "test", context => new TestEvent(context.운송.Id)), default);
        Verify(result.Errors.Single(), code, stage);
        Assert.Equal("상차지도착", row.상태); Assert.Empty(db.운송이벤트);
    }

    private static void Verify(IError error, string code, string stage)
    {
        Assert.Equal(code, error.Metadata["ErrorCode"]);
        var context = Assert.IsType<업무실패맥락Dto>(error.Metadata["WorkflowContext"]);
        Assert.True(context.유효한가()); Assert.Equal(stage, context.StageId);
        Assert.Equal(업무처리결과Codes.미적용, context.OutcomeCode);
        Assert.Equal(new[] { 업무복구행동Ids.상태전체재조회 }, error.Metadata["AvailableRecoveryActions"]);
        Assert.Equal(업무재시도정책Codes.상태재조회후사용자재시도, error.Metadata["RetryPolicyCode"]);
    }

    private static async Task<운송원장> Seed(SsalddelContext db, string state)
    {
        var row = new 운송원장 { 의뢰Id = "request-1", 운송번호 = "request-1", 기사_운송자 = "driver-1",
            확정기사Id = "driver-1", 상태 = state, 첨부_json = "[]" };
        db.Add(row); await db.SaveChangesAsync(); return row;
    }
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-workflow-recovery-" + Guid.NewGuid()).Options, new Encryption());
    private static 기사운송상태변경CommandExecutor Executor(SsalddelContext db) => new(db,
        new 기사운송상태전이Service(), new Publisher(), new User(), new 참여자실행권한검사(),
        NullLogger<기사운송상태변경CommandExecutor>.Instance);
    private sealed class RejectExecutor : I기사운송상태변경CommandExecutor
    {
        public int Calls { get; private set; }
        public Task<Result<기사운송상태변경응답>> 실행Async(기사운송상태변경요청 request, CancellationToken token)
        { Calls++; throw new InvalidOperationException("완료 명령을 실행하면 안 됩니다."); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class User : ICurrentUserAccessor
    { public string? UserId => "driver-1"; public string? Role => "기사"; }
    private sealed record TestEvent(long Id) : INotification;
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object value, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => Task.CompletedTask;
    }
}
