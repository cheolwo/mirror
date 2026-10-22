using FluentResults;
using Microsoft.AspNetCore.Http;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Services.LogisticsProcessing.Warehouse;

namespace Ssalddel.Application.Warehouse;

public interface I가입고사진신청UseCase
{
    Task<Result<가입고사진신청응답>> CreateAsync(Guid requestId, string? name, IFormFile? photo, CancellationToken ct);
    Task<Result<가입고사진신청목록응답>> GetListAsync(int page, int pageSize, CancellationToken ct);
    Task<Result<가입고사진신청응답>> GetAsync(Guid id, CancellationToken ct);
    Task<Result<가입고사진내용응답>> GetPhotoAsync(Guid id, CancellationToken ct);
    Task<Result<가입고사진신청응답>> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct);
}

/// <summary>가 입고 준비의 짧은 안내와 기존 입고 단계의 복구 행동을 연결한다.</summary>
public sealed class 가입고사진신청UseCase(I가입고사진신청Service service) : I가입고사진신청UseCase
{
    public Task<Result<가입고사진신청응답>> CreateAsync(Guid requestId, string? name, IFormFile? photo, CancellationToken ct)
        => Execute(() => service.CreateAsync(requestId, name, photo, ct), mutation: true);
    public Task<Result<가입고사진신청목록응답>> GetListAsync(int page, int pageSize, CancellationToken ct)
        => Execute(() => service.GetListAsync(page, pageSize, ct), mutation: false);
    public Task<Result<가입고사진신청응답>> GetAsync(Guid id, CancellationToken ct)
        => Execute(async () => await service.GetAsync(id, ct) ?? throw Missing(), mutation: false);
    public Task<Result<가입고사진내용응답>> GetPhotoAsync(Guid id, CancellationToken ct)
        => Execute(async () => await service.GetPhotoAsync(id, ct) ?? throw Missing(), mutation: false);
    public Task<Result<가입고사진신청응답>> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct)
        => Execute(() => service.RenameAsync(id, request, ct), mutation: true);

    private static 가입고사진신청Exception Missing() => new("PhotoInboundNotFound", "신청 결과가 아직 확인되지 않습니다.", 404);
    private static async Task<Result<T>> Execute<T>(Func<Task<T>> work, bool mutation)
    {
        try { return Result.Ok(await work()); }
        catch (가입고사진신청Exception ex)
        {
            var outcome = !mutation ? 업무처리결과Codes.조회
                : ex.DefinitivelyNotApplied ? 업무처리결과Codes.미적용
                : ex.StatusCode is >= 500 or 409 ? 업무처리결과Codes.미확정 : 업무처리결과Codes.미적용;
            return Failure<T>(ex.Message, ex.Code, ex.StatusCode, outcome);
        }
        catch (Exception)
        {
            // 저장 뒤 응답을 잃었을 수도 있으므로 자동 재전송을 허용하지 않는다.
            return Failure<T>(mutation ? "신청 결과를 확인해 주세요." : "신청을 다시 확인해 주세요.",
                mutation ? "PhotoInboundOutcomeUnknown" : "PhotoInboundLookupUnavailable", 503,
                mutation ? 업무처리결과Codes.미확정 : 업무처리결과Codes.조회);
        }
    }
    private static Result<T> Failure<T>(string message, string code, int status, string outcome)
        => Result.Fail<T>(new Error(message)
            .WithMetadata("StatusCode", status).WithMetadata("ErrorCode", code)
            .WithMetadata("WorkflowContext", new 업무실패맥락Dto(OperatingSystemIds.WarehouseCommerceFulfillment,
                OperatingSystemLifecycleStageIds.WarehouseInboundPlan, outcome))
            .WithMetadata("RequiresStateRefresh", true)
            .WithMetadata("RetryPolicyCode", 업무재시도정책Codes.자동재시도안함)
            .WithMetadata("AvailableRecoveryActions", new[] { 업무복구행동Ids.상태전체재조회 }));
}
