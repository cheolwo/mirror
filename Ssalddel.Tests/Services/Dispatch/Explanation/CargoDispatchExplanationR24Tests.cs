using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Explanation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Explanation;

/// <summary>실제 서비스/Recorder와 격리 메모리 DB·의견 저장소의 계약 회귀. 실 Mongo/HTTP/기기·발송 증거가 아니다.</summary>
public sealed partial class CargoDispatchExplanationR24Tests
{
    private static readonly DateTime Now = new(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc);
    private const string Owner = "private-owner-user-r24";
    private const string Orderer = "private-orderer-user-r24";
    private const string Driver = "private-driver-user-r24";

    [Fact]
    public async Task Recorder는직접저장하지않고_호출자의저장집합에설명사건만추가한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.ReserveAsync();

        Assert.NotNull(row);
        Assert.Equal(EntityState.Added, f.Db.Entry(row!).State);
        Assert.Equal(0, await f.Db.운송이벤트.AsNoTracking().CountAsync());
        Assert.StartsWith("protected:", row.메타데이터);
        Assert.DoesNotContain(Driver, row.메타데이터, StringComparison.Ordinal);

        await f.Db.SaveChangesAsync();
        Assert.Equal(1, await f.Db.운송이벤트.AsNoTracking().CountAsync());
        Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
    }

    [Fact]
    public async Task 접수입력이바뀌어도_과거중량과마감설명은변하지않고_비공개행위자나알고리즘원문은응답에없다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var metadata = row.메타데이터;
        var first = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
        var originalFacts = JsonSerializer.Serialize(first.Facts);

        f.Request.화물중량Kg = 990m;
        f.Request.픽업_시간창_종료일시 = Now.AddHours(9);
        f.Request.하차_시간창_종료일시 = Now.AddHours(10);
        f.Request.픽업_도로명주소 = "changed-private-address-r24";
        await f.Db.SaveChangesAsync();
        var later = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);

        Assert.Equal(first.EvidenceId, later.EvidenceId);
        Assert.Equal(Now, later.OccurredAtUtc);
        Assert.Equal(originalFacts, JsonSerializer.Serialize(later.Facts));
        Assert.Contains(later.Facts, x => x.Label.Contains("중량", StringComparison.Ordinal) && x.Value.Contains("100", StringComparison.Ordinal));
        Assert.Contains(later.Facts, x => x.Label.Contains("상차", StringComparison.Ordinal));
        Assert.Contains(later.Facts, x => x.Label.Contains("하차", StringComparison.Ordinal));
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        var exposed = JsonSerializer.Serialize(later);
        foreach (var secret in new[] { Owner, Orderer, Driver, "raw-private-algorithm-r24", "private-address-r24", "private-phone-r24" })
            Assert.DoesNotContain(secret, exposed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 같은라운드중복캡처는저장전후모두생략하고_재추천은별도사건으로남는다()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.ReserveAsync();
        Assert.Null(await f.ReserveAsync());
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.ReserveAsync());

        f.Queue.추천라운드 = 2;
        f.Request.화물중량Kg = 200m;
        await f.Db.SaveChangesAsync();
        var second = await f.RecordAsync(Now.AddMinutes(1));
        var response = await f.Service.GetAsync(Owner, f.Request.의뢰Id);

        Assert.Equal(2, response.Items.Count);
        Assert.NotEqual(first!.Id, second.Id);
        Assert.Equal(new[] { 1, 2 }, response.Items.Select(x => x.RecommendationRound).OrderBy(x => x));
        Assert.Equal(2, await f.Db.운송이벤트.CountAsync());
    }

    [Fact]
    public async Task 실패한호출의Added설명만정리하고_다른Added사건과기존저장설명은보존한다()
    {
        await using var f = await Fixture.CreateAsync();
        var reserved = await f.ReserveAsync();
        var unrelated = new 운송이벤트 { 의뢰Id = f.Request.의뢰Id, 이벤트타입 = "unrelated-r24", 메타데이터 = "preserved" };
        f.Db.운송이벤트.Add(unrelated);

        CargoDispatchExplanationRecorder.DiscardUncommitted(f.Db, reserved);

        Assert.Equal(EntityState.Detached, f.Db.Entry(reserved!).State);
        Assert.Equal(EntityState.Added, f.Db.Entry(unrelated).State);
        await f.Db.SaveChangesAsync();
        Assert.Equal("unrelated-r24", (await f.Db.운송이벤트.SingleAsync()).이벤트타입);

        var saved = await f.RecordAsync();
        CargoDispatchExplanationRecorder.DiscardUncommitted(f.Db, saved);
        Assert.Equal(EntityState.Unchanged, f.Db.Entry(saved).State);
        Assert.Equal(2, await f.Db.운송이벤트.CountAsync());
    }

    [Theory]
    [InlineData(Owner)]
    [InlineData(Orderer)]
    [InlineData(Driver)]
    public async Task 현재화주와주문자와해당설명의대상기사만설명을읽는다(string user)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();

        var item = Assert.Single((await f.Service.GetAsync(user, f.Request.의뢰Id)).Items);

        Assert.Equal(row.Id.ToString(CultureInfo.InvariantCulture), item.EvidenceId);
        Assert.Equal(f.Request.의뢰Id, item.RequestId);
        Assert.True(item.CanSubmitFeedback);
    }

    [Fact]
    public async Task 다른사용자는읽거나의견을제출할수없다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var request = Feedback(row);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAsync("foreign-user", f.Request.의뢰Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SubmitFeedbackAsync("foreign-user", f.Request.의뢰Id, request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetFeedbackAsync("foreign-user", f.Request.의뢰Id, request.EvidenceId, request.ClientRequestId));
        Assert.Empty(f.Feedback.Items);
    }

    [Fact]
    public async Task 재배정뒤에도본인의과거설명만읽고_다른기사의설명은보이지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.RecordAsync();
        f.Queue.현재추천대상기사Id = "second-driver";
        f.Queue.추천라운드 = 2;
        await f.Db.SaveChangesAsync();
        var second = await f.Recorder.ReserveAsync(f.Queue, "second-driver", Now.AddMinutes(1), "engine/r24/second", null);
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync("second-driver", f.Request.의뢰Id)).Items);

        Assert.Equal(second!.Id.ToString(CultureInfo.InvariantCulture), item.EvidenceId);
        Assert.Equal(2, item.RecommendationRound);
        Assert.Equal(2, (await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items.Count);
        var prior = Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items);
        Assert.Equal(first.Id.ToString(CultureInfo.InvariantCulture), prior.EvidenceId);
        Assert.Equal(1, prior.RecommendationRound);
        await f.Service.SubmitFeedbackAsync(Driver, f.Request.의뢰Id, Feedback(first));
        Assert.Single(f.Feedback.Items);
    }

    [Fact]
    public async Task 현재원장과입력이있어도_당시기록이없으면재계산하거나새사건을만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();

        var response = await f.Service.GetAsync(Owner, f.Request.의뢰Id);

        Assert.Equal("unrecorded", response.Status);
        Assert.Empty(response.Items);
        Assert.Empty(await f.Db.운송이벤트.AsNoTracking().ToArrayAsync());
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 보호기록이깨졌으면미기록이나샘플로대체하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        // Inject a corrupt newly added fixture; changing an already saved explanation is separately prohibited.
        var row = await f.ReserveAsync();
        row!.메타데이터 = "plain-corrupt-record-r24";
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<CargoDispatchExplanationUnavailableException>(() => f.Service.GetAsync(Owner, f.Request.의뢰Id));
    }

    [Fact]
    public async Task 평문호환보호구현도새설명기록의평문저장과평문읽기를허용하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var plaintextCompatibility = new PlaintextCompatibilityProtection();
        var plaintextRecorder = new CargoDispatchExplanationRecorder(f.Db, plaintextCompatibility);

        await Assert.ThrowsAsync<CargoDispatchExplanationUnavailableException>(() =>
            plaintextRecorder.ReserveAsync(f.Queue, Driver, Now, "engine/r24/plaintext", null));
        Assert.Empty(f.Db.운송이벤트.Local);
        Assert.Empty(await f.Db.운송이벤트.AsNoTracking().ToArrayAsync());

        var added = await f.ReserveAsync();
        added!.메타데이터 = new FakeProtection().Unprotect(added.메타데이터)!;
        await f.Db.SaveChangesAsync();
        var service = new CargoDispatchExplanationService(f.Db, plaintextCompatibility, f.Feedback, new Clock());

        await Assert.ThrowsAsync<CargoDispatchExplanationUnavailableException>(() => service.GetAsync(Owner, f.Request.의뢰Id));
    }

    [Theory]
    [InlineData("modify")]
    [InlineData("delete")]
    [InlineData("retype-modify")]
    [InlineData("retype-delete")]
    public async Task 저장된설명의수정삭제와원래타입변경우회를막는다(string mutation)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        if (mutation.StartsWith("retype-", StringComparison.Ordinal)) row.이벤트타입 = "unrelated-r24";
        if (mutation.EndsWith("delete", StringComparison.Ordinal)) f.Db.운송이벤트.Remove(row);
        else row.메타데이터 = "changed-r24";

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 추천만료나거절뒤에도대상기사의당시설명과의견입력을보존한다(bool declined)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        f.Queue.추천만료시각 = Now.AddSeconds(-1);
        if (declined) f.Queue.현재추천대상기사Id = null;
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items);
        var receipt = await f.Service.SubmitFeedbackAsync(Driver, f.Request.의뢰Id, Feedback(row));

        Assert.Equal(row.Id.ToString(CultureInfo.InvariantCulture), item.EvidenceId);
        Assert.True(item.CanSubmitFeedback);
        Assert.Equal("received", receipt.Status);
        Assert.Equal(상태값.배차대기상태.대기, f.Queue.상태);
        Assert.Null(f.Queue.확정기사Id);
    }

    [Fact]
    public async Task 입력지문은보호된설명안에연결하고_사용자응답에는복제하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        const string fingerprint = "private-input-fingerprint-r24";
        var row = await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/private-algorithm",
            new 국내화물배차판정근거 { 입력지문 = fingerprint });
        await f.Db.SaveChangesAsync();

        Assert.DoesNotContain(fingerprint, row!.메타데이터, StringComparison.Ordinal);
        var response = await f.Service.GetAsync(Owner, f.Request.의뢰Id);
        Assert.DoesNotContain(fingerprint, JsonSerializer.Serialize(response), StringComparison.Ordinal);
        Assert.Equal("화물 조율 추천 저장", Assert.Single(response.Items).MethodLabel);
    }

    [Fact]
    public async Task 당시계획적재와본인정차순서는현재원장JSON변경으로재작성하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var facts = new CargoDispatchExplanationDecisionFacts(true, false, 2, 5);
        await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/r24-verified", null, decisionFacts: facts);
        await f.Db.SaveChangesAsync();
        var first = Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items);
        Assert.Contains(first.Facts, x => x.Label == "당시 수행계획" && x.Value == "가능 확인");
        Assert.Contains(first.Facts, x => x.Label == "당시 구간 적재" && x.Value == "가능 미확인");
        Assert.Contains(first.Facts, x => x.Label == "이번 상차 순서" && x.Value == "2번째 정차");
        Assert.Contains(first.Facts, x => x.Label == "이번 하차 순서" && x.Value == "5번째 정차");

        f.Queue.추천판정근거Json = "{\"currentPlanChanged\":true,\"otherDriver\":\"private-other-driver-r24\"}";
        await f.Db.SaveChangesAsync();
        var after = Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items);

        Assert.Equal(JsonSerializer.Serialize(first.Facts), JsonSerializer.Serialize(after.Facts));
        Assert.DoesNotContain("private-other-driver-r24", JsonSerializer.Serialize(after), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 당시판정정보의미기록과미확인과확인을구분한다(bool? fullPlan, bool? loading)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/r24-status", null,
            decisionFacts: new(fullPlan, loading));
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);

        if (fullPlan.HasValue) Assert.Contains(item.Facts, x => x.Label == "당시 수행계획" && x.Value == (fullPlan.Value ? "가능 확인" : "가능 미확인"));
        else Assert.DoesNotContain(item.Facts, x => x.Label == "당시 수행계획");
        if (loading.HasValue) Assert.Contains(item.Facts, x => x.Label == "당시 구간 적재" && x.Value == (loading.Value ? "가능 확인" : "가능 미확인"));
        else Assert.DoesNotContain(item.Facts, x => x.Label == "당시 구간 적재");
        Assert.DoesNotContain(item.Facts, x => x.Label is "이번 상차 순서" or "이번 하차 순서");
    }

    [Fact]
    public async Task 수동추천저장은AI판단으로표현하거나없는검증사실을추가하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "manual/operator-selection-r24", null);
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);

        Assert.Equal("수동 추천 저장", item.MethodLabel);
        Assert.DoesNotContain("AI", item.MethodLabel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(item.Facts, x => x.Label is "당시 수행계획" or "당시 구간 적재" or "이번 상차 순서" or "이번 하차 순서");
    }

    [Fact]
    public async Task 다른의뢰의설명ID를본인의의뢰에붙여의견을제출할수없다()
    {
        await using var f = await Fixture.CreateAsync();
        var otherRequest = new 화주운송의뢰 { 의뢰Id = "other-request-r24", 화주Id = "other-owner" };
        var otherQueue = new 운송원장
        {
            의뢰Id = otherRequest.의뢰Id, 운송번호 = "other-transport-r24", 배차업무유형 = 상태값.배차업무유형.용달운송,
            현재추천대상기사Id = "other-driver", 추천라운드 = 1,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중
        };
        f.Db.화주운송의뢰.Add(otherRequest);
        f.Db.운송원장.Add(otherQueue);
        await f.Db.SaveChangesAsync();
        var otherEvidence = await f.Recorder.ReserveAsync(otherQueue, "other-driver", Now, "engine/test/cross-request", null);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<CargoDispatchExplanationNotFoundException>(() =>
            f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, Feedback(otherEvidence!)));

        Assert.Empty(f.Feedback.Items);
    }

    [Fact]
    public async Task 의견은같은요청내용에한접수만반환하고_응답유실후본인조회로복구한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var request = Feedback(row);
        Assert.Null(await f.Service.GetFeedbackAsync(Owner, f.Request.의뢰Id, request.EvidenceId, request.ClientRequestId));

        var first = await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, request);
        var repeated = await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, request);
        var restored = await f.Service.GetFeedbackAsync(Owner, f.Request.의뢰Id, request.EvidenceId, request.ClientRequestId);

        Assert.Equal("received", first.Status);
        Assert.Equal(first.FeedbackId, repeated.FeedbackId);
        Assert.Equal(first.SubmittedAtUtc, repeated.SubmittedAtUtc);
        Assert.Equal(first.FeedbackId, restored!.FeedbackId);
        var fingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, request.Comment);
        Assert.Equal(fingerprint, first.SubmissionFingerprint);
        Assert.Equal(first.SubmissionFingerprint, repeated.SubmissionFingerprint);
        Assert.Equal(first.SubmissionFingerprint, restored.SubmissionFingerprint);
        Assert.Single(f.Feedback.Items);
        Assert.Single(Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items).MyFeedback);
        Assert.Empty(Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items).MyFeedback);
    }

    [Fact]
    public async Task 같은요청ID에내용이다르면충돌하며_원접수내용과시각을보존한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var request = Feedback(row);
        var receipt = await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, request);
        var originalFingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, request.Comment);
        request.Comment = "다른 내용";
        var changedFingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, request.Comment);

        await Assert.ThrowsAsync<CargoDispatchFeedbackConflictException>(() => f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, request));

        var stored = Assert.Single(f.Feedback.Items);
        Assert.Equal("접수 조건을 확인했습니다.", stored.Comment);
        Assert.Equal(receipt.SubmittedAtUtc, stored.SubmittedAtUtc);
        Assert.Equal(receipt.CategoryCode, request.CategoryCode);
        Assert.Equal(originalFingerprint, receipt.SubmissionFingerprint);
        Assert.NotEqual(originalFingerprint, changedFingerprint);
        var restored = await f.Service.GetFeedbackAsync(Owner, f.Request.의뢰Id, request.EvidenceId, request.ClientRequestId);
        Assert.Equal(originalFingerprint, restored!.SubmissionFingerprint);
        Assert.NotEqual(changedFingerprint, restored.SubmissionFingerprint);
    }

    [Theory]
    [InlineData("oversize")]
    [InlineData("control")]
    [InlineData("newline")]
    [InlineData("category")]
    [InlineData("empty-request-id")]
    public async Task 잘못된의견은저장전에거절한다(string invalid)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var request = Feedback(row);
        if (invalid == "oversize") request.Comment = new string('가', 501);
        if (invalid == "control") request.Comment = "문자\0포함";
        if (invalid == "newline") request.Comment = "줄\n바꿈";
        if (invalid == "category") request.CategoryCode = "unregistered-category";
        if (invalid == "empty-request-id") request.ClientRequestId = Guid.Empty;

        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, request));

        Assert.Empty(f.Feedback.Items);
    }

    [Fact]
    public async Task 의견제출은원배차운임완료와설명사건을변경하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var metadata = row.메타데이터;
        var state = f.Queue.상태;
        var round = f.Queue.추천라운드;
        var fare = f.Request.최종운임;

        await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, Feedback(row));

        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        Assert.Equal(state, f.Queue.상태);
        Assert.Equal(round, f.Queue.추천라운드);
        Assert.Equal(fare, f.Request.최종운임);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 의견저장실패를접수성공으로바꾸지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        f.Feedback.FailWrites = true;

        await Assert.ThrowsAsync<CargoDispatchFeedbackUnavailableException>(() => f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, Feedback(row)));

        Assert.Empty(f.Feedback.Items);
    }

    private static CargoDispatchFeedbackRequest Feedback(운송이벤트 row) => new()
    {
        EvidenceId = row.Id.ToString(CultureInfo.InvariantCulture), ClientRequestId = Guid.NewGuid(),
        CategoryCode = "helpful", Comment = "접수 조건을 확인했습니다."
    };

    private sealed class Fixture(SsalddelContext db, FakeProtection protection, 화주운송의뢰 request, 운송원장 queue) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public 화주운송의뢰 Request { get; } = request;
        public 운송원장 Queue { get; } = queue;
        public MemoryFeedbackStore Feedback { get; } = new();
        public CargoDispatchExplanationRecorder Recorder { get; } = new(db, protection);
        public CargoDispatchExplanationService Service => new(Db, protection, Feedback, new Clock());
        public static async Task<Fixture> CreateAsync()
        {
            var protection = new FakeProtection();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-explanation-r24-{Guid.NewGuid():N}").Options, protection);
            var request = new 화주운송의뢰
            {
                의뢰Id = "request-r24", 화주Id = Owner, 주문자UserId = Orderer, 화물종류 = "상자",
                화물중량Kg = 100m, 운송방식 = "혼적", 화물온도조건 = "상온", 최종운임 = 20_000m,
                픽업_도로명주소 = "private-address-r24", 픽업_연락처_전화번호 = "private-phone-r24",
                픽업_시간창_종료일시 = Now.AddHours(2), 하차_시간창_종료일시 = Now.AddHours(3)
            };
            var queue = new 운송원장
            {
                의뢰Id = request.의뢰Id, 운송번호 = "transport-r24", 배차업무유형 = 상태값.배차업무유형.용달운송,
                현재추천대상기사Id = Driver, 추천라운드 = 1, 추천시작시각 = Now, 추천만료시각 = Now.AddHours(1),
                상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중
            };
            db.화주운송의뢰.Add(request);
            db.운송원장.Add(queue);
            await db.SaveChangesAsync();
            return new(db, protection, request, queue);
        }
        public Task<운송이벤트?> ReserveAsync(DateTime? at = null)
            => Recorder.ReserveAsync(Queue, Driver, at ?? Now, "engine/raw-private-algorithm-r24", null);
        public async Task<운송이벤트> RecordAsync(DateTime? at = null)
        {
            var row = await ReserveAsync(at);
            Assert.NotNull(row);
            await Db.SaveChangesAsync();
            return row!;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    // This is a reversible test substitute, not a cryptographic implementation or production encryption evidence.
    private sealed class FakeProtection : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : value.StartsWith("protected:", StringComparison.Ordinal)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(value[10..])) : throw new InvalidOperationException("TestCipherInvalid");
    }

    private sealed class PlaintextCompatibilityProtection : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class MemoryFeedbackStore : ICargoDispatchFeedbackStore
    {
        public List<CargoDispatchFeedbackStored> Items { get; } = [];
        public bool FailWrites { get; set; }
        public Task<CargoDispatchFeedbackStored> InsertAsync(CargoDispatchFeedbackStored candidate, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (FailWrites) throw new CargoDispatchFeedbackUnavailableException();
            var old = Items.SingleOrDefault(x => x.RequestId == candidate.RequestId && x.EvidenceId == candidate.EvidenceId
                && x.UserId == candidate.UserId && x.ClientRequestId == candidate.ClientRequestId);
            if (old is not null)
            {
                if (old.CategoryCode != candidate.CategoryCode || old.Comment != candidate.Comment) throw new CargoDispatchFeedbackConflictException();
                return Task.FromResult(old);
            }
            Items.Add(candidate);
            return Task.FromResult(candidate);
        }
        public Task<CargoDispatchFeedbackStored?> GetAsync(string requestId, string evidenceId, string userId, Guid clientRequestId, CancellationToken ct = default)
            => Task.FromResult(Items.SingleOrDefault(x => x.RequestId == requestId && x.EvidenceId == evidenceId && x.UserId == userId && x.ClientRequestId == clientRequestId));
        public Task<IReadOnlyList<CargoDispatchFeedbackStored>> ListAsync(string requestId, string evidenceId, string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CargoDispatchFeedbackStored>>(Items.Where(x => x.RequestId == requestId && x.EvidenceId == evidenceId && x.UserId == userId).ToArray());
    }
}
