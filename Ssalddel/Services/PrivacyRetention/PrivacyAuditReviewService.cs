using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.PrivacyRetention;

public sealed class 개인정보접속점검Record
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string ReviewerUserId { get; set; } = string.Empty;
    public string EvidenceId { get; set; } = string.Empty;
    public string ResultCode { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime ReviewedAtUtc { get; set; }
}

public interface I개인정보접속점검Service
{
    Task<bool> 현재월점검완료Async(CancellationToken ct = default);
    Task 점검기록Async(string adminUserId, string evidenceId, string resultCode, DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken ct = default);
}

/// <summary>관리자가 실제 수행한 점검의 증적만 기록하며 자동 스캔을 사람의 점검으로 인증하지 않습니다.</summary>
public sealed class 개인정보접속점검Service : I개인정보접속점검Service
{
    private readonly IMongoCollection<개인정보접속점검Record> _records;
    private readonly TimeProvider _clock;
    public 개인정보접속점검Service(IMongoClient client, IOptions<MongoDbOptions> options, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database)) throw new InvalidOperationException("MongoDb:Database is required.");
        _records = client.GetDatabase(options.Value.Database.Trim()).GetCollection<개인정보접속점검Record>("privacy_access_audit_reviews");
        _clock = clock;
    }
    public Task<bool> 현재월점검완료Async(CancellationToken ct = default)
    {
        var key = 점검월(_clock.GetUtcNow().UtcDateTime);
        return _records.Find(x => x.Id == key).AnyAsync(ct);
    }
    public async Task 점검기록Async(string adminUserId, string evidenceId, string resultCode, DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var period = 요구점검기간(now);
        if (string.IsNullOrWhiteSpace(adminUserId) || string.IsNullOrWhiteSpace(evidenceId) || evidenceId.Length > 160
            || evidenceId.Any(x => char.IsControl(x) || char.IsWhiteSpace(x)) || resultCode is not ("Reviewed" or "FindingsRecorded")
            || periodStartUtc >= periodEndUtc || periodEndUtc > now || periodStartUtc < now.AddDays(-62)
            || periodStartUtc > period.StartUtc || periodEndUtc < period.EndUtc)
            throw new InvalidOperationException("AuditReviewEvidenceInvalid");
        var record = new 개인정보접속점검Record
        {
            Id = 점검월(now),
            ReviewerUserId = adminUserId, EvidenceId = evidenceId, ResultCode = resultCode,
            PeriodStartUtc = periodStartUtc, PeriodEndUtc = periodEndUtc, ReviewedAtUtc = now
        };
        // One retained record per month; repeat requests cannot replace who reviewed which evidence.
        try { await _records.InsertOneAsync(record, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _records.Find(x => x.Id == record.Id).FirstOrDefaultAsync(ct);
            if (existing?.ReviewerUserId != adminUserId || existing.EvidenceId != evidenceId || existing.ResultCode != resultCode)
                throw new InvalidOperationException("AuditReviewAlreadyRecorded");
        }
    }
    internal static string 점검월(DateTime nowUtc)
        => nowUtc.AddHours(9).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    internal static (DateTime StartUtc, DateTime EndUtc, DateTime DeadlineUtc) 요구점검기간(DateTime nowUtc)
    {
        var korea = nowUtc.AddHours(9);
        var koreaMonthStart = new DateTime(korea.Year, korea.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        // Move between Korean calendar months before converting to UTC; UTC is still the previous month's last day.
        return (koreaMonthStart.AddMonths(-1).AddHours(-9), koreaMonthStart.AddHours(-9), koreaMonthStart.AddMonths(1).AddHours(-9));
    }
}
