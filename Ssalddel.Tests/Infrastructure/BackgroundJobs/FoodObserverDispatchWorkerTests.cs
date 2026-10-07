using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Development.FoodObserver;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs;

public sealed class FoodObserverDispatchWorkerTests
{
    [Fact]
    public async Task 음식점이먼저인지한주문은_후보가생긴다음관찰스캔에서_음식기사에게추천한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(Queue("food", 상태값.배차큐단계.계획배차, 상태값.배차노출상태.계획대기));

        await fixture.Worker.한번실행Async(CancellationToken.None);

        var waiting = await fixture.ReadAsync("food");
        Assert.Equal(상태값.배차노출상태.추천후보없음, waiting.배차노출상태);
        Assert.Equal(상태값.배차큐단계.배차추천, waiting.배차큐단계);
        Assert.Null(waiting.현재추천대상기사Id);
        Assert.Null(waiting.공개전환시각);
        Assert.Empty(fixture.Notifications.Created);

        fixture.Candidates.NextDriver = "food-driver-now-on-duty";
        var count = await fixture.Worker.한번실행Async(CancellationToken.None);

        var recommended = await fixture.ReadAsync("food");
        Assert.Equal(1, count);
        Assert.Equal(상태값.배차노출상태.추천중, recommended.배차노출상태);
        Assert.Equal("food-driver-now-on-duty", recommended.현재추천대상기사Id);
        Assert.Equal(1, recommended.추천라운드);
        Assert.Null(recommended.공개전환시각);
        Assert.Equal(["food"], fixture.Notifications.Created);
        Assert.All(fixture.Candidates.Calls, id => Assert.Equal("food", id));
        Assert.Equal(["food-driver-now-on-duty"], fixture.FoodRecorder.Drivers);
    }

    [Fact]
    public async Task 관찰재탐색은_화물과이미추천중인음식큐를_변경하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cargo = Queue("cargo", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음);
        cargo.배차업무유형 = 상태값.배차업무유형.용달운송;
        cargo.원본의뢰유형 = "CargoTransport";
        var alreadyRecommended = Queue("active-food", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        alreadyRecommended.현재추천대상기사Id = "existing-driver";
        alreadyRecommended.추천만료시각 = DateTime.UtcNow.AddMinutes(10);
        var expiredCargo = Queue("expired-cargo", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        expiredCargo.배차업무유형 = 상태값.배차업무유형.용달운송;
        expiredCargo.원본의뢰유형 = "CargoTransport";
        expiredCargo.현재추천대상기사Id = "cargo-driver";
        expiredCargo.추천만료시각 = DateTime.UtcNow.AddMinutes(-1);
        await fixture.SeedAsync(cargo, alreadyRecommended, expiredCargo);
        fixture.Candidates.NextDriver = "new-food-driver";

        var count = await fixture.Worker.한번실행Async(CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(fixture.Candidates.Calls);
        Assert.Empty(fixture.Notifications.Created);
        Assert.Empty(fixture.FoodRecorder.Drivers);
        var savedCargo = await fixture.ReadAsync("cargo");
        Assert.Equal(상태값.배차업무유형.용달운송, savedCargo.배차업무유형);
        Assert.Equal(상태값.배차노출상태.추천후보없음, savedCargo.배차노출상태);
        Assert.Null(savedCargo.현재추천대상기사Id);
        Assert.Null(savedCargo.공개전환시각);
        var savedFood = await fixture.ReadAsync("active-food");
        Assert.Equal(상태값.배차노출상태.추천중, savedFood.배차노출상태);
        Assert.Equal("existing-driver", savedFood.현재추천대상기사Id);
        Assert.Equal(alreadyRecommended.추천만료시각, savedFood.추천만료시각);
        var savedExpiredCargo = await fixture.ReadAsync("expired-cargo");
        Assert.Equal(상태값.배차노출상태.추천중, savedExpiredCargo.배차노출상태);
        Assert.Equal("cargo-driver", savedExpiredCargo.현재추천대상기사Id);
        Assert.Equal(expiredCargo.추천만료시각, savedExpiredCargo.추천만료시각);
    }

    [Fact]
    public async Task 만료된음식추천은_기존만료전환으로_다음기사에게새차수로추천한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expired = Queue("expired-food", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        expired.현재추천대상기사Id = "expired-driver";
        expired.추천라운드 = 1;
        expired.추천시작시각 = DateTime.UtcNow.AddMinutes(-2);
        expired.추천만료시각 = DateTime.UtcNow.AddMinutes(-1);
        await fixture.SeedAsync(expired);
        fixture.Candidates.NextDriver = "next-food-driver";

        var count = await fixture.Worker.한번실행Async(CancellationToken.None);

        var saved = await fixture.ReadAsync("expired-food");
        Assert.Equal(1, count);
        Assert.Equal(상태값.배차큐단계.배차추천, saved.배차큐단계);
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
        Assert.Equal("next-food-driver", saved.현재추천대상기사Id);
        Assert.Equal(2, saved.추천라운드);
        Assert.True(saved.추천만료시각 > DateTime.UtcNow);
        Assert.Null(saved.공개전환시각);
        Assert.Equal(["expired-driver"], fixture.Candidates.ExcludedDrivers);
        Assert.Equal(["expired-food"], fixture.Notifications.Created);
        Assert.Equal(["next-food-driver"], fixture.FoodRecorder.Drivers);
    }

    [Fact]
    public async Task 만료후다른후보가없으면_다음관찰스캔에서_다시음식추천을생성한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expired = Queue("retry-food", 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천중);
        expired.현재추천대상기사Id = "returned-food-driver";
        expired.추천라운드 = 1;
        expired.추천만료시각 = DateTime.UtcNow.AddMinutes(-1);
        await fixture.SeedAsync(expired);

        await fixture.Worker.한번실행Async(CancellationToken.None);

        var waiting = await fixture.ReadAsync("retry-food");
        Assert.Equal(상태값.배차노출상태.추천후보없음, waiting.배차노출상태);
        Assert.Null(waiting.현재추천대상기사Id);
        Assert.Null(waiting.추천만료시각);
        Assert.Null(waiting.공개전환시각);
        Assert.Empty(fixture.Notifications.Created);

        fixture.Candidates.NextDriver = "returned-food-driver";
        await fixture.Worker.한번실행Async(CancellationToken.None);

        var saved = await fixture.ReadAsync("retry-food");
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
        Assert.Equal("returned-food-driver", saved.현재추천대상기사Id);
        Assert.Equal(2, saved.추천라운드);
        Assert.True(saved.추천만료시각 > DateTime.UtcNow);
        Assert.Null(saved.공개전환시각);
        Assert.Equal(["retry-food"], fixture.Notifications.Created);
        Assert.Equal(["returned-food-driver"], fixture.FoodRecorder.Drivers);
    }

    private static 운송원장 Queue(string id, int stage, int exposure) => new()
    {
        운송번호 = id, 의뢰Id = id, 원본의뢰Id = id, 화주Id = "orderer",
        배차업무유형 = 상태값.배차업무유형.음식배달,
        원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
        상태 = 상태값.배차대기상태.대기, 배차큐단계 = stage, 배차노출상태 = exposure,
        CreatedAt = DateTime.UtcNow.AddMinutes(-2), UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
    };

    private sealed class Fixture(SqliteConnection connection, ServiceProvider services,
        음식배달관찰검증Runner runner, 음식배달관찰배차작업자 worker,
        CandidateSelection candidates, Notifications notifications, FoodRecorder recorder) : IAsyncDisposable
    {
        public 음식배달관찰배차작업자 Worker => worker;
        public CandidateSelection Candidates => candidates;
        public Notifications Notifications => notifications;
        public FoodRecorder FoodRecorder => recorder;
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var candidates = new CandidateSelection();
            var notifications = new Notifications();
            var recorder = new FoodRecorder();
            var services = new ServiceCollection()
                .AddScoped(_ => new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                    .UseSqlite(connection).Options, new Encryption()))
                .AddScoped<I배차대기원장전환Service>(provider => new 배차대기원장전환Service(
                    provider.GetRequiredService<SsalddelContext>(), Options.Create(new 배차큐정책Options()),
                    candidates, notifications, DispatchProxy.Create<I국내화물운송기사상태Service, CargoStateProxy>(),
                    new 음식배달배차흐름Resolver(), 음식배달기사추천기록Service: recorder))
                .BuildServiceProvider();
            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<SsalddelContext>().Database.EnsureCreatedAsync();
            var scopes = services.GetRequiredService<IServiceScopeFactory>();
            var runner = new 음식배달관찰검증Runner(scopes, new NoHttp(), new 음식배달관찰검증Options(),
                NullLogger<음식배달관찰검증Runner>.Instance);
            var worker = new 음식배달관찰배차작업자(scopes, runner, NullLogger<음식배달관찰배차작업자>.Instance);
            return new(connection, services, runner, worker, candidates, notifications, recorder);
        }
        public async Task SeedAsync(params 운송원장[] queues)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            db.운송원장.AddRange(queues);
            await db.SaveChangesAsync();
        }
        public async Task<운송원장> ReadAsync(string id)
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<SsalddelContext>()
                .운송원장.AsNoTracking().SingleAsync(queue => queue.의뢰Id == id);
        }
        public async ValueTask DisposeAsync()
        {
            worker.Dispose(); runner.Dispose(); await services.DisposeAsync(); await connection.DisposeAsync();
        }
    }

    private sealed class CandidateSelection : I배차추천후보선정Service
    {
        public string? NextDriver { get; set; }
        public List<string> Calls { get; } = [];
        public List<string?> ExcludedDrivers { get; } = [];
        public Task<배차추천후보선정결과> 다음후보선정Async(string id, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add(id); ExcludedDrivers.Add(제외기사Id);
            return Task.FromResult(NextDriver is null || NextDriver == 제외기사Id ? 배차추천후보선정결과.적격후보없음("기사 운행 시작 전")
                : 배차추천후보선정결과.선정됨(new(NextDriver, 1m, "기사 운행 시작 후")));
        }
    }
    private sealed class Notifications : I배차추천알림Service
    {
        public List<string> Created { get; } = [];
        public Task 추천알림요청생성Async(long queueId, string id, string driverId, int round, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Created.Add(id); return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FoodRecorder : I음식배달기사추천기록Service
    {
        public List<string> Drivers { get; } = [];
        public Task 추천기록Async(string driverId, DateTime atUtc, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Drivers.Add(driverId); return Task.CompletedTask; }
    }
    public class CargoStateProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => throw new InvalidOperationException("음식 관찰 작업자가 화물 기사 상태에 접근했습니다: " + method?.Name);
    }
    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("이 회귀는 외부 HTTP를 사용하지 않습니다.");
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
