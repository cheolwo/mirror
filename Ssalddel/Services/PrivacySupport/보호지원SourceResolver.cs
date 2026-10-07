using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.PrivacySupport;

/// <summary>존재와 당사자를 기존 업무 원장에서 확인합니다. 역할 선택이나 client가 제시한 상대방 ID를 믿지 않습니다.</summary>
public sealed class 보호지원SourceResolver(SsalddelContext db, I생활협업연결Query collaborations) : I보호지원SourceResolver
{
    public async Task<보호지원Source?> 조회Async(string sourceKind, string sourceId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var parties = new List<string>();
        if (sourceKind == 보호지원출처Codes.생활협업)
        {
            var source = await collaborations.조회Async(sourceId, cancellationToken);
            if (source is null) return null;
            parties.AddRange([source.OwnerUserId, source.RequesterUserId]);
        }
        else if (sourceKind == 보호지원출처Codes.음식주문)
        {
            var source = await db.음식주문.AsNoTracking().Where(x => x.주문번호 == sourceId)
                .Select(x => new { x.주문자UserId, x.음식점Id }).SingleOrDefaultAsync(cancellationToken);
            if (source is null) return null;
            parties.Add(source.주문자UserId);
            var restaurantKey = source.음식점Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var operators = await (from claim in db.UserClaims
                                   join userRole in db.UserRoles on claim.UserId equals userRole.UserId
                                   join role in db.Roles on userRole.RoleId equals role.Id
                                   where claim.ClaimType == 음식점접근ClaimTypes.음식점Id && claim.ClaimValue == restaurantKey
                                         && role.Name == "음식점"
                                   select claim.UserId).ToListAsync(cancellationToken);
            parties.AddRange(operators);
            // 실제 수행했던 기사도 자신의 업무 분쟁을 접수할 수 있습니다. 고객 상세를 함께 투영하지 않습니다.
            if (await db.음식배달시도.AsNoTracking().AnyAsync(x => x.주문번호 == sourceId && x.기사Id == actorUserId
                    && x.수락시각Utc != default, cancellationToken)) parties.Add(actorUserId);
        }
        else if (sourceKind is 보호지원출처Codes.생활배송 or 보호지원출처Codes.화물의뢰)
        {
            var source = await db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == sourceId)
                .Select(x => new { x.주문자UserId, x.클라이언트요청Id }).SingleOrDefaultAsync(cancellationToken);
            if (source is null) return null;
            var neighborhood = source.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal);
            if (neighborhood != (sourceKind == 보호지원출처Codes.생활배송)) return null;
            parties.Add(source.주문자UserId);
            parties.AddRange(await db.운송원장.AsNoTracking().Where(x => x.의뢰Id == sourceId
                    && x.배차업무유형 != 상태값.배차업무유형.음식배달 && x.확정기사Id != null)
                .Select(x => x.확정기사Id!).ToListAsync(cancellationToken));
        }
        else return null;
        var ids = parties.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        return ids.Contains(actorUserId, StringComparer.Ordinal) ? new(ids) : null;
    }
}
