using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Driver.Qualification;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Application.Driver.Qualification;

public sealed class 운송자격UseCaseTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(SsalddelExecutionMode.Operational, false, false)]
    [InlineData(SsalddelExecutionMode.Operational, true, false)]
    [InlineData(SsalddelExecutionMode.Simulation, false, false)]
    [InlineData(SsalddelExecutionMode.Simulation, true, true)]
    public async Task 명시적내부Simulation만_미제출자격을우회한다(SsalddelExecutionMode mode, bool bypass, bool expected)
    {
        await using var db = Context();
        var result = await Service(db, mode, bypass).평가Async("driver-1", null);
        Assert.Equal(expected, result.CanReceiveNewWork);
        Assert.Equal(expected, result.IsSimulationBypass);
        Assert.Empty(await db.운송자격.ToArrayAsync());
    }

    [Theory]
    [InlineData(운송자격Codes.Motorcycle, DriverVehicleKinds.Motorcycle, 5)]
    [InlineData(운송자격Codes.FreightVehicle, DriverVehicleKinds.Truck, 8)]
    public async Task 수단별자료제출은_승인이아니며_검토전배차불가(string kind, string vehicleKind, int count)
    {
        await using var db = Context(); await Seed(db, vehicleKind);
        var result = await Service(db).제출Async("driver-1", Submission(kind));
        Assert.True(result.IsSuccess);
        Assert.Equal(운송자격Codes.Pending, result.Value.Status);
        Assert.Equal(count, result.Value.RequiredEvidenceCodes.Count);
        Assert.False(result.Value.Eligibility.CanReceiveNewWork);
        Assert.False(result.Value.OfficialAuthorityVerified);
        if (kind == 운송자격Codes.Motorcycle) Assert.DoesNotContain(운송자격Codes.FreightDriver, result.Value.RequiredEvidenceCodes);
    }

    [Fact]
    public async Task 제출된모든근거의독립검토후_현재실차만_유효하다()
    {
        await using var db = Context(); await Seed(db);
        var service = Service(db);
        var submitted = await service.제출Async("driver-1", Submission());
        var result = await service.검토Async("admin-1", "driver-1", Review(submitted.Value));
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Eligibility.CanReceiveNewWork);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal("admin-1", result.Value.ReviewerId);
        Assert.False(result.Value.OfficialAuthorityVerified);
        Assert.All(result.Value.Evidence, x => Assert.Equal(운송자격Codes.DocumentReview, x.VerificationLevel));
        var row = await db.운송자격.SingleAsync();
        Assert.Contains("Submitted", row.HistoryJson);
        Assert.Contains("admin-1", row.HistoryJson);
    }

    [Fact]
    public async Task 본인의승인_및_체크만한승인_공식조회로위장한승인을거부한다()
    {
        await using var db = Context(); await Seed(db);
        var service = Service(db); var submitted = await service.제출Async("driver-1", Submission());
        var self = await service.검토Async("driver-1", "driver-1", Review(submitted.Value));
        Assert.True(self.IsFailed); Assert.Equal(403, self.Errors[0].Metadata["StatusCode"]);
        Assert.True((await service.검토Async("admin-1", "driver-1", Review(submitted.Value) with { Evidence = [] })).IsFailed);
        Assert.True((await service.검토Async("admin-1", "driver-1", Review(submitted.Value) with
        { Evidence = Review(submitted.Value).Evidence.Select(x => x with { VerificationLevel = 운송자격Codes.OfficialLookup }).ToArray() })).IsFailed);
        Assert.False((await service.평가Async("driver-1", null)).CanReceiveNewWork);
    }

    [Theory]
    [InlineData("driver-license-number")]
    [InlineData("https://public.example/license.png")]
    [InlineData("C:\\private\\photo.jpg")]
    public async Task 원본번호나외부사진URL은_비공개근거참조로받지않는다(string rawReference)
    {
        await using var db = Context(); await Seed(db);
        var request = Submission();
        var changed = request.Evidence.Select((x, i) => i == 0 ? x with { PrivateEvidenceReference = rawReference } : x).ToArray();
        Assert.True((await Service(db).제출Async("driver-1", request with { Evidence = changed })).IsFailed);
        Assert.Empty(await db.운송자격.ToArrayAsync());
    }

    [Fact]
    public async Task 등록한수단보다쉬운기준을임의로선택할수없다()
    {
        await using var db = Context(); await Seed(db, DriverVehicleKinds.Truck);
        var response = await Service(db).제출Async("driver-1", Submission(운송자격Codes.Motorcycle));
        Assert.True(response.IsFailed); Assert.Equal("Qualification.VehicleMismatch", response.Errors[0].Metadata["Code"]);
    }

    [Fact]
    public async Task 증빙기간과재확인일이만료되면_저장상태를소급변경하지않고차단한다()
    {
        await using var db = Context(); await Seed(db); var clock = new Clock(Now);
        var service = Service(db, clock: clock); var submitted = await service.제출Async("driver-1", Submission());
        var approved = await service.검토Async("admin-1", "driver-1", Review(submitted.Value) with { RecheckAtUtc = Now.AddDays(1) });
        Assert.True(approved.IsSuccess);
        clock.Utc = Now.AddDays(2);
        var expired = await service.본인조회Async("driver-1");
        Assert.Equal(운송자격Codes.Expired, expired.Value.Status);
        Assert.False(expired.Value.Eligibility.CanReceiveNewWork);
        Assert.Equal(운송자격Codes.Valid, (await db.운송자격.SingleAsync()).Status);
    }

    [Fact]
    public async Task 기간이명시되지않은자료에는_가짜만료일을만들지않지만_보험의기간누락은승인하지않는다()
    {
        await using var db = Context(); await Seed(db); var service = Service(db);
        var submitted = await service.제출Async("driver-1", Submission());
        var review = Review(submitted.Value);
        var missingInsurance = review with { Evidence = review.Evidence.Select(x => x.RequirementCode == 운송자격Codes.CompulsoryInsurance
            ? x with { ValidUntilUtc = null, NoStatedExpiry = true } : x).ToArray() };
        Assert.True((await service.검토Async("admin-1", "driver-1", missingInsurance)).IsFailed);
        var noExpiryLicense = review with { Evidence = review.Evidence.Select(x => x.RequirementCode == 운송자격Codes.DriverLicense
            ? x with { ValidUntilUtc = null, NoStatedExpiry = true } : x).ToArray() };
        var result = await service.검토Async("admin-1", "driver-1", noExpiryLicense);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Evidence.Single(x => x.RequirementCode == 운송자격Codes.DriverLicense).ValidUntilUtc);
    }

    [Fact]
    public void 자격조회는본인경로이며_관리자검토에는기존서버관리자정책과기능경계를유지한다()
    {
        var owner = typeof(Ssalddel.Controllers.Driver.Profile01.용달기사Controller);
        Assert.DoesNotContain(owner.GetMethod("내운송자격조회")!.GetParameters(), x => x.Name == "driverId");
        Assert.DoesNotContain(owner.GetMethod("운송자격제출")!.GetParameters(), x => x.Name == "driverId");
        var admin = typeof(Ssalddel.Controllers.Admin.Master06.기사관리Controller);
        var auth = Assert.Single(admin.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("서버관리자전용", auth.Policy);
        Assert.Single(admin.GetMethod("운송자격검토")!.GetCustomAttributes(typeof(Ssalddel.ApiMetadata.SsalddelApiFeatureAttribute), true));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("actual")]
    [InlineData("kind")]
    public async Task 승인뒤차량이나수단을바꾸면_이전자격으로추가배차불가(string changed)
    {
        await using var db = Context(); await Seed(db); var service = Service(db);
        var submitted = await service.제출Async("driver-1", Submission());
        Assert.True((await service.검토Async("admin-1", "driver-1", Review(submitted.Value))).IsSuccess);
        if (changed == "profile") (await db.용달기사.SingleAsync()).차량 = "다른 차량";
        if (changed == "actual") (await db.용달기사.SingleAsync()).현재실차참조 = "vehicle:" + Guid.NewGuid();
        if (changed == "kind") (await db.Set<DriverWorkParticipationRecord>().SingleAsync()).VehicleKind = DriverVehicleKinds.Motorcycle;
        await db.SaveChangesAsync();
        Assert.False((await service.평가Async("driver-1", null)).CanReceiveNewWork);
    }

    [Fact]
    public async Task 승인된자료를재제출하면_새검토중으로돌아가며_오래된개정판은충돌한다()
    {
        await using var db = Context(); await Seed(db); var service = Service(db);
        var submitted = await service.제출Async("driver-1", Submission());
        var approved = await service.검토Async("admin-1", "driver-1", Review(submitted.Value));
        var result = await service.제출Async("driver-1", Submission() with { ExpectedRevision = approved.Value.Revision });
        Assert.True(result.IsSuccess); Assert.Equal(운송자격Codes.Pending, result.Value.Status);
        Assert.False(result.Value.Eligibility.CanReceiveNewWork);
        var conflict = await service.검토Async("admin-1", "driver-1", Review(submitted.Value));
        Assert.True(conflict.IsFailed); Assert.Equal("Qualification.RevisionConflict", conflict.Errors[0].Metadata["Code"]);
    }

    [Fact]
    public async Task 정지뒤본인재제출로제한을해제할수없다()
    {
        await using var db = Context(); await Seed(db); var service = Service(db);
        var submitted = await service.제출Async("driver-1", Submission());
        var suspended = await service.검토Async("admin-1", "driver-1", Review(submitted.Value) with { Decision = 운송자격Codes.Suspended });
        Assert.True(suspended.IsSuccess);
        Assert.True((await service.제출Async("driver-1", Submission() with { ExpectedRevision = suspended.Value.Revision })).IsFailed);
        Assert.False((await service.평가Async("driver-1", null)).CanReceiveNewWork);
        var rejected = await service.검토Async("admin-1", "driver-1", Review(suspended.Value) with { Decision = 운송자격Codes.Rejected });
        Assert.True(rejected.IsSuccess);
        Assert.True((await service.제출Async("driver-1", Submission() with { ExpectedRevision = rejected.Value.Revision })).IsSuccess);
    }

    [Fact]
    public async Task 다른기사의조회에는_첫기사의근거나상태가나타나지않는다()
    {
        await using var db = Context(); await Seed(db);
        var service = Service(db); Assert.True((await service.제출Async("driver-1", Submission())).IsSuccess);
        var other = await service.본인조회Async("driver-2");
        Assert.True(other.IsFailed); Assert.Equal(404, other.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 근거이력과선택실차는_persistence에서암호화하고_개정판을동시변경토큰으로쓴다()
    {
        await using var db = Context();
        var model = db.Model.FindEntityType(typeof(운송자격Record))!;
        Assert.True(model.FindProperty(nameof(운송자격Record.Revision))!.IsConcurrencyToken);
        foreach (var property in new[] { nameof(운송자격Record.PrivateDetailsJson), nameof(운송자격Record.HistoryJson) })
        {
            var converter = model.FindProperty(property)!.GetValueConverter()!;
            Assert.Equal("protected:private-value", converter.ConvertToProvider("private-value"));
            Assert.Equal("private-value", converter.ConvertFromProvider("protected:private-value"));
        }
        var vehicleConverter = db.Model.FindEntityType(typeof(용달기사))!.FindProperty(nameof(용달기사.현재실차참조))!.GetValueConverter()!;
        Assert.Equal("protected:vehicle-reference", vehicleConverter.ConvertToProvider("vehicle-reference"));
    }

    [Fact]
    public async Task 동시관리자검토는_과거개정판으로새판정을덮어쓰지못한다()
    {
        var opts = new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("qualification-cas-" + Guid.NewGuid()).Options;
        await using var first = new QualificationTestContext(opts); await Seed(first);
        var submitted = await Service(first).제출Async("driver-1", Submission());
        await using var second = new QualificationTestContext(opts);
        await second.운송자격.SingleAsync();
        Assert.True((await Service(first).검토Async("admin-1", "driver-1", Review(submitted.Value))).IsSuccess);
        var stale = await Service(second).검토Async("admin-2", "driver-1", Review(submitted.Value) with { Decision = 운송자격Codes.Rejected });
        Assert.True(stale.IsFailed); Assert.Equal("Qualification.RevisionConflict", stale.Errors[0].Metadata["Code"]);
        first.ChangeTracker.Clear(); Assert.Equal(운송자격Codes.Valid, (await first.운송자격.SingleAsync()).Status);
    }

    private static 운송자격제출요청 Submission(string kind = 운송자격Codes.FreightVehicle) => new()
    {
        VehicleKind = kind, VehicleProfileValue = "검토 대상 차량",
        ActualVehicleReference = "vehicle:7e2be984-bb71-42ee-b2f9-208dd76fe499",
        TransportOperatorReference = kind == 운송자격Codes.FreightVehicle ? "operator:7e2be984-bb71-42ee-b2f9-208dd76fe499" : "",
        Evidence = 운송자격Codes.RequiredFor(kind).Select(x => new 운송자격근거Dto { RequirementCode = x,
            PrivateEvidenceReference = "private-evidence:" + Guid.NewGuid(), Summary = "비공개 자료 확인 요청" }).ToArray()
    };
    private static 운송자격검토요청 Review(운송자격상세Dto submitted) => new()
    {
        ExpectedRevision = submitted.Revision, Decision = 운송자격Codes.Valid,
        ActualVehicleReference = submitted.ActualVehicleReference, RecheckAtUtc = Now.AddDays(30),
        ReviewReason = "제출된 비공개 서류와 기사 및 실차의 관계를 확인했습니다.",
        Evidence = submitted.Evidence.Select(x => x with { VerificationLevel = 운송자격Codes.DocumentReview,
            ValidFromUtc = Now.AddDays(-1), ValidUntilUtc = Now.AddDays(90),
            ReviewReason = "해당 준비 근거의 적용 대상과 실제 유효기간을 확인했습니다." }).ToArray()
    };
    private static async Task Seed(SsalddelContext db, string vehicleKind = DriverVehicleKinds.Truck)
    {
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 차량 = "검토 대상 차량" });
        db.Set<DriverWorkParticipationRecord>().Add(new() { UserId = "driver-1", VehicleKind = vehicleKind });
        await db.SaveChangesAsync();
    }
    private static SsalddelContext Context() => new QualificationTestContext(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("qualification-" + Guid.NewGuid()).Options);
    private static 운송자격UseCase Service(SsalddelContext db, SsalddelExecutionMode mode = SsalddelExecutionMode.Operational,
        bool bypass = false, Clock? clock = null) => new(db, Options.Create(new 운송자격Options { AllowSimulationBypass = bypass }),
        new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })), clock ?? new Clock(Now));
    private sealed class Clock(DateTime now) : TimeProvider { public DateTime Utc { get; set; } = now; public override DateTimeOffset GetUtcNow() => new(Utc); }
    // 기존의 무암호화 시험 Context와 EF 모델 캐시를 공유하지 않는다.
    private sealed class QualificationTestContext(DbContextOptions<SsalddelContext> options) : SsalddelContext(options, new Protector()) { }
    private sealed class Protector : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + value;
        public string? Unprotect(string? value) => value?.StartsWith("protected:") == true ? value[10..] : value;
    }
}
