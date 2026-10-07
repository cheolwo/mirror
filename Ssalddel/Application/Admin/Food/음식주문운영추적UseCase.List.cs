using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Admin.Food;

public sealed partial class 음식주문운영추적UseCase
{
    public async Task<AdminFoodOrderListDto> 조회목록Async(
        string? query = null, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var clean = query?.Trim() ?? string.Empty;
        if (clean.Length > 100 || page < 1 || page > 10000 || pageSize is < 1 or > 50)
            throw new ArgumentException("검색어는 100자 이내, 페이지는 1 이상, 목록 크기는 1~50건이어야 합니다.");
        var orders = db.음식주문.AsNoTracking().AsQueryable();
        if (clean.Length > 0)
            orders = orders.Where(x => x.주문번호.Contains(clean) || x.음식점명.Contains(clean));
        var count = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminFoodOrderListItemDto
            {
                OrderNo = x.주문번호, RestaurantName = x.음식점명,
                OrderStatus = x.상태, DispatchStatus = x.배차상태,
                CreatedAtUtc = x.CreatedAt, UpdatedAtUtc = x.UpdatedAt
            }).ToListAsync(cancellationToken);
        foreach (var item in items) item.OrderStatus = 음식주문상태코드.Normalize(item.OrderStatus);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize, ServerNowUtc = DateTime.UtcNow };
    }
}
