using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Data;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Fact]
    public async Task 자격확인연결이없으면_신규수락을확정하지않는다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var result = await QualificationHandler(db, null).Handle(new("driver-1", request.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("Qualification.GuardNotConfigured", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
    }

    [Fact]
    public async Task 검토중기사는_추천을받았어도_수락할수없다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var guard = new QualificationSequence(new 운송자격평가결과(false, "Qualification.Pending", "검토 중", 운송자격Codes.Pending, 1));
        var result = await QualificationHandler(db, guard).Handle(new("driver-1", request.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("Qualification.Pending", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(1, guard.Reads);
    }

    [Fact]
    public async Task 수락검사중자격판본이바뀌면_다시확인하게한다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var guard = new QualificationSequence(ValidQualification(1), ValidQualification(2));
        var result = await QualificationHandler(db, guard).Handle(new("driver-1", request.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("Qualification.RevisionChanged", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(2, guard.Reads);
    }

    [Fact]
    public async Task 수락검사중정지된기사는_확정할수없다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var guard = new QualificationSequence(ValidQualification(1),
            new(false, "Qualification.Suspended", "정지", 운송자격Codes.Suspended, 2));
        var result = await QualificationHandler(db, guard).Handle(new("driver-1", request.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("Qualification.Suspended", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id);
    }

    [Fact]
    public async Task 자격이정지되어도_이미본인이수락한결과는_재조회할수있다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        queue.확정기사Id = "driver-1"; queue.기사_운송자 = "driver-1";
        queue.배차큐단계 = 상태값.배차큐단계.확정; queue.상태 = 상태값.배차대기상태.확정;
        await db.SaveChangesAsync();
        var guard = new QualificationSequence(new 운송자격평가결과(false, "Qualification.Suspended", "정지", 운송자격Codes.Suspended, 2));
        var result = await QualificationHandler(db, guard).Handle(new("driver-1", request.의뢰Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, guard.Reads);
        Assert.Equal("driver-1", (await db.운송원장.SingleAsync()).확정기사Id);
    }

    private static 운송자격평가결과 ValidQualification(int revision) => new(true, "Qualification.Valid", "내부 검토", 운송자격Codes.Valid, revision);
    private static 배차수락CommandHandler QualificationHandler(SsalddelContext db, I신규화물업무자격Guard? guard)
        => new(db, new ThrowingPublisher(), new TestCurrentUserAccessor("driver-1", "기사"), new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(), new StubEligibilityService(), new StubContinuityUseCase(),
            NullLogger<배차수락CommandHandler>.Instance,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용, 신규업무자격: guard);
    private sealed class QualificationSequence(params 운송자격평가결과[] results) : I신규화물업무자격Guard
    {
        public bool RequiresCargoEvidence => false;
        public int Reads { get; private set; }
        public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
            => Task.FromResult(results[Math.Min(Reads++, results.Length - 1)]);
    }
}
