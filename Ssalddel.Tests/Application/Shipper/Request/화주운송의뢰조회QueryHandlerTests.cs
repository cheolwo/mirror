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
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송의뢰조회QueryHandlerTests
{
    [Fact]
    public async Task 소유자단건조회는_확정기사와진행중최근위치를_같이반환한다()
    {
        await using var db = CreateContext();
        var recordedAt = DateTime.UtcNow.AddMinutes(-1);
        db.화주운송의뢰.Add(CreateRequest());
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "request-1",
            운송번호 = "transport-1",
            확정기사Id = "driver-1",
            기사_운송자 = "driver-1",
            상태 = 상태값.배차상태.운송중,
            UpdatedAt = DateTime.UtcNow
        });
        db.용달기사.Add(new 용달기사
        {
            기사Id = "driver-1",
            기사명 = "안전기사",
            차량 = "1톤 카고"
        });
        db.기사위치기록.Add(new 기사위치기록
        {
            기사Id = "driver-1",
            위도 = 37.501m,
            경도 = 127.039m,
            기록시각 = recordedAt
        });
        await db.SaveChangesAsync();

        var handler = new 의뢰단건조회QueryHandler(
            db,
            CreateOperatorUseCase(db, "shipper-1", "화주"));

        var response = await handler.Handle(
            new 의뢰단건조회Query("request-1"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(상태값.배차상태.운송중, response!.운송상태);
        Assert.Equal("driver-1", response.확정기사Id);
        Assert.Equal("안전기사", response.확정기사명);
        Assert.Equal("1톤 카고", response.확정기사차량);
        Assert.Equal(37.501m, response.기사최근위도);
        Assert.Equal(127.039m, response.기사최근경도);
        Assert.Equal(recordedAt, response.기사최근위치시각Utc);
    }

    [Fact]
    public async Task 운송종료뒤에는_확정기사정보를유지하되_마지막위치를노출하지않는다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "request-1",
            운송번호 = "transport-1",
            확정기사Id = "driver-1",
            기사_운송자 = "driver-1",
            상태 = 상태값.배차상태.인수완료,
            UpdatedAt = DateTime.UtcNow
        });
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 기사명 = "안전기사" });
        db.기사위치기록.Add(new 기사위치기록
        {
            기사Id = "driver-1",
            위도 = 37.501m,
            경도 = 127.039m,
            기록시각 = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new 의뢰단건조회QueryHandler(
            db,
            CreateOperatorUseCase(db, "shipper-1", "화주"));

        var response = await handler.Handle(
            new 의뢰단건조회Query("request-1"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("안전기사", response!.확정기사명);
        Assert.Null(response.기사최근위도);
        Assert.Null(response.기사최근경도);
        Assert.Null(response.기사최근위치시각Utc);
    }

    [Fact]
    public async Task 운송원장이아직없어도_화주에서화물운송으로넘긴Os인계를재조회한다()
    {
        await using var db = CreateContext();
        var request = CreateRequest();
        request.배차상태 = 상태값.배차상태.미시작;
        db.화주운송의뢰.Add(request);
        await db.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(request.CreatedAt, TimeSpan.Zero));
        var coordinator = new 운영체제업무인계Coordinator(db, timeProvider);
        var handoffService = new 화주운송의뢰화물운송인계Service(db, coordinator, timeProvider);
        await handoffService.인계Async(request.의뢰Id);

        var handler = new 의뢰단건조회QueryHandler(
            db,
            CreateOperatorUseCase(db, "shipper-1", "화주"));

        var response = await handler.Handle(
            new 의뢰단건조회Query("request-1"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotNull(response!.운영체제인계);
        Assert.Equal(운영체제업무인계상태Codes.수락됨, response.운영체제인계!.상태Code);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, response.운영체제인계.출발운영체제Id);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, response.운영체제인계.현재책임운영체제Id);
        Assert.Empty(response.운송상태);
    }

    [Fact]
    public async Task 운송완료뒤에는_화물운송에서화주인수로돌아온Os인계를재조회한다()
    {
        await using var db = CreateContext();
        var request = CreateRequest();
        request.배차상태 = 상태값.배차상태.인수완료;
        db.화주운송의뢰.Add(request);
        db.운송원장.Add(new 운송원장
        {
            Id = 502,
            의뢰Id = request.의뢰Id,
            운송번호 = request.의뢰Id,
            기사_운송자 = "driver-502",
            상태 = 상태값.배차상태.인수완료,
            도착 = request.CreatedAt.AddMinutes(40),
            UpdatedAt = request.CreatedAt.AddMinutes(40)
        });
        await db.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(request.CreatedAt.AddHours(1), TimeSpan.Zero));
        var coordinator = new 운영체제업무인계Coordinator(db, timeProvider);
        var handoffService = new 화물운송완료화주인수인계Service(db, coordinator, timeProvider);
        await handoffService.완료결과요청Async(502, true);

        var handler = new 의뢰단건조회QueryHandler(
            db,
            CreateOperatorUseCase(db, "shipper-1", "화주"));

        var response = await handler.Handle(
            new 의뢰단건조회Query("request-1"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotNull(response!.운송완료화주인계);
        Assert.Equal(운영체제업무인계상태Codes.요청됨, response.운송완료화주인계!.상태Code);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, response.운송완료화주인계.현재책임운영체제Id);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, response.운송완료화주인계.도착운영체제Id);
    }

    [Fact]
    public async Task 화주단건조회는_원문메모나증빙주소없이_비정상운송검토상태를반환한다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "abnormal-transport:601:DropoffRecipientUnavailable",
            운송Id = 601,
            운송의뢰Id = "request-1",
            사건유형Code = 비정상운송사건유형Codes.하차지부재,
            원본예외Code = "하차지부재",
            단계Code = "하차",
            상태Code = 비정상운송사건상태Codes.운영검토대기,
            현재담당Code = 비정상운송사건담당Codes.플랫폼운영검토,
            정산보류적용여부 = true,
            증빙참조있음 = true,
            최초신고시각Utc = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc),
            최근신고시각Utc = new DateTime(2026, 9, 12, 2, 2, 0, DateTimeKind.Utc),
            Revision = 2,
            CreatedAt = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 12, 2, 2, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        var handler = new 의뢰단건조회QueryHandler(
            db,
            CreateOperatorUseCase(db, "shipper-1", "화주"));

        var response = await handler.Handle(new 의뢰단건조회Query("request-1"), CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.비정상운송검토보류중);
        var incident = Assert.Single(response.비정상운송사건목록);
        Assert.Equal(비정상운송사건유형Codes.하차지부재, incident.사건유형Code);
        Assert.Equal("하차지부재", incident.사건유형표시명);
        Assert.True(incident.증빙참조있음);
        Assert.Equal(2, incident.Revision);
        var publicProperties = typeof(Ssalddel.Contracts.Shipper.Request.비정상운송사건Dto)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();
        Assert.DoesNotContain("메모", publicProperties);
        Assert.DoesNotContain("증빙Url", publicProperties);
        Assert.DoesNotContain("상세주소", publicProperties);
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            의뢰Id = "request-1",
            주문자UserId = "shipper-1",
            화주Id = "shipper-1",
            상태 = "생성됨",
            배차상태 = 상태값.배차상태.배차확정,
            결제상태 = 상태값.결제상태.결제완료,
            픽업_도로명주소 = "서울시 강남구",
            하차_도로명주소 = "서울시 송파구"
        };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"shipper-request-query-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private static I화주운송업무담당자UseCase CreateOperatorUseCase(
        SsalddelContext db,
        string? userId,
        string? role)
        => new 화주운송업무담당자UseCase(
            db,
            new TestCurrentUserAccessor(userId, role),
            TimeProvider.System);

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
