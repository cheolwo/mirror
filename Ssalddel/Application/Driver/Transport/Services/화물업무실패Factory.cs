using FluentResults;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Application.Driver.Transport;

/// <summary>상태를 바꾸기 전에 확정된 화물 업무 차단만 구조화한다. 저장/전송 예외를 미적용으로 추측하지 않는다.</summary>
internal static class 화물업무실패Factory
{
    public static Error 생성(string code, string message, string stageId, int statusCode = 400,
        string? responsibilityRoleCode = null)
        => new Error(message)
            .WithMetadata("StatusCode", statusCode)
            .WithMetadata("ErrorCode", code)
            .WithMetadata("FailureClassCode", statusCode >= 500
                ? 업무실패분류Codes.일시기술장애 : 업무실패분류Codes.업무규칙차단)
            .WithMetadata("ResponsibilityRoleCode", responsibilityRoleCode ?? OperatingSystemIds.DomesticCargoTransport)
            .WithMetadata("RequiresStateRefresh", true)
            .WithMetadata("RetryPolicyCode", 업무재시도정책Codes.상태재조회후사용자재시도)
            .WithMetadata("AvailableRecoveryActions", new[] { 업무복구행동Ids.상태전체재조회 })
            .WithMetadata("WorkflowContext", new 업무실패맥락Dto(
                OperatingSystemIds.DomesticCargoTransport, stageId, 업무처리결과Codes.미적용));

    public static string 목표단계(string state) => state switch
    {
        "상차지도착" or "상차완료" => OperatingSystemLifecycleStageIds.CargoPickup,
        "하차지도착" or "인수완료" => OperatingSystemLifecycleStageIds.CargoDropoff,
        _ => OperatingSystemLifecycleStageIds.CargoTransport
    };
}
