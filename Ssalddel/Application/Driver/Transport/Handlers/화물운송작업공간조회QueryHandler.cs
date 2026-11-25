using MediatR;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Continuity;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 화물운송작업공간조회QueryHandler
    : IRequestHandler<화물운송작업공간조회Query, 기사화물운송작업공간응답>
{
    private readonly ISender _sender;
    private readonly I기사운송일정구성Service _일정구성Service;
    private readonly I화물연속배차UseCase _연속배차UseCase;

    public 화물운송작업공간조회QueryHandler(
        ISender sender,
        I기사운송일정구성Service 일정구성Service,
        I화물연속배차UseCase 연속배차UseCase)
    {
        _sender = sender;
        _일정구성Service = 일정구성Service;
        _연속배차UseCase = 연속배차UseCase;
    }

    public async Task<기사화물운송작업공간응답> Handle(
        화물운송작업공간조회Query request,
        CancellationToken cancellationToken)
    {
        var transports = (await _sender.Send(new 운송목록조회Query(request.기사Id), cancellationToken))
            .Where(x => !string.Equals(x.상태, "인수완료", StringComparison.Ordinal))
            .OrderBy(x => 화물기사다음행동Policy.Priority(x.상태))
            .ThenBy(x => x.출발_픽업 ?? x.도착 ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .ToArray();
        // 표시용 운송번호와 의뢰 ID를 비교하지 않습니다. 현재 응답에 유일하게 연결되고 공개가 허용된 typed 원장 ID만 사용합니다.
        var uniqueTransports = transports.GroupBy(x => x.Id)
            .Where(group => group.Key > 0 && group.Count() == 1).Select(group => group.Single()).ToArray();
        var uniqueTransportIds = uniqueTransports.Select(x => x.Id).ToHashSet();
        var uniqueTransportMap = uniqueTransports.ToDictionary(x => x.Id);
        var disclosedTransportIds = uniqueTransports.Where(x => !x.개인정보제공보류)
            .Select(x => x.Id).ToHashSet();
        bool CanDisclose(기사운송일정항목 stop)
            => stop.운송Id is > 0 && disclosedTransportIds.Contains(stop.운송Id.Value);
        bool CanDiscloseCoordinates(기사운송일정항목 stop)
            => CanDisclose(stop) && (stop.단계유형 is "pickup" or "dropoff")
                && stop.좌표 is { Latitude: >= -90m and <= 90m, Longitude: >= -180m and <= 180m }
                && stop.좌표.Latitude != 0m && stop.좌표.Longitude != 0m;
        var plan = await _일정구성Service.구성Async(request.기사Id, null, cancellationToken);
        // 요약의 운송번호는 의뢰 ID가 아닐 수 있습니다. 일정 서비스가 검증한 typed 원장 ID를 대조합니다.
        var preferredId = 화물수락정차다음행동Policy.Select(plan,
            transports.Select(x => (x.Id, (string?)null, x.상태)), out var stopOrderHoldCode);
        var primary = preferredId is { } id ? transports.First(x => x.Id == id) : transports.FirstOrDefault();
        var acceptedOrderApplied = plan.수락정차순서반영됨 && stopOrderHoldCode is null;
        DateTime? ConfirmedWindowStart(기사운송일정항목 stop)
        {
            if (!acceptedOrderApplied || !CanDisclose(stop)) return null;
            // 일정의 기준시각은 하차 시작이 없을 때 원장 도착일시로 fallback하므로 시작의 출처로 쓰지 않는다.
            var transport = uniqueTransportMap[stop.운송Id!.Value];
            var start = stop.단계유형 switch
            {
                "pickup" => transport.상차시간창시작일시,
                "dropoff" => transport.하차시간창시작일시,
                _ => null
            };
            return start is { } value && value != default ? value : null;
        }
        var stops = plan.항목목록.OrderBy(x => x.순서).Select(x => new 기사화물경로정차응답
        {
            운송Id = x.운송Id is > 0 && uniqueTransportIds.Contains(x.운송Id.Value) ? x.운송Id : null,
            의뢰Id = x.의뢰Id,
            단계 = string.Equals(x.단계유형, "pickup", StringComparison.OrdinalIgnoreCase) ? "상차" : "하차",
            주소 = !CanDisclose(x)
                ? (string.Equals(x.단계유형, "pickup", StringComparison.OrdinalIgnoreCase)
                    ? 생활배송기사정보공개Policy.PickupPending : 생활배송기사정보공개Policy.DropoffPending)
                : x.주소,
            순서 = x.순서,
            위도 = CanDiscloseCoordinates(x) ? x.좌표!.Latitude : null,
            경도 = CanDiscloseCoordinates(x) ? x.좌표!.Longitude : null,
            시간창시작일시 = ConfirmedWindowStart(x),
            시간창종료일시 = x.시간창종료일시,
            좌표근거있음 = CanDiscloseCoordinates(x),
            시간창근거있음 = x.시간창종료일시.HasValue
        }).ToArray();

        var missingEvidence = stops.Any(x => !x.좌표근거있음 || !x.시간창근거있음);
        var additionalAcceptanceBlocked = transports.Length > 0 && missingEvidence;
        var continuity = await _연속배차UseCase.조회Async(request.기사Id, cancellationToken);
        var commitments = await _연속배차UseCase.시간약속목록Async(request.기사Id, cancellationToken);
        var routeRisk = await _연속배차UseCase.현재위험조회Async(request.기사Id, cancellationToken);
        return new 기사화물운송작업공간응답
        {
            활성운송목록 = transports,
            다음행동운송 = primary,
            다음행동 = stopOrderHoldCode is not null ? "저장된 방문 순서를 다시 확인해 주세요."
                : primary is null ? "대기" : primary.운송진행보류 ? primary.다음행동안내 : 화물기사다음행동Policy.NextAction(primary.상태),
            권장경로정차목록 = stops,
            수락정차순서반영됨 = acceptedOrderApplied,
            수락정차순서보류Code = stopOrderHoldCode,
            일정검증상태 = stopOrderHoldCode is not null ? "수락정차순서보류·재확인필요"
                : missingEvidence ? "근거부족·추가수락차단" : "수락시최신원장재검증",
            적재검증상태 = transports.Length == 0 ? "활성화물없음" : "수락시구간별재검증",
            WarningCodes = (missingEvidence ? new[] { "FreightScheduleEvidenceMissing" } : [])
                .Concat(stopOrderHoldCode is null ? [] : new[] { stopOrderHoldCode }).ToArray(),
            BlockCodes = (additionalAcceptanceBlocked ? new[] { "FreightScheduleEvidenceMissing" } : [])
                .Concat(stopOrderHoldCode is null ? [] : new[] { stopOrderHoldCode }).ToArray(),
            연속배차 = continuity,
            시간약속목록 = commitments,
            현재경로위험 = routeRisk
        };
    }
}

/// <summary>원장이 재검증한 typed 정차만 다음 업무로 사용하며 표시 문구에서 순서를 복원하지 않습니다.</summary>
internal static class 화물수락정차다음행동Policy
{
    internal static long? Select(기사운송일정계획 plan,
        IEnumerable<(long Id, string? RequestId, string Status)> transports, out string? holdCode)
    {
        holdCode = plan.수락정차순서보류Code;
        if (holdCode is not null || !plan.수락정차순서반영됨) return null;
        var first = plan.항목목록.OrderBy(x => x.순서).FirstOrDefault();
        var matching = first?.운송Id is > 0
            ? transports.Where(x => x.Id == first.운송Id && (x.RequestId is null || x.RequestId == first.의뢰Id)).ToArray() : [];
        if (first is null || matching.Length != 1 || !(first.단계유형 switch
            {
                "pickup" => matching[0].Status is "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중" or "상차지도착",
                "dropoff" => matching[0].Status is "상차완료" or "운송중" or "하차지도착",
                _ => false
            }))
        {
            holdCode = "FreightAcceptedStopOrder.NextActionMismatch";
            return null;
        }
        return matching[0].Id;
    }
}
