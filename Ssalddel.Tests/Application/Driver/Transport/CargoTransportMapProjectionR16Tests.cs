using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoTransportMapProjectionR16Tests
{
    [Fact]
    public async Task 기사_소유_운송의_명시_의뢰_관계로만_지도_좌표를_연결한다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        db.화주운송의뢰.Add(new() { 의뢰Id = transport.운송번호, 픽업_위도 = 35m, 픽업_경도 = 129m });
        db.화주운송의뢰.Add(new() { 의뢰Id = transport.Id.ToString(), 픽업_위도 = 36m, 픽업_경도 = 128m });
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, transport.Id);

        Assert.NotNull(result);
        Assert.Equal(37.57m, result.픽업위도);
        Assert.Equal(127.01m, result.픽업경도);
        Assert.Equal(37.59m, result.하차위도);
        Assert.Equal(127.04m, result.하차경도);
        Assert.Equal(transport.운송번호, result.운송번호);
    }

    [Fact]
    public async Task 다른_기사의_운송은_주소와_좌표를_조회할_수_없다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);

        Assert.Null(await new 운송상세조회QueryHandler(db)
            .Handle(new("other-driver", transport.Id), default));
    }

    [Fact]
    public async Task 음식_배달_원장은_화물_지도_계약으로_조회하지_않는다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        transport.배차업무유형 = 상태값.배차업무유형.음식배달;
        await db.SaveChangesAsync();

        Assert.Null(await ReadAsync(db, transport.Id));
    }

    [Fact]
    public async Task 의뢰Id가_없는_구판_운송은_기존_부가정보만_호환하고_좌표를_추정하지_않는다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        transport.운송번호 = transport.의뢰Id;
        transport.의뢰Id = string.Empty;
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, transport.Id);

        Assert.NotNull(result);
        Assert.Equal("상차 담당자", result.상차담당자명);
        AssertNoCoordinates(result);
    }

    [Fact]
    public async Task 명시_의뢰_원천이_없으면_운송번호나_원장_좌표로_대신하지_않는다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        transport.의뢰Id = "missing-request";
        transport.픽업_위도 = 35m;
        transport.픽업_경도 = 129m;
        db.화주운송의뢰.Add(new() { 의뢰Id = transport.운송번호, 픽업_위도 = 36m, 픽업_경도 = 128m });
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, transport.Id);

        Assert.NotNull(result);
        AssertNoCoordinates(result);
    }

    [Theory]
    [InlineData(null, 127.01)]
    [InlineData(91.0, 127.01)]
    [InlineData(37.57, 181.0)]
    public async Task 누락되거나_범위를_벗어난_좌표쌍은_미확인으로_유지한다(double? latitude, double longitude)
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        var source = await db.화주운송의뢰.SingleAsync();
        source.픽업_위도 = latitude.HasValue ? (decimal)latitude.Value : null;
        source.픽업_경도 = (decimal)longitude;
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, transport.Id);

        Assert.NotNull(result);
        Assert.Null(result.픽업위도);
        Assert.Null(result.픽업경도);
        Assert.Equal(37.59m, result.하차위도);
        Assert.Equal(127.04m, result.하차경도);
    }

    [Fact]
    public async Task 생활배송_기사_정보_제공_동의가_없으면_새_좌표도_공개하지_않는다()
    {
        await using var db = CreateContext();
        var transport = await SeedAsync(db);
        (await db.화주운송의뢰.SingleAsync()).클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "example";
        await db.SaveChangesAsync();

        var result = await ReadAsync(db, transport.Id);

        Assert.NotNull(result);
        Assert.True(result.개인정보제공보류);
        AssertNoCoordinates(result);
        Assert.Equal(string.Empty, result.상차연락처);
    }

    private static Task<기사운송상세응답?> ReadAsync(SsalddelContext db, long id)
        => new 운송상세조회QueryHandler(db).Handle(new("driver-a", id), default);

    private static void AssertNoCoordinates(기사운송상세응답 source)
    {
        Assert.Null(source.픽업위도); Assert.Null(source.픽업경도);
        Assert.Null(source.하차위도); Assert.Null(source.하차경도);
    }

    private static SsalddelContext CreateContext()
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("cargo-map-r16-" + Guid.NewGuid().ToString("N")).Options,
            new Encryption());

    private static async Task<운송원장> SeedAsync(SsalddelContext db)
    {
        var transport = new 운송원장
        {
            의뢰Id = "source-request", 운송번호 = "visible-transport-number",
            기사_운송자 = "driver-a", 확정기사Id = "driver-a", 화주Id = "shipper-a",
            출발지 = "상차 주소", 도착지 = "하차 주소", 상태 = "상차지도착"
        };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = transport.의뢰Id, 화주Id = "shipper-a", 주문자UserId = "shipper-a",
            픽업_위도 = 37.57m, 픽업_경도 = 127.01m, 하차_위도 = 37.59m, 하차_경도 = 127.04m,
            픽업_연락처_이름 = "상차 담당자", 픽업_연락처_전화번호 = "010-0000-1001"
        });
        await db.SaveChangesAsync();
        return transport;
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
