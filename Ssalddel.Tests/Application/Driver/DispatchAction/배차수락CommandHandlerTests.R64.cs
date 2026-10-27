using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Theory]
    [InlineData("상차완료")]
    [InlineData("이동중")]
    [InlineData("하차지도착")]
    [InlineData("인수완료")]
    public async Task R64_진행또는완료한본인업무는_수신Off와자동중지에도멱등복구한다(string state)
    {
        await using var db = CreateContext();
        var (queue, _) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        queue.상태 = state; queue.배차큐단계 = 상태값.배차큐단계.확정;
        queue.확정기사Id = "driver-1"; queue.기사_운송자 = "driver-1";
        queue.출발_픽업 = DateTime.UtcNow.AddMinutes(-20);
        queue.도착 = state == "인수완료" ? DateTime.UtcNow.AddMinutes(-1) : null;
        await db.SaveChangesAsync();
        var before = (queue.상태, queue.출발_픽업, queue.도착, queue.UpdatedAt);
        var eligibility = new R64Eligibility();
        var handler = R64Handler(db, new() { 수신On = false, 자동중지 = true }, eligibility);
        Assert.True((await handler.Handle(new("driver-1", queue.의뢰Id), default)).IsSuccess);
        Assert.Equal(before, (queue.상태, queue.출발_픽업, queue.도착, queue.UpdatedAt));
        Assert.Equal(0, eligibility.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R64_수신Off또는미응답중지는_새수락만막고확정업무재수락은보존한다(bool autoPause)
    {
        await using var db = CreateContext();
        var (queue, _) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var availability = new 운영배차시험수신UseCase { 수신On = autoPause, 자동중지 = autoPause };
        var eligibility = new R64Eligibility();
        var handler = R64Handler(db, availability, eligibility);
        var blocked = await handler.Handle(new("driver-1", queue.의뢰Id), default);
        Assert.True(blocked.IsFailed);
        Assert.Equal("FreightDispatchReceivingOff", Assert.Single(blocked.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(0, eligibility.Calls);
        queue.상태 = 상태값.배차대기상태.확정;
        queue.배차큐단계 = 상태값.배차큐단계.확정;
        queue.확정기사Id = "driver-1";
        queue.기사_운송자 = "driver-1";
        await db.SaveChangesAsync();
        Assert.True((await handler.Handle(new("driver-1", queue.의뢰Id), default)).IsSuccess);
        Assert.Equal(0, eligibility.Calls);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("pause")]
    [InlineData("completed")]
    [InlineData("new-work")]
    public async Task R64_수락적격성비동기검사중조건변경은_새업무를확정하지않는다(string change)
    {
        await using var db = CreateContext();
        var (queue, _) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var active = R64Active("active-a"); db.운송원장.Add(active); await db.SaveChangesAsync();
        var availability = new 운영배차시험수신UseCase();
        var eligibility = new R64Eligibility
        {
            BeforeReturn = async () =>
            {
                await Task.Yield();
                if (change == "off") availability.수신On = false;
                if (change == "pause") availability.자동중지 = true;
                if (change == "completed") active.상태 = 상태값.배차상태.인수완료;
                if (change == "new-work") db.운송원장.Add(R64Active("active-c"));
                await db.SaveChangesAsync();
            }
        };
        var result = await R64Handler(db, availability, eligibility, new 화물후속픽업시험Service())
            .Handle(new("driver-1", queue.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("FreightDispatchConditionsChanged", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(상태값.배차대기상태.대기, queue.상태);
        Assert.Equal(1, eligibility.Calls);
    }

    [Fact]
    public async Task R64_기존업무전체경로근거미확인은_수락시현재위치로대체하지않는다()
    {
        await using var db = CreateContext();
        var (queue, _) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        db.운송원장.Add(R64Active("active-a")); await db.SaveChangesAsync();
        var eligibility = new R64Eligibility();
        var result = await R64Handler(db, new(), eligibility, new 화물후속픽업시험Service { 확인됨 = false })
            .Handle(new("driver-1", queue.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal(화물배차수락오류코드.일정근거부족, Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Equal(0, eligibility.Calls);
        Assert.Null(queue.확정기사Id);
    }

    private static 운송원장 R64Active(string id) => new()
    {
        의뢰Id = id, 운송번호 = id, 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
        상태 = 상태값.배차상태.운송중, 배차업무유형 = 상태값.배차업무유형.용달운송,
        배차큐단계 = 상태값.배차큐단계.확정
    };

    private static 배차수락CommandHandler R64Handler(SsalddelContext db, 운영배차시험수신UseCase availability,
        I화물배차수락적격성Service eligibility, I화물후속픽업기준Service? anchor = null)
        => new(db, new ThrowingPublisher(), new TestCurrentUserAccessor("driver-1", "기사"), new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(), eligibility, new StubContinuityUseCase(), NullLogger<배차수락CommandHandler>.Instance,
            운영배차: availability, 후속픽업: anchor, 기사위치: new 화물현재위치시험Store(),
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

    private sealed class R64Eligibility : I화물배차수락적격성Service
    {
        public Func<Task>? BeforeReturn { get; init; }
        public int Calls { get; private set; }
        public async Task<화물배차수락적격성결과> 평가Async(string id, 화주운송의뢰 request, IReadOnlyCollection<string> warnings, CancellationToken ct = default)
        {
            Calls++;
            if (BeforeReturn is not null) await BeforeReturn();
            return new(true, null, null, [], [], [], null, null);
        }
    }
}
