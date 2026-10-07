using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodDeliveryDisclosureTests
{
    [Fact]
    public async Task 동의는선정기사와추천차수에결속하고_명시철회뒤과거허가로돌아가지않는다()
    {
        await using var fixture = await Fixture.Create();
        Assert.False(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-a", "member-a"));
        var request = Decision();
        var allowed = await fixture.Service.기록Async("delivery-a", request);
        Assert.True(allowed.IsSuccess);
        Assert.True(allowed.Value.Consented);
        Assert.True(allowed.Value.CanRecordConsent);
        Assert.Contains("DeliveryInstructions", allowed.Value.Fields);
        Assert.Equal("driver-a", allowed.Value.ConfirmedDriverId);
        Assert.Equal(fixture.Clock.Now.AddHours(72), allowed.Value.ExpiresAtUtc);
        Assert.True(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-a", "member-a"));
        Assert.False(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-b", "member-a"));
        Assert.False(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-a", "member-b"));
        Assert.True((await fixture.Service.기록Async("delivery-a", request)).IsSuccess);
        Assert.Single(fixture.Db.운송이벤트);
        var withdrawn = await fixture.Service.기록Async("delivery-a", Decision(consented: false));
        Assert.True(withdrawn.IsSuccess);
        Assert.False(withdrawn.Value.Consented);
        Assert.False(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-a", "member-a"));
        Assert.Equal(2, await fixture.Db.운송이벤트.CountAsync());
        Assert.All(await fixture.Db.운송이벤트.ToListAsync(), record =>
        {
            Assert.DoesNotContain("010-", record.메타데이터);
            Assert.DoesNotContain("담당자 실명", record.메타데이터);
            Assert.DoesNotContain("정밀한 실제 주소", record.메타데이터);
        });
    }

    [Theory]
    [InlineData("driver")]
    [InlineData("round")]
    [InlineData("completed")]
    [InlineData("canceled")]
    [InlineData("elapsed")]
    [InlineData("malformed")]
    [InlineData("missing-driver")]
    [InlineData("withdrawn-application-consent")]
    public async Task 기사교체와종결기간만료최신증거손상은_상세정보접근을닫는다(string change)
    {
        await using var fixture = await Fixture.Create();
        Assert.True((await fixture.Service.기록Async("delivery-a", Decision())).IsSuccess);
        switch (change)
        {
            case "driver": (await fixture.Db.운송원장.SingleAsync()).확정기사Id = "driver-b"; break;
            case "round": (await fixture.Db.운송원장.SingleAsync()).추천라운드 = 4; break;
            case "completed": (await fixture.Db.화주운송의뢰.SingleAsync()).배차상태 = 상태값.배차상태.하차완료; break;
            case "canceled": (await fixture.Db.화주운송의뢰.SingleAsync()).상태 = 상태값.의뢰상태.취소; break;
            case "elapsed": fixture.Clock.Now = fixture.Clock.Now.AddHours(73); break;
            case "malformed": fixture.Db.운송이벤트.Add(new 운송이벤트
                { 의뢰Id = "delivery-a", 이벤트타입 = NeighborhoodDeliveryDisclosureNotice.EventType, 메타데이터 = "{broken" }); break;
            case "missing-driver": fixture.Db.용달기사.RemoveRange(fixture.Db.용달기사); break;
            case "withdrawn-application-consent": fixture.Consent.Active = false; break;
        }
        await fixture.Db.SaveChangesAsync();
        Assert.False(await fixture.Service.유효한기사제공동의인가Async("delivery-a", "driver-a", "member-a"));
        Assert.False((await fixture.Service.내상태Async("delivery-a"))!.Consented);
    }

    [Fact]
    public async Task 본인과확정상대와최신문안이없으면동의를만들지않고_같은키의변경결정은409다()
    {
        await using var fixture = await Fixture.Create();
        fixture.Current.UserId = "member-b";
        var other = await fixture.Service.기록Async("delivery-a", Decision());
        Assert.True(other.IsFailed);
        Assert.Equal(404, Assert.Single(other.Errors).Metadata["StatusCode"]);
        Assert.Null(await fixture.Service.내상태Async("delivery-a"));
        fixture.Current.UserId = "member-a";
        var changedDriver = Decision();
        changedDriver.ConfirmedDriverId = "driver-b";
        Assert.Equal("AssignedDriverChanged", Assert.Single((await fixture.Service.기록Async("delivery-a", changedDriver)).Errors).Metadata["ErrorCode"]);
        var oldNotice = Decision();
        oldNotice.NoticeVersion = "old-notice";
        Assert.Equal("InvalidDriverDisclosure", Assert.Single((await fixture.Service.기록Async("delivery-a", oldNotice)).Errors).Metadata["ErrorCode"]);
        Assert.Empty(fixture.Db.운송이벤트);
        var command = Decision();
        Assert.True((await fixture.Service.기록Async("delivery-a", command)).IsSuccess);
        command.Consented = false;
        Assert.Equal("IdempotencyConflict", Assert.Single((await fixture.Service.기록Async("delivery-a", command)).Errors).Metadata["ErrorCode"]);
        Assert.Single(fixture.Db.운송이벤트);
    }

    private static NeighborhoodDeliveryDisclosureRequest Decision(bool consented = true)
        => new() { ClientRequestId = Guid.NewGuid(), ConfirmedDriverId = "driver-a", ExpectedRecommendationRound = 3, Consented = consented };

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("delivery-disclosure-" + Guid.NewGuid()).Options, new Encryption());
        public Current Current { get; } = new();
        public Clock Clock { get; } = new();
        public ApplicationConsent Consent { get; } = new();
        public 생활배송기사정보제공동의Service Service => new(Db, Current, Clock, Consent);
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            fixture.Db.화주운송의뢰.Add(new()
            {
                의뢰Id = "delivery-a", 주문자UserId = "member-a", 화주Id = "member-a",
                클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + Guid.NewGuid().ToString("N"),
                정산메모 = "neighborhood-delivery:v1:fixture:consent:" + Guid.NewGuid().ToString("N"),
                상태 = 상태값.의뢰상태.생성됨, 배차상태 = 상태값.배차상태.배차확정,
                픽업_도로명주소 = "정밀한 실제 주소", 픽업_연락처_이름 = "담당자 실명", 픽업_연락처_전화번호 = "010-0000-0000"
            });
            fixture.Db.운송원장.Add(new()
            {
                의뢰Id = "delivery-a", 화주Id = "member-a", 확정기사Id = "driver-a", 추천라운드 = 3,
                배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.확정,
                배차노출상태 = 상태값.배차노출상태.확정, 상태 = 상태값.배차대기상태.확정
            });
            fixture.Db.용달기사.Add(new() { 기사Id = "driver-a", 기사명 = "선정 기사", 상태 = "활동중" });
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId { get; set; } = "member-a";
        public string? Role => "일반회원";
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class ApplicationConsent : I신청개인정보동의증적Service
    {
        public bool Active { get; set; } = true;
        public Task 유효한동의요구Async(Guid? id, string work, string source, string user, CancellationToken cancellationToken = default)
        {
            Assert.Equal(신청개인정보업무Codes.운송대행, work);
            Assert.Equal(NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, source);
            Assert.Equal("member-a", user);
            Assert.NotNull(id);
            if (!Active) throw new InvalidOperationException("철회된 신청 동의");
            return Task.CompletedTask;
        }
        public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id, string user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response> 철회Async(Guid id, 신청개인정보동의철회Request request, string user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
