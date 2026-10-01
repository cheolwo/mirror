using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Food;

// 예상하지 못한 내부 InvalidOperationException과 확인된 메뉴명 충돌을 구분한다.
public sealed class 음식점메뉴명충돌Exception(string message) : InvalidOperationException(message);

public interface I음식점메뉴관리UseCase
{
    Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(long 음식점Id, CancellationToken cancellationToken);
    Task<음식점메뉴관리응답> 등록Async(long 음식점Id, 음식점메뉴등록요청 request, CancellationToken cancellationToken);
    Task<음식점메뉴관리응답?> 수정Async(long 음식점Id, long 메뉴Id, 음식점메뉴수정요청 request, CancellationToken cancellationToken);
}

public sealed class 음식점메뉴관리UseCase(SsalddelContext db) : I음식점메뉴관리UseCase
{
    public async Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(
        long 음식점Id,
        CancellationToken cancellationToken)
        => (await db.음식점메뉴
                .AsNoTracking()
                .Where(item => item.음식점공개프로필Id == 음식점Id)
                .OrderBy(item => item.표시순서)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken))
            .Select(item => ToResponse(item))
            .ToArray();

    public async Task<음식점메뉴관리응답> 등록Async(
        long 음식점Id,
        음식점메뉴등록요청 request,
        CancellationToken cancellationToken)
    {
        Validate(request.메뉴명, request.설명, request.판매가, request.대표이미지Url);
        if (request.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("메뉴 등록의 클라이언트 요청 ID가 필요합니다.");
        }

        if (!await db.음식점공개프로필.AsNoTracking().AnyAsync(x => x.Id == 음식점Id, cancellationToken))
        {
            throw new KeyNotFoundException("음식점 공개 프로필을 찾을 수 없습니다.");
        }

        var menuName = request.메뉴명.Trim();
        var existing = await db.음식점메뉴
            .SingleOrDefaultAsync(x => x.음식점공개프로필Id == 음식점Id && x.메뉴명 == menuName, cancellationToken);
        if (existing is not null)
        {
            if (!Same(existing, request))
            {
                throw new 음식점메뉴명충돌Exception("같은 이름의 메뉴가 다른 내용으로 이미 존재합니다.");
            }

            return ToResponse(existing, false);
        }

        var now = DateTime.UtcNow;
        var menu = new 음식점메뉴
        {
            음식점공개프로필Id = 음식점Id,
            메뉴명 = menuName,
            설명 = Clean(request.설명),
            판매가 = request.판매가,
            대표이미지Url = CleanNullable(request.대표이미지Url),
            공개여부 = request.공개여부,
            품절여부 = request.품절여부,
            표시순서 = request.표시순서,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.음식점메뉴.Add(menu);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ToResponse(menu, true);
        }
        catch (DbUpdateException)
        {
            db.Entry(menu).State = EntityState.Detached;
            existing = await db.음식점메뉴.AsNoTracking()
                .SingleOrDefaultAsync(x => x.음식점공개프로필Id == 음식점Id && x.메뉴명 == menuName, cancellationToken);
            if (existing is not null && Same(existing, request))
            {
                return ToResponse(existing, false);
            }

            throw;
        }
    }

    public async Task<음식점메뉴관리응답?> 수정Async(
        long 음식점Id,
        long 메뉴Id,
        음식점메뉴수정요청 request,
        CancellationToken cancellationToken)
    {
        Validate(request.메뉴명, request.설명, request.판매가, request.대표이미지Url);
        var menu = await db.음식점메뉴
            .SingleOrDefaultAsync(x => x.Id == 메뉴Id && x.음식점공개프로필Id == 음식점Id, cancellationToken);
        if (menu is null)
        {
            return null;
        }

        if (request.예상Revision <= 0 || Revision(menu) != request.예상Revision)
        {
            throw new DbUpdateConcurrencyException("메뉴가 다른 요청에서 먼저 변경되었습니다.");
        }

        var menuName = request.메뉴명.Trim();
        // 수정 전 업무 충돌을 알린다. 동시 쓰기의 최종 방어는 DB 고유 제약이며 이 사전 조회가 잠금은 아니다.
        if (await db.음식점메뉴.AsNoTracking().AnyAsync(
                x => x.음식점공개프로필Id == 음식점Id && x.Id != 메뉴Id && x.메뉴명 == menuName,
                cancellationToken))
        {
            throw new 음식점메뉴명충돌Exception("같은 이름의 메뉴가 이미 존재합니다. 다른 이름을 사용해 주세요.");
        }

        menu.메뉴명 = menuName;
        menu.설명 = Clean(request.설명);
        menu.판매가 = request.판매가;
        menu.대표이미지Url = CleanNullable(request.대표이미지Url);
        menu.공개여부 = request.공개여부;
        menu.품절여부 = request.품절여부;
        menu.표시순서 = request.표시순서;
        menu.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(menu);
    }

    private static void Validate(string? name, string? description, decimal price, string? imageUrl)
    {
        if (음식점메뉴입력Policy.오류조회(name, description, price, imageUrl) is { } error)
            throw new ArgumentException(error);
    }

    private static bool Same(음식점메뉴 menu, 음식점메뉴등록요청 request)
        => menu.메뉴명 == request.메뉴명.Trim()
           && menu.설명 == Clean(request.설명)
           && menu.판매가 == request.판매가
           && menu.대표이미지Url == CleanNullable(request.대표이미지Url)
           && menu.공개여부 == request.공개여부
           && menu.품절여부 == request.품절여부
           && menu.표시순서 == request.표시순서;

    private static 음식점메뉴관리응답 ToResponse(음식점메뉴 menu, bool newlyCreated = false)
        => new()
        {
            Id = menu.Id,
            음식점Id = menu.음식점공개프로필Id,
            메뉴명 = menu.메뉴명,
            설명 = menu.설명,
            판매가 = menu.판매가,
            대표이미지Url = menu.대표이미지Url,
            공개여부 = menu.공개여부,
            품절여부 = menu.품절여부,
            표시순서 = menu.표시순서,
            Revision = Revision(menu),
            UpdatedAtUtc = menu.UpdatedAtUtc,
            새로생성됨 = newlyCreated
        };

    private static long Revision(음식점메뉴 menu) => menu.UpdatedAtUtc.Ticks;
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static string? CleanNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
