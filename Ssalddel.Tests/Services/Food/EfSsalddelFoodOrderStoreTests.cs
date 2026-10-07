using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.설정;

namespace Ssalddel.Tests.Services.Food;

public sealed class EfSsalddelFoodOrderStoreTests
{
    // 이 세 관계형 검증은 전용 provider 하나를 공유하여 전체 suite의 EF provider cache를 늘리지 않습니다.
    private static readonly ServiceProvider RetryingSqliteProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite()
        .AddScoped<IExecutionStrategyFactory, RetryStrategyFactory>()
        .BuildServiceProvider(validateScopes: true);

    [Fact]
    public void 재시도전략의_관계형배차결속은_현재진행과최초시각을보존한다()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = RetryingSqliteOptions(connection);
        using var db = new SsalddelContext(options, new PassThroughEncryptionService());
        db.Database.EnsureCreated();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user");
        var firstTime = DateTime.UtcNow.AddMinutes(-10);

        // EF의 실제 transaction guard가 실행됩니다. 전략 밖에서 시작한 transaction의 조회/저장은 실패합니다.
        var first = store.배차대기반영(order.주문번호, 41, firstTime)!;
        Assert.Equal(41, first.배차대기Id);
        using (var writer = new SsalddelContext(options, new PassThroughEncryptionService()))
        {
            var saved = writer.음식주문.Single(x => x.주문번호 == order.주문번호);
            saved.상태 = 음식주문상태코드.수령확인;
            saved.배차상태 = 음식주문배차상태코드.배달완료;
            writer.SaveChanges();
        }
        var replay = store.배차대기반영(order.주문번호, 41, DateTime.UtcNow)!;
        Assert.Equal(음식주문상태코드.수령확인, replay.상태);
        Assert.Equal(음식주문배차상태코드.배달완료, replay.배차상태);
        Assert.Equal(firstTime, replay.배차요청시각Utc);
        Assert.Throws<InvalidOperationException>(() => store.배차대기반영(order.주문번호, 42, DateTime.UtcNow));
        using var read = new SsalddelContext(options, new PassThroughEncryptionService());
        var actual = read.음식주문.AsNoTracking().Single();
        Assert.Equal(41, actual.배차대기Id);
        Assert.Equal(firstTime, actual.배차요청시각Utc);
        Assert.Equal(음식주문상태코드.수령확인, actual.상태);
    }

    [Fact]
    public void 배차결속의_일시저장실패후_Modified사본은_정본재조회후다시저장한다()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var failure = new FailOnceBindingSave();
        var options = RetryingSqliteOptions(connection, failure);
        using var db = new SsalddelContext(options, new PassThroughEncryptionService());
        db.Database.EnsureCreated();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user");
        var firstTime = DateTime.UtcNow.AddMinutes(-10);
        failure.Armed = true;

        var bound = store.배차대기반영(order.주문번호, 41, firstTime)!;

        Assert.Equal(2, failure.BindingSaveAttempts);
        Assert.Equal(41, bound.배차대기Id);
        Assert.Equal(firstTime, bound.배차요청시각Utc);
        using var read = new SsalddelContext(options, new PassThroughEncryptionService());
        var actual = read.음식주문.AsNoTracking().Single();
        Assert.Equal(41, actual.배차대기Id);
        Assert.Equal(firstTime, actual.배차요청시각Utc);
        Assert.Equal(음식주문상태코드.주문확인, actual.상태);
        Assert.Equal(음식주문배차상태코드.배차대기, actual.배차상태);
    }

    [Fact]
    public void 배차결속은_호출자의기존트랜잭션을_commit하지않아_롤백가능하다()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = RetryingSqliteOptions(connection);
        using var db = new SsalddelContext(options, new PassThroughEncryptionService());
        db.Database.EnsureCreated();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user");

        db.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var callerTransaction = db.Database.BeginTransaction();
            var pending = db.음식주문.Single(x => x.주문번호 == order.주문번호);
            pending.상태 = 음식주문상태코드.기사배정;
            pending.배차상태 = 음식주문배차상태코드.기사배정;
            db.ChangeTracker.DetectChanges();
            var bound = store.배차대기반영(order.주문번호, 41, DateTime.UtcNow)!;
            Assert.Equal(41, bound.배차대기Id);
            Assert.Equal(음식주문상태코드.기사배정, bound.상태);
            Assert.Equal(음식주문배차상태코드.기사배정, bound.배차상태);
            Assert.Same(callerTransaction, db.Database.CurrentTransaction);
            callerTransaction.Rollback();
        });
        using var read = new SsalddelContext(options, new PassThroughEncryptionService());
        var actual = read.음식주문.AsNoTracking().Single();
        Assert.Null(actual.배차대기Id);
        Assert.Null(actual.배차요청시각Utc);
        Assert.Equal(음식주문상태코드.주문확인, actual.상태);
        Assert.Equal(음식주문배차상태코드.미요청, actual.배차상태);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsupported-food-status")]
    public void 잘못된저장상태는_수락거절취소배차연결을_변경하지않는다(string raw)
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        var entity = db.음식주문.Single(x => x.주문번호 == order.주문번호);
        entity.상태 = raw;
        db.SaveChanges();
        var historyCount = entity.상태이력.Count;
        Assert.Throws<InvalidOperationException>(() => store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user"));
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(order.주문번호,
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.거절, 사유 = "재료 품절" }, "restaurant-user"));
        Assert.Throws<InvalidOperationException>(() => store.주문자취소(order.주문번호,
            new 주문자음식주문취소요청 { 클라이언트요청Id = Guid.NewGuid(), 사유Code = "ChangedMind" }, "orderer-1"));
        Assert.Throws<InvalidOperationException>(() => store.배차대기반영(order.주문번호, 41, DateTime.UtcNow));
        db.ChangeTracker.Clear();
        var preserved = db.음식주문.Include(x => x.상태이력).Single(x => x.주문번호 == order.주문번호);
        Assert.Equal(raw, preserved.상태);
        Assert.Equal(음식주문배차상태코드.미요청, preserved.배차상태);
        Assert.Null(preserved.배차대기Id);
        Assert.Equal(historyCount, preserved.상태이력.Count);
    }

    [Fact]
    public void 과거주문접수별칭의_정상수락은_유지한다()
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        db.음식주문.Single(x => x.주문번호 == order.주문번호).상태 = " 주문접수 ";
        db.SaveChanges();
        Assert.Equal(음식주문상태코드.주문확인, store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user")!.주문.상태);
    }

    [Theory]
    [InlineData(음식주문상태코드.기사배정, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.조리중, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.픽업완료, 음식주문배차상태코드.기사배정)]
    [InlineData(음식주문상태코드.전달완료, 음식주문배차상태코드.배달완료)]
    [InlineData(음식주문상태코드.수령확인, 음식주문배차상태코드.배달완료)]
    [InlineData(음식주문상태코드.취소, 음식주문배차상태코드.배차대기)]
    public void 같은배차결속은_진행과최초시각을보존하고_다른큐로바꾸지않는다(string status, string dispatchStatus)
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        store.음식점수락멱등(order.주문번호, new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid() }, "restaurant-user");
        var requestedAt = DateTime.UtcNow.AddMinutes(-10);
        store.배차대기반영(order.주문번호, 41, requestedAt);
        var entity = db.음식주문.Single(x => x.주문번호 == order.주문번호);
        entity.상태 = status;
        entity.배차상태 = dispatchStatus;
        db.SaveChanges();
        var changedAt = entity.UpdatedAt;
        var historyCount = entity.상태이력.Count;
        var same = store.배차대기반영(order.주문번호, 41, DateTime.UtcNow)!;
        Assert.Equal(status, same.상태);
        Assert.Equal(dispatchStatus, same.배차상태);
        Assert.Equal(requestedAt, same.배차요청시각Utc);
        Assert.Throws<InvalidOperationException>(() => store.배차대기반영(order.주문번호, 42, DateTime.UtcNow));
        Assert.Equal(changedAt, entity.UpdatedAt);
        Assert.Equal(historyCount, entity.상태이력.Count);
        Assert.Equal(41, entity.배차대기Id);
    }

    [Fact]
    public void 주문확인은_배차전에_조리시계와_준비완료를_시작하지_않는다()
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        var accepted = store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 20 }, "restaurant-user")!.주문;
        Assert.Equal(음식주문상태코드.주문확인, accepted.상태);
        Assert.Equal(20, accepted.조리예상분);
        Assert.Null(accepted.조리시작시각Utc);
        Assert.Null(accepted.조리예상완료시각Utc);
        Assert.False(accepted.조리시작가능);
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(order.주문번호,
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = accepted.Revision,
                작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-user"));
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(order.주문번호,
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비 }, "restaurant-user"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void 추천뿐이거나_종료된_기사시도는_조리시작을_허용하지_않는다(bool confirmed, bool ended)
    {
        using var db = CreateContext();
        var (store, orderNo) = 배차표본생성(db, confirmed, ended);
        var order = store.GetOrder(orderNo)!;
        Assert.False(order.조리시작가능);
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(orderNo,
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
                작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-user"));
        Assert.Null(store.GetOrder(orderNo)!.조리시작시각Utc);
    }

    [Fact]
    public void 유효한배차후_조리시작은_한번저장되고_재조회와_배차해제에도_조리이력이_보존된다()
    {
        using var db = CreateContext();
        var (store, orderNo) = 배차표본생성(db, true, false);
        var assigned = store.GetOrder(orderNo)!;
        var actions = Ssalddel.Application.Food.음식배달가능행동Projector.음식점용(assigned).AvailableActions;
        Assert.Contains(actions, x => x.ActionId == 음식배달가능행동Ids.음식점조리시작);
        Assert.DoesNotContain(actions, x => x.ActionId == 음식배달가능행동Ids.음식점픽업준비완료);
        var request = new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(),
            예상Revision = assigned.Revision, 작업 = 음식점주문진행작업코드.조리시작 };
        var first = store.음식점진행변경(orderNo, request, "restaurant-user")!;
        var repeated = store.음식점진행변경(orderNo, request, "restaurant-user")!;
        Assert.True(first.새로변경됨);
        Assert.False(repeated.새로변경됨);
        db.ChangeTracker.Clear();
        var reloaded = new EfSsalddelFoodOrderStore(db).GetOrder(orderNo)!;
        Assert.Equal(음식주문상태코드.조리중, reloaded.상태);
        Assert.Equal(음식주문배차상태코드.기사배정, reloaded.배차상태);
        Assert.NotNull(reloaded.조리시작시각Utc);
        Assert.Equal(reloaded.조리시작시각Utc!.Value.AddMinutes(20), reloaded.조리예상완료시각Utc);
        Assert.Equal(reloaded.조리예상완료시각Utc, db.음식배달시도.Single().표시준비예정시각Utc);
        Assert.Single(db.음식주문상태이력.Where(x => x.클라이언트요청Id == request.클라이언트요청Id));
        var queue = db.운송원장.Single();
        queue.확정기사Id = null;
        db.SaveChanges();
        Assert.False(store.GetOrder(orderNo)!.조리시작가능);
        Assert.Equal(reloaded.조리시작시각Utc, store.GetOrder(orderNo)!.조리시작시각Utc);
    }

    [Fact]
    public void 조리시작은_누락되거나_오래된Revision과_배차해제후의_옛화면을_거부한다()
    {
        using var db = CreateContext();
        var (store, orderNo) = 배차표본생성(db, true, false);
        var order = store.GetOrder(orderNo)!;
        var request = new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.조리시작 };
        Assert.Throws<ArgumentException>(() => store.음식점진행변경(orderNo, request, "restaurant-user"));
        request.예상Revision = order.Revision + 1;
        Assert.Throws<DbUpdateConcurrencyException>(() => store.음식점진행변경(orderNo, request, "restaurant-user"));
        request.예상Revision = order.Revision;
        db.운송원장.Single().확정기사Id = null;
        db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => store.음식점진행변경(orderNo, request, "restaurant-user"));
        Assert.Null(store.GetOrder(orderNo)!.조리시작시각Utc);
    }

    private static (EfSsalddelFoodOrderStore Store, string OrderNo) 배차표본생성(SsalddelContext db, bool confirmed, bool ended)
    {
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 20 }, "restaurant-user");
        var queue = new 살뜰.도메인.운송.운송원장
        {
            의뢰Id = order.주문번호, 원본의뢰Id = order.주문번호,
            배차업무유형 = 살뜰.도메인.공통.상태값.배차업무유형.음식배달,
            상태 = confirmed ? 살뜰.도메인.공통.상태값.배차대기상태.확정 : "추천중",
            배차큐단계 = confirmed ? 살뜰.도메인.공통.상태값.배차큐단계.확정 : 살뜰.도메인.공통.상태값.배차큐단계.배차추천,
            확정기사Id = confirmed ? "driver-1" : null
        };
        db.운송원장.Add(queue);
        db.SaveChanges();
        var entity = db.음식주문.Single(x => x.주문번호 == order.주문번호);
        entity.상태 = 음식주문상태코드.기사배정;
        entity.배차상태 = 음식주문배차상태코드.기사배정;
        entity.배차대기Id = queue.Id;
        db.음식배달시도.Add(new 살뜰.도메인.음식.음식배달시도
        {
            시도StableId = "attempt-1", 주문번호 = order.주문번호, 제안Id = queue.의뢰Id,
            기사Id = "driver-1", 시도순번 = 1, 수락시각Utc = DateTime.UtcNow,
            중단시각Utc = ended ? DateTime.UtcNow : null
        });
        db.SaveChanges();
        return (store, order.주문번호);
    }

    [Fact]
    public void 새DB의_문자열복합인덱스가_MySql키길이를_초과하지_않는다()
    {
        using var db = CreateContext();
        var oversized = db.Model.GetEntityTypes().SelectMany(type => type.GetIndexes().Select(index => new
        {
            Name = type.ClrType.Name + ":" + string.Join(",", index.Properties.Select(x => x.Name)),
            Bytes = index.Properties.Sum(p => p.ClrType == typeof(string) ? (p.GetMaxLength() ?? 0) * 4 : 8)
        })).Where(x => x.Bytes > 3072).Select(x => x.Name + "=" + x.Bytes).ToArray();
        Assert.True(oversized.Length == 0, string.Join("; ", oversized));
    }

    [Fact]
    public void 새DB의_HR인덱스는_기존마이그레이션의_키길이를_유지한다()
    {
        using var db = CreateContext();
        var type = db.Model.GetEntityTypes().Single(x => x.ClrType.Name == "HrEmploymentContractRecord");
        var index = Assert.Single(type.GetIndexes());
        Assert.Equal(new[] { "WorkerUserId", "EmployerScopeType", "ContractStatus" }, index.Properties.Select(x => x.Name));
        Assert.True(index.Properties.Sum(x => x.GetMaxLength() ?? 0) * 4 <= 3072);
    }

    [Fact]
    public async Task 기사배정뒤_픽업준비는_배정과_원장을_보존하고_중복을_기록하지_않는다()
    {
        await using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        var entity = await db.음식주문.SingleAsync(x => x.주문번호 == order.주문번호);
        entity.상태 = 음식주문상태코드.기사배정;
        entity.배차상태 = 음식주문배차상태코드.기사배정;
        entity.조리예상완료시각Utc = DateTime.UtcNow.AddMinutes(1);
        entity.상태이력.Add(new 살뜰.도메인.음식.음식주문상태이력
        {
            이전상태 = 음식주문상태코드.주문대기,
            다음상태 = 음식주문상태코드.조리중,
            사유 = "음식점 주문 수락", 전이시각Utc = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var request = new 음식점주문진행변경요청
            { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비 };
        var first = store.음식점진행변경(order.주문번호, request, "restaurant-user");
        var repeated = store.음식점진행변경(order.주문번호, request, "restaurant-user");
        Assert.True(first!.새로변경됨);
        Assert.False(repeated!.새로변경됨);
        Assert.Equal(음식주문상태코드.기사배정, repeated.주문.상태);
        Assert.Equal(음식주문배차상태코드.기사배정, repeated.주문.배차상태);
        Assert.NotNull(repeated.주문.픽업준비시각Utc);
        Assert.Equal(3, repeated.주문.Revision);
        Assert.Single(await db.음식주문상태이력.Where(x => x.클라이언트요청Id == request.클라이언트요청Id).ToListAsync());
    }

    [Fact]
    public void 음식점진행변경은_오래된Revision을거부한다()
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));

        Assert.Throws<DbUpdateConcurrencyException>(() => store.음식점진행변경(order.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = order.Revision + 1,
                작업 = 음식점주문진행작업코드.거절,
                사유 = "재료 품절"
            }, "restaurant-user"));
    }

    [Fact]
    public async Task 같은주문자와클라이언트요청Id는_Rdb에한건만저장한다()
    {
        await using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var requestId = Guid.NewGuid();
        var request = CreateRequest(requestId);

        var first = store.AddOrder(request);
        var retried = store.AddOrder(CreateRequest(requestId));

        Assert.Equal(first.주문번호, retried.주문번호);
        Assert.Equal(requestId, retried.클라이언트요청Id);
        Assert.Equal(1, await db.음식주문.CountAsync());
        Assert.Equal(1001, (await db.음식주문상품.SingleAsync()).메뉴Id);
    }

    [Fact]
    public async Task 음식점진행요청Id와처리자는_Rdb상태이력에한번만저장한다()
    {
        await using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        var request = new 음식점주문진행변경요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            작업 = 음식점주문진행작업코드.거절,
            사유 = "재료 품절"
        };

        var first = store.음식점진행변경(order.주문번호, request, "restaurant-user");
        var retried = store.음식점진행변경(order.주문번호, request, "restaurant-user");

        Assert.True(first?.새로변경됨);
        Assert.False(retried?.새로변경됨);
        var history = Assert.Single(
            await db.음식주문상태이력
                .Where(item => item.클라이언트요청Id == request.클라이언트요청Id)
                .ToListAsync());
        Assert.Equal("restaurant-user", history.처리UserId);
        Assert.Equal(음식주문상태코드.거절, history.다음상태);
    }

    [Fact]
    public async Task 주문자수령확인은_전달완료와소유권을확인하고_요청Id를한번만저장한다()
    {
        await using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));
        var entity = await db.음식주문
            .Include(item => item.상태이력)
            .SingleAsync(item => item.주문번호 == order.주문번호);
        entity.상태 = 음식주문상태코드.전달완료;
        entity.배차상태 = 음식주문배차상태코드.배달완료;
        await db.SaveChangesAsync();
        var request = new 주문자음식주문수령확인요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            확인메모 = "정상 수령"
        };

        var otherUser = store.주문자수령확인(order.주문번호, request, "other-orderer");
        var first = store.주문자수령확인(order.주문번호, request, "orderer-1");
        var retried = store.주문자수령확인(order.주문번호, request, "orderer-1");

        Assert.Null(otherUser);
        Assert.True(first?.새로변경됨);
        Assert.False(retried?.새로변경됨);
        Assert.Equal(음식주문상태코드.수령확인, retried?.주문.상태);
        var history = Assert.Single(
            await db.음식주문상태이력
                .Where(item => item.클라이언트요청Id == request.클라이언트요청Id)
                .ToListAsync());
        Assert.Equal("orderer-1", history.처리UserId);
        Assert.Equal(음식주문상태코드.전달완료, history.이전상태);
        Assert.Equal(음식주문상태코드.수령확인, history.다음상태);
        Assert.Contains("정상 수령", history.사유);
        var projectionRequest = Assert.Single(
            await db.음식마트원장동기화Outbox
                .Where(item => item.동기화유형 == 음식마트원장동기화유형코드.음식배달완료WorldProjection)
                .ToListAsync());
        Assert.Equal(order.주문번호, projectionRequest.원천Id);
        Assert.Contains("orderRevision", projectionRequest.PayloadJson);
    }

    [Fact]
    public void 주문자수령확인은_기사전달완료전에는거부한다()
    {
        using var db = CreateContext();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateRequest(Guid.NewGuid()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            store.주문자수령확인(
                order.주문번호,
                new 주문자음식주문수령확인요청
                {
                    클라이언트요청Id = Guid.NewGuid()
                },
                "orderer-1"));

        Assert.Contains("기사 전달 완료", exception.Message);
    }

    private static 음식주문등록요청 CreateRequest(Guid requestId)
        => new()
        {
            클라이언트요청Id = requestId,
            음식점Id = 101,
            주문자UserId = "orderer-1",
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "주문자",
                연락처 = "010-1234-5678",
                주소 = "서울특별시 중구 세종대로 1"
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    메뉴Id = 1001,
                    상품명 = "살뜰김밥",
                    수량 = 2,
                    단가 = 4_500
                }
            ]
        };

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"food-order-idempotency-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryptionService());

    private static DbContextOptions<SsalddelContext> RetryingSqliteOptions(SqliteConnection connection, SaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection)
            .UseInternalServiceProvider(RetryingSqliteProvider);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private sealed class RetryStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryableBindingWriteException;
    }

    private sealed class RetryableBindingWriteException : Exception { }

    private sealed class FailOnceBindingSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public int BindingSaveAttempts { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (!Armed) return result;
            var db = eventData.Context!;
            db.ChangeTracker.DetectChanges();
            var changed = db.ChangeTracker.Entries<살뜰.도메인.음식.음식주문>()
                .Any(x => x.State == EntityState.Modified && x.Entity.배차대기Id.HasValue);
            if (!changed) return result;
            BindingSaveAttempts++;
            if (BindingSaveAttempts == 1) throw new RetryableBindingWriteException();
            return result;
        }
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
