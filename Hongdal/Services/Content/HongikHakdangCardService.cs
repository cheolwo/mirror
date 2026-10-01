using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Hongdal.Services.External.HongikHakdang;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IHongikHakdangCardService
{
    Task<IReadOnlyList<HongikHakdangCardCollectionDto>> GetCollectionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardSyncResultDto> SyncAsync(CancellationToken cancellationToken);

    Task<HongikHakdangCardActivationUpdateResponse?> SetCollectionEnabledAsync(
        long collectionId,
        bool enabled,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardActivationUpdateResponse?> SetCardEnabledAsync(
        long cardId,
        bool enabled,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardCommunityPublicationUpdateResponse?> SetCardCommunityPublicationApprovedAsync(
        long cardId,
        bool approved,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardImageContentResult?> GetCardImageAsync(
        long cardId,
        CancellationToken cancellationToken);
}

public sealed record HongikHakdangCardImageContentResult(byte[] Bytes, string ContentType);

public sealed class HongikHakdangCardService : IHongikHakdangCardService
{
    private readonly IHongikHakdangCardSourceClient _sourceClient;
    private readonly IHongikHakdangCardPageParser _parser;
    private readonly IHongikHakdangCardImageStore _imageStore;
    private readonly IHongikHakdangCardRepository _repository;
    private readonly HongikHakdangCardOptions _options;

    public HongikHakdangCardService(
        IHongikHakdangCardSourceClient sourceClient,
        IHongikHakdangCardPageParser parser,
        IHongikHakdangCardImageStore imageStore,
        IHongikHakdangCardRepository repository,
        IOptions<HongikHakdangCardOptions> options)
    {
        _sourceClient = sourceClient;
        _parser = parser;
        _imageStore = imageStore;
        _repository = repository;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<HongikHakdangCardCollectionDto>> GetCollectionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
        => (await _repository.GetCollectionsAsync(includeInactive, cancellationToken))
            .Select(ToCollectionDto)
            .ToArray();

    public async Task<HongikHakdangCardSyncResultDto> SyncAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new HongikHakdangCardSyncResultDto(
                false, 0, 0, 0, 0, 0, 0, 0, null,
                "홍익학당 카드 수집이 비활성화되어 있습니다.");
        }

        var html = await _sourceClient.GetCardPageHtmlAsync(cancellationToken);
        var parsedCollections = _parser.Parse(html);
        var now = DateTime.UtcNow;
        var collections = await _repository.GetCollectionsTrackedAsync(cancellationToken);
        var cards = await _repository.GetCardsTrackedAsync(cancellationToken);
        var collectionByKey = collections.ToDictionary(x => x.SourceKey, StringComparer.Ordinal);
        var cardByKey = cards.ToDictionary(x => x.SourceKey, StringComparer.Ordinal);

        foreach (var collection in collections)
        {
            collection.IsActive = false;
            foreach (var item in collection.Items)
            {
                item.IsActive = false;
            }
        }

        foreach (var card in cards)
        {
            card.IsActive = false;
        }

        var addedCardCount = 0;
        var updatedCardCount = 0;
        var seenCardKeys = new HashSet<string>(StringComparer.Ordinal);
        var activeCards = new List<HongikHakdangCard>();

        foreach (var parsedCollection in parsedCollections)
        {
            if (!collectionByKey.TryGetValue(parsedCollection.SourceKey, out var collection))
            {
                collection = new HongikHakdangCardCollection
                {
                    SourceKey = parsedCollection.SourceKey,
                    CreatedAtUtc = now
                };
                collectionByKey.Add(collection.SourceKey, collection);
                collections.Add(collection);
                _repository.AddCollection(collection);
            }

            collection.Name = parsedCollection.Name;
            collection.SortOrder = parsedCollection.SortOrder;
            collection.IsActive = true;
            collection.LastSeenAtUtc = now;
            collection.UpdatedAtUtc = now;

            var membershipByCardKey = collection.Items
                .Where(x => x.Card is not null)
                .GroupBy(x => x.Card.SourceKey, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            foreach (var parsedCard in parsedCollection.Cards)
            {
                if (!cardByKey.TryGetValue(parsedCard.SourceKey, out var card))
                {
                    card = new HongikHakdangCard
                    {
                        SourceKey = parsedCard.SourceKey,
                        CreatedAtUtc = now,
                        ImageDownloadStatus = HongikHakdangCard.ImagePendingStatus
                    };
                    cardByKey.Add(card.SourceKey, card);
                    cards.Add(card);
                    _repository.AddCard(card);
                    addedCardCount++;
                }
                else if (seenCardKeys.Add(parsedCard.SourceKey))
                {
                    updatedCardCount++;
                }

                if (!seenCardKeys.Contains(parsedCard.SourceKey))
                {
                    seenCardKeys.Add(parsedCard.SourceKey);
                }

                card.Title = PreferNonEmpty(parsedCard.Title, card.Title);
                card.Description = PreferNonEmpty(parsedCard.Description, card.Description);
                card.OriginalImageUrl = parsedCard.OriginalImageUrl;
                card.ThumbnailImageUrl = PreferNonEmpty(parsedCard.ThumbnailImageUrl, card.ThumbnailImageUrl);
                card.RelatedUrl = PreferNonEmpty(parsedCard.RelatedUrl, card.RelatedUrl);
                card.IsActive = true;
                card.LastSeenAtUtc = now;
                card.UpdatedAtUtc = now;
                if (!activeCards.Contains(card))
                {
                    activeCards.Add(card);
                }

                if (!membershipByCardKey.TryGetValue(parsedCard.SourceKey, out var membership))
                {
                    membership = new HongikHakdangCardCollectionItem
                    {
                        Collection = collection,
                        Card = card
                    };
                    membershipByCardKey.Add(parsedCard.SourceKey, membership);
                    collection.Items.Add(membership);
                    card.Collections.Add(membership);
                    _repository.AddCollectionItem(membership);
                }

                membership.SortOrder = parsedCard.SortOrder;
                membership.IsActive = true;
                membership.LastSeenAtUtc = now;
            }
        }

        await _repository.SaveAsync(cancellationToken);

        var downloadedImageCount = 0;
        var failedImageCount = 0;
        if (_options.DownloadImages)
        {
            var imageResults = await DownloadMissingImagesAsync(activeCards, cancellationToken);
            foreach (var result in imageResults)
            {
                if (result.StoredImage is not null)
                {
                    result.Card.LocalImagePath = result.StoredImage.RelativePath;
                    result.Card.ImageContentType = result.StoredImage.ContentType;
                    result.Card.ImageSizeBytes = result.StoredImage.SizeBytes;
                    result.Card.ImageSha256 = result.StoredImage.Sha256;
                    result.Card.ImageDownloadStatus = HongikHakdangCard.ImageDownloadedStatus;
                    result.Card.ImageDownloadError = null;
                    result.Card.ImageDownloadedAtUtc = now;
                    downloadedImageCount++;
                }
                else if (result.Error is not null)
                {
                    result.Card.ImageDownloadStatus = HongikHakdangCard.ImageFailedStatus;
                    result.Card.ImageDownloadError = Limit(result.Error, 1000);
                    failedImageCount++;
                }
            }

            if (imageResults.Count > 0)
            {
                await _repository.SaveAsync(cancellationToken);
            }
        }

        var occurrenceCount = parsedCollections.Sum(x => x.Cards.Count);
        return new HongikHakdangCardSyncResultDto(
            true,
            parsedCollections.Count,
            occurrenceCount,
            seenCardKeys.Count,
            addedCardCount,
            updatedCardCount,
            downloadedImageCount,
            failedImageCount,
            now,
            failedImageCount > 0
                ? $"홍익학당 카드 {seenCardKeys.Count}건을 수집했고 이미지 {failedImageCount}건은 다음 동기화에서 재시도합니다."
                : $"홍익학당 카드 {seenCardKeys.Count}건과 컬렉션 {parsedCollections.Count}건을 수집했습니다.");
    }

    public async Task<HongikHakdangCardActivationUpdateResponse?> SetCollectionEnabledAsync(
        long collectionId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var collection = await _repository.FindCollectionTrackedAsync(collectionId, cancellationToken);
        if (collection is null)
        {
            return null;
        }

        collection.IsAdminEnabled = enabled;
        collection.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveAsync(cancellationToken);
        return new HongikHakdangCardActivationUpdateResponse(
            collection.Id,
            enabled,
            enabled ? "카드 묶음을 관리자 검토 대상으로 켰습니다." : "카드 묶음을 관리자 검토 대상에서 껐습니다.");
    }

    public async Task<HongikHakdangCardActivationUpdateResponse?> SetCardEnabledAsync(
        long cardId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var card = await _repository.FindCardTrackedAsync(cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        card.IsAdminEnabled = enabled;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveAsync(cancellationToken);
        return new HongikHakdangCardActivationUpdateResponse(
            card.Id,
            enabled,
            enabled ? "카드를 관리자 검토 대상으로 켰습니다." : "카드를 관리자 검토 대상에서 껐습니다.");
    }

    public async Task<HongikHakdangCardCommunityPublicationUpdateResponse?> SetCardCommunityPublicationApprovedAsync(
        long cardId,
        bool approved,
        CancellationToken cancellationToken)
    {
        var card = await _repository.FindCardTrackedAsync(cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        card.IsCommunityPublicationApproved = approved;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveAsync(cancellationToken);
        return new HongikHakdangCardCommunityPublicationUpdateResponse(
            card.Id,
            approved,
            approved
                ? "카드를 반야 게시 후보로 승인했습니다. 배치가 활성화되면 순서대로 게시합니다."
                : "카드의 반야 게시 승인을 해제했습니다.");
    }

    public async Task<HongikHakdangCardImageContentResult?> GetCardImageAsync(
        long cardId,
        CancellationToken cancellationToken)
    {
        var card = await _repository.FindCardTrackedAsync(cardId, cancellationToken);
        if (card is null
            || string.IsNullOrWhiteSpace(card.LocalImagePath)
            || !_imageStore.Exists(card.LocalImagePath))
        {
            return null;
        }

        return new HongikHakdangCardImageContentResult(
            await _imageStore.ReadAsync(card.LocalImagePath, cancellationToken),
            string.IsNullOrWhiteSpace(card.ImageContentType) ? "image/jpeg" : card.ImageContentType);
    }

    private async Task<IReadOnlyList<CardImageDownloadResult>> DownloadMissingImagesAsync(
        IReadOnlyList<HongikHakdangCard> cards,
        CancellationToken cancellationToken)
    {
        var candidates = cards
            .Where(x => !_imageStore.Exists(x.LocalImagePath))
            .DistinctBy(x => x.SourceKey, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        var gate = new SemaphoreSlim(Math.Clamp(_options.MaxConcurrentDownloads, 1, 12));
        try
        {
            var tasks = candidates.Select(async card =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var content = await _sourceClient.DownloadImageAsync(
                        card.OriginalImageUrl,
                        cancellationToken);
                    var stored = await _imageStore.SaveAsync(card.SourceKey, content, cancellationToken);
                    return new CardImageDownloadResult(card, stored, null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return new CardImageDownloadResult(card, null, ex.Message);
                }
                finally
                {
                    gate.Release();
                }
            });

            return await Task.WhenAll(tasks);
        }
        finally
        {
            gate.Dispose();
        }
    }

    private static HongikHakdangCardCollectionDto ToCollectionDto(
        HongikHakdangCardCollection collection)
        => new(
            collection.Id,
            collection.SourceKey,
            collection.Name,
            collection.SortOrder,
            collection.IsActive,
            collection.LastSeenAtUtc,
            collection.Items
                .Where(x => x.IsActive && x.Card is not null)
                .OrderBy(x => x.SortOrder)
                .Select(x => new HongikHakdangCardDto(
                    x.Card.Id,
                    x.Card.SourceKey,
                    x.Card.Title,
                    x.Card.Description,
                    x.Card.OriginalImageUrl,
                    x.Card.ThumbnailImageUrl,
                    x.Card.RelatedUrl,
                    x.Card.LocalImagePath,
                    x.Card.ImageDownloadStatus,
                    x.Card.ImageSizeBytes,
                    x.Card.ImageSha256,
                    x.Card.IsActive,
                    x.Card.LastSeenAtUtc,
                    x.SortOrder,
                    x.Card.IsAdminEnabled,
                    x.Card.IsCommunityPublicationApproved))
                .ToArray(),
            collection.IsAdminEnabled);

    private static string? PreferNonEmpty(string? candidate, string? existing)
        => string.IsNullOrWhiteSpace(candidate) ? existing : candidate;

    private static string Limit(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record CardImageDownloadResult(
        HongikHakdangCard Card,
        HongikHakdangStoredImage? StoredImage,
        string? Error);
}
