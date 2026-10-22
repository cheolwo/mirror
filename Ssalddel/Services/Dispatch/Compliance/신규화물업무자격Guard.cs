using Ssalddel.Application.Driver.Qualification;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Operations;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Dispatch.Compliance;

/// <summary>신규 업무만 제한한다. 기존 운송의 조회·인계·완료를 차단하지 않는다.</summary>
public interface I신규화물업무자격Guard
{
    bool RequiresCargoEvidence => true;
    Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default);
}

public sealed class 신규화물업무자격Guard(
    I기사운송자격Service qualification,
    I화물플랫폼등록준비Service platform,
    ISsalddelExecutionModePolicy execution) : I신규화물업무자격Guard
{
    public bool RequiresCargoEvidence => !execution.IsSimulation;

    public async Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
    {
        var result = await qualification.평가Async(driverId, null, ct);
        if (!result.CanReceiveNewWork) return result;
        if (execution.IsSimulation) return result;
        if (!execution.IsOperational || result.IsSimulationBypass)
            return new(false, "Qualification.ExecutionModeInvalid", "운영 자격 확인 상태를 다시 확인해 주세요.", result.Status, result.Revision);

        var readiness = platform.조회();
        // 등록증·요금/약관·내부 준비를 확인해도 별도 기능/운영 경계는 열지 않는다.
        // 실제 영업 허용 여부를 AI 또는 준비도 점수로 대신하지 않는 내부 출시 관문이다.
        if (!readiness.FreeConnectionConfigured || !readiness.IsPreparationComplete || !readiness.ActualRegistrationConfirmed
            || !readiness.RequiredAuthorityProceduresConfirmed)
            return new(false, "FreightPlatform.ReadinessRequired",
                "플랫폼 등록과 무료 연결 운영 준비를 확인 중입니다. 현재 운송은 계속 확인할 수 있습니다.", result.Status, result.Revision);
        return result;
    }
}

public static class 신규화물업무자격검사
{
    public static bool 화물정보완료(I신규화물업무자격Guard? guard, 살뜰.도메인.화주.화주운송의뢰 request)
        => guard is not null && (!guard.RequiresCargoEvidence || FreightCargoRegistrationPolicy.MissingFields(
            new(request.화물종류, request.화물중량Kg, request.화물길이Mm, request.화물폭Mm, request.화물높이Mm,
                request.픽업_도로명주소, request.하차_도로명주소)).Count == 0);

    public static Task<운송자격평가결과> 평가Async(I신규화물업무자격Guard? guard,
        string driverId, CancellationToken ct = default)
        => guard is not null ? guard.평가Async(driverId, ct)
            : Task.FromResult(new 운송자격평가결과(false, "Qualification.GuardNotConfigured",
                "운송 자격 확인 연결을 준비 중입니다. 현재 운송은 계속 확인할 수 있습니다.", 운송자격Codes.NotSubmitted));
}
