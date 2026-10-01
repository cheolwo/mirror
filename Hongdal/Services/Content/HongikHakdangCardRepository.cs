using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Content;

public interface IHongikHakdangCardRepository
{
    Task<List<HongikHakdangCardCollection>> GetCollectionsTrackedAsync(
        CancellationToken cancellationToken);

    Task<List<HongikHakdangCard>> GetCardsTrackedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<HongikHakdangCardCollection>> GetCollectionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardCollection?> FindCollectionTrackedAsync(
        long collectionId,
        CancellationToken cancellationToken);

    Task<HongikHakdangCard?> FindCardTrackedAsync(
        long cardId,
        CancellationToken cancellationToken);

    void AddCollection(HongikHakdangCardCollection collection);

    void AddCard(HongikHakdangCard card);

    void AddCollectionItem(HongikHakdangCardCollectionItem item);

    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class EfHongikHakdangCardRepository : IHongikHakdangCardRepository
{
    private readonly HongdalContext _db;

    public EfHongikHakdangCardRepository(HongdalContext db)
    {
        _db = db;
    }

    public Task<List<HongikHakdangCardCollection>> GetCollectionsTrackedAsync(
        CancellationToken cancellationToken)
        => _db.HongikHakdangCardCollections
            .Include(x => x.Items)
            .ThenInclude(x => x.Card)
            .ToListAsync(cancellationToken);

    public Task<List<HongikHakdangCard>> GetCardsTrackedAsync(CancellationToken cancellationToken)
        => _db.HongikHakdangCards.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HongikHakdangCardCollection>> GetCollectionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.HongikHakdangCardCollections
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.Card)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive && x.IsAdminEnabled);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<HongikHakdangCardCollection?> FindCollectionTrackedAsync(
        long collectionId,
        CancellationToken cancellationToken)
        => _db.HongikHakdangCardCollections
            .FirstOrDefaultAsync(x => x.Id == collectionId, cancellationToken);

    public Task<HongikHakdangCard?> FindCardTrackedAsync(
        long cardId,
        CancellationToken cancellationToken)
        => _db.HongikHakdangCards
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

    public void AddCollection(HongikHakdangCardCollection collection)
        => _db.HongikHakdangCardCollections.Add(collection);

    public void AddCard(HongikHakdangCard card)
        => _db.HongikHakdangCards.Add(card);

    public void AddCollectionItem(HongikHakdangCardCollectionItem item)
        => _db.HongikHakdangCardCollectionItems.Add(item);

    public Task SaveAsync(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}
