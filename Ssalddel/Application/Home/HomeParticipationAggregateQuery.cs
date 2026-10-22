using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Home;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Home;

/// <summary>공개 누적·월별 조회가 같은 전문 원장 선정·계정 결속 기준을 소비합니다.</summary>
internal static class HomeParticipationAggregateQuery
{
    internal static IQueryable<HomeParticipationCompletedRecord> EligibleRecords(SsalddelContext db)
        => from transport in db.운송원장.AsNoTracking()
           where transport.배차업무유형 == 상태값.배차업무유형.용달운송
               && transport.상태 == 기사운송상태코드.인수완료
               && transport.도착 != null && transport.의뢰Id != ""
               // 의뢰의 업무 키에는 unique 제약이 없으므로 모호한 연결은 제외하고 행 증폭을 막습니다.
               && db.화주운송의뢰.Count(request => request.의뢰Id == transport.의뢰Id) == 1
               && db.화주운송의뢰.Any(request => request.의뢰Id == transport.의뢰Id && !request.모의처리처수행)
           select new HomeParticipationCompletedRecord
           {
               ArrivedAtUtc = transport.도착!.Value,
               ShipperId = transport.화주Id,
               ShipperBound = transport.화주Id != ""
                   && db.화주운송의뢰.Any(request => request.의뢰Id == transport.의뢰Id && request.화주Id == transport.화주Id)
                   && db.Users.Any(user => user.Id == transport.화주Id),
               DriverId = transport.확정기사Id,
               DriverBound = transport.확정기사Id != null && transport.확정기사Id != ""
                   && transport.확정기사Id == transport.기사_운송자
                   && db.Users.Any(user => user.Id == transport.확정기사Id)
           };

    internal static async Task<HomeParticipationSummaryResponse> ReadSummaryAsync(SsalddelContext db,
        ISsalddelExecutionModePolicy execution, TimeProvider clock, CancellationToken cancellationToken)
    {
        // 숫자·서로 다른 계정·누락 여부만 SQL에서 함께 읽습니다. 원장 전체나 개인 식별값은 적재하지 않습니다.
        var totals = await EligibleRecords(db).GroupBy(_ => 1).Select(group => new
        {
            CompletedCount = group.Count(),
            ShipperCount = group.Where(item => item.ShipperBound).Select(item => item.ShipperId).Distinct().Count(),
            DriverCount = group.Where(item => item.DriverBound).Select(item => item.DriverId).Distinct().Count(),
            Incomplete = group.Any(item => !item.ShipperBound || !item.DriverBound)
        }).SingleOrDefaultAsync(cancellationToken);
        return new()
        {
            AsOfUtc = clock.GetUtcNow(), ExecutionMode = execution.Mode.ToString(),
            CompletedFreightTransportCount = totals?.CompletedCount ?? 0,
            FreightShipperAccountCount = totals?.ShipperCount ?? 0,
            FreightDriverAccountCount = totals?.DriverCount ?? 0,
            HasIncompleteParticipantBindings = totals?.Incomplete ?? false
            // 확정 플랫폼 이익과 실분배 지급액은 근거 정본을 연결할 때까지 null을 유지합니다.
        };
    }
}

internal sealed class HomeParticipationCompletedRecord
{
    public DateTime ArrivedAtUtc { get; init; }
    public string ShipperId { get; init; } = "";
    public bool ShipperBound { get; init; }
    public string? DriverId { get; init; }
    public bool DriverBound { get; init; }
}
