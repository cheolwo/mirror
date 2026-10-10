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
    "현재 기사에게 유효한 제안과 수행 업무를 조회한다.",
    ContractType = typeof(I음식배달기사업무Service),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall,
    FlowOrder = 55,
    StepKey = "application.driver-offer-query",
    DependsOnStepKeys = ["contract.offer", "contract.active-delivery"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "SQL 전문 상태는 조회만 한다. 공통 수신 상태 조회는 영속 원장 기준 Redis 투영을 재구성할 수 있으며 수신 의사 자체를 바꾸지 않는다.")]
public sealed partial class 음식배달기사업무Service
{
    public async Task<IReadOnlyList<DriverWorkOfferDto>> 제안조회Async(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var receiving = 살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(
            await _수신의사.수신상태조회Async(driverId, cancellationToken));
        var martSourceTypes = new[]
        {
            운송의뢰배차원천유형.살뜰마트주문.ToUpperInvariant(),
            운송의뢰배차원천유형.살뜰마트음식주문.ToUpperInvariant(),
            운송의뢰배차원천유형.살뜰마트포장완료주문.ToUpperInvariant()
        };
        var queues = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                        && (x.원본의뢰유형 == null || !Enumerable.Contains(martSourceTypes, x.원본의뢰유형.ToUpper()))
                        && x.상태 != 상태값.배차상태.인수완료
                        && ((receiving && x.상태 == 상태값.배차대기상태.대기
                             && x.배차큐단계 == 상태값.배차큐단계.배차추천
                             && x.배차노출상태 == 상태값.배차노출상태.추천중
                             && x.현재추천대상기사Id == driverId
                             && x.추천만료시각.HasValue
                             && x.추천만료시각 > now)
                            || (x.확정기사Id == driverId
                                && x.배차큐단계 == 상태값.배차큐단계.확정)))
            .OrderBy(x => x.추천만료시각)
            .ThenBy(x => x.CreatedAt)
            .Take(12)
            .ToListAsync(cancellationToken);
        if (queues.Count == 0)
        {
            return [];
        }

        // 마트 원천은 Take 전에 제외해 정상 음식 제안 12칸을 소모하지 않습니다.
        var orderNos = queues
            .Select(ResolveOrderNo)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var orders = await _db.음식주문
            .AsNoTracking()
            .Include(x => x.상품목록)
            .Where(x => Enumerable.Contains(orderNos, x.주문번호))
            .ToDictionaryAsync(x => x.주문번호, StringComparer.Ordinal, cancellationToken);

        _locationStore.TryGetLatest(driverId, out var driverLocation);
        return queues
            .Where(queue => orders.ContainsKey(ResolveOrderNo(queue)))
            .Select(queue =>
            {
                var offer = ToOffer(queue, orders[ResolveOrderNo(queue)], driverLocation);
                return offer.Status == DriverWorkOfferStatus.Recommended || _experience is null ? offer
                    : offer with { ExperienceSummaries = _experience.기사현재업무요약(driverId, ResolveOrderNo(queue)) };
            })
            .ToArray();
    }

    private DriverWorkOfferDto ToOffer(
        운송원장 queue,
        음식주문 order,
        DriverLocationSnapshot? driverLocation)
    {
        var pickup = CreatePoint(queue.픽업_위도, queue.픽업_경도);
        var dropoff = CreatePoint(queue.하차_위도, queue.하차_경도);
        var deliveryDistance = pickup is not null && dropoff is not null
            ? _routeService.CalculateDistanceKm(pickup, dropoff)
            : null;
        var pickupDistance = driverLocation is not null && pickup is not null
            ? _routeService.CalculateDistanceKm(
                new 배차경로좌표(driverLocation.Latitude, driverLocation.Longitude),
                pickup)
            : null;
        var status = ResolveOfferStatus(queue, order);
        var menu = order.상품목록.Count == 0
            ? "음식 주문"
            : string.Join(", ", order.상품목록.Take(2).Select(x => $"{x.상품명} {x.수량}개"));
        var reason = pickupDistance.HasValue
            ? $"음식점까지 {pickupDistance.Value:0.0}km · 픽업 준비 {FormatReadyTime(order.조리예상완료시각Utc)}"
            : $"픽업 준비 {FormatReadyTime(order.조리예상완료시각Utc)}";
        var isRecommended = status == DriverWorkOfferStatus.Recommended;
        var dropoffAddress = isRecommended
            ? ToApproximateAddress(queue.하차_도로명주소)
            : JoinAddress(queue.하차_도로명주소, queue.하차_상세주소);
        var dropoffLatitude = isRecommended
            ? ToApproximateCoordinate(queue.하차_위도)
            : queue.하차_위도;
        var dropoffLongitude = isRecommended
            ? ToApproximateCoordinate(queue.하차_경도)
            : queue.하차_경도;

        return new DriverWorkOfferDto(
            queue.의뢰Id,
            기사앱식별자.FoodDeliveryDriverApp,
            기사도메인구분.음식배달,
            기사업무유형코드.음식배달,
            menu,
            $"{order.음식점명} 픽업 · {dropoffAddress} 전달",
            new DriverWorkStopDto(
                string.IsNullOrWhiteSpace(order.음식점명) ? "음식점" : order.음식점명,
                JoinAddress(queue.픽업_도로명주소, queue.픽업_상세주소),
                (double)(queue.픽업_위도 ?? 0m),
                (double)(queue.픽업_경도 ?? 0m),
                ToOffset(order.조리예상완료시각Utc)),
            new DriverWorkStopDto(
                isRecommended ? "고객 전달 권역" : "고객 주소",
                dropoffAddress,
                (double)(dropoffLatitude ?? 0m),
                (double)(dropoffLongitude ?? 0m),
                ToOffset(order.조리예상완료시각Utc?.AddMinutes(42))),
            queue.기사지급예정액 ?? CalculateDriverPayout(deliveryDistance),
            deliveryDistance.HasValue ? (double)deliveryDistance.Value : null,
            reason,
            status,
            ToOffset(queue.추천만료시각),
            [order.주문번호],
            운송실행프로필Factory.Create(queue),
            isRecommended
                ? null
                : new DriverWorkRecipientDto(
                    order.수령인명,
                    order.수령인연락처,
                    order.수령요청사항,
                    order.주문자본인수령여부),
            queue.기사기상할증액 ?? 0m,
            queue.기사기상할증적용여부,
            queue.기사한시수요할증액 ?? 0m,
            queue.기사한시수요할증적용여부,
            queue.기사제안요금정책판본,
            ToOffset(queue.픽업지기상기준시각Utc));
    }

    private static string ResolveOfferStatus(운송원장 queue, 음식주문 order)
    {
        var foodState = 음식주문상태코드.Normalize(order.상태);
        if (foodState is 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
        {
            return DriverWorkOfferStatus.Completed;
        }

        if (foodState == 음식주문상태코드.픽업완료)
        {
            return DriverWorkOfferStatus.MovingToDropoff;
        }

        return queue.배차큐단계 == 상태값.배차큐단계.확정
            ? DriverWorkOfferStatus.MovingToPickup
            : DriverWorkOfferStatus.Recommended;
    }

    private static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue
            ? new 배차경로좌표(latitude.Value, longitude.Value)
            : null;

    private static decimal CalculateDriverPayout(decimal? deliveryDistanceKm)
    {
        const decimal minimumPayout = 2500m;
        const decimal includedDistanceKm = 1m;
        const decimal perAdditionalKm = 900m;
        var distance = Math.Max(0m, deliveryDistanceKm ?? 0m);
        return Math.Max(minimumPayout, minimumPayout + Math.Max(0m, distance - includedDistanceKm) * perAdditionalKm);
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;

    private static string FormatReadyTime(DateTime? value)
        => value.HasValue ? $"{value.Value.ToLocalTime():HH:mm}" : "시간 미정";

    private static string JoinAddress(string primary, string detail)
        => string.IsNullOrWhiteSpace(detail) ? primary : $"{primary} {detail}";

    internal static string ToApproximateAddress(string address)
    {
        var parts = address.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nonNumericParts = parts
            .Where(part => !part.Any(char.IsDigit))
            .Take(2)
            .ToArray();
        return nonNumericParts.Length switch
        {
            0 => "상세 위치는 수락 후 공개",
            1 => $"{nonNumericParts[0]} 인근",
            _ => $"{nonNumericParts[0]} {nonNumericParts[1]} 인근"
        };
    }

    internal static decimal? ToApproximateCoordinate(decimal? coordinate)
        => coordinate.HasValue
            ? Math.Round(coordinate.Value, 2, MidpointRounding.AwayFromZero)
            : null;

}
