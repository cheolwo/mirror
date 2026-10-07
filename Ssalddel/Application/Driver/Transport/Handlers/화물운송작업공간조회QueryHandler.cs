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
        var primary = transports.FirstOrDefault();
        var privateRequestIds = transports.Where(x => x.개인정보제공보류).Select(x => x.운송번호)
            .ToHashSet(StringComparer.Ordinal);
        var plan = await _일정구성Service.구성Async(request.기사Id, null, cancellationToken);
        var stops = plan.항목목록.OrderBy(x => x.순서).Select(x => new 기사화물경로정차응답
        {
            의뢰Id = x.의뢰Id,
            단계 = string.Equals(x.단계유형, "pickup", StringComparison.OrdinalIgnoreCase) ? "상차" : "하차",
            주소 = privateRequestIds.Contains(x.의뢰Id)
                ? (string.Equals(x.단계유형, "pickup", StringComparison.OrdinalIgnoreCase)
                    ? 생활배송기사정보공개Policy.PickupPending : 생활배송기사정보공개Policy.DropoffPending)
                : x.주소,
            순서 = x.순서,
            시간창종료일시 = x.시간창종료일시,
            좌표근거있음 = !privateRequestIds.Contains(x.의뢰Id) && x.좌표 is not null,
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
            다음행동 = primary is null ? "대기" : primary.운송진행보류 ? primary.다음행동안내 : 화물기사다음행동Policy.NextAction(primary.상태),
            권장경로정차목록 = stops,
            일정검증상태 = missingEvidence ? "근거부족·추가수락차단" : "수락시최신원장재검증",
            적재검증상태 = transports.Length == 0 ? "활성화물없음" : "수락시구간별재검증",
            WarningCodes = missingEvidence ? ["FreightScheduleEvidenceMissing"] : [],
            BlockCodes = additionalAcceptanceBlocked ? ["FreightScheduleEvidenceMissing"] : [],
            연속배차 = continuity,
            시간약속목록 = commitments,
            현재경로위험 = routeRisk
        };
    }
}
