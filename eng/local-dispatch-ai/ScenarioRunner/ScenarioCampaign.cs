using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.LocalDispatchScenarios;

internal sealed record CampaignSpec(int SchemaVersion, bool SyntheticOnly, int HorizonMinutes,
    int DrainMinutes, int Seed, ScenarioSpec[] Scenarios);
internal sealed record ScenarioSpec(string Id, string Title, int InitialDrivers, int InitialActiveDrivers,
    int InitialTasksPerActiveDriver, decimal ServiceMinutes, ArrivalWave[] ArrivalWaves,
    DriverWave[] DriverWaves, FaultSpec[] Faults);
internal sealed record ArrivalWave(int StartMinute, int EndMinute, int IntervalMinutes, int Count);
internal sealed record DriverWave(int Minute, int Count);
internal sealed record FaultSpec(string Kind, int StartMinute, int EndMinute, int DriverCount);
internal sealed record DriverMetric(string Id, int JoinedMinute, int Assigned, int Completed,
    int Pending, int NoWorkMinutes, int MaxWaitMinutes);
internal sealed record MinuteMetric(int Minute, int WaitingOrders, int UnfinishedOrders,
    int ProtectedDrivers, int AdmissionRecommendations, int WaitingDrivers,
    int EligibleDrivers, int DataFallback, int AiFallback);
internal sealed record RunResult(string ScenarioId, string Title, string Mode, decimal PlanningThroughput,
    int SubmittedOrders, int InitialAcceptedOrders, int CompletedOrders, int UnassignedOrders,
    int UnfinishedOrders, decimal AssignedWaitP95Minutes, int MaxAssignedWaitMinutes,
    int MaxOpenWaitMinutes, int OrdersWaitedTenMinutes, decimal CompletedLeadP95Minutes,
    decimal MaxCompletedLeadMinutes, int MaxUnfinishedAgeMinutes, int OrdersLeadOver45Minutes, int MaxConcurrentTasks,
    int MaxWaitingOrders, int MaxAdmissionRecommendations, int MaxProtectedDrivers,
    int LongWaitRecommendationTicks, int DelayExceptionTicks, int DataFallbackTicks,
    int AiFallbackTicks, int RawDriverAssignmentsSpread, string ReviewStatus,
    string DecisionDigest, DriverMetric[] Drivers, MinuteMetric[] Timeline, string[] Violations);

internal static class ScenarioCampaign
{
    public static readonly string[] Modes = ["policy_off", "observed", "oracle", "under", "over",
        "timeout", "invalid", "stale"];
    public static readonly decimal[] Throughputs = [3m, 3.5m, 4m];
    public static readonly DateTimeOffset Epoch = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Hash<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, Json));

    public static void Validate(CampaignSpec spec)
    {
        if (spec.SchemaVersion != 1 || !spec.SyntheticOnly || spec.HorizonMinutes is < 1 or > 240
            || spec.DrainMinutes is < 0 or > 240 || spec.Scenarios is null
            || spec.Scenarios.Length is < 1 or > 20)
            throw new ArgumentException("합성 시나리오 판본·시간창·개수를 확인하세요.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in spec.Scenarios)
        {
            if (!s.Id.StartsWith("SYN-", StringComparison.Ordinal) || !ids.Add(s.Id)
                || string.IsNullOrWhiteSpace(s.Title) || s.InitialDrivers is < 0 or > 100
                || s.InitialActiveDrivers < 0 || s.InitialActiveDrivers > s.InitialDrivers
                || s.InitialTasksPerActiveDriver is < 0 or > 3 || s.ServiceMinutes is < 1 or > 120
                || s.ArrivalWaves is null || s.DriverWaves is null || s.Faults is null)
                throw new ArgumentException("시나리오 또는 기사·수행시간 범위가 잘못됐습니다.");
            foreach (var wave in s.ArrivalWaves)
                if (wave.StartMinute < 0 || wave.EndMinute > spec.HorizonMinutes
                    || wave.EndMinute <= wave.StartMinute || wave.IntervalMinutes < 1
                    || wave.Count is < 1 or > 100)
                    throw new ArgumentException("주문 도착표 범위가 잘못됐습니다.");
            foreach (var wave in s.DriverWaves)
                if (wave.Minute < 0 || wave.Minute >= spec.HorizonMinutes || wave.Count is < 1 or > 100)
                    throw new ArgumentException("기사 유입표 범위가 잘못됐습니다.");
            if (s.InitialDrivers + s.DriverWaves.Sum(x => x.Count) > 100)
                throw new ArgumentException("가상 기사 100명 상한을 넘었습니다.");
            foreach (var fault in s.Faults)
                if (fault.Kind is not ("demand_stale" or "demand_unknown" or "gps_unavailable" or "new_offers_off")
                    || fault.StartMinute < 0 || fault.EndMinute > spec.HorizonMinutes + spec.DrainMinutes
                    || fault.EndMinute <= fault.StartMinute || fault.DriverCount is < 0 or > 100)
                    throw new ArgumentException("장애 표본 범위가 잘못됐습니다.");
        }
    }

    // 실제 주문·경로 엔진이 아니다. 고정 도착표/순차 수행표에서 제품의 참여 판단만 비교한다.
    public static RunResult Run(CampaignSpec campaign, ScenarioSpec spec, string mode, decimal throughput,
        Action<ScenarioPolicyObservation>? observer = null)
    {
        Validate(campaign);
        if (!Modes.Contains(mode, StringComparer.Ordinal) || throughput <= 0)
            throw new ArgumentException("비교 방식 또는 처리량이 잘못됐습니다.");
        var policy = new 음식배달수요참여Policy();
        var settings = new 음식배달수요참여설정 { 시간당목표처리건수 = throughput };
        var end = campaign.HorizonMinutes + campaign.DrainMinutes;
        var arrivals = Enumerable.Range(0, campaign.HorizonMinutes).Select(t => spec.ArrivalWaves
            .Where(w => t >= w.StartMinute && t < w.EndMinute && (t - w.StartMinute) % w.IntervalMinutes == 0)
            .Sum(w => w.Count)).ToArray();
        var drivers = new List<가상기사>();
        var orders = new List<가상주문>();
        var pending = new Queue<가상주문>();
        var timeline = new List<MinuteMetric>();
        var decisions = new List<object>();
        var violations = new HashSet<string>(StringComparer.Ordinal);
        int maxConcurrent = 0, longWaitTicks = 0, delayTicks = 0, dataFallbackTicks = 0, aiFallbackTicks = 0;

        void AddDrivers(int count, int minute, int activeCount = 0)
        {
            for (var i = 0; i < count; i++)
            {
                var driver = new 가상기사($"SYN-D{drivers.Count + 1:000}", minute,
                    i < activeCount ? minute : null);
                drivers.Add(driver);
                if (i >= activeCount) continue;
                for (var task = 0; task < spec.InitialTasksPerActiveDriver; task++)
                {
                    var order = new 가상주문($"SYN-I{orders.Count + 1:00000}", minute, true)
                    { AssignedMinute = minute, FinishedMinute = minute + (task + 1) * spec.ServiceMinutes };
                    orders.Add(order);
                    driver.Tasks.Add(order);
                }
                for (var extra = 1; extra < spec.InitialTasksPerActiveDriver; extra++)
                    driver.AcceptedMinutes.Add(minute);
            }
        }

        AddDrivers(spec.InitialDrivers, 0, spec.InitialActiveDrivers);
        maxConcurrent = drivers.Count == 0 ? 0 : drivers.Max(x => x.Tasks.Count);
        for (var minute = 0; minute < end; minute++)
        {
            foreach (var driver in drivers)
            {
                var finished = driver.Tasks.Where(x => x.FinishedMinute <= minute).ToArray();
                foreach (var order in finished)
                {
                    order.Completed = true;
                    driver.Completed++;
                    driver.Tasks.Remove(order);
                    driver.WaitAnchor = minute;
                }
            }
            foreach (var wave in spec.DriverWaves.Where(x => x.Minute == minute)) AddDrivers(wave.Count, minute);
            if (minute < arrivals.Length)
                for (var i = 0; i < arrivals[minute]; i++)
                {
                    var order = new 가상주문($"SYN-O{orders.Count + 1:00000}", minute, false);
                    orders.Add(order);
                    pending.Enqueue(order);
                }
            var faults = spec.Faults.Where(x => minute >= x.StartMinute && minute < x.EndMinute).ToArray();
            var noOffers = faults.Where(x => x.Kind is "gps_unavailable" or "new_offers_off")
                .Select(x => x.DriverCount).DefaultIfEmpty().Max();
            var eligible = drivers.Where((x, index) => index >= noOffers && x.Tasks.Count < 3).ToArray();
            var now = Epoch.AddMinutes(minute);
            var recent = Enumerable.Range(Math.Max(0, minute - 14), Math.Min(15, minute + 1))
                .Where(x => x < arrivals.Length).Sum(x => arrivals[x]);
            var future = Enumerable.Range(minute + 1, 60).Where(x => x < arrivals.Length).Sum(x => arrivals[x]);
            var signal = 모의예측(mode, future, now);
            if (signal.Fallback) aiFallbackTicks++;
            var input = new 음식배달수요참여입력
            {
                권역Key = spec.Id, 판정시각Utc = now,
                수요관측시각Utc = faults.Any(x => x.Kind == "demand_unknown") ? null
                    : faults.Any(x => x.Kind == "demand_stale") ? now.AddMinutes(-6) : now,
                대기주문수 = pending.Count, 최근시간창신규주문수 = recent,
                향후수요예측건수 = signal.Forecast,
                지연위험주문존재 = pending.TryPeek(out var head) && minute - head.ArrivedMinute >= 10,
                적격활동기사부재 = !eligible.Any(x => x.Active(minute)),
                기사목록 = drivers.Select((driver, index) => new 음식배달수요기사입력
                {
                    기사Id = driver.Id, 기존활동여부 = driver.Active(minute),
                    운행시작시각Utc = Epoch.AddMinutes(driver.JoinedMinute),
                    대기시작시각Utc = Epoch.AddMinutes(driver.WaitAnchor), 마지막관측시각Utc = now,
                    남은수행시간분 = driver.Tasks.Count == 0 ? 0 : driver.Tasks.Max(x => x.FinishedMinute!.Value) - minute,
                    최근시간창배차수 = driver.AcceptedMinutes.Count(x => x > minute - 60),
                    적격여부 = index >= noOffers && driver.Tasks.Count < 3
                }).ToArray()
            };
            var result = policy.평가(input, settings);
            // 외부 검토용 사본은 정책 평가 직후, 가상 주문 배정 전에 만든다. 원본 계약은 전달하지 않는다.
            observer?.Invoke(new(minute,
                pending.Select(x => minute - x.ArrivedMinute).DefaultIfEmpty().Max(),
                faults.Length != 0,
                JsonSerializer.SerializeToElement(input, Json),
                JsonSerializer.SerializeToElement(result, Json)));
            var dataFallback = result.데이터상태Code != 음식배달수요자료상태Code.최신;
            if (dataFallback && mode != "policy_off") dataFallbackTicks++;
            var acceptedIds = (mode == "policy_off" || dataFallback
                    ? eligible.OrderBy(x => x.AcceptedMinutes.Count(t => t > minute - 60))
                        .ThenBy(x => x.WaitAnchor).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id)
                    : result.기사판정목록.Where(x => x.신규제안허용).OrderBy(x => x.우선순위).Select(x => x.기사Id))
                .ToArray();
            if (mode != "policy_off" && !dataFallback)
                foreach (var driver in eligible.Where(x => x.Active(minute)))
                    if (!result.기사판정목록.Any(x => x.기사Id == driver.Id
                        && x.상태Code == 음식배달수요참여상태Code.기존활동보호 && x.신규제안허용))
                        violations.Add("eligible_incumbent_not_protected");
            if (mode != "policy_off" && result.기사판정목록.Any(x => x.사유Code == "LongWaitOpportunity")) longWaitTicks++;
            if (mode != "policy_off" && result.기사판정목록.Any(x => x.사유Code == "OrderDelayCapacityException")) delayTicks++;
            var assignedAtMinute = new List<string>();
            // 한 분에 기사별 최대 1건을 제안한 것으로 가정한다. 기존3건을 넘겨 채우지 않는다.
            foreach (var id in acceptedIds)
            {
                if (!pending.TryDequeue(out var order)) break;
                var driver = drivers.Single(x => x.Id == id);
                if (!eligible.Contains(driver) || driver.Tasks.Count >= 3)
                { violations.Add("ineligible_assignment"); pending.Enqueue(order); break; }
                order.AssignedMinute = minute;
                order.FinishedMinute = Math.Max(minute, driver.Tasks.Select(x => x.FinishedMinute!.Value)
                    .DefaultIfEmpty(minute).Max()) + spec.ServiceMinutes;
                driver.Tasks.Add(order);
                driver.AcceptedMinutes.Add(minute);
                driver.WaitAnchor = minute;
                driver.Assigned++;
                maxConcurrent = Math.Max(maxConcurrent, driver.Tasks.Count);
                assignedAtMinute.Add(order.Id + ":" + driver.Id);
            }
            foreach (var driver in drivers.Where(x => x.Tasks.Count == 0))
            {
                driver.NoWorkMinutes++;
                driver.MaxWaitMinutes = Math.Max(driver.MaxWaitMinutes, minute + 1 - driver.WaitAnchor);
            }
            decisions.Add(new { minute, assignedAtMinute, allowed = acceptedIds,
                protectedCount = mode == "policy_off" ? 0 : result.보호활동기사수,
                dataStatus = mode == "policy_off" ? "PolicyOffAssumption" : result.데이터상태Code });
            timeline.Add(new(minute, pending.Count, orders.Count(x => !x.Completed),
                mode == "policy_off" ? 0 : result.보호활동기사수,
                mode == "policy_off" ? 0 : result.추가참여인원,
                mode == "policy_off" ? 0 : result.기사판정목록.Count(x => x.상태Code == 음식배달수요참여상태Code.수요대기),
                eligible.Length, mode != "policy_off" && dataFallback ? 1 : 0, signal.Fallback ? 1 : 0));
        }
        // 마지막 분이 끝난 시각까지 완료된 업무도 포함한다.
        foreach (var driver in drivers)
            foreach (var order in driver.Tasks.Where(x => x.FinishedMinute <= end).ToArray())
            { order.Completed = true; driver.Completed++; driver.Tasks.Remove(order); }
        var submitted = orders.Where(x => !x.Initial).ToArray();
        var waits = submitted.Where(x => x.AssignedMinute.HasValue)
            .Select(x => x.AssignedMinute!.Value - x.ArrivedMinute).Order().ToArray();
        var unassigned = submitted.Count(x => !x.AssignedMinute.HasValue);
        var unfinished = orders.Count(x => !x.Completed);
        if (maxConcurrent > 3) violations.Add("concurrency_limit_exceeded");
        if (orders.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != orders.Count)
            violations.Add("duplicate_order");
        if (orders.Count(x => x.Completed) + unfinished != orders.Count
            || unassigned != pending.Count || unfinished != pending.Count + drivers.Sum(x => x.Tasks.Count))
            violations.Add("order_conservation_failed");
        var pendingIds = pending.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var assignedCounts = drivers.SelectMany(x => x.Tasks).GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        if (orders.Any(x => (x.Completed ? 1 : 0) + (pendingIds.Contains(x.Id) ? 1 : 0)
                + assignedCounts.GetValueOrDefault(x.Id) != 1))
            violations.Add("order_membership_not_unique");
        var maxOpenWait = pending.Select(x => end - x.ArrivedMinute).DefaultIfEmpty().Max();
        var delayed = submitted.Count(x => (x.AssignedMinute ?? end) - x.ArrivedMinute >= 10);
        var completedLead = submitted.Where(x => x.Completed)
            .Select(x => x.FinishedMinute!.Value - x.ArrivedMinute).Order().ToArray();
        var unfinishedAge = submitted.Where(x => !x.Completed).Select(x => end - x.ArrivedMinute).DefaultIfEmpty().Max();
        // 45분은 이 실험의 검토용 가정이며 상품별 SLA나 자동 취소/운영 설정이 아니다.
        var longLead = submitted.Count(x => (x.Completed ? x.FinishedMinute!.Value : end) - x.ArrivedMinute > 45);
        var spreadDrivers = drivers.Where(x => x.JoinedMinute < campaign.HorizonMinutes).ToArray();
        return new(spec.Id, spec.Title, mode, throughput, submitted.Length, orders.Count(x => x.Initial),
            orders.Count(x => x.Completed), unassigned, unfinished,
            waits.Length == 0 ? 0 : waits[(int)Math.Ceiling(waits.Length * 0.95) - 1],
            waits.DefaultIfEmpty().Max(), maxOpenWait, delayed,
            completedLead.Length == 0 ? 0 : Math.Round(completedLead[(int)Math.Ceiling(completedLead.Length * 0.95) - 1], 3),
            Math.Round(completedLead.DefaultIfEmpty().Max(), 3), unfinishedAge, longLead, maxConcurrent,
            timeline.Select(x => x.WaitingOrders).DefaultIfEmpty().Max(),
            timeline.Select(x => x.AdmissionRecommendations).DefaultIfEmpty().Max(),
            timeline.Select(x => x.ProtectedDrivers).DefaultIfEmpty().Max(),
            longWaitTicks, delayTicks, dataFallbackTicks, aiFallbackTicks,
            spreadDrivers.Length == 0 ? 0 : spreadDrivers.Max(x => x.Assigned) - spreadDrivers.Min(x => x.Assigned),
            violations.Count > 0 ? "INVARIANT_FAILED" : unfinished > 0 || delayed > 0 || longLead > 0 ? "REVIEW_REQUIRED" : "WITHIN_ASSUMED_LIMITS",
            Hash(decisions), drivers.Select(x => new DriverMetric(x.Id, x.JoinedMinute, x.Assigned, x.Completed,
                x.Tasks.Count, x.NoWorkMinutes, x.MaxWaitMinutes)).ToArray(), timeline.ToArray(), violations.Order().ToArray());
    }

    // 고정 fixture 신호일 뿐 모델 호출이 아니다. stale/invalid/timeout은 실제 대기 없이 주입한다.
    internal static (decimal? Forecast, bool Fallback) 모의예측(string mode, int future, DateTimeOffset now)
    {
        if (mode is "observed" or "policy_off") return (null, false);
        if (mode == "timeout") return (null, true);
        decimal value = mode switch { "oracle" or "stale" => future, "under" => 0, "over" => future * 2 + 10, _ => -1 };
        var observedAt = mode == "stale" ? now.AddMinutes(-6) : now;
        if (value < 0 || value > 10000 || observedAt > now || now - observedAt > TimeSpan.FromMinutes(5))
            return (null, true);
        return (value, false);
    }

    private sealed class 가상기사(string id, int joinedMinute, int? initialActivity)
    {
        public string Id { get; } = id;
        public int JoinedMinute { get; } = joinedMinute;
        public int WaitAnchor { get; set; } = joinedMinute;
        public List<가상주문> Tasks { get; } = [];
        public List<int> AcceptedMinutes { get; } = initialActivity.HasValue ? [initialActivity.Value] : [];
        public int Assigned { get; set; }
        public int Completed { get; set; }
        public int NoWorkMinutes { get; set; }
        public int MaxWaitMinutes { get; set; }
        public bool Active(int minute) => Tasks.Count > 0 || AcceptedMinutes.Any(x => x > minute - 60);
    }
    private sealed class 가상주문(string id, int arrivedMinute, bool initial)
    {
        public string Id { get; } = id;
        public int ArrivedMinute { get; } = arrivedMinute;
        public bool Initial { get; } = initial;
        public int? AssignedMinute { get; set; }
        public decimal? FinishedMinute { get; set; }
        public bool Completed { get; set; }
    }
}
