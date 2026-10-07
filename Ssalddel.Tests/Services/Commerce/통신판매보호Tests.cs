using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Services.Commerce;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Commerce;

public sealed class 통신판매보호Tests
{
    [Fact]
    public async Task 기본설정은원장쓰기전에새거래를차단한다()
    {
        var f = new Fixture();
        var guard = f.Guard(new 통신판매운영Options());
        var ex = await Assert.ThrowsAsync<거래보호Exception>(() => guard.요구Async("buyer", "seller", Notice(), "food-order", "request", default));
        Assert.Equal("CommerceOperationsNotReady", ex.Code);
        Assert.Empty(f.Evidence.Rows); Assert.Equal(0, f.Gateway.Calls);
    }

    [Fact]
    public void 검토참조만입력해도보존과달력이미검증이면운영준비가되지않는다()
    {
        var notices = new 통신판매안내Service(Options.Create(ReadyOptions()));
        Assert.False(notices.조회().IsOperationalReady);
        Assert.Contains("실제 보존 작업·백업 복원 차단 검증", notices.운영부족());
        Assert.Contains("분쟁 처리 영업일 달력 검증", notices.운영부족());
    }

    [Theory]
    [InlineData("minor")]
    [InlineData("other-actor")]
    [InlineData("wrong-reference")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("local-time")]
    [InlineData("phone-missing")]
    [InlineData("email-missing")]
    public async Task 확인결과는서버귀속현재시각성년연락처를모두검사한다(string fault)
    {
        var f = new Fixture(); var proof = f.Proof();
        f.Gateway.Proof = fault switch
        {
            "minor" => proof with { IsAdult = false },
            "other-actor" => proof with { ActorUserId = "another" },
            "wrong-reference" => proof with { Reference = "another" },
            "expired" => proof with { ExpiresAtUtc = f.Clock.Now },
            "future" => proof with { VerifiedAtUtc = f.Clock.Now.AddMinutes(1) },
            "local-time" => proof with { VerifiedAtUtc = DateTime.SpecifyKind(f.Clock.Now, DateTimeKind.Unspecified) },
            "phone-missing" => proof with { PhoneVerified = false },
            _ => proof with { EmailVerified = false }
        };
        var ex = await Assert.ThrowsAsync<거래보호Exception>(() => f.Service.확인Async("seller", Verification(0), default));
        Assert.Equal("IdentityNotVerified", ex.Code); Assert.Empty(f.Store.Rows);
    }

    [Fact]
    public async Task 신원기관미연결은자가입력으로확인완료를만들지않는다()
    {
        var f = new Fixture(); f.Gateway.IsConfigured = false;
        Assert.Equal("IdentityProviderUnavailable", (await Assert.ThrowsAsync<거래보호Exception>(() => f.Service.확인Async("seller", Verification(0), default))).Code);
        Assert.Equal(0, f.Gateway.Calls); Assert.Empty(f.Store.Rows);
    }

    [Fact]
    public async Task 개인판매자의확인정보는공개응답에반환하지않는다()
    {
        var f = new Fixture();
        await f.Service.등록Async("seller", Registration(), default);
        f.Gateway.Proof = f.Proof() with { ProfileHash = 판매자확인Service.Hash(f.Store.Rows["seller"].ProfileJson) };
        Assert.Equal("Verified", (await f.Service.확인Async("seller", Verification(1), default)).StatusCode);
        var json = JsonSerializer.Serialize(await f.Service.공개Async("seller", default));
        Assert.DoesNotContain("010-1234-5678", json); Assert.DoesNotContain("seller@example.test", json);
        Assert.DoesNotContain("우리집", json); Assert.Contains("Individual", json);
    }

    [Fact]
    public async Task 판매자정보가바뀌면과거신원확인을재사용하지않는다()
    {
        var f = new Fixture(); await f.Service.등록Async("seller", Registration(), default);
        f.Gateway.Proof = f.Proof() with { ProfileHash = 판매자확인Service.Hash(f.Store.Rows["seller"].ProfileJson) };
        await f.Service.확인Async("seller", Verification(1), default);
        var changed = Registration(); changed.ExpectedRevision = 2; changed.DisplayName = "다른 이름";
        Assert.Equal("Unverified", (await f.Service.등록Async("seller", changed, default)).StatusCode);
        Assert.Null(await f.Service.공개Async("seller", default));
    }

    [Fact]
    public async Task 사업자정보가검증결과와다르면공개확인이되지않는다()
    {
        var f = new Fixture(); var request = Registration(); request.SellerKind = 판매자유형Codes.사업자;
        request.RepresentativeName = "대표"; request.BusinessAddress = "공개 영업 주소";
        request.BusinessRegistrationNumber = "123-45-67890"; request.MailOrderRegistrationNumber = "신고번호";
        await f.Service.등록Async("seller", request, default);
        f.Gateway.Proof = f.Proof() with { ProfileHash = 판매자확인Service.Hash(f.Store.Rows["seller"].ProfileJson), BusinessVerified = true,
            MailOrderStatusVerified = true, BusinessRegistrationNumber = "wrong" };
        Assert.Equal("SellerInformationNotVerified", (await Assert.ThrowsAsync<거래보호Exception>(() => f.Service.확인Async("seller", Verification(1), default))).Code);
        Assert.Null(await f.Service.공개Async("seller", default));
    }

    [Fact]
    public async Task 정보없는성년구매자확인은판매자등록을강제하지않는다()
    {
        var f = new Fixture(); f.Gateway.Proof = f.Proof();
        var result = await f.Service.확인Async("seller", Verification(0), default);
        Assert.True(result.IsAdult); Assert.Equal("IdentityVerified", result.StatusCode);
        Assert.Null(await f.Service.공개Async("seller", default));
    }

    [Fact]
    public async Task 기존고지또는미확인판매자는거래증적을생성하지않는다()
    {
        var f = new Fixture(); f.Store.Rows["buyer"] = new() { UserId = "buyer", Verification = f.Proof() with { ActorUserId = "buyer" } };
        var guard = f.Guard(ReadyOptions()); var notice = Notice(); notice.NoticeVersion = "old";
        Assert.Equal("CommerceNoticeRequired", (await Assert.ThrowsAsync<거래보호Exception>(() => guard.요구Async("buyer", "seller", notice, "order", "r1", default))).Code);
        Assert.Equal("SellerVerificationRequired", (await Assert.ThrowsAsync<거래보호Exception>(() => guard.요구Async("buyer", "seller", Notice(), "order", "r2", default))).Code);
        Assert.Empty(f.Evidence.Rows);
    }

    [Fact]
    public async Task Simulation은운영확인증적을만들지않는다()
    {
        var f = new Fixture(); await f.Guard(new(), simulation: true).요구Async("", "", null, "order", "r", default);
        Assert.Empty(f.Evidence.Rows);
    }

    [Theory]
    [InlineData("sales-offer-publication", null)]
    [InlineData("sales-offer-publication", "")]
    [InlineData("sales-offer-publication", " ")]
    [InlineData("sales-offer-update", null)]
    [InlineData("sales-offer-update", "")]
    [InlineData("sales-offer-update", " ")]
    public async Task 판매글은성년수정자라도계약판매자가없으면고지증적을생성하지않는다(string source, string? seller)
    {
        var f = new Fixture();
        f.Store.Rows["buyer"] = new() { UserId = "buyer", Verification = f.Proof() with { ActorUserId = "buyer" } };
        var ex = await Assert.ThrowsAsync<거래보호Exception>(() => f.Guard(ReadyOptions()).요구Async("buyer", seller, Notice(), source, "r1", default));
        Assert.Equal("SellerVerificationRequired", ex.Code);
        Assert.Empty(f.Evidence.Rows);
    }

    [Fact]
    public async Task 판매자가없는생활배송신청은성년신청자의현재안내확인으로처리한다()
    {
        var f = new Fixture();
        f.Store.Rows["buyer"] = new() { UserId = "buyer", Verification = f.Proof() with { ActorUserId = "buyer" } };
        await f.Guard(ReadyOptions()).요구Async("buyer", null, Notice(), "neighborhood-delivery", "r1", default);
        Assert.Null(Assert.Single(f.Evidence.Rows).SellerUserId);
    }

    [Fact]
    public async Task 확인한판매자판본이달라지면현재조건재확인을요구한다()
    {
        var f = new Fixture();
        f.Store.Rows["buyer"] = new() { UserId = "buyer", Verification = f.Proof() with { ActorUserId = "buyer" } };
        f.Store.Rows["seller"] = new() { UserId = "seller", Revision = 2, SellerKind = 판매자유형Codes.개인, Verification = f.Proof() };
        var guard = f.Guard(ReadyOptions()); var notice = Notice(); notice.SellerRevision = 1;
        Assert.Equal("SellerDisclosureChanged", (await Assert.ThrowsAsync<거래보호Exception>(() => guard.요구Async("buyer", "seller", notice, "order", "r1", default))).Code);
        Assert.Empty(f.Evidence.Rows);
        notice.SellerRevision = 2;
        await guard.요구Async("buyer", "seller", notice, "order", "r2", default);
        var record = Assert.Single(f.Evidence.Rows); Assert.Equal(2, record.SellerRevision);
        Assert.Equal("PreflightVerified", record.StateCode);
    }

    [Fact]
    public async Task 판매자등록재송신과다른내용재사용을구별한다()
    {
        var f = new Fixture(); var request = Registration();
        var first = await f.Service.등록Async("seller", request, default);
        Assert.Equal(first.Revision, (await f.Service.등록Async("seller", request, default)).Revision);
        request.DisplayName = "변경";
        Assert.Equal("IdempotencyConflict", (await Assert.ThrowsAsync<거래보호Exception>(() => f.Service.등록Async("seller", request, default))).Code);
    }

    [Fact]
    public void 외부처리는API키나클라우드이름만으로허용하지않는다()
    {
        var options = new 외부개인정보처리Options(); var guard = new 외부개인정보처리Guard(Options.Create(options));
        Assert.False(guard.허용("AzureBlob"));
        options.Providers["AzureBlob"] = new() { Purpose = "증거", Recipient = "사업자", ProcessingCountry = "실제 지역", DataFields = "사진",
            LegalBasis = "위탁", ContractReviewReference = "검토", NoticeVersion = "v1" };
        Assert.False(guard.허용("AzureBlob"));
        options.Providers["AzureBlob"].TransferReviewReference = "실제 경로 검토";
        Assert.True(guard.허용("AzureBlob")); Assert.False(guard.허용("GoogleGeocoding"));
    }

    private static 거래보호확인Request Notice() => new() { NoticeVersion = 통신판매안내Service.Version, NoticeAccepted = true };
    private static 판매자확인Request Verification(long revision) => new() { ExpectedRevision = revision, VerificationReference = "verified-reference" };
    private static 판매자등록Request Registration() => new() { ClientRequestId = Guid.NewGuid(), SellerKind = 판매자유형Codes.개인, DisplayName = "판매자",
        PhoneNumber = "010-1234-5678", Email = "seller@example.test", BusinessAddress = "우리집", NoticeVersion = 통신판매안내Service.Version, CollectionConsentAccepted = true };
    private static 통신판매운영Options ReadyOptions() => new() { EnableOperationalTransactions = true,
        Operator = new() { 상호 = "운영", 대표자 = "대표", 주소 = "주소", 전화번호 = "전화", 이메일 = "메일", 사업자등록번호 = "번호", 개인정보담당연락처 = "담당" },
        TermsApprovalReference = "검토", PrivacyApprovalReference = "검토", DisputeOperationsReference = "검토", RetentionOperationsReference = "검토", ExternalProcessingReference = "검토",
        RightsExecutionReviewReference = "test-only", SupportRetentionReviewReference = "test-only", PersonalIdentityDisclosureReviewReference = "test-only" };

    private sealed class Fixture
    {
        public Clock Clock { get; } = new(); public Store Store { get; } = new(); public Gateway Gateway { get; } = new(); public Evidence Evidence { get; } = new();
        public 판매자확인Service Service => new(Store, Gateway, Clock);
        public 거래신원확인결과 Proof() => new("seller", "verified-reference", true, true, true, false, false, false,
            "010-1234-5678", "seller@example.test", "", Clock.Now.AddMinutes(-1), Clock.Now.AddDays(1));
        public 통신판매거래Guard Guard(통신판매운영Options options, bool simulation = false)
        {
            var retention = Options.Create(new 개인정보보존Options { Enabled = true, InventoryVerified = true, ProvidersVerified = true, BackupRestoreBarrierVerified = true });
            var calendar = new 한국영업일Calendar(Options.Create(new 보호지원Options { CalendarVersion = "test", CalendarVerifiedFrom = new(2026, 1, 1), CalendarVerifiedThrough = new(2027, 1, 1) }));
            return new(Service, new(Options.Create(options), retention, calendar, Clock, new Rights(), Options.Create(new 보호지원Options
                { ClosedDisputePurgeEnabled = true, ClosedDisputeRetentionPolicyConfirmed = true, ClosedDisputeRetentionPolicyVersion = "internal-closed-dispute-three-years.r1" })), Gateway, Options.Create(options),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = simulation ? SsalddelExecutionMode.Simulation : SsalddelExecutionMode.Operational })), Evidence, Clock);
        }
    }
    private sealed class Clock : TimeProvider { public DateTime Now = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc); public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Rights : I보호지원권리실행확인 { public Task<bool> 완료확인Async(string caseId, string ownerUserId, string requestKind, string evidenceRef, CancellationToken cancellationToken = default) => Task.FromResult(false); }
    private sealed class Gateway : I거래신원확인Gateway
    { public bool IsConfigured { get; set; } = true; public int Calls; public 거래신원확인결과? Proof;
        public Task<거래신원확인결과?> 확인Async(string actorUserId, string reference, CancellationToken cancellationToken) { Calls++; return Task.FromResult(Proof); } }
    private sealed class Store : I판매자확인Store
    {
        public Dictionary<string, 판매자확인Record> Rows { get; } = [];
        private static 판매자확인Record Clone(판매자확인Record row) => JsonSerializer.Deserialize<판매자확인Record>(JsonSerializer.Serialize(row))!;
        public Task<판매자확인Record?> 조회Async(string userId, CancellationToken cancellationToken) => Task.FromResult(Rows.TryGetValue(userId, out var row) ? Clone(row) : null);
        public Task<bool> 저장Async(판매자확인Record row, long expectedRevision, CancellationToken cancellationToken)
        { if ((Rows.GetValueOrDefault(row.UserId)?.Revision ?? 0) != expectedRevision) return Task.FromResult(false); Rows[row.UserId] = Clone(row); return Task.FromResult(true); }
    }
    private sealed class Evidence : I거래고지증적Store { public List<거래고지증적> Rows = []; public Task 기록Async(거래고지증적 row, CancellationToken cancellationToken) { Rows.Add(row); return Task.CompletedTask; } }
}
