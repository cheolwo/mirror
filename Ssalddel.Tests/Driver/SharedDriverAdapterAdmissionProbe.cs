using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Driver;

/// <summary>호출 순서·실제 transaction·오류 전달을 검사하는 adapter 시험 전용 도구입니다.</summary>
internal sealed class SharedDriverAdapterAdmissionProbe(SsalddelContext? db = null) : ISharedDriverWorkAdmissionService
{
    public bool Enabled => true;
    public bool Deny { get; set; } = true;
    public bool RequireTransaction { get; init; }
    public List<string> Calls { get; } = [];
    public IReadOnlyList<string> CandidateIds { get; private set; } = [];
    public Func<string, bool>? AllowsWorkKind { get; set; }
    public Func<운송원장, bool>? AllowsRecommendation { get; set; }

    public Task<Result> CheckRecommendationAsync(string driverId, 운송원장 queue, CancellationToken ct)
    {
        Calls.Add("Recommendation:" + queue.의뢰Id);
        if (AllowsRecommendation is not null && !AllowsRecommendation(queue))
            return Task.FromResult(Failure());
        return CheckParticipationAsync(driverId, SharedDriverWorkAdmissionService.ResolveWorkKind(queue), false, ct);
    }

    public Task<IDisposable> EnterAsync(string driverId, CancellationToken ct)
    {
        Calls.Add("Enter:" + driverId);
        return Task.FromResult<IDisposable>(new Lease(Calls));
    }

    public Task LockAsync(string driverId, CancellationToken ct)
    {
        if (RequireTransaction) Assert.NotNull(db!.Database.CurrentTransaction);
        Calls.Add("Lock:" + driverId);
        return Task.CompletedTask;
    }

    public Task<Result> CheckAsync(string driverId, IReadOnlyList<string> candidateRequestIds, CancellationToken ct)
    {
        if (RequireTransaction) Assert.NotNull(db!.Database.CurrentTransaction);
        Calls.Add("Check:" + driverId);
        CandidateIds = candidateRequestIds.ToArray();
        return Task.FromResult(Deny ? Failure() : Result.Ok());
    }

    public Task<Result> CheckParticipationAsync(string driverId, string workKind, bool requiresDirectPicking, CancellationToken ct)
    {
        Calls.Add("Participation:" + workKind);
        return Task.FromResult((AllowsWorkKind?.Invoke(workKind) ?? !Deny) ? Result.Ok() : Failure());
    }

    private static Result Failure() => Result.Fail(new Error("공통 수락 조건을 확인해 주세요.")
        .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "SharedDriverWorkProbeDenied"));
    private sealed class Lease(List<string> calls) : IDisposable
    {
        public void Dispose() => calls.Add("Exit");
    }
}
