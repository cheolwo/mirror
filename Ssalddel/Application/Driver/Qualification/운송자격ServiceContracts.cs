using FluentResults;
using Ssalddel.Contracts.Driver.Profile;

namespace Ssalddel.Application.Driver.Qualification;

public interface I기사운송자격Service
{
    Task<운송자격평가결과> 평가Async(string driverId, string? vehicleKind, CancellationToken ct = default);
}

public interface I운송자격UseCase : I기사운송자격Service
{
    Task<Result<운송자격상세Dto>> 본인조회Async(string? actorId, CancellationToken ct = default);
    Task<Result<운송자격상세Dto>> 제출Async(string? actorId, 운송자격제출요청 request, CancellationToken ct = default);
    Task<Result<운송자격상세Dto>> 관리자조회Async(string driverId, CancellationToken ct = default);
    Task<Result<운송자격상세Dto>> 검토Async(string? reviewerId, string driverId, 운송자격검토요청 request, CancellationToken ct = default);
}

public sealed class 운송자격Options
{
    public const string SectionName = "TransportQualification";
    /// <summary>Simulation 모드의 명시적 내부 시험에서만 허용. 자격 승인으로 저장하지 않는다.</summary>
    public bool AllowSimulationBypass { get; set; }
}
