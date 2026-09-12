using System.Text.Json;
using Microsoft.AspNetCore.Http;
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

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송업무망조회UseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 화주주문에서_화물실행과_화주인수까지의실제인계를_읽기전용왕복업무망으로조회한다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        db.운송원장.Add(CreateCompletedTransport());
        await db.SaveChangesAsync();
        var coordinator = new 운영체제업무인계Coordinator(db, new FixedTimeProvider(Now));
        var outgoingService = new 화주운송의뢰화물운송인계Service(
            db,
            coordinator,
            new FixedTimeProvider(Now));
        var completionService = new 화물운송완료화주인수인계Service(
            db,
            coordinator,
            new FixedTimeProvider(Now));
        await outgoingService.인계Async("request-network-1");
        await completionService.완료결과요청Async(901, true);
        await completionService.화주인수Async("request-network-1");
        var useCase = CreateUseCase(db, "shipper-network-1");

        var result = await useCase.조회Async("request-network-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderWorkNetworkProjectionContract.Revision, result.Value.ContractRevision);
        Assert.Equal(OrderWorkNetworkProjectionContract.AuthorityBoundary, result.Value.AuthorityBoundary);
        Assert.Equal(OrderWorkNetworkRootTypeCodes.TransportServiceOrder, result.Value.RootTypeCode);
        Assert.Equal(result.Value.RootWorkStableId, result.Value.CorrelationStableId);
        Assert.True(result.Value.RoundTripCompleted);
        Assert.False(result.Value.ContainsPersonalData);
        Assert.Empty(result.Value.PendingCodes);
        Assert.Equal(4, result.Value.Nodes.Count);
        Assert.Collection(
            result.Value.Interactions,
            outgoing =>
            {
                Assert.Equal(OperatingSystemInteractionIds.ShipperRequestToCargo, outgoing.InteractionId);
                Assert.Equal(운영체제업무인계상태Codes.수락됨, outgoing.HandoffStateCode);
                Assert.Equal(OperatingSystemIds.DomesticCargoTransport, outgoing.CurrentResponsibleOperatingSystemId);
            },
            returned =>
            {
                Assert.Equal(OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance, returned.InteractionId);
                Assert.Equal(운영체제업무인계상태Codes.수락됨, returned.HandoffStateCode);
                Assert.Equal(OperatingSystemIds.ShipperTransportManagement, returned.CurrentResponsibleOperatingSystemId);
            });
        var serialized = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("서울 중랑구 비공개 출발지", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("서울 동대문구 비공개 도착지", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1111-2222", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("driver-private-901", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 아직인계되지않은주문은_없는단계를꾸며내지않고_미완료코드로보인다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db, "shipper-network-1");

        var result = await useCase.조회Async("request-network-1");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RoundTripCompleted);
        Assert.Single(result.Value.Nodes);
        Assert.Empty(result.Value.Interactions);
        Assert.Contains(OrderWorkNetworkPendingCodes.CargoHandoffPending, result.Value.PendingCodes);
        Assert.Contains(OrderWorkNetworkPendingCodes.CargoExecutionLedgerPending, result.Value.PendingCodes);
        Assert.Contains(OrderWorkNetworkPendingCodes.CompletionReturnPending, result.Value.PendingCodes);
    }

    [Fact]
    public async Task 조회권한이없는사용자에게는_업무망존재를노출하지않는다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db, "outsider");

        var result = await useCase.조회Async("request-network-1");

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error =>
            error.Metadata.TryGetValue("StatusCode", out var status)
            && status is int code
            && code == StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task 화주가지정한보조담당자는_기본진행조회권한으로업무망을볼수있다()
    {
        await using var db = CreateContext();
        db.Users.AddRange(
            new ApplicationUser { Id = "shipper-network-1", UserName = "shipper-network-1" },
            new ApplicationUser { Id = "assistant-network-1", UserName = "assistant-network-1" });
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(Now);
        var ownerAssignments = new 화주운송업무담당자UseCase(
            db,
            new TestCurrentUserAccessor("shipper-network-1", "화주"),
            timeProvider);
        var assignment = await ownerAssignments.변경Async(
            "request-network-1",
            new 운송업무담당자배정변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = 0,
                담당자목록 =
                [
                    new 운송업무담당자지정요청
                    {
                        UserId = "shipper-network-1",
                        담당유형Code = 운송업무담당유형Codes.주담당
                    },
                    new 운송업무담당자지정요청
                    {
                        UserId = "assistant-network-1",
                        담당유형Code = 운송업무담당유형Codes.보조담당
                    }
                ]
            });
        Assert.True(assignment.IsSuccess);
        var assistantUseCase = CreateUseCase(db, "assistant-network-1");

        var result = await assistantUseCase.조회Async("request-network-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("shipper-transport-request:request-network-1", result.Value.RootWorkStableId);
    }

    private static 화주운송업무망조회UseCase CreateUseCase(SsalddelContext db, string userId)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new 화주운송업무망조회UseCase(
            db,
            new 화주운송업무담당자UseCase(
                db,
                new TestCurrentUserAccessor(userId, "화주"),
                timeProvider),
            timeProvider);
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            의뢰Id = "request-network-1",
            화주Id = "shipper-network-1",
            주문자UserId = "shipper-network-1",
            상태 = 상태값.의뢰상태.생성됨,
            배차상태 = 상태값.배차상태.인수완료,
            픽업_도로명주소 = "서울 중랑구 비공개 출발지",
            픽업_연락처_전화번호 = "010-1111-2222",
            하차_도로명주소 = "서울 동대문구 비공개 도착지",
            CreatedAt = Now.AddHours(-2).UtcDateTime,
            UpdatedAt = Now.AddMinutes(-5).UtcDateTime
        };

    private static 운송원장 CreateCompletedTransport()
        => new()
        {
            Id = 901,
            의뢰Id = "request-network-1",
            운송번호 = "request-network-1",
            기사_운송자 = "driver-private-901",
            확정기사Id = "driver-private-901",
            상태 = 상태값.배차상태.인수완료,
            출발지 = "서울 중랑구 비공개 출발지",
            도착지 = "서울 동대문구 비공개 도착지",
            도착 = Now.AddMinutes(-5).UtcDateTime,
            CreatedAt = Now.AddHours(-1).UtcDateTime,
            UpdatedAt = Now.AddMinutes(-5).UtcDateTime
        };

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"transport-work-network-{Guid.NewGuid():N}")
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
