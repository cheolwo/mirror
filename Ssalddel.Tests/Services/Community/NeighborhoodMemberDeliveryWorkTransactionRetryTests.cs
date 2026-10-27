using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Domain.Community;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.Community;
using Ssalddel.Services.OperationsAlerts;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Community;

/// <summary>SQLite 재시도/응답 유실 주입. 외부 쓰기 횟수와 SQL 정본을 검증하며 실제 클라우드 증거가 아니다.</summary>
public sealed class NeighborhoodMemberDeliveryWorkTransactionRetryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task 사진준비업로드잠금완료의커밋응답유실은증적하나와외부쓰기한번으로복구한다(int commit)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var fault = new FailAfterCommit(commit); var storage = new CountedStorage(f.Storage);
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var result = await Service(f, db, storage).EvidenceAsync(work.RequestId, request);
        Assert.True(fault.Fired); Assert.True(result.CanPickup); Assert.Equal(work.Revision + 1, result.Revision);
        Assert.Equal(1, storage.UploadCalls); Assert.Equal(commit == 2 ? 2 : 1, storage.DownloadCalls);
        await AssertEvidenceOnce(f, request);
        var replay = await Service(f, db, storage).EvidenceAsync(work.RequestId, request);
        Assert.Equal(result.Revision, replay.Revision); Assert.Equal(1, storage.UploadCalls);
    }

    [Theory]
    [InlineData("Uploading")]
    [InlineData("Uploaded")]
    public async Task 사진SQL저장실패는현재판본을다시읽고사진객체를중복전송하지않는다(string state)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var fault = new FailAfterSave(x => x is NeighborhoodMemberPhoto p && p.StateCode == state);
        var storage = new CountedStorage(f.Storage);
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var result = await Service(f, db, storage).EvidenceAsync(work.RequestId, request);
        Assert.True(result.CanPickup); Assert.Equal(2, fault.MatchedAttempts); Assert.Equal(1, storage.UploadCalls);
        await AssertEvidenceOnce(f, request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 사진예산SQL집계는롤백뒤복원되고커밋응답유실이나같은요청에서중복되지않는다(bool commitLoss)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        IInterceptor fault = commitLoss ? new FailAfterCommit(1) : new FailAfterSave(x => x is NeighborhoodMemberPhoto p && p.StateCode == "Uploading");
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var budget = new OperationsBudgetGate(db, new BudgetOptions(), f.Clock);
        var storage = new CountedStorage(f.Storage); var service = Service(f, db, storage, budget: budget);
        Assert.True((await service.EvidenceAsync(work.RequestId, request)).CanPickup);
        Assert.True((await service.EvidenceAsync(work.RequestId, request)).CanPickup);
        await using var reread = f.OpenDb();
        var usage = Assert.Single(await reread.Set<OperationsBudgetUsage>().ToListAsync());
        Assert.Equal("photos", usage.Category); Assert.Equal(1, usage.Count); Assert.Equal(1, storage.UploadCalls);
        await AssertEvidenceOnce(f, request);
    }

    [Fact]
    public async Task 객체쓰기결과불명확은동일호출에서재전송하지않고같은요청으로복구할수있다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var storage = new CountedStorage(f.Storage) { FailAfterUpload = true };
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() =>
            Service(f, db, storage).EvidenceAsync(work.RequestId, request));
        Assert.Equal(1, storage.UploadCalls); Assert.Single(f.Storage.Objects);
        await using (var reread = f.OpenDb())
        {
            Assert.Equal("Uploading", (await reread.Set<NeighborhoodMemberPhoto>().SingleAsync()).StateCode);
            Assert.Empty(await reread.Set<NeighborhoodMemberReceipt>().Where(x => x.ClientRequestId == request.ClientRequestId).ToListAsync());
        }
        storage.FailAfterUpload = false;
        Assert.True((await Service(f, db, storage).EvidenceAsync(work.RequestId, request)).CanPickup);
        Assert.Equal(2, storage.UploadCalls); Assert.Single(f.Storage.Objects); await AssertEvidenceOnce(f, request);
    }

    [Fact]
    public async Task 업로드잠금재시도시사진이만료되면물리쓰기를다시보내거나업무증빙을확정하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var storage = new CountedStorage(f.Storage);
        var fault = new FailAfterCommit(2, () => f.Clock.Now = f.Clock.Now.AddHours(25));
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => Service(f, db, storage).EvidenceAsync(work.RequestId, request));
        Assert.Equal("MemberPhotoUploadExpired", error.Code); Assert.Equal(1, storage.UploadCalls);
        await using var reread = f.OpenDb();
        Assert.Equal("Uploading", (await reread.Set<NeighborhoodMemberPhoto>().SingleAsync()).StateCode);
        Assert.Empty(await reread.Set<NeighborhoodMemberReceipt>().Where(x => x.ClientRequestId == request.ClientRequestId).ToListAsync());
    }

    [Fact]
    public async Task 업로드잠금커밋유실뒤다른동일요청이확정하면만료거절대신원증적을복구한다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var storage = new CountedStorage(f.Storage);
        var fault = new CompleteSameEvidenceAfterCommit(2, async () =>
        {
            await using var other = f.OpenDb();
            Assert.True((await f.Service(other).EvidenceAsync(work.RequestId, request)).CanPickup);
        });
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var result = await Service(f, db, storage).EvidenceAsync(work.RequestId, request);
        Assert.True(fault.Fired); Assert.True(result.CanPickup); Assert.Equal(work.Revision + 1, result.Revision);
        Assert.Equal(1, storage.UploadCalls); await AssertEvidenceOnce(f, request);
    }

    [Fact]
    public async Task 사진최종확정재시도사이에만료되면업로드증적과완료가능상태를만들지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var request = f.Evidence(work, "Pickup");
        var fault = new FailAfterSave(x => x is NeighborhoodMemberPhoto p && p.StateCode == "Uploaded",
            () => f.Clock.Now = f.Clock.Now.AddHours(25));
        var storage = new CountedStorage(f.Storage);
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => Service(f, db, storage).EvidenceAsync(work.RequestId, request));
        Assert.Equal("MemberPhotoUploadExpired", error.Code); Assert.Equal(1, storage.UploadCalls);
        Assert.False((await Service(f, db, storage).WorkAsync(work.RequestId))!.CanPickup);
        await using var reread = f.OpenDb();
        Assert.Equal("Uploading", (await reread.Set<NeighborhoodMemberPhoto>().SingleAsync()).StateCode);
        Assert.Empty(await reread.Set<NeighborhoodMemberReceipt>().Where(x => x.ClientRequestId == request.ClientRequestId).ToListAsync());
    }

    [Theory]
    [InlineData("Pickup", false)]
    [InlineData("Pickup", true)]
    [InlineData("Delivery", false)]
    [InlineData("Delivery", true)]
    public async Task 픽업전달SQL재시도는사진귀속업무상태이벤트증적을한번만확정한다(string stage, bool commitLoss)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f);
        if (stage == "Delivery")
        {
            work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Pickup"));
            work = await f.Service().PickupAsync(work.RequestId, f.Command(work.Revision));
        }
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, stage));
        var request = f.Command(work.Revision); var storage = new CountedStorage(f.Storage);
        IInterceptor fault = commitLoss ? new FailAfterCommit(1) : new FailAfterSave(x => x is NeighborhoodMemberPhoto p && p.StateCode == "Bound");
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var service = Service(f, db, storage);
        var result = stage == "Pickup" ? await service.PickupAsync(work.RequestId, request) : await service.DeliverAsync(work.RequestId, request);
        Assert.Equal(stage == "Pickup" ? "PickedUp" : "Delivered", result.StateCode);
        Assert.Equal(work.Revision + 1, result.Revision); Assert.Equal(0, storage.UploadCalls);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.ClientRequestId == request.ClientRequestId));
        Assert.Equal(1, await reread.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMember" + stage));
        Assert.Equal(상태값.결제상태.결제대기, (await reread.화주운송의뢰.SingleAsync()).정산상태);
        Assert.Empty(await reread.기사운송대금지급요청.ToListAsync());
        Assert.Equal(stage == "Pickup" ? work.RequestId : null, (await reread.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 문제접수SQL재시도는기존지원원장하나에연결하고적재책임을유지한다(bool commitLoss)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var work = await Assigned(f); var cases = new NeighborhoodMemberDeliveryRecoveryTests.Cases();
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "member-a")], "test"));
        var options = Options.Create(new 보호지원Options());
        var support = new 보호지원UseCase(cases, new 보호지원SourceResolver(f.Db, new Collaboration()),
            new 한국영업일Calendar(options), options, f.Clock, http, new Authorization());
        IInterceptor fault = commitLoss ? new FailAfterCommit(1) : new FailAfterSave(x => x is NeighborhoodMemberDelivery d && d.StateCode == "Issue");
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var request = new NeighborhoodMemberIssueRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, Reason = "픽업지에서 연락이 안 됨" };
        var result = await Service(f, db, new CountedStorage(f.Storage), support).IssueAsync(work.RequestId, request);
        var replay = await Service(f, db, new CountedStorage(f.Storage), support).IssueAsync(work.RequestId, request);
        Assert.Equal("Issue", result.StateCode); Assert.Equal(result.Revision, replay.Revision);
        Assert.Equal(result.SupportCaseId, replay.SupportCaseId); Assert.Single(cases.Rows);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.ClientRequestId == request.ClientRequestId));
        Assert.Equal(1, await reread.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberIssue"));
        Assert.Equal(work.RequestId, (await reread.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 사진파기SQL재시도는행잠금을유지하고물리삭제를한번씩만전송한다(bool commitLoss)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        await Delivered(f); f.Clock.Now = f.Clock.Now.AddDays(31);
        IInterceptor fault = commitLoss ? new FailAfterCommit(1) : new FailAfterSave(x => x is NeighborhoodMemberPhoto p && p.StateCode == "Deleted");
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f, fault);
        var storage = new CountedStorage(f.Storage); var cases = new NeighborhoodMemberDeliveryRecoveryTests.Cases { Preserve = false };
        Assert.Equal(2, await Service(f, db, storage).SweepPhotosAsync(cases));
        Assert.Equal(2, storage.DeleteCalls); Assert.Empty(f.Storage.Objects);
        await using var reread = f.OpenDb();
        Assert.All(await reread.Set<NeighborhoodMemberPhoto>().ToListAsync(), p => { Assert.Equal("Deleted", p.StateCode); Assert.Empty(p.PrivatePayload); });
        Assert.Equal(5000m, (await reread.Set<NeighborhoodMemberDelivery>().SingleAsync()).FareKrw);
    }

    [Fact]
    public async Task 삭제결과불명확은동기예외여도동일호출에서재전송하지않고파기완료로승격하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        await Delivered(f); f.Clock.Now = f.Clock.Now.AddDays(31);
        await using var db = NeighborhoodMemberDeliveryTransactionRetryTests.Open(f);
        var storage = new CountedStorage(f.Storage) { FailAfterDelete = true };
        var cases = new NeighborhoodMemberDeliveryRecoveryTests.Cases { Preserve = false };
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => Service(f, db, storage).SweepPhotosAsync(cases));
        Assert.Equal(1, storage.DeleteCalls);
        await using var reread = f.OpenDb();
        Assert.All(await reread.Set<NeighborhoodMemberPhoto>().ToListAsync(), p => { Assert.Equal("Bound", p.StateCode); Assert.NotEmpty(p.PrivatePayload); });
    }

    private static NeighborhoodMemberDeliveryUseCase Service(NeighborhoodMemberDeliveryTests.Fixture f, SsalddelContext db,
        IObjectStorageService storage, 보호지원UseCase? support = null, OperationsBudgetGate? budget = null)
        => new(db, f.Actor, Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled = true }),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            f.Clock, new NeighborhoodMemberDeliveryTests.Encryption(), f.Consent, f.Routes, storage, support: support, operationsBudget: budget);
    private static async Task<NeighborhoodMemberWorkResponse> Assigned(NeighborhoodMemberDeliveryTests.Fixture f)
    { var offer = await f.Ready(); return await f.Service().AcceptAsync(offer.OfferId, f.Accept(offer)); }
    private static async Task Delivered(NeighborhoodMemberDeliveryTests.Fixture f)
    {
        var work = await Assigned(f);
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Pickup"));
        work = await f.Service().PickupAsync(work.RequestId, f.Command(work.Revision));
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Delivery"));
        await f.Service().DeliverAsync(work.RequestId, f.Command(work.Revision));
    }
    private static async Task AssertEvidenceOnce(NeighborhoodMemberDeliveryTests.Fixture f, NeighborhoodMemberEvidenceRequest request)
    {
        await using var reread = f.OpenDb();
        var photo = await reread.Set<NeighborhoodMemberPhoto>().SingleAsync();
        Assert.Equal("Uploaded", photo.StateCode); Assert.Equal(request.ClientRequestId, photo.UploadRequestId);
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.ClientRequestId == request.ClientRequestId));
        Assert.Single(f.Storage.Objects);
    }
    private sealed class CountedStorage(NeighborhoodMemberDeliveryTests.Storage inner) : IObjectStorageService
    {
        public int UploadCalls { get; private set; } public int DownloadCalls { get; private set; } public int DeleteCalls { get; private set; }
        public bool FailAfterUpload { get; set; } public bool FailAfterDelete { get; set; }
        public bool IsConfigured(ObjectStorageAccess access) => inner.IsConfigured(access);
        public string? GetContainerName(ObjectStorageAccess access) => inner.GetContainerName(access);
        public Task<ObjectStorageUploadResult> UploadAsync(Stream stream, string name, string? type, string? folder, ObjectStorageAccess access, CancellationToken ct = default)
            => throw new NotSupportedException();
        public async Task<ObjectStorageUploadResult> UploadImmutableAsync(Stream stream, string name, string? type, ObjectStorageAccess access,
            IReadOnlyDictionary<string, string>? metadata = null, CancellationToken ct = default)
        { UploadCalls++; var result = await inner.UploadImmutableAsync(stream, name, type, access, metadata, ct); if (FailAfterUpload) throw new NeighborhoodMemberDeliveryTransactionRetryTests.RetryableFailure(); return result; }
        public Task<byte[]> DownloadAsync(string container, string name, CancellationToken ct = default)
        { DownloadCalls++; return inner.DownloadAsync(container, name, ct); }
        public Task<ObjectStorageDeleteResult> DeleteAsync(string container, string name, string prefix, CancellationToken ct = default)
        { DeleteCalls++; var result = inner.DeleteAsync(container, name, prefix, ct); if (FailAfterDelete) throw new NeighborhoodMemberDeliveryTransactionRetryTests.RetryableFailure(); return result; }
    }
    private sealed class BudgetOptions : IOptionsMonitor<OperationsBudgetOptions>
    {
        public OperationsBudgetOptions CurrentValue { get; } = new() { Enabled = true };
        public OperationsBudgetOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OperationsBudgetOptions, string?> listener) => null;
    }
    private sealed class FailAfterSave(Func<object, bool> matches, Action? beforeFailure = null) : SaveChangesInterceptor
    {
        public int MatchedAttempts { get; private set; } private bool pending;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { pending = eventData.Context!.ChangeTracker.Entries().Any(x => x.State is EntityState.Added or EntityState.Modified && matches(x.Entity)); if (pending) MatchedAttempts++; return ValueTask.FromResult(result); }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        { if (pending && MatchedAttempts == 1) { beforeFailure?.Invoke(); throw new NeighborhoodMemberDeliveryTransactionRetryTests.RetryableFailure(); } return ValueTask.FromResult(result); }
    }
    private sealed class FailAfterCommit(int target, Action? beforeFailure = null) : DbTransactionInterceptor
    {
        public bool Fired { get; private set; } private int commits;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
        { if (++commits == target && !Fired) { Fired = true; beforeFailure?.Invoke(); throw new NeighborhoodMemberDeliveryTransactionRetryTests.RetryableFailure(); } return Task.CompletedTask; }
    }
    private sealed class CompleteSameEvidenceAfterCommit(int target, Func<Task> complete) : DbTransactionInterceptor
    {
        public bool Fired { get; private set; } private int commits;
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
        { if (++commits == target && !Fired) { Fired = true; await complete(); throw new NeighborhoodMemberDeliveryTransactionRetryTests.RetryableFailure(); } }
    }
    private sealed class Collaboration : I생활협업연결Query
    { public Task<생활협업연결Context?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult<생활협업연결Context?>(null); }
    private sealed class Authorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policy) => Task.FromResult(AuthorizationResult.Failed());
    }
}
