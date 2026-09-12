using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.Services.Operations;

namespace Ssalddel.Application.Shipper.Request;

internal sealed record 화주운송실행정보(
    운송원장? 운송원장,
    용달기사? 기사,
    기사위치기록? 최근위치,
    운영체제업무인계Dto? 운영체제인계,
    운영체제업무인계Dto? 운송완료화주인계,
    IReadOnlyList<비정상운송사건Dto> 비정상운송사건목록);

internal static class 화주운송실행정보조회
{
    internal static async Task<IReadOnlyDictionary<string, 화주운송실행정보>> 조회Async(
        SsalddelContext db,
        IReadOnlyCollection<string> requestIds,
        CancellationToken cancellationToken)
    {
        if (requestIds.Count == 0)
        {
            return new Dictionary<string, 화주운송실행정보>(StringComparer.OrdinalIgnoreCase);
        }

        var ledgers = await db.운송원장
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.의뢰Id))
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var latestLedgers = ledgers
            .GroupBy(x => x.의뢰Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var incidents = await db.비정상운송사건
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.운송의뢰Id))
            .OrderByDescending(x => x.최근신고시각Utc)
            .ToListAsync(cancellationToken);
        var incidentsByRequestId = incidents
            .GroupBy(x => x.운송의뢰Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<비정상운송사건Dto>)x.Select(ToDto).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var sourceStableIds = requestIds
            .Select(화주운송의뢰화물운송인계Contract.출발업무StableId)
            .ToList();
        var handoffs = await db.운영체제업무인계
            .AsNoTracking()
            .Where(x => x.출발운영체제Id == OperatingSystemIds.ShipperTransportManagement
                        && x.출발업무유형Code == 화주운송의뢰화물운송인계Contract.출발업무유형Code
                        && sourceStableIds.Contains(x.출발업무StableId))
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        var handoffByRequestId = handoffs
            .GroupBy(
                x => 화주운송의뢰화물운송인계Contract.운송의뢰Id(x.출발업무StableId),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => 운영체제업무인계Coordinator.ToDto(x.First()),
                StringComparer.OrdinalIgnoreCase);

        var completionSourceIds = latestLedgers.Values
            .Select(x => 화물운송완료화주인수인계Contract.출발업무StableId(x.Id))
            .ToList();
        var completionHandoffs = await db.운영체제업무인계
            .AsNoTracking()
            .Where(x => x.출발운영체제Id == OperatingSystemIds.DomesticCargoTransport
                        && x.도착운영체제Id == OperatingSystemIds.ShipperTransportManagement
                        && x.출발업무유형Code == 화물운송완료화주인수인계Contract.출발업무유형Code
                        && completionSourceIds.Contains(x.출발업무StableId))
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        var completionHandoffBySourceId = completionHandoffs
            .GroupBy(x => x.출발업무StableId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => 운영체제업무인계Coordinator.ToDto(x.First()),
                StringComparer.Ordinal);

        var driverIds = latestLedgers.Values
            .Select(x => x.확정기사Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var drivers = driverIds.Count == 0
            ? new Dictionary<string, 용달기사>(StringComparer.OrdinalIgnoreCase)
            : (await db.용달기사
                .AsNoTracking()
                .Where(x => driverIds.Contains(x.기사Id))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.기사Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var locations = driverIds.Count == 0
            ? new Dictionary<string, 기사위치기록>(StringComparer.OrdinalIgnoreCase)
            : (await db.기사위치기록
                .AsNoTracking()
                .Where(x => driverIds.Contains(x.기사Id))
                .GroupBy(x => x.기사Id)
                .Select(group => group
                    .OrderByDescending(x => x.기록시각)
                    .ThenByDescending(x => x.Id)
                    .First())
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.기사Id, x => x, StringComparer.OrdinalIgnoreCase);

        return requestIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                requestId => requestId,
                requestId =>
                {
                    latestLedgers.TryGetValue(requestId, out var ledger);
                    var driverId = ledger?.확정기사Id;
                    var hasDriver = !string.IsNullOrWhiteSpace(driverId);
                    var canExposeLocation = hasDriver && 기사위치공개가능(ledger?.상태);

                    drivers.TryGetValue(driverId ?? string.Empty, out var driver);
                    locations.TryGetValue(driverId ?? string.Empty, out var location);
                    handoffByRequestId.TryGetValue(requestId, out var handoff);
                    incidentsByRequestId.TryGetValue(requestId, out var requestIncidents);
                    운영체제업무인계Dto? completionHandoff = null;
                    if (ledger is not null)
                    {
                        completionHandoffBySourceId.TryGetValue(
                            화물운송완료화주인수인계Contract.출발업무StableId(ledger.Id),
                            out completionHandoff);
                    }

                    return new 화주운송실행정보(
                        ledger,
                        driver,
                        canExposeLocation ? location : null,
                        handoff,
                        completionHandoff,
                        requestIncidents ?? Array.Empty<비정상운송사건Dto>());
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool 기사위치공개가능(string? transportStatus)
        => transportStatus is not null
           && !string.Equals(transportStatus, 상태값.배차상태.인수완료, StringComparison.Ordinal)
           && !string.Equals(transportStatus, 상태값.배차상태.하차완료, StringComparison.Ordinal)
           && !string.Equals(transportStatus, 상태값.배차상태.취소, StringComparison.Ordinal);

    private static 비정상운송사건Dto ToDto(비정상운송사건 entity)
        => new()
        {
            사건StableId = entity.사건StableId,
            사건유형Code = entity.사건유형Code,
            사건유형표시명 = entity.원본예외Code,
            원본예외Code = entity.원본예외Code,
            단계Code = entity.단계Code,
            상태Code = entity.상태Code,
            현재담당Code = entity.현재담당Code,
            정산보류적용여부 = entity.정산보류적용여부,
            전체수량 = entity.전체수량,
            정상확인수량 = entity.정상확인수량,
            영향수량 = entity.영향수량,
            업무통제상태Code = entity.업무통제상태Code,
            보류범위Code = entity.보류범위Code,
            현장진행불가 = entity.현장진행불가,
            보험검토상태Code = entity.보험검토상태Code,
            해결결과Code = entity.해결결과Code,
            증빙참조있음 = entity.증빙참조있음,
            최초신고시각Utc = entity.최초신고시각Utc,
            최근신고시각Utc = entity.최근신고시각Utc,
            최근검토시각Utc = entity.최근검토시각Utc,
            Revision = entity.Revision
        };
}
