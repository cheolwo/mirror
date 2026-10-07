using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원보존조정Tests
{
    [Fact]
    public async Task 의도CAS가실패하면다른원장에는보존변경을쓰지않는다()
    {
        var f = new Fixture(); f.Store.FailIntent = true;
        var error = await Assert.ThrowsAsync<보호지원Exception>(() => f.UseCase.변경Async("case", f.Hold(), true));
        Assert.Equal("RevisionConflict", error.Code);
        Assert.Equal(0, f.Retention.HoldCalls);
        Assert.Null(f.Store.Row.RetentionIntent);
    }

    [Fact]
    public async Task 외부보존실패는영속대기로남고같은명령재시도로완료된다()
    {
        var f = new Fixture(); f.Retention.FailHold = true; var command = f.Hold();
        Assert.Equal("RetentionPending", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.UseCase.변경Async("case", command, true))).Code);
        Assert.NotNull(f.Store.Row.RetentionIntent);
        Assert.False(f.Store.Row.SourceHoldActive);
        Assert.Empty(f.Store.Row.Receipts);
        Assert.True((await f.UseCase.상세Async("case", true)).RetentionPending);
        Assert.Equal(0, await f.Purge.실행Async());
        var result = await f.UseCase.변경Async("case", command, true);
        Assert.False(result.RetentionPending);
        Assert.True(f.Store.Row.SourceHoldActive);
        Assert.Single(f.Store.Row.Receipts);
        Assert.True((await f.UseCase.변경Async("case", command, true)).Replay);
    }

    [Fact]
    public async Task 실제hold뒤완료CAS실패는만료후에도실제hold를읽어이어간다()
    {
        var f = new Fixture(); f.Store.FailCompletion = true; var command = f.Hold();
        await Assert.ThrowsAsync<보호지원Exception>(() => f.UseCase.변경Async("case", command, true));
        Assert.NotNull(f.Retention.HeldReview);
        Assert.NotNull(f.Store.Row.RetentionIntent);
        Assert.Equal(0, await f.Purge.실행Async());
        Assert.Equal(1, f.Retention.HoldCalls);
        f.Store.FailCompletion = false; f.Clock.Now = f.Clock.Now.AddDays(95);
        Assert.Equal(1, await f.Coordinator.대기조정Async());
        Assert.Null(f.Store.Row.RetentionIntent);
        Assert.True(f.Store.Row.SourceHoldActive);
        Assert.Equal(1, f.Retention.HoldCalls);
        Assert.Single(f.Store.Row.Receipts);
        Assert.Single(f.Store.Row.History, x => x.Action == "hold-record");
    }

    [Fact]
    public async Task 해제원장의확인없는null은성공으로표시하지않고다른명령을차단한다()
    {
        var f = new Fixture(); f.Store.Row.SourceHoldActive = true; f.Retention.HeldReview = f.Clock.Now.AddDays(1);
        f.Retention.ReleaseVerified = false;
        var command = f.Hold(); command.Action = "release-hold";
        await Assert.ThrowsAsync<보호지원Exception>(() => f.UseCase.변경Async("case", command, true));
        Assert.True(f.Store.Row.SourceHoldActive);
        Assert.Empty(f.Store.Row.Receipts);
        var different = f.Hold(); different.ExpectedRevision = f.Store.Row.Revision;
        Assert.Equal("RetentionPending", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.UseCase.변경Async("case", different, true))).Code);
        f.Retention.ReleaseVerified = true;
        await f.UseCase.변경Async("case", command, true);
        Assert.False(f.Store.Row.SourceHoldActive);
        Assert.Null(f.Store.Row.RetentionIntent);
        Assert.Single(f.Store.Row.Receipts);
    }

    [Fact]
    public async Task 원장조정worker와파기worker는시뮬레이션에서실행하지않는다()
    {
        var options = Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation });
        var scopes = new FailingScopes(); var policy = new SsalddelExecutionModePolicy(options);
        using var reconcile = new 보호지원보존Worker(scopes, policy, NullLogger<보호지원보존Worker>.Instance);
        using var purge = new 보호지원파기Worker(scopes, Options.Create(new 보호지원Options { ClosedDisputePurgeEnabled = true }),
            NullLogger<보호지원파기Worker>.Instance, policy);
        await reconcile.StartAsync(default); await purge.StartAsync(default);
        await reconcile.StopAsync(default); await purge.StopAsync(default);
        Assert.Equal(0, scopes.Calls);
    }

    [Fact]
    public async Task 새해제의도후뒤늦은옛보존의도는순번Fence가거부한다()
    {
        var f = new Fixture(); var hold = f.Hold();
        await f.UseCase.변경Async("case", hold, true);
        var originalSequence = f.Retention.Applied!.Sequence;
        var release = f.Hold(); release.Action = "release-hold";
        await f.UseCase.변경Async("case", release, true);
        var appliedRelease = f.Retention.Applied;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Retention.보존상태적용Async("case", "food-order", "order",
            originalSequence, true, hold.LegalBasis!, hold.HoldReviewAtUtc));
        Assert.Equal(appliedRelease, f.Retention.Applied);
        Assert.Null(f.Retention.HeldReview);
        Assert.False(f.Store.Row.SourceHoldActive);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Retention Retention { get; } = new();
        public Clock Clock { get; } = new();
        public 보호지원UseCase UseCase { get; }
        public 보호지원보존Service Coordinator { get; }
        public 보호지원파기Service Purge { get; }
        public Fixture()
        {
            var options = Options.Create(new 보호지원Options { ClosedDisputePurgeEnabled = true, ClosedDisputeRetentionPolicyConfirmed = true,
                ClosedDisputeRetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion });
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "admin")], "test"));
            UseCase = new(Store, new Source(), new(options), options, Clock, http, new AdminAuthorization(), retention: Retention);
            Coordinator = new(Store, Retention, Clock); Purge = new(Store, options, Clock);
        }
        public 보호지원CommandRequest Hold() => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Store.Row.Revision,
            Action = "hold-record", LegalBasis = "내부 검토 근거", HoldReviewAtUtc = Clock.Now.AddDays(30) };
    }
    private sealed class Store : I보호지원Store
    {
        public 보호지원Record Row { get; set; } = new() { CaseId = "case", Kind = "transaction-dispute", StatusCode = "closed", Revision = 10,
            AssignedAdminUserId = "admin", OwnerUserId = "owner", PartyUserIds = ["owner"], SourceKind = "food-order", SourceId = "order",
            ClosedAtUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), PurgeAfterAtUtc = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion };
        public bool FailIntent { get; set; }
        public bool FailCompletion { get; set; }
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult<보호지원Record?>(Clone(Row));
        public Task<bool> 생성Async(보호지원Record row, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(보호지원Record row, long expected, CancellationToken ct = default)
        {
            if (expected != Row.Revision || FailIntent && row.RetentionIntent is not null
                || FailCompletion && Row.RetentionIntent is not null && row.RetentionIntent is null) return Task.FromResult(false);
            Row = Clone(row); return Task.FromResult(true);
        }
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? actor, string? kind, int skip, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>([Clone(Row)]);
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>([Clone(Row)]);
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Row.RetentionIntent is not null ? [Clone(Row)] : []);
        public Task<bool> 파기Async(보호지원Record row, DateTime now, CancellationToken ct = default) => throw new InvalidOperationException("Must not purge pending intent");
        private static 보호지원Record Clone(보호지원Record row) => JsonSerializer.Deserialize<보호지원Record>(JsonSerializer.Serialize(row))!;
    }
    private sealed class Retention : I보호지원보존연결
    {
        public DateTime? HeldReview { get; set; }
        public bool FailHold { get; set; }
        public bool ReleaseVerified { get; set; } = true;
        public int HoldCalls { get; set; }
        public 보호지원보존적용? Applied { get; set; }
        public Task 보존정지Async(string id, string kind, string source, string legal, DateTime review, CancellationToken ct = default)
        {
            HoldCalls++; if (FailHold) { FailHold = false; throw new InvalidOperationException("temporary"); }
            HeldReview = review; return Task.CompletedTask;
        }
        public Task 보존정지해제Async(string id, string kind, string source, CancellationToken ct = default) { HeldReview = null; return Task.CompletedTask; }
        public Task<DateTime?> 보존정지조회Async(string id, string kind, string source, CancellationToken ct = default) => Task.FromResult(HeldReview);
        public Task<bool> 보존정지해제확인Async(string id, string kind, string source, CancellationToken ct = default) => Task.FromResult(ReleaseVerified && HeldReview is null);
        public async Task 보존상태적용Async(string id, string kind, string source, long sequence, bool hold, string legal,
            DateTime? review, CancellationToken ct = default)
        {
            if (Applied is { } prior)
            {
                if (sequence < prior.Sequence) throw new InvalidOperationException("RetentionIntentStale");
                if (sequence == prior.Sequence)
                {
                    if (prior.Hold != hold || prior.ReviewAtUtc != review) throw new InvalidOperationException("RetentionIntentConflict");
                    return;
                }
            }
            if (hold) await 보존정지Async(id, kind, source, legal, review!.Value, ct);
            else await 보존정지해제Async(id, kind, source, ct);
            Applied = new(sequence, hold, hold ? review : null);
        }
        public Task<보호지원보존적용?> 보존적용조회Async(string id, string kind, string source, CancellationToken ct = default)
            => Task.FromResult(Applied);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Source : I보호지원SourceResolver
    {
        public Task<보호지원Source?> 조회Async(string kind, string id, string actor, CancellationToken ct = default)
            => Task.FromResult<보호지원Source?>(new(["owner"]));
    }
    private sealed class AdminAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) => Task.FromResult(AuthorizationResult.Success());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Success());
    }
    private sealed class FailingScopes : IServiceScopeFactory
    {
        public int Calls { get; private set; }
        public IServiceScope CreateScope() { Calls++; throw new InvalidOperationException("Simulation worker must not create scope"); }
    }
}
