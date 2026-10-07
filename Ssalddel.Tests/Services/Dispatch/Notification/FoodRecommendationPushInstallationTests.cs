using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Domain.Notifications;
using Ssalddel.Extensions;
using Ssalddel.Services.Notifications;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Notification;

public sealed class FoodRecommendationPushInstallationTests
{
    [Fact]
    public async Task ActiveInstallationRegistrationFeedsFoodPushWithoutUsingTheSameUsersCargoToken()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var db = fixture.Context;
        var registrations = new SsalddelMobilePushInstallationService(db);
        await registrations.UpsertAsync("same-driver", new("food-installation", 기사앱식별자.FoodDeliveryDriverApp,
            "Android", "food-token"), CancellationToken.None);
        await registrations.UpsertAsync("same-driver", new("cargo-installation", 기사앱식별자.CargoYongdalDriverApp,
            "Android", "installation-cargo-token"), CancellationToken.None);
        await registrations.UpsertAsync("other-driver", new("other-food", 기사앱식별자.FoodDeliveryDriverApp,
            "Android", "other-food-token"), CancellationToken.None);
        var legacy = new LegacyCargoTokens();
        await legacy.SetAsync("same-driver", "legacy-cargo-token");
        var tokens = new AppScopedDriverPushTokenStore(db, legacy);
        var service = CreateService(db, tokens, new RecordingPush());
        var queue = SeedQueue("same-driver");
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        await service.추천알림요청생성Async(queue.Id, queue.의뢰Id, "same-driver", 1);
        var push = new RecordingPush();
        service = CreateService(db, tokens, push);

        Assert.Equal(1, await service.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달));

        var message = Assert.Single(push.Messages);
        Assert.Equal("food-token", message.Token);
        Assert.True(message.DataOnly);
        Assert.True(message.HighPriority);
        Assert.Equal(string.Empty, message.Title);
        Assert.Equal(string.Empty, message.Body);
        Assert.Equal(new[] { "appKey", "expiresAtUtc", "offerId", "type", "userId" }, message.Data.Keys.OrderBy(x => x));
        Assert.Equal("FoodDeliveryRecommendation", message.Data["type"]);
        Assert.Equal(기사앱식별자.FoodDeliveryDriverApp, message.Data["appKey"]);
        Assert.Equal("same-driver", message.Data["userId"]);
        Assert.Equal(queue.의뢰Id, message.Data["offerId"]);
        Assert.Equal(queue.추천만료시각, DateTime.Parse(message.Data["expiresAtUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind));
        Assert.Equal(0, legacy.Reads);
        Assert.Equal(0, push.LegacyCalls);
        Assert.Equal("Succeeded", Assert.Single(db.배차추천알림Outbox).발송상태);

        await registrations.DeactivateAsync("same-driver", "food-installation", CancellationToken.None);
        Assert.Null(await tokens.GetForAppAsync("same-driver", 기사앱식별자.FoodDeliveryDriverApp));
        Assert.Equal(0, legacy.Reads);
        Assert.Equal("legacy-cargo-token", await tokens.GetForAppAsync("same-driver", 기사앱식별자.CargoYongdalDriverApp));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("other-user")]
    [InlineData("cargo-app")]
    [InlineData("unknown-app")]
    public async Task NonFoodOrInactiveInstallationsCannotReceiveFoodRecommendations(string mismatch)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var db = fixture.Context;
        db.SsalddelMobilePushInstallations.Add(new SsalddelMobilePushInstallation
        {
            InstallationId = "fixture", UserId = mismatch == "other-user" ? "other-driver" : "same-driver",
            AppKey = mismatch switch { "cargo-app" => 기사앱식별자.CargoYongdalDriverApp, "unknown-app" => "UnknownDriverApp", _ => 기사앱식별자.FoodDeliveryDriverApp },
            IsActive = mismatch != "inactive", PushToken = "wrong-token", Platform = "Android", PushTokenHash = "fixture-hash"
        });
        var queue = SeedQueue("same-driver");
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var legacy = new LegacyCargoTokens();
        await legacy.SetAsync("same-driver", "legacy-cargo-token");
        var push = new RecordingPush();
        var service = CreateService(db, new AppScopedDriverPushTokenStore(db, legacy), push);
        await service.추천알림요청생성Async(queue.Id, queue.의뢰Id, "same-driver", 1);

        Assert.Equal(1, await service.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달));

        Assert.Empty(push.Messages);
        Assert.Equal(0, legacy.Reads);
        Assert.Equal("Failed", Assert.Single(db.배차추천알림Outbox).발송상태);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("reassigned")]
    [InlineData("new-round")]
    [InlineData("accepted")]
    public async Task StaleOutboxCannotAnnounceAnExpiredOrChangedRecommendation(string change)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var db = fixture.Context;
        var queue = SeedQueue("same-driver");
        db.운송원장.Add(queue);
        db.SsalddelMobilePushInstallations.Add(new SsalddelMobilePushInstallation { InstallationId = "food", UserId = "same-driver",
            AppKey = 기사앱식별자.FoodDeliveryDriverApp, IsActive = true, PushToken = "food-token", PushTokenHash = "hash", Platform = "Android" });
        await db.SaveChangesAsync();
        var push = new RecordingPush();
        var service = CreateService(db, new AppScopedDriverPushTokenStore(db, new LegacyCargoTokens()), push);
        await service.추천알림요청생성Async(queue.Id, queue.의뢰Id, "same-driver", 1);
        switch (change)
        {
            case "expired": queue.추천만료시각 = DateTime.UtcNow.AddMinutes(-1); break;
            case "reassigned": queue.현재추천대상기사Id = "other-driver"; break;
            case "new-round": queue.추천라운드 = 2; break;
            case "accepted": queue.배차노출상태 = 상태값.배차노출상태.확정; break;
        }
        await db.SaveChangesAsync();

        await service.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달);

        Assert.Empty(push.Messages);
        Assert.Equal("Failed", Assert.Single(db.배차추천알림Outbox).발송상태);
    }

    [Fact]
    public async Task MemoryRegistrationKeepsTheCargoStoreAcrossScopesAndLoadsFoodInstallationFromTheScopedDatabase()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=fixture;User=fixture;Password=fixture",
            ["TransientState:Provider"] = "Memory", ["MongoDb:ConnectionString"] = "mongodb://127.0.0.1:1", ["MongoDb:Database"] = "fixture"
        }).Build();
        var services = new ServiceCollection();
        services.AddSsalddelPersistence(config);
        var dbOptions = new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        services.AddScoped(_ => new SsalddelContext(dbOptions, new PassThroughEncryption()));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var first = provider.CreateAsyncScope())
        {
            await first.ServiceProvider.GetRequiredService<IDriverPushTokenStore>().SetAsync("same-driver", "cargo-token");
            var db = first.ServiceProvider.GetRequiredService<SsalddelContext>();
            await new SsalddelMobilePushInstallationService(db).UpsertAsync("same-driver",
                new SsalddelMobilePushInstallationUpsertRequest("food", 기사앱식별자.FoodDeliveryDriverApp, "Android", "food-token"), CancellationToken.None);
        }
        await using var second = provider.CreateAsyncScope();
        var tokens = second.ServiceProvider.GetRequiredService<IDriverPushTokenStore>();
        Assert.Equal("cargo-token", await tokens.GetAsync("same-driver"));
        Assert.Equal("food-token", await tokens.GetForAppAsync("same-driver", 기사앱식별자.FoodDeliveryDriverApp));
        Assert.Null(await tokens.GetForAppAsync("same-driver", "UnknownDriverApp"));
    }

    private static 운송원장 SeedQueue(string driverId) => new()
    {
        Id = 1, 운송번호 = "food-fixture", 의뢰Id = "food-offer", 배차업무유형 = 상태값.배차업무유형.음식배달,
        상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
        현재추천대상기사Id = driverId, 추천라운드 = 1, 추천만료시각 = DateTime.UtcNow.AddMinutes(5)
    };
    private static 배차추천알림Service CreateService(SsalddelContext db, IDriverPushTokenStore tokens, IFcmPushService push)
        => new(db, tokens, push, NullLogger<배차추천알림Service>.Instance);

    private sealed class LegacyCargoTokens : IDriverPushTokenStore
    {
        private readonly Dictionary<string, string> _tokens = [];
        public int Reads { get; private set; }
        public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default) { _tokens[driverId] = pushToken; return Task.CompletedTask; }
        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default) { Reads++; return Task.FromResult(_tokens.GetValueOrDefault(driverId)); }
        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default) { _tokens.Remove(driverId); return Task.CompletedTask; }
    }
    private sealed class RecordingPush : IFcmPushService
    {
        public List<FcmPushMessage> Messages { get; } = [];
        public int LegacyCalls { get; private set; }
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.FromResult(true); }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
        { LegacyCalls++; return Task.FromResult(true); }
    }
    private sealed class DatabaseFixture(SqliteConnection connection, SsalddelContext context) : IAsyncDisposable
    {
        public SsalddelContext Context { get; } = context;
        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new PassThroughEncryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
