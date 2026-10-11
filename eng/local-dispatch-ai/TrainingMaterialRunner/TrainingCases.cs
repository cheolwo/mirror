using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Tests.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

internal sealed record TrainingRow(int schemaVersion, string caseId, string taskCode, string scenarioFamily,
    string[] sourceRecordIds, string[] sourceBranchIds, string split, object input, object target,
    object teacher, object provenance, object review);

internal sealed class TrainingCases(IReadOnlyList<JsonElement> records)
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    private readonly 음식배달수요참여Policy policy = new();
    private readonly TrainingGeometry routes = new();

    public async Task<List<TrainingRow>> GenerateAsync()
    {
        var output = new List<TrainingRow>(4041);
        using var denied = new DeniedNetwork();
        using var http = new HttpClient(denied);
        var service = new 음식권역배차관리Service(new DeniedDemandRead(), new 음식멀티배차조합Service(routes),
            new Ollama음식권역배차AI(http, Options.Create(new 음식권역배차OllamaOptions { Enabled = false })), new FixedClock());
        foreach (var record in records.OrderBy(Id, StringComparer.Ordinal))
        {
            foreach (var family in new[] { "shortage", "surplus", "long-wait", "stale", "missing", "no-eligible" })
            {
                var draft = Demand(Id(record), family);
                var result = policy.평가(draft.Source);
                AssertDemand(family, result);
                output.Add(Row("DemandParticipation", family, [record], draft.Input, new
                {
                    dataStateCode = result.데이터상태Code, reasonCode = result.사유Code,
                    targetActiveDrivers = result.목표활동인원, additionalParticipants = result.추가참여인원,
                    driverDecisions = result.기사판정목록.OrderBy(x => x.우선순위).Select(x => new
                    {
                        driverToken = draft.Tokens[x.기사Id], statusCode = x.상태Code, reasonCode = x.사유Code,
                        allowProposal = x.신규제안허용, admitted = x.승격여부, priority = x.우선순위
                    }).ToArray()
                }, 음식배달수요참여Policy.현재정책판본, "NotApplicable"));
            }

            foreach (var family in new[] { "single", "two-order" })
            {
                var seed = Id(record) + "|bundle|" + family;
                var draft = Demand(seed, "shortage");
                var current = policy.평가(draft.Source);
                JsonElement[] selected = [record];
                멀티배차조합요청 request = Request(selected, seed);
                음식권역배차관리결과 result;
                if (family == "two-order")
                {
                    // Neighbours are considered only within the anchor's immutable branch fold.
                    result = null!;
                    음식권역배차관리결과? nearestReview = null;
                    JsonElement nearestRecord = default;
                    var attemptedNeighbours = 0;
                    foreach (var neighbour in records.Where(x => Id(x) != Id(record)
                        && TrainingSources.Split(Branch(x)) == TrainingSources.Split(Branch(record)))
                        .OrderBy(x => routes.CalculateDistanceKm(Point(record, "pickup"), Point(x, "pickup")))
                        .ThenBy(Id, StringComparer.Ordinal))
                    {
                        selected = [record, neighbour]; request = Request(selected, seed);
                        var candidate = await service.검토자료Async(current, request);
                        attemptedNeighbours++;
                        if (nearestReview is null) { nearestReview = candidate; nearestRecord = neighbour; }
                        if (!candidate.유효조합목록.Any(x => x.의뢰Ids.Count == 2)) continue;
                        result = candidate; break;
                    }
                    if (result is null)
                    {
                        if (nearestReview is null) throw new InvalidOperationException("SameFoldNeighbourUnavailable");
                        // Two available orders do not imply a feasible two-order bundle. Preserve the
                        // unchanged engine's single-plan result and both historical source references.
                        result = nearestReview; selected = [record, nearestRecord]; request = Request(selected, seed);
                        Console.Error.WriteLine(JsonSerializer.Serialize(new { code = "NoValidPairSingleFallback",
                            sourceRecordId = Id(record), sourceBranchId = Branch(record), split = TrainingSources.Split(Branch(record)),
                            attemptedNeighbours, nearestSourceRecordId = Id(nearestRecord),
                            pairExclusions = result.생성조합목록.Where(x => x.의뢰Ids.Count == 2)
                                .SelectMany(x => x.제외사유 ?? []).Distinct(StringComparer.Ordinal).ToArray() }));
                    }
                }
                else result = await service.검토자료Async(current, request);
                if (result.AIStatusCode != "Disabled" || result.선택조합 is null || result.OperationalApplied
                    || result.실제적재검증됨 || result.전체약속시간검증됨
                    || result.선택조합.의뢰Ids.Count is not (1 or 2)
                    || family == "single" && result.선택조합.의뢰Ids.Count != 1)
                    throw new InvalidOperationException("DisabledBundleTeacherMismatch");
                var planTokens = TokenMap(result.유효조합목록.Select(x => x.조합키), "P", seed + "|plan-token");
                var orderTokens = TokenMap(request.작업목록.Select(x => x.의뢰Id), "O", seed + "|order-token");
                var plans = Permute(result.유효조합목록, seed + "|plan-order").Select(plan =>
                {
                    var first = plan.작업목록[0]; var last = plan.작업목록[^1];
                    return new
                    {
                        planToken = planTokens[plan.조합키], orderTokens = plan.의뢰Ids.Select(x => orderTokens[x]).ToArray(),
                        orderCount = plan.의뢰Ids.Count, pickupSeparationKm = plan.상차지간거리Km,
                        dropoffSeparationKm = plan.하차지간거리Km, routeDistanceKm = plan.묶음내예상거리Km,
                        samePickup = first.픽업주소 == last.픽업주소,
                        samePickupRegion = first.픽업배달권키 == last.픽업배달권키,
                        sameDropoffRegion = first.하차배달권키 == last.하차배달권키,
                        regionAdjacent = first.하차배달권키 != last.하차배달권키
                            && (국내행정구역배달권Catalog.인접배달권여부(first.하차배달권키!, last.하차배달권키!)
                                || 국내행정구역배달권Catalog.인접배달권여부(last.하차배달권키!, first.하차배달권키!)),
                        pickupWindowGapMinutes = (decimal?)Math.Abs((first.픽업시간창종료일시!.Value - last.픽업시간창종료일시!.Value).TotalMinutes)
                    };
                }).ToArray();
                output.Add(Row("BundleSelection", family, selected, new
                {
                    demand = draft.Input,
                    orders = Permute(request.작업목록.Select((job, index) => new
                    { orderToken = orderTokens[job.의뢰Id], requestOrdinal = index + 1 }), seed + "|order-facts"),
                    plans, limits = new
                    {
                        maximumBundleSize = 2, maximumPickupSeparationKm = request.최대상차지간거리Km,
                        maximumDropoffSeparationKm = request.최대하차지간거리Km,
                        maximumApproximateRouteKm = request.좌표근사총거리상한Km,
                        recommendedPickupSeparationKm = request.같은상권픽업권장거리Km,
                        recommendedDropoffSeparationKm = request.하차권역권장거리Km,
                        recommendedPickupWindowGapMinutes = request.픽업시간창권장차이분
                    }
                }, new
                {
                    actionCode = result.수요ActionCode, aiStatusCode = result.AIStatusCode,
                    selectedPlanToken = planTokens[result.선택조합.조합키],
                    allowedDriverTokens = result.제안허용기사Ids.Select(x => draft.Tokens[x]).ToArray(),
                    requiresCurrentGates = result.RequiresCurrentGates, operationalApplied = false,
                    capacityVerified = false, promisesVerified = false
                }, "food-regional-dispatch-review/r50", "GeometryApproximation"));
            }
        }

        // The public fixture uses a wall clock internally. Only its relative preconditions and
        // stable policy result are exported, so no observed timestamp enters model messages.
        foreach (var district in new[] { "중랑구", "동대문구", "광진구" })
        {
            var anchor = records.Where(x => x.GetProperty("district").GetString() == district).OrderBy(Id, StringComparer.Ordinal).First();
            foreach (var family in new[] { "valid", "late", "missingproof" })
            {
                var pickup = Point(anchor, "pickup");
                var evidence = await 음식배달배차업무정책Tests.RunArrivalAnchorLocalScenarioAsync(pickup.Latitude, pickup.Longitude, family);
                if (!evidence.ExpectedPassed || evidence.ActualMapApiCalled || evidence.Review.후보목록.Count != 1)
                    throw new InvalidOperationException("ArrivalTeacherMismatch");
                var options = new 배차큐정책Options();
                output.Add(Row("ArrivalAnchor", family, [anchor], new
                {
                    activeJobCount = 1, driverObservationAgeMinutes = 0, hasAttemptProof = family != "missingproof",
                    remainingRouteMinutes = 1.2m, nextPickupRouteMinutes = 0.4m,
                    pickupReadyInMinutes = family == "late" ? -10 : 5, vehicleRouteOption = "traavoidcaronly",
                    earlyToleranceMinutes = options.음식배달픽업조기도착허용분,
                    lateToleranceMinutes = options.음식배달픽업지연허용분, driverToken = "D1"
                }, new
                {
                    decisionCode = evidence.Review.후보목록.Single().판정Code,
                    selectedDriverToken = evidence.Review.선정후보 is null ? null : "D1"
                }, "food-arrival-anchor/r50", "SyntheticVehicleRoutePort"));
            }
        }
        if (denied.Calls != 0) throw new InvalidOperationException("NetworkBoundaryInvoked");
        return output;
    }

    private static DemandDraft Demand(string source, string family)
    {
        var random = new StableRandom(source + "|" + family);
        var drivers = new List<음식배달수요기사입력>();
        var activeCount = 2 + random.Next(3); var newCount = 2 + random.Next(4);
        for (var i = 0; i < activeCount + newCount; i++)
        {
            var active = i < activeCount;
            var shiftAge = 120 + i * 11 + random.Next(10);
            var wait = !active && i == activeCount && family == "long-wait" ? 35 + random.Next(55) : 1 + random.Next(24);
            drivers.Add(new()
            {
                기사Id = $"synthetic-driver-{i + 1}", 기존활동여부 = active, 적격여부 = family != "no-eligible",
                운행시작시각Utc = FixedNow.AddMinutes(-shiftAge), 대기시작시각Utc = FixedNow.AddMinutes(-wait),
                마지막관측시각Utc = FixedNow.AddSeconds(-random.Next(241)),
                남은수행시간분 = active ? family == "shortage" ? 15 + random.Next(40) : random.Next(21) : 0,
                최근시간창배차수 = active ? 1 + random.Next(6) : random.Next(3)
            });
        }
        var sourceInput = new 음식배달수요참여입력
        {
            권역Key = "synthetic-region", 판정시각Utc = FixedNow,
            수요관측시각Utc = family == "missing" ? null : FixedNow.AddSeconds(-(family == "stale" ? 361 + random.Next(1200) : random.Next(241))),
            대기주문수 = family == "missing" ? null : family == "shortage" ? 15 + random.Next(36) : 1 + random.Next(2),
            최근시간창신규주문수 = family == "shortage" ? 3 + random.Next(10) : 0,
            관측시간창분 = 15m,
            향후수요예측건수 = family == "shortage" ? random.Next(10) : 0m,
            지연위험주문존재 = family == "shortage" && random.Next(3) == 0,
            적격활동기사부재 = family == "no-eligible", 기사목록 = drivers
        };
        var tokens = TokenMap(drivers.Select(x => x.기사Id), "D", source + "|" + family + "|driver-token");
        var settings = new 음식배달수요참여설정();
        var facts = new
        {
            planningWindowMinutes = settings.계획시간창분, hourlyTarget = settings.시간당목표처리건수,
            observationWindowMinutes = sourceInput.관측시간창분,
            demandObservationAgeMinutes = Age(sourceInput.수요관측시각Utc), waitingOrders = sourceInput.대기주문수,
            recentOrders = sourceInput.최근시간창신규주문수, forecastOrders = sourceInput.향후수요예측건수,
            delayRisk = sourceInput.지연위험주문존재, noEligibleActiveDriver = sourceInput.적격활동기사부재,
            drivers = Permute(drivers, source + "|" + family + "|driver-order").Select(x => new
            {
                driverToken = tokens[x.기사Id], eligible = x.적격여부, active = x.기존활동여부,
                shiftAgeMinutes = Age(x.운행시작시각Utc)!.Value, waitMinutes = Age(x.대기시작시각Utc)!.Value,
                observationAgeMinutes = Age(x.마지막관측시각Utc), remainingMinutes = x.남은수행시간분,
                recentAssignments = x.최근시간창배차수
            }).ToArray()
        };
        return new(sourceInput, facts, tokens);
    }

    private static void AssertDemand(string family, 음식배달수요참여결과 result)
    {
        var valid = family switch
        {
            "shortage" => result.데이터상태Code == "Current" && result.추가참여인원 > 0,
            "surplus" => result.데이터상태Code == "Current" && result.추가참여인원 == 0
                && result.기사판정목록.Any(x => x.상태Code == "WaitingForDemand") && result.보호활동기사수 > 0,
            "long-wait" => result.데이터상태Code == "Current" && result.기사판정목록.First().사유Code == "LongWaitOpportunity",
            "stale" => result.데이터상태Code == "Stale" && result.사유Code == "DemandSnapshotStale" && result.추가참여인원 == 0,
            "missing" => result.데이터상태Code == "Unknown" && result.사유Code == "DemandSnapshotUnavailable" && result.추가참여인원 == 0,
            "no-eligible" => result.데이터상태Code == "Current" && result.기사판정목록.All(x => x.상태Code == "Ineligible" && !x.신규제안허용),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("DemandFamilyAssertionFailed");
        if (family is "stale" or "missing" && result.기사판정목록.Any(x => x.상태Code != "ExistingGateRequired" || x.승격여부))
            throw new InvalidOperationException("UnknownDemandMustRequireExistingGates");
    }

    private static 멀티배차조합요청 Request(IReadOnlyList<JsonElement> selected, string seed)
    {
        var random = new StableRandom(seed + "|window");
        return new(selected.Select((x, i) =>
        {
            var pickup = Point(x, "pickup"); var destination = Point(x, "syntheticDestination");
            return new 픽업하차경로작업($"synthetic-order-{i + 1}", "branch:" + Branch(x), pickup,
                FixedNow.UtcDateTime.AddMinutes(12 + random.Next(7)), "synthetic-dropoff", destination,
                FixedNow.UtcDateTime.AddMinutes(40 + random.Next(11)), FixedNow.UtcDateTime.AddMinutes(3 + random.Next(5)),
                30m, 음식배달권정책.판정(new(pickup.Latitude, pickup.Longitude), null).배달권키,
                음식배달권정책.판정(new(destination.Latitude, destination.Longitude), null).배달권키);
        }).ToArray());
    }

    private static TrainingRow Row(string task, string family, JsonElement[] sources, object input, object target, string policyVersion, string routeBasis)
    {
        var ids = sources.Select(Id).ToArray(); var branches = sources.Select(Branch).Distinct(StringComparer.Ordinal).ToArray();
        var splits = branches.Select(TrainingSources.Split).Distinct(StringComparer.Ordinal).ToArray();
        if (splits.Length != 1) throw new InvalidOperationException("CrossFoldBundleForbidden");
        return new(1, "r51-" + TrainingSources.Hash(Encoding.UTF8.GetBytes(string.Join('|', ids) + "|" + task + "|" + family))[..24],
            task, family, ids, branches, splits[0], input, target,
            new { kind = "CurrentPolicyDraft", policyVersion, optimalityProven = false },
            new { synthetic = true, routeBasis, realCustomerLocationsUsed = 0 },
            new { status = "Pending", eligibleForTraining = false });
    }

    private static decimal? Age(DateTimeOffset? time) => time.HasValue ? (decimal)(FixedNow - time.Value).TotalSeconds / 60m : null;
    private static string Id(JsonElement record) => record.GetProperty("recordId").GetString()!;
    private static string Branch(JsonElement record) => record.GetProperty("branchId").GetString()!;
    private static 배차경로좌표 Point(JsonElement r, string key) => new(r.GetProperty(key).GetProperty("latitude").GetDecimal(), r.GetProperty(key).GetProperty("longitude").GetDecimal());
    private static T[] Permute<T>(IEnumerable<T> source, string seed)
    {
        var values = source.ToArray(); var random = new StableRandom(seed);
        for (var i = values.Length - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
        return values;
    }
    private static Dictionary<string, string> TokenMap(IEnumerable<string> source, string prefix, string seed)
        => Permute(source, seed).Select((id, index) => (id, token: prefix + (index + 1)))
            .ToDictionary(x => x.id, x => x.token, StringComparer.Ordinal);

    private sealed record DemandDraft(음식배달수요참여입력 Source, object Input, Dictionary<string, string> Tokens);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => FixedNow; }
    private sealed class DeniedDemandRead : I음식배달수요참여Service
    {
        public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken ct = default)
            => throw new InvalidOperationException("OperationalDemandReadForbidden");
    }
    private sealed class DeniedNetwork : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; throw new InvalidOperationException("TrainingNetworkForbidden"); }
    }
}

// Seeded byte-level PRNG avoids runtime-version-dependent Random sequences.
internal sealed class StableRandom(string seed)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes("r51|51|" + seed);
    private uint counter;
    public int Next(int maximum)
    {
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        var payload = new byte[key.Length + 4]; key.CopyTo(payload, 0);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(key.Length), counter++);
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(SHA256.HashData(payload)) % (uint)maximum);
    }
}

internal sealed class TrainingGeometry : I배차추천경로Service
{
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? target) => Task.FromResult<배차경로예상결과?>(null);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
    public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b)
        => Math.Abs(a.Latitude - b.Latitude) * 111m + Math.Abs(a.Longitude - b.Longitude) * 88m;
}
