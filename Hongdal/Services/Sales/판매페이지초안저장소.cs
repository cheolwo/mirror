using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace 홍달.Services.Sales;

public interface I판매페이지초안저장소
{
    Task<IReadOnlyList<판매페이지초안저장모델>> 목록Async(string ownerUserId, CancellationToken cancellationToken);
    Task<판매페이지초안저장모델?> 조회Async(string pageId, string ownerUserId, CancellationToken cancellationToken);
    Task<판매페이지초안저장모델> 저장Async(판매페이지초안저장모델 model, long expectedRevision, CancellationToken cancellationToken);
}

public sealed class Mongo판매페이지초안저장소 : I판매페이지초안저장소
{
    private const string CollectionName = "sales_page_drafts";
    private readonly IMongoCollection<판매페이지초안문서> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public Mongo판매페이지초안저장소(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        _collection = mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<판매페이지초안문서>(CollectionName);
    }

    public async Task<IReadOnlyList<판매페이지초안저장모델>> 목록Async(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var documents = await _collection
            .Find(x => x.소유자UserId == ownerUserId)
            .SortByDescending(x => x.수정시각Utc)
            .Limit(100)
            .ToListAsync(cancellationToken);
        return documents.Select(ToModel).ToArray();
    }

    public async Task<판매페이지초안저장모델?> 조회Async(
        string pageId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var document = await _collection
            .Find(x => x.페이지Id == pageId && x.소유자UserId == ownerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToModel(document);
    }

    public async Task<판매페이지초안저장모델> 저장Async(
        판매페이지초안저장모델 model,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var document = ToDocument(model, expectedRevision + 1);
        if (expectedRevision == 0)
        {
            try
            {
                await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new InvalidOperationException("같은 판매 페이지 초안이 이미 존재합니다.", ex);
            }
        }
        else
        {
            var current = await _collection
                .Find(x => x.페이지Id == model.페이지Id
                           && x.소유자UserId == model.소유자UserId
                           && x.Revision == expectedRevision)
                .Project(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (current == ObjectId.Empty)
            {
                throw new InvalidOperationException("판매 페이지 초안이 이미 변경되었습니다. 최신 내용을 다시 불러와 주세요.");
            }

            document.Id = current;
            var result = await _collection.ReplaceOneAsync(
                x => x.페이지Id == model.페이지Id
                     && x.소유자UserId == model.소유자UserId
                     && x.Revision == expectedRevision,
                document,
                cancellationToken: cancellationToken);
            if (result.MatchedCount == 0)
            {
                throw new InvalidOperationException("판매 페이지 초안이 이미 변경되었습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        return ToModel(document);
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady) return;
            await _collection.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<판매페이지초안문서>(
                        Builders<판매페이지초안문서>.IndexKeys.Ascending(x => x.페이지Id),
                        new CreateIndexOptions { Unique = true, Name = "ux_sales_page_id" }),
                    new CreateIndexModel<판매페이지초안문서>(
                        Builders<판매페이지초안문서>.IndexKeys
                            .Ascending(x => x.소유자UserId)
                            .Descending(x => x.수정시각Utc),
                        new CreateIndexOptions { Name = "ix_sales_page_owner_updated" })
                ],
                cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static 판매페이지초안문서 ToDocument(판매페이지초안저장모델 source, long revision)
        => new()
        {
            Id = ObjectId.GenerateNewId(),
            페이지Id = source.페이지Id,
            소유자UserId = source.소유자UserId,
            상태 = source.상태,
            판매자유형 = source.판매자유형,
            판매자표시명 = source.판매자표시명,
            상품명 = source.상품명,
            한줄소개 = source.한줄소개,
            상세설명 = source.상세설명,
            원산지표시 = source.원산지표시,
            출고지표시 = source.출고지표시,
            판매가 = source.판매가,
            통화코드 = source.통화코드,
            최소주문수량 = source.최소주문수량,
            개별주문허용 = source.개별주문허용,
            공동주문허용 = source.공동주문허용,
            공동주문최소수량 = source.공동주문최소수량,
            이미지Url목록 = source.이미지Url목록.ToArray(),
            핵심정보목록 = source.핵심정보목록.ToArray(),
            외부참고자료 = source.외부참고자료,
            연결된판매상품Id = source.연결된판매상품Id,
            Revision = revision,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };

    private static 판매페이지초안저장모델 ToModel(판매페이지초안문서 source)
        => new()
        {
            페이지Id = source.페이지Id,
            소유자UserId = source.소유자UserId,
            상태 = source.상태,
            판매자유형 = source.판매자유형,
            판매자표시명 = source.판매자표시명,
            상품명 = source.상품명,
            한줄소개 = source.한줄소개,
            상세설명 = source.상세설명,
            원산지표시 = source.원산지표시,
            출고지표시 = source.출고지표시,
            판매가 = source.판매가,
            통화코드 = source.통화코드,
            최소주문수량 = source.최소주문수량,
            개별주문허용 = source.개별주문허용,
            공동주문허용 = source.공동주문허용,
            공동주문최소수량 = source.공동주문최소수량,
            이미지Url목록 = source.이미지Url목록,
            핵심정보목록 = source.핵심정보목록,
            외부참고자료 = source.외부참고자료,
            연결된판매상품Id = source.연결된판매상품Id,
            Revision = source.Revision,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };
}

public sealed class 판매페이지초안문서
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string 페이지Id { get; set; } = string.Empty;
    public string 소유자UserId { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string 판매자유형 { get; set; } = string.Empty;
    public string 판매자표시명 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 한줄소개 { get; set; } = string.Empty;
    public string 상세설명 { get; set; } = string.Empty;
    public string? 원산지표시 { get; set; }
    public string? 출고지표시 { get; set; }
    public decimal? 판매가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public int 최소주문수량 { get; set; }
    public bool 개별주문허용 { get; set; }
    public bool 공동주문허용 { get; set; }
    public int? 공동주문최소수량 { get; set; }
    public IReadOnlyList<string> 이미지Url목록 { get; set; } = [];
    public IReadOnlyList<string> 핵심정보목록 { get; set; } = [];
    public 판매페이지외부참고저장모델? 외부참고자료 { get; set; }
    public long? 연결된판매상품Id { get; set; }
    public long Revision { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}
