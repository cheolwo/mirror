using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Operations;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Operations;

/// <summary>격리 SQLite 서비스 시험. 실제 인증 HTTP·외부 저장소·기기·운영 활성화 증거가 아닙니다.</summary>
public sealed class 마트기사업무ServiceTests
{
    private const string Ref = "MART-R58-GUARDS";
    private const string Driver = "r58-guard-driver";
    private const string Staff = "r58-guard-staff";
    private static readonly DateTime Initial = new(2026, 10, 10, 7, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation, false, null)]
    [InlineData(true, SsalddelExecutionMode.Operational, false, null)]
    [InlineData(true, SsalddelExecutionMode.Simulation, true, null)]
    [InlineData(true, SsalddelExecutionMode.Simulation, false, VersionFeatureFlagKeys.SsalddelMartWorkflow)]
    [InlineData(true, SsalddelExecutionMode.Simulation, false, VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    [InlineData(true, SsalddelExecutionMode.Simulation, false, VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    public void 독립옵션과Simulation과세기능을모두요구한다(bool enabled, SsalddelExecutionMode mode, bool readOnly, string? disabled)
    {
        var gate = new 마트기사업무Gate(Options.Create(new 마트기사업무Options { Enabled = enabled }),
            Options.Create(new SsalddelExecutionOptions { Mode = mode, DevelopmentReadOnly = readOnly }), new Features(disabled));
        Assert.False(gate.Enabled);
        Assert.False(new 마트기사업무Options().Enabled);
    }

    [Fact]
    public void 명시Simulation과전체기능을켠경우에만허용한다()
        => Assert.True(new 마트기사업무Gate(Options.Create(new 마트기사업무Options { Enabled = true }),
            Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }), new Features()).Enabled);

    [Theory]
    [InlineData("identity")]
    [InlineData("source-type")]
    [InlineData("source-id")]
    [InlineData("seller")]
    [InlineData("member-only")]
    [InlineData("other-driver")]
    public async Task 다른주체나음식원천충돌은기사업무로노출하지않는다(string mismatch)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.Db.운송원장.SingleAsync();
        switch (mismatch)
        {
            case "source-type": queue.원본의뢰유형 = "FoodOrder"; break;
            case "source-id": queue.원본의뢰Id = "another-order"; break;
            case "seller": queue.화주Id = "another-seller"; break;
            case "member-only": queue.일반회원배송전용 = true; break;
            case "other-driver": queue.현재추천대상기사Id = "another-driver"; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.Service().업무조회Async(mismatch == "identity" ? "another-driver" : Driver, Ref);
        Assert.True(result.IsFailed);
        Assert.Empty(await f.Db.Set<마트배송시도>().ToListAsync());
        Assert.Empty(await f.Db.Set<마트기사요청영수증>().ToListAsync());
    }

    [Theory]
    [InlineData("round")]
    [InlineData("updated")]
    [InlineData("expired")]
    [InlineData("receiving")]
    [InlineData("packing")]
    [InlineData("active-limit")]
    public async Task 수락은현재추천과수신의사와준비와상한을검사한다(string mismatch)
    {
        await using var f = await Fixture.CreateAsync();
        var request = await f.AcceptRequestAsync();
        if (mismatch == "round") request.ExpectedRecommendationRound++;
        if (mismatch == "updated") request.ExpectedQueueUpdatedAtUtc = Initial.AddSeconds(-1);
        if (mismatch == "expired") (await f.Db.운송원장.SingleAsync()).추천만료시각 = Initial;
        if (mismatch == "packing") (await f.Db.피킹포장작업.SingleAsync(x => x.작업유형 == 피킹포장작업유형.포장)).상태 = 피킹포장작업상태.대기;
        if (mismatch == "active-limit")
            for (var i = 0; i < 3; i++) f.Db.운송원장.Add(new 운송원장 { 의뢰Id = "active-" + i,
                원본의뢰Id = "active-" + i, 화주Id = "seller", 원본의뢰유형 = "FoodOrder",
                배차업무유형 = 상태값.배차업무유형.음식배달, 확정기사Id = Driver,
                배차큐단계 = 상태값.배차큐단계.확정 });
        await f.Db.SaveChangesAsync();
        var result = await f.Service(receivingOff: mismatch == "receiving").수락Async(Driver, Ref, request);
        Assert.True(result.IsFailed);
        Assert.Empty(await f.Db.Set<마트배송시도>().ToListAsync());
        Assert.Empty(await f.Db.Set<마트기사후속Outbox>().ToListAsync());
    }

    [Fact]
    public async Task 전표GET은발급하지않고포장전발급은픽업을허용하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.True((await f.StaffService().전표조회Async(Ref)).IsFailed);
        Assert.Empty(await f.Db.Set<마트포장전표>().ToListAsync());
        var accepted = await f.AcceptAsync();
        (await f.Db.피킹포장작업.SingleAsync(x => x.작업유형 == 피킹포장작업유형.포장)).상태 = 피킹포장작업상태.대기;
        await f.Db.SaveChangesAsync();
        var issued = await f.StaffService().전표발급Async(Ref, new() { ClientRequestId = Guid.NewGuid() });
        Assert.True(issued.IsSuccess);
        var pickup = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = issued.Value.Revision, Code = issued.Value.QrCode });
        Assert.True(pickup.IsFailed);
        Assert.Equal("MartDriverPreparationNotReady", pickup.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(2, (await f.Db.입고상품.AsNoTracking().SingleAsync()).가용수량);
        Assert.Empty(await f.Db.재고이동.ToListAsync());
    }

    [Fact]
    public async Task 창고일부에만접근가능한담당자는전체주문전표를조회하거나발급할수없다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.창고.Add(new 창고 { Id = 2, 소유자UserId = "other-owner", 창고명 = "다른 창고" });
        f.Db.출고예정.Add(new 출고예정 { 주문참조번호 = Ref, 판매자UserId = "seller", 주문자UserId = "orderer", 출고창고Id = 2 });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.StaffService().전표발급Async(Ref, new() { ClientRequestId = Guid.NewGuid() })).IsFailed);
        Assert.True((await f.StaffService().전표조회Async(Ref)).IsFailed);
        Assert.Empty(await f.Db.Set<마트포장전표>().ToListAsync());
    }

    [Fact]
    public async Task 잘못된번호재전송은한번만기록하고변경된의미는충돌한다()
    {
        await using var f = await Fixture.CreateAsync();
        var accepted = await f.AcceptAsync(); var label = await f.IssueAsync();
        var wrong = label.ConfirmationCode == "0000" ? "0001" : "0000";
        var input = new 마트기사픽업요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = label.Revision, Code = wrong, ByNumber = true };
        var first = await f.Service().픽업Async(Driver, Ref, input);
        var replay = await f.Service().픽업Async(Driver, Ref, input);
        Assert.True(first.IsFailed); Assert.True(replay.IsFailed);
        Assert.Equal("MartPickupCodeMismatch", (await f.Service().요청결과조회Async(input.ClientRequestId)).Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(1, (await f.Db.Set<마트포장전표>().AsNoTracking().SingleAsync()).확인실패횟수);
        input.Code = label.ConfirmationCode;
        Assert.Equal("MartDriverRequestConflict", (await f.Service().픽업Async(Driver, Ref, input)).Errors.Single().Metadata["ErrorCode"]);
        var receipt = await f.Db.Set<마트기사요청영수증>().AsNoTracking().SingleAsync(x => x.클라이언트요청Id == input.ClientRequestId);
        var receiptJson = JsonSerializer.Serialize(receipt);
        Assert.DoesNotContain(label.QrCode, receiptJson); Assert.DoesNotContain("private-address", receiptJson);
        Assert.DoesNotContain(wrong, receipt.보호입력Digest);
        Assert.Equal(1, (await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task 번호다섯번오류는번호만잠그고현재QR은계속검증한다()
    {
        await using var f = await Fixture.CreateAsync();
        var accepted = await f.AcceptAsync(); var label = await f.IssueAsync();
        for (var i = 0; i < 5; i++) Assert.True((await f.Service().픽업Async(Driver, Ref, new()
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision, ExpectedLabelRevision = label.Revision,
            ByNumber = true, Code = label.ConfirmationCode == "0000" ? "0001" : "0000" })).IsFailed);
        var blocked = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = label.Revision, ByNumber = true, Code = label.ConfirmationCode });
        Assert.Equal(429, blocked.Errors.Single().Metadata["StatusCode"]);
        var qr = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = label.Revision, Code = label.QrCode });
        Assert.True(qr.IsSuccess); Assert.Equal("PickedUp", qr.Value.StateCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 낮은시도또는이전전표판본은픽업하지않는다(bool attemptRevision)
    {
        await using var f = await Fixture.CreateAsync(); var accepted = await f.AcceptAsync(); var label = await f.IssueAsync();
        var result = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(),
            ExpectedRevision = attemptRevision ? 0 : accepted.Revision, ExpectedLabelRevision = attemptRevision ? label.Revision : 0, Code = label.QrCode });
        Assert.True(result.IsFailed);
        Assert.Equal(2, (await f.Db.입고상품.AsNoTracking().SingleAsync()).가용수량);
        Assert.True((await f.Db.Set<마트포장전표>().AsNoTracking().SingleAsync()).활성여부);
    }

    [Fact]
    public async Task 픽업저장실패는재고출고시도영수증후속과사건을전부되돌린다()
    {
        await using var f = await Fixture.CreateAsync(); var accepted = await f.AcceptAsync(); var label = await f.IssueAsync();
        var beforeReceipts = await f.Db.Set<마트기사요청영수증>().CountAsync();
        var beforeOutbox = await f.Db.Set<마트기사후속Outbox>().CountAsync();
        var beforeEvents = await f.Db.운송이벤트.CountAsync();
        var beforeQueue = JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync());
        f.Interceptor.FailPickup = true;
        var result = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = label.Revision, Code = label.QrCode });
        Assert.True(result.IsFailed);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(beforeQueue, JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync()));
        Assert.Equal(2, (await f.Db.입고상품.SingleAsync()).가용수량);
        Assert.Equal(출고상태.준비중, (await f.Db.출고예정.SingleAsync()).상태);
        Assert.Equal(1, (await f.Db.Set<마트배송시도>().SingleAsync()).Revision);
        Assert.True((await f.Db.Set<마트포장전표>().SingleAsync()).활성여부);
        Assert.Equal(beforeReceipts, await f.Db.Set<마트기사요청영수증>().CountAsync());
        Assert.Equal(beforeOutbox, await f.Db.Set<마트기사후속Outbox>().CountAsync());
        Assert.Equal(beforeEvents, await f.Db.운송이벤트.CountAsync());
        Assert.Empty(await f.Db.재고이동.ToListAsync());
    }

    [Fact]
    public async Task 전달은저장된픽업증빙을확인하고준비Adapter를다시요구하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); var accepted = await f.AcceptAsync(); var label = await f.IssueAsync();
        var picked = await f.Service().픽업Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision,
            ExpectedLabelRevision = label.Revision, Code = label.QrCode });
        Assert.True(picked.IsSuccess);
        var delivered = await f.Service(adapter: new ThrowingSource()).전달Async(Driver, Ref, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = picked.Value.Revision });
        Assert.True(delivered.IsSuccess); Assert.Equal("Delivered", delivered.Value.StateCode);
        Assert.Equal(상태값.배차상태.하차완료, (await f.Db.운송원장.AsNoTracking().SingleAsync()).상태);
        Assert.Equal("전달 완료", (await f.Db.마트주문.AsNoTracking().SingleAsync()).상태);
        Assert.Empty(await f.Db.음식배달시도.ToListAsync()); Assert.Empty(await f.Db.기사지급Outbox.ToListAsync());
        Assert.Equal(3, await f.Db.운송이벤트.CountAsync());
    }

    [Fact]
    public async Task 정상거절은타기사의후속주소를노출하지않고다음추천의도만예약한다()
    {
        await using var f = await Fixture.CreateAsync(); var offered = await f.AcceptRequestAsync();
        var input = new 마트기사거절요청 { ClientRequestId = offered.ClientRequestId, ExpectedRecommendationRound = offered.ExpectedRecommendationRound,
            ExpectedQueueUpdatedAtUtc = offered.ExpectedQueueUpdatedAtUtc };
        Assert.True((await f.Service().거절Async(Driver, Ref, input)).IsSuccess);
        var queue = await f.Db.운송원장.SingleAsync(); queue.확정기사Id = "another-driver"; queue.하차_도로명주소 = "private-other-address";
        await f.Db.SaveChangesAsync();
        var receipt = await f.Service().요청결과조회Async(input.ClientRequestId);
        Assert.True(receipt.IsSuccess); Assert.Equal("Rejected", receipt.Value.StateCode); Assert.True(receipt.Value.IsReplay);
        Assert.DoesNotContain("private-other-address", JsonSerializer.Serialize(receipt.Value));
        Assert.Contains(await f.Db.Set<마트기사후속Outbox>().ToListAsync(), x => x.종류Code == "AdvanceRecommendation");
        Assert.Empty(await f.Db.Set<마트배송시도>().ToListAsync());
    }

    private sealed record Actor(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Gate(bool enabled = true) : I마트기사업무Gate { public bool Enabled => enabled; }
    private sealed class Features(string? disabled = null) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string key) => key != disabled;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Initial); }
    private sealed class ProtectedData : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "test:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value[5..]));
    }
    private sealed class ThrowingSource : I마트라스트마일원천Adapter
    {
        public Task<마트라스트마일원천상태사본> 조회Async(string reference, CancellationToken ct = default)
            => throw new InvalidOperationException("출고 이후 준비 Adapter를 호출하면 안 됩니다.");
    }
    private sealed class PickupFailureInterceptor : SaveChangesInterceptor
    {
        public bool FailPickup { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailPickup && eventData.Context!.ChangeTracker.Entries<재고이력>().Any(x => x.State == EntityState.Added && x.Entity.이력유형 == "출고"))
            { FailPickup = false; throw new DbUpdateException("injected-store-failure"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db, PickupFailureInterceptor interceptor) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public PickupFailureInterceptor Interceptor { get; } = interceptor;
        public 마트기사업무Service Service(bool receivingOff = false, I마트라스트마일원천Adapter? adapter = null)
            => new(Db, new Actor(Driver, 역할명.기사), new Gate(), adapter ?? new 마트라스트마일원천Adapter(Db), new ProtectedData(),
                receivingOff ? new TestDispatchAvailability(Ssalddel.Contracts.Common.Dispatch.운영배차수신의사Code.Off) : new TestDispatchAvailability(), new Clock());
        public 마트기사업무Service StaffService() => new(Db, new Actor(Staff, 역할명.창고관리자), new Gate(),
            new 마트라스트마일원천Adapter(Db), new ProtectedData(), new TestDispatchAvailability(), new Clock());
        public async Task<마트기사수락요청> AcceptRequestAsync()
        { var q = await Db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == Ref); return new() { ClientRequestId = Guid.NewGuid(),
            ExpectedRecommendationRound = q.추천라운드, ExpectedQueueUpdatedAtUtc = DateTime.SpecifyKind(q.UpdatedAt, DateTimeKind.Utc) }; }
        public async Task<마트기사업무항목Dto> AcceptAsync()
        { var result = await Service().수락Async(Driver, Ref, await AcceptRequestAsync()); Assert.True(result.IsSuccess, string.Join(",", result.Errors)); return result.Value; }
        public async Task<마트포장전표Dto> IssueAsync()
        { var result = await StaffService().전표발급Async(Ref, new() { ClientRequestId = Guid.NewGuid() }); Assert.True(result.IsSuccess); return result.Value; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var interceptor = new PickupFailureInterceptor();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).AddInterceptors(interceptor).Options, new ProtectedData());
            await db.Database.EnsureCreatedAsync();
            db.창고.Add(new 창고 { Id = 1, 소유자UserId = Staff, 창고명 = "격리 시험 창고" });
            db.입고상품.Add(new 입고상품 { Id = 1, 창고Id = 1, 소유자UserId = "seller", 판매자UserId = "seller", 상품명 = "상품", SKU = "R58-GUARD",
                가용수량 = 2, 입고수량 = 2, 상태 = "포장완료-일반포장" });
            db.출고예정.Add(new 출고예정 { Id = 1, 주문참조번호 = Ref, 운송의뢰Id = Ref, 출고창고Id = 1, 입고상품Id = 1,
                판매자UserId = "seller", 주문자UserId = "orderer", 상품명 = "상품", SKU = "R58-GUARD", 수량 = 2, 상태 = 출고상태.준비중 });
            db.마트주문.Add(new 마트주문 { 주문참조번호 = Ref, 판매자UserId = "seller", 주문자UserId = "orderer", 상태 = "포장 완료",
                상품목록 = [new 마트주문상품 { 출고예정Id = 1, 상품명 = "상품", SKU = "R58-GUARD", 수량 = 2 }] });
            foreach (var type in new[] { 피킹포장작업유형.피킹, 피킹포장작업유형.포장 })
                db.피킹포장작업.Add(new 피킹포장작업 { 작업Key = "guard:" + type, 라인Key = "guard-line", 작업유형 = type, 상태 = 피킹포장작업상태.완료,
                    출고예정Id = 1, 입고상품Id = 1, 창고Id = 1, 주문참조번호 = Ref, 상품명 = "상품", SKU = "R58-GUARD", 수량 = 2, 완료일시Utc = Initial.AddMinutes(-1) });
            db.재고이력.Add(new 재고이력 { 입고상품Id = 1, 이력유형 = "포장", 변경후수량 = 2, 처리일시 = Initial.AddMinutes(-1) });
            db.운송원장.Add(new 운송원장 { 의뢰Id = Ref, 원본의뢰Id = Ref, 원본의뢰유형 = "SsalddelMartPackedOrder", 화주Id = "seller",
                배차업무유형 = 상태값.배차업무유형.음식배달, 상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천,
                배차노출상태 = 상태값.배차노출상태.추천중, 현재추천대상기사Id = Driver, 추천라운드 = 1, 추천만료시각 = Initial.AddMinutes(10),
                UpdatedAt = Initial, CreatedAt = Initial, 픽업_도로명주소 = "private-address-pickup", 하차_도로명주소 = "private-address-dropoff" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            return new(connection, db, interceptor);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
