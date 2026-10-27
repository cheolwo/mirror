using System.Text.Json.Nodes;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.운송;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace Ssalddel.DispatchTraining;

/// <summary>합성 경로 provider와 실제 일정 계산기를 연결해 검사한다. 모델·DB·외부 API·실제 주행을 실행하지 않는다.</summary>
public static class DispatchTrainingFixtureSelfChecks
{
    public static async Task<IReadOnlyList<DispatchTrainingSelfCheckResult>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<DispatchTrainingSelfCheckResult>();
        var routes = new DispatchTrainingFixtureRoutes();
        배차경로좌표 origin = new(37.58m, 127.08m);
        배차경로좌표 pickup = new(37.584m, 127.08m);
        배차경로좌표 dropoff = new(37.584m, 127.087m);
        배차경로좌표 anchor = new(37.581m, 127.085m);

        await Check("fixture-all-addresses-stay-within-synthetic-grid", async () =>
        {
            var geocoding = new DispatchTrainingGeocoding();
            for (var index = 1; index <= 99; index++)
            foreach (var stage in new[] { "픽업", "전달" })
            {
                var point = await geocoding.GeocodeAsync($"가상 {stage} {index:00}");
                Require(point.HasValue && routes.CalculateDistanceKm(origin, new(point.Value.lat, point.Value.lng)) is >= 0m);
            }
            Require(await geocoding.GeocodeAsync("실제 주소") is null);
        });
        await Check("fixture-leg-is-deterministic-and-explicitly-synthetic", async () =>
        {
            var first = await routes.EstimateRouteAsync(origin, dropoff);
            var second = await routes.EstimateRouteAsync(origin, dropoff);
            var distance = StraightLineDeliveryPricingPolicy.CalculateDistanceKm(origin.Latitude, origin.Longitude, dropoff.Latitude, dropoff.Longitude);
            Require(first is not null && first == second && first.DistanceKm == distance && first.TollFare == 0m);
            Require(first!.Duration!.Value.Ticks == decimal.ToInt64(decimal.Round(distance / 20m * TimeSpan.TicksPerHour, 0, MidpointRounding.AwayFromZero)));
            Require(first.계산방식 == DispatchTrainingFixtureRoutes.Source + ".Speed20Kph"
                && first.RouteOption == "DispatchTrainingSyntheticOnly");
            // true는 제품 일정 서비스가 요구하는 완전한 mock leg다. 현실 경로/ETA 확인을 주장하지 않는다.
            Require(first.실제경로여부 && DispatchTrainingFixtureRoutes.Source.StartsWith("DispatchTrainingSynthetic.", StringComparison.Ordinal));
        });
        await Check("fixture-same-point-has-zero-distance-and-duration", async () =>
        {
            var route = await routes.EstimateRouteAsync(origin, origin);
            Require(route is { DistanceKm: 0m, Duration: not null } && route.Duration == TimeSpan.Zero);
        });
        await Check("fixture-ordered-route-sums-every-leg-in-declared-order", async () =>
        {
            var first = (await routes.EstimateRouteAsync(origin, pickup))!;
            var second = (await routes.EstimateRouteAsync(pickup, dropoff))!;
            var third = (await routes.EstimateRouteAsync(dropoff, anchor))!;
            var ordered = await routes.EstimateOrderedRouteAsync(origin, [pickup, dropoff, anchor], ct);
            Require(ordered?.DistanceKm == first.DistanceKm + second.DistanceKm + third.DistanceKm);
            Require(ordered!.Duration!.Value.Ticks == first.Duration!.Value.Ticks + second.Duration!.Value.Ticks + third.Duration!.Value.Ticks);
            var reordered = await routes.EstimateOrderedRouteAsync(origin, [dropoff, pickup, anchor], ct);
            Require(reordered?.DistanceKm != ordered.DistanceKm);
        });
        await Check("fixture-insertion-agrees-with-ordered-legs", async () =>
        {
            foreach (var existingEnd in new 배차경로좌표?[] { null, anchor })
            {
                var inserted = await routes.EstimateInsertionDelayAsync(origin, existingEnd, pickup, dropoff);
                var ordered = await routes.EstimateOrderedRouteAsync(origin,
                    existingEnd is null ? [pickup, dropoff] : [pickup, existingEnd, dropoff], ct);
                var baseline = (await routes.EstimateRouteAsync(origin, existingEnd ?? dropoff))!;
                Require(inserted is not null && inserted.삽입경로거리Km == ordered!.DistanceKm
                    && inserted.삽입경로소요시간분 == (decimal)ordered.Duration!.Value.TotalMinutes
                    && inserted.기존경로거리Km == baseline.DistanceKm
                    && inserted.기존배송지연분 == Math.Max(0m, inserted.삽입경로소요시간분!.Value - inserted.기존경로소요시간분!.Value));
            }
        });
        await Check("fixture-missing-and-outside-points-are-not-routes", async () =>
        {
            배차경로좌표 outside = new(37.6m, 127.08m);
            Require(await routes.EstimateRouteAsync(null, pickup) is null);
            Require(await routes.EstimateRouteAsync(origin, null) is null);
            Require(await routes.EstimateRouteAsync(origin, outside) is null);
            Require(routes.CalculateDistanceKm(origin, outside) is null);
            Require(await routes.EstimateOrderedRouteAsync(null, [pickup], ct) is null);
            Require(await routes.EstimateOrderedRouteAsync(origin, [], ct) is null);
            Require(await routes.EstimateOrderedRouteAsync(origin, [pickup, outside, dropoff], ct) is null);
            Require(await routes.EstimateInsertionDelayAsync(origin, outside, pickup, dropoff) is null);
        });
        await Check("fixture-origin-and-anchor-use-only-present-grid-location", async () =>
        {
            DriverLocationSnapshot Location(decimal latitude) => new("synthetic-driver", latitude, 127.08m, 5m, "운행중", DateTime.UtcNow, DateTime.UtcNow);
            Require(await routes.ResolveOriginLocationAsync("synthetic-driver", null, null, null) is null);
            Require(await routes.ResolveRouteAnchorLocationAsync("synthetic-driver", null, Location(38m)) is null);
            Require(await routes.ResolveOriginLocationAsync("synthetic-driver", null, Location(origin.Latitude), null) == origin);
        });
        await Check("fixture-speed-is-explicit-and-bounded", async () =>
        {
            foreach (var invalid in new[] { 0m, -1m, 0.01m, 121m, decimal.MaxValue })
            {
                try { _ = new DispatchTrainingFixtureRoutes(invalid); throw new InvalidOperationException("Invalid speed accepted."); }
                catch (ArgumentOutOfRangeException) { }
            }
            var slower = (await routes.EstimateRouteAsync(origin, dropoff))!;
            var faster = (await new DispatchTrainingFixtureRoutes(40m).EstimateRouteAsync(origin, dropoff))!;
            Require(slower.DistanceKm == faster.DistanceKm && Math.Abs(slower.Duration!.Value.Ticks - 2 * faster.Duration!.Value.Ticks) <= 1);
        });
        await Check("fixture-cancellation-precedes-route-and-ordered-work", async () =>
        {
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await RejectCanceled(() => routes.EstimateRouteAsync(origin, dropoff, canceled.Token));
            await RejectCanceled(() => routes.EstimateOrderedRouteAsync(origin, [pickup, dropoff], canceled.Token));
        });
        await Check("fixture-real-schedule-service-accepts-feasible-and-rejects-late-window", async () =>
        {
            var at = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
            var service = new 운송일정삽입평가Service(routes);
            var plan = new 기사운송일정계획("synthetic-driver", at, origin, []);
            var order = new 화주운송의뢰
            {
                의뢰Id = "fixture-schedule", 픽업_위도 = pickup.Latitude, 픽업_경도 = pickup.Longitude,
                하차_위도 = dropoff.Latitude, 하차_경도 = dropoff.Longitude,
                픽업_시간창_시작일시 = at.AddMinutes(-1), 픽업_시간창_종료일시 = at.AddHours(1),
                하차_시간창_시작일시 = at.AddMinutes(-1), 하차_시간창_종료일시 = at.AddHours(2)
            };
            var accepted = await service.평가Async(plan, order, ct);
            Require(accepted.삽입가능여부 && accepted.전체완수가능여부 && accepted.후보포함실제경로확인됨
                && accepted.복귀비교경로옵션 == DispatchTrainingFixtureRoutes.RouteOption);
            order.하차_시간창_종료일시 = at;
            var late = await service.평가Async(plan, order, ct);
            Require(!late.삽입가능여부 && !late.전체완수가능여부 && late.위반사유.Length > 0);
        });
        await Check("fixture-order-builder-preserves-identity-and-complete-courier-input", () =>
        {
            var at = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
            for (var index = 0; index < 15; index++)
            {
                var requestId = Guid.NewGuid(); var consent = Guid.NewGuid();
                var input = DispatchTrainingOrderFixture.Create(index, requestId, consent, at);
                var loading = NeighborhoodDeliveryLoadingInput.Calculate(input);
                Require(input.ClientRequestId == requestId && input.PrivacyConsentEvidenceId == consent
                    && input.CargoName == $"가상 소형 상자 {index + 1:00}" && input.VehicleType == DispatchTrainingSeed.VehicleName
                    && input.Pickup.주소.도로명주소 == $"가상 픽업 {index + 1:00}"
                    && input.Dropoff.주소.도로명주소 == $"가상 전달 {index + 1:00}"
                    && input.DispatchMode == "automatic" && input.DispatchRequested && input.DirectPaymentAgreed);
                Require(NeighborhoodDeliveryLoadingInput.InternalCourierMissing(input).Count == 0
                    && NeighborhoodDeliveryLoadingInput.InputErrors(input).Count == 0
                    && loading.유효여부 && loading.유효부피Cbm == 0.003m
                    && input.PalletCount == 0 && input.Pickup.작업시간분 == 1m && input.Dropoff.작업시간분 == 1m
                    && input.Pickup.시간창!.종료일시 == at.AddHours(2) && input.Dropoff.시간창!.종료일시 == at.AddHours(2));
            }
            return Task.CompletedTask;
        });
        await Check("fixture-missing-pallet-is-refused-by-real-segment-engine-and-zero-is-valid", () =>
        {
            var input = DispatchTrainingOrderFixture.Create(0, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var engine = new 화물구간적재검증Service(new 차량적재추천Engine());
            var vehicle = new 차량제원
            {
                차량코드 = DispatchTrainingSeed.VehicleCode, 차량명 = DispatchTrainingSeed.VehicleName,
                적재함길이Mm = 500, 적재함폭Mm = 450, 적재함높이Mm = 400,
                최대적재중량Kg = 20, 운영권장중량Kg = 20, 권장최대CBM = 0.09m, 팔레트적재개수 = 0,
                추천사용여부 = true, 비눈보호가능 = true
            };
            화주운송의뢰 Projection()
            {
                var cargo = NeighborhoodDeliveryLoadingInput.ToCargo(input);
                var loading = NeighborhoodDeliveryLoadingInput.Calculate(input);
                return new()
                {
                    의뢰Id = "synthetic-pallet-regression", 화물종류 = cargo.화물종류, 화물수량 = cargo.수량,
                    화물길이Mm = cargo.길이Mm, 화물폭Mm = cargo.폭Mm, 화물높이Mm = cargo.높이Mm,
                    화물중량Kg = cargo.중량Kg, 화물팔레트개수 = cargo.팔레트개수,
                    화물바닥회전가능여부 = cargo.바닥회전가능여부, 화물적층가능여부 = cargo.적층가능여부,
                    화물온도조건 = "상온", 운송방식 = "직송", 화물부피Cbm = loading.유효부피Cbm,
                    화물부피근거Code = loading.부피근거Code, 화물적재계산판본 = loading.계산판본
                };
            }
            기사운송일정항목[] stops = [new("synthetic-pallet-regression", "pickup", "가상 픽업", origin, null, null, 0, null, false, true),
                new("synthetic-pallet-regression", "dropoff", "가상 전달", dropoff, null, null, 1, null, false, true)];
            input.PalletCount = null;
            var missing = engine.평가(vehicle, [new(Projection())], stops);
            Require(!missing.확인됨 && !missing.적합여부 && missing.사유Code == "FreightSegment.EvidenceMissing"
                && NeighborhoodDeliveryLoadingInput.InternalCourierMissing(input).Count == 1);
            input.PalletCount = 0;
            var complete = engine.평가(vehicle, [new(Projection())], stops);
            Require(complete.확인됨 && complete.적합여부 && complete.사유Code == "FreightSegment.Verified"
                && complete.구간목록.All(x => x.팔레트개수 == 0));
            return Task.CompletedTask;
        });
        await Check("fixture-missing-handling-times-remain-missing-instead-of-zero", () =>
        {
            var input = DispatchTrainingOrderFixture.Create(0, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            input.Pickup.작업시간분 = null; input.Dropoff.작업시간분 = null;
            Require(NeighborhoodDeliveryLoadingInput.InternalCourierMissing(input).Count == 2);
            input.Pickup.작업시간분 = 1m; input.Dropoff.작업시간분 = 1m;
            Require(NeighborhoodDeliveryLoadingInput.InternalCourierMissing(input).Count == 0);
            return Task.CompletedTask;
        });
        await Check("fixture-acceptance-acknowledges-only-server-advertised-vehicle-advisory", () =>
        {
            Require(DispatchTrainingActions.VehicleWarningAcknowledgements(null).Length == 0);
            Require(DispatchTrainingActions.VehicleWarningAcknowledgements(JsonNode.Parse("{\"경고\":[\"일반 안내\"]}")).Length == 0);
            Require(DispatchTrainingActions.VehicleWarningAcknowledgements(JsonNode.Parse("{\"차량경고\":[]}")).Length == 0);
            Require(DispatchTrainingActions.VehicleWarningAcknowledgements(JsonNode.Parse("{\"경고\":[\"일반 안내\"],\"차량경고\":[\"공통 제원 기준\",\"실차 확인 필요\"]}"))
                .SequenceEqual(new[] { "FreightVehicleAdvisory" }));
            return Task.CompletedTask;
        });
        return results;

        async Task Check(string name, Func<Task> action)
        {
            ct.ThrowIfCancellationRequested();
            try { await action(); results.Add(new(name, true, null)); }
            catch (Exception error) when (error is not OutOfMemoryException && !ct.IsCancellationRequested)
            { results.Add(new(name, false, error.GetType().Name + ": " + error.Message)); }
        }
    }

    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Synthetic fixture invariant failed."); }
    private static async Task RejectCanceled<T>(Func<Task<T>> action)
    {
        try { await action(); throw new InvalidOperationException("Cancellation was ignored."); }
        catch (OperationCanceledException) { }
    }
}
