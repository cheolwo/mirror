using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;
using Microsoft.EntityFrameworkCore;
using 홍달.도메인.창고;

namespace Hongdal.Services.Orderer;

public sealed record 공동구매수령창고배정결과(
    long 창고Id,
    string 창고유형,
    string 창고명,
    string 주소참조키,
    bool 자동생성여부);

public interface I공동구매수령창고Service
{
    Task<공동구매수령창고배정결과> 확보Async(
        공동구매자동수요등록Command command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 공동구매 주문자의 실물 창고 또는 자택 수령지 가상 창고를 결정합니다.
/// 주소 원문은 관계형 창고 영역에만 저장하고 공동구매·커뮤니티 원장에는 창고 ID와 참조키만 전달합니다.
/// </summary>
public sealed class 공동구매수령창고Service : I공동구매수령창고Service
{
    private readonly HongdalContext _db;

    public 공동구매수령창고Service(HongdalContext db)
    {
        _db = db;
    }

    public async Task<공동구매수령창고배정결과> 확보Async(
        공동구매자동수요등록Command command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var 주문자키 = command.주문자키?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(주문자키))
        {
            throw new InvalidOperationException("입고 수령 창고를 배정하려면 주문자 식별키가 필요합니다.");
        }

        if (command.도착창고Id is > 0)
        {
            var 선택창고 = await _db.창고.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == command.도착창고Id.Value
                && x.IsActive
                && (x.소유자UserId == 주문자키
                    || _db.창고사용자.Any(user => user.창고Id == x.Id && user.UserId == 주문자키)),
                cancellationToken);
            if (선택창고 is null)
            {
                throw new InvalidOperationException("주문자가 사용할 수 있는 도착 창고를 찾을 수 없습니다.");
            }

            return 결과(선택창고, 자동생성여부: false);
        }

        var 수령주소 = 주소결합(command.수령도로명주소, command.수령상세주소);
        if (!string.IsNullOrWhiteSpace(수령주소))
        {
            var 동일수령지 = await _db.창고.AsNoTracking().FirstOrDefaultAsync(x =>
                x.소유자UserId == 주문자키
                && x.소유자유형 == 창고소유자유형.주문자
                && x.창고유형 == 홍달.도메인.창고.창고유형.가상창고
                && x.주소 == 수령주소
                && x.IsActive,
                cancellationToken);
            if (동일수령지 is not null)
            {
                return 결과(동일수령지, 자동생성여부: false);
            }

            var 기본창고존재 = await _db.창고.AnyAsync(x =>
                x.소유자UserId == 주문자키 && x.기본창고여부 && x.IsActive,
                cancellationToken);
            var 수령지명 = 정규화(command.수령지표시명, "자택 수령지");
            var 가상창고 = new 창고
            {
                소유자UserId = 주문자키,
                소유자유형 = 창고소유자유형.주문자,
                창고유형 = 홍달.도메인.창고.창고유형.가상창고,
                창고명 = 수령지명.EndsWith("가상 창고", StringComparison.Ordinal)
                    ? 수령지명
                    : $"{수령지명} 가상 창고",
                주소 = 수령주소,
                국가코드 = "KR",
                담당자명 = 정규화(command.주문자표시명, 주문자키),
                기본창고여부 = !기본창고존재,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.창고.Add(가상창고);
            await _db.SaveChangesAsync(cancellationToken);
            return 결과(가상창고, 자동생성여부: true);
        }

        var 기본수령창고 = await _db.창고.AsNoTracking()
            .Where(x => x.소유자UserId == 주문자키 && x.IsActive)
            .OrderByDescending(x => x.기본창고여부)
            .ThenByDescending(x => x.창고유형 == 홍달.도메인.창고.창고유형.가상창고)
            .FirstOrDefaultAsync(cancellationToken);
        if (기본수령창고 is null)
        {
            throw new InvalidOperationException(
                "보유 창고가 없으면 자택 등 수령 도로명주소를 입력해 가상 창고를 만들어야 합니다.");
        }

        return 결과(기본수령창고, 자동생성여부: false);
    }

    private static 공동구매수령창고배정결과 결과(창고 warehouse, bool 자동생성여부)
        => new(
            warehouse.Id,
            warehouse.창고유형,
            warehouse.창고명,
            $"warehouse:{warehouse.Id}:receiving-address",
            자동생성여부);

    private static string 주소결합(string? 도로명주소, string? 상세주소)
        => string.Join(" ", new[] { 도로명주소, 상세주소 }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

    private static string 정규화(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
