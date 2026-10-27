using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.마트;
using 살뜰.도메인.설정;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Community;

public sealed class FoodMartLedgerSyncOutboxServiceTests
{
    [Fact]
    public async Task Mongo결과가없으면_대기로남기고_다음처리에서성공한다()
    {
        await using var db = CreateContext();
        var sync = new RecordingLedgerSync { ReturnNull = true };
        var service = CreateService(db, sync);

        await service.음식주문예약후즉시처리Async(
            CreateOrder(),
            "orderer-a",
            "food-event:event-1");

        var pending = await db.음식마트원장동기화Outbox.SingleAsync();
        Assert.Equal(OutboxProcessingStatuses.Pending, pending.처리상태);
        Assert.Equal(1, pending.시도횟수);
        Assert.NotEmpty(pending.마지막오류);

        pending.UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        await db.SaveChangesAsync();
        sync.ReturnNull = false;

        var processed = await service.대기항목처리Async();

        Assert.Equal(1, processed);
        Assert.Equal(OutboxProcessingStatuses.Succeeded, pending.처리상태);
        Assert.Equal(2, pending.시도횟수);
        Assert.Equal(2, sync.FoodAttempts);
    }

    [Fact]
    public async Task 같은멱등키는_Outbox항목을중복생성하지않는다()
    {
        await using var db = CreateContext();
        var sync = new RecordingLedgerSync();
        var service = CreateService(db, sync);
        var order = CreateOrder();

        await service.음식주문예약후즉시처리Async(order, "orderer-a", "food-event:event-2");
        await service.음식주문예약후즉시처리Async(order, "orderer-a", "food-event:event-2");

        Assert.Equal(1, await db.음식마트원장동기화Outbox.CountAsync());
        Assert.Equal(1, sync.FoodAttempts);
    }

    [Fact]
    public async Task 출고동기화는_Outbox에저장한Id로최신Rdb항목을다시읽는다()
    {
        await using var db = CreateContext();
        var outbound = new 출고예정
        {
            주문참조번호 = "MART-100",
            상품명 = "쌀",
            SKU = "RICE-10KG",
            수량 = 1,
            상태 = 출고상태.예정,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.출고예정.Add(outbound);
        await db.SaveChangesAsync();

        var sync = new RecordingLedgerSync();
        var service = CreateService(db, sync);
        await service.출고원장예약후즉시처리Async(
            [outbound],
            [],
            "warehouse-a",
            "warehouse:MART-100:1",
            currentStageKey: "출고 예정");

        var savedOutbox = await db.음식마트원장동기화Outbox.SingleAsync();
        Assert.True(sync.OutboundAttempts == 1, savedOutbox.마지막오류);
        Assert.Equal(outbound.Id, Assert.Single(sync.LastOutboundIds));
        Assert.Equal(
            OutboxProcessingStatuses.Succeeded,
            savedOutbox.처리상태);
    }

    [Fact]
    public async Task 개인정보차단은_claim저장뒤_잘못된Json을읽기전에_payload를비운다()
    {
        await using var db = CreateContext();
        db.음식마트원장동기화Outbox.Add(CreatePendingItem("privacy-malformed", "not-json"));
        await db.SaveChangesAsync();
        var sync = new RecordingLedgerSync();
        var privacy = new ClaimRecordingPrivacy(db);
        var service = new 음식마트원장동기화OutboxService(db, sync,
            NullLogger<음식마트원장동기화OutboxService>.Instance, privacy);

        Assert.Equal(1, await service.대기항목처리Async());

        Assert.Equal(개인정보파기원천Codes.FoodOrder, privacy.SourceCode);
        Assert.Equal("FOOD-100", privacy.RecordId);
        Assert.Equal(OutboxProcessingStatuses.Processing, privacy.ObservedStatus);
        Assert.Equal(1, privacy.ObservedAttemptCount);
        db.ChangeTracker.Clear();
        var saved = await db.음식마트원장동기화Outbox.SingleAsync();
        Assert.Equal(음식개인정보OutboxPolicy.PrivacyExpired, saved.처리상태);
        Assert.Equal("{}", saved.PayloadJson);
        Assert.Empty(saved.마지막오류);
        Assert.Equal(0, sync.FoodAttempts);
        Assert.Equal(0, await service.대기항목처리Async());
    }

    [Fact]
    public async Task 만료된Processing만_같은행의시도이력을늘려재개한다()
    {
        await using var db = CreateContext();
        var payload = JsonSerializer.Serialize(CreateOrder(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var expired = CreatePendingItem("expired-lease", payload);
        expired.처리상태 = OutboxProcessingStatuses.Processing;
        expired.시도횟수 = 2;
        expired.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.LeaseTimeout - TimeSpan.FromMinutes(1);
        var active = CreatePendingItem("active-lease", payload);
        active.처리상태 = OutboxProcessingStatuses.Processing;
        active.시도횟수 = 2;
        db.음식마트원장동기화Outbox.AddRange(expired, active);
        await db.SaveChangesAsync();
        var sync = new RecordingLedgerSync();
        var service = CreateService(db, sync);

        Assert.Equal(1, await service.대기항목처리Async());

        Assert.Equal(OutboxProcessingStatuses.Succeeded, expired.처리상태);
        Assert.Equal(3, expired.시도횟수);
        Assert.Equal(OutboxProcessingStatuses.Processing, active.처리상태);
        Assert.Equal(2, active.시도횟수);
        Assert.Equal(1, sync.FoodAttempts);
        Assert.Equal(2, await db.음식마트원장동기화Outbox.CountAsync());
        Assert.Equal(0, await service.대기항목처리Async());
    }

    [Fact]
    public async Task payload처리중취소는_실패재예약없이_저장된Processing을유지한다()
    {
        await using var db = CreateContext();
        var payload = JsonSerializer.Serialize(CreateOrder(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var item = CreatePendingItem("cancel-during-payload", payload);
        item.마지막오류 = "previous attempt";
        db.음식마트원장동기화Outbox.Add(item);
        await db.SaveChangesAsync();
        using var stop = new CancellationTokenSource();
        var sync = new RecordingLedgerSync
        {
            OnFood = () => { stop.Cancel(); throw new OperationCanceledException(stop.Token); }
        };
        var service = CreateService(db, sync);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.대기항목처리Async(cancellationToken: stop.Token));

        db.ChangeTracker.Clear();
        var saved = await db.음식마트원장동기화Outbox.SingleAsync();
        Assert.Equal(OutboxProcessingStatuses.Processing, saved.처리상태);
        Assert.Equal(1, saved.시도횟수);
        Assert.Equal("previous attempt", saved.마지막오류);
        Assert.NotNull(saved.마지막시도시각Utc);
        Assert.Equal(0, await service.대기항목처리Async());
    }

    [Fact]
    public async Task 여러봉투가있어도_합의취소를먼저선택하고_기사나일반음식으로우회하지않는다()
    {
        await using var db = CreateContext();
        var item = CreatePendingItem("envelope-precedence",
            "{\"agreedCancellation\":null,\"driverFollowup\":null,\"주문번호\":\"FOOD-100\"}");
        db.음식마트원장동기화Outbox.Add(item);
        await db.SaveChangesAsync();
        var sync = new RecordingLedgerSync();

        Assert.Equal(1, await CreateService(db, sync).대기항목처리Async());

        Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
        Assert.Equal("FoodAgreedCancellationIntentUnavailable", item.마지막오류);
        Assert.Equal(0, sync.FoodAttempts);
        Assert.Equal(0, sync.OutboundAttempts);
    }

    [Fact]
    public async Task 창고의과거payload단계는_최신기사시도와현재출고자료를덮어쓰지않는다()
    {
        await using var db = CreateContext();
        var outbound = new 출고예정
        {
            주문참조번호 = "MART-100", 운송의뢰Id = "mart-dispatch", 상품명 = "쌀", SKU = "RICE-10KG",
            수량 = 2, 상태 = 출고상태.예정, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.출고예정.Add(outbound);
        db.Set<마트배송시도>().Add(new 마트배송시도
        {
            시도StableId = "mart-attempt", 주문참조번호 = "MART-100", 배차의뢰Id = "mart-dispatch",
            기사Id = "mart-driver", 상태Code = "PickedUp", Revision = 2
        });
        await db.SaveChangesAsync();
        var item = CreatePendingItem("warehouse-current-source", JsonSerializer.Serialize(new
        {
            outboundIds = new[] { outbound.Id }, inboundIds = Array.Empty<long>(),
            currentStageKey = "포장중", ledgerTemplateKey = "previous-template"
        }));
        item.동기화유형 = 음식마트원장동기화유형코드.창고출고;
        item.원천Id = "MART-100";
        db.음식마트원장동기화Outbox.Add(item);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var sync = new RecordingLedgerSync();

        Assert.Equal(1, await CreateService(db, sync).대기항목처리Async());

        Assert.Equal("배송중", sync.LastStageKey);
        Assert.Equal(CommunityLedgerTemplateKeys.SsalddelMart, sync.LastTemplateKey);
        Assert.Equal(2, sync.LastOutboundQuantity);
        Assert.Equal(OutboxProcessingStatuses.Succeeded, (await db.음식마트원장동기화Outbox.SingleAsync()).처리상태);
    }

    [Fact]
    public async Task 기사후속예약은_호출자가저장하기전까지_추적항목만추가한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, new RecordingLedgerSync());
        var intent = new 음식기사후속의도("FOOD-100", 11, "dispatch-100", "driver-100", "기사배정",
            DateTime.UtcNow, "기사 배정");

        await service.예약Async(intent);

        Assert.Equal(4, db.음식마트원장동기화Outbox.Local.Count);
        Assert.All(db.음식마트원장동기화Outbox.Local, item => Assert.Equal(EntityState.Added, db.Entry(item).State));
        Assert.Equal(0, await db.음식마트원장동기화Outbox.AsNoTracking().CountAsync());
        await db.SaveChangesAsync();
        Assert.Equal(4, await db.음식마트원장동기화Outbox.AsNoTracking().CountAsync());
    }

    private static 음식마트원장동기화Outbox CreatePendingItem(string key, string payload)
        => new()
        {
            멱등키 = key, 동기화유형 = 음식마트원장동기화유형코드.음식주문, 원천Id = "FOOD-100", 변경자 = "actor",
            PayloadJson = payload, 처리상태 = OutboxProcessingStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };

    private static 음식마트원장동기화OutboxService CreateService(
        SsalddelContext db,
        I음식마트원장Mongo동기화Service sync)
        => new(
            db,
            sync,
            NullLogger<음식마트원장동기화OutboxService>.Instance);

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .EnableSensitiveDataLogging()
                .Options,
            new PassThroughEncryption());

    private static 음식주문응답 CreateOrder()
        => new()
        {
            주문번호 = "FOOD-100",
            음식점Id = 101,
            음식점명 = "테스트 음식점",
            주문자UserId = "orderer-a",
            상태 = "주문 접수",
            배차상태 = 음식주문배차상태코드.미요청,
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "주문자",
                주소 = "서울시"
            },
            CreatedAt = DateTime.UtcNow
        };

    private sealed class RecordingLedgerSync : I음식마트원장Mongo동기화Service
    {
        public bool ReturnNull { get; set; }
        public int FoodAttempts { get; private set; }
        public int OutboundAttempts { get; private set; }
        public IReadOnlyList<long> LastOutboundIds { get; private set; } = [];
        public string? LastStageKey { get; private set; }
        public string? LastTemplateKey { get; private set; }
        public int LastOutboundQuantity { get; private set; }
        public Action? OnFood { get; init; }

        public Task<커뮤니티원장Dto?> 음식주문동기화Async(
            음식주문응답 주문,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            FoodAttempts++;
            OnFood?.Invoke();
            return Task.FromResult(ReturnNull ? null : CreateLedger($"food-order:{주문.주문번호}"));
        }

        public Task<커뮤니티원장Dto?> 출고원장동기화Async(
            IReadOnlyList<출고예정> 출고목록,
            IReadOnlyList<입고요청> 입고목록,
            string updatedBy,
            string? 현재단계Key = null,
            string? 원장템플릿Key = null,
            CancellationToken cancellationToken = default)
        {
            OutboundAttempts++;
            LastOutboundIds = 출고목록.Select(x => x.Id).ToArray();
            LastStageKey = 현재단계Key;
            LastTemplateKey = 원장템플릿Key;
            LastOutboundQuantity = 출고목록.Sum(x => x.수량);
            return Task.FromResult(ReturnNull ? null : CreateLedger("warehouse-outbound:MART-100"));
        }

        private static 커뮤니티원장Dto CreateLedger(string id)
            => new()
            {
                원장Id = id,
                상태 = "동기화됨"
            };
    }

    private sealed class ClaimRecordingPrivacy(SsalddelContext db) : I개인정보복원차단Service
    {
        public string? SourceCode { get; private set; }
        public string? RecordId { get; private set; }
        public string? ObservedStatus { get; private set; }
        public int ObservedAttemptCount { get; private set; }

        public Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken ct = default)
        {
            SourceCode = sourceCode;
            RecordId = recordId;
            var item = db.음식마트원장동기화Outbox.Local.Single();
            ObservedStatus = item.처리상태;
            ObservedAttemptCount = item.시도횟수;
            return Task.FromResult(false);
        }
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
