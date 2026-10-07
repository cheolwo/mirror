using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Privacy;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Privacy;

public interface I신청개인정보동의증적Service
{
    Task<신청개인정보동의증적Response> 동의기록Async(
        신청개인정보동의기록Request request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<신청개인정보동의증적Response?> 내증적조회Async(
        Guid evidenceId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<신청개인정보동의증적Response> 철회Async(
        Guid evidenceId,
        신청개인정보동의철회Request request,
        string userId,
        CancellationToken cancellationToken = default);

    Task 유효한동의요구Async(
        Guid? evidenceId,
        string workCode,
        string sourceCode,
        string userId,
        CancellationToken cancellationToken = default);
}

public interface I신청개인정보동의증적Store
{
    Task<신청개인정보동의증적Record?> 조회Async(Guid evidenceId, CancellationToken cancellationToken);
    Task 추가Async(신청개인정보동의증적Record record, CancellationToken cancellationToken);
    Task<bool> 교체Async(신청개인정보동의증적Record record, long expectedRevision, CancellationToken cancellationToken);
}

public sealed class 신청개인정보동의증적Service(
    I신청개인정보동의증적Store store) : I신청개인정보동의증적Service
{
    public async Task<신청개인정보동의증적Response> 동의기록Async(
        신청개인정보동의기록Request request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireUser(userId);
        if (request.증적Id == Guid.Empty)
        {
            throw new InvalidOperationException("동의 증적 ID가 필요합니다.");
        }

        if (!request.수집이용동의 || !request.연령요건확인)
        {
            throw new InvalidOperationException("개인정보 수집·이용과 연령 요건을 모두 명시적으로 확인해야 합니다.");
        }

        if (!string.Equals(request.동의문버전, 신청개인정보동의정책.현재버전, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("현재 개인정보 동의문 버전을 다시 확인해 주세요.");
        }

        if (!신청개인정보출처Codes.지원하는출처인가(request.출처Code))
        {
            throw new InvalidOperationException("지원하지 않는 신청 출처입니다.");
        }

        var notice = 신청개인정보동의정책.For(request.업무Code);
        var existing = await store.조회Async(request.증적Id, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.UserId, actor, StringComparison.Ordinal)
                || !string.Equals(existing.업무Code, notice.업무Code, StringComparison.Ordinal)
                || !string.Equals(existing.출처Code, request.출처Code, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("같은 동의 증적 ID를 다른 사용자나 신청에 사용할 수 없습니다.");
            }

            return ToResponse(existing);
        }

        var record = new 신청개인정보동의증적Record
        {
            Id = request.증적Id,
            UserId = actor,
            업무Code = notice.업무Code,
            출처Code = request.출처Code,
            동의문버전 = 신청개인정보동의정책.현재버전,
            수집이용목적 = notice.수집이용목적,
            수집항목 = notice.수집항목.ToArray(),
            보유이용기간 = notice.보유이용기간,
            동의문Hash = NoticeHash(notice),
            상태Code = 신청개인정보동의상태Codes.유효,
            동의일시Utc = DateTime.UtcNow
        };
        await store.추가Async(record, cancellationToken);
        return ToResponse(record);
    }

    public async Task<신청개인정보동의증적Response?> 내증적조회Async(
        Guid evidenceId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireUser(userId);
        var record = await store.조회Async(evidenceId, cancellationToken);
        return record is not null && string.Equals(record.UserId, actor, StringComparison.Ordinal)
            ? ToResponse(record)
            : null;
    }

    public async Task<신청개인정보동의증적Response> 철회Async(
        Guid evidenceId,
        신청개인정보동의철회Request request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireUser(userId);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var record = await store.조회Async(evidenceId, cancellationToken)
                         ?? throw new KeyNotFoundException("개인정보 동의 증적을 찾을 수 없습니다.");
            if (!string.Equals(record.UserId, actor, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("본인의 개인정보 동의만 철회할 수 있습니다.");
            }

            if (record.철회일시Utc.HasValue)
            {
                return ToResponse(record);
            }

            record.상태Code = 신청개인정보동의상태Codes.철회;
            record.철회일시Utc = DateTime.UtcNow;
            record.철회사유 = Clean(request.철회사유);
            var expectedRevision = record.Revision++;
            if (await store.교체Async(record, expectedRevision, cancellationToken))
            {
                return ToResponse(record);
            }
        }

        throw new InvalidOperationException("개인정보 동의 상태가 먼저 변경되었습니다. 다시 조회해 주세요.");
    }

    public async Task 유효한동의요구Async(
        Guid? evidenceId,
        string workCode,
        string sourceCode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!신청개인정보출처Codes.지원하는출처인가(sourceCode))
        {
            return;
        }

        var actor = RequireUser(userId);
        if (!evidenceId.HasValue || evidenceId == Guid.Empty)
        {
            throw new InvalidOperationException("해당 출처에서 시작한 신청에는 개인정보 동의 증적이 필요합니다.");
        }

        var record = await store.조회Async(evidenceId.Value, cancellationToken);
        if (record is null
            || !string.Equals(record.UserId, actor, StringComparison.Ordinal)
            || !string.Equals(record.업무Code, workCode, StringComparison.Ordinal)
            || !string.Equals(record.출처Code, sourceCode, StringComparison.Ordinal)
            || !string.Equals(record.동의문버전, 신청개인정보동의정책.현재버전, StringComparison.Ordinal)
            || !string.Equals(record.상태Code, 신청개인정보동의상태Codes.유효, StringComparison.Ordinal)
            || record.철회일시Utc.HasValue)
        {
            throw new InvalidOperationException("현재 신청에 사용할 수 있는 개인정보 동의 증적이 없습니다. 동의 내용을 다시 확인해 주세요.");
        }
    }

    private static string RequireUser(string? userId)
        => !string.IsNullOrWhiteSpace(userId)
            ? userId.Trim()
            : throw new UnauthorizedAccessException("개인정보 동의를 기록하려면 로그인이 필요합니다.");

    private static string NoticeHash(신청개인정보동의안내 notice)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            Version = 신청개인정보동의정책.현재버전,
            notice.업무Code,
            notice.수집이용목적,
            notice.수집항목,
            notice.보유이용기간,
            notice.동의거부안내,
            notice.제3자제공안내,
            notice.국외이전안내
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static 신청개인정보동의증적Response ToResponse(신청개인정보동의증적Record record)
        => new()
        {
            증적Id = record.Id,
            업무Code = record.업무Code,
            출처Code = record.출처Code,
            동의문버전 = record.동의문버전,
            수집이용목적 = record.수집이용목적,
            수집항목 = record.수집항목,
            보유이용기간 = record.보유이용기간,
            동의문Hash = record.동의문Hash,
            상태Code = record.상태Code,
            동의일시Utc = record.동의일시Utc,
            철회일시Utc = record.철회일시Utc
        };
}

internal sealed class Mongo신청개인정보동의증적Store : I신청개인정보동의증적Store
{
    private readonly IMongoCollection<신청개인정보동의증적Record> collection;

    public Mongo신청개인정보동의증적Store(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        collection = client.GetDatabase(options.Value.Database.Trim())
            .GetCollection<신청개인정보동의증적Record>("application_privacy_consent_evidence");
    }

    public async Task<신청개인정보동의증적Record?> 조회Async(Guid evidenceId, CancellationToken cancellationToken)
        => (신청개인정보동의증적Record?)await collection
            .Find(record => record.Id == evidenceId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task 추가Async(신청개인정보동의증적Record record, CancellationToken cancellationToken)
        => collection.InsertOneAsync(record, cancellationToken: cancellationToken);

    public async Task<bool> 교체Async(신청개인정보동의증적Record record, long expectedRevision, CancellationToken cancellationToken)
        => (await collection.ReplaceOneAsync(
            candidate => candidate.Id == record.Id && candidate.Revision == expectedRevision,
            record,
            cancellationToken: cancellationToken)).ModifiedCount == 1;
}

public sealed class InMemory신청개인정보동의증적Store : I신청개인정보동의증적Store
{
    private readonly Dictionary<Guid, 신청개인정보동의증적Record> records = [];
    private readonly object sync = new();

    public Task<신청개인정보동의증적Record?> 조회Async(Guid evidenceId, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            return Task.FromResult(records.TryGetValue(evidenceId, out var record) ? Clone(record) : null);
        }
    }

    public Task 추가Async(신청개인정보동의증적Record record, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (!records.TryAdd(record.Id, Clone(record)))
            {
                throw new InvalidOperationException("같은 개인정보 동의 증적 ID가 이미 존재합니다.");
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> 교체Async(신청개인정보동의증적Record record, long expectedRevision, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (!records.TryGetValue(record.Id, out var current) || current.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }
            records[record.Id] = Clone(record);
            return Task.FromResult(true);
        }
    }

    private static 신청개인정보동의증적Record Clone(신청개인정보동의증적Record record)
        => JsonSerializer.Deserialize<신청개인정보동의증적Record>(JsonSerializer.Serialize(record))!;
}

public sealed class 신청개인정보동의증적Record
{
    [BsonId, BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    public long Revision { get; set; } = 1;
    public string UserId { get; set; } = string.Empty;
    public string 업무Code { get; set; } = string.Empty;
    public string 출처Code { get; set; } = string.Empty;
    public string 동의문버전 { get; set; } = string.Empty;
    public string 수집이용목적 { get; set; } = string.Empty;
    public IReadOnlyList<string> 수집항목 { get; set; } = [];
    public string 보유이용기간 { get; set; } = string.Empty;
    public string 동의문Hash { get; set; } = string.Empty;
    public string 상태Code { get; set; } = 신청개인정보동의상태Codes.유효;
    public DateTime 동의일시Utc { get; set; }
    public DateTime? 철회일시Utc { get; set; }
    public string 철회사유 { get; set; } = string.Empty;
}
