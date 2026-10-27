using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using 살뜰.Services.Dispatch.AI;

namespace 살뜰.Services.Dispatch.Queue;

public sealed class 음식배달배차업무정책 : I배차업무정책
{
    private readonly 음식배달후보준비Service _후보준비;
    private readonly IFoodDispatchCandidateAI? _candidateAI;

    public 음식배달배차업무정책(
        I국내화물운송기사상태Store 기사상태Store,
        IDriverRejectedRequestStore 거절Store,
        I배차추천경로Service 경로Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        ISsalddelFoodOrderStore 음식주문Store,
        IOptions<배차큐정책Options> options,
        I운영배차공통UseCase 수신의사,
        IAgreedRateTableUseCase? agreedRates = null,
        I기사권역복귀Service? 권역복귀 = null,
        I음식권역복귀일정평가Service? 음식일정 = null,
        I음식배달수요참여Service? 수요참여 = null,
        IFoodDispatchCandidateAI? candidateAI = null,
        ISharedDriverWorkAdmissionService? sharedAdmission = null)
    {
        var settings = options.Value;
        _candidateAI = candidateAI;
        var 평가 = new 음식배달후보평가Service(경로Service, settings, 권역복귀, 음식일정, sharedAdmission);
        _후보준비 = new 음식배달후보준비Service(기사상태Store, 거절Store, 음식배달권실행공간Store,
            음식주문Store, settings, 수신의사, agreedRates, 권역복귀, 음식일정, 수요참여, 평가);
    }

    public int 배차업무유형 => 상태값.배차업무유형.음식배달;

    public async Task<배차추천후보?> 다음후보선정Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        var review = await 후보검토Async(queue, 제외기사Id, cancellationToken);
        if (_candidateAI?.Enabled != true || review.선정후보 is null)
            return review.선정후보;

        FoodDispatchAISelection decision;
        try
        {
            decision = await _candidateAI.SelectAsync(review, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            decision = new(null, "Unavailable");
        }
        cancellationToken.ThrowIfCancellationRequested();

        // 모델 응답을 기다린 동안 수신·위치·거절·수요가 바뀔 수 있다. 모델을 다시 부르지 않고 관문만 재실행한다.
        // 이 재조회 자체가 실패하면 기존 저장소 오류를 전파하며 오래된 후보로 숨기지 않는다.
        var current = await 후보검토Async(queue, 제외기사Id, cancellationToken);
        if (current.선정후보 is null) return null;
        var initiallyAllowed = FoodDispatchCandidateConstraints.Eligible(review);
        var currentlyAllowed = FoodDispatchCandidateConstraints.Eligible(current);
        var selected = decision.StatusCode == "Applied"
            && initiallyAllowed.Any(x => string.Equals(x.기사Id, decision.DriverId, StringComparison.Ordinal))
            ? currentlyAllowed.FirstOrDefault(x => string.Equals(x.기사Id, decision.DriverId, StringComparison.Ordinal))
            : null;
        if (selected is null)
        {
            var status = decision.StatusCode switch
            {
                "Disabled" or "InvalidConfiguration" or "NoCandidates" or "SingleCandidate" or "HttpFailure"
                    or "ResponseTooLarge" or "InvalidResponse" or "Timeout" or "NetworkFailure" or "Unavailable"
                    => decision.StatusCode,
                _ => "CandidateChanged"
            };
            return current.선정후보 with { 추천사유 = $"{current.선정후보.추천사유} · FoodDispatchAI.{status}" };
        }

        if (string.Equals(current.선정후보.DriverId, selected.기사Id, StringComparison.Ordinal))
            return current.선정후보 with { 추천사유 = $"{current.선정후보.추천사유} · FoodDispatchAI.Applied" };
        return new(selected.기사Id, selected.추천점수!.Value,
            $"{selected.공간설명} · 음식점까지 {selected.거리Km:0.0}km · 픽업 단계 {selected.픽업단계}"
            + $" · 도착 차이 {selected.도착차이분:0.0}분 · F드라이버 대기 보정 {selected.대기점수:0} · FoodDispatchAI.Applied");
    }

    /// <summary>기존 후보 관문과 정렬을 실행하고 실제 확인 범위만 반환한다. 추천 확정은 수행하지 않는다.</summary>
    public Task<음식배달후보검토결과> 후보검토Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
        => _후보준비.준비Async(queue, 제외기사Id, cancellationToken);
}
