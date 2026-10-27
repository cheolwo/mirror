using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed class CargoRejectionReasonTests
{
    [Theory]
    [InlineData(null, "NotProvided")]
    [InlineData(" ", "NotProvided")]
    [InlineData("NotProvided", "NotProvided")]
    [InlineData("동선이 맞지 않음", "DistanceBurden")]
    [InlineData(" DistanceBurden ", "DistanceBurden")]
    [InlineData("시간 조건이 맞지 않음", "TimeConstraint")]
    [InlineData("상하차 시간에 맞추기 어려움", "TimeConstraint")]
    [InlineData("다른 업무를 진행 중", "TimeConstraint")]
    [InlineData("TimeConstraint", "TimeConstraint")]
    [InlineData("차량 조건이 맞지 않음", "CapacityUnavailable")]
    [InlineData("차량·적재 조건이 맞지 않음", "CapacityUnavailable")]
    [InlineData("CapacityUnavailable", "CapacityUnavailable")]
    [InlineData("PersonalCircumstance", "PersonalCircumstance")]
    [InlineData("제안 보수가 맞지 않음", "Other")]
    [InlineData("수익성이 낮음", "Other")]
    [InlineData("Other", "Other")]
    [InlineData("010-0000-0000 연락 어려움", "Other")]
    public void 기존문구와코드는_원문대신_허용된사유분류만반환한다(string? reason, string expected)
    {
        Assert.Equal(expected, 배차거절사유Policy.분류(reason));
        Assert.Equal(expected, 운영배차거절사유Code.정규화(배차거절사유Policy.분류(reason)));
    }

    [Fact]
    public async Task 정상거절은_안전한사유를영속화하고_기존사건은바꾸지않는다()
    {
        await using var db = Context();
        await Seed(db);
        var legacy = 운영배차활동사건Factory.거절("driver", "legacy-request", 1, DateTime.UtcNow.AddDays(-1), null);
        db.운영배차활동사건.Add(legacy); await db.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        var handler = new 배차거절CommandHandler(db, new RejectedStore(), Queue(db), publisher,
            new User(), new 참여자실행권한검사(), NullLogger<배차거절CommandHandler>.Instance);

        var result = await handler.Handle(new("driver", "request", "동선이 맞지 않음"), default);

        Assert.True(result.IsSuccess);
        var rows = await db.운영배차활동사건.AsNoTracking().Where(x => x.사건유형Code == "Rejected").ToArrayAsync();
        Assert.Equal("NotProvided", Assert.Single(rows.Where(x => x.제안Id == "legacy-request")).사유Code);
        Assert.Equal("DistanceBurden", Assert.Single(rows.Where(x => x.제안Id == "request")).사유Code);
        Assert.Equal("DistanceBurden", Assert.IsType<배차거절됨Event>(publisher.Notification).사유);
        Assert.Null((await db.운송원장.SingleAsync()).확정기사Id);
    }

    [Theory]
    [InlineData("동선이 맞지 않음", "DistanceBurden")]
    [InlineData("010-0000-0000 연락 어려움", "Other")]
    public async Task 사후이벤트도_같은분류를영속화하고_자유문구를로그에남기지않는다(string raw, string expected)
    {
        await using var db = Context(); await Seed(db);
        var logger = new RecordingLogger<배차거절사후처리EventHandler>();
        var handler = new 배차거절사후처리EventHandler(Queue(db), logger);

        await handler.Handle(new("driver", "request", raw, DateTime.UtcNow, "test-trace"), default);

        var row = await db.운영배차활동사건.AsNoTracking().SingleAsync(x => x.사건유형Code == "Rejected");
        Assert.Equal(expected, row.사유Code);
        Assert.DoesNotContain(raw, string.Join('\n', logger.Messages));
        Assert.Contains("ReasonCode=" + expected, string.Join('\n', logger.Messages));
    }

    private static async Task Seed(SsalddelContext db)
    {
        db.운송원장.Add(new 운송원장 { 의뢰Id = "request", 운송번호 = "request", 상태 = 상태값.배차대기상태.대기,
            배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중, 현재추천대상기사Id = "driver", 추천라운드 = 1,
            추천시작시각 = DateTime.UtcNow, 추천만료시각 = DateTime.UtcNow.AddMinutes(5) });
        await db.SaveChangesAsync();
    }

    // 라운드 한도 뒤의 공개 전환만 사용해 실제 후보·알림·외부 호출 없이 제품의 거절 저장 경로를 검증합니다.
    private static 배차대기원장전환Service Queue(SsalddelContext db) => new(db,
        Options.Create(new 배차큐정책Options { 최대추천라운드 = 1 }), null!, null!, null!, null!);
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-rejection-reason-" + Guid.NewGuid()).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record User : ICurrentUserAccessor { public string? UserId => "driver"; public string? Role => "기사"; }
    private sealed class RejectedStore : IDriverRejectedRequestStore
    {
        public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
    private sealed class RecordingPublisher : IPublisher
    {
        public object? Notification { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default) { Notification = notification; return Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        { Notification = notification; return Task.CompletedTask; }
    }
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
