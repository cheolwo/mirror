using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송현장정보조회Tests
{
    private static readonly DateTime PickupStart = new(2026, 10, 3, 1, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 배정기사조회는_같은화주의_연락처와상하차시간창을반환한다(string query)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);
        var result = await ReadAsync(db, query, "driver-a", id);

        Assert.NotNull(result);
        Assert.Equal("상차 담당자", result.상차담당자명);
        Assert.Equal("010-0000-1001", result.상차연락처);
        Assert.Equal("수령 담당자", result.수령자명);
        Assert.Equal("010-0000-1002", result.수령자연락처);
        Assert.Equal(PickupStart, result.상차시간창시작일시);
        Assert.Equal(PickupStart.AddHours(1), result.상차시간창종료일시);
        Assert.Equal(PickupStart.AddHours(2), result.하차시간창시작일시);
        Assert.Equal(PickupStart.AddHours(3), result.하차시간창종료일시);
        Assert.True(result.인수증필요);
        Assert.True(result.인수증서명필수);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 운송번호가별도이면_의뢰Id를우선하고_legacy빈의뢰Id는운송번호로조회한다(string query)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);
        var transport = await db.운송원장.SingleAsync();
        transport.운송번호 = "display-transport-number";
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = transport.운송번호,
            픽업_연락처_이름 = "다른 의뢰의 담당자",
            픽업_연락처_전화번호 = "010-0000-9999"
        });
        await db.SaveChangesAsync();

        var canonical = await ReadAsync(db, query, "driver-a", id);
        Assert.NotNull(canonical);
        Assert.Equal("상차 담당자", canonical.상차담당자명);
        Assert.Equal("010-0000-1001", canonical.상차연락처);
        Assert.Equal(PickupStart, canonical.상차시간창시작일시);

        transport.의뢰Id = string.Empty;
        transport.운송번호 = "cargo-field-info";
        await db.SaveChangesAsync();
        var legacy = await ReadAsync(db, query, "driver-a", id);
        Assert.NotNull(legacy);
        Assert.Equal("상차 담당자", legacy.상차담당자명);
        Assert.Equal(PickupStart, legacy.상차시간창시작일시);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 다른기사는_연락처시간창을조회할수없다(string query)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);

        Assert.Null(await ReadAsync(db, query, "driver-other", id));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 기존의뢰의_미입력시간창은_기본날짜를만들지않는다(string query)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);
        var shipper = await db.화주운송의뢰.SingleAsync();
        shipper.픽업_시간창_시작일시 = default;
        shipper.픽업_시간창_종료일시 = default;
        shipper.하차_시간창_시작일시 = default(DateTime);
        shipper.하차_시간창_종료일시 = null;
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, query, "driver-a", id);

        Assert.NotNull(result);
        Assert.Null(result.상차시간창시작일시);
        Assert.Null(result.상차시간창종료일시);
        Assert.Null(result.하차시간창시작일시);
        Assert.Null(result.하차시간창종료일시);
    }

    [Fact]
    public async Task 화주본인재조회는_주소담당자시간창을함께복원하고_다른화주에는노출하지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var actualAuthority = new 화주운송업무담당자UseCase(db, new CurrentUser("shipper-a", "화주"), TimeProvider.System);
        var handler = new 의뢰단건조회QueryHandler(db, actualAuthority);

        var result = await handler.Handle(new 의뢰단건조회Query("cargo-field-info"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("서울 상차 테스트로 1", result.픽업?.주소.도로명주소);
        Assert.Equal("1층 창고", result.픽업?.주소.상세주소);
        Assert.Equal("상차 담당자", result.픽업?.연락처.이름);
        Assert.Equal("010-0000-1001", result.픽업?.연락처.전화번호);
        Assert.Equal(PickupStart, result.픽업?.시간창?.시작일시);
        Assert.Equal(PickupStart.AddHours(1), result.픽업?.시간창?.종료일시);
        Assert.Equal("서울 하차 테스트로 2", result.하차?.주소.도로명주소);
        Assert.Equal("수령 담당자", result.하차?.연락처.이름);
        Assert.Equal(PickupStart.AddHours(2), result.하차?.시간창?.시작일시);
        Assert.Equal(PickupStart.AddHours(3), result.하차?.시간창?.종료일시);

        var otherAuthority = new 화주운송업무담당자UseCase(db, new CurrentUser("shipper-other", "화주"), TimeProvider.System);
        Assert.Null(await new 의뢰단건조회QueryHandler(db, otherAuthority)
            .Handle(new 의뢰단건조회Query("cargo-field-info"), CancellationToken.None));
    }

    [Fact]
    public void 화주응답의_빈위치는null이고_공개화물요약은연락처시간창을추가하지않는다()
    {
        var empty = 화주운송의뢰매퍼.To응답(new 화주운송의뢰());
        Assert.Null(empty.픽업);
        Assert.Null(empty.하차);

        var entity = new 화주운송의뢰
        {
            의뢰Id = "private-contact",
            픽업_연락처_이름 = "상차 비공개 담당자",
            픽업_연락처_전화번호 = "010-0000-1001",
            하차_연락처_이름 = "하차 비공개 담당자",
            하차_연락처_전화번호 = "010-0000-1002",
            픽업_시간창_시작일시 = PickupStart,
            픽업_시간창_종료일시 = PickupStart.AddHours(1)
        };
        var publicSummary = JsonSerializer.Serialize(화주운송의뢰매퍼.To공개화물요약응답(entity));

        Assert.DoesNotContain("010-0000-1001", publicSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("010-0000-1002", publicSummary, StringComparison.Ordinal);
        using var publicJson = JsonDocument.Parse(publicSummary);
        Assert.False(publicJson.RootElement.TryGetProperty("픽업", out _));
        Assert.False(publicJson.RootElement.TryGetProperty("하차", out _));
    }

    [Theory]
    [InlineData("current", null)]
    [InlineData("detail", null)]
    [InlineData("list", null)]
    [InlineData("current", 0)]
    [InlineData("detail", 0)]
    [InlineData("list", 0)]
    [InlineData("current", 475)]
    [InlineData("detail", 475)]
    [InlineData("list", 475)]
    public async Task 세기사조회는_연결된견적거리의null과0을구분한다(string query, int? distanceHundredths)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);
        var fare = new 운임구성 { 의뢰Id = "cargo-field-info", 예상거리Km = distanceHundredths / 100m,
            거리계산방식 = distanceHundredths.HasValue ? "입력거리" : null };
        db.운임구성.Add(fare);
        await db.SaveChangesAsync();
        var shipper = await db.화주운송의뢰.SingleAsync();
        shipper.운임구성Id = fare.Id;
        await db.SaveChangesAsync();
        var result = await ReadAsync(db, query, "driver-a", id);
        Assert.NotNull(result);
        Assert.Equal(fare.예상거리Km, result.예상거리Km);
        Assert.Equal(fare.거리계산방식, result.거리계산방식);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 다른의뢰의잘못된견적연결은_거리를노출하지않는다(string query)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db);
        var foreignFare = new 운임구성 { 의뢰Id = "other-request", 예상거리Km = 99m, 거리계산방식 = "foreign-source" };
        db.운임구성.Add(foreignFare);
        await db.SaveChangesAsync();
        (await db.화주운송의뢰.SingleAsync()).운임구성Id = foreignFare.Id;
        await db.SaveChangesAsync();
        var result = await ReadAsync(db, query, "driver-a", id);
        Assert.NotNull(result);
        Assert.Null(result.예상거리Km);
        Assert.Null(result.거리계산방식);
    }

    private static Task<기사운송요약응답?> ReadAsync(SsalddelContext db, string query, string driver, long id)
        => query switch
        {
            "current" => new 운송현재조회QueryHandler(db).Handle(new 운송현재조회Query(driver), CancellationToken.None),
            "detail" => DetailAsync(db, driver, id),
            "list" => ListAsync(db, driver),
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

    private static async Task<기사운송요약응답?> DetailAsync(SsalddelContext db, string driver, long id)
        => await new 운송상세조회QueryHandler(db).Handle(new 운송상세조회Query(driver, id), CancellationToken.None);

    private static async Task<기사운송요약응답?> ListAsync(SsalddelContext db, string driver)
        => (await new 운송목록조회QueryHandler(db).Handle(new 운송목록조회Query(driver), CancellationToken.None)).SingleOrDefault();

    private static SsalddelContext CreateContext()
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"cargo-contact-window-{Guid.NewGuid():N}").Options, new Encryption());

    private static async Task<long> SeedAsync(SsalddelContext db)
    {
        var transport = new 운송원장
        {
            의뢰Id = "cargo-field-info", 운송번호 = "cargo-field-info", 화주Id = "shipper-a",
            기사_운송자 = "driver-a", 확정기사Id = "driver-a", 상태 = "상차지도착"
        };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = transport.의뢰Id, 화주Id = "shipper-a", 주문자UserId = "shipper-a",
            픽업_도로명주소 = "서울 상차 테스트로 1", 픽업_상세주소 = "1층 창고",
            픽업_연락처_이름 = "상차 담당자", 픽업_연락처_전화번호 = "010-0000-1001",
            픽업_시간창_시작일시 = PickupStart, 픽업_시간창_종료일시 = PickupStart.AddHours(1),
            하차_도로명주소 = "서울 하차 테스트로 2", 하차_연락처_이름 = "수령 담당자",
            하차_연락처_전화번호 = "010-0000-1002", 하차_시간창_시작일시 = PickupStart.AddHours(2),
            하차_시간창_종료일시 = PickupStart.AddHours(3), 증빙방식 = "인수증", 정산메모 = "서명 필수"
        });
        await db.SaveChangesAsync();
        return transport.Id;
    }

    private sealed record CurrentUser(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
