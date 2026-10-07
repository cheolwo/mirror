using Ssalddel.Contracts.Common.Orderer;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Orderer;

public interface I공동구매해외선적추적저장소
{
    Task<IReadOnlyList<공동구매해외선적추적Dto>> ListAsync(
        공동구매해외선적추적조회조건 query,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적추적Dto?> GetBy문서관리번호Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적추적Dto> UpsertAsync(
        공동구매해외선적추적저장요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적추적Dto?> AppendEventAsync(
        string documentManagementNumber,
        공동구매해외선적추적이벤트추가요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class Mongo공동구매해외선적추적저장소 : I공동구매해외선적추적저장소
{
    private const string CollectionName = "orderer_group_purchase_overseas_shipments";
    private readonly IMongoCollection<공동구매해외선적추적문서> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public Mongo공동구매해외선적추적저장소(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        _collection = mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<공동구매해외선적추적문서>(CollectionName);
    }

    public async Task<IReadOnlyList<공동구매해외선적추적Dto>> ListAsync(
        공동구매해외선적추적조회조건 query,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);

        var items = await _collection
            .Find(BuildFilter(query))
            .SortByDescending(x => x.수정시각Utc)
            .Limit(200)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToArray();
    }

    public async Task<공동구매해외선적추적Dto?> GetBy문서관리번호Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);

        var normalized = NormalizeRequired(documentManagementNumber, "documentManagementNumber");
        var item = await _collection
            .Find(x => x.문서관리번호정규화 == normalized)
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? null : ToDto(item);
    }

    public async Task<공동구매해외선적추적Dto> UpsertAsync(
        공동구매해외선적추적저장요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        Validate(request);

        var now = DateTime.UtcNow;
        var documentManagementNumber = request.문서관리번호.Trim();
        var documentManagementNumberNormalized = NormalizeRequired(documentManagementNumber, "documentManagementNumber");
        var existing = await _collection
            .Find(x => x.문서관리번호정규화 == documentManagementNumberNormalized)
            .FirstOrDefaultAsync(cancellationToken);
        var trackingId = string.IsNullOrWhiteSpace(request.추적Id)
            ? existing?.추적Id ?? ObjectId.GenerateNewId().ToString()
            : request.추적Id.Trim();
        var events = (existing?.이벤트목록 ?? [])
            .Concat(request.이벤트목록.Select(ToDocument))
            .DistinctBy(x => (x.이벤트코드, x.발생시각Utc, x.출처주체코드, x.증빙참조,
                x.위치요약, x.표시명, x.메모, x.주문자공개여부))
            .OrderBy(x => x.발생시각Utc).ToArray();

        var document = new 공동구매해외선적추적문서
        {
            Id = existing?.Id ?? ObjectId.GenerateNewId(),
            추적Id = trackingId,
            공동구매Id = request.공동구매Id.Trim(),
            주문자집단배송권키 = request.주문자집단배송권키.Trim(),
            주문자집단배송권명 = request.주문자집단배송권명.Trim(),
            상품요약 = request.상품요약.Trim(),
            문서관리번호 = documentManagementNumber,
            문서관리번호정규화 = documentManagementNumberNormalized,
            운송문서유형 = Normalize운송문서유형(request.운송문서유형),
            운송문서번호 = request.운송문서번호.Trim(),
            운송문서번호정규화 = NormalizeOptional(request.운송문서번호),
            운송수단 = Normalize운송수단(request.운송수단),
            운송사명 = request.운송사명.Trim(),
            선박명 = request.선박명.Trim(),
            항차번호 = request.항차번호.Trim(),
            항공편번호 = request.항공편번호.Trim(),
            출발국가코드 = request.출발국가코드.Trim().ToUpperInvariant(),
            출발항코드 = request.출발항코드.Trim().ToUpperInvariant(),
            도착항코드 = request.도착항코드.Trim().ToUpperInvariant(),
            예상출발시각Utc = request.예상출발시각Utc,
            실제출발시각Utc = request.실제출발시각Utc,
            예상도착시각Utc = request.예상도착시각Utc,
            실제도착시각Utc = request.실제도착시각Utc,
            현재상태코드 = string.IsNullOrWhiteSpace(request.현재상태코드)
                ? Resolve현재상태코드(events)
                : request.현재상태코드.Trim(),
            현재위치요약 = request.현재위치요약.Trim(),
            마지막단계시각Utc = request.마지막단계시각Utc ?? events.LastOrDefault()?.발생시각Utc,
            이벤트목록 = events,
            관리자메모 = request.관리자메모.Trim(),
            수정자 = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim(),
            생성시각Utc = existing?.생성시각Utc ?? now,
            수정시각Utc = now
        };

        var lastNormal = existing is null ? null : 공동구매선적진행Policy.LastNormalState(existing.현재상태코드,
            existing.마지막정상상태코드, existing.이벤트목록.Select(x => x.이벤트코드));
        if (existing is not null && !공동구매선적진행Policy.CanAdvance(existing.현재상태코드,
                existing.마지막단계시각Utc, document.현재상태코드, document.마지막단계시각Utc ?? DateTime.MinValue, lastNormal))
        {
            document.현재상태코드 = existing.현재상태코드;
            document.현재위치요약 = existing.현재위치요약;
            document.마지막단계시각Utc = existing.마지막단계시각Utc;
        }
        document.마지막정상상태코드 = 공동구매선적진행Policy.LastNormalState(document.현재상태코드, lastNormal,
            events.Select(x => x.이벤트코드));

        var filter = Builders<공동구매해외선적추적문서>.Filter.Eq(x => x.문서관리번호정규화, documentManagementNumberNormalized);
        if (existing is not null)
            filter &= Builders<공동구매해외선적추적문서>.Filter.Eq(x => x.수정시각Utc, existing.수정시각Utc)
                & Builders<공동구매해외선적추적문서>.Filter.Size(x => x.이벤트목록, existing.이벤트목록.Count);
        var saved = await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = existing is null },
            cancellationToken);
        if (existing is not null && saved.MatchedCount == 0)
            throw new InvalidOperationException("선적 상태가 다른 요청에서 변경되었습니다. 최신 추적 정보를 다시 조회한 뒤 재시도해 주세요.");

        return ToDto(document);
    }

    public async Task<공동구매해외선적추적Dto?> AppendEventAsync(
        string documentManagementNumber,
        공동구매해외선적추적이벤트추가요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        Validate(request);

        var normalized = NormalizeRequired(documentManagementNumber, "documentManagementNumber");
        var eventDocument = ToDocument(request);
        var now = DateTime.UtcNow;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var current = await _collection.Find(x => x.문서관리번호정규화 == normalized)
                .FirstOrDefaultAsync(cancellationToken);
            if (current is null) return null;
            var update = Builders<공동구매해외선적추적문서>.Update
                .Push(x => x.이벤트목록, eventDocument)
                .Set(x => x.수정자, string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim())
                .Set(x => x.수정시각Utc, now);
            var lastNormal = 공동구매선적진행Policy.LastNormalState(current.현재상태코드,
                current.마지막정상상태코드, current.이벤트목록.Select(x => x.이벤트코드));
            if (공동구매선적진행Policy.CanAdvance(current.현재상태코드, current.마지막단계시각Utc,
                    eventDocument.이벤트코드, eventDocument.발생시각Utc, lastNormal))
            {
                update = update.Set(x => x.현재상태코드, eventDocument.이벤트코드)
                    .Set(x => x.마지막정상상태코드, 공동구매선적진행Policy.LastNormalState(eventDocument.이벤트코드, lastNormal, []))
                    .Set(x => x.현재위치요약, eventDocument.위치요약)
                    .Set(x => x.마지막단계시각Utc, eventDocument.발생시각Utc);
            }
            // 투영 판단 이후 다른 이벤트가 진행했다면 재조회한다. 이력 추가와 투영은 한 쓰기다.
            var item = await _collection.FindOneAndUpdateAsync(
                x => x.문서관리번호정규화 == normalized
                    && x.현재상태코드 == current.현재상태코드
                    && x.마지막단계시각Utc == current.마지막단계시각Utc,
                update,
                new FindOneAndUpdateOptions<공동구매해외선적추적문서> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
            if (item is not null) return ToDto(item);
        }

        throw new InvalidOperationException("선적 상태가 다른 요청에서 변경되었습니다. 최신 추적 정보를 다시 조회한 뒤 재시도해 주세요.");
    }

    private FilterDefinition<공동구매해외선적추적문서> BuildFilter(
        공동구매해외선적추적조회조건 query)
    {
        var builder = Builders<공동구매해외선적추적문서>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(query.공동구매Id))
        {
            filter &= builder.Eq(x => x.공동구매Id, query.공동구매Id.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.주문자집단배송권키))
        {
            filter &= builder.Eq(x => x.주문자집단배송권키, query.주문자집단배송권키.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.문서관리번호))
        {
            filter &= builder.Eq(
                x => x.문서관리번호정규화,
                NormalizeRequired(query.문서관리번호, "documentManagementNumber"));
        }

        if (!string.IsNullOrWhiteSpace(query.운송문서번호))
        {
            filter &= builder.Eq(
                x => x.운송문서번호정규화,
                NormalizeOptional(query.운송문서번호));
        }

        if (!string.IsNullOrWhiteSpace(query.현재상태코드))
        {
            filter &= builder.Eq(x => x.현재상태코드, query.현재상태코드.Trim());
        }

        return filter;
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

            var indexes = new[]
            {
                new CreateIndexModel<공동구매해외선적추적문서>(
                    Builders<공동구매해외선적추적문서>.IndexKeys
                        .Ascending(x => x.문서관리번호정규화),
                    new CreateIndexOptions { Unique = true, Name = "ux_document_management_number" }),
                new CreateIndexModel<공동구매해외선적추적문서>(
                    Builders<공동구매해외선적추적문서>.IndexKeys
                        .Ascending(x => x.공동구매Id)
                        .Ascending(x => x.주문자집단배송권키),
                    new CreateIndexOptions { Name = "ix_group_purchase_scope" }),
                new CreateIndexModel<공동구매해외선적추적문서>(
                    Builders<공동구매해외선적추적문서>.IndexKeys
                        .Ascending(x => x.운송문서번호정규화),
                    new CreateIndexOptions { Name = "ix_transport_document_number" }),
                new CreateIndexModel<공동구매해외선적추적문서>(
                    Builders<공동구매해외선적추적문서>.IndexKeys
                        .Ascending(x => x.현재상태코드)
                        .Descending(x => x.수정시각Utc),
                    new CreateIndexOptions { Name = "ix_status_updated" })
            };

            await _collection.Indexes.CreateManyAsync(indexes, cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static void Validate(공동구매해외선적추적저장요청 request)
    {
        if (string.IsNullOrWhiteSpace(request.공동구매Id)) throw new InvalidOperationException("groupPurchaseId is required.");
        if (string.IsNullOrWhiteSpace(request.주문자집단배송권키)) throw new InvalidOperationException("ordererGroupScopeKey is required.");
        if (string.IsNullOrWhiteSpace(request.문서관리번호)) throw new InvalidOperationException("documentManagementNumber is required.");
        if (string.IsNullOrWhiteSpace(request.운송문서번호)) throw new InvalidOperationException("transportDocumentNumber is required.");
        if (Normalize운송문서유형(request.운송문서유형) == 공동구매선적문서유형코드.항공화물운송장
            && Normalize운송수단(request.운송수단) != 공동구매선적운송수단코드.항공)
        {
            throw new InvalidOperationException("airWaybill requires air transport mode.");
        }
    }

    private static void Validate(공동구매해외선적추적이벤트추가요청 request)
    {
        if (string.IsNullOrWhiteSpace(request.이벤트코드)) throw new InvalidOperationException("eventCode is required.");
        if (string.IsNullOrWhiteSpace(request.표시명)) throw new InvalidOperationException("displayName is required.");
        if (string.IsNullOrWhiteSpace(request.출처주체코드)) throw new InvalidOperationException("sourcePartyCode is required.");
    }

    private static string Resolve현재상태코드(IReadOnlyList<공동구매해외선적추적이벤트문서> events)
        => events.LastOrDefault()?.이벤트코드 ?? 공동구매선적상태코드.문서등록;

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return NormalizeOptional(value);
    }

    private static string NormalizeOptional(string? value)
        => (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();

    private static string Normalize운송문서유형(string? value)
        => string.Equals(value?.Trim(), 공동구매선적문서유형코드.항공화물운송장, StringComparison.OrdinalIgnoreCase)
            ? 공동구매선적문서유형코드.항공화물운송장
            : 공동구매선적문서유형코드.선하증권;

    private static string Normalize운송수단(string? value)
        => string.Equals(value?.Trim(), 공동구매선적운송수단코드.항공, StringComparison.OrdinalIgnoreCase)
            ? 공동구매선적운송수단코드.항공
            : 공동구매선적운송수단코드.해상;

    private static 공동구매해외선적추적이벤트문서 ToDocument(
        공동구매해외선적추적이벤트Dto source)
        => new()
        {
            이벤트코드 = source.이벤트코드.Trim(),
            표시명 = source.표시명.Trim(),
            위치요약 = source.위치요약.Trim(),
            발생시각Utc = source.발생시각Utc,
            출처주체코드 = source.출처주체코드.Trim(),
            증빙참조 = source.증빙참조.Trim(),
            메모 = source.메모.Trim(),
            주문자공개여부 = source.주문자공개여부
        };

    private static 공동구매해외선적추적이벤트문서 ToDocument(
        공동구매해외선적추적이벤트추가요청 source)
        => new()
        {
            이벤트코드 = source.이벤트코드.Trim(),
            표시명 = source.표시명.Trim(),
            위치요약 = source.위치요약.Trim(),
            발생시각Utc = source.발생시각Utc ?? DateTime.UtcNow,
            출처주체코드 = source.출처주체코드.Trim(),
            증빙참조 = source.증빙참조.Trim(),
            메모 = source.메모.Trim(),
            주문자공개여부 = source.주문자공개여부
        };

    private static 공동구매해외선적추적Dto ToDto(
        공동구매해외선적추적문서 source)
        => new()
        {
            추적Id = source.추적Id,
            공동구매Id = source.공동구매Id,
            주문자집단배송권키 = source.주문자집단배송권키,
            주문자집단배송권명 = source.주문자집단배송권명,
            상품요약 = source.상품요약,
            문서관리번호 = source.문서관리번호,
            운송문서유형 = source.운송문서유형,
            운송문서번호 = source.운송문서번호,
            운송수단 = source.운송수단,
            운송사명 = source.운송사명,
            선박명 = source.선박명,
            항차번호 = source.항차번호,
            항공편번호 = source.항공편번호,
            출발국가코드 = source.출발국가코드,
            출발항코드 = source.출발항코드,
            도착항코드 = source.도착항코드,
            예상출발시각Utc = source.예상출발시각Utc,
            실제출발시각Utc = source.실제출발시각Utc,
            예상도착시각Utc = source.예상도착시각Utc,
            실제도착시각Utc = source.실제도착시각Utc,
            현재상태코드 = source.현재상태코드,
            현재위치요약 = source.현재위치요약,
            마지막단계시각Utc = source.마지막단계시각Utc,
            이벤트목록 = source.이벤트목록.OrderBy(x => x.발생시각Utc).Select(ToDto).ToArray(),
            관리자메모 = source.관리자메모,
            수정자 = source.수정자,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };

    private static 공동구매해외선적추적이벤트Dto ToDto(
        공동구매해외선적추적이벤트문서 source)
        => new()
        {
            이벤트코드 = source.이벤트코드,
            표시명 = source.표시명,
            위치요약 = source.위치요약,
            발생시각Utc = source.발생시각Utc,
            출처주체코드 = source.출처주체코드,
            증빙참조 = source.증빙참조,
            메모 = source.메모,
            주문자공개여부 = source.주문자공개여부
        };
}

public sealed class 공동구매해외선적추적문서
{
    public string? 마지막정상상태코드 { get; set; }
    [BsonId]
    public ObjectId Id { get; set; }
    public string 추적Id { get; set; } = string.Empty;
    public string 공동구매Id { get; set; } = string.Empty;
    public string 주문자집단배송권키 { get; set; } = string.Empty;
    public string 주문자집단배송권명 { get; set; } = string.Empty;
    public string 상품요약 { get; set; } = string.Empty;
    public string 문서관리번호 { get; set; } = string.Empty;
    public string 문서관리번호정규화 { get; set; } = string.Empty;
    public string 운송문서유형 { get; set; } = 공동구매선적문서유형코드.선하증권;
    public string 운송문서번호 { get; set; } = string.Empty;
    public string 운송문서번호정규화 { get; set; } = string.Empty;
    public string 운송수단 { get; set; } = 공동구매선적운송수단코드.해상;
    public string 운송사명 { get; set; } = string.Empty;
    public string 선박명 { get; set; } = string.Empty;
    public string 항차번호 { get; set; } = string.Empty;
    public string 항공편번호 { get; set; } = string.Empty;
    public string 출발국가코드 { get; set; } = string.Empty;
    public string 출발항코드 { get; set; } = string.Empty;
    public string 도착항코드 { get; set; } = string.Empty;
    public DateTime? 예상출발시각Utc { get; set; }
    public DateTime? 실제출발시각Utc { get; set; }
    public DateTime? 예상도착시각Utc { get; set; }
    public DateTime? 실제도착시각Utc { get; set; }
    public string 현재상태코드 { get; set; } = 공동구매선적상태코드.문서등록;
    public string 현재위치요약 { get; set; } = string.Empty;
    public DateTime? 마지막단계시각Utc { get; set; }
    public IReadOnlyList<공동구매해외선적추적이벤트문서> 이벤트목록 { get; set; } = [];
    public string 관리자메모 { get; set; } = string.Empty;
    public string 수정자 { get; set; } = string.Empty;
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 공동구매해외선적추적이벤트문서
{
    public string 이벤트코드 { get; set; } = string.Empty;
    public string 표시명 { get; set; } = string.Empty;
    public string 위치요약 { get; set; } = string.Empty;
    public DateTime 발생시각Utc { get; set; }
    public string 출처주체코드 { get; set; } = string.Empty;
    public string 증빙참조 { get; set; } = string.Empty;
    public string 메모 { get; set; } = string.Empty;
    public bool 주문자공개여부 { get; set; } = true;
}
