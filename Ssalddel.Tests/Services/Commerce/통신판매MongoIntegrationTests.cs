using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Services.Commerce;
using Ssalddel.Tests.Services.PrivacySupport;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Commerce;

public sealed class 통신판매MongoIntegrationTests
{
    [보호지원MongoFact]
    public async Task 실제격리Mongo에서판매자CAS암호문결속과고지재송신충돌을검증한다()
    {
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_PRIVACY_SUPPORT_TEST_MONGO")
                         ?? throw new InvalidOperationException("Commerce integration connection is required.");
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) || uri.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Commerce integration must use a loopback Mongo instance.");
        var databaseName = "r25_commerce_" + Guid.NewGuid().ToString("N");
        if (!databaseName.StartsWith("r25_commerce_", StringComparison.Ordinal) || databaseName.Length != 45)
            throw new InvalidOperationException("Isolated commerce database identity mismatch.");
        var client = new MongoClient(connection);
        var database = client.GetDatabase(databaseName);
        var crypto = new AesGcmIsmsPProtectedDataCryptoService(Options.Create(new IsmsPProtectedDataOptions
        { Aes256GcmKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), HashSalt = "isolated-commerce-test" }));
        var options = Options.Create(new MongoDbOptions { Database = databaseName });
        var store = new Mongo판매자확인Store(client, options, crypto);
        var second = new Mongo판매자확인Store(client, options, crypto);
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var record = new 판매자확인Record { UserId = "isolated-user", Revision = 1, SellerKind = "Personal",
                DisplayName = "개인 표시명", ProfileJson = "{\"PhoneNumber\":\"010-0000-1111\"}" };
            Assert.True(await store.저장Async(record, 0, default));
            Assert.False(await second.저장Async(record, 0, default));
            var raw = await database.GetCollection<BsonDocument>("commerce_seller_verifications").Find(new BsonDocument("_id", record.UserId)).SingleAsync();
            Assert.DoesNotContain("010-0000-1111", raw.ToJson());
            Assert.Equal(record.ProfileJson, (await second.조회Async(record.UserId, default))!.ProfileJson);
            var firstChange = await store.조회Async(record.UserId, default);
            var secondChange = await second.조회Async(record.UserId, default);
            firstChange!.Revision = 2; secondChange!.Revision = 2;
            firstChange.DisplayName = "변경 A"; secondChange.DisplayName = "변경 B";
            Assert.Single(await Task.WhenAll(store.저장Async(firstChange, 1, default), second.저장Async(secondChange, 1, default)), x => x);
            Assert.Equal(2, (await second.조회Async(record.UserId, default))!.Revision);
            // 원문 계정과 envelope의 계정이 서로 다른 보호 payload를 고의 혼입합니다. 임시 DB 안의 문서만 대상입니다.
            var forged = new 판매자확인Record { UserId = "different-user", Revision = 2, ProfileJson = "{}" };
            await database.GetCollection<BsonDocument>("commerce_seller_verifications").UpdateOneAsync(new BsonDocument("_id", record.UserId),
                new BsonDocument("$set", new BsonDocument("Payload", crypto.EncryptAtRest("commerce.seller.identity", JsonSerializer.Serialize(forged)).StoredValue)));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.조회Async(record.UserId, default));

            var disclosures = new Mongo거래고지증적Store(client, options, crypto);
            var disclosure = new 거래고지증적 { Id = "isolated-disclosure", ActorUserId = "actor", SellerUserId = "seller",
                SourceCode = "food-order", ClientRequestId = "client-1", NoticeVersion = "version-1", NoticeHash = "notice-hash",
                SellerRevision = 1, SellerKind = "Business", StateCode = "PreflightVerified", AcceptedAtUtc = DateTime.UtcNow };
            await disclosures.기록Async(disclosure, default);
            var originalTime = disclosure.AcceptedAtUtc;
            disclosure.AcceptedAtUtc = originalTime.AddMinutes(1);
            await disclosures.기록Async(disclosure, default);
            var saved = await database.GetCollection<Mongo거래고지증적Store.저장Record>("commerce_transaction_disclosures")
                .Find(x => x.Id == disclosure.Id).SingleAsync();
            var original = JsonSerializer.Deserialize<거래고지증적>(crypto.DecryptAtRest("commerce.transaction.disclosure", saved.Payload))!;
            Assert.Equal(originalTime, original.AcceptedAtUtc);
            disclosure.SellerUserId = "changed-seller";
            Assert.Equal("IdempotencyConflict", (await Assert.ThrowsAsync<거래보호Exception>(() => disclosures.기록Async(disclosure, default))).Code);
            saved = await database.GetCollection<Mongo거래고지증적Store.저장Record>("commerce_transaction_disclosures")
                .Find(x => x.Id == disclosure.Id).SingleAsync();
            Assert.Equal("seller", JsonSerializer.Deserialize<거래고지증적>(crypto.DecryptAtRest("commerce.transaction.disclosure", saved.Payload))!.SellerUserId);
        }
        finally { await client.DropDatabaseAsync(databaseName); }
        var databases = await client.ListDatabaseNamesAsync(new ListDatabaseNamesOptions { Filter = new BsonDocument("name", databaseName) });
        Assert.DoesNotContain(databaseName, await databases.ToListAsync());
    }
}
