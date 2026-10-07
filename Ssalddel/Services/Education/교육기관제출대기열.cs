using Ssalddel.Contracts.Common.Education;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Education;

public interface I교육기관제출대기열
{
    Task<현장체험제출상태응답> 예약Async(
        string 제출Id,
        string 원장Id,
        string 전송방식,
        string? 제출처Key,
        string? 담당이메일,
        CancellationToken cancellationToken);

    Task<교육기관제출작업?> 다음작업확보Async(CancellationToken cancellationToken);

    Task 완료Async(string 제출Id, string 상태, CancellationToken cancellationToken);

    Task 원장반영완료Async(string 제출Id, CancellationToken cancellationToken);

    Task 실패Async(
        string 제출Id,
        string 오류,
        bool 설정대기,
        int 최대시도횟수,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<현장체험제출상태응답>> 원장별조회Async(
        string 원장Id,
        CancellationToken cancellationToken);
}

public sealed record 교육기관제출작업(
    string 제출Id,
    string 원장Id,
    string 전송방식,
    string? 제출처Key,
    string? 담당이메일,
    int 시도횟수,
    bool 전송완료 = false);

public sealed class Mongo교육기관제출대기열 : I교육기관제출대기열
{
    private const string CollectionName = "education_field_experience_submissions";
    private readonly IMongoCollection<교육기관제출문서> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public Mongo교육기관제출대기열(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        _collection = mongoClient
            .GetDatabase(options.Value.Database.Trim())
            .GetCollection<교육기관제출문서>(CollectionName);
    }

    public async Task<현장체험제출상태응답> 예약Async(
        string 제출Id,
        string 원장Id,
        string 전송방식,
        string? 제출처Key,
        string? 담당이메일,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var document = new 교육기관제출문서
        {
            제출Id = 제출Id,
            원장Id = 원장Id,
            전송방식 = 전송방식,
            제출처Key = Clean(제출처Key),
            담당이메일 = Clean(담당이메일),
            상태 = 교육기관제출상태.전송대기,
            다음시도시각Utc = now,
            생성시각Utc = now,
            수정시각Utc = now
        };

        var saved = await _collection.FindOneAndUpdateAsync(
            x => x.제출Id == 제출Id,
            Builders<교육기관제출문서>.Update
                .SetOnInsert(x => x.원장Id, document.원장Id)
                .SetOnInsert(x => x.전송방식, document.전송방식)
                .SetOnInsert(x => x.제출처Key, document.제출처Key)
                .SetOnInsert(x => x.담당이메일, document.담당이메일)
                .SetOnInsert(x => x.상태, document.상태)
                .SetOnInsert(x => x.다음시도시각Utc, now)
                .SetOnInsert(x => x.생성시각Utc, now)
                .SetOnInsert(x => x.수정시각Utc, now),
            new FindOneAndUpdateOptions<교육기관제출문서> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (saved.원장Id != document.원장Id || saved.전송방식 != document.전송방식
            || saved.제출처Key != document.제출처Key || saved.담당이메일 != document.담당이메일)
            throw new InvalidOperationException("같은 제출Id에 다른 제출 내용을 예약할 수 없습니다.");
        return ToResponse(saved);
    }

    public async Task<교육기관제출작업?> 다음작업확보Async(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var expiredLease = now.AddMinutes(-5);
        var filter = Builders<교육기관제출문서>.Filter;
        var document = await _collection.FindOneAndUpdateAsync(
            filter.Where(x => ((x.상태 == 교육기관제출상태.전송대기 || x.상태 == 교육기관제출상태.설정대기)
                  && x.다음시도시각Utc <= now)
                 || (x.상태 == 교육기관제출상태.전송중 && x.수정시각Utc <= expiredLease))
                | (filter.Eq(x => x.상태, 교육기관제출상태.전송완료)
                    & filter.Ne(x => x.원장반영완료, true)
                    & filter.Lte(x => x.다음시도시각Utc, now)),
            Builders<교육기관제출문서>.Update
                .Set(x => x.상태, 교육기관제출상태.전송중)
                .Set(x => x.수정시각Utc, now)
                .Inc(x => x.시도횟수, 1),
            new FindOneAndUpdateOptions<교육기관제출문서>
            {
                Sort = Builders<교육기관제출문서>.Sort.Ascending(x => x.다음시도시각Utc),
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        return document is null
            ? null
            : new 교육기관제출작업(
                document.제출Id,
                document.원장Id,
                document.전송방식,
                document.제출처Key,
                document.담당이메일,
                document.시도횟수,
                document.전송완료시각Utc.HasValue);
    }

    public async Task 완료Async(string 제출Id, string 상태, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await _collection.UpdateOneAsync(
            x => x.제출Id == 제출Id && x.전송완료시각Utc == null,
            Builders<교육기관제출문서>.Update
                .Set(x => x.상태, 상태)
                .Set(x => x.전송완료시각Utc, now)
                .Set(x => x.원장반영완료, 상태 != 교육기관제출상태.전송완료)
                .Set(x => x.다음시도시각Utc, now.AddMinutes(5))
                .Set(x => x.수정시각Utc, now)
                .Unset(x => x.마지막오류),
            cancellationToken: cancellationToken);
    }

    public Task 원장반영완료Async(string 제출Id, CancellationToken cancellationToken)
        => _collection.UpdateOneAsync(
            x => x.제출Id == 제출Id && x.전송완료시각Utc != null,
            Builders<교육기관제출문서>.Update
                .Set(x => x.원장반영완료, true)
                .Set(x => x.상태, 교육기관제출상태.전송완료)
                .Set(x => x.수정시각Utc, DateTime.UtcNow)
                .Unset(x => x.마지막오류),
            cancellationToken: cancellationToken);

    public async Task 실패Async(
        string 제출Id,
        string 오류,
        bool 설정대기,
        int 최대시도횟수,
        CancellationToken cancellationToken)
    {
        var document = await _collection.Find(x => x.제출Id == 제출Id).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return;
        }
        if (document.원장반영완료) return;

        var now = DateTime.UtcNow;
        var 재시도가능 = !설정대기 && document.시도횟수 < Math.Max(1, 최대시도횟수);
        var 상태 = document.전송완료시각Utc.HasValue
            ? 교육기관제출상태.전송완료
            : 설정대기
            ? 교육기관제출상태.설정대기
            : 재시도가능 ? 교육기관제출상태.전송대기 : 교육기관제출상태.전송실패;
        var delayMinutes = 설정대기
            ? 30
            : Math.Min(60, Math.Pow(2, Math.Max(0, document.시도횟수 - 1)));

        await _collection.UpdateOneAsync(
            Builders<교육기관제출문서>.Filter.Eq(x => x.제출Id, 제출Id)
                & Builders<교육기관제출문서>.Filter.Ne(x => x.원장반영완료, true)
                & Builders<교육기관제출문서>.Filter.Eq(x => x.전송완료시각Utc, document.전송완료시각Utc),
            Builders<교육기관제출문서>.Update
                .Set(x => x.상태, 상태)
                .Set(x => x.마지막오류, 오류)
                .Set(x => x.다음시도시각Utc, now.AddMinutes(delayMinutes))
                .Set(x => x.수정시각Utc, now),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<현장체험제출상태응답>> 원장별조회Async(
        string 원장Id,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var documents = await _collection
            .Find(x => x.원장Id == 원장Id)
            .SortByDescending(x => x.생성시각Utc)
            .ToListAsync(cancellationToken);
        return documents.Select(ToResponse).ToArray();
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady)
            {
                return;
            }

            await _collection.Indexes.CreateManyAsync(
            [
                // 제출Id는 BsonId로 Mongo의 고유 _id 인덱스를 사용합니다.
                // _id에 unique 옵션을 붙여 별도 이름으로 만들면 Mongo 8이 거절합니다.
                new CreateIndexModel<교육기관제출문서>(
                    Builders<교육기관제출문서>.IndexKeys
                        .Ascending(x => x.상태)
                        .Ascending(x => x.다음시도시각Utc),
                    new CreateIndexOptions { Name = "ix_education_submission_pending" }),
                new CreateIndexModel<교육기관제출문서>(
                    Builders<교육기관제출문서>.IndexKeys
                        .Ascending(x => x.원장Id)
                        .Descending(x => x.생성시각Utc),
                    new CreateIndexOptions { Name = "ix_education_submission_ledger" })
            ], cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static 현장체험제출상태응답 ToResponse(교육기관제출문서 document)
        => new()
        {
            제출Id = document.제출Id,
            전송방식 = document.전송방식,
            상태 = document.상태,
            제출처 = document.전송방식 == 교육기관제출방식.Api ? document.제출처Key : document.담당이메일,
            마지막오류 = document.마지막오류,
            생성시각Utc = document.생성시각Utc,
            전송완료시각Utc = document.전송완료시각Utc
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class 교육기관제출문서
{
    [BsonId]
    public string 제출Id { get; set; } = string.Empty;
    public string 원장Id { get; set; } = string.Empty;
    public string 전송방식 { get; set; } = string.Empty;
    public string? 제출처Key { get; set; }
    public string? 담당이메일 { get; set; }
    public string 상태 { get; set; } = 교육기관제출상태.전송대기;
    public int 시도횟수 { get; set; }
    public string? 마지막오류 { get; set; }
    public DateTime 다음시도시각Utc { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
    public DateTime? 전송완료시각Utc { get; set; }
    public bool 원장반영완료 { get; set; }
}
