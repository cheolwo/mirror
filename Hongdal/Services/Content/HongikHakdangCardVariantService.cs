using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Content;

public interface IHongikHakdangCardVariantService
{
    Task<HongikHakdangCardVariantPreparationResultDto> EnsureActiveVariantsAsync(
        CancellationToken cancellationToken);

    Task<HongikHakdangCardVariantPreparationResultDto> EnsureCardVariantsAsync(
        long cardId,
        CancellationToken cancellationToken);
}

public sealed class HongikHakdangCardVariantService : IHongikHakdangCardVariantService
{
    private static readonly string[] RequiredVariantKinds =
    [
        HongikHakdangCardImageVariantKinds.Notification,
        HongikHakdangCardImageVariantKinds.LockScreenPortrait
    ];

    private readonly HongdalContext _db;
    private readonly IHongikHakdangCardImageStore _imageStore;
    private readonly IHongikHakdangCardVariantRenderer _renderer;
    private readonly ILogger<HongikHakdangCardVariantService> _logger;

    public HongikHakdangCardVariantService(
        HongdalContext db,
        IHongikHakdangCardImageStore imageStore,
        IHongikHakdangCardVariantRenderer renderer,
        ILogger<HongikHakdangCardVariantService> logger)
    {
        _db = db;
        _imageStore = imageStore;
        _renderer = renderer;
        _logger = logger;
    }

    public Task<HongikHakdangCardVariantPreparationResultDto> EnsureActiveVariantsAsync(
        CancellationToken cancellationToken)
        => EnsureVariantsAsync(null, cancellationToken);

    public Task<HongikHakdangCardVariantPreparationResultDto> EnsureCardVariantsAsync(
        long cardId,
        CancellationToken cancellationToken)
        => EnsureVariantsAsync(cardId, cancellationToken);

    private async Task<HongikHakdangCardVariantPreparationResultDto> EnsureVariantsAsync(
        long? cardId,
        CancellationToken cancellationToken)
    {
        var query = _db.HongikHakdangCards
            .Include(x => x.ImageVariants)
            .Where(x => x.IsActive
                        && x.IsAdminEnabled
                        && x.ImageDownloadStatus == HongikHakdangCard.ImageDownloadedStatus
                        && x.LocalImagePath != null
                        && x.ImageSha256 != null);
        if (cardId.HasValue)
        {
            query = query.Where(x => x.Id == cardId.Value);
        }

        var cards = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var generated = 0;
        var reused = 0;
        var failed = 0;

        foreach (var card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? sourceBytes = null;
            var cardChanged = false;

            foreach (var variantKind in RequiredVariantKinds)
            {
                var existing = card.ImageVariants.FirstOrDefault(x => x.VariantKind == variantKind);
                if (existing is not null
                    && string.Equals(existing.SourceImageSha256, card.ImageSha256, StringComparison.OrdinalIgnoreCase)
                    && _imageStore.Exists(existing.LocalImagePath))
                {
                    reused++;
                    continue;
                }

                try
                {
                    sourceBytes ??= await _imageStore.ReadAsync(card.LocalImagePath!, cancellationToken);
                    var rendered = _renderer.Render(variantKind, sourceBytes);
                    var stored = await _imageStore.SaveVariantAsync(
                        rendered.VariantKind,
                        rendered.Bytes,
                        cancellationToken);
                    var now = DateTime.UtcNow;

                    if (existing is null)
                    {
                        existing = new HongikHakdangCardImageVariant
                        {
                            Card = card,
                            VariantKind = rendered.VariantKind,
                            CreatedAtUtc = now
                        };
                        card.ImageVariants.Add(existing);
                    }

                    existing.Width = rendered.Width;
                    existing.Height = rendered.Height;
                    existing.LocalImagePath = stored.RelativePath;
                    existing.ContentType = stored.ContentType;
                    existing.SizeBytes = stored.SizeBytes;
                    existing.Sha256 = stored.Sha256;
                    existing.SourceImageSha256 = card.ImageSha256!;
                    existing.UpdatedAtUtc = now;
                    cardChanged = true;
                    generated++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(
                        ex,
                        "홍익학당 카드 파생 이미지 생성 실패. CardId={CardId} VariantKind={VariantKind}",
                        card.Id,
                        variantKind);
                }
            }

            if (cardChanged)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return new HongikHakdangCardVariantPreparationResultDto(
            cards.Count,
            generated,
            reused,
            failed);
    }
}
