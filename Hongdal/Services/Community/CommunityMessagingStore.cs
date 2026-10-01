using Hongdal.Contracts.Common.Community;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public interface I커뮤니티대화저장소
{
    Task<커뮤니티대화방Dto> 대화방저장Async(
        커뮤니티대화방저장요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<커뮤니티대화방Dto?> 대화방조회Async(
        string 대화방Id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<커뮤니티대화방Dto>> 대화방목록조회Async(
        커뮤니티대화방조회조건 query,
        CancellationToken cancellationToken = default);

    Task<커뮤니티메시지Dto> 메시지저장Async(
        커뮤니티메시지저장요청 request,
        string senderUserId,
        string senderDisplayName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<커뮤니티메시지Dto>> 메시지목록조회Async(
        커뮤니티메시지조회조건 query,
        CancellationToken cancellationToken = default);

    Task<커뮤니티대화방Dto?> 읽음표시Async(
        커뮤니티메시지읽음표시요청 request,
        string userId,
        CancellationToken cancellationToken = default);
}

public sealed class Mongo커뮤니티대화저장소 : I커뮤니티대화저장소
{
    private const string ConversationCollectionName = "community_conversations";
    private const string MessageCollectionName = "community_messages";

    private readonly IMongoCollection<커뮤니티대화방문서> _conversations;
    private readonly IMongoCollection<커뮤니티메시지문서> _messages;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public Mongo커뮤니티대화저장소(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        var database = mongoClient.GetDatabase(databaseName.Trim());
        _conversations = database.GetCollection<커뮤니티대화방문서>(ConversationCollectionName);
        _messages = database.GetCollection<커뮤니티메시지문서>(MessageCollectionName);
    }

    public async Task<커뮤니티대화방Dto> 대화방저장Async(
        커뮤니티대화방저장요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        Validate(request);

        var now = DateTime.UtcNow;
        var 대화방Id = string.IsNullOrWhiteSpace(request.대화방Id)
            ? $"conversation-{Guid.NewGuid():N}"
            : request.대화방Id.Trim();

        var existing = await _conversations
            .Find(x => x.대화방Id == 대화방Id)
            .FirstOrDefaultAsync(cancellationToken);

        var 문서 = new 커뮤니티대화방문서
        {
            대화방Id = 대화방Id,
            커뮤니티Id = request.커뮤니티Id.Trim(),
            유형 = string.IsNullOrWhiteSpace(request.유형) ? 커뮤니티대화방유형.Group : request.유형.Trim(),
            제목 = request.제목.Trim(),
            원장Id = Clean(request.원장Id),
            원장템플릿Key = Clean(request.원장템플릿Key),
            다이어그램Id = Clean(request.다이어그램Id),
            다이어그램이름 = Clean(request.다이어그램이름),
            업무Context = request.업무Context is null ? null : ToDocument(request.업무Context),
            참여자목록 = request.참여자목록.Select(ToDocument).ToArray(),
            마지막메시지Id = existing?.마지막메시지Id,
            마지막메시지요약 = existing?.마지막메시지요약,
            마지막메시지종류 = existing?.마지막메시지종류,
            마지막메시지시각Utc = existing?.마지막메시지시각Utc,
            확장속성 = NormalizeDictionary(request.확장속성),
            생성시각Utc = existing?.생성시각Utc ?? now,
            수정시각Utc = now,
            수정자 = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim()
        };

        await _conversations.ReplaceOneAsync(
            x => x.대화방Id == 대화방Id,
            문서,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return ToDto(문서);
    }

    public async Task<커뮤니티대화방Dto?> 대화방조회Async(
        string 대화방Id,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(대화방Id))
        {
            return null;
        }

        var 문서 = await _conversations
            .Find(x => x.대화방Id == 대화방Id.Trim())
            .FirstOrDefaultAsync(cancellationToken);

        return 문서 is null ? null : ToDto(문서);
    }

    public async Task<IReadOnlyList<커뮤니티대화방Dto>> 대화방목록조회Async(
        커뮤니티대화방조회조건 query,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);

        var 문서목록 = await _conversations
            .Find(BuildConversationFilter(query))
            .SortByDescending(x => x.수정시각Utc)
            .Limit(query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200))
            .ToListAsync(cancellationToken);

        return 문서목록.Select(ToDto).ToArray();
    }

    public async Task<커뮤니티메시지Dto> 메시지저장Async(
        커뮤니티메시지저장요청 request,
        string senderUserId,
        string senderDisplayName,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        Validate(request);

        var now = DateTime.UtcNow;
        var messageId = string.IsNullOrWhiteSpace(request.MessageId)
            ? Guid.NewGuid().ToString("N")
            : request.MessageId.Trim();
        var 대화방Id = request.대화방Id.Trim();
        var 커뮤니티Id = string.IsNullOrWhiteSpace(request.커뮤니티Id) ? "platform" : request.커뮤니티Id.Trim();
        var 보낸사람UserId = string.IsNullOrWhiteSpace(senderUserId) ? "anonymous" : senderUserId.Trim();
        var 보낸사람표시명 = string.IsNullOrWhiteSpace(senderDisplayName) ? "익명 참여자" : senderDisplayName.Trim();
        var 메시지종류 = string.IsNullOrWhiteSpace(request.메시지종류) ? 커뮤니티메시지종류.Text : request.메시지종류.Trim();

        var 문서 = new 커뮤니티메시지문서
        {
            MessageId = messageId,
            대화방Id = 대화방Id,
            커뮤니티Id = 커뮤니티Id,
            보낸사람UserId = 보낸사람UserId,
            보낸사람표시명 = 보낸사람표시명,
            메시지 = request.메시지.Trim(),
            메시지종류 = 메시지종류,
            원장Id = Clean(request.원장Id),
            다이어그램Id = Clean(request.다이어그램Id),
            다이어그램이름 = Clean(request.다이어그램이름),
            다이어그램스냅샷 = request.다이어그램스냅샷 is null ? null : ToDocument(request.다이어그램스냅샷),
            업무Context = request.업무Context is null ? null : ToDocument(request.업무Context),
            확장속성 = NormalizeDictionary(request.확장속성),
            생성시각Utc = now
        };

        await _messages.ReplaceOneAsync(
            x => x.대화방Id == 대화방Id && x.MessageId == messageId,
            문서,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        await TouchConversationAsync(
            request,
            문서,
            보낸사람UserId,
            보낸사람표시명,
            now,
            cancellationToken);

        return ToDto(문서);
    }

    public async Task<IReadOnlyList<커뮤니티메시지Dto>> 메시지목록조회Async(
        커뮤니티메시지조회조건 query,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(query.대화방Id))
        {
            throw new InvalidOperationException("대화방Id is required.");
        }

        var 문서목록 = await _messages
            .Find(BuildMessageFilter(query))
            .SortBy(x => x.생성시각Utc)
            .Limit(query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500))
            .ToListAsync(cancellationToken);

        return 문서목록.Select(ToDto).ToArray();
    }

    public async Task<커뮤니티대화방Dto?> 읽음표시Async(
        커뮤니티메시지읽음표시요청 request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.대화방Id))
        {
            throw new InvalidOperationException("대화방Id is required.");
        }

        var 문서 = await _conversations
            .Find(x => x.대화방Id == request.대화방Id.Trim())
            .FirstOrDefaultAsync(cancellationToken);
        if (문서 is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var 참여자목록 = 문서.참여자목록.ToList();
        var 참여자 = 참여자목록.FirstOrDefault(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (참여자 is null)
        {
            참여자목록.Add(new 커뮤니티대화방참여자문서
            {
                UserId = string.IsNullOrWhiteSpace(userId) ? "anonymous" : userId.Trim(),
                DisplayName = "익명 참여자",
                RoleLabel = "참여자",
                ParticipationState = "참여중",
                마지막읽은MessageId = Clean(request.MessageId),
                마지막읽은시각Utc = now
            });
        }
        else
        {
            참여자.마지막읽은MessageId = Clean(request.MessageId);
            참여자.마지막읽은시각Utc = now;
        }

        문서.참여자목록 = 참여자목록;
        문서.수정시각Utc = now;
        문서.수정자 = string.IsNullOrWhiteSpace(userId) ? "anonymous" : userId.Trim();

        await _conversations.ReplaceOneAsync(
            x => x.대화방Id == 문서.대화방Id,
            문서,
            cancellationToken: cancellationToken);

        return ToDto(문서);
    }

    private async Task TouchConversationAsync(
        커뮤니티메시지저장요청 request,
        커뮤니티메시지문서 message,
        string senderUserId,
        string senderDisplayName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var 제목 = string.IsNullOrWhiteSpace(request.제목)
            ? BuildFallbackTitle(request)
            : request.제목.Trim();

        var 참여자 = new[]
        {
            new 커뮤니티대화방참여자문서
            {
                UserId = senderUserId,
                DisplayName = senderDisplayName,
                RoleLabel = "참여자",
                ParticipationState = "참여중"
            }
        };

        var update = Builders<커뮤니티대화방문서>.Update
            .SetOnInsert(x => x.대화방Id, message.대화방Id)
            .SetOnInsert(x => x.커뮤니티Id, message.커뮤니티Id)
            .SetOnInsert(x => x.유형, string.IsNullOrWhiteSpace(request.유형) ? 커뮤니티대화방유형.Group : request.유형.Trim())
            .SetOnInsert(x => x.제목, 제목)
            .SetOnInsert(x => x.원장Id, Clean(request.원장Id))
            .SetOnInsert(x => x.원장템플릿Key, Clean(request.원장템플릿Key))
            .SetOnInsert(x => x.다이어그램Id, Clean(request.다이어그램Id))
            .SetOnInsert(x => x.다이어그램이름, Clean(request.다이어그램이름))
            .SetOnInsert(x => x.업무Context, request.업무Context is null ? null : ToDocument(request.업무Context))
            .SetOnInsert(x => x.참여자목록, 참여자)
            .SetOnInsert(x => x.확장속성, NormalizeDictionary(request.확장속성))
            .SetOnInsert(x => x.생성시각Utc, now)
            .Set(x => x.마지막메시지Id, message.MessageId)
            .Set(x => x.마지막메시지요약, BuildSummary(message.메시지))
            .Set(x => x.마지막메시지종류, message.메시지종류)
            .Set(x => x.마지막메시지시각Utc, message.생성시각Utc)
            .Set(x => x.수정시각Utc, now)
            .Set(x => x.수정자, senderUserId);

        await _conversations.UpdateOneAsync(
            x => x.대화방Id == message.대화방Id,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    private FilterDefinition<커뮤니티대화방문서> BuildConversationFilter(커뮤니티대화방조회조건 query)
    {
        var builder = Builders<커뮤니티대화방문서>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(query.커뮤니티Id))
        {
            filter &= builder.Eq(x => x.커뮤니티Id, query.커뮤니티Id.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.유형))
        {
            filter &= builder.Eq(x => x.유형, query.유형.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.원장Id))
        {
            filter &= builder.Eq(x => x.원장Id, query.원장Id.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.다이어그램Id))
        {
            filter &= builder.Eq(x => x.다이어그램Id, query.다이어그램Id.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.참여자UserId))
        {
            filter &= builder.ElemMatch(x => x.참여자목록, participant => participant.UserId == query.참여자UserId.Trim());
        }

        return filter;
    }

    private FilterDefinition<커뮤니티메시지문서> BuildMessageFilter(커뮤니티메시지조회조건 query)
    {
        var builder = Builders<커뮤니티메시지문서>.Filter;
        var filter = builder.Eq(x => x.대화방Id, query.대화방Id.Trim());

        if (query.AfterUtc is not null)
        {
            filter &= builder.Gt(x => x.생성시각Utc, query.AfterUtc.Value);
        }

        if (query.BeforeUtc is not null)
        {
            filter &= builder.Lt(x => x.생성시각Utc, query.BeforeUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.메시지종류))
        {
            filter &= builder.Eq(x => x.메시지종류, query.메시지종류.Trim());
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

            await _conversations.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<커뮤니티대화방문서>(
                        Builders<커뮤니티대화방문서>.IndexKeys.Ascending(x => x.대화방Id),
                        new CreateIndexOptions { Unique = true, Name = "ux_conversation_id" }),
                    new CreateIndexModel<커뮤니티대화방문서>(
                        Builders<커뮤니티대화방문서>.IndexKeys
                            .Ascending(x => x.커뮤니티Id)
                            .Ascending(x => x.유형)
                            .Descending(x => x.수정시각Utc),
                        new CreateIndexOptions { Name = "ix_conversation_scope_type" }),
                    new CreateIndexModel<커뮤니티대화방문서>(
                        Builders<커뮤니티대화방문서>.IndexKeys
                            .Ascending("참여자목록.UserId")
                            .Descending(x => x.수정시각Utc),
                        new CreateIndexOptions { Name = "ix_conversation_participant" })
                ],
                cancellationToken);

            await _messages.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<커뮤니티메시지문서>(
                        Builders<커뮤니티메시지문서>.IndexKeys
                            .Ascending(x => x.대화방Id)
                            .Ascending(x => x.MessageId),
                        new CreateIndexOptions { Unique = true, Name = "ux_message_room_message" }),
                    new CreateIndexModel<커뮤니티메시지문서>(
                        Builders<커뮤니티메시지문서>.IndexKeys
                            .Ascending(x => x.대화방Id)
                            .Ascending(x => x.생성시각Utc),
                        new CreateIndexOptions { Name = "ix_message_room_time" }),
                    new CreateIndexModel<커뮤니티메시지문서>(
                        Builders<커뮤니티메시지문서>.IndexKeys
                            .Ascending(x => x.커뮤니티Id)
                            .Ascending(x => x.메시지종류)
                            .Descending(x => x.생성시각Utc),
                        new CreateIndexOptions { Name = "ix_message_scope_kind" })
                ],
                cancellationToken);

            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static void Validate(커뮤니티대화방저장요청 request)
    {
        if (string.IsNullOrWhiteSpace(request.커뮤니티Id)) throw new InvalidOperationException("커뮤니티Id is required.");
        if (string.IsNullOrWhiteSpace(request.제목)) throw new InvalidOperationException("제목 is required.");
    }

    private static void Validate(커뮤니티메시지저장요청 request)
    {
        if (string.IsNullOrWhiteSpace(request.대화방Id)) throw new InvalidOperationException("대화방Id is required.");
        if (string.IsNullOrWhiteSpace(request.메시지)) throw new InvalidOperationException("메시지 is required.");
    }

    private static 커뮤니티대화방참여자문서 ToDocument(커뮤니티대화방참여자Dto dto)
        => new()
        {
            UserId = Clean(dto.UserId),
            DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? "익명 참여자" : dto.DisplayName.Trim(),
            RoleLabel = string.IsNullOrWhiteSpace(dto.RoleLabel) ? "참여자" : dto.RoleLabel.Trim(),
            ParticipationState = string.IsNullOrWhiteSpace(dto.ParticipationState) ? "참여중" : dto.ParticipationState.Trim(),
            마지막읽은MessageId = Clean(dto.마지막읽은MessageId),
            마지막읽은시각Utc = dto.마지막읽은시각Utc
        };

    private static 커뮤니티다이어그램스냅샷문서 ToDocument(DiagramSnapshotDto dto)
        => new()
        {
            DiagramId = dto.DiagramId,
            DiagramName = dto.DiagramName,
            LedgerId = dto.LedgerId,
            LedgerTemplateKey = dto.LedgerTemplateKey,
            WorkflowModeKey = dto.WorkflowModeKey,
            Nodes = dto.Nodes.Select(node => new 커뮤니티다이어그램노드문서
            {
                NodeId = node.NodeId,
                Kind = node.Kind,
                Title = node.Title,
                GroupLabel = node.GroupLabel,
                Description = node.Description,
                X = node.X,
                Y = node.Y,
                RelatedRoute = node.RelatedRoute,
                OrganizationReferences = (node.OrganizationReferences ?? [])
                    .Select(ToDocument)
                    .ToArray(),
                Data = NormalizeDictionary(node.Data)
            }).ToArray(),
            Edges = dto.Edges.Select(edge => new 커뮤니티다이어그램연결선문서
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Label = edge.Label,
                MeaningCode = edge.MeaningCode,
                Data = NormalizeDictionary(edge.Data)
            }).ToArray(),
            Metadata = NormalizeDictionary(dto.Metadata)
        };

    private static 커뮤니티업무Context문서 ToDocument(DiagramWorkContextDto dto)
        => new()
        {
            WorkType = dto.WorkType,
            WorkLabel = dto.WorkLabel,
            AppKey = dto.AppKey,
            PrimaryRoute = dto.PrimaryRoute,
            PrimaryActionLabel = dto.PrimaryActionLabel,
            Parameters = NormalizeDictionary(dto.Parameters)
        };

    private static 커뮤니티대화방Dto ToDto(커뮤니티대화방문서 문서)
        => new()
        {
            대화방Id = 문서.대화방Id,
            커뮤니티Id = 문서.커뮤니티Id,
            유형 = 문서.유형,
            제목 = 문서.제목,
            원장Id = 문서.원장Id,
            원장템플릿Key = 문서.원장템플릿Key,
            다이어그램Id = 문서.다이어그램Id,
            다이어그램이름 = 문서.다이어그램이름,
            업무Context = 문서.업무Context is null ? null : ToDto(문서.업무Context),
            참여자목록 = 문서.참여자목록.Select(ToDto).ToArray(),
            마지막메시지Id = 문서.마지막메시지Id,
            마지막메시지요약 = 문서.마지막메시지요약,
            마지막메시지종류 = 문서.마지막메시지종류,
            마지막메시지시각Utc = 문서.마지막메시지시각Utc,
            확장속성 = 문서.확장속성,
            생성시각Utc = 문서.생성시각Utc,
            수정시각Utc = 문서.수정시각Utc
        };

    private static 커뮤니티메시지Dto ToDto(커뮤니티메시지문서 문서)
        => new()
        {
            MessageId = 문서.MessageId,
            대화방Id = 문서.대화방Id,
            커뮤니티Id = 문서.커뮤니티Id,
            보낸사람UserId = 문서.보낸사람UserId,
            보낸사람표시명 = 문서.보낸사람표시명,
            메시지 = 문서.메시지,
            메시지종류 = 문서.메시지종류,
            원장Id = 문서.원장Id,
            다이어그램Id = 문서.다이어그램Id,
            다이어그램이름 = 문서.다이어그램이름,
            다이어그램스냅샷 = 문서.다이어그램스냅샷 is null ? null : ToDto(문서.다이어그램스냅샷),
            업무Context = 문서.업무Context is null ? null : ToDto(문서.업무Context),
            확장속성 = 문서.확장속성,
            생성시각Utc = 문서.생성시각Utc
        };

    private static 커뮤니티대화방참여자Dto ToDto(커뮤니티대화방참여자문서 문서)
        => new()
        {
            UserId = 문서.UserId,
            DisplayName = 문서.DisplayName,
            RoleLabel = 문서.RoleLabel,
            ParticipationState = 문서.ParticipationState,
            마지막읽은MessageId = 문서.마지막읽은MessageId,
            마지막읽은시각Utc = 문서.마지막읽은시각Utc
        };

    private static DiagramSnapshotDto ToDto(커뮤니티다이어그램스냅샷문서 문서)
        => new()
        {
            DiagramId = 문서.DiagramId,
            DiagramName = 문서.DiagramName,
            LedgerId = 문서.LedgerId,
            LedgerTemplateKey = 문서.LedgerTemplateKey,
            WorkflowModeKey = 문서.WorkflowModeKey,
            Nodes = 문서.Nodes.Select(node => new DiagramNodeDto
            {
                NodeId = node.NodeId,
                Kind = node.Kind,
                Title = node.Title,
                GroupLabel = node.GroupLabel,
                Description = node.Description,
                X = node.X,
                Y = node.Y,
                RelatedRoute = node.RelatedRoute,
                OrganizationReferences = (node.OrganizationReferences ?? [])
                    .Select(ToDto)
                    .ToArray(),
                Data = node.Data
            }).ToArray(),
            Edges = 문서.Edges.Select(edge => new DiagramEdgeDto
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Label = edge.Label,
                MeaningCode = edge.MeaningCode,
                Data = edge.Data
            }).ToArray(),
            Metadata = 문서.Metadata
        };

    private static 커뮤니티다이어그램업체참조문서 ToDocument(
        DiagramOrganizationReferenceDto dto)
        => new()
        {
            ReferenceId = dto.ReferenceId,
            OrganizationKey = dto.OrganizationKey,
            DisplayName = dto.DisplayName,
            RoleLabel = dto.RoleLabel,
            CountryCode = dto.CountryCode,
            OfficialWebsiteUrl = dto.OfficialWebsiteUrl,
            SourceKindCode = dto.SourceKindCode,
            SourceReferenceUrl = dto.SourceReferenceUrl,
            DirectoryStatusCode = dto.DirectoryStatusCode,
            PlatformRelationshipStatusCode = dto.PlatformRelationshipStatusCode,
            CompanySourceVerificationStatusCode = dto.CompanySourceVerificationStatusCode,
            RegulatoryVerificationStatusCode = dto.RegulatoryVerificationStatusCode,
            IsPlatformPartner = dto.IsPlatformPartner,
            CanBeSelectedForOperations = dto.CanBeSelectedForOperations,
            CapabilityCodes = (dto.CapabilityCodes ?? []).ToArray()
        };

    private static DiagramOrganizationReferenceDto ToDto(
        커뮤니티다이어그램업체참조문서 문서)
        => new()
        {
            ReferenceId = 문서.ReferenceId,
            OrganizationKey = 문서.OrganizationKey,
            DisplayName = 문서.DisplayName,
            RoleLabel = 문서.RoleLabel,
            CountryCode = 문서.CountryCode,
            OfficialWebsiteUrl = 문서.OfficialWebsiteUrl,
            SourceKindCode = 문서.SourceKindCode,
            SourceReferenceUrl = 문서.SourceReferenceUrl,
            DirectoryStatusCode = 문서.DirectoryStatusCode,
            PlatformRelationshipStatusCode = 문서.PlatformRelationshipStatusCode,
            CompanySourceVerificationStatusCode = 문서.CompanySourceVerificationStatusCode,
            RegulatoryVerificationStatusCode = 문서.RegulatoryVerificationStatusCode,
            IsPlatformPartner = 문서.IsPlatformPartner,
            CanBeSelectedForOperations = 문서.CanBeSelectedForOperations,
            CapabilityCodes = (문서.CapabilityCodes ?? []).ToArray()
        };

    private static DiagramWorkContextDto ToDto(커뮤니티업무Context문서 문서)
        => new()
        {
            WorkType = 문서.WorkType,
            WorkLabel = 문서.WorkLabel,
            AppKey = 문서.AppKey,
            PrimaryRoute = 문서.PrimaryRoute,
            PrimaryActionLabel = 문서.PrimaryActionLabel,
            Parameters = 문서.Parameters
        };

    private static string BuildFallbackTitle(커뮤니티메시지저장요청 request)
    {
        if (!string.IsNullOrWhiteSpace(request.다이어그램이름))
        {
            return request.다이어그램이름.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.원장Id))
        {
            return $"원장 {request.원장Id.Trim()} 대화";
        }

        return "커뮤니티 대화";
    }

    private static string BuildSummary(string message)
    {
        var value = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        return value.Length <= 80 ? value : value[..80];
    }

    private static Dictionary<string, string> NormalizeDictionary(IReadOnlyDictionary<string, string>? source)
        => source?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase)
           ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class 커뮤니티대화방조회조건
{
    public string? 커뮤니티Id { get; set; }
    public string? 유형 { get; set; }
    public string? 원장Id { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 참여자UserId { get; set; }
    public int Limit { get; set; } = 50;
}

public sealed class 커뮤니티메시지조회조건
{
    public string 대화방Id { get; set; } = string.Empty;
    public string? 메시지종류 { get; set; }
    public DateTime? AfterUtc { get; set; }
    public DateTime? BeforeUtc { get; set; }
    public int Limit { get; set; } = 100;
}

public sealed class 커뮤니티대화방저장요청
{
    public string? 대화방Id { get; set; }
    public string 커뮤니티Id { get; set; } = "platform";
    public string 유형 { get; set; } = 커뮤니티대화방유형.Group;
    public string 제목 { get; set; } = string.Empty;
    public string? 원장Id { get; set; }
    public string? 원장템플릿Key { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public DiagramWorkContextDto? 업무Context { get; set; }
    public IReadOnlyList<커뮤니티대화방참여자Dto> 참여자목록 { get; set; } = [];
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
}

public sealed class 커뮤니티메시지저장요청
{
    public string? MessageId { get; set; }
    public string 대화방Id { get; set; } = string.Empty;
    public string 커뮤니티Id { get; set; } = "platform";
    public string 유형 { get; set; } = 커뮤니티대화방유형.Group;
    public string? 제목 { get; set; }
    public string 메시지 { get; set; } = string.Empty;
    public string 메시지종류 { get; set; } = 커뮤니티메시지종류.Text;
    public string? 원장Id { get; set; }
    public string? 원장템플릿Key { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public DiagramSnapshotDto? 다이어그램스냅샷 { get; set; }
    public DiagramWorkContextDto? 업무Context { get; set; }
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
}

public sealed class 커뮤니티메시지읽음표시요청
{
    public string 대화방Id { get; set; } = string.Empty;
    public string? MessageId { get; set; }
}

public sealed class 커뮤니티대화방Dto
{
    public string 대화방Id { get; set; } = string.Empty;
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 유형 { get; set; } = 커뮤니티대화방유형.Group;
    public string 제목 { get; set; } = string.Empty;
    public string? 원장Id { get; set; }
    public string? 원장템플릿Key { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public DiagramWorkContextDto? 업무Context { get; set; }
    public IReadOnlyList<커뮤니티대화방참여자Dto> 참여자목록 { get; set; } = [];
    public string? 마지막메시지Id { get; set; }
    public string? 마지막메시지요약 { get; set; }
    public string? 마지막메시지종류 { get; set; }
    public DateTime? 마지막메시지시각Utc { get; set; }
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 커뮤니티대화방참여자Dto
{
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = "익명 참여자";
    public string RoleLabel { get; set; } = "참여자";
    public string ParticipationState { get; set; } = "참여중";
    public string? 마지막읽은MessageId { get; set; }
    public DateTime? 마지막읽은시각Utc { get; set; }
}

public sealed class 커뮤니티메시지Dto
{
    public string MessageId { get; set; } = string.Empty;
    public string 대화방Id { get; set; } = string.Empty;
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 보낸사람UserId { get; set; } = string.Empty;
    public string 보낸사람표시명 { get; set; } = "익명 참여자";
    public string 메시지 { get; set; } = string.Empty;
    public string 메시지종류 { get; set; } = 커뮤니티메시지종류.Text;
    public string? 원장Id { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public DiagramSnapshotDto? 다이어그램스냅샷 { get; set; }
    public DiagramWorkContextDto? 업무Context { get; set; }
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
    public DateTime 생성시각Utc { get; set; }
}

public static class 커뮤니티대화방유형
{
    public const string Direct = "Direct";
    public const string Group = "Group";
    public const string Diagram = "Diagram";
    public const string Ledger = "Ledger";
}

public static class 커뮤니티메시지종류
{
    public const string Text = "Text";
    public const string Image = "Image";
    public const string Diagram = "Diagram";
    public const string Ledger = "Ledger";
    public const string WorkAction = "WorkAction";
    public const string System = "System";
}

public sealed class 커뮤니티대화방문서
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string 대화방Id { get; set; } = string.Empty;
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 유형 { get; set; } = 커뮤니티대화방유형.Group;
    public string 제목 { get; set; } = string.Empty;
    public string? 원장Id { get; set; }
    public string? 원장템플릿Key { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public 커뮤니티업무Context문서? 업무Context { get; set; }
    public IReadOnlyList<커뮤니티대화방참여자문서> 참여자목록 { get; set; } = [];
    public string? 마지막메시지Id { get; set; }
    public string? 마지막메시지요약 { get; set; }
    public string? 마지막메시지종류 { get; set; }
    public DateTime? 마지막메시지시각Utc { get; set; }
    public Dictionary<string, string> 확장속성 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
    public string 수정자 { get; set; } = "system";
}

public sealed class 커뮤니티대화방참여자문서
{
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = "익명 참여자";
    public string RoleLabel { get; set; } = "참여자";
    public string ParticipationState { get; set; } = "참여중";
    public string? 마지막읽은MessageId { get; set; }
    public DateTime? 마지막읽은시각Utc { get; set; }
}

public sealed class 커뮤니티메시지문서
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string MessageId { get; set; } = string.Empty;
    public string 대화방Id { get; set; } = string.Empty;
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 보낸사람UserId { get; set; } = string.Empty;
    public string 보낸사람표시명 { get; set; } = "익명 참여자";
    public string 메시지 { get; set; } = string.Empty;
    public string 메시지종류 { get; set; } = 커뮤니티메시지종류.Text;
    public string? 원장Id { get; set; }
    public string? 다이어그램Id { get; set; }
    public string? 다이어그램이름 { get; set; }
    public 커뮤니티다이어그램스냅샷문서? 다이어그램스냅샷 { get; set; }
    public 커뮤니티업무Context문서? 업무Context { get; set; }
    public Dictionary<string, string> 확장속성 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime 생성시각Utc { get; set; }
}

public sealed class 커뮤니티다이어그램스냅샷문서
{
    public string DiagramId { get; set; } = string.Empty;
    public string DiagramName { get; set; } = string.Empty;
    public string? LedgerId { get; set; }
    public string? LedgerTemplateKey { get; set; }
    public string? WorkflowModeKey { get; set; }
    public IReadOnlyList<커뮤니티다이어그램노드문서> Nodes { get; set; } = [];
    public IReadOnlyList<커뮤니티다이어그램연결선문서> Edges { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class 커뮤니티다이어그램노드문서
{
    public string NodeId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? GroupLabel { get; set; }
    public string? Description { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string? RelatedRoute { get; set; }
    public IReadOnlyList<커뮤니티다이어그램업체참조문서> OrganizationReferences { get; set; } = [];
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class 커뮤니티다이어그램업체참조문서
{
    public string ReferenceId { get; set; } = string.Empty;
    public string OrganizationKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "ZZ";
    public string OfficialWebsiteUrl { get; set; } = string.Empty;
    public string SourceKindCode { get; set; } = DiagramOrganizationSourceKindCodes.ManualResearch;
    public string SourceReferenceUrl { get; set; } = string.Empty;
    public string DirectoryStatusCode { get; set; } = string.Empty;
    public string PlatformRelationshipStatusCode { get; set; } = string.Empty;
    public string CompanySourceVerificationStatusCode { get; set; } =
        DiagramOrganizationVerificationStatusCodes.VerificationRequired;
    public string RegulatoryVerificationStatusCode { get; set; } = string.Empty;
    public bool IsPlatformPartner { get; set; }
    public bool CanBeSelectedForOperations { get; set; }
    public IReadOnlyList<string> CapabilityCodes { get; set; } = [];
}

public sealed class 커뮤니티다이어그램연결선문서
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? MeaningCode { get; set; }
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class 커뮤니티업무Context문서
{
    public string WorkType { get; set; } = string.Empty;
    public string WorkLabel { get; set; } = string.Empty;
    public string? AppKey { get; set; }
    public string? PrimaryRoute { get; set; }
    public string? PrimaryActionLabel { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
