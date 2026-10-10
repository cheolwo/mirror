using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Versioning;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 마트라스트마일원천AdapterTests
{
    private const string Reference = "MART-PRIVATE-REFERENCE-001";
    private static readonly DateTime PickedAt = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PackedAt = PickedAt.AddMinutes(3);

    [Fact]
    public async Task 실제상품피킹포장근거가이어지면_읽기전용준비사본을반환한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        db.ChangeTracker.Clear();

        var adapter = new 마트라스트마일원천Adapter(db);
        var first = await adapter.조회Async($" {Reference} ");
        var replay = await adapter.조회Async(Reference);

        Assert.True(first.원천결속됨);
        Assert.True(first.픽업준비완료);
        Assert.Equal(마트라스트마일원천결과Codes.준비완료, first.결과Code);
        Assert.Equal(1, first.출고건수);
        Assert.Equal(2, first.총수량);
        Assert.Equal(PackedAt, first.포장완료시각Utc);
        Assert.Equal(first, replay);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await db.운송원장.AsNoTracking().ToListAsync());
        Assert.Empty(await db.운영체제업무인계.AsNoTracking().ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.AsNoTracking().ToListAsync());
        Assert.Equal("포장완료-일반포장", (await db.입고상품.AsNoTracking().SingleAsync()).상태);
        Assert.Equal(1, await db.재고이력.CountAsync());
    }

    [Theory]
    [InlineData("empty-reference")]
    [InlineData("unknown-reference")]
    [InlineData("missing-order")]
    [InlineData("duplicate-order")]
    [InlineData("empty-seller")]
    [InlineData("empty-orderer")]
    [InlineData("missing-outbound")]
    [InlineData("seller-mismatch")]
    [InlineData("orderer-mismatch")]
    public async Task 기본원천이없거나모호하면_결속하지않는다(string scenario)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var order = await db.마트주문.SingleAsync();
        var outbound = await db.출고예정.SingleAsync();
        switch (scenario)
        {
            case "missing-order": db.마트주문.Remove(order); break;
            case "duplicate-order": db.마트주문.Add(new 마트주문
                { 주문참조번호 = Reference, 판매자UserId = order.판매자UserId, 주문자UserId = order.주문자UserId }); break;
            case "empty-seller": order.판매자UserId = " "; break;
            case "empty-orderer": order.주문자UserId = " "; break;
            case "missing-outbound": db.출고예정.Remove(outbound); break;
            case "seller-mismatch": outbound.판매자UserId = "another-seller"; break;
            case "orderer-mismatch": outbound.주문자UserId = "another-orderer"; break;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reference = scenario == "empty-reference" ? " " : scenario == "unknown-reference" ? "missing" : Reference;
        var result = await new 마트라스트마일원천Adapter(db).조회Async(reference);

        Assert.False(result.원천결속됨);
        Assert.False(result.픽업준비완료);
        Assert.Equal(마트라스트마일원천결과Codes.원천미결속, result.결과Code);
        Assert.Null(result.포장완료시각Utc);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("no-lines")]
    [InlineData("order-cancelled")]
    [InlineData("line-cancelled")]
    [InlineData("line-outbound-mismatch")]
    [InlineData("line-quantity-mismatch")]
    [InlineData("inventory-missing")]
    [InlineData("inventory-sku-mismatch")]
    [InlineData("inventory-not-packed")]
    [InlineData("picking-missing")]
    [InlineData("picking-pending")]
    [InlineData("picking-quantity-mismatch")]
    [InlineData("packing-missing")]
    [InlineData("packing-pending")]
    [InlineData("packing-cancelled")]
    [InlineData("packing-quantity-mismatch")]
    [InlineData("packing-inventory-mismatch")]
    [InlineData("packing-warehouse-mismatch")]
    [InlineData("packing-unknown-outbound")]
    [InlineData("packing-empty-key")]
    [InlineData("packing-completion-missing")]
    [InlineData("packing-completion-old")]
    [InlineData("history-missing")]
    [InlineData("history-quantity-mismatch")]
    [InlineData("history-before-picking")]
    [InlineData("latest-history-invalid")]
    [InlineData("outbound-cancelled")]
    public async Task 준비사본은_취소누락관계오류와낮은포장근거를완료로해석하지않는다(string scenario)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var order = await db.마트주문.Include(item => item.상품목록).SingleAsync();
        var line = order.상품목록.Single();
        var outbound = await db.출고예정.SingleAsync();
        var inventory = await db.입고상품.SingleAsync();
        var picking = await db.피킹포장작업.SingleAsync(item => item.작업유형 == 피킹포장작업유형.피킹);
        var packing = await db.피킹포장작업.SingleAsync(item => item.작업유형 == 피킹포장작업유형.포장);
        var history = await db.재고이력.SingleAsync();
        switch (scenario)
        {
            case "no-lines": db.마트주문상품.Remove(line); break;
            case "order-cancelled": order.상태 = "취소"; break;
            case "line-cancelled": line.상태 = "취소"; break;
            case "line-outbound-mismatch": line.출고예정Id = 99; break;
            case "line-quantity-mismatch": line.수량 = 1; break;
            case "inventory-missing": db.입고상품.Remove(inventory); break;
            case "inventory-sku-mismatch": inventory.SKU = "OTHER"; break;
            case "inventory-not-packed": inventory.상태 = "적재완료"; break;
            case "picking-missing": db.피킹포장작업.Remove(picking); break;
            case "picking-pending": picking.상태 = 피킹포장작업상태.대기; break;
            case "picking-quantity-mismatch": picking.수량 = 1; break;
            case "packing-missing": db.피킹포장작업.Remove(packing); break;
            case "packing-pending": packing.상태 = 피킹포장작업상태.대기; break;
            case "packing-cancelled": packing.상태 = 피킹포장작업상태.취소; break;
            case "packing-quantity-mismatch": packing.수량 = 1; break;
            case "packing-inventory-mismatch": packing.입고상품Id = 99; break;
            case "packing-warehouse-mismatch": packing.창고Id = 99; break;
            case "packing-unknown-outbound": packing.출고예정Id = 99; break;
            case "packing-empty-key": packing.작업Key = string.Empty; break;
            case "packing-completion-missing": packing.완료일시Utc = null; break;
            case "packing-completion-old": packing.완료일시Utc = PackedAt.AddSeconds(-1); break;
            case "history-missing": db.재고이력.Remove(history); break;
            case "history-quantity-mismatch": history.변경후수량 = 1; break;
            case "history-before-picking": history.처리일시 = PickedAt.AddSeconds(-1); break;
            case "latest-history-invalid": db.재고이력.Add(new 재고이력
                { 입고상품Id = inventory.Id, 이력유형 = "포장", 변경후수량 = 1, 처리일시 = PackedAt.AddMinutes(1) }); break;
            case "outbound-cancelled": outbound.상태 = 출고상태.취소; break;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new 마트라스트마일원천Adapter(db).조회Async(Reference);

        Assert.True(result.원천결속됨);
        Assert.False(result.픽업준비완료);
        Assert.Equal(마트라스트마일원천결과Codes.준비대기, result.결과Code);
        Assert.Null(result.포장완료시각Utc);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task 같은입고상품을여러출고가참조하면_포장수량을중복인정하지않고준비대기로보류한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var order = await db.마트주문.Include(item => item.상품목록).SingleAsync();
        db.마트주문상품.Add(new 마트주문상품
        { Id = 2, 마트주문Id = order.Id, 출고예정Id = 2, 상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2 });
        db.출고예정.Add(new 출고예정
        {
            Id = 2, 주문참조번호 = Reference, 판매자UserId = "private-seller", 주문자UserId = "private-orderer",
            출고창고Id = 1, 입고상품Id = 1, 상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 상태 = 출고상태.준비중
        });
        db.피킹포장작업.AddRange(new 피킹포장작업
        {
            Id = 3, 작업Key = "pick-2", 라인Key = "line-2", 작업유형 = 피킹포장작업유형.피킹,
            상태 = 피킹포장작업상태.완료, 출고예정Id = 2, 입고상품Id = 1, 창고Id = 1, 주문참조번호 = Reference,
            상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 완료일시Utc = PickedAt
        }, new 피킹포장작업
        {
            Id = 4, 작업Key = "pack-2", 라인Key = "line-2", 작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.완료, 출고예정Id = 2, 입고상품Id = 1, 창고Id = 1, 주문참조번호 = Reference,
            상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 완료일시Utc = PackedAt
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // 개별 출고의 피킹·포장 근거는 맞아도 단일 재고의 수량 2를 총수량 4로 반복 인정하면 안 됩니다.
        Assert.Null(await 마트주문피킹완료Guard.주문차단사유Async(db, Reference, CancellationToken.None));
        var result = await new 마트라스트마일원천Adapter(db).조회Async(Reference);

        Assert.True(result.원천결속됨);
        Assert.False(result.픽업준비완료);
        Assert.Equal(마트라스트마일원천결과Codes.준비대기, result.결과Code);
        Assert.Equal(2, result.출고건수);
        Assert.Equal(4, result.총수량);
        Assert.Null(result.포장완료시각Utc);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(2, (await db.입고상품.AsNoTracking().SingleAsync()).가용수량);
        Assert.Equal(1, await db.재고이력.CountAsync());
        Assert.Empty(await db.운송원장.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task 사본직렬화에는_원문식별자상품연락처주소위치가포함되지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var result = await new 마트라스트마일원천Adapter(db).조회Async(Reference);
        var json = JsonSerializer.Serialize(result);

        foreach (var forbidden in new[] { Reference, "private-seller", "private-orderer", "PRIVATE-PRODUCT", "PRIVATE-SKU", "private-worker", "PRIVATE-LOCATION", "PRIVATE-NOTE" })
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        Assert.Equal(6, typeof(마트라스트마일원천상태사본).GetProperties().Length);
        Assert.Single(typeof(마트라스트마일원천상태사본).GetProperties().Where(property => property.PropertyType == typeof(string)));
    }

    [Theory]
    [InlineData(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
    [InlineData(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    [InlineData(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    public async Task 제품기능관문이꺼지면_원천Adapter도호출하지않는다(string disabledFeature)
    {
        await using var db = CreateContext();
        var adapter = new RecordingSource(new(true, true, 마트라스트마일원천결과Codes.준비완료));
        var result = await new MartLastMileDispatchReadinessPolicy(db, new Features(disabledFeature), adapter)
            .EvaluateAsync(Reference, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(MartLastMileDispatchReadinessCodes.FeatureDisabled, result.Code);
        Assert.Equal(0, adapter.CallCount);
    }

    [Theory]
    [InlineData(false, MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData(true, MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable)]
    public async Task 원천사본이준비되어도_제품기사소비자를자동으로활성화하지않는다(bool bound, string expected)
    {
        await using var db = CreateContext();
        var adapter = new RecordingSource(new(bound, bound, bound ? 마트라스트마일원천결과Codes.준비완료 : 마트라스트마일원천결과Codes.원천미결속));
        var result = await new MartLastMileDispatchReadinessPolicy(db, new Features(), adapter)
            .EvaluateAsync(Reference, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(expected, result.Code);
        Assert.Equal(1, adapter.CallCount);
    }

    [Fact]
    public async Task 기존두인자생성호환은_상품없는기본원천을소비자미지원으로유지한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        db.마트주문상품.RemoveRange(db.마트주문상품);
        await db.SaveChangesAsync();
        var result = await new MartLastMileDispatchReadinessPolicy(db, new Features())
            .EvaluateAsync(Reference, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable, result.Code);
    }

    [Fact]
    public async Task 실제준비사본도_제품연결준비완료를뜻하지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var result = await new MartLastMileDispatchReadinessPolicy(db, new Features(), new 마트라스트마일원천Adapter(db))
            .EvaluateAsync(Reference, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable, result.Code);
        Assert.Empty(await db.운송원장.ToListAsync());
    }

    private static SsalddelContext CreateContext() => new(
        new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"mart-source-{Guid.NewGuid():N}").Options,
        new PassThroughEncryption());

    private static async Task SeedAsync(SsalddelContext db)
    {
        db.마트주문.Add(new 마트주문
        {
            Id = 1, 주문참조번호 = Reference, 판매자UserId = "private-seller", 주문자UserId = "private-orderer",
            상태 = "포장 완료", CreatedAt = PickedAt, UpdatedAt = PackedAt,
            상품목록 = [new 마트주문상품
            { Id = 1, 출고예정Id = 1, 상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2 }]
        });
        db.출고예정.Add(new 출고예정
        {
            Id = 1, 주문참조번호 = Reference, 판매자UserId = "private-seller", 주문자UserId = "private-orderer",
            출고창고Id = 1, 입고상품Id = 1, 상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 상태 = 출고상태.준비중
        });
        db.입고상품.Add(new 입고상품
        {
            Id = 1, 창고Id = 1, 상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 가용수량 = 2,
            상태 = "포장완료-일반포장", 소유자UserId = "private-seller", 보관위치 = "PRIVATE-LOCATION"
        });
        db.피킹포장작업.AddRange(new 피킹포장작업
        {
            Id = 1, 작업Key = "pick-1", 라인Key = "line-1", 작업유형 = 피킹포장작업유형.피킹,
            상태 = 피킹포장작업상태.완료, 출고예정Id = 1, 입고상품Id = 1, 창고Id = 1, 주문참조번호 = Reference,
            상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 작업자UserId = "private-worker", 완료일시Utc = PickedAt
        }, new 피킹포장작업
        {
            Id = 2, 작업Key = "pack-1", 라인Key = "line-1", 작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.완료, 출고예정Id = 1, 입고상품Id = 1, 창고Id = 1, 주문참조번호 = Reference,
            상품명 = "PRIVATE-PRODUCT", SKU = "PRIVATE-SKU", 수량 = 2, 작업자UserId = "private-worker", 완료일시Utc = PackedAt
        });
        db.재고이력.Add(new 재고이력
        { Id = 1, 입고상품Id = 1, 이력유형 = "포장", 변경후수량 = 2, 처리일시 = PackedAt, 처리UserId = "private-worker", 메모 = "PRIVATE-NOTE" });
        await db.SaveChangesAsync();
    }

    private sealed class RecordingSource(마트라스트마일원천상태사본 result) : I마트라스트마일원천Adapter
    {
        public int CallCount { get; private set; }
        public Task<마트라스트마일원천상태사본> 조회Async(string 주문참조번호, CancellationToken cancellationToken = default)
        { CallCount++; return Task.FromResult(result); }
    }

    private sealed class Features(string? disabled = null) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => featureKey != disabled;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
