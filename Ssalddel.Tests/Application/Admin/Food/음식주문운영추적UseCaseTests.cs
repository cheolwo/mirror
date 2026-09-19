using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class 음식주문운영추적UseCaseTests
{
    [Fact]
    public async Task 수령확인완료주문의_stableId와Outbox를_개인정보없이추적한다()
    {
        await using var db = CreateContext();
        await SeedCompletedAsync(db, "FOOD-TRACE-COMPLETE");

        var result = await new 음식주문운영추적UseCase(db)
            .조회Async("FOOD-TRACE-COMPLETE");

        Assert.NotNull(result);
        Assert.Equal(음식주문운영추적상태코드.완료, result.전체상태);
        Assert.Equal(
            음식배달운영생명주기단계Codes.종료,
            result.생명주기조화.현재단계Code);
        Assert.True(result.생명주기조화.정상경로조화여부);
        Assert.Equal("FOOD-TRACE-COMPLETE", result.원본의뢰Id);
        Assert.NotNull(result.배차대기Id);
        Assert.Equal("TR-FOOD-TRACE-COMPLETE", result.운송번호);
        Assert.Contains(result.체크포인트, x =>
            x.단계Key == "receipt"
            && x.상태 == 음식주문운영추적상태코드.완료);
        Assert.Equal(2, result.Outbox목록.Count);
        Assert.All(result.Outbox목록, x => Assert.False(x.운영자확인필요));

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("서울시 중랑구 상세주소", json, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1234-5678", json, StringComparison.Ordinal);
        Assert.DoesNotContain("payload_json", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 만료된추천과실패Outbox는_복구필요와안내를표시한다()
    {
        await using var db = CreateContext();
        var queue = await SeedQueueAsync(
            db,
            "FOOD-TRACE-EXPIRED",
            상태값.배차노출상태.추천중,
            DateTime.UtcNow.AddMinutes(-2));
        db.음식주문.Add(CreateOrder(
            "FOOD-TRACE-EXPIRED",
            음식주문상태코드.조리중,
            queue.Id));
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = "food-trace-expired",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = "FOOD-TRACE-EXPIRED",
            처리상태 = OutboxProcessingStatuses.Failed,
            시도횟수 = OutboxProcessingPolicy.MaximumAttempts,
            마지막오류 = "Mongo timeout at 서울시 중랑구 상세주소",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        db.배차추천알림Outbox.Add(new 배차추천알림Outbox
        {
            배차대기Id = queue.Id,
            의뢰Id = "FOOD-TRACE-EXPIRED",
            기사Id = "driver-sensitive",
            추천라운드 = 1,
            제목 = "추천",
            본문 = "추천",
            DataJson = """{"phone":"010-1234-5678"}""",
            발송상태 = OutboxProcessingStatuses.Failed,
            시도횟수 = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
        });
        await db.SaveChangesAsync();

        var result = await new 음식주문운영추적UseCase(db)
            .조회Async("FOOD-TRACE-EXPIRED");

        Assert.NotNull(result);
        Assert.True(result.추천만료됨);
        Assert.Equal(음식주문운영추적상태코드.복구필요, result.전체상태);
        Assert.DoesNotContain(result.경고목록, x => x.Contains("추천 유효시간", StringComparison.Ordinal));
        Assert.DoesNotContain(result.복구안내목록, x => x.Contains("추천 만료", StringComparison.Ordinal));
        Assert.Contains(result.Outbox목록, x => x.운영자확인필요);
        Assert.Equal(
            음식배달운영주의상태Codes.운영자확인필요,
            result.생명주기조화.주의상태Code);
        Assert.Contains(
            음식배달운영자동회복Codes.기사추천만료,
            result.생명주기조화.자동회복Codes);
        Assert.DoesNotContain(
            음식배달운영자동회복Codes.기사추천만료,
            result.생명주기조화.예외Codes);
        Assert.Contains(
            음식배달운영책임주체Codes.플랫폼운영자,
            result.생명주기조화.현재책임주체Codes);

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("driver-sensitive", json, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1234-5678", json, StringComparison.Ordinal);
        Assert.DoesNotContain("서울시 중랑구 상세주소", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 추천만료만있으면_자동회복중이며_운영자복구필요가아니다()
    {
        await using var db = CreateContext();
        const string orderNo = "FOOD-TRACE-AUTO-REDISPATCH";
        var queue = await SeedQueueAsync(
            db,
            orderNo,
            상태값.배차노출상태.추천중,
            DateTime.UtcNow.AddMinutes(-2));
        var order = CreateOrder(orderNo, 음식주문상태코드.조리중, queue.Id);
        order.조리예상완료시각Utc = DateTime.UtcNow.AddMinutes(20);
        db.음식주문.Add(order);
        AddSucceededOutboxes(db, queue, orderNo);
        await db.SaveChangesAsync();

        var result = await new 음식주문운영추적UseCase(db).조회Async(orderNo);

        Assert.NotNull(result);
        Assert.Equal(음식주문운영추적상태코드.진행중, result.전체상태);
        Assert.Equal(음식배달운영주의상태Codes.자동회복중, result.생명주기조화.주의상태Code);
        Assert.True(result.생명주기조화.자동회복대상여부);
        Assert.False(result.생명주기조화.운영자확인필요여부);
        Assert.Empty(result.생명주기조화.예외Codes);
        Assert.Contains(
            음식배달운영자동회복Codes.기사추천만료,
            result.생명주기조화.자동회복Codes);
        Assert.DoesNotContain(result.체크포인트, x =>
            x.단계Key == "dispatch"
            && x.상태 == 음식주문운영추적상태코드.복구필요);
    }

    [Fact]
    public async Task 조리예상완료10분초과는_음식점준비지연한건으로운영자에게표시한다()
    {
        await using var db = CreateContext();
        const string orderNo = "FOOD-TRACE-PREPARATION-DELAY";
        var queue = await SeedQueueAsync(
            db,
            orderNo,
            상태값.배차노출상태.추천중,
            DateTime.UtcNow.AddMinutes(2));
        var order = CreateOrder(orderNo, 음식주문상태코드.조리중, queue.Id);
        order.조리예상완료시각Utc = DateTime.UtcNow.AddMinutes(-11);
        db.음식주문.Add(order);
        AddSucceededOutboxes(db, queue, orderNo);
        await db.SaveChangesAsync();

        var result = await new 음식주문운영추적UseCase(db).조회Async(orderNo);

        Assert.NotNull(result);
        Assert.Equal(음식주문운영추적상태코드.복구필요, result.전체상태);
        Assert.Equal(
            음식배달운영지연상태Codes.운영자확인필요,
            result.생명주기조화.음식점준비지연상태Code);
        Assert.Equal(
            [음식배달운영업무지연Codes.음식점준비지연],
            result.생명주기조화.업무지연Codes);
        Assert.Equal(
            [음식배달운영업무지연Codes.음식점준비지연],
            result.생명주기조화.예외Codes);
        Assert.Empty(result.생명주기조화.자동회복Codes);
        Assert.Contains(result.경고목록, x =>
            x.Contains("음식점 준비", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 서버재시작후에도_Rdb원장에서같은추적상태를복구한다()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = $"food-operations-trace-restart-{Guid.NewGuid():N}";
        await using (var firstDb = CreateContext(databaseName, root))
        {
            await SeedCompletedAsync(firstDb, "FOOD-TRACE-RESTART");
        }

        await using var restartedDb = CreateContext(databaseName, root);
        var result = await new 음식주문운영추적UseCase(restartedDb)
            .조회Async("FOOD-TRACE-RESTART");

        Assert.NotNull(result);
        Assert.Equal(음식주문운영추적상태코드.완료, result.전체상태);
        Assert.Equal("FOOD-TRACE-RESTART", result.원본의뢰Id);
        Assert.NotEmpty(result.운송이벤트목록);
    }

    [Fact]
    public async Task 음식점이행중인데배차원장이없으면_복구필요로판정한다()
    {
        await using var db = CreateContext();
        db.음식주문.Add(CreateOrder(
            "FOOD-TRACE-MISSING-DISPATCH",
            음식주문상태코드.조리중,
            null));
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = "food-trace-missing-dispatch",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = "FOOD-TRACE-MISSING-DISPATCH",
            처리상태 = OutboxProcessingStatuses.Succeeded,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await new 음식주문운영추적UseCase(db)
            .조회Async("FOOD-TRACE-MISSING-DISPATCH");

        Assert.NotNull(result);
        Assert.Equal(음식주문운영추적상태코드.복구필요, result.전체상태);
        Assert.Contains(result.경고목록, x => x.Contains("배차·운송 실행 원장", StringComparison.Ordinal));
        Assert.Contains(result.체크포인트, x =>
            x.단계Key == "dispatch"
            && x.상태 == 음식주문운영추적상태코드.복구필요);
        Assert.True(result.생명주기조화.운영자확인필요여부);
        Assert.Contains(
            음식배달운영예외Codes.배차원장누락,
            result.생명주기조화.예외Codes);
    }

    private static async Task SeedCompletedAsync(SsalddelContext db, string orderNo)
    {
        var queue = await SeedQueueAsync(
            db,
            orderNo,
            상태값.배차노출상태.종료,
            null);
        queue.상태 = 상태값.배차상태.인수완료;
        queue.확정기사Id = "driver-complete";
        queue.UpdatedAt = DateTime.UtcNow.AddMinutes(-1);

        var order = CreateOrder(orderNo, 음식주문상태코드.수령확인, queue.Id);
        order.배차상태 = 음식주문배차상태코드.배달완료;
        order.커뮤니티원장Id = $"ledger:{orderNo}";
        order.커뮤니티원장상태 = "Completed";
        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = 음식주문상태코드.전달완료,
            다음상태 = 음식주문상태코드.수령확인,
            사유 = "주문자 수령 확인",
            전이시각Utc = DateTime.UtcNow.AddMinutes(-1)
        });
        db.음식주문.Add(order);
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = $"food-trace-complete:{orderNo}",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = orderNo,
            처리상태 = OutboxProcessingStatuses.Succeeded,
            시도횟수 = 1,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        db.배차추천알림Outbox.Add(new 배차추천알림Outbox
        {
            배차대기Id = queue.Id,
            의뢰Id = orderNo,
            기사Id = "driver-complete",
            추천라운드 = 1,
            제목 = "추천",
            본문 = "추천",
            DataJson = "{}",
            발송상태 = OutboxProcessingStatuses.Succeeded,
            시도횟수 = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-9)
        });
        db.운송이벤트.Add(new 운송이벤트
        {
            의뢰Id = orderNo,
            이벤트타입 = 운송이벤트유형.배차엔진판단감사,
            이벤트시각 = DateTime.UtcNow.AddMinutes(-10),
            메타데이터 = """{"correlationId":"safe"}"""
        });
        await db.SaveChangesAsync();
    }

    private static void AddSucceededOutboxes(
        SsalddelContext db,
        운송원장 queue,
        string orderNo)
    {
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = $"food-trace-succeeded:{orderNo}",
            동기화유형 = 음식마트원장동기화유형코드.음식주문,
            원천Id = orderNo,
            처리상태 = OutboxProcessingStatuses.Succeeded,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        db.배차추천알림Outbox.Add(new 배차추천알림Outbox
        {
            배차대기Id = queue.Id,
            의뢰Id = orderNo,
            기사Id = "driver-succeeded",
            추천라운드 = 1,
            제목 = "추천",
            본문 = "추천",
            DataJson = "{}",
            발송상태 = OutboxProcessingStatuses.Succeeded,
            시도횟수 = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
        });
    }

    private static async Task<운송원장> SeedQueueAsync(
        SsalddelContext db,
        string orderNo,
        int exposureStatus,
        DateTime? expiresAt)
    {
        var queue = new 운송원장
        {
            운송번호 = $"TR-{orderNo}",
            의뢰Id = orderNo,
            화주Id = "restaurant:101",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            원본의뢰Id = orderNo,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = exposureStatus == 상태값.배차노출상태.종료
                ? 상태값.배차큐단계.종료
                : 상태값.배차큐단계.배차추천,
            배차노출상태 = exposureStatus,
            추천라운드 = 1,
            추천만료시각 = expiresAt,
            픽업_도로명주소 = "서울시 중랑구 음식점",
            하차_도로명주소 = "서울시 중랑구 상세주소",
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        return queue;
    }

    private static 음식주문 CreateOrder(string orderNo, string status, long? queueId)
        => new()
        {
            주문번호 = orderNo,
            음식점Id = 101,
            음식점명 = "살뜰식당",
            주문자UserId = "orderer-sensitive",
            수령인명 = "수령자",
            수령인연락처 = "010-1234-5678",
            수령지주소 = "서울시 중랑구",
            수령지상세주소 = "서울시 중랑구 상세주소",
            총주문금액 = 15000m,
            상태 = status,
            배차상태 = queueId.HasValue
                ? 음식주문배차상태코드.배차대기
                : 음식주문배차상태코드.미요청,
            배차대기Id = queueId,
            음식점수락시각Utc = status == 음식주문상태코드.주문대기
                ? null
                : DateTime.UtcNow.AddMinutes(-15),
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

    private static SsalddelContext CreateContext()
        => CreateContext(
            $"food-operations-trace-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot());

    private static SsalddelContext CreateContext(string databaseName, InMemoryDatabaseRoot root)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
