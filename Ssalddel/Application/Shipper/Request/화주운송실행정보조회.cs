using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using Ssalddel.Services.Community;
using Ssalddel.Application.Driver.DispatchAction;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.Services.Operations;
using Ssalddel.Application.Common.Transport;
using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Application.Shipper.Request;

internal sealed record 화주운송실행정보(
    운송원장? 운송원장,
    용달기사? 기사,
    기사위치기록? 최근위치,
    운영체제업무인계Dto? 운영체제인계,
    운영체제업무인계Dto? 운송완료화주인계,
    IReadOnlyList<비정상운송사건Dto> 비정상운송사건목록,
    string 기사위치상태Code);

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
            .ThenByDescending(x => x.Id)
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

        var protectedLocations = await 현재배정기사위치조회.조회Async(db, requestIds, cancellationToken);
        var requests = await db.화주운송의뢰.AsNoTracking()
            .Where(x => requestIds.Contains(x.의뢰Id)).ToListAsync(cancellationToken);
        var requestById = requests.ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);
        var neighborhoodRequests = requests.Where(NeighborhoodDeliveryLocationPolicy.IsNeighborhoodDelivery);
        var neighborhoodLocations = new Dictionary<string, 기사위치기록?>(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;
        foreach (var neighborhoodRequest in neighborhoodRequests)
        {
            latestLedgers.TryGetValue(neighborhoodRequest.의뢰Id, out var neighborhoodLedger);
            var recent = await NeighborhoodDeliveryLocationPolicy.ReadAsync(db, neighborhoodRequest, neighborhoodLedger, nowUtc, cancellationToken);
            neighborhoodLocations[neighborhoodRequest.의뢰Id] = recent.Location;
        }

        // 조회 중 종료·재배정이 일어났는지 다시 읽는다. 새 배정에 이전 기사의 좌표를 넘기지 않는다.
        var currentRequests = (await db.화주운송의뢰.AsNoTracking()
            .Where(x => requestIds.Contains(x.의뢰Id)).ToListAsync(cancellationToken))
            .ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);
        var currentLedgers = (await db.운송원장.AsNoTracking()
            .Where(x => requestIds.Contains(x.의뢰Id)).OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id).ToListAsync(cancellationToken))
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return requestIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                requestId => requestId,
                requestId =>
                {
                    latestLedgers.TryGetValue(requestId, out var ledger);
                    var driverId = ledger?.확정기사Id;
                    var hasDriver = !string.IsNullOrWhiteSpace(driverId);
                    requestById.TryGetValue(requestId, out var source);
                    currentRequests.TryGetValue(requestId, out var currentSource);
                    currentLedgers.TryGetValue(requestId, out var currentLedger);
                    var canExposeLocation = hasDriver && 현재배정유효(source, ledger)
                        && currentSource is not null && source!.상태 == currentSource.상태
                        && source.화주Id == currentSource.화주Id && source.주문자UserId == currentSource.주문자UserId
                        && source.배차상태 == currentSource.배차상태 && source.UpdatedAt == currentSource.UpdatedAt
                        && 같은배정(ledger, currentLedger);

                    drivers.TryGetValue(driverId ?? string.Empty, out var driver);
                    기사위치기록? location = null;
                    protectedLocations.TryGetValue(requestId, out var protectedLocation);
                    var locationCode = protectedLocation?.상태Code ?? 기사위치상태Codes.Unavailable;
                    if (neighborhoodLocations.TryGetValue(requestId, out var neighborhoodLocation))
                    {
                        location = neighborhoodLocation;
                        locationCode = location is not null && canExposeLocation ? 기사위치상태Codes.Available
                            : source is not null && NeighborhoodDeliveryLocationPolicy.IsClosed(source, currentLedger)
                                ? 기사위치상태Codes.Closed : 기사위치상태Codes.Unavailable;
                    }
                    else location = protectedLocation?.운송Id == ledger?.Id ? protectedLocation?.위치 : null;
                    if (!canExposeLocation && locationCode != 기사위치상태Codes.Closed)
                        locationCode = 기사위치상태Codes.Unavailable;
                    if (canExposeLocation && location is not null)
                        location.기록시각 = Utc(location.기록시각); // EF의 Unspecified UTC를 JSON에서도 UTC로 보존한다.
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
                        requestIncidents ?? Array.Empty<비정상운송사건Dto>(),
                        locationCode);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool 현재배정유효(살뜰.도메인.화주.화주운송의뢰? request, 운송원장? ledger)
        => request is not null && request.상태 == 상태값.의뢰상태.생성됨
           && request.배차상태 is not (상태값.배차상태.인수완료 or 상태값.배차상태.하차완료 or 상태값.배차상태.취소)
           && ledger is not null && ledger.의뢰Id == request.의뢰Id
           && ledger.배차업무유형 == 상태값.배차업무유형.용달운송
           && ledger.배차큐단계 == 상태값.배차큐단계.확정 && !string.IsNullOrWhiteSpace(ledger.확정기사Id)
           && ledger.상태 is (상태값.배차대기상태.확정 or 상태값.배차상태.배차확정
               or "이동중" or "상차지도착" or 상태값.배차상태.상차중 or 상태값.배차상태.상차완료
               or 상태값.배차상태.운송중 or "하차지도착");

    private static bool 같은배정(운송원장? before, 운송원장? current)
        => before is not null && current is not null && before.Id == current.Id
           && before.확정기사Id == current.확정기사Id && before.추천라운드 == current.추천라운드
           && before.상태 == current.상태 && before.배차큐단계 == current.배차큐단계
           && before.UpdatedAt == current.UpdatedAt && before.RowVersion == current.RowVersion;

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

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
