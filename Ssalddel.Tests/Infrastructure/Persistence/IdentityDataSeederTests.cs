using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Food;
using System.Text.Json;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.차량;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class IdentityDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WithDefaultOptions_CreatesRolesButNoUsers()
    {
        await using var fixture = await IdentitySeedFixture.CreateAsync();

        await IdentityDataSeeder.SeedAsync(
            fixture.Services,
            includeDevelopmentAccounts: false);

        using var assertionScope = fixture.Services.CreateScope();
        var roleManager = assertionScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = assertionScope.ServiceProvider.GetRequiredService<SsalddelContext>();
        Assert.True(await roleManager.RoleExistsAsync(역할명.서버관리자));
        Assert.True(await roleManager.RoleExistsAsync(역할명.음식점));
        Assert.Empty(await db.Users.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task 개발계정시드는_음식점역할과고정된음식점범위를부여한다()
    {
        const string password = "StrongSeed123!";
        await using var fixture = await IdentitySeedFixture.CreateAsync(options =>
        {
            options.DevelopmentAccounts.Enabled = true;
            options.DevelopmentAccounts.AdminPassword = password;
            options.DevelopmentAccounts.OrdererPassword = password;
            options.DevelopmentAccounts.DriverPassword = password;
            options.DevelopmentAccounts.ShipperPassword = password;
        });

        await IdentityDataSeeder.SeedAsync(
            fixture.Services,
            includeDevelopmentAccounts: true);

        var restaurant = Assert.IsType<ApplicationUser>(
            await fixture.UserManager.FindByNameAsync("restaurant101"));
        Assert.True(await fixture.UserManager.IsInRoleAsync(restaurant, 역할명.음식점));
        var claims = await fixture.UserManager.GetClaimsAsync(restaurant);
        Assert.Contains(
            claims,
            claim => claim.Type == 음식점접근ClaimTypes.음식점Id
                     && claim.Value == "101");
        var orderer = Assert.IsType<ApplicationUser>(
            await fixture.UserManager.FindByNameAsync("field-orderer"));
        Assert.True(await fixture.UserManager.IsInRoleAsync(orderer, 역할명.커뮤니티회원));
        Assert.True(await fixture.Db.주문자프로필.AnyAsync(profile => profile.UserId == orderer.Id));
    }

    [Fact]
    public async Task SeedAsync_WithExplicitBootstrap_CreatesOneServerAdminIdempotently()
    {
        await using var fixture = await IdentitySeedFixture.CreateAsync(options =>
        {
            options.BootstrapAdmin.Enabled = true;
            options.BootstrapAdmin.UserName = "bootstrap-admin";
            options.BootstrapAdmin.Email = "bootstrap-admin@example.com";
            options.BootstrapAdmin.Password = "StrongBootstrap123!";
        });

        await IdentityDataSeeder.SeedAsync(fixture.Services);
        await IdentityDataSeeder.SeedAsync(fixture.Services);

        using var assertionScope = fixture.Services.CreateScope();
        var userManager = assertionScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = assertionScope.ServiceProvider.GetRequiredService<SsalddelContext>();
        var admin = Assert.IsType<ApplicationUser>(
            await userManager.FindByNameAsync("bootstrap-admin"));
        Assert.True(admin.LockoutEnabled);
        Assert.True(await userManager.IsInRoleAsync(admin, 역할명.서버관리자));
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task 기본시드는_소형차미확인템플릿을_비활성으로한번만추가한다()
    {
        await using var fixture = await IdentitySeedFixture.CreateAsync();

        await IdentityDataSeeder.SeedAsync(fixture.Services);
        await IdentityDataSeeder.SeedAsync(fixture.Services);

        var templates = await fixture.Db.차량제원.AsNoTracking()
            .Where(x => x.차량코드 == 소형차제원SeedCatalog.오토바이코드 || x.차량코드 == 소형차제원SeedCatalog.다마스코드)
            .ToArrayAsync();
        Assert.Equal(2, templates.Length);
        Assert.All(templates, item =>
        {
            Assert.False(item.추천사용여부);
            Assert.Equal(0, item.적재함길이Mm);
            Assert.Equal(0, item.적재함폭Mm);
            Assert.Null(item.적재함높이Mm);
            Assert.Equal(0, item.최대적재중량Kg);
            Assert.Null(item.운영권장중량Kg);
            Assert.Null(item.권장최대CBM);
        });
        Assert.True(await fixture.Db.차량제원.AsNoTracking()
            .Where(x => x.차량코드 == "1톤 카고").Select(x => x.추천사용여부).SingleAsync());
    }

    [Theory]
    [InlineData(소형차제원SeedCatalog.오토바이코드, true)]
    [InlineData(소형차제원SeedCatalog.오토바이코드, false)]
    [InlineData(소형차제원SeedCatalog.다마스코드, true)]
    [InlineData(소형차제원SeedCatalog.다마스코드, false)]
    public async Task 반복시드는_기존소형차의실측값과사용여부와수정시각을보존한다(string code, bool enabled)
    {
        await using var fixture = await IdentitySeedFixture.CreateAsync();
        var observedAt = new DateTime(2026, 10, 18, 1, 2, 3, DateTimeKind.Utc);
        fixture.Db.차량제원.Add(new 차량제원
        {
            차량코드 = code,
            차량명 = "합성 실측 검증 차량",
            제조사 = "시험 제조사",
            모델명 = "시험 실차",
            차급 = "시험 차급",
            차체형태 = "시험 적재함",
            적재함길이Mm = 800,
            적재함폭Mm = 500,
            적재함높이Mm = 500,
            최대적재중량Kg = 20,
            운영권장중량Kg = 15,
            권장최대CBM = 0.1m,
            팔레트적재개수 = 0,
            추천사용여부 = enabled,
            추천우선순위 = 7,
            비눈보호가능 = true,
            장점메모 = "가상 시험용 확인 기록",
            단점메모 = "실제 운영 제원 아님",
            CreatedAt = observedAt,
            UpdatedAt = observedAt
        });
        await fixture.Db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await fixture.Db.차량제원.AsNoTracking().SingleAsync(x => x.차량코드 == code));

        await IdentityDataSeeder.SeedAsync(fixture.Services);
        await IdentityDataSeeder.SeedAsync(fixture.Services);

        var after = await fixture.Db.차량제원.AsNoTracking().SingleAsync(x => x.차량코드 == code);
        Assert.Equal(before, JsonSerializer.Serialize(after));
        Assert.Equal(2, await fixture.Db.차량제원.AsNoTracking()
            .CountAsync(x => x.차량코드 == 소형차제원SeedCatalog.오토바이코드 || x.차량코드 == 소형차제원SeedCatalog.다마스코드));
    }

    private sealed class IdentitySeedFixture : IAsyncDisposable
    {
        // EF 내부 provider는 공유하고, 각 시험의 데이터는 고유 databaseName으로 분리합니다.
        private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
        private readonly ServiceProvider provider;

        private IdentitySeedFixture(ServiceProvider provider)
        {
            this.provider = provider;
            Services = provider;
            Db = provider.GetRequiredService<SsalddelContext>();
            UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        }

        public IServiceProvider Services { get; }

        public SsalddelContext Db { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public RoleManager<IdentityRole> RoleManager { get; }

        public static async Task<IdentitySeedFixture> CreateAsync(
            Action<IdentitySeedOptions>? configure = null)
        {
            var services = new ServiceCollection();
            var databaseName = $"identity-seed-{Guid.NewGuid():N}";
            services.AddLogging();
            services.AddSingleton<IPersonalDataEncryptionService, DummyPersonalDataEncryptionService>();
            services.AddDbContext<SsalddelContext>(options =>
                options.UseInMemoryDatabase(
                    databaseName,
                    DatabaseRoot));
            services.Configure<IdentitySeedOptions>(options => configure?.Invoke(options));
            services
                .AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SsalddelContext>();

            var provider = services.BuildServiceProvider();
            var fixture = new IdentitySeedFixture(provider);
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
