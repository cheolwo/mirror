using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 비정상운송사건ServiceTests
{
    [Theory]
    [InlineData("수량불일치", 비정상운송사건유형Codes.수량불일치)]
    [InlineData("화물훼손", 비정상운송사건유형Codes.화물훼손)]
    [InlineData("하차지부재", 비정상운송사건유형Codes.하차지부재)]
    public async Task 우선대상_세유형은_사건을열고_화주정산을보류한다(
        string exceptionCode,
        string expectedTypeCode)
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateShipperRequest());
        await db.SaveChangesAsync();
        var now = new DateTime(2026, 9, 12, 1, 0, 0, DateTimeKind.Utc);
        var service = new 비정상운송사건Service(db);

        var result = await service.접수Async(
            new 비정상운송사건접수요청(71, "request-71", "하차", exceptionCode, true, now));
        await db.SaveChangesAsync();

        Assert.True(result.처리대상);
        Assert.True(result.신규생성);
        Assert.NotNull(result.사건);
        Assert.Equal(expectedTypeCode, result.사건!.사건유형Code);
        Assert.Equal(비정상운송사건상태Codes.운영검토대기, result.사건.상태Code);
        Assert.Equal(비정상운송사건담당Codes.플랫폼운영검토, result.사건.현재담당Code);
        Assert.Equal(운임정산상태.입금확인완료.ToString(), result.사건.보류전정산상태Code);
        Assert.True(result.사건.정산보류적용여부);
        Assert.Equal(비정상운송업무통제상태Codes.주의진행, result.사건.업무통제상태Code);
        Assert.Equal(비정상운송보류범위Codes.없음, result.사건.보류범위Code);
        Assert.True(result.사건.증빙참조있음);
        Assert.Equal(
            운임정산상태.비정상운송검토보류.ToString(),
            (await db.화주운송의뢰.SingleAsync()).정산상태);
    }

    [Fact]
    public async Task 열개중_정상여덟개와_파손두개를기록하고_검토결정뒤_영향분만보류한다()
    {
        await using var db = CreateContext();
        var shipperRequest = CreateShipperRequest();
        shipperRequest.화물수량 = 10;
        db.화주운송의뢰.Add(shipperRequest);
        await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);
        var reportedAt = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);

        var reported = await service.접수Async(
            new 비정상운송사건접수요청(
                71,
                "request-71",
                "하차",
                "화물훼손",
                true,
                reportedAt,
                정상확인수량: 8,
                영향수량: 2,
                현장진행불가: false));
        await db.SaveChangesAsync();

        Assert.NotNull(reported.사건);
        Assert.Equal(10, reported.사건!.전체수량);
        Assert.Equal(8, reported.사건.정상확인수량);
        Assert.Equal(2, reported.사건.영향수량);
        Assert.Equal(비정상운송업무통제상태Codes.일부보류, reported.사건.업무통제상태Code);
        Assert.Equal(비정상운송보류범위Codes.영향수량, reported.사건.보류범위Code);
        Assert.True(reported.사건.정산보류적용여부);

        var reviewRequestId = Guid.NewGuid();
        var reviewed = await service.검토Async(
            new 비정상운송사건검토요청(
                reported.사건.사건StableId,
                reviewRequestId,
                예상Revision: 1,
                비정상운송해결결과Codes.정상분인수영향분보류,
                정상확인수량: 8,
                영향수량: 2,
                보험적용가능성검토요청: true,
                "정상 8개를 인수하고 파손 2개는 적재물 보험 적용 가능성을 검토합니다.",
                "admin-1"));
        await db.SaveChangesAsync();

        Assert.True(reviewed.찾음);
        Assert.False(reviewed.멱등재시도);
        Assert.Equal(2, reviewed.사건!.Revision);
        Assert.Equal(비정상운송사건상태Codes.조치결정, reviewed.사건.상태Code);
        Assert.Equal(비정상운송업무통제상태Codes.일부보류, reviewed.사건.업무통제상태Code);
        Assert.Equal(비정상운송보류범위Codes.영향수량, reviewed.사건.보류범위Code);
        Assert.Equal(비정상운송보험검토상태Codes.적용가능성검토대기, reviewed.사건.보험검토상태Code);
        Assert.False(reviewed.사건.정산보류적용여부);
        Assert.Equal(
            운임정산상태.입금확인완료.ToString(),
            (await db.화주운송의뢰.SingleAsync()).정산상태);

        var replay = await service.검토Async(
            new 비정상운송사건검토요청(
                reviewed.사건.사건StableId,
                reviewRequestId,
                예상Revision: 1,
                비정상운송해결결과Codes.정상분인수영향분보류,
                정상확인수량: 8,
                영향수량: 2,
                보험적용가능성검토요청: true,
                "정상 8개를 인수하고 파손 2개는 적재물 보험 적용 가능성을 검토합니다.",
                "admin-1"));

        Assert.True(replay.멱등재시도);
        Assert.Equal(2, replay.사건!.Revision);
    }

    [Fact]
    public async Task 검토는_예상revision이다르면_거부한다()
    {
        await using var db = CreateContext();
        var request = CreateShipperRequest();
        request.화물수량 = 10;
        db.화주운송의뢰.Add(request);
        await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);
        var reported = await service.접수Async(
            new 비정상운송사건접수요청(
                71,
                "request-71",
                "하차",
                "화물훼손",
                true,
                DateTime.UtcNow,
                8,
                2));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.검토Async(
            new 비정상운송사건검토요청(
                reported.사건!.사건StableId,
                Guid.NewGuid(),
                예상Revision: 99,
                비정상운송해결결과Codes.정상분인수영향분보류,
                8,
                2,
                true,
                "사람 검토",
                "admin-1")));
    }

    [Fact]
    public async Task 정상수량과_영향수량합이_전체수량과다르면_접수를거부한다()
    {
        await using var db = CreateContext();
        var request = CreateShipperRequest();
        request.화물수량 = 10;
        db.화주운송의뢰.Add(request);
        await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.접수Async(
            new 비정상운송사건접수요청(
                71,
                "request-71",
                "하차",
                "화물훼손",
                true,
                DateTime.UtcNow,
                정상확인수량: 8,
                영향수량: 1)));
    }

    [Fact]
    public async Task 같은운송과유형의_재신고는_새사건없이_revision만전진한다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateShipperRequest());
        await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);
        var firstAt = new DateTime(2026, 9, 12, 1, 0, 0, DateTimeKind.Utc);
        await service.접수Async(
            new 비정상운송사건접수요청(71, "request-71", "상차", "수량불일치", false, firstAt));
        await db.SaveChangesAsync();

        var replay = await service.접수Async(
            new 비정상운송사건접수요청(71, "request-71", "운행중", "수량불일치", true, firstAt.AddMinutes(3)));
        await db.SaveChangesAsync();

        Assert.True(replay.처리대상);
        Assert.False(replay.신규생성);
        var incident = await db.비정상운송사건.SingleAsync();
        Assert.Equal(2, incident.Revision);
        Assert.Equal("운행중", incident.단계Code);
        Assert.True(incident.증빙참조있음);
        Assert.Equal(firstAt.AddMinutes(3), incident.최근신고시각Utc);
    }

    [Fact]
    public async Task 그밖의예외는_기존신고흐름만유지하고_사건과정산보류를만들지않는다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateShipperRequest());
        await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);

        var result = await service.접수Async(
            new 비정상운송사건접수요청(
                71,
                "request-71",
                "증빙",
                "사진재촬영필요",
                true,
                DateTime.UtcNow));
        await db.SaveChangesAsync();

        Assert.False(result.처리대상);
        Assert.Empty(db.비정상운송사건);
        Assert.Equal(
            운임정산상태.입금확인완료.ToString(),
            (await db.화주운송의뢰.SingleAsync()).정산상태);
    }

    private static 화주운송의뢰 CreateShipperRequest()
        => new()
        {
            의뢰Id = "request-71",
            화주Id = "shipper-71",
            정산상태 = 운임정산상태.입금확인완료.ToString()
        };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"abnormal-transport-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
