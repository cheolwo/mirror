using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 기사운송원장상호작용Tests
{
    [Fact]
    public async Task 수량불일치신고는_기존운송메모와함께_비정상사건과정산보류를저장한다()
    {
        await using var db = CreateContext();
        var transport = new 운송원장
        {
            운송번호 = "request-abnormal",
            의뢰Id = "request-abnormal",
            화주Id = "shipper-1",
            기사_운송자 = "driver-1",
            확정기사Id = "driver-1",
            상태 = 상태값.배차상태.운송중
        };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-abnormal",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            정산상태 = 운임정산상태.입금확인완료.ToString()
        });
        await db.SaveChangesAsync();
        var handler = new 운송문제신고CommandHandler(
            db,
            new ThrowingPublisher(),
            new TestCurrentUserAccessor("driver-1", 역할명.기사),
            new 참여자실행권한검사(),
            new 운송증빙첨부JsonWriter(),
            new 비정상운송사건Service(db),
            NullLogger<운송문제신고CommandHandler>.Instance);

        var result = await handler.Handle(
            new 운송문제신고Command(
                "driver-1",
                transport.Id,
                "상차",
                "수량불일치",
                "수량이 다릅니다.",
                "운영자 확인 필요",
                "evidence/photo-1.jpg",
                "https://private.example/evidence/photo-1.jpg",
                true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("[운송예외][상차][수량불일치]", transport.메모, StringComparison.Ordinal);
        var incident = await db.비정상운송사건.SingleAsync();
        Assert.Equal(비정상운송사건유형Codes.수량불일치, incident.사건유형Code);
        Assert.True(incident.증빙참조있음);
        Assert.Equal(
            운임정산상태.비정상운송검토보류.ToString(),
            (await db.화주운송의뢰.SingleAsync()).정산상태);
        Assert.DoesNotContain("private.example", incident.사건StableId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 현재운송조회는_배정기사에게_실제수령자정보를반환한다()
    {
        await using var db = CreateContext();
        db.운송원장.Add(new 운송원장
        {
            운송번호 = "request-recipient",
            의뢰Id = "request-recipient",
            화주Id = "shipper-1",
            기사_운송자 = "driver-1",
            확정기사Id = "driver-1",
            상태 = 상태값.배차대기상태.확정,
            출발지 = "서울시 강남구",
            도착지 = "서울시 송파구"
        });
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-recipient",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            화물종류 = "테스트 화물",
            하차_연락처_이름 = "김수령",
            하차_연락처_전화번호 = "010-1234-5678",
            요청사항 = "도착 10분 전에 연락해 주세요."
        });
        await db.SaveChangesAsync();

        var response = await new 운송현재조회QueryHandler(db).Handle(
            new 운송현재조회Query("driver-1"),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("김수령", response.수령자명);
        Assert.Equal("010-1234-5678", response.수령자연락처);
        Assert.Equal("도착 10분 전에 연락해 주세요.", response.전달요청);
    }

    [Fact]
    public async Task 상태변경Event발행이실패해도_화주원장상태는_같이저장된다()
    {
        await using var db = CreateContext();
        var transport = new 운송원장
        {
            운송번호 = "request-1",
            의뢰Id = "request-1",
            화주Id = "shipper-1",
            기사_운송자 = "driver-1",
            확정기사Id = "driver-1",
            상태 = 상태값.배차대기상태.확정
        };
        var shipperRequest = new 화주운송의뢰
        {
            의뢰Id = "request-1",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            화물종류 = "테스트 화물",
            결제상태 = 상태값.결제상태.결제완료,
            배차상태 = 상태값.배차상태.배차확정
        };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(shipperRequest);
        await db.SaveChangesAsync();

        var executor = new 기사운송상태변경CommandExecutor(
            db,
            new 기사운송상태전이Service(),
            new ThrowingPublisher(),
            new TestCurrentUserAccessor("driver-1", "기사"),
            new 참여자실행권한검사(),
            NullLogger<기사운송상태변경CommandExecutor>.Instance);

        var result = await executor.실행Async(
            new 기사운송상태변경요청(
                "driver-1",
                transport.Id,
                "driver-1",
                살뜰역할유형.기사,
                기사운송상태코드.상차지도착,
                "TestTransportArrived",
                context => new TestTransportEvent(context.운송.Id)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(기사운송상태코드.상차지도착, transport.상태);
        Assert.Equal(기사운송상태코드.상차지도착, shipperRequest.배차상태);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"driver-shipper-ledger-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed record TestTransportEvent(long TransportId) : INotification;

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("temporary event failure"));

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.FromException(new InvalidOperationException("temporary event failure"));
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
