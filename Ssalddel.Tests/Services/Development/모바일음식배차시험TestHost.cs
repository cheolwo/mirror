using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Development.MobileFieldTest;
using Ssalddel.Services.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Google;
using 살뜰.Services.External.Naver;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Development;

/// <summary>실제 큐 전환/음식 후보 정책과 SQLite를 사용한다. 신원 접속/알림 전송은 대역이다.</summary>
public sealed class 모바일음식배차시험TestHost : IAsyncDisposable
{
    public const string DriverId = "mobile-fixture-driver";
    public const string OrdererId = "mobile-fixture-orderer";
    public const string RestaurantUserId = "mobile-fixture-restaurant";
    public 모바일음식배차시험Options Options { get; } = new() { Enabled = true, BootstrapSyntheticDriverState = true };
    public 모바일음식배차시험자료 Fixtures { get; } = new();
    public 모바일음식배차시험Guard Guard { get; private set; } = null!;
    public InMemory국내화물운송기사상태Store States { get; } = new();
    public InMemoryDriverLocationStore Locations { get; } = new();
    public InMemory음식배달권실행공간Store Scopes { get; } = new();
    public InMemoryDriverRejectedRequestStore Rejections { get; } = new();
    public Receiving ReceivingState { get; } = new();
    public Notifications NotificationState { get; } = new();
    public 모바일음식배차시험작업자 Worker { get; private set; } = null!;
    public ServiceProvider Services { get; private set; } = null!;
    private SqliteConnection _connection = null!;

    public static IConfiguration Configuration(Dictionary<string, string?>? changes = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["SsalddelExecution:Mode"] = "Simulation", ["DOTNET_RUNNING_IN_CONTAINER"] = "true",
            ["ConnectionStrings:DefaultConnection"] = "server=mysql;database=ssalddel_food_mobile_test;user=test;password=fixture-password;",
            ["MongoDb:ConnectionString"] = "mongodb://mongo:27017", ["MongoDb:Database"] = "ssalddel_food_mobile_test",
            ["MobileFieldTest:Enabled"] = "true", ["MobileFieldTest:FoodDispatch:Enabled"] = "true",
            ["IdentitySeed:DevelopmentAccounts:Enabled"] = "true", ["IdentitySeed:DevelopmentAccounts:AllowInSimulation"] = "true",
            ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "true"
        };
        foreach (var change in changes ?? new Dictionary<string, string?>()) values[change.Key] = change.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public static 모바일음식배차시험Guard CreateGuard(모바일음식배차시험Options options,
        IConfiguration? configuration = null, string environment = "Staging", SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        => new(options, configuration ?? Configuration(), new HostEnvironment { EnvironmentName = environment },
            new SsalddelExecutionModePolicy(Microsoft.Extensions.Options.Options.Create(new SsalddelExecutionOptions { Mode = mode })));

    public static async Task<모바일음식배차시험TestHost> CreateAsync(bool prepare = true)
    {
        var host = new 모바일음식배차시험TestHost();
        host._connection = new SqliteConnection("Data Source=:memory:");
        await host._connection.OpenAsync();
        host.Guard = CreateGuard(host.Options);
        var services = new ServiceCollection();
        services.AddScoped(_ => host.CreateContext());
        services.AddSingleton(host.Options).AddSingleton(host.Fixtures).AddSingleton(host.Guard);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<I국내화물운송기사상태Store>(host.States);
        services.AddSingleton<IDriverLocationStore>(host.Locations);
        services.AddSingleton<IDriverWorkQueueStore, InMemoryDriverWorkQueueStore>();
        services.AddSingleton<I음식배달권실행공간Store>(host.Scopes);
        services.AddSingleton<IDriverRejectedRequestStore>(host.Rejections);
        services.AddSingleton<I운영배차공통UseCase>(host.ReceivingState);
        services.AddSingleton<I배차추천알림Service>(host.NotificationState);
        services.AddScoped<I국내화물운송기사상태Service, 국내화물운송기사상태Service>();
        services.AddScoped<I음식배달기사추천기록Service, 음식배달기사추천기록Service>();
        services.AddScoped<모바일음식배차시험준비Service>();
        services.AddSingleton<IOptions<FoodDriverRoutePricingOptions>>(Microsoft.Extensions.Options.Options.Create(new FoodDriverRoutePricingOptions()));
        services.AddScoped<I음식배달기사제안요금Service, 모바일음식배차시험요금Service>();
        services.AddScoped<I배차추천경로Service>(p => new 배차추천경로Service(p.GetRequiredService<SsalddelContext>(),
            p.GetRequiredService<IDriverWorkQueueStore>(), DispatchProxy.Create<IGeocodingService, NoExternal>(),
            DispatchProxy.Create<INaverCloudDirectionsService, NoExternal>(), Microsoft.Extensions.Options.Options.Create(new NaverCloudDirectionsOptions()),
            NullLogger<배차추천경로Service>.Instance));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new 배차큐정책Options()));
        services.AddScoped<음식배달배차업무정책>(p => new(p.GetRequiredService<I국내화물운송기사상태Store>(),
            p.GetRequiredService<IDriverRejectedRequestStore>(), p.GetRequiredService<I배차추천경로Service>(), host.Scopes,
            DispatchProxy.Create<ISsalddelFoodOrderStore, ReadOnlyFoodOrders>(), p.GetRequiredService<IOptions<배차큐정책Options>>(), host.ReceivingState));
        services.AddScoped<I배차추천후보선정Service, Candidates>();
        services.AddScoped<I음식배달배차흐름Resolver, 음식배달배차흐름Resolver>();
        services.AddScoped<I배차대기원장전환Service, 배차대기원장전환Service>();
        host.Services = services.BuildServiceProvider();
        host.Worker = new(host.Services.GetRequiredService<IServiceScopeFactory>(), host.Options, host.Guard, host.Fixtures,
            NullLogger<모바일음식배차시험작업자>.Instance);
        await using (var db = host.CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            foreach (var definition in new[]
            {
                (DriverId, 모바일음식배차시험자료.DriverUserName, 역할명.기사),
                (OrdererId, 모바일음식배차시험자료.OrdererUserName, 역할명.커뮤니티회원),
                (RestaurantUserId, 모바일음식배차시험자료.RestaurantUserName, 역할명.음식점)
            })
            {
                db.Users.Add(new() { Id = definition.Item1, UserName = definition.Item2, NormalizedUserName = definition.Item2.ToUpperInvariant() });
                db.Roles.Add(new IdentityRole { Id = definition.Item3, Name = definition.Item3, NormalizedName = definition.Item3 });
                db.UserRoles.Add(new() { UserId = definition.Item1, RoleId = definition.Item3 });
            }
            db.UserClaims.Add(new() { UserId = RestaurantUserId, ClaimType = 음식점접근ClaimTypes.음식점Id, ClaimValue = "101" });
            db.용달기사.Add(new 용달기사 { 기사Id = DriverId, 상태 = "활동중", 차량 = "오토바이" });
            await db.SaveChangesAsync();
        }
        if (prepare) await host.PrepareAsync();
        return host;
    }

    public SsalddelContext CreateContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseSqlite(_connection).Options, new Encryption());
    public async Task PrepareAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<모바일음식배차시험준비Service>().PrepareAsync(CancellationToken.None);
    }
    public async Task RefreshAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<모바일음식배차시험준비Service>().RefreshLocationAsync(CancellationToken.None);
    }
    public async Task SeedAsync(params 운송원장[] queues)
    {
        await using var db = CreateContext();
        db.운송원장.AddRange(queues);
        foreach (var queue in queues)
            db.음식주문.Add(new 음식주문
            {
                주문번호 = queue.원본의뢰Id, 음식점Id = 101, 주문자UserId = OrdererId,
                음식점명 = 모바일음식배차시험자료.RestaurantName, 음식점주소 = 모바일음식배차시험자료.PickupAddress,
                음식점위도 = 모바일음식배차시험자료.ConfiguredRoute.PickupLatitude,
                음식점경도 = 모바일음식배차시험자료.ConfiguredRoute.PickupLongitude,
                수령지주소 = 모바일음식배차시험자료.DropoffAddress, 상태 = 음식주문상태코드.주문확인
            });
        await db.SaveChangesAsync();
    }
    public async Task<운송원장> ReadAsync(string id)
    {
        await using var db = CreateContext();
        return await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id);
    }
    public static 운송원장 Queue(string id = "fixture-order") => new()
    {
        운송번호 = id, 의뢰Id = id, 원본의뢰Id = id, 화주Id = "restaurant:101",
        원본의뢰유형 = 운송의뢰배차원천유형.음식점주문, 배차업무유형 = 상태값.배차업무유형.음식배달,
        상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.계획배차,
        배차노출상태 = 상태값.배차노출상태.계획대기, CreatedAt = DateTime.UtcNow.AddMinutes(-1), UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
        픽업_도로명주소 = 모바일음식배차시험자료.PickupAddress, 하차_도로명주소 = 모바일음식배차시험자료.DropoffAddress,
        픽업_위도 = 모바일음식배차시험자료.ConfiguredRoute.PickupLatitude, 픽업_경도 = 모바일음식배차시험자료.ConfiguredRoute.PickupLongitude,
        하차_위도 = 모바일음식배차시험자료.ConfiguredRoute.DropoffLatitude, 하차_경도 = 모바일음식배차시험자료.ConfiguredRoute.DropoffLongitude
    };
    public async ValueTask DisposeAsync()
    {
        Worker.Dispose(); await Services.DisposeAsync(); await _connection.DisposeAsync();
    }
    private sealed class Candidates(SsalddelContext db, 음식배달배차업무정책 policy) : I배차추천후보선정Service
    {
        public async Task<배차추천후보선정결과> 다음후보선정Async(string id, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id, cancellationToken);
            var candidate = await policy.다음후보선정Async(queue, 제외기사Id, cancellationToken);
            return candidate is null ? 배차추천후보선정결과.적격후보없음("현재 적격 기사 없음") : 배차추천후보선정결과.선정됨(candidate);
        }
    }
    public class NoExternal : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => throw new InvalidOperationException("시험이 외부 API를 호출했습니다: " + method?.Name);
    }
    public class ReadOnlyFoodOrders : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetOrder" ? null : throw new InvalidOperationException("시험이 음식 상태를 변경했습니다.");
    }
    public sealed class Receiving : I운영배차공통UseCase
    {
        public 운영배차수신상태Dto Current { get; set; } = new();
        public int IntentChanges { get; private set; }
        public Task<운영배차수신상태Dto> 수신상태조회Async(string id, CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
        {
            IntentChanges++; Current.수신의사Code = request.수신의사Code; Current.수신의사변경시각Utc = DateTimeOffset.UtcNow;
            return Task.FromResult(Current);
        }
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid request, string code, string reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto item, CancellationToken ct = default) => throw new NotSupportedException();
    }
    public sealed class Notifications : I배차추천알림Service
    {
        public List<string> Created { get; } = [];
        public Task 추천알림요청생성Async(long queueId, string id, string driverId, int round, CancellationToken cancellationToken = default)
        { Created.Add(id); return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Staging";
        public string ApplicationName { get; set; } = "MobileFoodDispatchTest";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
