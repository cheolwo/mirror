using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원MongoIntegrationTests
{
    [보호지원MongoFact]
    public async Task 실제격리Mongo에서암호화재조회중복접수CAS와당사자조회후본인DB만정리한다()
    {
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_PRIVACY_SUPPORT_TEST_MONGO")
                         ?? throw new InvalidOperationException("Support integration connection is required.");
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var address) || address.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Support integration must use a loopback Mongo instance.");
        var client = new MongoClient(connection);
        var databaseName = "r25_privacy_support_" + Guid.NewGuid().ToString("N");
        if (!databaseName.StartsWith("r25_privacy_support_", StringComparison.Ordinal) || databaseName.Length != 52)
            throw new InvalidOperationException("Isolated support database identity mismatch.");
        var database = client.GetDatabase(databaseName);
        var protection = new EphemeralDataProtectionProvider();
        var settings = Options.Create(new MongoDbOptions { Database = databaseName });
        var first = new Mongo보호지원Store(client, settings, protection);
        var reloaded = new Mongo보호지원Store(client, settings, protection);
        var record = new 보호지원Record
        {
            CaseId = "isolated-case", Kind = 보호지원종류Codes.분쟁, SourceKind = 보호지원출처Codes.음식주문,
            SourceId = "only-test-order", OwnerUserId = "only-test-owner", PartyUserIds = ["only-test-owner", "only-test-counterparty"],
            Summary = "비공개 테스트 010-0000-1111", Revision = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            Assert.True(await first.생성Async(record));
            Assert.False(await reloaded.생성Async(record));
            var raw = await database.GetCollection<BsonDocument>(Mongo보호지원Store.CollectionName).Find(new BsonDocument("_id", record.CaseId)).SingleAsync();
            var rawJson = raw.ToJson();
            Assert.DoesNotContain("010-0000-1111", rawJson);
            Assert.DoesNotContain("only-test-owner", rawJson);
            Assert.DoesNotContain("only-test-order", rawJson);
            var afterReload = await reloaded.조회Async(record.CaseId);
            Assert.Equal(record.Summary, afterReload!.Summary);
            var one = await first.조회Async(record.CaseId);
            var two = await reloaded.조회Async(record.CaseId);
            one!.Revision = 2; one.StatusCode = "reviewing";
            two!.Revision = 2; two.StatusCode = "result-prepared";
            var changes = await Task.WhenAll(first.교체Async(one, 1), reloaded.교체Async(two, 1));
            Assert.Single(changes, x => x);
            Assert.Single(changes, x => !x);
            Assert.Equal(2, (await reloaded.조회Async(record.CaseId))!.Revision);
            Assert.Single(await reloaded.목록Async("only-test-owner", 보호지원종류Codes.분쟁, 0, 20));
            Assert.Empty(await reloaded.목록Async("outsider", 보호지원종류Codes.분쟁, 0, 20));

            var expired = (await reloaded.조회Async(record.CaseId))!;
            expired.StatusCode = "closed"; expired.ClosedAtUtc = DateTime.UtcNow.AddYears(-4);
            expired.PurgeAfterAtUtc = DateTime.UtcNow.AddYears(-1);
            expired.RetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion;
            expired.RetentionIntent = new(Guid.NewGuid(), "pending-fingerprint", "admin", "hold-record", "review-basis", DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
            expired.Revision++;
            Assert.True(await reloaded.교체Async(expired, 2));
            var purge = new 보호지원파기Service(reloaded, Options.Create(new 보호지원Options
            {
                ClosedDisputePurgeEnabled = true, ClosedDisputeRetentionPolicyConfirmed = true,
                ClosedDisputeRetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion
            }), TimeProvider.System);
            Assert.Equal(0, await purge.실행Async());
            Assert.NotNull(await reloaded.조회Async(record.CaseId));
            expired = (await reloaded.조회Async(record.CaseId))!;
            expired.RetentionIntent = null; expired.Revision++;
            Assert.True(await reloaded.교체Async(expired, 3));
            Assert.Equal(1, await purge.실행Async());
            Assert.Null(await reloaded.조회Async(record.CaseId));
            var manifest = await database.GetCollection<Mongo보호지원Store.보호지원파기Document>("commerce_privacy_support_deletion_manifest")
                .Find(x => x.CaseId == record.CaseId).SingleAsync();
            Assert.NotNull(manifest.DeletedAtUtc);
            Assert.Equal(expired.Revision + 1, manifest.ClaimedRevision);
            var indexes = await database.GetCollection<BsonDocument>(Mongo보호지원Store.CollectionName).Indexes.ListAsync();
            Assert.DoesNotContain(await indexes.ToListAsync(), x => x.Contains("expireAfterSeconds"));

            // 실제 삭제 뒤 완료 manifest 쓰기만 빠진 경우, 원본 부재 확인을 별도로 기록하며 삭제시각을 지어내지 않습니다.
            await database.GetCollection<Mongo보호지원Store.보호지원파기Document>("commerce_privacy_support_deletion_manifest")
                .InsertOneAsync(new() { CaseId = "only-isolated-missing-source", ClaimedRevision = 4,
                    PolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion, IntendedAtUtc = DateTime.UtcNow });
            await reloaded.파기후보Async(DateTime.UtcNow, 10);
            var recovered = await database.GetCollection<Mongo보호지원Store.보호지원파기Document>("commerce_privacy_support_deletion_manifest")
                .Find(x => x.CaseId == "only-isolated-missing-source").SingleAsync();
            Assert.Null(recovered.DeletedAtUtc);
            Assert.NotNull(recovered.VerifiedAbsentAtUtc);

            // 외부 보존 원장도 같은 고유 테스트 DB에서 실제 CAS합니다. 늦게 도착한 옛 hold가 해제를 되돌릴 수 없습니다.
            var retentionStore = new Mongo개인정보보존Store(client, settings, new UnusedEvidenceCrypto());
            var coordinator = new 개인정보보존조정Service(retentionStore, [new IsolatedRetentionSource()],
                Options.Create(new 개인정보보존Options()), TimeProvider.System);
            var holdReview = DateTime.UtcNow.AddDays(5);
            await coordinator.보존상태적용Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case", 10, true,
                "transaction-dispute-review", holdReview);
            Assert.Equal(10, (await coordinator.보존적용조회Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case"))!.Sequence);
            await coordinator.보존상태적용Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case", 11, false,
                "transaction-dispute-review", null);
            var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.보존상태적용Async("FoodOrder",
                "only-isolated-retention-source", "only-isolated-case", 10, true, "transaction-dispute-review", holdReview));
            Assert.Equal("RetentionIntentStale", stale.Message);
            Assert.Null(await coordinator.보존정지조회Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case"));
            Assert.True(await coordinator.보존정지해제확인Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case"));
            var finalReceipt = await coordinator.보존적용조회Async("FoodOrder", "only-isolated-retention-source", "only-isolated-case");
            Assert.Equal(11, finalReceipt!.Sequence); Assert.False(finalReceipt.Hold);
        }
        finally
        {
            // 위에서 생성한 고유 DB 하나만 정리합니다. 사용자 DB나 다른 테스트 DB를 열거·삭제하지 않습니다.
            await client.DropDatabaseAsync(databaseName);
        }
        var remaining = await client.ListDatabaseNamesAsync(new ListDatabaseNamesOptions { Filter = new BsonDocument("name", databaseName) });
        Assert.DoesNotContain(databaseName, await remaining.ToListAsync());
    }

    private sealed class UnusedEvidenceCrypto : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => throw new InvalidOperationException("This test never writes retained evidence.");
        public string? Unprotect(string? value) => throw new InvalidOperationException("This test never reads retained evidence.");
    }
    private sealed class IsolatedRetentionSource : I개인정보파기Adapter
    {
        public string SourceCode => "FoodOrder";
        public Task<개인정보파기대상?> FindAsync(string id, CancellationToken ct)
            => Task.FromResult<개인정보파기대상?>(new(SourceCode, id, DateTime.UtcNow, "only-isolated-owner"));
        public Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
            => throw new InvalidOperationException("This test never discovers user data.");
        public Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
            => throw new InvalidOperationException("This test never purges source data.");
    }
}

public sealed class 보호지원MongoFactAttribute : FactAttribute
{
    public 보호지원MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_PRIVACY_SUPPORT_TEST_MONGO")))
            Skip = "격리된 실제 Mongo 시험용 loopback 연결이 지정되지 않았습니다.";
    }
}
