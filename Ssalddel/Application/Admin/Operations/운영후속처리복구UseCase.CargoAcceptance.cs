using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Application.Admin.Operations;

public sealed partial class 운영후속처리복구UseCase
{
    private async Task<IReadOnlyList<운영후속처리복구항목Dto>> 화물수락후속목록Async(bool includeCompleted,
        int take, DateTime now, CancellationToken ct)
    {
        var query = db.운송이벤트.AsNoTracking().Where(x => x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType);
        if (!includeCompleted) query = query.Where(x => !x.메타데이터.Contains("\"state\":\"Done\""));
        var rows = await query.OrderByDescending(x => x.메타데이터.Contains("\"state\":\"Failed\"")
                || x.메타데이터.Contains("\"state\":\"Blocked\""))
            .ThenByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
        return rows.Select(row => (Row: row, Job: CargoAcceptanceRecoveryMarker.Read(row.메타데이터)))
            .Where(x => x.Job is not null).Select(x =>
            {
                var job = x.Job!; var row = x.Row;
                var failed = job.State is "Failed" or "Blocked";
                var retryable = job.State == "Failed" && 화물수락후속복구 is not null;
                var processing = job.State == "Processing" && job.LeaseUntilUtc > now;
                var pending = job.State is "Pending" or "Retry" || job.State == "Processing" && !processing;
                var summary = job.State switch
                {
                    "Done" => "현재 설정에서 필요한 화물 수락 후속 준비를 마쳤습니다. 실제 알림 수신·배송·지급 완료와는 별개입니다.",
                    "Blocked" => "화물의 현재 담당자 또는 수락 상태가 바뀌어 이전 후속 처리를 보류했습니다. 현재 운송을 먼저 확인해 주세요.",
                    "Failed" => "화물 수락 후속 처리가 반복 실패했습니다. 원인을 확인한 뒤 같은 수락을 안전하게 재예약할 수 있습니다.",
                    "Processing" when processing => "화물 수락의 시간약속·알림 의도를 준비 중입니다.",
                    _ => "화물 수락의 시간약속·알림 의도가 대기 중입니다. 자동 복구가 꺼져 있으면 재조회 또는 운영 점검이 필요합니다."
                };
                return new 운영후속처리복구항목Dto
                {
                    복구StableId = BuildStableId(운영후속처리복구원천Codes.화물수락후속, row.Id),
                    원천Code = 운영후속처리복구원천Codes.화물수락후속, 원천항목Id = row.Id,
                    업무유형Code = "CargoDispatchAcceptanceFollowups",
                    원천상태Code = job.State is "Pending" or "Retry" or "Processing" or "Failed" or "Blocked" or "Done" ? job.State : "Unknown",
                    원천판본 = job.Revision,
                    상태Code = job.State == "Done" ? 운영후속처리복구상태Codes.정상완료
                        : processing ? 운영후속처리복구상태Codes.처리중
                        : pending ? 운영후속처리복구상태Codes.자동재시도대기 : 운영후속처리복구상태Codes.운영자확인필요,
                    현재책임운영체제Id = OperatingSystemIds.PlatformOperations,
                    처리시도수 = job.Attempts, 최대자동시도수 = Math.Clamp(화물수락후속Options?.Value.MaximumAttempts ?? 5, 1, 10),
                    CreatedAtUtc = row.이벤트시각, UpdatedAtUtc = job.UpdatedAtUtc ?? row.이벤트시각,
                    다음처리예정시각Utc = job.State == "Processing" ? job.LeaseUntilUtc : pending ? job.NextAttemptAtUtc : null,
                    운영자확인필요 = failed || !pending && !processing && job.State != "Done",
                    재시도예약가능 = retryable,
                    실패분류Code = failed ? 업무실패분류Codes.최종수동검토 : null,
                    재시도정책Code = retryable ? 업무재시도정책Codes.운영자안전재시도 : 업무재시도정책Codes.자동재시도안함,
                    상태전체재조회필요 = true,
                    복구가능행동 = retryable ? [업무복구행동Ids.운영자안전재시도예약] : [],
                    안전요약 = summary + 안전사유(job.ReasonCode)
                };
            }).ToArray();
    }

    private async Task<운영후속처리재시도응답Dto?> 화물수락후속재시도Async(long id,
        운영후속처리재시도요청Dto request, CancellationToken ct)
    {
        if (request.예상원천판본 is not { } revision || revision < 0)
            throw new InvalidOperationException("현재 후속 처리 판본을 다시 조회해 주세요.");
        if (화물수락후속복구 is null) throw new InvalidOperationException("화물 수락 후속 복구 서비스가 연결되지 않았습니다.");
        if (!await 화물수락후속복구.RetryAsync(id, request.예상처리시도수, revision, ct))
            throw new InvalidOperationException("후속 처리 또는 현재 운송이 변경되었습니다. 최신 상태를 확인해 주세요.");
        var row = await db.운송이벤트.AsNoTracking().SingleAsync(x => x.Id == id && x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType, ct);
        var job = CargoAcceptanceRecoveryMarker.Read(row.메타데이터)!;
        return new()
        {
            복구StableId = BuildStableId(운영후속처리복구원천Codes.화물수락후속, id),
            상태Code = 운영후속처리복구상태Codes.자동재시도대기, 처리시도수 = job.Attempts, 원천판본 = job.Revision,
            재시도예약시각Utc = job.NextAttemptAtUtc ?? job.UpdatedAtUtc ?? row.이벤트시각
        };
    }

    private static string 안전사유(string code) => code switch
    {
        "AcceptancePromisePreparationFailed" => " 시간약속 근거를 확인해야 합니다.",
        "AcceptanceNotificationPreparationFailed" => " 화주 알림 의도 저장을 확인해야 합니다.",
        "AcceptanceRecoveryAttemptsExhausted" => " 자동 재시도 횟수에 도달했습니다.",
        "AcceptanceGenerationChanged" => " 이전 수락의 후속을 현재 운송에 적용하지 않습니다.",
        _ => ""
    };
}
