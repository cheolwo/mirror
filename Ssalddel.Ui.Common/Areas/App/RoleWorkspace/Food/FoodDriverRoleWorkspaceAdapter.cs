using System.Globalization;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public sealed class FoodDriverRoleWorkspaceAdapter(IRoleWorkspaceApi api, IRoleWorkspaceLocationProvider locationProvider) : IRoleWorkspaceAdapter
{
    private FoodDeliveryDriverWorkspaceDto? workspace;
    private 기사운행상태응답? work;
    private 운영배차수신상태Dto? dispatch;
    private RoleWorkspaceLocation? currentLocation;
    public string RoleKey => "food-driver";
    public const string StartWork = "food-driver.work-start";
    public const string StopWork = "food-driver.work-stop";
    public const string EnableDispatch = "food-driver.enable-dispatch";
    public const string DisableDispatch = "food-driver.disable-dispatch";
    public const string UpdateLocation = "food-driver.update-location";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var value = await api.GetAsync<FoodDeliveryDriverWorkspaceDto>(RoleKey, "api/v1/driver/food-deliveries/workspace", cancellationToken);
        var status = await api.GetAsync<기사운행상태응답>(RoleKey, "api/v1/driver/food-deliveries/work/status", cancellationToken);
        var availability = await api.GetAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        workspace = value; work = status; dispatch = availability;
        var active = value.ActiveDeliveries.Select(MapActive).ToArray();
        var items = active.Concat(value.Recommendations.Select(MapOffer)).ToArray();
        var selected = active.FirstOrDefault(item => item.Id == selectedId)?.Id ?? active.FirstOrDefault()?.Id
            ?? items.FirstOrDefault(item => item.Id == selectedId)?.Id ?? items.FirstOrDefault()?.Id;
        var (routes, routeNotice) = await ReadRoutesAsync(selected, value, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (selected is not null && currentLocation is { } location && Fresh(location))
            items = items.Select(item => item.Id == selected ? item with
            {
                Markers = (item.Markers ?? []).Concat([new NeighborhoodMapMarker(item.Id + ":driver", "최근 확인한 내 위치", NeighborhoodMapMarkerKinds.Driver,
                    location.Latitude, location.Longitude, MeasuredAt: location.MeasuredAt, ExpiresAt: location.MeasuredAt.AddSeconds(30))]).ToArray(),
                Routes = routes
            } : item).ToArray();
        var onDuty = status.Status.Equals("운행중", StringComparison.OrdinalIgnoreCase);
        var receives = availability.수신의사Code == 운영배차수신의사Code.On;
        var controls = new List<RoleWorkspaceAction>
        {
            new(onDuty ? StopWork : StartWork, onDuty ? "운행 종료" : "운행 시작", IsPrimary: !onDuty),
            new(receives ? DisableDispatch : EnableDispatch, receives ? "신규 배차 받기 끄기" : "신규 배차 받기 켜기", IsPrimary: onDuty && !receives),
            new(UpdateLocation, "현재 위치 갱신")
        };
        var notice = !onDuty ? "운행을 시작하면 배달 요청을 받을 준비를 합니다."
            : !receives ? "신규 배차 받기를 켜면 배달 요청을 받을 수 있습니다."
            : availability.실효상태Code switch
            {
                운영배차실효상태Code.서버일시정지 => "신규 배차가 일시 중지되었습니다. 운영자에게 문의해 주세요.",
                운영배차실효상태Code.연결확인불가 => "배차 연결 상태를 확인할 수 없습니다. 새로고침해 주세요.",
                운영배차실효상태Code.조건부적합 => "신규 배차 조건을 확인해 주세요.",
                _ => active.Length > 0 ? "현재 배달의 진행 상태와 가능한 행동을 확인해 주세요."
                    : value.Recommendations.Count > 0 ? "새로운 배달 제안을 확인해 주세요."
                    : "새로운 배달 요청을 기다리고 있습니다."
            };
        if (!string.IsNullOrWhiteSpace(value.DispatchAutomationNotice)) notice += " " + value.DispatchAutomationNotice;
        if (!string.IsNullOrWhiteSpace(routeNotice)) notice += " " + routeNotice;
        return new(RoleKey, items, notice, selected, controls);
    }

    private async Task<(IReadOnlyList<NeighborhoodMapRoute> Routes, string? Notice)> ReadRoutesAsync(string? selected,
        FoodDeliveryDriverWorkspaceDto value, CancellationToken token)
    {
        if (selected is null) return ([], null);
        if (currentLocation is not { } location || !Fresh(location)) return ([], "현재 위치를 갱신하면 실제 이동 경로를 확인할 수 있습니다.");
        var active = value.ActiveDeliveries.FirstOrDefault(item => item.OfferId == selected);
        var offer = value.Recommendations.FirstOrDefault(item => item.OfferId == selected);
        if (active is null && offer is null) return ([], null);
        var pickup = active?.Pickup ?? offer!.Pickup;
        var dropoff = active?.Dropoff ?? offer!.Dropoff;
        var isDelivery = active is not null && FoodDriverStagePresentation.Resolve(active) == FoodDriverStage.Delivery;
        var legs = new List<(decimal Latitude, decimal Longitude, FoodDeliveryDriverStopDto Stop, string Color, string Key)>();
        if (isDelivery)
            legs.Add(((decimal)location.Latitude, (decimal)location.Longitude, dropoff, "#2563eb", "delivery"));
        else
        {
            legs.Add(((decimal)location.Latitude, (decimal)location.Longitude, pickup, "#f97316", "pickup"));
            if (FoodWorkspacePresentation.Coordinates(pickup.Latitude, pickup.Longitude))
                legs.Add((pickup.Latitude!.Value, pickup.Longitude!.Value, dropoff, "#6b7280", "delivery"));
        }
        var routes = new List<NeighborhoodMapRoute>();
        var incomplete = false;
        foreach (var leg in legs)
        {
            if (!FoodWorkspacePresentation.Coordinates(leg.Stop.Latitude, leg.Stop.Longitude)) { incomplete = true; continue; }
            try
            {
                var route = await api.PostAsync<FoodDeliveryDriverRouteResponseDto>(RoleKey, "api/v1/driver/food-deliveries/route", new FoodDeliveryDriverRouteRequestDto
                {
                    StartLatitude = leg.Latitude, StartLongitude = leg.Longitude,
                    Stops = [new() { Label = leg.Stop.Label, Latitude = leg.Stop.Latitude!.Value, Longitude = leg.Stop.Longitude!.Value }]
                }, token);
                token.ThrowIfCancellationRequested();
                if (route is null || route.Source != "NaverDirections5" || route.IsEstimated || route.Points.Count < 2
                    || route.Points.Any(point => !FoodWorkspacePresentation.Coordinates(point.Latitude, point.Longitude)))
                { incomplete = true; continue; }
                routes.Add(new(selected + ":" + leg.Key, route.Points.Select(point => new NeighborhoodMapPoint((double)point.Latitude, (double)point.Longitude)).ToArray(),
                    leg.Color, SourceCode: route.Source, ExpiresAt: location.MeasuredAt.AddSeconds(30)));
            }
            catch (OperationCanceledException) { throw; }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 401 or 403) { throw; }
            catch { incomplete = true; }
        }
        return (routes, incomplete ? "일부 실제 이동 경로를 확인하지 못했습니다. 목적지 핀과 업무 내용을 확인해 주세요." : null);
    }

    public async Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
    {
        if (workspace is null) throw new InvalidOperationException("기사 업무를 먼저 새로고침해 주세요.");
        if (actionKey is StartWork or StopWork or EnableDispatch or DisableDispatch or UpdateLocation)
        {
            await PerformWorkAsync(actionKey, requestId, cancellationToken); return;
        }
        var active = workspace.ActiveDeliveries.FirstOrDefault(value => value.OfferId == itemId);
        var offer = workspace.Recommendations.FirstOrDefault(value => value.OfferId == itemId);
        var actions = active?.AvailableActions ?? offer?.AvailableActions ?? [];
        var allowed = FoodWorkspacePresentation.Require(actions, actionKey);
        if (offer?.ExpiresAtUtc is { } expiry && expiry <= DateTime.UtcNow)
            throw new InvalidOperationException("배달 제안이 만료되었습니다. 새로고침해 주세요.");
        if (actionKey == 음식배달가능행동Ids.기사배달중단)
            throw new InvalidOperationException("중단 사유를 입력해 주세요.");
        var endpoint = actionKey switch
        {
            음식배달가능행동Ids.기사제안수락 when offer is not null => "accept",
            음식배달가능행동Ids.기사제안거절 when offer is not null => "reject",
            음식배달가능행동Ids.기사가게도착 when active is not null => "restaurant-arrival",
            음식배달가능행동Ids.기사픽업확인 when active is not null => "pickup-complete",
            음식배달가능행동Ids.기사전달완료 when active is not null => "delivery-complete",
            _ => throw new InvalidOperationException("최신 배달 단계와 가능 행동을 확인해 주세요.")
        };
        if (endpoint == "restaurant-arrival" && (active is null || string.IsNullOrWhiteSpace(active.DeliveryAttemptId)
            || active.RestaurantArrivedAtUtc.HasValue || FoodDriverStagePresentation.Resolve(active) == FoodDriverStage.Delivery))
            throw new InvalidOperationException("현재 배달 시도와 음식점 도착 상태를 확인해 주세요.");
        if (actionKey != 음식배달가능행동Ids.기사제안거절) await PublishLocationAsync(cancellationToken);
        object body = active is not null && endpoint == "restaurant-arrival"
            ? new 음식배달가게도착요청 { 클라이언트요청Id = requestId, 예상시도Revision = allowed.ExpectedRevision ?? active.AttemptRevision }
            : new { };
        await api.PostAsync<FoodDeliveryDriverActionResponse>(RoleKey,
            $"api/v1/driver/food-deliveries/offers/{Uri.EscapeDataString(itemId)}/{endpoint}", body, cancellationToken);
    }

    private async Task PerformWorkAsync(string action, Guid requestId, CancellationToken token)
    {
        if (work is null || dispatch is null) throw new InvalidOperationException("운행·배차 상태를 확인해 주세요.");
        if (action is EnableDispatch or DisableDispatch)
        {
            await api.PutAsync<운영배차수신상태Dto>(RoleKey, "api/v1/driver/operational-dispatch/availability/intent",
                new 운영배차수신의사변경요청 { 클라이언트요청Id = requestId, 수신의사Code = action == EnableDispatch ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off }, token);
        }
        else if (action == StartWork)
        {
            var location = await RequireLocationAsync(token);
            var started = await api.PostAsync<기사운행시작응답>(RoleKey, "api/v1/driver/food-deliveries/work/start", new 기사운행시작요청
            {
                시작모드 = "바로시작", 시작시각 = DateTime.UtcNow,
                시작위치 = string.Create(CultureInfo.InvariantCulture, $"{location.Latitude},{location.Longitude}"),
                커뮤니티운행공개 = false, 커뮤니티구단위위치공개동의 = false
            }, token);
            token.ThrowIfCancellationRequested();
            if (started is null) throw new InvalidOperationException("운행 시작 결과를 확인하지 못했습니다. 새로고침해 주세요.");
            work = new() { DriverId = started.DriverId, Status = started.Status };
            await PublishLocationAsync(token, location);
        }
        else if (action == StopWork) await api.PostAsync<object>(RoleKey, "api/v1/driver/food-deliveries/work/stop", new { }, token);
        else await PublishLocationAsync(token);
    }

    private async Task<RoleWorkspaceLocation> RequireLocationAsync(CancellationToken token)
    {
        var location = await locationProvider.GetCurrentAsync(token);
        token.ThrowIfCancellationRequested();
        if (location is null || !Fresh(location) || !double.IsFinite(location.Latitude) || !double.IsFinite(location.Longitude)
            || location.Latitude is < -90 or > 90 || location.Longitude is < -180 or > 180
            || location.Latitude == 0 && location.Longitude == 0
            || location.AccuracyMeters is { } accuracy && (!double.IsFinite(accuracy) || accuracy < 0 || accuracy > (double)decimal.MaxValue))
            throw new InvalidOperationException("현재 위치를 확인할 수 없습니다. 위치 권한과 GPS를 확인해 주세요.");
        return location;
    }

    private static bool Fresh(RoleWorkspaceLocation location)
        => DateTimeOffset.UtcNow - location.MeasuredAt <= TimeSpan.FromSeconds(30) && location.MeasuredAt <= DateTimeOffset.UtcNow.AddSeconds(5);

    private async Task PublishLocationAsync(CancellationToken token, RoleWorkspaceLocation? captured = null)
    {
        var location = captured ?? await RequireLocationAsync(token);
        var response = await api.PostAsync<기사위치갱신응답>(RoleKey, "api/v1/driver/food-deliveries/work/location", new 기사위치갱신요청
        {
            AppKey = 기사앱식별자.FoodDeliveryDriverApp, 위도 = (decimal)location.Latitude, 경도 = (decimal)location.Longitude,
            정확도_m = location.AccuracyMeters is { } accuracy ? (decimal)accuracy : null,
            상차접근허용반경Km = 3m, 운행상태 = work?.Status, 기록시각 = location.MeasuredAt.UtcDateTime
        }, token);
        token.ThrowIfCancellationRequested();
        if (response is null || !FoodWorkspacePresentation.Coordinates(response.현재위도, response.현재경도)
            || response.현재위도 != (decimal)location.Latitude || response.현재경도 != (decimal)location.Longitude)
            throw new InvalidOperationException("현재 위치 전송 결과를 확인하지 못했습니다. 다시 갱신해 주세요.");
        currentLocation = location;
    }

    private static RoleWorkspaceItem MapOffer(FoodDeliveryDriverOfferDto offer)
    {
        var choices = new[] { (음식배달가능행동Ids.기사제안수락, "배달 수락", true), (음식배달가능행동Ids.기사제안거절, "제안 거절", false) };
        var actions = choices.Select(value => FoodWorkspacePresentation.Available(offer.AvailableActions, value.Item1, value.Item2, value.Item3))
            .OfType<RoleWorkspaceAction>().Select(value => offer.ExpiresAtUtc is { } expiry && expiry <= DateTime.UtcNow
                ? value with { Enabled = false, DisabledReason = "제안이 만료되었습니다. 새로고침해 주세요." } : value).ToArray();
        return new(offer.OfferId, offer.RestaurantName, "배달 제안 · 수락 전", offer.OrderSummary,
            [FoodWorkspacePresentation.Section("픽업할 음식점", ("음식점", offer.RestaurantName), ("픽업 주소", offer.Pickup.Address)),
             FoodWorkspacePresentation.Section("배달 조건", ("배달료", FoodWorkspacePresentation.Money(offer.DriverPayout)),
                ("제안 거리", offer.DistanceKm is { } distance ? distance.ToString("0.###", CultureInfo.InvariantCulture) + "km" : "미확인"),
                ("제안 만료", FoodWorkspacePresentation.Time(offer.ExpiresAtUtc)), ("추천 안내", offer.RecommendationReason))], actions,
            Stops(offer.OfferId, offer.Pickup, offer.Dropoff), Summary: new("픽업지", FoodWorkspacePresentation.Value(offer.Pickup.Address, "미확인"),
                [new("배달료", Payout(offer.DriverPayout)), new("제안 거리", offer.DistanceKm is >= 0
                    ? offer.DistanceKm.Value.ToString("0.###", CultureInfo.InvariantCulture) + " km" : "미확인")]));
    }

    private static RoleWorkspaceItem MapActive(FoodDeliveryDriverActiveDeliveryDto active)
    {
        var stage = FoodDriverStagePresentation.Resolve(active);
        var primary = FoodDriverStagePresentation.PrimaryAction(stage);
        var primaryLabel = stage switch { FoodDriverStage.Delivery => "고객 전달 완료", FoodDriverStage.PickupWaiting => "음식 픽업 확인", _ => "음식점 도착" };
        var actions = new List<RoleWorkspaceAction>
        {
            FoodWorkspacePresentation.Available(active.AvailableActions, primary, primaryLabel, true)
                ?? new(primary, primaryLabel, IsPrimary: true, Enabled: false, DisabledReason: stage == FoodDriverStage.Delivery
                    ? "최신 전달 상태와 가능 행동을 확인해 주세요." : FoodDriverStagePresentation.PickupGuide(active, stage))
        };
        if (primary == 음식배달가능행동Ids.기사가게도착 && string.IsNullOrWhiteSpace(active.DeliveryAttemptId))
            actions[0] = actions[0] with { Enabled = false, DisabledReason = "현재 배달 시도를 새로고침해 주세요." };
        if (stage != FoodDriverStage.Delivery && primary != 음식배달가능행동Ids.기사가게도착 && !active.RestaurantArrivedAtUtc.HasValue
            && !string.IsNullOrWhiteSpace(active.DeliveryAttemptId)
            && FoodWorkspacePresentation.Available(active.AvailableActions, 음식배달가능행동Ids.기사가게도착, "음식점 도착") is { } arrival) actions.Add(arrival);
        if (FoodWorkspacePresentation.Available(active.AvailableActions, 음식배달가능행동Ids.기사배달중단, "배달 중단",
            route: FoodWorkspacePresentation.ActionRoute("food-driver", 음식배달가능행동Ids.기사배달중단, active.OfferId)) is { } interrupt) actions.Add(interrupt);
        IReadOnlyList<RoleWorkspaceSection> sections;
        if (stage == FoodDriverStage.Delivery)
        {
            sections = [FoodWorkspacePresentation.Section("전달지", ("전달 주소", active.Dropoff.Address)),
                FoodWorkspacePresentation.Section("고객 요청 사항", ("전달 요청", FoodWorkspacePresentation.Value(active.Recipient.DeliveryInstructions, "별도 전달 요청 없음")),
                    ("수령인", active.Recipient.DisplayName), ("수령 관계", active.Recipient.OrdererIsRecipient ? "주문자 본인 수령" : "지정 수령인"),
                    ("연락처", FoodWorkspacePresentation.MaskPhone(active.Recipient.ContactPhone))), Order(active)];
        }
        else
        {
            var recooking = active.CurrentPreparationRound > 1 || active.RecookingRequestedAtUtc.HasValue;
            sections = [FoodWorkspacePresentation.Section("음식점", ("음식점", active.RestaurantName),
                    ("준비 예정", FoodWorkspacePresentation.Time(active.DisplayedPreparationReadyAtUtc)),
                    ("현재 음식", recooking ? "재조리 음식" : "첫 조리 음식"),
                    ("조리 회차", active.CurrentPreparationRound > 0 ? $"{active.CurrentPreparationRound}회차" : "미확인"),
                    ("준비 완료", FoodWorkspacePresentation.Time(active.CurrentPickupReadyAtUtc))),
                FoodWorkspacePresentation.Section("픽업 주소", ("픽업할 곳", active.Pickup.Address)), Order(active)];
        }
        return new(active.OfferId, active.RestaurantName, FoodDriverStagePresentation.Text(stage, active.RestaurantArrivedAtUtc.HasValue),
            active.OrderSummary, sections, actions, Stops(active.OfferId, active.Pickup, active.Dropoff), IsCurrent: true,
            Summary: CurrentSummary(active, stage));
    }

    private static RoleWorkspaceSummary CurrentSummary(FoodDeliveryDriverActiveDeliveryDto active, FoodDriverStage stage)
    {
        if (stage == FoodDriverStage.Delivery)
            return new("전달지", FoodWorkspacePresentation.Value(active.Dropoff.Address, "미확인"),
                [new("배달료", Payout(active.DriverPayout))],
                new("전달 요청", FoodWorkspacePresentation.Value(active.Recipient.DeliveryInstructions, "별도 전달 요청 없음")));
        return new("픽업지", FoodWorkspacePresentation.Value(active.Pickup.Address, "미확인"),
            [new("배달료", Payout(active.DriverPayout)), new("준비 예정", FoodWorkspacePresentation.Time(active.DisplayedPreparationReadyAtUtc))],
            stage == FoodDriverStage.PickupWaiting ? new("픽업 확인", FoodDriverStagePresentation.PickupGuide(active, stage)) : null);
    }

    private static string Payout(decimal payout) => payout > 0 ? FoodWorkspacePresentation.Money(payout) : "미확인";

    private static RoleWorkspaceSection Order(FoodDeliveryDriverActiveDeliveryDto active) => FoodWorkspacePresentation.Section("주문 정보",
        ("주문", active.OrderSummary), ("배달료", FoodWorkspacePresentation.Money(active.DriverPayout)));
    private static IReadOnlyList<NeighborhoodMapMarker> Stops(string id, FoodDeliveryDriverStopDto pickup, FoodDeliveryDriverStopDto dropoff)
        => FoodWorkspacePresentation.Marker(id + ":pickup", "픽업", NeighborhoodMapMarkerKinds.Pickup, pickup.Latitude, pickup.Longitude)
            .Concat(FoodWorkspacePresentation.Marker(id + ":dropoff", "전달", NeighborhoodMapMarkerKinds.Dropoff, dropoff.Latitude, dropoff.Longitude)).ToArray();
    public void Clear() { workspace = null; work = null; dispatch = null; currentLocation = null; }
}
