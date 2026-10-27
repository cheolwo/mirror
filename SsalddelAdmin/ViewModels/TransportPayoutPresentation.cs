using Ssalddel.Contracts.Admin.Settlement;
using SsalddelAdmin.Services;

namespace SsalddelAdmin.ViewModels;

/// <summary>동일 의뢰·운송의 지급 조회 결과만 표현합니다. 수납·월 이용료로 지급을 추정하지 않습니다.</summary>
public sealed class TransportPayoutPresentation
{
    public string PreparationStatus => "관리자 조회 미연결";
    public bool QueryCompleted { get; private init; }
    public bool QueryFailed { get; private init; }
    public string QueryScope { get; private init; } = "지급요청 조회 미연결";
    public IReadOnlyList<기사지급승인응답> Requests { get; private init; } = [];
    public bool HasActualTransferConfirmation => Requests.Any(IsActualTransferConfirmed);
    public bool RequiresAttention => QueryFailed || Requests.Any(x => x.StatusCode is "RetryScheduled" or "OperationalProviderNotConfigured");
    public string ActualTransferStatus => HasActualTransferConfirmation ? "실지급 확인" : "실지급 미확인";
    public string RequestStatus => QueryFailed ? "조회 실패" : !QueryCompleted ? "조회 미연결"
        : Requests.Count == 0 ? "조회월 기록 없음" : RequestStatusOf(Requests[0]);

    public static TransportPayoutPresentation Create(string requestId, 운송진행응답? transport, 기사운송료지급조회응답? query)
    {
        var completed = query?.조회완료 == true && query.조회실패 == false
            && query.조회년 is >= 2000 and <= 2100 && query.조회월 is >= 1 and <= 12;
        return new()
        {
            QueryCompleted = completed,
            QueryFailed = query?.조회실패 == true,
            QueryScope = query?.조회년 is not null && query.조회월 is not null
                ? $"{query.조회년:0000}-{query.조회월:00} 승인월 · UTC" : "지급요청 조회 미연결",
            Requests = completed ? query!.지급요청목록
                .Where(x => x is not null && transport?.Id > 0 && x.TransportId == transport.Id
                    && string.Equals(x.RequestId, requestId, StringComparison.Ordinal))
                .OrderByDescending(x => x.ApprovedAtUtc).ToArray() : []
        };
    }

    public static string RequestStatusOf(기사지급승인응답 item) => item.StatusCode switch
    {
        "SimulationVerified" => "시험 지급 검증", "Approved" => "지급요청 접수",
        "RetryScheduled" => "재확인 대기", "OperationalProviderNotConfigured" => "실지급 연결 전",
        _ => "처리 결과 미확인"
    };
    public static bool IsActualTransferConfirmed(기사지급승인응답 item)
        => item.IsActualTransferCompleted && string.Equals(item.ExecutionModeCode, "Operational", StringComparison.Ordinal);
    public static string ExecutionModeOf(기사지급승인응답 item)
        => item.ExecutionModeCode switch { "Simulation" => "시험", "Operational" => "운영", _ => "미확인" };
    public static string ExpectedAmountOf(기사지급승인응답 item)
        => string.Equals(item.CurrencyCode, "KRW", StringComparison.Ordinal)
            ? $"{item.ExpectedPayoutAmount:N0}원" : $"{item.ExpectedPayoutAmount:N0} {item.CurrencyCode}";
}
