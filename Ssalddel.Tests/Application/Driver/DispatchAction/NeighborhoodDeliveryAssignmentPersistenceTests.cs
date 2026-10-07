using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

/// <summary>SQLite의 실제 수락 트랜잭션으로 배정 시각 저장·롤백·중복 수락을 검증합니다.</summary>
public sealed partial class NeighborhoodDeliveryAssignmentPersistenceTests
{
    [Fact]
    public async Task 생활배송수락은_후처리실패에도_같은SQL에배정근거를한번저장한다()
    {
        await using var f = await Fixture.Create();
        var result = await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), CancellationToken.None);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        f.Db.ChangeTracker.Clear();
        var request = await f.Db.화주운송의뢰.SingleAsync();
        var queue = await f.Db.운송원장.SingleAsync();
        var evidence = Assert.Single(await f.Db.운송이벤트.ToListAsync());
        Assert.Equal("driver-1", queue.확정기사Id);
        Assert.NotNull(NeighborhoodDeliveryLocationPolicy.CurrentAssignmentAt(request, queue, evidence, DateTime.UtcNow));
        Assert.DoesNotContain("비공개 주소", evidence.메타데이터);
        Assert.DoesNotContain("010-1234-5678", evidence.메타데이터);
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), CancellationToken.None)).IsSuccess);
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
    }

    [Fact]
    public async Task 수락트랜잭션실패는_기사확정과배정근거를함께롤백한다()
    {
        await using var f = await Fixture.Create();
        f.Continuity.FailCompletion = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), CancellationToken.None));
        f.Db.ChangeTracker.Clear();
        Assert.Null((await f.Db.운송원장.SingleAsync()).확정기사Id);
        Assert.Equal(상태값.배차상태.매칭중, (await f.Db.화주운송의뢰.SingleAsync()).배차상태);
        Assert.Empty(await f.Db.운송이벤트.ToListAsync());
    }

    [Fact]
    public async Task 기존화물의수락에는_생활배송지도근거를추가하지않는다()
    {
        await using var f = await Fixture.Create(neighborhood: false);
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), CancellationToken.None)).IsSuccess);
        Assert.Empty(await f.Db.운송이벤트.ToListAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public SsalddelContext Db { get; private set; } = null!;
        public Continuity Continuity { get; } = new();
        public 배차수락CommandHandler Handler => new(Db, new FailedPublisher(), new Current(), new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(), new Eligibility(), Continuity, NullLogger<배차수락CommandHandler>.Instance);
        public static async Task<Fixture> Create(bool neighborhood = true)
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(f.connection).Options, new Encryption());
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.화주운송의뢰.Add(new()
            {
                의뢰Id = "delivery-1", 주문자UserId = "owner-1", 화주Id = "owner-1", 상태 = 상태값.의뢰상태.생성됨,
                클라이언트요청Id = neighborhood ? NeighborhoodDeliveryRoutes.ClientRequestPrefix + "fixture" : "legacy:1",
                배차상태 = 상태값.배차상태.매칭중, 결제상태 = 상태값.결제상태.결제대기,
                정산시점 = "현장지급", 정산상태 = "현장수금예정",
                픽업_도로명주소 = "비공개 주소", 픽업_연락처_전화번호 = "010-1234-5678"
            });
            f.Db.운송원장.Add(new()
            {
                의뢰Id = "delivery-1", 상태 = 상태값.배차대기상태.대기, 배차업무유형 = 상태값.배차업무유형.용달운송,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = "driver-1", 추천라운드 = 3, 추천만료시각 = DateTime.UtcNow.AddMinutes(5)
            });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId => "driver-1";
        public string? Role => "기사";
    }
    private sealed class FailedPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("격리된 사후 알림 실패"));
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Task.FromException(new InvalidOperationException("격리된 사후 알림 실패"));
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Eligibility : I화물배차수락적격성Service
    {
        public Task<화물배차수락적격성결과> 평가Async(string 기사Id, 화주운송의뢰 후보의뢰, IReadOnlyCollection<string> 확인한경고코드, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물배차수락적격성결과(true, null, null, [], [], [], null, null));
    }
    private sealed class Continuity : I화물연속배차UseCase
    {
        public bool FailCompletion { get; set; }
        public Task<화물연속배차상태Dto> 조회Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult(new 화물연속배차상태Dto());
        public Task<화물연속배차상태Dto> 의사변경Async(string 기사Id, 화물연속배차의사변경요청 요청, CancellationToken cancellationToken = default) => 조회Async(기사Id, cancellationToken);
        public Task<화물다음콜예약Dto?> 추천탐색Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult<화물다음콜예약Dto?>(null);
        public Task<화물예약검증결과> 수락예약검증Async(string 기사Id, string 의뢰Id, string? reservationId, long? expectedRevision, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물예약검증결과(true));
        public Task 수락완료Async(string 기사Id, string 의뢰Id, CancellationToken cancellationToken = default)
            => FailCompletion ? Task.FromException(new InvalidOperationException("격리된 SQL 트랜잭션 실패")) : Task.CompletedTask;
        public Task 시간약속잠금Async(string 기사Id, 화주운송의뢰 의뢰, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 완료기록Async(string 기사Id, DateTime 완료시각Utc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string 기사Id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult(new 화물경로위험Dto());
    }
}
