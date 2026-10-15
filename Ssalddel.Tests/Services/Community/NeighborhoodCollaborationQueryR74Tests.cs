using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Extensions;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodCollaborationQueryR74Tests
{
    [Fact]
    public async Task 실제_DI의_보관협업조회는_배송과_보호지원_쓰기UseCase를_생성하지않는다()
    {
        var clock = new Clock(); var store = new Store(Record(clock));
        var services = new ServiceCollection();
        services.AddSsalddelDomainServices();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<I생활협업Store>(store);
        services.AddSingleton<I생활협업보관상태Source>(new Storage());
        // 이전 Query 팩터리는 이 쓰기 UseCase를 통해 배송 -> 보호지원 -> Query로 재진입했습니다.
        services.AddScoped<생활협업UseCase>(_ => throw new InvalidOperationException("Write use case must not be constructed for a storage query."));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var adapter = scope.ServiceProvider.GetRequiredService<I생활보관협업ContextSource>();
        var result = await adapter.조회Async(store.Item.StableId);

        Assert.NotNull(result);
        Assert.Equal(store.Item.StableId, result.CollaborationId);
        Assert.Equal(store.Item.TermsRevision, result.TermsRevision);
        Assert.Equal(store.Item.SpaceRevision, result.ExpectedStorageOfferRevision);
        Assert.Equal("owner", result.OwnerUserId);
        Assert.Equal("requester", result.RequesterUserId);
        Assert.True(result.AcceptedCurrentTerms);
        Assert.True(result.PrivateDisclosureConsented);
        Assert.False(result.IsClosed);
        Assert.Equal("space-1", result.StorageSpaceId);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(0, store.ReplaceAttempts);
    }

    [Theory]
    [InlineData("agreed", false, true)]
    [InlineData("agreed", true, false)]
    [InlineData("requested", false, false)]
    [InlineData("in-progress", false, true)]
    [InlineData("completed", false, false)]
    [InlineData("cancelled", false, false)]
    public async Task 기존_내부조회와_동일한_조건동의_당사자_판본_상세조건을_반환한다(string state, bool oldNotice, bool accepted)
    {
        var clock = new Clock(); var record = Record(clock); record.StatusCode = state;
        if (oldNotice) record.RequesterPrivacyNoticeVersion = "old-version";
        if (state == "requested") record.RequesterAgreed = false;
        var currentStore = new Store(record); var oldStore = new Store(record);
        await using var db = CreateDb();
        var oldQuery = (I생활협업연결Query)new 생활협업UseCase(oldStore, db, new Current(), clock, storage: new Storage());

        var expected = await oldQuery.조회Async(record.StableId);
        var result = await new 생활협업연결Query(currentStore, clock, new Storage()).조회Async(record.StableId);

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result));
        Assert.Equal(accepted, result!.AcceptedCurrentTerms);
        Assert.Equal(7, result.Revision);
        Assert.Equal(3, result.TermsRevision);
        Assert.Equal("delivery-1", result.LinkedDeliveryRequestId);
        Assert.Equal(42, result.SourcePostId);
        Assert.Equal(9, result.StorageOfferRevision);
        Assert.Equal("비공개 인계 장소", result.Terms.HandoverPlace);
    }

    [Theory]
    [InlineData("reserved", 3, false)]
    [InlineData("in-custody", 3, false)]
    [InlineData("returned", 3, false)]
    [InlineData("cancelled", 3, true)]
    [InlineData("reserved", 2, true)]
    public async Task 만료조회도_기존_예약판본보호와_CAS이력을_그대로_보존한다(string reservationState, long termsRevision, bool expired)
    {
        var clock = new Clock(); var record = Record(clock); record.ExpiresAtUtc = clock.Now.AddSeconds(-1);
        var currentStore = new Store(record); var oldStore = new Store(record);
        var storage = new Storage { Reservation = new(termsRevision, reservationState) };
        await using var db = CreateDb();
        var oldQuery = (I생활협업연결Query)new 생활협업UseCase(oldStore, db, new Current(), clock, storage: storage);

        var expected = await oldQuery.조회Async(record.StableId);
        var result = await new 생활협업연결Query(currentStore, clock, storage).조회Async(record.StableId);

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result));
        Assert.Equal(JsonSerializer.Serialize(oldStore.Item), JsonSerializer.Serialize(currentStore.Item));
        Assert.Equal(expired ? "expired" : "agreed", result!.StatusCode);
        Assert.Equal(expired ? 1 : 0, currentStore.ReplaceAttempts);
        if (expired)
        {
            var history = Assert.Single(currentStore.Item.History);
            Assert.Equal("expire", history.Action);
            Assert.Equal("system", history.ActorRoleCode);
            Assert.Equal(8, history.Revision);
            Assert.Equal(clock.Now, history.RecordedAtUtc);
        }
    }

    [Theory]
    [InlineData("delivery-pending")]
    [InlineData("receipt-pending")]
    [InlineData("storage-pending")]
    [InlineData("storage-confirmed")]
    public async Task 진행중인_인계intent의_권한은_자동만료로_제거하지않는다(string intent)
    {
        var clock = new Clock(); var record = Record(clock); record.ExpiresAtUtc = clock.Now.AddSeconds(-1);
        if (intent == "delivery-pending") record.DeliveryIntent = new() { StateCode = "pending" };
        if (intent == "receipt-pending") record.ReceiptConfirmationIntent = new() { StateCode = "pending" };
        if (intent.StartsWith("storage-", StringComparison.Ordinal)) record.StorageReservationIntents.Add(new() { StateCode = intent[8..] });
        var store = new Store(record);

        var result = await new 생활협업연결Query(store, clock).조회Async(record.StableId);

        Assert.Equal("agreed", result!.StatusCode);
        Assert.Equal(7, result.Revision);
        Assert.Equal(0, store.ReplaceAttempts);
    }

    [Fact]
    public async Task 만료CAS경합은_최신원장을_다시읽고_성공이력_한번만_남긴다()
    {
        var clock = new Clock(); var record = Record(clock); record.ExpiresAtUtc = clock.Now.AddSeconds(-1);
        var store = new Store(record) { RejectReplacements = 1 };

        var result = await new 생활협업연결Query(store, clock, new Storage()).조회Async(record.StableId);

        Assert.Equal("expired", result!.StatusCode);
        Assert.Equal(8, result.Revision);
        Assert.Equal(2, store.Reads);
        Assert.Equal(2, store.ReplaceAttempts);
        Assert.Single(store.Item.History);
    }

    [Fact]
    public async Task 저장소실패와_만료CAS실패를_빈조회로_숨기지않는다()
    {
        var clock = new Clock(); var record = Record(clock); record.ExpiresAtUtc = clock.Now.AddSeconds(-1);
        var store = new Store(record) { RejectReplacements = 3 };
        var query = new 생활협업연결Query(store, clock, new Storage());

        await Assert.ThrowsAsync<InvalidOperationException>(() => query.조회Async(record.StableId));
        Assert.Equal(3, store.ReplaceAttempts);
        Assert.Equal(7, store.Item.Revision);
        Assert.Empty(store.Item.History);
        store.ThrowOnRead = true;
        await Assert.ThrowsAsync<IOException>(() => query.조회Async(record.StableId));
    }

    private static 생활협업Record Record(Clock clock) => new()
    {
        StableId = "collaboration-r74", Revision = 7, TermsRevision = 3, SpaceRevision = 9,
        SourcePostId = 42, LinkedDeliveryRequestId = "delivery-1", OwnerUserId = "owner", RequesterUserId = "requester",
        Kind = NeighborhoodCollaborationKinds.Storage, StatusCode = NeighborhoodCollaborationStates.Agreed,
        OwnerAgreed = true, RequesterAgreed = true,
        OwnerPrivacyNoticeVersion = NeighborhoodCollaborationAgreementNotice.Version,
        RequesterPrivacyNoticeVersion = NeighborhoodCollaborationAgreementNotice.Version,
        CreatedAtUtc = clock.Now.AddDays(-1), UpdatedAtUtc = clock.Now, ExpiresAtUtc = clock.Now.AddHours(1),
        Terms = new() { Summary = "보관 합의", HandoverPlace = "비공개 인계 장소", StorageSpaceId = "space-1",
            StorageQuantity = 2, StorageUnit = "상자", StorageFromUtc = clock.Now, StorageUntilUtc = clock.Now.AddDays(1), AgreedCostKrw = 2500 }
    };

    private static SsalddelContext CreateDb() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("collaboration-query-r74-" + Guid.NewGuid()).Options, new Encryption());
    private sealed record Current : ICurrentUserAccessor { public string? UserId => "owner"; public string? Role => "일반회원"; }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; } = new(2026, 10, 15, 3, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Storage : I생활협업보관상태Source
    {
        public 생활협업보관예약Snapshot? Reservation { get; init; }
        public Task<bool> 반환확인Async(string id, long revision, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<생활협업보관예약Snapshot?> 예약조회Async(string id, CancellationToken cancellationToken = default) => Task.FromResult(Reservation);
    }
    private sealed class Store(생활협업Record record) : I생활협업Store
    {
        public 생활협업Record Item { get; private set; } = Copy(record);
        public int Reads { get; private set; }
        public int ReplaceAttempts { get; private set; }
        public int RejectReplacements { get; set; }
        public bool ThrowOnRead { get; set; }
        public Task<생활협업Record?> 조회Async(string stableId, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (ThrowOnRead) throw new IOException("Store unavailable.");
            return Task.FromResult(stableId == Item.StableId ? Copy(Item) : null);
        }
        public Task<bool> 교체Async(생활협업Record item, long expectedRevision, CancellationToken cancellationToken = default)
        {
            ReplaceAttempts++;
            if (RejectReplacements > 0) { RejectReplacements--; return Task.FromResult(false); }
            if (Item.Revision != expectedRevision) return Task.FromResult(false);
            Item = Copy(item); return Task.FromResult(true);
        }
        public Task<bool> 생성Async(생활협업Record item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private static 생활협업Record Copy(생활협업Record value) => JsonSerializer.Deserialize<생활협업Record>(JsonSerializer.Serialize(value))!;
    }
}
