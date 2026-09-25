using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Development.MobileFieldTest;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Development;

public sealed class 모바일현장검증자료SeederTests
{
    [Fact]
    public async Task 같은판본을반복시드해도_합성음식점과메뉴가중복되지않는다()
    {
        await using var db = CreateContext();
        var now = new DateTime(2026, 9, 20, 3, 0, 0, DateTimeKind.Utc);

        await 모바일현장검증자료Seeder.SeedAsync(db, now);
        await 모바일현장검증자료Seeder.SeedAsync(db, now.AddMinutes(1));

        Assert.Equal(3, await db.음식점공개프로필.CountAsync());
        Assert.Equal(9, await db.음식점메뉴.CountAsync());
        var managed = await db.음식점공개프로필
            .Include(item => item.메뉴목록)
            .SingleAsync(item => item.Id == 101);
        Assert.Equal("사가정 한상 샘플", managed.상호명);
        Assert.Contains(모바일현장검증자료Seeder.FixtureRevision, managed.소개);
        Assert.All(managed.메뉴목록, menu => Assert.Contains("합성 샘플", menu.설명));
    }

    [Fact]
    public async Task 예약Id에다른자료가있으면_덮어쓰지않고차단한다()
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필
        {
            Id = 101,
            상호명 = "기존 운영 음식점",
            카테고리 = "한식"
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            모바일현장검증자료Seeder.SeedAsync(db, DateTime.UtcNow));

        Assert.Equal("MobileFieldTestFixtureConflict:101", error.Message);
        Assert.Equal("기존 운영 음식점", (await db.음식점공개프로필.FindAsync(101L))!.상호명);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
