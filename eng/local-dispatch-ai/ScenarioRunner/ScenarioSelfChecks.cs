using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.LocalDispatchScenarios;

internal sealed record ScenarioCheck(string Name, bool Passed, string Detail);

// 별도 서버 시험이 아니라 오프라인 모의 실행 자체와 제품의 순수 참여 정책을 점검한다.
internal static class ScenarioSelfChecks
{
    public static ScenarioCheck[] VerifyAdvisory(CampaignSpec spec, RunResult[] results,
        IReadOnlyDictionary<string, List<ScenarioPolicyObservation>> observations,
        ScenarioAdvisoryCase[] cases)
    {
        var checks = new List<ScenarioCheck>();
        void Check(string name, Func<bool> assertion, string detail)
        {
            try { checks.Add(new(name, assertion(), detail)); }
            catch (Exception error)
            { checks.Add(new(name, false, detail + " · 예외 유형: " + error.GetType().Name)); }
        }
        Check("advisory.capture_every_observed_policy_tick", () =>
            observations.Keys.Order().SequenceEqual(spec.Scenarios.Select(x => x.Id).Order())
            && observations.All(pair => pair.Value.Select(x => x.Minute)
                .SequenceEqual(Enumerable.Range(0, spec.HorizonMinutes + spec.DrainMinutes))),
            "관측 방식·3.5건/h의 모든 분을 정책 평가 직후 수집하며 배정 루프의 결과와 구분한다.");
        var replayDetail = string.Empty;
        Check("advisory.immutable_typed_snapshot_replays_actual_policy", () =>
        {
            var policy = new 음식배달수요참여Policy();
            return observations.All(pair => pair.Value.All(snapshot =>
            {
                var input = snapshot.Input.Deserialize<음식배달수요참여입력>(ScenarioCampaign.Json)!;
                var captured = snapshot.Decision.Deserialize<음식배달수요참여결과>(ScenarioCampaign.Json)!;
                var expected = policy.평가(input, new 음식배달수요참여설정
                    { 시간당목표처리건수 = ScenarioAdvisoryEvidence.PlanningThroughput });
                // JsonElement은 날짜의 '+' 등 문자열을 다시 escape할 수 있다. 동일 계약 타입으로 값을 비교한다.
                var matched = input.권역Key == pair.Key
                    && input.판정시각Utc == ScenarioCampaign.Epoch.AddMinutes(snapshot.Minute)
                    && ScenarioCampaign.Hash(expected) == ScenarioCampaign.Hash(captured);
                if (!matched) replayDetail = $" · 최초 차이 {pair.Key}/{snapshot.Minute}: "
                    + $"expected={ScenarioCampaign.Hash(expected)}, captured={ScenarioCampaign.Hash(captured)}, "
                    + $"regionMatches={input.권역Key == pair.Key}, timeMatches={input.판정시각Utc == ScenarioCampaign.Epoch.AddMinutes(snapshot.Minute)}";
                return matched;
            }));
        }, "내보낸 실제 계약 사본으로 정책을 독립 재평가해 각 판정과 비교한다.");
        if (replayDetail.Length != 0)
            checks[^1] = checks[^1] with { Detail = checks[^1].Detail + replayDetail };
        Check("advisory.no_future_forecast_or_real_driver_ids", () => observations.All(pair =>
            pair.Value.All(snapshot => snapshot.Input.GetProperty("향후수요예측건수").ValueKind == JsonValueKind.Null
                && snapshot.Input.GetProperty("기사목록").EnumerateArray().All(driver =>
                    Regex.IsMatch(driver.GetProperty("기사Id").GetString()!, "^SYN-D[0-9]{3}$",
                        RegexOptions.CultureInvariant)))) && cases.All(x => x.ScenarioId.StartsWith("SYN-", StringComparison.Ordinal)),
            "현재 판단 입력에는 알려진 미래 예측값을 넣지 않고 합성 기사 식별값만 사용한다.");
        Check("advisory.case_bound_unique_safe_ids_and_order", () => cases.Length is > 0
            && cases.Length <= spec.Scenarios.Length * 3
            && cases.Select(x => x.CaseId).Distinct(StringComparer.Ordinal).Count() == cases.Length
            && spec.Scenarios.All(s => cases.Count(x => x.ScenarioId == s.Id) is >= 1 and <= 3)
            && cases.All(x => x.PlanningThroughput == 3.5m && x.CaseId == x.ScenarioId + "-"
                + x.Minute.ToString("000", CultureInfo.InvariantCulture)
                && Regex.IsMatch(x.CaseId, "^[A-Z0-9-]+-[0-9]{3}$", RegexOptions.CultureInvariant))
            && cases.GroupBy(x => x.ScenarioId).All(group => group.Select(x => x.Minute).SequenceEqual(group.Select(x => x.Minute).Order())),
            "시나리오별 최대 3개를 중복 없이 시간순으로 고르며 파일명에 사용할 안전한 caseId를 유지한다.");
        Check("advisory.start_and_maximum_wait_are_present", () => spec.Scenarios.All(s =>
        {
            var snapshots = observations[s.Id];
            var maxWait = snapshots.Max(x => x.OldestWaitingMinutes);
            var maxCount = snapshots.Where(x => x.OldestWaitingMinutes == maxWait)
                .Max(x => x.Input.GetProperty("대기주문수").GetInt32());
            var earliest = snapshots.Where(x => x.OldestWaitingMinutes == maxWait
                && x.Input.GetProperty("대기주문수").GetInt32() == maxCount).Min(x => x.Minute);
            var selected = cases.Where(x => x.ScenarioId == s.Id).ToArray();
            return selected.Any(x => x.Minute == 0 && x.SelectionReasons.Contains("start"))
                && selected.Any(x => x.Minute == earliest
                    && x.SelectionReasons.Contains("maximum_open_assignment_wait"));
        }), "시작과 최대 열린 배정 대기를 포함한다. 동률은 대기건수·가장 이른 분으로 고정한다.");
        Check("advisory.explicit_fault_is_not_hidden_by_normal_change", () => spec.Scenarios.All(s =>
        {
            var first = s.Faults.Select(x => x.StartMinute).DefaultIfEmpty(-1).Min();
            return first == -1 || cases.Any(x => x.ScenarioId == s.Id && x.Minute == first
                && x.SelectionReasons.Contains("first_explicit_fault"));
        }), "정상 주문 증감의 판정 변화보다 명시 장애의 최초 시점을 우선해 검토한다.");
        Check("advisory.selected_input_and_decision_are_exact_capture", () => cases.All(item =>
        {
            var captured = observations[item.ScenarioId].Single(x => x.Minute == item.Minute);
            return ScenarioCampaign.Hash(item.Input) == ScenarioCampaign.Hash(captured.Input)
                && ScenarioCampaign.Hash(item.Decision) == ScenarioCampaign.Hash(captured.Decision);
        }), "선별 과정은 원본 정책 입력·결과에 새로운 상태나 미래값을 추가하지 않는다.");
        Check("advisory.alternatives_are_existing_whole_run_metrics", () => cases.All(item =>
            item.Alternatives.Select(x => x.AlternativeId).SequenceEqual(new[] { "observed", "policy_off", "over", "oracle" })
            && item.Alternatives.All(alternative =>
            {
                var run = results.Single(x => x.ScenarioId == item.ScenarioId
                    && x.PlanningThroughput == item.PlanningThroughput && x.Mode == alternative.AlternativeId);
                return alternative.Kind == (run.Mode == "oracle" ? "known_future_upper_bound" : "synthetic_comparison")
                    && alternative.AssignedWaitP95Minutes == run.AssignedWaitP95Minutes
                    && alternative.CompletedLeadP95Minutes == run.CompletedLeadP95Minutes
                    && alternative.UnassignedOrders == run.UnassignedOrders
                    && alternative.UnfinishedOrders == run.UnfinishedOrders
                    && alternative.ReviewStatus == run.ReviewStatus;
            })), "대안은 같은 전체 합성 실행의 실측 지표이며 특정 시점에서 새로 예측한 ETA나 모델 성능이 아니다.");
        Check("advisory.selection_and_serialized_cases_are_deterministic", () =>
            ScenarioCampaign.Hash(ScenarioAdvisoryEvidence.Select(spec, results, observations)) == ScenarioCampaign.Hash(cases),
            "같은 정책 평가 사본으로 선별·직렬화를 반복해 사례 해시를 비교한다.");
        return checks.ToArray();
    }

    public static ScenarioCheck[] Verify(CampaignSpec spec, RunResult[] results)
    {
        var checks = VerifyCore(spec, results).ToList();
        const string regressionName = "campaign.single_scenario_can_return_serializable_checks";
        const string regressionDetail = "원래 첫 사례 하나만 남겨도 고정 경계 ID를 요구하지 않고 점검 반환·결과 JSON 직렬화까지 진행한다. Verify 재귀 호출은 하지 않는다.";
        try
        {
            var reduced = spec with { Scenarios = [spec.Scenarios[0]] };
            var reducedResults = (from throughput in ScenarioCampaign.Throughputs
                                  from mode in ScenarioCampaign.Modes
                                  select ScenarioCampaign.Run(reduced, reduced.Scenarios[0], mode, throughput)).ToArray();
            var reducedChecks = VerifyCore(reduced, reducedResults);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { checks = reducedChecks, results = reducedResults }, ScenarioCampaign.Json);
            using var document = JsonDocument.Parse(payload);
            var passed = reducedChecks.All(x => x.Passed)
                && document.RootElement.GetProperty("checks").GetArrayLength() == reducedChecks.Length
                && document.RootElement.GetProperty("results").GetArrayLength()
                    == ScenarioCampaign.Modes.Length * ScenarioCampaign.Throughputs.Length;
            checks.Add(new(regressionName, passed, regressionDetail));
        }
        catch (Exception error)
        {
            checks.Add(new(regressionName, false, regressionDetail + " · 예외 유형: " + error.GetType().Name));
        }
        return checks.ToArray();
    }

    private static ScenarioCheck[] VerifyCore(CampaignSpec spec, RunResult[] results)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(results);
        var checks = new List<ScenarioCheck>();

        void Check(string name, Func<bool> assertion, string detail)
        {
            try { checks.Add(new(name, assertion(), detail)); }
            catch (Exception error)
            {
                checks.Add(new(name, false, detail + " · 예외 유형: " + error.GetType().Name));
            }
        }

        var expectedKeys = (from scenario in spec.Scenarios
                            from throughput in ScenarioCampaign.Throughputs
                            from mode in ScenarioCampaign.Modes
                            select (scenario.Id, throughput, mode)).ToHashSet();
        var actualKeys = results.Select(x => (x.ScenarioId, x.PlanningThroughput, x.Mode)).ToArray();
        Check("campaign.complete_unique_run_grid", () => results.Length == expectedKeys.Count
            && actualKeys.Distinct().Count() == expectedKeys.Count && actualKeys.All(expectedKeys.Contains),
            $"사례·계획 처리량·비교 방식의 전체 격자 {expectedKeys.Count}개를 누락·중복 없이 실행했는지 확인한다.");

        var invariantFailures = results.Where(x => x.Violations.Length != 0).Select(Key).ToArray();
        checks.Add(new("runs.no_reported_invariant_violations", invariantFailures.Length == 0,
            Summary(results.Length, invariantFailures, "실행 내부 불변식")));
        var capFailures = results.Where(x => x.MaxConcurrentTasks is < 0 or > 3).Select(Key).ToArray();
        checks.Add(new("runs.initial_and_new_work_within_three", capFailures.Length == 0,
            Summary(results.Length, capFailures, "초기 수락 업무와 신규 업무를 포함한 동시 수행 3건 상한")));
        Check("runs.submitted_initial_and_driver_order_conservation", () => results.All(run =>
        {
            var scenario = spec.Scenarios.Single(x => x.Id == run.ScenarioId);
            var expectedSubmitted = scenario.ArrivalWaves.Sum(w =>
                ((w.EndMinute - w.StartMinute - 1) / w.IntervalMinutes + 1) * w.Count);
            var expectedInitial = scenario.InitialActiveDrivers * scenario.InitialTasksPerActiveDriver;
            return run.SubmittedOrders == expectedSubmitted && run.InitialAcceptedOrders == expectedInitial
                && run.CompletedOrders >= 0 && run.UnassignedOrders >= 0 && run.UnfinishedOrders >= 0
                && run.SubmittedOrders + run.InitialAcceptedOrders == run.CompletedOrders + run.UnfinishedOrders
                && run.UnassignedOrders <= run.UnfinishedOrders
                && run.Drivers.Length == scenario.InitialDrivers + scenario.DriverWaves.Sum(x => x.Count)
                && run.Drivers.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == run.Drivers.Length
                && run.Drivers.Sum(x => x.Assigned) == run.SubmittedOrders - run.UnassignedOrders
                && run.Drivers.Sum(x => x.Completed) == run.CompletedOrders
                && run.Drivers.Sum(x => x.Pending) == run.UnfinishedOrders - run.UnassignedOrders
                && run.Drivers.Select((driver, index) => driver.Assigned >= 0 && driver.Completed >= 0
                    && driver.Pending is >= 0 and <= 3
                    && driver.Assigned + (index < scenario.InitialActiveDrivers
                        ? scenario.InitialTasksPerActiveDriver : 0) == driver.Completed + driver.Pending).All(x => x);
        }), $"{results.Length}개 실행의 도착표 건수·초기 수락·완료·미완료·미배정과 기사별 보존을 독립 대조한다.");

        var lookup = results.ToLookup(x => (x.ScenarioId, x.PlanningThroughput, x.Mode));
        foreach (var scenario in spec.Scenarios)
        foreach (var throughput in ScenarioCampaign.Throughputs)
        foreach (var mode in new[] { "under", "timeout", "invalid", "stale" })
        {
            var baseline = lookup[(scenario.Id, throughput, "observed")].ToArray();
            var compared = lookup[(scenario.Id, throughput, mode)].ToArray();
            Check($"observed_equivalence.{scenario.Id}.{Number(throughput)}.{mode}", () =>
            {
                if (baseline.Length != 1 || compared.Length != 1) return false;
                var reference = baseline[0];
                var candidate = compared[0];
                var expectedAiFallback = mode == "under" ? 0 : spec.HorizonMinutes + spec.DrainMinutes;
                // 모드 이름과 AI 장애 진단만 다르다. 주문·기사·시간표·정책 진단은 전부 같아야 한다.
                var normalized = candidate with
                {
                    Mode = "observed", AiFallbackTicks = 0,
                    Timeline = candidate.Timeline.Select(x => x with { AiFallback = 0 }).ToArray()
                };
                return candidate.AiFallbackTicks == expectedAiFallback
                    && candidate.DecisionDigest == reference.DecisionDigest
                    && ScenarioCampaign.Hash(normalized) == ScenarioCampaign.Hash(reference);
            }, "관측 바닥보다 낮은 예측 또는 응답 없음·무효·만료는 관측 기준선의 결정·주문·기사 결과를 유지한다.");
        }

        // 각 실행은 독립 상태에서 다시 시작한다. 결과 전체의 해시를 비교하고 시간 측정값은 넣지 않는다.
        var replayFailures = new List<string>();
        foreach (var run in results)
        {
            try
            {
                var scenario = spec.Scenarios.Single(x => x.Id == run.ScenarioId);
                var replay = ScenarioCampaign.Run(spec, scenario, run.Mode, run.PlanningThroughput);
                if (ScenarioCampaign.Hash(replay) != ScenarioCampaign.Hash(run)) replayFailures.Add(Key(run));
            }
            catch (Exception error) { replayFailures.Add(Key(run) + ":" + error.GetType().Name); }
        }
        checks.Add(new("runs.all_full_results_replay_identically", replayFailures.Count == 0,
            Summary(results.Length, replayFailures, "독립 재실행의 전체 결과 해시")));

        var zeroSpec = new ScenarioSpec("SYN-ZERO-DEMAND-LONG-WAIT", "무수요 독립 경계 표본",
            12, 0, 0, 17.143m, [], [], []);
        Check("zero_demand.recommendation_is_not_assignment", () =>
        {
            var zeroDemand = BoundaryRuns(spec, results, zeroSpec);
            return zeroDemand.Length == ScenarioCampaign.Modes.Length * ScenarioCampaign.Throughputs.Length
                && zeroDemand.All(run => run.SubmittedOrders == 0 && run.InitialAcceptedOrders == 0
                && run.CompletedOrders == 0 && run.UnassignedOrders == 0 && run.UnfinishedOrders == 0
                && run.Drivers.All(x => x.Assigned == 0 && x.Completed == 0 && x.Pending == 0
                    && x.NoWorkMinutes == 240 - x.JoinedMinute
                    && x.MaxWaitMinutes == x.NoWorkMinutes)
                && (run.Mode == "policy_off"
                    ? run.LongWaitRecommendationTicks == 0 && run.MaxAdmissionRecommendations == 0
                    : run.LongWaitRecommendationTicks > 0 && run.MaxAdmissionRecommendations > 0));
        }, "독립 경계 표본(120+120분·기사 12명)의 무수요 장기 대기 권고는 배정·완료를 만들지 않고 유휴시간도 합류 시점과 일치한다.");

        var initialCap = new ScenarioSpec("SYN-INITIAL-ACTIVE-CAP-THREE", "초기 수행 상한 독립 경계 표본",
            6, 6, 3, 17.143m, [new(0, 36, 3, 1)], [], []);
        Check("initial_cap.no_fourth_assignment_before_completion", () =>
        {
            var cappedRuns = BoundaryRuns(spec, results, initialCap);
            return cappedRuns.Length == ScenarioCampaign.Modes.Length * ScenarioCampaign.Throughputs.Length
                && cappedRuns.All(run => run.InitialAcceptedOrders == initialCap.InitialActiveDrivers * 3
                && run.MaxConcurrentTasks == 3
                && run.Timeline.Where(x => x.Minute < initialCap.ServiceMinutes).All(x =>
                    x.EligibleDrivers == 0 && x.UnfinishedOrders
                        == run.InitialAcceptedOrders + x.WaitingOrders));
        }, "독립 경계 표본(120+120분·기사 6명×초기 3건·17.143분 순차 수행)에서 첫 완료 전 추가 배정을 받지 않는다.");

        VerifyDomain(Check);
        Check("input.reject_non_synthetic_campaign", () => ThrowsArgument(() =>
            ScenarioCampaign.Validate(spec with { SyntheticOnly = false })), "실자료 사용 캠페인은 허용하지 않는다.");
        Check("input.reject_unsupported_mode", () => ThrowsArgument(() =>
            ScenarioCampaign.Run(spec, spec.Scenarios[0], "unsupported", 3.5m)), "정해지지 않은 비교 방식은 거절한다.");
        Check("input.reject_zero_arrival_interval", () =>
        {
            var invalidScenario = spec.Scenarios[0] with { ArrivalWaves = [new(0, 1, 0, 1)] };
            return ThrowsArgument(() => ScenarioCampaign.Validate(spec with { Scenarios = [invalidScenario] }));
        }, "주문 도착 간격 0은 실행 전에 거절한다.");
        return checks.ToArray();
    }

    private static RunResult[] BoundaryRuns(CampaignSpec campaign, RunResult[] results, ScenarioSpec boundary)
    {
        // ID만 같은 임의 입력을 고정 경계 표본으로 오인하지 않는다. 시간창·표본 전체가 같을 때만 재사용한다.
        var matching = campaign.Scenarios.FirstOrDefault(x => x.Id == boundary.Id);
        if (campaign.HorizonMinutes == 120 && campaign.DrainMinutes == 120 && matching is not null
            && ScenarioCampaign.Hash(matching) == ScenarioCampaign.Hash(boundary))
        {
            var reusable = results.Where(x => x.ScenarioId == boundary.Id).ToArray();
            if (reusable.Length == ScenarioCampaign.Modes.Length * ScenarioCampaign.Throughputs.Length
                && reusable.Select(x => (x.PlanningThroughput, x.Mode)).Distinct().Count() == reusable.Length
                && reusable.All(x => ScenarioCampaign.Throughputs.Contains(x.PlanningThroughput)
                    && ScenarioCampaign.Modes.Contains(x.Mode, StringComparer.Ordinal)))
                return reusable;
        }
        var independent = new CampaignSpec(1, true, 120, 120, 17, [boundary]);
        return (from throughput in ScenarioCampaign.Throughputs
                from mode in ScenarioCampaign.Modes
                select ScenarioCampaign.Run(independent, boundary, mode, throughput)).ToArray();
    }

    private static void VerifyDomain(Action<string, Func<bool>, string> check)
    {
        var policy = new 음식배달수요참여Policy();
        var now = ScenarioCampaign.Epoch.AddMinutes(60);
        음식배달수요기사입력 Driver(string id, bool existing = false, int waited = 10, bool eligible = true)
            => new()
            {
                기사Id = id, 기존활동여부 = existing, 적격여부 = eligible,
                운행시작시각Utc = now.AddMinutes(-waited), 대기시작시각Utc = now.AddMinutes(-waited),
                마지막관측시각Utc = now, 남은수행시간분 = 0
            };
        음식배달수요참여입력 Input(params 음식배달수요기사입력[] drivers)
            => new()
            {
                권역Key = "SYN-DOMAIN-CHECK", 판정시각Utc = now, 수요관측시각Utc = now,
                대기주문수 = 1, 최근시간창신규주문수 = 0, 관측시간창분 = 15, 기사목록 = drivers
            };

        check("domain.latest_duplicate_and_order_are_deterministic", () =>
        {
            var current = Driver("SYN-EXISTING", true);
            current.남은수행시간분 = 30;
            current.최근시간창배차수 = 2;
            var old = Driver(current.기사Id, eligible: false);
            old.마지막관측시각Utc = now.AddMinutes(-1);
            var olderWait = Driver("SYN-WAIT-A", waited: 20);
            var newerWait = Driver("SYN-WAIT-B");
            var baseline = policy.평가(Input(current, olderWait, newerWait));
            var reversed = policy.평가(Input(newerWait, old, olderWait, current, old));
            var permuted = policy.평가(Input(old, current, newerWait, olderWait));
            return baseline.기사판정목록.Count == 3
                && reversed.보호활동기사수 == 1
                && reversed.기사판정목록.Single(x => x.기사Id == current.기사Id).신규제안허용
                && ScenarioCampaign.Hash(baseline) == ScenarioCampaign.Hash(reversed)
                && ScenarioCampaign.Hash(baseline) == ScenarioCampaign.Hash(permuted);
        }, "같은 기사의 오래된 부적격 관측은 최신 관측을 덮지 않고 입력 순서·중복으로 인원과 순위가 달라지지 않는다.");

        check("domain.same_timestamp_duplicate_is_conservative", () =>
        {
            var eligible = Driver("SYN-DUPLICATE");
            var ineligible = Driver(eligible.기사Id, eligible: false);
            var first = policy.평가(Input(eligible, ineligible));
            var reverse = policy.평가(Input(ineligible, eligible));
            return first.기사판정목록.Count == 1 && !first.기사판정목록[0].신규제안허용
                && first.기사판정목록[0].상태Code == 음식배달수요참여상태Code.적격조건미충족
                && ScenarioCampaign.Hash(first) == ScenarioCampaign.Hash(reverse);
        }, "같은 시각 적격 충돌은 더 제한적인 관측으로 한 번만 판정한다.");

        foreach (var stale in new[] { false, true })
        {
            check(stale ? "domain.stale_hands_back_to_existing_gates" : "domain.unknown_hands_back_to_existing_gates", () =>
            {
                var input = Input(Driver("SYN-EXISTING", true), Driver("SYN-NEW"), Driver("SYN-BLOCKED", eligible: false));
                input.수요관측시각Utc = stale ? now.AddMinutes(-6) : null;
                var result = policy.평가(input);
                return result.데이터상태Code == (stale ? 음식배달수요자료상태Code.만료 : 음식배달수요자료상태Code.미확인)
                    && result.목표활동인원 is null && result.예상업무량건수 is null
                    && result.기사판정목록.Where(x => x.기사Id != "SYN-BLOCKED").All(x =>
                        x.신규제안허용 && x.상태Code == 음식배달수요참여상태Code.기존관문재검사)
                    && !result.기사판정목록.Single(x => x.기사Id == "SYN-BLOCKED").신규제안허용;
            }, "자료 장애를 수요 0건으로 바꾸지 않으며 독립적인 부적격 조건은 유지한다.");
        }

        check("domain.over_forecast_never_revokes_eligible_incumbent", () =>
        {
            var input = Input(Driver("SYN-PROTECTED", true), Driver("SYN-BLOCKED", true, eligible: false), Driver("SYN-NEW"));
            input.대기주문수 = 0;
            var baseline = policy.평가(input);
            input.향후수요예측건수 = 500;
            var over = policy.평가(input);
            var protectedDriver = over.기사판정목록.Single(x => x.기사Id == "SYN-PROTECTED");
            return protectedDriver.신규제안허용 && !protectedDriver.승격여부
                && protectedDriver.상태Code == 음식배달수요참여상태Code.기존활동보호
                && !over.기사판정목록.Single(x => x.기사Id == "SYN-BLOCKED").신규제안허용
                && over.보호활동기사수 == baseline.보호활동기사수
                && over.목표활동인원 > baseline.목표활동인원;
        }, "과대 예측도 기존 적격 활동을 취소하거나 독립 부적격 기사에게 권한을 주지 못한다.");

        check("domain.negative_forecast_is_unknown_not_zero_demand", () =>
        {
            var input = Input(Driver("SYN-NEW"));
            input.향후수요예측건수 = -1;
            var result = policy.평가(input);
            var signal = ScenarioCampaign.모의예측("invalid", 42, now);
            return result.데이터상태Code == 음식배달수요자료상태Code.미확인
                && result.목표활동인원 is null && result.사유Code == "DemandSnapshotUnavailable"
                && result.기사판정목록.Single().상태Code == 음식배달수요참여상태Code.기존관문재검사
                && result.기사판정목록.Single().신규제안허용 && signal.Forecast is null && signal.Fallback;
        }, "음수 예측은 최신 수요로 사용하지 않고 정책 인계 또는 예측 신호 폐기로 처리한다.");

        check("domain.observation_and_forecast_are_not_added_twice", () =>
        {
            var input = Input();
            input.대기주문수 = 2;
            input.최근시간창신규주문수 = 7;
            input.향후수요예측건수 = 30;
            return policy.평가(input).예상업무량건수 == 32m;
        }, "대기 2건과 max(15분 관측 7건의 60분 환산 28건, 미래 예측 30건)는 32건이다.");

        check("domain.long_wait_boundary_adds_one_without_eviction", () =>
        {
            var before = policy.평가(Input(Driver("SYN-EXISTING", true), Driver("SYN-WAIT", waited: 29)));
            var after = policy.평가(Input(Driver("SYN-EXISTING", true), Driver("SYN-WAIT", waited: 30)));
            return !before.기사판정목록.Single(x => x.기사Id == "SYN-WAIT").신규제안허용
                && after.추가참여인원 == 1
                && after.기사판정목록[0].사유Code == "LongWaitOpportunity"
                && after.기사판정목록.Single(x => x.기사Id == "SYN-EXISTING").신규제안허용;
        }, "29분에는 대기하고 30분에는 추가 기회를 주되 기존 적격 활동도 유지한다.");

        check("domain.delay_exception_opens_additional_opportunity", () =>
        {
            var input = Input(Driver("SYN-EXISTING", true), Driver("SYN-NEW"));
            var before = policy.평가(input);
            input.지연위험주문존재 = true;
            var after = policy.평가(input);
            return before.추가참여인원 == 0 && after.추가참여인원 == 1
                && after.기사판정목록.Single(x => x.기사Id == "SYN-NEW").사유Code == "OrderDelayCapacityException";
        }, "기존 계획 여력이 충분하더라도 주문 지연 시 신규 기사 기회를 추가한다.");

        check("domain.planning_capacity_does_not_mutate_input_or_service", () =>
        {
            var existing = Driver("SYN-EXISTING", true);
            existing.남은수행시간분 = 40;
            var input = Input(existing, Driver("SYN-NEW"));
            input.대기주문수 = 5;
            var before = ScenarioCampaign.Hash(input);
            var lower = policy.평가(input, new() { 시간당목표처리건수 = 3 });
            var higher = policy.평가(input, new() { 시간당목표처리건수 = 4 });
            return lower.목표활동인원 > higher.목표활동인원
                && ScenarioCampaign.Hash(input) == before && existing.남은수행시간분 == 40;
        }, "계획 처리량 변경은 권고에만 작용하며 입력 업무·잔여 수행시간을 바꾸지 않는다.");
    }

    private static bool ThrowsArgument(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Key(RunResult run) => $"{run.ScenarioId}/{Number(run.PlanningThroughput)}/{run.Mode}";
    private static string Summary(int count, IEnumerable<string> failures, string subject)
    {
        var failed = failures.ToArray();
        return $"{count}개 모의 실행의 {subject} 확인 · 불일치 {failed.Length}개"
            + (failed.Length == 0 ? string.Empty : " · 첫 항목: " + string.Join(", ", failed.Take(6)));
    }
}
