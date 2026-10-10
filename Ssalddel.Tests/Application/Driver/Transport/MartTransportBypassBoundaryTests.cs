using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Transport;

/// <summary>격리 SQLite에서 기존 화물 실행기를 직접 호출합니다. 인증 HTTP 시험은 아닙니다.</summary>
public sealed class MartTransportBypassBoundaryTests
{
    [Theory]
    [InlineData(기사운송상태코드.배차확정, 기사운송상태코드.상차지도착)]
    [InlineData(기사운송상태코드.상차지도착, 기사운송상태코드.상차완료)]
    [InlineData(기사운송상태코드.상차완료, 기사운송상태코드.하차지도착)]
    [InlineData(기사운송상태코드.하차지도착, 기사운송상태코드.인수완료)]
    public async Task 화물상태변경경로는_마트전표와현재시도검증을우회할수없다(string beforeState, string nextState)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using var db = new SsalddelContext(options, new 통과암호화());
        await db.Database.EnsureCreatedAsync();
        var transport = new 운송원장
        {
            운송번호 = "mart-bypass-transport", 의뢰Id = "mart-bypass-request", 화주Id = "mart-seller",
            원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문, 원본의뢰Id = "mart-bypass-request",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            기사_운송자 = "mart-driver", 확정기사Id = "mart-driver", 상태 = beforeState,
            UpdatedAt = new DateTime(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc)
        };
        db.운송원장.Add(transport);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.운송원장.AsNoTracking().SingleAsync());
        var publisher = new 기록Publisher();
        var executor = new 기사운송상태변경CommandExecutor(db, new 기사운송상태전이Service(),
            publisher, new 현재기사(), new 참여자실행권한검사(), NullLogger<기사운송상태변경CommandExecutor>.Instance);

        var result = await executor.실행Async(new 기사운송상태변경요청(
            "mart-driver", transport.Id, "mart-driver", 살뜰역할유형.기사,
            nextState, "MartBypassProbe", context => new 시험Event(context.운송.Id)), default);

        Assert.True(result.IsFailed);
        Assert.Equal("MartDriverWorkflowRequired", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Empty(publisher.Events);
        Assert.Empty(await db.운송이벤트.ToListAsync());
        Assert.Empty(await db.음식배달시도.ToListAsync());
        await using var reloaded = new SsalddelContext(options, new 통과암호화());
        Assert.Equal(before, JsonSerializer.Serialize(await reloaded.운송원장.AsNoTracking().SingleAsync()));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private sealed class 통과암호화 : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class 현재기사 : ICurrentUserAccessor
    {
        public string? UserId => "mart-driver";
        public string? Role => 역할명.기사;
    }
    private sealed record 시험Event(long TransportId) : INotification;
    private sealed class 기록Publisher : IPublisher
    {
        public List<object> Events { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        { Events.Add(notification); return Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);
    }
}
