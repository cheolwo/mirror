using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Explanation;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Dispatch.Explanation;

public sealed class CargoDispatchFeedbackMongoR24Tests
{
    [CargoDispatchFeedbackMongoFact]
    public async Task 실제격리Mongo에서보호접수동시멱등충돌과독립재조회를검증한다()
    {
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_CARGO_DISPATCH_FEEDBACK_MONGO_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Isolated cargo feedback Mongo connection is required.");
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) || uri.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Cargo feedback tests require loopback Mongo.");
        const string prefix = "ssalddel_cargo_feedback_r24_";
        var databaseName = prefix + Guid.NewGuid().ToString("N");
        var settings = MongoClientSettings.FromConnectionString(connection);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var options = Options.Create(new MongoDbOptions { Database = databaseName });
        // The protection provider is shared by independent store instances. Key persistence/restart is not claimed.
        var provider = new EphemeralDataProtectionProvider();
        var protection = new Protection(provider.CreateProtector("Ssalddel.Tests.CargoDispatchFeedback.R24"));
        var first = new MongoCargoDispatchFeedbackStore(client, options, protection);
        var second = new MongoCargoDispatchFeedbackStore(new MongoClient(settings), options,
            new Protection(provider.CreateProtector("Ssalddel.Tests.CargoDispatchFeedback.R24")));
        try
        {
            await client.GetDatabase(databaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            const string user = "private-feedback-user-r24";
            const string comment = "private-feedback-comment-r24";
            var requestKey = Guid.NewGuid();
            var submitted = new DateTime(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc);
            var candidate = Candidate("request-mongo-r24", "123", user, requestKey, comment, submitted);
            var competing = await Task.WhenAll(first.InsertAsync(candidate),
                second.InsertAsync(candidate with { SubmittedAtUtc = submitted.AddSeconds(1) }));

            Assert.Equal(competing[0].FeedbackId, competing[1].FeedbackId);
            Assert.Equal(competing[0].SubmittedAtUtc, competing[1].SubmittedAtUtc);
            var collection = client.GetDatabase(databaseName)
                .GetCollection<BsonDocument>(MongoCargoDispatchFeedbackStore.CollectionName);
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var raw = await collection.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            var ciphertext = raw.Elements.Single(x => string.Equals(x.Name, "ProtectedPayload", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrWhiteSpace(ciphertext.Value.AsString));
            foreach (var privateValue in new[] { user, comment, candidate.RequestId, "helpful" })
                Assert.DoesNotContain(privateValue, raw.ToJson(), StringComparison.Ordinal);

            var third = new MongoCargoDispatchFeedbackStore(new MongoClient(settings), options,
                new Protection(provider.CreateProtector("Ssalddel.Tests.CargoDispatchFeedback.R24")));
            var read = await third.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey);
            Assert.Equal(competing[0], read);
            Assert.Equal(comment, Assert.Single(await third.ListAsync(candidate.RequestId, candidate.EvidenceId, user)).Comment);
            Assert.Null(await third.GetAsync(candidate.RequestId, candidate.EvidenceId, "other-user", requestKey));
            Assert.Null(await third.GetAsync(candidate.RequestId, "124", user, requestKey));
            Assert.Empty(await third.ListAsync(candidate.RequestId, candidate.EvidenceId, "other-user"));
            Assert.Empty(await third.ListAsync(candidate.RequestId, "124", user));

            await Assert.ThrowsAsync<CargoDispatchFeedbackConflictException>(() => second.InsertAsync(candidate with { Comment = "different comment" }));
            await Assert.ThrowsAsync<CargoDispatchFeedbackConflictException>(() => first.InsertAsync(candidate with { CategoryCode = "other" }));
            Assert.Equal(competing[0], await third.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey));
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

            // A valid encrypted payload for another actor cannot be transplanted under the original document key.
            var forged = Candidate(candidate.RequestId, candidate.EvidenceId, "other-user", requestKey, comment, submitted);
            var encoded = protection.Protect(JsonSerializer.Serialize(forged, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", raw["_id"]),
                Builders<BsonDocument>.Update.Set(ciphertext.Name, encoded));
            await Assert.ThrowsAsync<CargoDispatchFeedbackUnavailableException>(() => third.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey));
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", raw["_id"]),
                Builders<BsonDocument>.Update.Set(ciphertext.Name, ciphertext.Value));
            Assert.Equal(competing[0], await third.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey));

            var compatibilityStore = new MongoCargoDispatchFeedbackStore(new MongoClient(settings), options, new PlaintextCompatibilityProtection());
            await Assert.ThrowsAsync<CargoDispatchFeedbackUnavailableException>(() => compatibilityStore.InsertAsync(candidate));
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", raw["_id"]),
                Builders<BsonDocument>.Update.Set(ciphertext.Name, JsonSerializer.Serialize(competing[0], new JsonSerializerOptions(JsonSerializerDefaults.Web))));
            await Assert.ThrowsAsync<CargoDispatchFeedbackUnavailableException>(() => compatibilityStore.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey));
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", raw["_id"]),
                Builders<BsonDocument>.Update.Set(ciphertext.Name, ciphertext.Value));
            Assert.Equal(competing[0], await third.GetAsync(candidate.RequestId, candidate.EvidenceId, user, requestKey));
        }
        finally
        {
            if (!databaseName.StartsWith(prefix, StringComparison.Ordinal) || databaseName.Length != prefix.Length + 32
                || !databaseName[prefix.Length..].All(Uri.IsHexDigit))
                throw new InvalidOperationException("Test database ownership mismatch.");
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static CargoDispatchFeedbackStored Candidate(string requestId, string evidenceId, string userId,
        Guid clientRequestId, string comment, DateTime submitted)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new[] { "cargo-dispatch-feedback-v1", evidenceId, userId, clientRequestId.ToString("D") }))));
        return new(requestId, evidenceId, userId, clientRequestId, "helpful", comment, submitted, key);
    }

    private sealed class Protection(IDataProtector protector) : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : protector.Protect(value);
        public string? Unprotect(string? value) => value is null ? null : protector.Unprotect(value);
    }

    private sealed class PlaintextCompatibilityProtection : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}

public sealed class CargoDispatchFeedbackMongoFactAttribute : FactAttribute
{
    public CargoDispatchFeedbackMongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_CARGO_DISPATCH_FEEDBACK_MONGO_TEST_CONNECTION")))
            Skip = "격리된 화물 배차 의견 Mongo 시험용 loopback 연결이 지정되지 않았습니다.";
    }
}
