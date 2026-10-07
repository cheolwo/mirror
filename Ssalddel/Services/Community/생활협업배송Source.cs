using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Data;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

/// <summary>개인 주소를 복사하지 않고 기존 생활 화물의 본인·출처·실제 수행 상태만 읽습니다.</summary>
public sealed class Ef생활협업배송Source(SsalddelContext db) : I생활협업배송Source
{
    public async Task<생활협업배송Snapshot?> 조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return null;
        var request = await db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == requestId)
            .Select(x => new { x.의뢰Id, x.주문자UserId, x.클라이언트요청Id, x.정산메모, x.상태, x.배차상태 })
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return null;
        var cancelled = request.상태 == 상태값.의뢰상태.취소 || request.배차상태 == 상태값.배차상태.취소;
        return new(request.의뢰Id, request.주문자UserId ?? string.Empty,
            생활배송출처Evidence.Read(request.정산메모),
            request.클라이언트요청Id?.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal) == true,
            !cancelled && request.배차상태 == 상태값.배차상태.인수완료, cancelled);
    }
}

/// <summary>신규 신청의 출처만 보존하며 과거 지문에서 게시글 번호를 역추정하지 않습니다.</summary>
public static class 생활배송출처Evidence
{
    public const string Marker = ":source-post:";
    public static long? Read(string? memo)
    {
        if (memo is null || !memo.StartsWith("neighborhood-delivery:v1:", StringComparison.Ordinal)) return null;
        var markerAt = memo.IndexOf(Marker, StringComparison.Ordinal);
        if (markerAt < 0 || markerAt != memo.LastIndexOf(Marker, StringComparison.Ordinal)) return null;
        var suffix = memo.AsSpan(markerAt + Marker.Length);
        var policyAt = suffix.IndexOf(생활배송배차Policy.Marker, StringComparison.Ordinal);
        if (policyAt >= 0) suffix = suffix[..policyAt];
        return long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
