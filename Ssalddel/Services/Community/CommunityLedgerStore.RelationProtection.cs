using MongoDB.Driver;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Services.Community;

public sealed partial class Mongo커뮤니티원장저장소
{
    private async Task SaveWithRelationProtectionAsync(
        커뮤니티원장문서? existing,
        커뮤니티원장문서 document,
        CancellationToken cancellationToken)
    {
        var affected = 커뮤니티원장관계보호Policy.삭제성변경대상(existing, document);
        if (affected.Count == 0)
        {
            var result = await _collection.ReplaceOneAsync(
                BuildRevisionFilter(document.원장Id, existing?.Revision ?? 0, existing is null,
                    existing?.관계편집Revision ?? 0),
                document,
                new ReplaceOptions { IsUpsert = existing is null },
                cancellationToken);
            if (existing is not null && result.MatchedCount == 0)
                throw CreateConcurrencyException(document.원장Id);
            return;
        }

        // 읽기만 하는 transaction은 write skew를 막지 못합니다. 자식 문서를 실제로
        // 쓰고 일반 상태 진행의 복합 CAS에도 같은 fence를 포함합니다.
        using var session = await StartRelationSessionAsync(cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(
                readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
            var currentRoot = await _collection.Find(session, x => x.원장Id == document.원장Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (currentRoot is null || currentRoot.Revision != existing!.Revision
                || currentRoot.관계편집Revision != existing.관계편집Revision)
                throw CreateConcurrencyException(document.원장Id);
            if (currentRoot.확장속성.ContainsKey(배송원장개인정보파기Service.Marker))
                throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");

            foreach (var reference in affected.OrderBy(x => x.원장Id, StringComparer.Ordinal))
            {
                var child = await _collection.Find(session, x => x.원장Id == reference.원장Id)
                    .FirstOrDefaultAsync(cancellationToken);
                커뮤니티원장관계보호Policy.잠금검증(currentRoot, child);
                // Mongo의 원장 ID는 정확히 대응합니다. 대소문자만 다른 ID도 기존
                // 문서의 별칭이 아니며, 초안 분리 후 연결할 별도 문서가 있어야 합니다.
                var caseChangedTarget = document.포함원장목록.FirstOrDefault(x =>
                    string.Equals(x.원장Id, reference.원장Id, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.원장Id, reference.원장Id, StringComparison.Ordinal));
                if (caseChangedTarget is not null)
                {
                    var target = await _collection.Find(session, x => x.원장Id == caseChangedTarget.원장Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (target is null)
                        throw new InvalidOperationException("OrderLedgerChildIdentityUnverified: 변경할 연결 원장의 정확한 ID를 확인할 수 없습니다.");
                }
                var result = await _collection.UpdateOneAsync(session,
                    BuildRevisionFilter(child!.원장Id, child.Revision, isNew: false, child.관계편집Revision),
                    Builders<커뮤니티원장문서>.Update.Inc(x => x.관계편집Revision, 1),
                    cancellationToken: cancellationToken);
                if (result.MatchedCount != 1)
                    throw CreateConcurrencyException(child.원장Id);
            }

            var rootResult = await _collection.ReplaceOneAsync(session,
                BuildRevisionFilter(currentRoot.원장Id, currentRoot.Revision, isNew: false,
                    currentRoot.관계편집Revision), document, cancellationToken: cancellationToken);
            if (rootResult.MatchedCount != 1)
                throw CreateConcurrencyException(document.원장Id);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            // 적용 여부를 모르는 commit은 성공이나 단순 미적용으로 바꾸지 않습니다.
            throw new InvalidOperationException(
                "OrderLedgerRelationCommitUnknown: 연결 변경의 적용 여부를 확인하지 못했습니다. 최신 원장을 다시 조회해야 합니다.", exception);
        }
        catch (MongoException exception) when (TransactionUnavailable(exception))
        {
            await AbortRelationTransactionAsync(session, cancellationToken);
            throw CreateTransactionRequiredException(exception);
        }
        catch (NotSupportedException exception)
        {
            await AbortRelationTransactionAsync(session, cancellationToken);
            throw CreateTransactionRequiredException(exception);
        }
        catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError")
                                              || exception is MongoCommandException { Code: 112 or 251 }
                                              || exception is MongoWriteException { WriteError.Code: 112 })
        {
            await AbortRelationTransactionAsync(session, cancellationToken);
            throw CreateConcurrencyException(document.원장Id, exception);
        }
        catch
        {
            await AbortRelationTransactionAsync(session, cancellationToken);
            throw;
        }
    }

    private static bool TransactionUnavailable(MongoException exception)
        => exception is MongoCommandException { Code: 20 or 263 or 303 };

    private async Task<IClientSessionHandle> StartRelationSessionAsync(CancellationToken cancellationToken)
    {
        try { return await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken); }
        catch (NotSupportedException exception) { throw CreateTransactionRequiredException(exception); }
        catch (MongoException exception) when (TransactionUnavailable(exception))
        { throw CreateTransactionRequiredException(exception); }
    }

    private static InvalidOperationException CreateTransactionRequiredException(Exception exception)
        => new("OrderLedgerRelationTransactionRequired: 이 저장 환경은 원장 연결의 안전한 분리·역할 변경·필수 해제를 지원하지 않습니다.", exception);

    private static async Task AbortRelationTransactionAsync(IClientSessionHandle session, CancellationToken cancellationToken)
    {
        if (!session.IsInTransaction) return;
        try { await session.AbortTransactionAsync(cancellationToken); }
        catch (MongoException) { /* 원래 실패를 보존합니다. */ }
        catch (OperationCanceledException) { /* 원래 취소·실패를 보존합니다. */ }
    }
}

internal static class 커뮤니티원장관계보호Policy
{
    internal static IReadOnlyList<커뮤니티포함원장참조문서> 삭제성변경대상(
        커뮤니티원장문서? existing, 커뮤니티원장문서 next)
    {
        if (existing is null || !주문원장구성정책.주문루트인가(existing.원장템플릿Key)
            || existing.포함원장목록.Count == 0) return [];
        var nextById = next.포함원장목록.ToDictionary(x => x.원장Id, StringComparer.Ordinal);
        return existing.포함원장목록.Where(previous =>
            !string.Equals(existing.원장템플릿Key, next.원장템플릿Key, StringComparison.OrdinalIgnoreCase)
            || !nextById.TryGetValue(previous.원장Id, out var current)
            || !string.Equals(previous.원장템플릿Key, current.원장템플릿Key, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previous.역할, current.역할, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previous.관계유형, current.관계유형, StringComparison.OrdinalIgnoreCase)
            || previous.필수여부 && !current.필수여부).ToArray();
    }

    internal static void 잠금검증(커뮤니티원장문서 root, 커뮤니티원장문서? child)
    {
        if (child?.확장속성.ContainsKey(배송원장개인정보파기Service.Marker) == true)
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");
        var reason = 주문원장연결변경Policy.잠금사유(
            커뮤니티원장문서읽기Mapper.ToDto(root), child is null ? null : 커뮤니티원장문서읽기Mapper.ToDto(child));
        if (reason is not null) throw new InvalidOperationException(reason);
    }
}
