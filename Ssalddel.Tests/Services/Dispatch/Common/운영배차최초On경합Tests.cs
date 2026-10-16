using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Infrastructure.Storage.Memory;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Services.Dispatch.Common;

public sealed class 운영배차최초On경합Tests
{
    [Fact]
    public async Task 최초의사없는_조건부On은한번만저장하고_이후수동Off를재생으로바꾸지않는다()
    {
        await using var f = await Fixture.Create(); await using var db = f.Context();
        var service = f.Service(db);
        Assert.Null((await service.수신상태조회Async("driver")).수신의사판본Token);
        var initial = Initial(null);
        var on = await service.기사의사변경Async("driver", initial);
        Assert.Equal(운영배차수신의사Code.On, on.수신의사Code); Assert.NotNull(on.수신의사판본Token);
        await service.기사의사변경Async("driver", initial);
        Assert.Single(await db.운영배차활동사건.ToArrayAsync());
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        var off = await service.기사의사변경Async("driver", Manual(운영배차수신의사Code.Off));
        var replay = await service.기사의사변경Async("driver", initial);
        Assert.Equal(운영배차수신의사Code.Off, replay.수신의사Code);
        Assert.Equal(off.수신의사판본Token, replay.수신의사판본Token);
        Assert.Equal(2, await db.운영배차활동사건.CountAsync());
    }

    [Fact]
    public async Task 같은발생시각에낮은StableId로저장된수동Off도최신의사이며_오래된최초On을거부한다()
    {
        await using var f = await Fixture.Create(); await using var db = f.Context(); var service = f.Service(db);
        var onRequest = Manual(운영배차수신의사Code.On);
        onRequest.클라이언트요청Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var observed = await service.기사의사변경Async("driver", onRequest);
        var offRequest = Manual(운영배차수신의사Code.Off);
        offRequest.클라이언트요청Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var off = await service.기사의사변경Async("driver", offRequest);
        Assert.Equal(observed.수신의사변경시각Utc, off.수신의사변경시각Utc);
        Assert.Equal(운영배차수신의사Code.Off, off.수신의사Code);
        Assert.NotEqual(observed.수신의사판본Token, off.수신의사판본Token);
        var result = await service.기사의사변경Async("driver", Initial(observed.수신의사판본Token));
        Assert.Equal(운영배차수신의사Code.Off, result.수신의사Code);
        Assert.Equal(off.수신의사판본Token, result.수신의사판본Token);
        Assert.Equal(2, await db.운영배차활동사건.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 희망설정행없는_두Store의_수동Off와최초On은_같은DB경계에서비교한다(bool alreadyHasIntent)
    {
        var gate = new HoldManualOff();
        await using var f = await Fixture.Create(sqlite: true, gate);
        await using var seed = f.Context(); var service = f.Service(seed);
        if (alreadyHasIntent) await service.기사의사변경Async("driver", Manual(운영배차수신의사Code.Off));
        var observed = await service.수신상태조회Async("driver");
        Assert.Empty(await seed.Set<DriverWorkParticipationRecord>().ToArrayAsync());
        var initial = Initial(observed.수신의사판본Token);
        await using var manualDb = f.Context(); await using var initialDb = f.Context();
        gate.Arm();
        var manual = Task.Run(() => f.Service(manualDb).기사의사변경Async("driver", Manual(운영배차수신의사Code.Off)));
        Task<운영배차수신상태Dto>? conditional = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            conditional = Task.Run(async () =>
            {
                started.SetResult();
                return await f.Service(initialDb).기사의사변경Async("driver", initial);
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(conditional.IsCompleted);
        }
        finally { gate.Release.TrySetResult(); }
        await manual.WaitAsync(TimeSpan.FromSeconds(15));
        var result = await conditional!.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(운영배차수신의사Code.Off, result.수신의사Code);
        await using var canonical = f.Context();
        Assert.False(await canonical.운영배차활동사건.AnyAsync(x => x.사건StableId == $"dispatch-intent:{initial.클라이언트요청Id:N}"));
        Assert.Equal(alreadyHasIntent ? 2 : 1, await canonical.운영배차활동사건.CountAsync());
    }

    [Fact]
    public async Task 조건부On의같은ID에다른관측판본을넣으면충돌한다()
    {
        await using var f = await Fixture.Create(); await using var db = f.Context(); var service = f.Service(db);
        var initial = Initial(null); await service.기사의사변경Async("driver", initial);
        initial.예상수신의사판본Token = "different-observation";
        await Assert.ThrowsAsync<운영배차수신요청충돌Exception>(() => service.기사의사변경Async("driver", initial));
        Assert.Single(await db.운영배차활동사건.ToArrayAsync());
    }

    [Theory]
    [InlineData(운영배차수신의사Code.Off, false)]
    [InlineData(운영배차수신의사Code.On, true)]
    public async Task 조건부최초On은수동재개나Off요청으로사용할수없다(string code, bool resume)
    {
        await using var f = await Fixture.Create(); await using var db = f.Context();
        var request = Initial(null); request.수신의사Code = code; request.미응답자동중지해제 = resume;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service(db).기사의사변경Async("driver", request));
        Assert.Empty(await db.운영배차활동사건.ToArrayAsync());
    }

    [Fact]
    public async Task 조건부최초On도자동중지와기존서버제한을해제하지않는다()
    {
        await using var f = await Fixture.Create(); await using var db = f.Context(); var service = f.Service(db);
        for (var round = 1; round <= 3; round++)
        {
            var occurred = f.Clock.Now.UtcDateTime.AddMinutes(-10 + round);
            db.운영배차활동사건.Add(운영배차활동사건Factory.제안표시확인("driver", "offer-" + round, round, occurred));
            db.운영배차활동사건.Add(운영배차활동사건Factory.무응답("driver", "offer-" + round, round, occurred.AddSeconds(20)));
        }
        await db.SaveChangesAsync();
        var observed = await service.기사의사변경Async("driver", Manual(운영배차수신의사Code.Off));
        await service.서버실효상태변경Async("driver", Guid.NewGuid(), 운영배차실효상태Code.연결확인불가, "OtherRestriction");
        var result = await service.기사의사변경Async("driver", Initial(observed.수신의사판본Token));
        Assert.Equal(운영배차수신의사Code.On, result.수신의사Code);
        Assert.True(result.미응답자동중지여부); Assert.Equal(3, result.연속미응답횟수);
        Assert.Equal(운영배차실효상태Code.연결확인불가, result.실효상태Code);
        Assert.Equal("OtherRestriction", result.실효사유Code); Assert.False(운영배차수신상태Policy.신규배차수신허용(result));
    }

    [Fact]
    public async Task 첫저장실패뒤같은Context재시도는추적중복없이조건부On을기록한다()
    {
        await using var f = await Fixture.Create(sqlite: true, new FailFirstInitialSave());
        await using var db = f.Context(); var service = f.Service(db); var request = Initial(null);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.기사의사변경Async("driver", request));
        Assert.Empty(db.ChangeTracker.Entries<운영배차활동사건>().Where(x => x.State == EntityState.Added));
        Assert.Equal(운영배차수신의사Code.On, (await service.기사의사변경Async("driver", request)).수신의사Code);
        Assert.Single(await db.운영배차활동사건.ToArrayAsync());
    }

    [Fact]
    public async Task 기존수동요청은조건부Flag기본값과무관한관측토큰으로제한하지않는다()
    {
        await using var f = await Fixture.Create(); await using var db = f.Context(); var service = f.Service(db);
        var request = Manual(운영배차수신의사Code.On); request.예상수신의사판본Token = "unused-legacy-client";
        Assert.False(request.최초On조건부적용);
        Assert.Equal(운영배차수신의사Code.On, (await service.기사의사변경Async("driver", request)).수신의사Code);
        Assert.Equal(운영배차수신의사Code.On, (await service.기사의사변경Async("driver", request)).수신의사Code);
        Assert.Single(await db.운영배차활동사건.ToArrayAsync());
    }

    [Fact]
    public async Task 낮은격리수준의외부Transaction에서는조건부On을기록하지않는다()
    {
        await using var f = await Fixture.Create(sqlite: true, sharedCache: true); await using var db = f.Context();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var ledger = new Ef운영배차활동원장Store(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ledger.수신의사추가Async(new 운영배차활동사건Dto
            {
                사건StableId = "ambient-initial", 주체Id = "driver", 주체역할Code = 운영배차주체역할Code.기사,
                사건유형Code = 운영배차사건유형Code.수신의사변경, 상태값Code = 운영배차수신의사Code.On,
                발생시각Utc = f.Clock.Now
            }, true, null));
        Assert.Contains("Serializable", error.Message);
        await tx.RollbackAsync(); Assert.Empty(await db.운영배차활동사건.ToArrayAsync());
    }

    private static 운영배차수신의사변경요청 Initial(string? token) => new()
    { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On, 최초On조건부적용 = true, 예상수신의사판본Token = token };
    private static 운영배차수신의사변경요청 Manual(string code) => new() { 클라이언트요청Id = Guid.NewGuid(), 수신의사Code = code };

    private sealed class Fixture(DbContextOptions<SsalddelContext> options, string? file) : IAsyncDisposable
    {
        public Clock Clock { get; } = new();
        public SsalddelContext Context() => new(options, new Encryption());
        public 운영배차공통UseCase Service(SsalddelContext db) => new(new Ef운영배차활동원장Store(db),
            new InMemory운영배차판정ProjectionStore(), new 운영배차수신상태Policy(), new 운영배차단기지표Calculator(),
            Clock, NullLogger<운영배차공통UseCase>.Instance);
        public static async Task<Fixture> Create(bool sqlite = false, IInterceptor? interceptor = null, bool sharedCache = false)
        {
            var builder = new DbContextOptionsBuilder<SsalddelContext>(); string? file = null;
            if (sqlite)
            {
                file = Path.Combine(Path.GetTempPath(), "ssalddel-initial-on-" + Guid.NewGuid().ToString("N") + ".db");
                builder.UseSqlite("Data Source=" + file + ";Default Timeout=10;Pooling=False" + (sharedCache ? ";Cache=Shared" : ""));
            }
            else builder.UseInMemoryDatabase("initial-on-" + Guid.NewGuid());
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            var f = new Fixture(builder.Options, file); await using var db = f.Context(); await db.Database.EnsureCreatedAsync(); return f;
        }
        public ValueTask DisposeAsync()
        {
            if (file is not null && File.Exists(file)) File.Delete(file);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now { get; set; } = new(2026, 10, 16, 1, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class HoldManualOff : SaveChangesInterceptor
    {
        private int armed;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken token = default)
        {
            if (data.Context!.ChangeTracker.Entries<운영배차활동사건>().Any(x => x.State == EntityState.Added
                    && x.Entity.사건유형Code == 운영배차사건유형Code.수신의사변경 && x.Entity.상태값Code == 운영배차수신의사Code.Off)
                && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
            return result;
        }
    }
    private sealed class FailFirstInitialSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken token = default)
        {
            if (!failed && data.Context!.ChangeTracker.Entries<운영배차활동사건>().Any(x => x.State == EntityState.Added
                    && x.Entity.사유Code == 운영배차수신의사판본.최초On사유))
            { failed = true; throw new DbUpdateException("InitialIntentSaveFailed"); }
            return ValueTask.FromResult(result);
        }
    }
}
