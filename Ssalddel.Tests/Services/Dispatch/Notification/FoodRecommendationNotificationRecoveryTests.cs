using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Notification;

public sealed class FoodRecommendationNotificationRecoveryTests
{
    [Fact]
    public async Task PersistedRecommendationMissingOutbox_IsRepairedAfterServiceRecreationWithoutChangingOffer()
    {
        await using var f = await Fixture.CreateAsync();
        await using (var db = f.Context()) Assert.Empty(await db.배차추천알림Outbox.ToListAsync());
        Assert.Equal(1, await f.RunAsync());
        var row = await f.RowAsync();
        Assert.Equal("Succeeded", row.발송상태);
        Assert.Equal(1, row.추천라운드);
        Assert.Equal(1, row.시도횟수);
        await using var read = f.Context();
        var queue = await read.운송원장.SingleAsync();
        Assert.Equal(f.Expires, queue.추천만료시각);
        Assert.Equal("driver", queue.현재추천대상기사Id);
        Assert.Equal(0, await f.RunAsync());
        Assert.Single(f.Push.Messages);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("transport")]
    [InlineData("exception")]
    public async Task TransientFailure_UsesSameOutboxWithBackoffThenRecovers(string failure)
    {
        await using var f = await Fixture.CreateAsync();
        f.Tokens.Token = failure == "token" ? null : "token";
        f.Push.Succeeds = failure != "transport";
        f.Push.Throws = failure == "exception";
        Assert.Equal(1, await f.RunAsync());
        var failed = await f.RowAsync();
        Assert.Equal("Failed", failed.발송상태);
        Assert.False(FoodRecommendationNotificationRecoveryPolicy.RequiresOperatorReview(failed, f.Clock.Now));
        f.Tokens.Token = "token"; f.Push.Succeeds = true; f.Push.Throws = false;
        Assert.Equal(0, await f.RunAsync());
        f.Clock.Advance(5);
        Assert.Equal(1, await f.RunAsync());
        var recovered = await f.RowAsync();
        Assert.Equal(failed.Id, recovered.Id);
        Assert.Equal(2, recovered.시도횟수);
        Assert.Equal("Succeeded", recovered.발송상태);
        Assert.Equal(0, await f.RunAsync());
    }

    [Fact]
    public async Task DefaultSixtySecondOffer_ExpiresBeforeFiveRetriesWithoutExtendingDispatchDeadline()
    {
        await using var f = await Fixture.CreateAsync(); f.Push.Succeeds = false;
        await using (var db = f.Context())
        {
            var q = await db.운송원장.SingleAsync(); q.추천만료시각 = f.Clock.Start.AddSeconds(60);
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, await f.RunAsync()); f.Clock.Advance(15);
        Assert.Equal(1, await f.RunAsync()); f.Clock.Advance(15);
        Assert.Equal(1, await f.RunAsync()); f.Clock.Advance(15);
        Assert.Equal(0, await f.RunAsync()); f.Clock.Advance(15);
        Assert.Equal(1, await f.RunAsync());
        var row = await f.RowAsync();
        Assert.Equal("Obsolete", row.발송상태);
        Assert.False(FoodRecommendationNotificationRecoveryPolicy.RequiresOperatorReview(row, f.Clock.Now));
        Assert.Equal(3, f.Push.Messages.Count);
        await using var read = f.Context();
        Assert.Equal(f.Clock.Start.AddSeconds(60), (await read.운송원장.SingleAsync()).추천만료시각);
    }

    [Fact]
    public async Task RepeatedFailure_StopsAtFiveAttemptsAndRequiresReview()
    {
        await using var f = await Fixture.CreateAsync(); f.Push.Succeeds = false;
        for (var i = 0; i < 5; i++) { Assert.Equal(1, await f.RunAsync()); f.Clock.Advance(31); }
        Assert.Equal(0, await f.RunAsync());
        var exhausted = await f.RowAsync();
        Assert.Equal(5, exhausted.시도횟수);
        Assert.Equal("Failed", exhausted.발송상태);
        Assert.True(FoodRecommendationNotificationRecoveryPolicy.RequiresOperatorReview(exhausted, f.Clock.Now));
        Assert.Equal(5, f.Push.Messages.Count);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("accepted")]
    [InlineData("reassigned")]
    [InlineData("round")]
    public async Task FailedOutbox_CannotAnnounceAnObsoleteRecommendation(string change)
    {
        await using var f = await Fixture.CreateAsync(); f.Push.Succeeds = false;
        await f.RunAsync(); f.Clock.Advance(6); f.Push.Succeeds = true;
        await using (var db = f.Context())
        {
            var q = await db.운송원장.SingleAsync();
            switch (change)
            {
                case "expired": q.추천만료시각 = f.Clock.Now.AddSeconds(-1); break;
                case "accepted": q.배차노출상태 = 상태값.배차노출상태.확정; break;
                case "reassigned": q.현재추천대상기사Id = "other"; break;
                case "round": q.추천라운드++; break;
            }
            // A new valid recommendation may get its own Outbox. Prevent that separate effect in this test.
            if (change is "round" or "reassigned") q.추천만료시각 = f.Clock.Now.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, await f.RunAsync());
        Assert.Single(f.Push.Messages);
        Assert.Equal("Obsolete", (await f.RowAsync()).발송상태);
    }

    [Fact]
    public async Task QueueChangesDuringTokenLookup_IsRecheckedBeforeSending()
    {
        await using var f = await Fixture.CreateAsync();
        f.Tokens.BeforeRead = async () =>
        {
            await using var db = f.Context(); var q = await db.운송원장.SingleAsync();
            q.배차노출상태 = 상태값.배차노출상태.확정; await db.SaveChangesAsync();
        };
        await f.RunAsync();
        Assert.Empty(f.Push.Messages);
        Assert.Equal("Obsolete", (await f.RowAsync()).발송상태);
    }

    [Fact]
    public async Task SendingLease_SurvivesWorkerInterruptionAndDoesNotAllowParallelSend()
    {
        await using var f = await Fixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Push.BeforeSend = async () => { entered.SetResult(); await release.Task; };
        var first = f.RunAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, await f.RunAsync());
        release.SetResult(); await first;
        Assert.Single(f.Push.Messages);
        // Model a committed claim whose process died before final status write.
        await using (var db = f.Context())
        {
            var row = await db.배차추천알림Outbox.SingleAsync();
            row.발송상태 = "Sending"; row.UpdatedAt = f.Clock.Now; await db.SaveChangesAsync();
        }
        f.Push.BeforeSend = null;
        Assert.Equal(0, await f.RunAsync());
        f.Clock.Advance(61);
        Assert.Equal(1, await f.RunAsync());
        Assert.Equal("Succeeded", (await f.RowAsync()).발송상태);
        Assert.Equal(2, f.Push.Messages.Count); // At-least-once; client re-queries/deduplicates.
    }

    [Fact]
    public async Task TwoWorkersReadingSamePendingRow_OnlyOneConditionalClaimWins()
    {
        await using var f = await Fixture.CreateAsync();
        await using (var db = f.Context())
            await new 배차추천알림Service(db, f.Tokens, f.Push, NullLogger<배차추천알림Service>.Instance, f.Clock)
                .추천알림요청생성Async(1, "offer", "driver", 1);
        f.Barrier = new ClaimBarrier();
        var results = await Task.WhenAll(f.RunAsync(), f.RunAsync());
        Assert.Equal(2, f.Barrier.Claimants);
        Assert.Equal(1, results.Sum());
        Assert.Single(f.Push.Messages);
        Assert.Equal(1, (await f.RowAsync()).시도횟수);
        Assert.Equal("Succeeded", (await f.RowAsync()).발송상태);
    }

    [Fact]
    public async Task ConcurrentMissingOutboxRepair_ReusesTheUniqueKeyAndSendsOnce()
    {
        await using var f = await Fixture.CreateAsync();
        var results = await Task.WhenAll(f.RunAsync(), f.RunAsync());
        Assert.Equal(1, results.Sum());
        Assert.Single(f.Push.Messages);
        Assert.Equal("Succeeded", (await f.RowAsync()).발송상태);
    }

    [Fact]
    public async Task ReadingTheNotificationDuringSend_DoesNotInvalidateTheClaimOrExtendItsLease()
    {
        await using var f = await Fixture.CreateAsync();
        f.Push.BeforeSend = async () =>
        {
            await using var db = f.Context();
            var row = await db.배차추천알림Outbox.SingleAsync();
            row.읽은시각 = f.Clock.Now.AddSeconds(1); row.UpdatedAt = f.Clock.Now.AddSeconds(1);
            await db.SaveChangesAsync();
        };
        Assert.Equal(1, await f.RunAsync());
        var row = await f.RowAsync();
        Assert.NotNull(row.읽은시각);
        Assert.Equal("Succeeded", row.발송상태);
        f.Clock.Advance(61);
        Assert.Equal(0, await f.RunAsync());
        Assert.Single(f.Push.Messages);
    }

    [Fact]
    public async Task CancelledWorker_LeavesDurableClaimForLeaseRecovery()
    {
        await using var f = await Fixture.CreateAsync(); using var cancellation = new CancellationTokenSource();
        f.Push.BeforeSend = () => { cancellation.Cancel(); return Task.FromCanceled(cancellation.Token); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.RunAsync(cancellation.Token));
        Assert.Equal("Sending", (await f.RowAsync()).발송상태);
        f.Clock.Advance(61); f.Push.BeforeSend = null;
        Assert.Equal(1, await f.RunAsync());
        Assert.Equal("Succeeded", (await f.RowAsync()).발송상태);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "r54-push-" + Guid.NewGuid().ToString("N") + ".db");
        public Clock Clock { get; } = new();
        public Tokens Tokens { get; } = new();
        public Push Push { get; } = new();
        public ClaimBarrier? Barrier;
        public DateTime Expires => Clock.Start.AddMinutes(10);
        public SsalddelContext Context()
        {
            var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite("Data Source=" + _path + ";Pooling=False");
            if (Barrier is not null) options.AddInterceptors(Barrier);
            return new(options.Options, new Encryption());
        }
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); await using var db = f.Context(); await db.Database.EnsureCreatedAsync();
            db.운송원장.Add(new 운송원장 { Id = 1, 운송번호 = "r54-food", 의뢰Id = "offer", 배차업무유형 = 상태값.배차업무유형.음식배달,
                상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = "driver", 추천라운드 = 1, 추천시작시각 = f.Clock.Now, 추천만료시각 = f.Expires });
            await db.SaveChangesAsync(); return f;
        }
        public async Task<int> RunAsync(CancellationToken ct = default)
        {
            await using var db = Context();
            return await new 배차추천알림Service(db, Tokens, Push, NullLogger<배차추천알림Service>.Instance, Clock,
                notificationSettings: new Ssalddel.Tests.Support.ExplicitOptInNotificationSettingsStore())
                .업무유형별대기알림발송Async(상태값.배차업무유형.음식배달, cancellationToken: ct);
        }
        public async Task<배차추천알림Outbox> RowAsync() { await using var db = Context(); return await db.배차추천알림Outbox.SingleAsync(); }
        public ValueTask DisposeAsync() { if (File.Exists(_path)) File.Delete(_path); return ValueTask.CompletedTask; }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Start { get; } = DateTime.UtcNow;
        public DateTime Now { get; private set; }
        public Clock() => Now = Start;
        public void Advance(int seconds) => Now = Now.AddSeconds(seconds);
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
    private sealed class ClaimBarrier : DbCommandInterceptor
    {
        private int _entered;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Claimants => Math.Min(_entered, 2);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(x => Equals(x.Value, "Sending")))
            {
                var entered = Interlocked.Increment(ref _entered);
                if (entered == 2) _both.TrySetResult();
                if (entered <= 2) await _both.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
    private sealed class Tokens : IDriverPushTokenStore
    {
        public string? Token = "token"; public Func<Task>? BeforeRead;
        public Task SetAsync(string driver, string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(string driver, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string driver, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public async Task<string?> GetForAppAsync(string driver, string appKey, CancellationToken cancellationToken = default)
        { Assert.Equal("FoodDeliveryDriverApp", appKey); if (BeforeRead is not null) await BeforeRead(); return Token; }
    }
    private sealed class Push : IFcmPushService
    {
        public bool Succeeds = true; public bool Throws; public Func<Task>? BeforeSend;
        public List<FcmPushMessage> Messages { get; } = [];
        public async Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default)
        { Messages.Add(message); if (BeforeSend is not null) await BeforeSend(); if (Throws) throw new IOException("synthetic"); return Succeeds; }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Cargo path must not be used.");
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
