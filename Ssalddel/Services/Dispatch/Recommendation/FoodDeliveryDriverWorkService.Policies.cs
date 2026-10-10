using Ssalddel.Contracts.Common.Metadata;

using FluentResults;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "이미 조회된 업무에 대한 활성 상한과 묶음 동선 조건을 판정한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 52,
    StepKey = "application.driver-action-policy",
    DependsOnStepKeys = ["contract.offer", "contract.active-delivery"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "이 partial의 판정은 영속 상태를 확정하지 않는다. 실제 권한·현재 Revision 재검사와 SQL 저장은 상태 전이 경계가 맡는다.")]
public sealed partial class 음식배달기사업무Service
{
    private static Result<T> 활성업무상한초과<T>(int currentActiveDeliveries, int requestedDeliveries)
        => Result.Fail<T>(new Error(음식배달기사활성업무Policy.초과안내(
                currentActiveDeliveries,
                requestedDeliveries))
            .WithMetadata("StatusCode", StatusCodes.Status409Conflict)
            .WithMetadata("ErrorCode", 음식배달기사활성업무Policy.LimitExceededErrorCode));

    private bool 묶음동선가능(IReadOnlyList<(운송원장 Queue, 음식주문 Order)> assignments)
    {
        for (var firstIndex = 0; firstIndex < assignments.Count - 1; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < assignments.Count; secondIndex++)
            {
                var first = assignments[firstIndex];
                var second = assignments[secondIndex];
                if (first.Order.조리예상완료시각Utc.HasValue
                    && second.Order.조리예상완료시각Utc.HasValue
                    && (first.Order.조리예상완료시각Utc.Value - second.Order.조리예상완료시각Utc.Value).Duration()
                    > 묶음조리완료최대차이)
                {
                    return false;
                }

                var firstPickup = CreatePoint(first.Queue.픽업_위도, first.Queue.픽업_경도);
                var secondPickup = CreatePoint(second.Queue.픽업_위도, second.Queue.픽업_경도);
                var firstDropoff = CreatePoint(first.Queue.하차_위도, first.Queue.하차_경도);
                var secondDropoff = CreatePoint(second.Queue.하차_위도, second.Queue.하차_경도);
                if (firstPickup is null || secondPickup is null || firstDropoff is null || secondDropoff is null
                    || (_routeService.CalculateDistanceKm(firstPickup, secondPickup) ?? decimal.MaxValue) > 묶음픽업최대거리Km
                    || (_routeService.CalculateDistanceKm(firstDropoff, secondDropoff) ?? decimal.MaxValue) > 묶음전달최대거리Km)
                {
                    return false;
                }
            }
        }

        return true;
    }

}
