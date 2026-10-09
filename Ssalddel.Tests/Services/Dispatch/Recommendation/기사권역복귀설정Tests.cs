using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Ssalddel.Application.Driver.Settings;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 기사권역복귀설정Tests
{
    [Theory]
    [InlineData("off", null, null, 7, 6, true)]
    [InlineData("automatic", 37.5d, 127d, 7, 6, true)]
    [InlineData("on", null, null, 7, 6, false)]
    [InlineData("automatic", 37.5d, null, 7, 6, false)]
    [InlineData("automatic", 91d, 127d, 7, 6, false)]
    [InlineData("자동", 37.5d, 127d, 7, 6, false)]
    [InlineData("on", 37.5d, 127d, 6, 7, false)]
    [InlineData("off", null, null, 7, 0, false)]
    [InlineData("off", null, null, 101, 6, false)]
    public void 설정은명시적code좌표쌍범위와진입해제반경을검증한다(string mode, double? lat, double? lon, int enter, int release, bool valid)
    {
        var settings = new 기사권역복귀설정 { Mode = mode, 기준위도 = (decimal?)lat, 기준경도 = (decimal?)lon, 진입반경Km = enter, 해제반경Km = release };
        Assert.Equal(valid, 기사권역복귀설정검증.유효(settings, 0));
        Assert.False(기사권역복귀설정검증.유효(settings, 1));
    }

    [Fact]
    public void 기준점은보호payload만보관하고소유자문서판본불일치는거부한다()
    {
        var store = new Mongo기사권역복귀설정Store(new MongoClient("mongodb://127.0.0.1:27017"),
            Options.Create(new MongoDbOptions { Database = "never-connected-unit-test" }), new EphemeralDataProtectionProvider());
        var settings = new 기사권역복귀설정 { Revision = 2, Mode = "automatic", 기준위치명 = "비공개 활동 중심", 기준위도 = 37.5m, 기준경도 = 127.12m };
        var document = Write(store, "private-driver", settings);
        Assert.DoesNotContain("private-driver", document.Id);
        Assert.DoesNotContain(settings.기준위치명!, document.ProtectedPayload);
        Assert.DoesNotContain("37.5", document.ProtectedPayload);
        var roundtrip = Read(store, "private-driver", document);
        Assert.Equal(settings.기준위도, roundtrip.설정.기준위도);
        Assert.False(roundtrip.활성여부);
        document.Revision++;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Read(store, "private-driver", document)).InnerException);
        document.Revision--;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Read(store, "other-driver", document)).InnerException);
    }

    [Fact]
    public async Task 본인기사프로필이없으면저장소에접근하지않고등록된본인설정만조회한다()
    {
        using var db = Context(); var store = new Store(); var usecase = new 기사권역복귀설정UseCase(db, store);
        Assert.True((await usecase.조회Async(null)).IsFailed);
        Assert.True((await usecase.저장Async("unregistered", new() { 설정 = new() })).IsFailed);
        Assert.Empty(store.Owners);
        db.용달기사.Add(new 용달기사 { 기사Id = "driver", 기본복귀지주소 = "기존 집 주소", 기본복귀지위도 = 37m, 기본복귀지경도 = 127m });
        await db.SaveChangesAsync();
        var result = await usecase.조회Async("driver");
        Assert.Equal("driver", result.Value.DriverId); Assert.Equal("off", result.Value.설정.Mode);
        Assert.Equal(["driver"], store.Owners);
        Assert.Equal("기존 집 주소", db.용달기사.Single().기본복귀지주소);
    }

    [Fact]
    public async Task 음식배달기사만등록된본인도조회저장할수있고다른기사프로필로대신접근하지않는다()
    {
        using var db = Context(); db.배달기사.Add(new 배달기사 { 기사Id = "food-only" }); await db.SaveChangesAsync();
        Assert.Empty(db.용달기사);
        var store = new Store(); var usecase = new 기사권역복귀설정UseCase(db, store);
        var query = await usecase.조회Async("food-only");
        Assert.True(query.IsSuccess); Assert.Equal("food-only", query.Value.DriverId);
        var saved = await usecase.저장Async("food-only", new() { 설정 = new() });
        Assert.True(saved.IsSuccess); Assert.Equal("food-only", saved.Value.DriverId); Assert.Equal(1, saved.Value.설정.Revision);
        Assert.True((await usecase.조회Async("other-food-driver")).IsFailed);
        Assert.Equal(["food-only", "food-only"], store.Owners);
    }

    [Fact]
    public async Task 저장판본충돌은409와고정code이며기존집복귀지를수정하지않는다()
    {
        using var db = Context(); db.용달기사.Add(new 용달기사 { 기사Id = "driver", 기본복귀지주소 = "기존 복귀지" }); await db.SaveChangesAsync();
        var result = await new 기사권역복귀설정UseCase(db, new Store { Conflict = true }).저장Async("driver", new() { 설정 = new() });
        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal("DriverReturnAreaSettingsRevisionConflict", result.Errors.Single().Metadata["Code"]);
        Assert.Equal("기존 복귀지", db.용달기사.Single().기본복귀지주소);
    }

    private static Mongo기사권역복귀설정Store.기사권역복귀설정Document Write(Mongo기사권역복귀설정Store store, string owner, 기사권역복귀설정 settings)
        => (Mongo기사권역복귀설정Store.기사권역복귀설정Document)typeof(Mongo기사권역복귀설정Store)
            .GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [owner, settings])!;
    private static 기사권역복귀설정Snapshot Read(Mongo기사권역복귀설정Store store, string owner, Mongo기사권역복귀설정Store.기사권역복귀설정Document document)
        => (기사권역복귀설정Snapshot)typeof(Mongo기사권역복귀설정Store)
            .GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [owner, document])!;
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("return-area-settings-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Store : I기사권역복귀설정Store
    {
        public List<string> Owners { get; } = [];
        public bool Conflict { get; set; }
        public Task<기사권역복귀설정Snapshot> 조회Async(string driverId, CancellationToken cancellationToken = default)
        { Owners.Add(driverId); return Task.FromResult(new 기사권역복귀설정Snapshot(new())); }
        public Task<기사권역복귀설정Snapshot> 저장Async(string driverId, 기사권역복귀설정 settings, long expectedRevision, CancellationToken cancellationToken = default)
        { if (Conflict) throw new 기사권역복귀설정ConflictException(); Owners.Add(driverId); return Task.FromResult(new 기사권역복귀설정Snapshot(new() { Revision = expectedRevision + 1 })); }
        public Task<bool> 활성상태갱신Async(string driverId, long revision, bool active, DateTime positionRecordedUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
