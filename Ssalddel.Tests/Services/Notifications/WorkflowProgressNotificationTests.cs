using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;

namespace Ssalddel.Tests.Services.Notifications;

public sealed class WorkflowProgressNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero);
    public static IEnumerable<object[]> Stages => WorkflowProgressNotification.All.Select(x => new object[] { x.Code });

    [Theory, MemberData(nameof(Stages))]
    public async Task 모든단계는_최소정보로예약되고_같은수신자발생은재저장하지않는다(string stage)
    {
        await using var db = Db();
        var row = 업무진행알림Recorder.Reserve(db, stage, "source-1", "revision-1", "owner-a",
            "/shipper/warehouse/inventory", Now.UtcDateTime)!;
        Assert.NotNull(row);
        Assert.Null(업무진행알림Recorder.Reserve(db, stage, "source-1", "revision-1", "owner-a",
            "/shipper/warehouse/inventory", Now.UtcDateTime));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Null(업무진행알림Recorder.Reserve(db, stage, "source-1", "revision-1", "owner-a",
            "/shipper/warehouse/inventory", Now.UtcDateTime));
        Assert.Single(db.Command알림Outbox);
        using var json = JsonDocument.Parse(row.PayloadJson);
        var keys = json.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(keys, key => key.Contains("address", StringComparison.OrdinalIgnoreCase)
            || key.Contains("phone", StringComparison.OrdinalIgnoreCase) || key.Contains("photo", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Succeeded", row.Status);
    }

    [Fact]
    public async Task 다른발생과다른수신자는별도로예약한다()
    {
        await using var db = Db();
        foreach (var (occurrence, user) in new[] { ("1", "a"), ("2", "a"), ("1", "b") })
            업무진행알림Recorder.Reserve(db, "warehouse.picked", "task", occurrence, user,
                "/work/outbound/packing", Now.UtcDateTime);
        await db.SaveChangesAsync(); Assert.Equal(3, await db.Command알림Outbox.CountAsync());
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("//example.com/")]
    [InlineData("/work/../admin")]
    [InlineData("/work/%2e%2e/admin")]
    [InlineData("/work/%5cadmin")]
    [InlineData("/work/task?returnUrl=https%3A%2F%2Fexample.com")]
    [InlineData("/admin/users")]
    [InlineData("/workspace/not-a-role")]
    public void 알림에서임의외부또는지원하지않는경로를열지않는다(string route)
    {
        var data = Data(); data["deepLink"] = route;
        Assert.False(WorkflowProgressNotification.TryParse(data, out _));
    }

    [Theory]
    [InlineData("type", "FoodDeliveryRecommendation")]
    [InlineData("stageCode", "cargo.fake")]
    [InlineData("workflowKey", "food")]
    [InlineData("eventId", "not-an-id")]
    [InlineData("sourceId", "")]
    [InlineData("userId", "")]
    [InlineData("occurredAtUtc", "unknown")]
    public void 불완전하거나다른종류의알림은거부한다(string key, string value)
    {
        var data = Data(); data[key] = value;
        Assert.False(WorkflowProgressNotification.TryParse(data, out _));
    }

    [Theory]
    [InlineData("/workspace/food-driver?selected=food%3Aorder-1", true)]
    [InlineData("/workspace/orderer?selected=order-1", true)]
    [InlineData("/workspace/orderer?returnUrl=https%3A%2F%2Fexample.com", false)]
    [InlineData("/workspace/orderer?selected=a%26returnUrl%3Dhttps%3A%2F%2Fexample.com", false)]
    [InlineData("/workspace/orderer?selected=a%252Fadmin", false)]
    public void 업무원천식별자의콜론은보존하고_추가이동인자는거부한다(string route, bool expected)
        => Assert.Equal(expected, WorkflowProgressNotification.IsSafeRoute(route));

    [Fact]
    public async Task 같은계정설치에서중복과늦은이전단계를거르고_클릭할때만이동한다()
    {
        var store = new Store(); var published = new List<WorkflowProgressHint>();
        var session = new MemberDeliveryPushSession("owner-a", true, 1);
        var coordinator = Coordinator(store, () => session, hint => { published.Add(hint); return true; });
        Assert.True(await coordinator.ReceiveAsync(Data()));
        Assert.True(await coordinator.ReceiveAsync(Data()));
        Assert.Single(published); Assert.Null(await coordinator.TakeOpenTargetAsync());
        var old = Data("cargo.picked-up", Now.AddMinutes(-1));
        Assert.False(await coordinator.ReceiveAsync(old));
        Assert.True(await coordinator.QueueOpenTargetAsync(Data()));
        Assert.Equal("/shipper/request/req-1", (await coordinator.TakeOpenTargetAsync())!.Route);
        Assert.Null(await coordinator.TakeOpenTargetAsync());
        await coordinator.QueueOpenTargetAsync(Data());
        session = new("owner-b", true, 2);
        Assert.Null(await coordinator.TakeOpenTargetAsync());
        Assert.False(await coordinator.ReceiveAsync(Data()));
    }

    [Theory]
    [InlineData(false, "owner-a", true)]
    [InlineData(true, "owner-b", true)]
    [InlineData(true, "owner-a", false)]
    public async Task 로그아웃_설치계정불일치_철회대기에서는표시하지않는다(bool loggedIn, string registered, bool revocation)
    {
        var store = new Store { State = new("installation") { RegisteredOwnerId = registered, RevocationPending = !revocation } };
        var c = Coordinator(store, () => new("owner-a", loggedIn, 1), _ => throw new Exception("must not publish"));
        Assert.False(await c.ReceiveAsync(Data()));
    }

    [Fact]
    public async Task 세션이복원도중바뀌면표시하지않으며_운송제안거절설정과별도로진행을받는다()
    {
        var store = new Store(); var current = new MemberDeliveryPushSession("owner-a", true, 1);
        var c = new WorkflowProgressPushCoordinator(store, () => current,
            _ => { current = new("owner-b", true, 2); return Task.CompletedTask; }, (_, _) => throw new Exception(), new Clock(Now));
        Assert.False(await c.ReceiveAsync(Data()));
        var posted = 0;
        c = Coordinator(store, () => new("owner-a", true, 1), _ => { posted++; return true; });
        Assert.False(store.State.Preferences.Enabled); // 배송 제안 참여 OFF는 업무 진행과 다른 목적이다.
        Assert.True(await c.ReceiveAsync(Data())); Assert.Equal(1, posted);
    }

    [Theory]
    [InlineData(-8 * 24 * 60)]
    [InlineData(6)]
    public async Task 너무오래되거나미래시각인알림은표시하지않는다(int minutes)
    {
        var c = Coordinator(new Store(), () => new("owner-a", true, 1), _ => throw new Exception());
        Assert.False(await c.ReceiveAsync(Data(at: Now.AddMinutes(minutes))));
    }

    [Fact]
    public async Task 기기표시실패는완료로저장하지않고_다시시도할수있다()
    {
        var store = new Store(); var attempts = 0;
        var c = Coordinator(store, () => new("owner-a", true, 1), _ => ++attempts > 1);
        Assert.False(await c.ReceiveAsync(Data())); Assert.Empty(store.State.ProgressPublished);
        Assert.True(await c.ReceiveAsync(Data())); Assert.Single(store.State.ProgressPublished);
        await c.ClearAccountAsync("owner-a"); Assert.Empty(store.State.ProgressPublished);
    }

    [Fact]
    public async Task 로그인전대상은등록계정에묶어보존하고_같은계정로그인후한번만소비한다()
    {
        var store = new Store(); var current = new MemberDeliveryPushSession(null, false, 1);
        var c = Coordinator(store, () => current, _ => true);
        Assert.True(await c.QueueOpenTargetAsync(Data()));
        Assert.Null(await c.TakeOpenTargetAsync());
        Assert.Equal("/shipper/request/req-1", await c.SignInTargetAsync());
        current = new("owner-a", true, 2);
        Assert.NotNull(await c.TakeOpenTargetAsync()); Assert.Null(await c.SignInTargetAsync());
        Assert.Null(await c.TakeOpenTargetAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 기존소리진동설정을존중하고_확인된설정이없으면조용히표시한다(bool hasPreferences)
    {
        var store = new Store();
        if (hasPreferences) store.State = store.State with { PreferencesOwnerId = "owner-a", Preferences = new(true, false, false) };
        NotificationAlertPlan? received = null;
        var c = new WorkflowProgressPushCoordinator(store, () => new("owner-a", true, 1), _ => Task.CompletedTask,
            (_, plan) => { received = plan; return true; }, new Clock(Now));
        Assert.True(await c.ReceiveAsync(Data())); Assert.NotNull(received);
        Assert.True(received.Enabled); Assert.False(received.Sound); Assert.False(received.Vibrate);
    }

    [Fact]
    public async Task 기존발송기가단계정보와정적문구를전달하고_통합앱만data전용으로보낸다()
    {
        await using var db = Db(); var fcm = new Fcm();
        업무진행알림Recorder.Reserve(db, "cargo.delivered", "req-1", "1", "owner-a", "/shipper/request/req-1", Now.UtcDateTime);
        db.SsalddelMobilePushInstallations.AddRange(
            new() { UserId = "owner-a", AppKey = "ssalddel.shipper", PushToken = "integrated", InstallationId = "i", Platform = "Android" },
            new() { UserId = "owner-a", AppKey = "legacy", PushToken = "legacy", InstallationId = "l", Platform = "Android" });
        await db.SaveChangesAsync();
        var sender = Sender(db, fcm);
        Assert.Equal(1, await sender.대기알림발송Async()); Assert.Equal(2, fcm.Messages.Count);
        Assert.True(fcm.Messages.Single(x => x.Token == "integrated").DataOnly);
        Assert.False(fcm.Messages.Single(x => x.Token == "legacy").DataOnly);
        Assert.All(fcm.Messages, message =>
        {
            Assert.Equal("전달 완료", message.Title);
            Assert.True(WorkflowProgressNotification.TryParse(message.Data, out _));
            Assert.DoesNotContain("driverId", message.Data.Keys);
        });
        Assert.Equal("Succeeded", (await db.Command알림Outbox.SingleAsync()).Status);
        Assert.Equal(0, await sender.대기알림발송Async()); // 전송 접수이며 사용자 수령/지급 확인이 아님.
    }

    [Fact]
    public async Task 설정미완료발송은대기로남고_잘못된단계payload는전송하지않는다()
    {
        await using var db = Db(); var fcm = new Fcm { Result = false };
        var row = 업무진행알림Recorder.Reserve(db, "food.ready", "o-1", "1", "owner-a", "/workspace/orderer?selected=o-1", Now.UtcDateTime)!;
        await db.SaveChangesAsync();
        Assert.Equal(1, await Sender(db, fcm).대기알림발송Async()); Assert.Equal("Pending", row.Status);
        row.PayloadJson = row.PayloadJson.Replace("food.ready", "food.invalid"); row.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync(); fcm.Messages.Clear();
        Assert.Equal(1, await Sender(db, fcm).대기알림발송Async()); Assert.Equal("Failed", row.Status); Assert.Empty(fcm.Messages);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task 기존화물알림톡설정은보존하고_연락처는현재정본에서만읽는다(bool enabled, bool ownerChanged)
    {
        await using var db = Db(); var fcm = new Fcm(); var kakao = new Kakao();
        var source = new 살뜰.도메인.화주.화주운송의뢰
        {
            의뢰Id = "kakao-source", 주문자UserId = "owner-a", 화주Id = "business", 화물종류 = "물품",
            픽업_연락처_전화번호 = "private-phone", 픽업_도로명주소 = "private-pickup", 하차_도로명주소 = "private-dropoff"
        };
        db.화주운송의뢰.Add(source);
        var row = CargoWorkflowProgressNotifications.Create("cargo.accepted", source, null, "driver", Now.UtcDateTime);
        Assert.DoesNotContain("private", row.PayloadJson);
        db.Command알림Outbox.Add(row); if (ownerChanged) source.주문자UserId = "owner-b";
        await db.SaveChangesAsync();
        var sender = new Command알림Outbox발송Service(db, new Tokens(), fcm, kakao,
            Options.Create(new KakaoAlimTalkOptions { Enabled = enabled }), NullLogger<Command알림Outbox발송Service>.Instance);
        Assert.Equal(1, await sender.대기알림발송Async());
        if (enabled && !ownerChanged)
        {
            var message = Assert.Single(kakao.Messages);
            Assert.Equal("private-phone", message.RecipientPhoneNumber);
            Assert.Equal("private-pickup", message.Variables["pickupAddress"]);
            Assert.Equal("driver", message.Variables["driverId"]);
        }
        else Assert.Empty(kakao.Messages);
        Assert.Equal(enabled && ownerChanged ? "Pending" : "Succeeded", row.Status);
        Assert.DoesNotContain("private", row.PayloadJson);
        var pushed = Assert.Single(fcm.Messages);
        Assert.DoesNotContain("driverId", pushed.Data.Keys);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(pushed.Data));
    }

    private static Dictionary<string, string> Data(string stage = "cargo.delivered", DateTimeOffset? at = null) => new()
    {
        ["type"] = WorkflowProgressNotification.MessageType, ["eventId"] = 업무진행알림Recorder.EventId(stage, "req-1", "1", "owner-a"),
        ["workflowKey"] = "cargo", ["stageCode"] = stage, ["sourceId"] = "req-1", ["userId"] = "owner-a",
        ["occurredAtUtc"] = (at ?? Now).ToString("O"), ["deepLink"] = "/shipper/request/req-1"
    };
    private static WorkflowProgressPushCoordinator Coordinator(Store store, Func<MemberDeliveryPushSession> session,
        Func<WorkflowProgressHint, bool> publish) => new(store, session, _ => Task.CompletedTask,
            (hint, _) => publish(hint), new Clock(Now));
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Store : IMemberDeliveryPushStateStore
    {
        public MemberDeliveryPushState State { get; set; } = new("installation") { RegisteredOwnerId = "owner-a" };
        public Task<MemberDeliveryPushState> LoadAsync(CancellationToken ct = default) => Task.FromResult(State);
        public Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update, CancellationToken ct = default)
        { State = update(State); return Task.CompletedTask; }
    }
    private static SsalddelContext Db() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new DummyPersonalDataEncryptionService());
    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private static Command알림Outbox발송Service Sender(SsalddelContext db, Fcm fcm) => new(db, new Tokens(), fcm,
        new Kakao(), Options.Create(new KakaoAlimTalkOptions()), NullLogger<Command알림Outbox발송Service>.Instance);
    private sealed class Tokens : I사용자PushTokenStore
    {
        public Task<string?> GetAsync(string userId, CancellationToken ct = default) => Task.FromResult<string?>("fallback");
        public Task SetAsync(string userId, string token, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Kakao : IKakaoAlimTalkService
    {
        public List<KakaoAlimTalkMessage> Messages { get; } = [];
        public Task<bool> SendAsync(KakaoAlimTalkMessage message, CancellationToken ct = default)
        { Messages.Add(message); return Task.FromResult(true); }
    }
    private sealed class Fcm : IFcmPushService
    {
        public List<FcmPushMessage> Messages { get; } = []; public bool Result { get; init; } = true;
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken ct = default)
        { Messages.Add(message); return Task.FromResult(Result); }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
            => SendAsync(new(token, title, body, data), ct);
    }
}
