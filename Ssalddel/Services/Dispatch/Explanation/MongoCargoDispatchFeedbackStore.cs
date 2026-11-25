using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace 살뜰.Services.Dispatch.Explanation;

/// <summary>의견 접수만 insert-only로 보관한다. 배차/RAG/운임/사용자 평점을 수정하지 않는다.</summary>
public sealed class MongoCargoDispatchFeedbackStore : ICargoDispatchFeedbackStore
{
    public const string CollectionName = "cargo_dispatch_feedback_private";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IMongoCollection<FeedbackDocument> collection;
    private readonly IPersonalDataEncryptionService protection;

    public MongoCargoDispatchFeedbackStore(IMongoClient mongo, IOptions<MongoDbOptions> options,
        IPersonalDataEncryptionService protection)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database))
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = mongo.GetDatabase(options.Value.Database.Trim()).GetCollection<FeedbackDocument>(CollectionName);
        this.protection = protection;
    }

    public async Task<CargoDispatchFeedbackStored> InsertAsync(CargoDispatchFeedbackStored candidate, CancellationToken ct = default)
    {
        Validate(candidate);
        var key = Key(candidate.EvidenceId, candidate.UserId, candidate.ClientRequestId);
        var participantKey = ParticipantKey(candidate.EvidenceId, candidate.UserId);
        string? protectedPayload;
        var raw = JsonSerializer.Serialize(candidate, Json);
        try { protectedPayload = protection.Protect(raw); }
        catch (Exception ex) { throw new CargoDispatchFeedbackUnavailableException(ex); }
        if (string.IsNullOrWhiteSpace(protectedPayload) || protectedPayload == raw) throw new CargoDispatchFeedbackUnavailableException();
        var row = new FeedbackDocument { Id = key, ParticipantEvidenceKey = participantKey,
            ProtectedPayload = protectedPayload, CreatedAtUtc = candidate.SubmittedAtUtc };
        try
        {
            await collection.InsertOneAsync(row, cancellationToken: ct);
            return candidate;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var saved = await GetAsync(candidate.RequestId, candidate.EvidenceId, candidate.UserId, candidate.ClientRequestId, ct)
                ?? throw new CargoDispatchFeedbackUnavailableException();
            if (saved.CategoryCode != candidate.CategoryCode || saved.Comment != candidate.Comment)
                throw new CargoDispatchFeedbackConflictException();
            return saved;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new CargoDispatchFeedbackUnavailableException(ex); }
    }

    public async Task<CargoDispatchFeedbackStored?> GetAsync(string requestId, string evidenceId, string userId,
        Guid clientRequestId, CancellationToken ct = default)
    {
        ValidateLookup(requestId, evidenceId, userId, clientRequestId);
        try
        {
            var row = await collection.Find(x => x.Id == Key(evidenceId, userId, clientRequestId)).FirstOrDefaultAsync(ct);
            if (row is null) return null;
            var saved = Read(row);
            if (saved.RequestId != requestId || saved.EvidenceId != evidenceId || saved.UserId != userId
                || saved.ClientRequestId != clientRequestId) throw new CargoDispatchFeedbackUnavailableException();
            return saved;
        }
        catch (OperationCanceledException) { throw; }
        catch (CargoDispatchFeedbackUnavailableException) { throw; }
        catch (Exception ex) { throw new CargoDispatchFeedbackUnavailableException(ex); }
    }

    public async Task<IReadOnlyList<CargoDispatchFeedbackStored>> ListAsync(string requestId, string evidenceId,
        string userId, CancellationToken ct = default)
    {
        ValidateLookup(requestId, evidenceId, userId);
        try
        {
            var participant = ParticipantKey(evidenceId, userId);
            var rows = await collection.Find(x => x.ParticipantEvidenceKey == participant)
                .SortByDescending(x => x.CreatedAtUtc).Limit(50).ToListAsync(ct);
            var results = rows.Select(Read).ToArray();
            if (results.Any(x => x.RequestId != requestId || x.EvidenceId != evidenceId || x.UserId != userId))
                throw new CargoDispatchFeedbackUnavailableException();
            return results;
        }
        catch (OperationCanceledException) { throw; }
        catch (CargoDispatchFeedbackUnavailableException) { throw; }
        catch (Exception ex) { throw new CargoDispatchFeedbackUnavailableException(ex); }
    }

    private CargoDispatchFeedbackStored Read(FeedbackDocument row)
    {
        try
        {
            var raw = protection.Unprotect(row.ProtectedPayload);
            if (raw == row.ProtectedPayload) throw new CargoDispatchFeedbackUnavailableException();
            var result = raw is null ? null : JsonSerializer.Deserialize<CargoDispatchFeedbackStored>(raw, Json);
            if (result is null) throw new CargoDispatchFeedbackUnavailableException();
            Validate(result);
            if (row.Id != Key(result.EvidenceId, result.UserId, result.ClientRequestId)
                || row.ParticipantEvidenceKey != ParticipantKey(result.EvidenceId, result.UserId))
                throw new CargoDispatchFeedbackUnavailableException();
            return result;
        }
        catch (CargoDispatchFeedbackUnavailableException) { throw; }
        catch (Exception ex) { throw new CargoDispatchFeedbackUnavailableException(ex); }
    }

    private static void Validate(CargoDispatchFeedbackStored value)
    {
        ValidateLookup(value.RequestId, value.EvidenceId, value.UserId, value.ClientRequestId);
        if (!Ssalddel.Contracts.Common.Dispatch.CargoDispatchFeedbackCategories.All.Contains(value.CategoryCode, StringComparer.Ordinal)
            || value.Comment is null || value.Comment.Length > 500 || value.Comment.Any(char.IsControl)
            || value.SubmittedAtUtc.Kind != DateTimeKind.Utc || value.SubmittedAtUtc == default
            || value.FeedbackId != Key(value.EvidenceId, value.UserId, value.ClientRequestId))
            throw new ArgumentException("CargoDispatchFeedbackInvalid");
    }

    private static void ValidateLookup(string requestId, string evidenceId, string userId, Guid? requestKey = null)
    {
        CargoDispatchExplanationCodec.ValidateIdentifier(requestId, 200);
        CargoDispatchExplanationCodec.ValidateIdentifier(userId, 450);
        if (requestKey == Guid.Empty || !long.TryParse(evidenceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id <= 0 || evidenceId != id.ToString(CultureInfo.InvariantCulture))
            throw new ArgumentException("CargoDispatchFeedbackInvalid");
    }

    private static string Key(string evidenceId, string userId, Guid requestKey)
        => Hash(["cargo-dispatch-feedback-v1", evidenceId, userId, requestKey.ToString("D")]);
    private static string ParticipantKey(string evidenceId, string userId)
        => Hash(["cargo-dispatch-feedback-participant-v1", evidenceId, userId]);
    private static string Hash(string[] values)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));

    private sealed class FeedbackDocument
    {
        [BsonId] public string Id { get; set; } = "";
        public string ParticipantEvidenceKey { get; set; } = "";
        public string ProtectedPayload { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }
}
