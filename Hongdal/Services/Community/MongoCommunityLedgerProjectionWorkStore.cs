using Hongdal.Contracts.Common.Metadata;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ledger,
    HongdalModuleKind.Persistence,
    "원장 변경의 RDB 투영 작업을 lease로 확보하고 완료·재시도·실패 상태를 기록",
    ReleaseStage = HongdalCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "투영 저장소는 원장 원본이나 업무 상태를 변경하지 않고 이미 기록된 revision의 후속 투영 상태만 관리합니다.")]
public sealed class Mongo커뮤니티원장투영작업저장소 : I커뮤니티원장투영작업저장소
{
    private readonly IMongoCollection<커뮤니티원장문서> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexReady;

    public Mongo커뮤니티원장투영작업저장소(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        _collection = 커뮤니티원장MongoCollectionFactory.Create(mongoClient, options);
    }

    public async Task<커뮤니티원장투영작업?> 다음작업확보Async(
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var leaseExpiredAt = now.Subtract(leaseTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseTimeout);
        var token = Guid.NewGuid().ToString("N");
        var builder = Builders<커뮤니티원장문서>.Filter;
        var due = builder.And(
            builder.In(x => x.투영상태, [커뮤니티원장투영상태.대기, 커뮤니티원장투영상태.재시도대기]),
            builder.Or(
                builder.Eq(x => x.투영다음시도시각Utc, null),
                builder.Lte(x => x.투영다음시도시각Utc, now)));
        var expired = builder.And(
            builder.Eq(x => x.투영상태, 커뮤니티원장투영상태.처리중),
            builder.Or(
                builder.Eq(x => x.투영처리시작시각Utc, null),
                builder.Lte(x => x.투영처리시작시각Utc, leaseExpiredAt)));

        var document = await _collection.FindOneAndUpdateAsync(
            builder.Or(due, expired),
            Builders<커뮤니티원장문서>.Update
                .Set(x => x.투영상태, 커뮤니티원장투영상태.처리중)
                .Set(x => x.투영처리Token, token)
                .Set(x => x.투영처리시작시각Utc, now)
                .Set(x => x.투영마지막오류, null)
                .Inc(x => x.투영시도횟수, 1),
            new FindOneAndUpdateOptions<커뮤니티원장문서>
            {
                Sort = Builders<커뮤니티원장문서>.Sort
                    .Ascending(x => x.투영다음시도시각Utc)
                    .Ascending(x => x.수정시각Utc),
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (document is null)
        {
            return null;
        }

        var stateRequest = string.Equals(
            document.투영변경유형,
            커뮤니티원장변경유형값.상태변경,
            StringComparison.Ordinal)
            ? new 커뮤니티원장상태변경요청
            {
                원장Id = document.원장Id,
                상태 = document.상태,
                이전상태 = document.투영이전상태,
                현재단계Key = document.현재단계Key,
                메모 = document.투영변경메모,
                기대Revision = document.Revision
            }
            : null;

        return new 커뮤니티원장투영작업(
            커뮤니티원장문서읽기Mapper.ToDto(document),
            document.투영EventId ?? 커뮤니티원장EventIdFactory.Create(document.원장Id, document.Revision),
            document.투영변경유형 ?? 커뮤니티원장변경유형값.저장,
            document.수정자,
            stateRequest,
            document.투영발생시각Utc ?? document.수정시각Utc,
            token,
            document.투영시도횟수);
    }

    public async Task 완료Async(
        string 원장Id,
        long revision,
        string? processingToken,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<커뮤니티원장문서>.Filter;
        var filter = builder.And(
            builder.Eq(x => x.원장Id, 원장Id),
            builder.Eq(x => x.Revision, revision));
        filter &= string.IsNullOrWhiteSpace(processingToken)
            ? builder.Ne(x => x.투영상태, 커뮤니티원장투영상태.처리중)
            : builder.Eq(x => x.투영처리Token, processingToken);

        var now = DateTime.UtcNow;
        await _collection.UpdateOneAsync(
            filter,
            Builders<커뮤니티원장문서>.Update
                .Set(x => x.투영완료Revision, revision)
                .Set(x => x.투영상태, 커뮤니티원장투영상태.완료)
                .Set(x => x.투영완료시각Utc, now)
                .Set(x => x.투영다음시도시각Utc, null)
                .Set(x => x.투영처리Token, null)
                .Set(x => x.투영처리시작시각Utc, null)
                .Set(x => x.투영마지막오류, null),
            cancellationToken: cancellationToken);
    }

    public async Task 실패Async(
        string 원장Id,
        long revision,
        string processingToken,
        string 오류,
        int 최대시도횟수,
        TimeSpan 기본재시도간격,
        CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(x =>
                x.원장Id == 원장Id
                && x.Revision == revision
                && x.투영처리Token == processingToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return;
        }

        var attempts = document.투영시도횟수;
        var terminal = attempts >= Math.Max(1, 최대시도횟수);
        var baseSeconds = Math.Max(1, 기본재시도간격.TotalSeconds);
        var delaySeconds = Math.Min(3600, baseSeconds * Math.Pow(2, Math.Max(0, attempts - 1)));
        var now = DateTime.UtcNow;

        await _collection.UpdateOneAsync(
            x => x.원장Id == 원장Id
                 && x.Revision == revision
                 && x.투영처리Token == processingToken,
            Builders<커뮤니티원장문서>.Update
                .Set(x => x.투영상태, terminal ? 커뮤니티원장투영상태.실패 : 커뮤니티원장투영상태.재시도대기)
                .Set(x => x.투영다음시도시각Utc, terminal ? null : now.AddSeconds(delaySeconds))
                .Set(x => x.투영처리Token, null)
                .Set(x => x.투영처리시작시각Utc, null)
                .Set(x => x.투영마지막오류, Truncate(오류, 2000)),
            cancellationToken: cancellationToken);
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexReady)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady)
            {
                return;
            }

            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<커뮤니티원장문서>(
                    Builders<커뮤니티원장문서>.IndexKeys
                        .Ascending(x => x.투영상태)
                        .Ascending(x => x.투영다음시도시각Utc)
                        .Ascending(x => x.수정시각Utc),
                    new CreateIndexOptions { Name = "ix_ledger_projection_queue" }),
                cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];
}
