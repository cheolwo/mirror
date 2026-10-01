using 홍달.도메인.배차;
using 홍달.Services.Dispatch.Queue;
using Hongdal.Contracts.Common.Versioning;

namespace 홍달.Services.Dispatch.Engine;

public abstract class 정책기반배차엔진 : I운송의뢰배차엔진
{
    private readonly IReadOnlyDictionary<int, I배차업무정책> _policies;

    protected 정책기반배차엔진(IEnumerable<I배차업무정책> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        var policyGroups = policies
            .GroupBy(x => x.배차업무유형)
            .ToArray();
        var duplicatePolicyTypes = policyGroups
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .OrderBy(x => x)
            .ToArray();
        if (duplicatePolicyTypes.Length > 0)
        {
            throw new InvalidOperationException(
                $"동일 배차업무유형의 배차 정책이 중복 등록되었습니다. Types={string.Join(',', duplicatePolicyTypes)}");
        }

        _policies = policyGroups.ToDictionary(x => x.Key, x => x.Single());
    }

    public virtual string 논리엔진코드 => EngineFamilyIds.TransportRequestDispatch;

    public abstract string 엔진코드 { get; }

    public abstract string 표시명 { get; }

    public abstract int 배차업무유형 { get; }

    public virtual async Task<배차추천후보선정결과> 다음후보선정Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        if (queue is null)
        {
            return 배차추천후보선정결과.잘못된입력("배차대기 원장이 제공되지 않았습니다.");
        }

        if (queue.배차업무유형 != 배차업무유형)
        {
            return 배차추천후보선정결과.잘못된입력(
                $"엔진의 배차업무유형과 원장의 배차업무유형이 일치하지 않습니다. EngineType={배차업무유형}, QueueType={queue.배차업무유형}");
        }

        if (!queue.픽업_위도.HasValue || !queue.픽업_경도.HasValue)
        {
            return 배차추천후보선정결과.준비안됨("픽업 좌표가 준비되지 않아 배차 후보를 선정할 수 없습니다.");
        }

        if (!_policies.TryGetValue(배차업무유형, out var policy))
        {
            return 배차추천후보선정결과.구성오류(
                $"배차 엔진에 필요한 업무 정책이 등록되지 않았습니다. Engine={엔진코드}, Type={배차업무유형}");
        }

        var candidate = await policy.다음후보선정Async(queue, 제외기사Id, cancellationToken);
        return candidate is null
            ? 배차추천후보선정결과.적격후보없음("현재 조건을 충족하는 배차 추천 후보가 없습니다.")
            : 배차추천후보선정결과.선정됨(candidate);
    }
}
