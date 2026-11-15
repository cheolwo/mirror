using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Ssalddel.BatchMonitor.Windows;

namespace Ssalddel.Sagajeong.Core
{
    /// <summary>Single-process Simulation authority; has no Unity, network, operational API or file dependencies.</summary>
    public sealed class SagajeongSimulationCore
    {
        public const string RulesRevision = "mobile-food-core.v1";
        private const string SaveSchema = "hongdal-mobile-food-save.v1";
        private readonly object gate = new object();
        private readonly string sampleJson;
        private readonly string graphJson;
        private readonly string inputHash;
        private readonly DeliverySample sample;
        private readonly DeliveryRoadSample graph;
        private readonly DeliveryRoadNetwork roads;
        private readonly List<string> history = new List<string>();
        private FoodCampaignStage stage = FoodCampaignStage.All[0];
        private FoodManagementRound? round;
        private LocalCampaign? campaign;
        private long elapsedMs;
        private bool manualPickup;
        private string sessionId = string.Empty;
        private bool replaying;

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64,
        };

        public SagajeongSimulationCore(string deliverySampleJson, string roadGraphJson)
        {
            sampleJson = deliverySampleJson ?? throw new ArgumentNullException(nameof(deliverySampleJson));
            graphJson = roadGraphJson ?? throw new ArgumentNullException(nameof(roadGraphJson));
            inputHash = Hash(sampleJson + "\n" + graphJson);
            sample = JsonConvert.DeserializeObject<DeliverySample>(sampleJson, Json)
                ?? throw new ArgumentException("음식 배달 표본을 읽을 수 없습니다.", nameof(deliverySampleJson));
            graph = JsonConvert.DeserializeObject<DeliveryRoadSample>(graphJson, Json)
                ?? throw new ArgumentException("도로 표본을 읽을 수 없습니다.", nameof(roadGraphJson));
            roads = new DeliveryRoadNetwork(graph);
            if (sample.Stores is null || sample.Orders is null || sample.Stores.Length == 0)
                throw new ArgumentException("매장과 메뉴 표본을 확인하세요.", nameof(deliverySampleJson));
        }

        /// <summary>Compatible with existing JsonUtility command and snapshot shapes.</summary>
        public string Execute(string requestJson)
        {
            lock (gate)
            {
                try
                {
                    var request = JsonConvert.DeserializeObject<CoreRequest>(requestJson, Json);
                    if (request is null || string.IsNullOrWhiteSpace(request.Action))
                        return Serialize(Reply(false, "게임 입력을 확인하세요."));
                    var response = Apply(request);
                    if (!replaying && round is not null && IsStateCommand(request.Action)) RecordHistory(request, response.Ok);
                    return Serialize(response.Payload);
                }
                catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                    exception is InvalidOperationException || exception is OverflowException)
                {
                    return Serialize(Reply(false, "게임 입력이나 저장 자료를 확인하세요.", errorCode: exception.GetType().Name));
                }
            }
        }

        private CoreResult Apply(CoreRequest request)
        {
            if (request.Action == "restore") return Restore(request.SaveJson);
            if (request.Action == "start" || request.Action == "campaign-start")
            {
                var selectedStage = string.IsNullOrWhiteSpace(request.StageId) ? FoodCampaignStage.All[0] : FoodCampaignStage.Find(request.StageId);
                if (selectedStage is null) return Result(false, "지원하지 않는 스테이지입니다.");
                stage = selectedStage;
                manualPickup = request.ManualPickup;
                campaign = request.Action == "campaign-start" ? new LocalCampaign(roads, sample, manualPickup) : null;
                round = campaign?.Round ?? FoodManagementRound.CreateCampaign(roads, sample, stage);
                if (round is null) return Result(false, "연결 가능한 매장과 도로 표본을 확인하세요.");
                if (manualPickup) round.EnableManualPickup();
                elapsedMs = 0;
                sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId!;
                request.SessionId = sessionId;
                history.Clear();
                return Result(true, null, true);
            }
            if (round is null) return Result(false, "회차를 먼저 시작하세요.");
            if (!string.IsNullOrEmpty(request.SessionId) && request.SessionId != sessionId)
                return Result(false, "현재 회차와 다른 요청입니다.");
            if (request.Action == "snapshot" || request.Action == "state") return Result(true);
            if (request.Action == "save")
                return new CoreResult(true, new { ok = true, state = Snapshot(), saveJson = BuildSave() });
            if (request.Action == "export")
                return new CoreResult(true, new { ok = true, state = Snapshot(), exportJson = BuildExport() });
            if (request.Action == "candidates")
            {
                var order = FindVisibleOrder(request.OrderId);
                if (order is null) return Result(false, "후보를 확인할 주문을 선택하세요.");
                var preview = order.Preview();
                return new CoreResult(true, new { ok = true, state = Snapshot(), revision = preview.Revision,
                    recommendedDriverId = preview.RecommendedDriverId,
                    candidates = preview.Candidates.Select(value => new {
                        value.DriverId, value.Label, value.Eligible, value.PickupKilometers,
                        arrivalAt = value.ArrivalAt?.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        readyDifferenceSeconds = value.ReadyDifference?.TotalSeconds, value.Reason }).ToArray() });
            }
            if (request.Action == "campaign-policy")
            {
                var accepted = campaign is not null && request.ElapsedMs == campaign.ElapsedMs && campaign.TryChoosePolicy(request.Policy);
                return Result(accepted, accepted ? "다음 운영 방침을 확정했습니다." : "현재 시각에는 방침을 확정할 수 없습니다.");
            }
            if (request.Action != "tick" && request.Action != "assign" && request.Action != "pickup-confirm")
                return Result(false, "지원하지 않는 게임 행동입니다.");
            var previousStageId = round.CampaignStage?.Id;
            if (campaign is not null)
            {
                if (!campaign.AcceptsTime(request.ElapsedMs)) return Result(false, "가상 시각을 다시 확인하세요.");
                campaign.Advance(request.ElapsedMs);
                round = campaign.Round;
                elapsedMs = campaign.ElapsedMs;
            }
            else
            {
                if (request.ElapsedMs < elapsedMs || request.ElapsedMs > TimeSpan.FromDays(1).TotalMilliseconds)
                    return Result(false, "현재 회차의 가상 시각을 확인하세요.");
                if (!round.IsSettled)
                {
                    elapsedMs = request.ElapsedMs;
                    round.Tick(TimeSpan.FromMilliseconds(elapsedMs));
                }
            }
            var coverageChanged = campaign is not null && previousStageId != round.CampaignStage?.Id;
            if (request.Action == "tick") return Result(true, null, coverageChanged);
            var selected = FindVisibleOrder(request.OrderId);
            if (selected is null || string.IsNullOrWhiteSpace(request.CommandId) || string.IsNullOrWhiteSpace(request.DriverId))
                return Result(false, "현재 회차의 주문과 기사를 선택하세요.", coverageChanged);
            if (request.Action == "assign" && campaign is not null && !campaign.AcceptingAssignments)
                return Result(false, "현재 영업일에는 새로 배정할 수 없습니다.", coverageChanged);
            var result = request.Action == "assign"
                ? round.Assign(request.CommandId!, selected.OrderId, request.DriverId!, request.Revision)
                : round.ConfirmPickup(request.CommandId!, selected.OrderId, request.DriverId!, request.Revision);
            return Result(result.Accepted, result.Message, coverageChanged);
        }

        private FoodManagementSession? FindVisibleOrder(string? id) =>
            round is null ? null : (campaign?.VisibleOrders() ?? round.Orders).FirstOrDefault(value => PublicOrderId(value) == id);

        private string PublicOrderId(FoodManagementSession value) => campaign?.OrderId(value) ?? value.OrderId;

        private object? Snapshot()
        {
            if (round is null) return null;
            var map = round.MapSnapshot();
            var result = round.CampaignResult;
            var visibleOrders = (campaign?.VisibleOrders() ?? round.Orders).ToArray();
            var visibleIds = new HashSet<string>(visibleOrders.Select(value => value.OrderId), StringComparer.Ordinal);
            object? settlement = result is null ? null : new {
                result.Cleared, onTimeCount = result.OnTimeAssignmentCount, revenue = result.PlatformRevenue,
                result.DriverPay, baseBalance = result.PlatformBalance - result.FastDispatchBonus,
                bonus = result.FastDispatchBonus, balance = result.PlatformBalance };
            if (campaign is not null) settlement = campaign.Settlement();
            return new {
                sessionId, sourceKind = "Simulation", rulesRevision = RulesRevision, manualPickup,
                elapsedMs = campaign?.ElapsedMs ?? elapsedMs,
                now = campaign?.Now ?? round.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                stageId = round.CampaignStage?.Id ?? stage.Id,
                remainingSeconds = (int)Math.Ceiling(round.DispatchRemaining.TotalSeconds),
                orders = visibleOrders.Select(order => new {
                    id = PublicOrderId(order), storeId = order.PickupStoreId, storeName = order.PickupStoreName,
                    order.Menu, stage = order.Stage.ToString(), driverId = order.AssignedDriverId, order.Revision,
                    recommendedDriverId = order.Stage == FoodManagementStage.Waiting ? order.Preview().RecommendedDriverId : null,
                    assignedOnTime = order.AssignedAt is not null && !round.LateOrders.Contains(order),
                    deliveryMinutes = order.Settlement?.DeliveryTime.TotalMinutes,
                    canConfirmPickup = order.CanConfirmPickup }).ToArray(),
                map.Couriers,
                stores = map.Restaurants.Select(store => new { store.Id, store.Name, store.Latitude, store.Longitude }).ToArray(),
                destinations = map.Orders.Zip(round.Orders, (destination, order) => (destination, order))
                    .Where(item => visibleIds.Contains(item.order.OrderId))
                    .Select(item => new { id = PublicOrderId(item.order), item.destination.Latitude, item.destination.Longitude }).ToArray(),
                events = visibleOrders.SelectMany(order => order.Events.Select(entry => new {
                    orderId = PublicOrderId(order), at = entry.At.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    entry.Code, entry.Text })).OrderBy(entry => entry.at, StringComparer.Ordinal).ToArray(),
                settled = campaign?.IsSettled ?? round.IsSettled, settlement, campaign = campaign?.Snapshot() };
        }

        private object[] BackgroundRoads()
        {
            if (round is null) return Array.Empty<object>();
            var map = round.MapSnapshot();
            var covered = new List<DeliveryPoint>(map.Coverage);
            foreach (var order in map.Orders)
            {
                var pickup = map.Restaurants.Single(store => store.Id == order.StoreId);
                var pickupNode = roads.Closest(new DeliveryPoint(pickup.Latitude, pickup.Longitude));
                var destinationNode = roads.Closest(new DeliveryPoint(order.Latitude, order.Longitude));
                var delivery = roads.FindPath(pickupNode, destinationNode);
                if (delivery is not null) covered.AddRange(delivery.Points);
                foreach (var courier in map.Couriers)
                {
                    var origin = roads.Closest(new DeliveryPoint(courier.Latitude, courier.Longitude));
                    var route = roads.FindPath(origin, pickupNode);
                    if (route is not null) covered.AddRange(route.Points);
                }
            }
            var south = covered.Min(value => value.Latitude) - 0.0013;
            var north = covered.Max(value => value.Latitude) + 0.0013;
            var west = covered.Min(value => value.Longitude) - 0.0016;
            var east = covered.Max(value => value.Longitude) + 0.0016;
            bool Visible(DeliveryPoint point) => point.Latitude >= south && point.Latitude <= north && point.Longitude >= west && point.Longitude <= east;
            return graph.Edges.Where(edge => Visible(graph.Nodes[edge[0]]) && Visible(graph.Nodes[edge[1]]))
                .Select(edge => (object)new { a = graph.Nodes[edge[0]], b = graph.Nodes[edge[1]] }).ToArray();
        }

        private CoreResult Result(bool ok, string? message = null, bool includeRoads = false) =>
            new CoreResult(ok, Reply(ok, message, includeRoads));
        private object Reply(bool ok, string? message = null, bool includeRoads = false, string? errorCode = null) =>
            new { ok, message, errorCode, state = Snapshot(), roads = includeRoads ? BackgroundRoads() : null };
        private static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.None, Json);
        private static bool IsStateCommand(string action) => action == "start" || action == "campaign-start" || action == "tick" ||
            action == "assign" || action == "pickup-confirm" || action == "campaign-policy";

        private void RecordHistory(CoreRequest request, bool ok)
        {
            // A completed legacy round freezes at the first settling tick. Later no-op ticks must
            // not replace that boundary with a larger time in the replay journal.
            if (campaign is null && request.Action == "tick" && round!.IsSettled && request.ElapsedMs != elapsedMs)
                return;
            var json = Serialize(request);
            // Campaign round start times can depend on intermediate ticks after overnight deliveries.
            // Only a single-round session may compact adjacent ticks without changing replay semantics.
            if (campaign is null && ok && request.Action == "tick" && history.Count > 0)
            {
                var previous = JsonConvert.DeserializeObject<CoreRequest>(history[history.Count - 1], Json);
                if (previous?.Action == "tick" && previous.ElapsedMs <= request.ElapsedMs)
                    { history[history.Count - 1] = json; return; }
            }
            history.Add(json);
        }

        private string BuildSave() => Serialize(new { schemaVersion = SaveSchema, rulesRevision = RulesRevision,
            inputHashSha256 = inputHash, stateHashSha256 = Hash(Serialize(Snapshot()!)),
            sessionId, commands = history.ToArray() });

        private CoreResult Restore(string? saveJson)
        {
            if (string.IsNullOrWhiteSpace(saveJson)) return Result(false, "저장 자료를 선택하세요.");
            var saved = JsonConvert.DeserializeObject<SaveDocument>(saveJson, Json);
            if (saved is null || saved.SchemaVersion != SaveSchema || saved.RulesRevision != RulesRevision ||
                saved.InputHashSha256 != inputHash || saved.Commands is null || saved.Commands.Length == 0 ||
                string.IsNullOrWhiteSpace(saved.SessionId) || string.IsNullOrWhiteSpace(saved.StateHashSha256))
                return Result(false, "저장 판본과 원본 자료가 현재 게임과 다릅니다.");
            var candidate = new SagajeongSimulationCore(sampleJson, graphJson) { replaying = true };
            var ordinal = 0;
            foreach (var command in saved.Commands)
            {
                var request = JsonConvert.DeserializeObject<CoreRequest>(command, Json);
                if (request is null || !IsStateCommand(request.Action ?? string.Empty))
                    return Result(false, "저장된 게임 행동을 확인하세요.");
                if (ordinal++ == 0 && request.Action != "start" && request.Action != "campaign-start")
                    return Result(false, "저장된 회차 시작을 확인하세요.");
                var response = candidate.Apply(request);
                if (!response.Ok && (request.Action == "start" || request.Action == "campaign-start"))
                    return Result(false, "저장된 게임 시작을 재생할 수 없습니다.");
            }
            if (candidate.round is null || candidate.sessionId != saved.SessionId)
                return Result(false, "저장된 회차를 확인하세요.");
            if (Hash(Serialize(candidate.Snapshot()!)) != saved.StateHashSha256)
                return Result(false, "저장된 행동과 회차 결과가 일치하지 않습니다.");
            stage = candidate.stage; round = candidate.round; campaign = candidate.campaign;
            elapsedMs = candidate.elapsedMs; manualPickup = candidate.manualPickup; sessionId = candidate.sessionId;
            history.Clear(); history.AddRange(saved.Commands);
            return Result(true, "저장한 가상 회차를 불러왔습니다.", true);
        }

        private string BuildExport()
        {
            var map = round!.MapSnapshot();
            var visible = (campaign?.VisibleOrders() ?? round.Orders).ToArray();
            var completed = visible.Where(value => value.Settlement is not null).ToArray();
            return Serialize(new {
                schemaVersion = "game-business-preparation.v1", sourceKind = "Simulation", sessionId,
                inputHashSha256 = inputHash, rulesRevision = RulesRevision, virtualElapsedMs = campaign?.ElapsedMs ?? elapsedMs,
                stageId = round.CampaignStage?.Id ?? stage.Id, manualPickup,
                stores = map.Restaurants.Select(value => new { value.Id, value.Name, value.Latitude, value.Longitude }).ToArray(),
                couriers = map.Couriers.Select(value => new { value.Id, value.Label, value.Phase }).ToArray(),
                orders = visible.Select(value => new {
                    id = PublicOrderId(value), storeId = value.PickupStoreId, value.Menu,
                    stage = value.Stage.ToString(), driverId = value.AssignedDriverId }).ToArray(),
                summary = new { totalOrders = visible.Length, completedOrders = completed.Length,
                    virtualRevenue = completed.Sum(value => value.Settlement!.PlatformRevenue),
                    virtualDriverPay = completed.Sum(value => value.Settlement!.DriverPay),
                    virtualBaseBalance = completed.Sum(value => value.Settlement!.PlatformBalance) },
                operationalActionsAllowed = false, approvedForTraining = false, weightsTrained = false });
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private sealed class CoreResult
        {
            internal CoreResult(bool ok, object payload) { Ok = ok; Payload = payload; }
            internal bool Ok { get; }
            internal object Payload { get; }
        }
        private sealed class CoreRequest
        {
            public string? Action { get; set; }
            public long ElapsedMs { get; set; }
            public string? OrderId { get; set; }
            public string? DriverId { get; set; }
            public string? CommandId { get; set; }
            public long Revision { get; set; }
            public string? Policy { get; set; }
            public bool ManualPickup { get; set; }
            public string? StageId { get; set; }
            public string? SessionId { get; set; }
            public string? SaveJson { get; set; }
        }
        private sealed class SaveDocument
        {
            public string? SchemaVersion { get; set; }
            public string? RulesRevision { get; set; }
            public string? InputHashSha256 { get; set; }
            public string? StateHashSha256 { get; set; }
            public string? SessionId { get; set; }
            public string[]? Commands { get; set; }
        }
    }
}
