using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Connections.Commands;
using Ssalddel.Application.Connections.Handlers;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.사용자;

namespace Ssalddel.Tests.Application.Connections;

public sealed class 친구요청응답CommandHandlerTests
{
    [Theory]
    [InlineData("sender-1")]
    [InlineData("other-user")]
    [InlineData("")]
    public async Task 수신자는_타인이나빈식별자명의의_공개동의를저장할수없다(string consentUserId)
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();

        var result = await Handler(fixture.Context, "recipient-1").Handle(
            new 친구요청응답Command(requestId, true, null, Consent(consentUserId)),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("본인 명의", result.Errors.Single().Message);
        await using var read = fixture.NewContext();
        Assert.Equal(친구요청상태.대기, (await read.친구요청.SingleAsync()).상태);
        Assert.Empty(await read.연락처공개동의.ToArrayAsync());
        Assert.Empty(await read.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 요청수신자가아니면_응답과동의및알림을저장하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();

        var result = await Handler(fixture.Context, "sender-1").Handle(
            new 친구요청응답Command(requestId, true, null, Consent("sender-1")),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        await using var read = fixture.NewContext();
        Assert.Equal(친구요청상태.대기, (await read.친구요청.SingleAsync()).상태);
        Assert.Empty(await read.연락처공개동의.ToArrayAsync());
        Assert.Empty(await read.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 본인항목별동의와응답알림을함께저장하고_재응답은알림을중복생성하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();
        var command = new 친구요청응답Command(requestId, true, null, Consent("recipient-1"));

        var result = await Handler(fixture.Context, "recipient-1").Handle(command, CancellationToken.None);
        await using var replayContext = fixture.NewContext();
        var replay = await Handler(replayContext, "recipient-1").Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(replay.IsFailed);
        await using var read = fixture.NewContext();
        var entity = await read.친구요청.SingleAsync();
        Assert.Equal(친구요청상태.수락, entity.상태);
        Assert.NotNull(entity.응답일시);
        var consent = await read.연락처공개동의.SingleAsync();
        Assert.Equal("recipient-1", consent.동의자참여자Id);
        Assert.Equal(requestId, consent.친구요청Id);
        Assert.True(consent.프로필공개);
        Assert.True(consent.이메일공개);
        Assert.False(consent.업체명공개);
        Assert.False(consent.전화번호공개);
        Assert.False(consent.카카오채널공개);
        Assert.False(consent.판매채널공개);
        Assert.Equal("다음 업무 연락", consent.제공목적);
        var outbox = await read.Command알림Outbox.SingleAsync();
        Assert.Equal("인연연결요청응답됨", outbox.EventName);
        Assert.Equal("Connection", outbox.FeatureName);
        Assert.Equal("Participant", outbox.Target);
        Assert.Equal("Pending", outbox.Status);
        using var payload = JsonDocument.Parse(outbox.PayloadJson);
        Assert.Equal(requestId, payload.RootElement.GetProperty("Id").GetInt64());
        Assert.Equal("수락", payload.RootElement.GetProperty("상태").GetString());
    }

    [Fact]
    public async Task 동의없는수락은_연락처공개를자동으로기록하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();

        var result = await Handler(fixture.Context, "recipient-1").Handle(
            new 친구요청응답Command(requestId, true, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var read = fixture.NewContext();
        Assert.Equal(친구요청상태.수락, (await read.친구요청.SingleAsync()).상태);
        Assert.Empty(await read.연락처공개동의.ToArrayAsync());
        Assert.Single(await read.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 거절은_첨부된동의를무시하고_거절과알림만함께저장한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();

        var result = await Handler(fixture.Context, "recipient-1").Handle(
            new 친구요청응답Command(requestId, false, " 지금은 어렵습니다. ", Consent("sender-1")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var read = fixture.NewContext();
        var entity = await read.친구요청.SingleAsync();
        Assert.Equal(친구요청상태.거절, entity.상태);
        Assert.Equal("지금은 어렵습니다.", entity.거절사유);
        Assert.Empty(await read.연락처공개동의.ToArrayAsync());
        Assert.Single(await read.Command알림Outbox.ToArrayAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 알림저장실패는_응답과동의를롤백하고_새요청에서한번만재처리한다(bool accept)
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_connection_response_outbox
            BEFORE INSERT ON "Command_알림_Outbox"
            BEGIN
                SELECT RAISE(ABORT, 'connection response outbox failure');
            END;
            """);
        var command = new 친구요청응답Command(requestId, accept, accept ? null : "거절합니다.",
            accept ? Consent("recipient-1") : null);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Handler(fixture.Context, "recipient-1").Handle(command, CancellationToken.None));

        await using (var read = fixture.NewContext())
        {
            var entity = await read.친구요청.SingleAsync();
            Assert.Equal(친구요청상태.대기, entity.상태);
            Assert.Null(entity.응답일시);
            Assert.Null(entity.거절사유);
            Assert.Empty(await read.연락처공개동의.ToArrayAsync());
            Assert.Empty(await read.Command알림Outbox.ToArrayAsync());
            await read.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_connection_response_outbox;");
        }

        await using var retryContext = fixture.NewContext();
        var retry = await Handler(retryContext, "recipient-1").Handle(command, CancellationToken.None);
        await using var replayContext = fixture.NewContext();
        var replay = await Handler(replayContext, "recipient-1").Handle(command, CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.True(replay.IsFailed);
        await using var completed = fixture.NewContext();
        Assert.Equal(accept ? 친구요청상태.수락 : 친구요청상태.거절,
            (await completed.친구요청.SingleAsync()).상태);
        Assert.Equal(accept ? 1 : 0, await completed.연락처공개동의.CountAsync());
        Assert.Single(await completed.Command알림Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 동시에응답해도_한응답과한알림만저장한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestId = await fixture.SeedAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new 친구요청응답Command(requestId, true, null, null);

        async Task<bool> RespondAsync()
        {
            await start.Task;
            await using var context = fixture.NewContext();
            try
            {
                return (await Handler(context, "recipient-1").Handle(command, CancellationToken.None)).IsSuccess;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
            {
                return false;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
            {
                return false;
            }
        }

        var first = Task.Run(RespondAsync);
        var second = Task.Run(RespondAsync);
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results.Where(x => x));
        await using var read = fixture.NewContext();
        Assert.Equal(친구요청상태.수락, (await read.친구요청.SingleAsync()).상태);
        Assert.Single(await read.Command알림Outbox.ToArrayAsync());
        Assert.Empty(await read.연락처공개동의.ToArrayAsync());
    }

    private static 친구요청응답CommandHandler Handler(SsalddelContext context, string userId)
        => new(context, new User(userId, "CommunityMember"));

    private static 연락처공개동의입력 Consent(string userId)
        => new()
        {
            동의자참여자Id = userId,
            프로필공개 = true,
            이메일공개 = true,
            제공목적 = "다음 업무 연락"
        };

    private sealed class Fixture(SqliteConnection connection, DbContextOptions<SsalddelContext> options)
        : IAsyncDisposable
    {
        public SsalddelContext Context { get; } = new(options, new Encryption());

        public SsalddelContext NewContext() => new(options, new Encryption());

        public static async Task<Fixture> CreateAsync()
        {
            var connectionString = $"Data Source=friend-response-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=1";
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var fixture = new Fixture(connection, new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connectionString).Options);
            await fixture.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<long> SeedAsync()
        {
            var request = new 친구요청
            {
                요청자참여자Id = "sender-1",
                요청자역할 = 살뜰역할유형.기사,
                대상자참여자Id = "recipient-1",
                대상자역할 = 살뜰역할유형.판매자,
                요청목적 = "다음 업무 연락",
                요청메시지 = "함께한 업무에 감사드립니다."
            };
            Context.친구요청.Add(request);
            await Context.SaveChangesAsync();
            return request.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
