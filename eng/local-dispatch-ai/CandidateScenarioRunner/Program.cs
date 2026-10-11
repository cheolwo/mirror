using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.LocalDispatchCandidates;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    private static readonly string[] SourcePaths =
    [
        "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
        "Ssalddel/Services/Dispatch/Queue/음식배달후보검토결과.cs",
        "Ssalddel/Services/Dispatch/Queue/QueueOptions.cs",
        "Ssalddel/Services/Dispatch/Queue/기사위치신선도정책.cs",
        "Ssalddel/Services/Dispatch/Coordination/음식배달권정책.cs",
        "Ssalddel/Services/Dispatch/Coordination/배달권실행공간Store.cs",
        "Ssalddel/Services/Dispatch/Recommendation/배차추천경로Service.cs",
        "Ssalddel/Services/Dispatch/Common/운영배차공통UseCase.cs",
        "Ssalddel/Services/Dispatch/Queue/음식배달수요참여Service.cs",
        "Ssalddel.WorkflowRules/UnityPackage/Runtime/음식배달픽업평가Policy.cs",
        "Ssalddel.Domain/배차/음식배달수요참여Policy.cs",
        "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
        "Ssalddel.Domain/배차/운영배차수신상태Policy.cs",
        "Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs",
        "eng/local-dispatch-ai/CandidateScenarioRunner/CandidateScenarioRunner.csproj",
        "eng/local-dispatch-ai/CandidateScenarioRunner/Program.cs",
        "eng/local-dispatch-ai/CandidateScenarioRunner/fixture.json"
    ];
    private static string Hash(byte[] raw) => Convert.ToHexStringLower(SHA256.HashData(raw));
    private static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length is not (0 or 2) || args.Length == 2 && args[0] != "--output")
                throw new ArgumentException("--output <artifacts/local 아래 새 폴더>만 사용할 수 있습니다. 모델이나 서버를 시작하지 않습니다.");
            var root = RepoRoot();
            var output = Path.GetFullPath(args.Length == 0
                ? "artifacts/local/local-dispatch-ai/candidate-advisory-r47" : args[1], root);
            var allowed = Path.GetFullPath(Path.Combine(root, "artifacts/local")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력은 저장소 artifacts/local 아래 새 폴더여야 합니다.");
            foreach (var name in new[] { "evidence.json", "cases.json", "fixture.json" })
                if (File.Exists(Path.Combine(output, name)))
                    throw new InvalidOperationException("기존 검토 근거를 덮어쓰지 않습니다. 새 --output 폴더를 지정하세요.");
            var fixtureRaw = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixture.json"));
            if (!fixtureRaw.SequenceEqual(File.ReadAllBytes(Path.Combine(root, SourcePaths[^1]))))
                throw new InvalidOperationException("실행 위치의 fixture와 현재 소스가 다릅니다. 다시 빌드하세요.");
            var fixture = JsonSerializer.Deserialize<Fixture>(fixtureRaw, Json)
                ?? throw new ArgumentException("합성 입력이 비어 있습니다.");
            Validate(fixture);
            var sourceHashes = SourcePaths.ToDictionary(x => x,
                x => Hash(File.ReadAllBytes(Path.Combine(root, x))), StringComparer.Ordinal);
            var cases = new List<object>();
            var allChecks = new List<Check>();
            foreach (var spec in fixture.Cases)
            {
                var (result, checks) = await RunCase(spec);
                cases.Add(result);
                allChecks.AddRange(checks);
            }
            var sourcesStable = sourceHashes.All(pair => Hash(File.ReadAllBytes(Path.Combine(root, pair.Key))) == pair.Value);
            allChecks.Add(new("source_hashes_stable", sourcesStable, "실행 시작과 끝의 입력 소스 해시를 대조한다."));
            var casesRaw = Bytes(cases);
            var fixtureHash = Hash(fixtureRaw);
            var casesDigest = Hash(casesRaw);
            var evidenceId = Hash(Bytes(new { fixtureSha256 = fixtureHash, sourceHashes, casesDigest }));
            var evidence = new
            {
                schemaVersion = 1, generatedAtUtc = DateTimeOffset.UtcNow, synthetic = true,
                evidenceId, policyVersion = "food-candidate-review/r47", sourceHashes,
                fixtureFile = "fixture.json", fixtureSha256 = fixtureHash,
                casesFile = "cases.json", casesDigest, cases,
                runnerAssemblySha256 = Hash(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location)),
                serverAssemblySha256 = Hash(File.ReadAllBytes(typeof(음식배달배차업무정책).Assembly.Location)),
                domainAssemblySha256 = Hash(File.ReadAllBytes(typeof(음식배달수요참여Policy).Assembly.Location)),
                checksPassed = allChecks.All(x => x.Passed), checks = allChecks,
                modelCalls = 0, networkCalls = 0, operationalWrites = 0,
                actualAssignment = false, weightsTrained = false,
                boundary = "실제 서버 후보 정책을 합성 읽기 포트로 실행한 사후 검토 근거다. 실배차·DB·도로 경로·수락·수입 증거가 아니다."
            };
            var evidenceRaw = Bytes(evidence);
            var run = Path.Combine(output, "runs", evidenceId);
            Directory.CreateDirectory(run);
            foreach (var folder in new[] { run, output })
            {
                Preserve(Path.Combine(folder, "fixture.json"), fixtureRaw);
                Preserve(Path.Combine(folder, "cases.json"), casesRaw);
                Preserve(Path.Combine(folder, "evidence.json"), evidenceRaw);
            }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                caseCount = cases.Count, checks = allChecks.Count, failedChecks = allChecks.Count(x => !x.Passed),
                evidenceId, evidenceSha256 = Hash(evidenceRaw), casesDigest, evidence.checksPassed,
                evidence.modelCalls, evidence.networkCalls, evidence.operationalWrites, output
            }, Json));
            return evidence.checksPassed ? 0 : 2;
        }
        catch (Exception error) when (error is ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("후보 검토 생성 중단: " + error.GetType().Name + ": " + error.Message);
            return 1;
        }
    }

    private static async Task<(object Result, List<Check> Checks)> RunCase(CaseSpec spec)
    {
        var now = DateTime.UtcNow;
        var pickup = new 배차경로좌표(37.5m, 127m);
        var main = 음식배달권정책.판정(pickup, null).배달권키;
        var adjacent = 음식배달권정책.인접배달권키조회(main);
        var options = new 배차큐정책Options
        {
            음식배달동일권최소후보기사수 = 1, 음식배달후보검색반경Km = 5m,
            음식배달후보최대조회수 = 20, 음식배달평균이동속도KmH = 20m,
            음식배달픽업조기도착허용분 = 3m, 음식배달픽업지연허용분 = 5m, 기사위치유효시간분 = 10
        };
        var queue = new 운송원장
        {
            의뢰Id = spec.CaseId, 원본의뢰Id = spec.CaseId,
            배차업무유형 = 상태값.배차업무유형.음식배달, 픽업_위도 = pickup.Latitude, 픽업_경도 = pickup.Longitude
        };
        var beforeQueue = Bytes(queue);
        var spaces = spec.Drivers.GroupBy(x => 음식배달권정책.판정(new(x.Latitude, x.Longitude), null).배달권키)
            .ToDictionary(g => g.Key, g => new 음식배달권실행공간Snapshot(g.Key,
                음식배달권정책.인접배달권키조회(g.Key), g.Select(x => x.DriverId).ToArray(), [], now), StringComparer.Ordinal);
        var drivers = spec.Drivers.Select(x => new 국내화물운송기사상태Snapshot(
            x.DriverId, null, 상태값.기사운행상태.운행중, now.AddMinutes(-x.WaitMinutes), now.AddMinutes(-x.WaitMinutes),
            x.AgingScore, x.Latitude, x.Longitude, 5m, now.AddMinutes(-x.GpsAgeMinutes), now.AddMinutes(-x.GpsAgeMinutes),
            null, null, 0, "immediate", "synthetic", null,
            상차접근허용반경Km: x.ApproachRadiusKm, AppKey: 기사앱식별자.FoodDeliveryDriverApp)).ToArray();
        DateTime? readyAt = spec.CookingReadyOffsetMinutes is { } offset ? now.AddMinutes(offset) : null;
        var order = new 음식주문응답 { 주문번호 = spec.CaseId, 조리예상완료시각Utc = readyAt };
        var ledger = new ReadLedger();
        var queriedSpaces = new List<string>();
        var statePort = ledger.Port<I국내화물운송기사상태Store>("driver", (method, args) => method == "GetAsync"
            ? Task.FromResult(drivers.FirstOrDefault(x => x.DriverId == (string)args[0]!)) : null);
        var rejectedPort = ledger.Port<IDriverRejectedRequestStore>("rejected", (method, _) => method == "GetRejectedDriverIdsAsync"
            ? Task.FromResult<IReadOnlySet<string>>(spec.Drivers.Where(x => x.Rejected).Select(x => x.DriverId).ToHashSet(StringComparer.Ordinal)) : null);
        var spacesPort = ledger.Port<I음식배달권실행공간Store>("space", (method, args) =>
        {
            if (method != "GetAsync") return null;
            var key = (string)args[0]!;
            queriedSpaces.Add(key);
            return Task.FromResult(spaces.GetValueOrDefault(key));
        });
        var foodPort = ledger.Port<ISsalddelFoodOrderStore>("food", (method, args) => method == "GetOrder"
            && string.Equals((string)args[0]!, spec.CaseId, StringComparison.Ordinal) ? order : null);
        var receivingPort = ledger.Port<I운영배차공통UseCase>("receiving", (method, args) => method == "수신상태조회Async"
            ? Task.FromResult(new 운영배차수신상태Dto
            {
                주체Id = (string)args[0]!, 수신의사Code = spec.Drivers.Single(x => x.DriverId == (string)args[0]!).ReceivingOn
                    ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
                수신의사변경시각Utc = now.AddMinutes(-60)
            }) : null);
        var routePort = ledger.Port<I배차추천경로Service>("route", (method, args) => method == "CalculateDistanceKm"
            ? AxisDistance((배차경로좌표)args[0]!, (배차경로좌표)args[1]!) : null);
        음식배달수요참여입력? demandInput = null;
        음식배달수요참여설정? demandSettings = null;
        음식배달수요참여결과? demandResult = null;
        I음식배달수요참여Service? demandPort = null;
        if (spec.DemandStatus != "none")
        {
            demandInput = new()
            {
                권역Key = main, 판정시각Utc = now, 수요관측시각Utc = spec.DemandStatus == "stale" ? now.AddMinutes(-6) : now,
                대기주문수 = spec.PendingOrders, 최근시간창신규주문수 = spec.RecentOrders,
                향후수요예측건수 = null,
                기사목록 = spec.Drivers.Select(x => new 음식배달수요기사입력
                {
                    기사Id = x.DriverId, 기존활동여부 = x.ExistingActivity, 운행시작시각Utc = now.AddMinutes(-Math.Max(60, x.WaitMinutes)),
                    대기시작시각Utc = now.AddMinutes(-x.WaitMinutes), 마지막관측시각Utc = now,
                    남은수행시간분 = x.RemainingMinutes, 최근시간창배차수 = x.RecentAssignments, 적격여부 = x.CapacityEligible
                }).ToArray()
            };
            // 사용자의 약 4건/h를 이 합성 fixture의 명시적 계획 가정으로만 사용한다. 제품 기본값은 변경하지 않는다.
            demandSettings = new() { 시간당목표처리건수 = 4m };
            demandResult = new 음식배달수요참여Policy().평가(demandInput, demandSettings);
            demandPort = ledger.Port<I음식배달수요참여Service>("demand", (method, _) => method == "평가Async"
                ? Task.FromResult<음식배달수요참여결과?>(demandResult) : null);
        }
        var policy = new 음식배달배차업무정책(statePort, rejectedPort, routePort, spacesPort, foodPort,
            Options.Create(options), receivingPort, 수요참여: demandPort);
        var trace = await policy.후보검토Async(queue);
        var traceReads = queriedSpaces.ToArray();
        var wrapper = await policy.다음후보선정Async(queue);
        var checks = new List<Check>();
        void Check(string name, bool passed, string detail) => checks.Add(new(spec.CaseId + "/" + name, passed, detail));
        Check("queue_unchanged", beforeQueue.SequenceEqual(Bytes(queue)), "후보 검토가 원장·추천·확정·요금을 변경하지 않는다.");
        Check("read_only_ports", ledger.ForbiddenCalls == 0, "허용한 합성 조회 외 포트 호출은 즉시 실패한다.");
        Check("trace_matches_space_reads", trace.조회배달권키목록.SequenceEqual(traceReads), "실제 공간 조회 목록과 trace를 대조한다.");
        Check("wrapper_same_choice", trace.선정후보?.DriverId == wrapper?.DriverId, "기존 다음후보선정 경로와 trace 경로의 선택이 같다.");
        Check("selected_within_allowed", trace.선정후보 is null ? trace.최종허용기사Ids.Count == 0
            : trace.최종허용기사Ids.Contains(trace.선정후보.DriverId), "선정 후보는 최종 허용집합 안에 있어야 한다.");
        Check("allowed_known_unique", trace.최종허용기사Ids.Distinct(StringComparer.Ordinal).Count() == trace.최종허용기사Ids.Count
            && trace.최종허용기사Ids.All(id => spec.Drivers.Any(x => x.DriverId == id)), "알 수 없는 기사나 중복 후보를 만들지 않는다.");
        Check("space_role_matches_coordinates", spec.Drivers.All(x =>
        {
            var key = 음식배달권정책.판정(new(x.Latitude, x.Longitude), null).배달권키;
            return x.SpaceRole switch { "same" => key == main, "adjacent" => adjacent.Contains(key),
                "expanded" => key != main && !adjacent.Contains(key), _ => false };
        }), "합성 좌표가 실제 공간 정책의 동일·인접·확장 분류와 일치한다.");
        switch (spec.CaseId)
        {
            case "SYN-CANDIDATE-SAME-SUFFICIENT":
                Check("same_without_adjacent_read", traceReads.SequenceEqual([main]) && trace.선정후보?.DriverId == "SYN-D001",
                    "동일권이 충분하면 인접 기사를 조회했다고 기록하지 않는다."); break;
            case "SYN-CANDIDATE-ADJACENT":
                Check("adjacent_selected", trace.선정후보?.DriverId == "SYN-D001"
                    && trace.후보목록.Any(x => x.기사Id == "SYN-D001" && x.공간단계 == 1), "실제 인접권 후보가 선택된다."); break;
            case "SYN-CANDIDATE-EXPANDED":
                Check("expanded_selected", trace.선정후보?.DriverId == "SYN-D001"
                    && trace.후보목록.Any(x => x.기사Id == "SYN-D001" && x.공간단계 == 2), "동일권·인접권 부재 뒤 제한확장 후보가 선택된다."); break;
            case "SYN-CANDIDATE-TIMELINESS":
                Check("timely_before_long_wait", trace.선정후보?.DriverId == "SYN-D001"
                    && !trace.최종허용기사Ids.Contains("SYN-D002"), "수요 장기대기 우선권도 픽업 지연 후보를 살리지 않는다."); break;
            case "SYN-CANDIDATE-LONG-WAIT":
                Check("similar_feasibility_long_wait", trace.선정후보?.DriverId == "SYN-D001"
                    && demandResult?.기사판정목록.Single(x => x.기사Id == "SYN-D001").사유Code == "LongWaitOpportunity",
                    "실제 수요 정책의 장기대기 판정과 후보 선택을 함께 대조한다."); break;
            case "SYN-CANDIDATE-GATES":
                Check("physical_gates_retained", trace.최종허용기사Ids.SequenceEqual(["SYN-D005"])
                    && new[] { "DriverReceivingOff", "DriverLocationStale", "DriverRejected", "OutsideApproachRadius" }
                        .All(code => trace.후보목록.Any(x => x.판정Code == code)), "수신·위치·거절·반경 제외 사유가 실제 trace에 남는다."); break;
            case "SYN-CANDIDATE-NONE":
                Check("no_candidate_retained", trace.선정후보 is null && trace.최종허용기사Ids.Count == 0
                    && trace.중단사유Code == "NoEligibleCandidate", "후보가 없으면 미배정 상태를 유지한다."); break;
            case "SYN-CANDIDATE-DEMAND-STALE":
                Check("stale_demand_fallback", demandResult?.데이터상태Code == "Stale" && !trace.수요자료최신
                    && trace.선정후보?.DriverId == "SYN-D001", "만료 수요 우선순위 대신 기존 물리 평가를 유지한다."); break;
            case "SYN-CANDIDATE-CAPACITY-SHORTAGE":
                Check("capacity_math_actual", demandResult?.예상업무량건수 == 58m
                    && demandResult.기존기사가용처리건수 == 2m && demandResult.추가참여인원 == 2,
                    "대기50 + 관측환산8 = 58건, 기존30분 잔여의 계획여력2건, 실제 신규후보2명에 제한한다.");
                Check("ineligible_capacity_not_revived", !trace.최종허용기사Ids.Contains("SYN-D004")
                    && trace.후보목록.Any(x => x.기사Id == "SYN-D004" && x.판정Code == "DemandIneligible"),
                    "처리 여력이 부족해도 입력에서 부적격인 기사를 장기대기 점수로 살리지 않는다."); break;
            case "SYN-CANDIDATE-REMAINING-UNKNOWN":
                Check("unknown_not_zero", demandInput?.기사목록[0].남은수행시간분 is null
                    && demandResult?.데이터상태Code == "Unknown" && demandResult.기존기사가용처리건수 is null
                    && !trace.수요자료최신, "잔여시간 미확인을 영으로 치환하지 않는다."); break;
        }
        var input = new
        {
            nowUtc = now, pickup = new { latitude = pickup.Latitude, longitude = pickup.Longitude }, cookingReadyAtUtc = readyAt,
            approachAnchor = "current_gps", futurePickupAnchorStatus = "proposed_not_applied",
            routeModel = "synthetic_axis_distance_km_not_driving_api",
            options = new { sameScopeMinimumCandidates = options.음식배달동일권최소후보기사수,
                searchRadiusKm = options.음식배달후보검색반경Km, maximumCandidates = options.음식배달후보최대조회수,
                speedKmH = options.음식배달평균이동속도KmH, earlyMinutes = options.음식배달픽업조기도착허용분,
                lateMinutes = options.음식배달픽업지연허용분, gpsMaxAgeMinutes = options.기사위치유효시간분 },
            drivers = spec.Drivers.Select(x => new { driverId = x.DriverId, latitude = x.Latitude, longitude = x.Longitude,
                spaceKey = 음식배달권정책.판정(new(x.Latitude, x.Longitude), null).배달권키,
                agingScore = x.AgingScore, receivedAtUtc = now.AddMinutes(-x.GpsAgeMinutes), receivingOn = x.ReceivingOn,
                existingActivity = x.ExistingActivity, waitMinutes = x.WaitMinutes, remainingMinutes = x.RemainingMinutes,
                recentAssignments = x.RecentAssignments, capacityEligible = x.CapacityEligible,
                approachRadiusKm = x.ApproachRadiusKm, rejected = x.Rejected }).ToArray(), demandInput, demandSettings
        };
        return (new { spec.CaseId, spec.Title, input, demandResult, trace, checks,
            readCalls = ledger.Calls, operationalWrites = 0, modelCalls = 0, networkCalls = 0 }, checks);
    }

    private static decimal AxisDistance(배차경로좌표 source, 배차경로좌표 target)
        => Math.Abs(source.Latitude - target.Latitude) * 111m + Math.Abs(source.Longitude - target.Longitude) * 88m;

    private static void Validate(Fixture fixture)
    {
        if (fixture.SchemaVersion != 1 || !fixture.Synthetic || fixture.Cases.Length != 10
            || fixture.Cases.Select(x => x.CaseId).Distinct(StringComparer.Ordinal).Count() != fixture.Cases.Length)
            throw new ArgumentException("고정된 합성 사례 10개가 필요합니다.");
        foreach (var spec in fixture.Cases)
        {
            if (!Regex.IsMatch(spec.CaseId, "^SYN-CANDIDATE-[A-Z-]+$") || string.IsNullOrWhiteSpace(spec.Title)
                || spec.DemandStatus is not ("none" or "current" or "stale") || spec.PendingOrders < 0 || spec.RecentOrders < 0
                || spec.Drivers.Select(x => x.DriverId).Distinct(StringComparer.Ordinal).Count() != spec.Drivers.Length)
                throw new ArgumentException("합성 사례의 고유 식별자·수요 조건이 잘못됐습니다.");
            foreach (var driver in spec.Drivers)
                if (!Regex.IsMatch(driver.DriverId, "^SYN-D[0-9]{3}$") || driver.SpaceRole is not ("same" or "adjacent" or "expanded")
                    || driver.Latitude is < -90 or > 90 || driver.Longitude is < -180 or > 180
                    || driver.WaitMinutes < 0 || driver.GpsAgeMinutes < 0 || driver.RemainingMinutes < 0
                    || driver.RecentAssignments < 0 || driver.ApproachRadiusKm < 0)
                    throw new ArgumentException("실제 개인정보나 잘못된 수치 대신 합성 기사 입력만 사용합니다.");
        }
    }

    private static void Preserve(string path, byte[] raw)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(raw);
    }

    private static string RepoRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AGENTS.md"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("저장소 루트를 찾지 못했습니다.");
    }

    private sealed record Check(string Name, bool Passed, string Detail);
    private sealed class Fixture
    {
        public int SchemaVersion { get; set; }
        public bool Synthetic { get; set; }
        public CaseSpec[] Cases { get; set; } = [];
    }
    private sealed class CaseSpec
    {
        public string CaseId { get; set; } = "";
        public string Title { get; set; } = "";
        public int? CookingReadyOffsetMinutes { get; set; }
        public string DemandStatus { get; set; } = "none";
        public int PendingOrders { get; set; }
        public int RecentOrders { get; set; }
        public DriverSpec[] Drivers { get; set; } = [];
    }
    private sealed class DriverSpec
    {
        public string DriverId { get; set; } = "";
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string SpaceRole { get; set; } = "";
        public decimal AgingScore { get; set; }
        public bool ReceivingOn { get; set; } = true;
        public int GpsAgeMinutes { get; set; }
        public bool ExistingActivity { get; set; }
        public int WaitMinutes { get; set; } = 5;
        public decimal? RemainingMinutes { get; set; } = 0m;
        public int RecentAssignments { get; set; }
        public bool CapacityEligible { get; set; } = true;
        public decimal? ApproachRadiusKm { get; set; }
        public bool Rejected { get; set; }
    }
}

// 모든 외부 dependency는 미리 작성한 합성 조회값만 반환한다. 미등록/쓰기 메서드는 실행하지 않는다.
internal sealed class ReadLedger
{
    public Dictionary<string, int> Calls { get; } = new(StringComparer.Ordinal);
    public int ForbiddenCalls { get; private set; }
    public T Port<T>(string name, Func<string, object?[], object?> read) where T : class
    {
        var port = DispatchProxy.Create<T, ReadOnlyProxy>();
        ((ReadOnlyProxy)(object)port).Handler = (method, args) =>
        {
            var value = read(method, args);
            if (value is null)
            {
                ForbiddenCalls++;
                throw new InvalidOperationException("합성 조회 허용목록 외 포트 호출: " + name + "." + method);
            }
            var key = name + "." + method;
            Calls[key] = Calls.GetValueOrDefault(key) + 1;
            return value;
        };
        return port;
    }
}

public class ReadOnlyProxy : DispatchProxy
{
    internal Func<string, object?[], object?>? Handler { get; set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => Handler?.Invoke(targetMethod?.Name ?? "", args ?? [])
           ?? throw new InvalidOperationException("합성 포트가 초기화되지 않았습니다.");
}
