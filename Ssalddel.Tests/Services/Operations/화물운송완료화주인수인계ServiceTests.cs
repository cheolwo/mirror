using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Operations;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using ShipRequest = Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 화물운송완료화주인수인계ServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 인수완료된화물운송은_증빙경로와개인정보없이_화주인수단계로멱등반환된다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        db.운송원장.Add(CreateCompletedTransport());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.완료결과요청Async(501, true);
        var replay = await service.완료결과요청Async(501, true);

        Assert.Equal(운영체제업무인계상태Codes.요청됨, result.상태Code);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.출발운영체제Id);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, result.도착운영체제Id);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.현재책임운영체제Id);
        Assert.Equal("cargo-transport-completion:501", result.출발업무StableId);
        Assert.Equal(result.인계StableId, replay.인계StableId);
        Assert.Single(await db.운영체제업무인계.ToListAsync());
        Assert.Single(await db.운영체제업무인계Outbox.ToListAsync());

        var stored = await db.운영체제업무인계.AsNoTracking().SingleAsync();
        using var snapshot = JsonDocument.Parse(stored.최소상태사본Json);
        Assert.Equal("인수완료", snapshot.RootElement.GetProperty("completionStatus").GetString());
        Assert.True(snapshot.RootElement.GetProperty("evidenceRegistered").GetBoolean());
        Assert.Equal(
            OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance,
            snapshot.RootElement.GetProperty("shipperStageId").GetString());
        Assert.DoesNotContain("secret-dropoff-photo", stored.최소상태사본Json, StringComparison.Ordinal);
        Assert.DoesNotContain("서울 중랑구", stored.최소상태사본Json, StringComparison.Ordinal);
        Assert.DoesNotContain("driver-501", stored.최소상태사본Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 화주인수증등록은_완료결과인계를수락하지만_정산완료를자동확정하지않는다()
    {
        await using var db = CreateContext();
        var request = CreateRequest();
        db.화주운송의뢰.Add(request);
        db.운송원장.Add(CreateCompletedTransport());
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.완료결과요청Async(501, true);
        var handler = new 화주운송의뢰인수증등록CommandHandler(
            db,
            service,
            new 화주운송업무담당자UseCase(
                db,
                new TestCurrentUserAccessor("shipper-501", "화주"),
                TimeProvider.System));

        var result = await handler.Handle(
            new 화주운송의뢰인수증등록Command("request-501", "receipt-501", "화주 검수 완료"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.운송완료화주인계);
        Assert.Equal(운영체제업무인계상태Codes.수락됨, result.Value.운송완료화주인계!.상태Code);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, result.Value.운송완료화주인계.현재책임운영체제Id);
        var storedRequest = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal("receipt-501", storedRequest.인수증번호);
        Assert.Equal(ShipRequest.운임정산상태.인수증등록완료.ToString(), storedRequest.정산상태);
        Assert.NotEqual(ShipRequest.운임정산상태.정산완료.ToString(), storedRequest.정산상태);
        Assert.Equal(2, await db.운영체제업무인계Outbox.CountAsync());
    }

    [Theory]
    [InlineData(false, "인수완료")]
    [InlineData(true, "운송중")]
    public async Task 증빙이나완료상태가없으면_화주인수인계를만들지않는다(
        bool evidenceAvailable,
        string transportStatus)
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        var transport = CreateCompletedTransport();
        transport.상태 = transportStatus;
        if (transportStatus != 상태값.배차상태.인수완료)
        {
            transport.도착 = null;
        }
        db.운송원장.Add(transport);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.완료결과요청Async(501, evidenceAvailable));

        Assert.Empty(await db.운영체제업무인계.ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.ToListAsync());
    }

    private static 화물운송완료화주인수인계Service CreateService(SsalddelContext db)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new 화물운송완료화주인수인계Service(
            db,
            new 운영체제업무인계Coordinator(db, timeProvider),
            timeProvider);
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            의뢰Id = "request-501",
            화주Id = "shipper-501",
            주문자UserId = "shipper-501",
            상태 = 상태값.의뢰상태.생성됨,
            배차상태 = 상태값.배차상태.인수완료,
            정산상태 = ShipRequest.운임정산상태.인수증대기.ToString(),
            픽업_도로명주소 = "서울 중랑구 출발로 1",
            하차_도로명주소 = "서울 중랑구 도착로 2",
            CreatedAt = Now.AddHours(-2).UtcDateTime,
            UpdatedAt = Now.AddMinutes(-5).UtcDateTime
        };

    private static 운송원장 CreateCompletedTransport()
        => new()
        {
            Id = 501,
            의뢰Id = "request-501",
            운송번호 = "request-501",
            기사_운송자 = "driver-501",
            확정기사Id = "driver-501",
            상태 = 상태값.배차상태.인수완료,
            도착 = Now.AddMinutes(-5).UtcDateTime,
            출발지 = "서울 중랑구 출발로 1",
            도착지 = "서울 중랑구 도착로 2",
            첨부_json = "[{\"objectName\":\"secret-dropoff-photo\"}]",
            CreatedAt = Now.AddHours(-1).UtcDateTime,
            UpdatedAt = Now.AddMinutes(-5).UtcDateTime
        };

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-completion-shipper-handoff-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryption());

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
