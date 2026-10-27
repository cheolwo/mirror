using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Admin.DispatchTraining;
using 살뜰.Services.Dispatch.Coordination;

namespace CargoRegionalTrainingReview;

/// <summary>
/// 공식 법정동의 이름표와 가상 수치로 검토 자료를 만든다. 아래 적격성·점수는 교육용
/// fixture이며 제품 guard·실제 ETA·확정 정답이 아니다. 실제 조율 엔진은 비교 기준만 계산한다.
/// </summary>
public static class CargoTrainingCases
{
    private const string RegionRelativePath = "eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json";
    private const string FactoryRelativePath = "eng/CargoRegionalTrainingReview/CargoTrainingCases.cs";
    private const string RegionJsonSha256 = "737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b";
    private const string ArchiveSha256 = "7b4b544a6302d26c4f4c89d2c1355beae82e958c786bad8cc8572db0d2e2eb33";
    private static readonly DateTime FixtureClock = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] RegionCodes =
    [
        "1121510100", "1123010400", "1123010500", "1123010600", "1123010800",
        "1123010900", "1123011000", "1126010100", "1126010200", "1126010300",
        "1126010400", "1126010500", "1126010600", "1135010200", "1135010300"
    ];

    /// <summary>실제로 호출하는 조율기와 그 순수 규칙, 이 fixture 및 지역 자료의 판본 경계다.</summary>
    public static IReadOnlyList<string> PolicySourcePaths { get; } = new[]
    {
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율Service.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율Service.Strategy.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율Service.Optimization.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율Service.MinCostFlow.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율Service.PlatformBundle.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물배차조율모델.cs",
        "Ssalddel/Services/Dispatch/Coordination/국내화물기사배정AIService.cs",
        "Ssalddel/Services/Dispatch/Coordination/배차AI판단근거Service.cs",
        "Ssalddel/Services/Dispatch/Coordination/배차AI판단근거Source.cs",
        "Ssalddel/Services/Dispatch/Coordination/배차AI판단근거Formatter.cs",
        "eng/CargoRegionalTrainingReview/CargoTrainingDatasetEvidence.cs",
        "eng/CargoRegionalTrainingReview/CargoTrainingEventLog.cs",
        "eng/CargoRegionalTrainingReview/CargoTrainingModelComparison.cs",
        FactoryRelativePath,
        RegionRelativePath
    }.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public static string PolicyFingerprint(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var entries = PolicySourcePaths.Select(relative => new
        {
            path = relative,
            sha256 = Digest(File.ReadAllBytes(WithinRoot(root, relative)))
        });
        return Digest(JsonSerializer.SerializeToUtf8Bytes(entries));
    }

    public static List<CargoTrainingCase> Build(string regionsPath)
    {
        var regions = ReadRegions(regionsPath);
        var result = new List<CargoTrainingCase>();
        var reviewFamilies = new[]
        {
            "long_wait", "deadline_flip", "near_boundary", "busy_final_destination", "capacity_weight",
            "capacity_volume", "receiving_toggle", "stale_location", "return_direction", "missing_eta"
        };
        var holdoutFamilies = new[] { "combined_deadline_capacity", "combined_busy_stale", "no_candidate", "equal_acceptable" };
        foreach (var family in reviewFamilies)
            for (var variant = 1; variant <= 2; variant++)
                result.Add(Create(family, variant, "review", regions));
        foreach (var family in holdoutFamilies)
            for (var variant = 1; variant <= 2; variant++)
                result.Add(Create(family, variant, "holdout", regions));
        Require(result.Count == 28 && result.Count(x => x.Split == "review") == 20, "CaseCount");
        Require(result.GroupBy(x => x.FamilyId).All(g => g.Select(x => x.Split).Distinct().Count() == 1), "FamilySplitLeak");
        Require(result.Select(x => x.InputSha256).Distinct(StringComparer.Ordinal).Count() == 28, "DuplicateInput");
        Require(result.All(x => x.Engine.CandidateId is null || x.Candidates.Any(c => c.Id == x.Engine.CandidateId && c.Eligible)), "EngineIneligibleSelection");
        return result;
    }

    private static CargoTrainingCase Create(string family, int variant, string split, Dictionary<string, CargoTrainingRegion> regions)
    {
        var index = Array.IndexOf(RegionCodes, family switch
        {
            "long_wait" => "1126010100", "deadline_flip" => "1126010200", "near_boundary" => "1121510100",
            "busy_final_destination" => "1123011000", "capacity_weight" => "1135010300", "capacity_volume" => "1126010300",
            "receiving_toggle" => "1126010400", "stale_location" => "1126010500", "return_direction" => "1126010600",
            "missing_eta" => "1123010900", "combined_deadline_capacity" => "1123010400", "combined_busy_stale" => "1123010500",
            "no_candidate" => "1123010600", "equal_acceptable" => "1135010200", _ => throw new InvalidDataException("UnknownFamily")
        });
        var pickup = regions[RegionCodes[index]];
        var dropoff = regions[RegionCodes[(index + 1) % RegionCodes.Length]];
        var other = regions[RegionCodes[(index + 2) % RegionCodes.Length]];
        var a = Candidate("C1", pickup, wait: 30, travel: 5);
        var b = Candidate("C2", pickup, wait: 5, travel: 5);
        decimal deadline = 15, weight = 3, volume = 8;
        string title;
        string situation;
        switch (family)
        {
            case "long_wait":
                title = "비슷한 도착 시간과 오래 기다린 기사";
                situation = "두 후보의 가상 ETA·적재·약속 조건은 같고 대기 시간만 교환합니다.";
                a = a with { IdleWaitMinutes = variant == 1 ? 35 : 5 };
                b = b with { IdleWaitMinutes = variant == 1 ? 5 : 35 };
                break;
            case "deadline_flip":
                title = "픽업 기한이 촉박해질 때";
                situation = "장기 대기 C1의 ETA는 10분, C2는 4분입니다. 기한만 12분에서 6분으로 바뀝니다.";
                a = a with { TravelAfterAvailableMinutes = 10 };
                b = b with { TravelAfterAvailableMinutes = 4 };
                deadline = variant == 1 ? 12 : 6;
                break;
            case "near_boundary":
                title = "다른 법정동의 가까운 후보";
                situation = "다른 법정동 후보와 같은 법정동 후보의 가상 ETA를 바꿉니다. 경계·인접·거리 자료를 확인한 사례가 아닙니다.";
                a = a with { CurrentRegion = other, AvailableRegion = other, TravelAfterAvailableMinutes = variant == 1 ? 3 : 9 };
                b = b with { TravelAfterAvailableMinutes = variant == 1 ? 8 : 4 };
                break;
            case "busy_final_destination":
                title = "현재 수행의 마지막 목적지에서 이어 받기";
                situation = "C1의 현재 지역과 업무 종료 지역은 다릅니다. 마지막 목적지에서 가용해진 뒤 이동 2분을 더합니다. 현재 위치에서 바로 출발하지 않습니다.";
                a = a with { CurrentRegion = other, AvailableRegion = pickup, AvailableAfterMinutes = variant == 1 ? 6 : 20, TravelAfterAvailableMinutes = 2 };
                b = b with { TravelAfterAvailableMinutes = 9 };
                break;
            case "capacity_weight":
                title = "남은 적재 중량";
                situation = "물품 8kg/12kg를 비교합니다. C1 남은 적재량 10kg, C2는 15kg이며 부피 조건은 같습니다.";
                weight = variant == 1 ? 8 : 12;
                a = a with { CapacityKg = 10 }; b = b with { CapacityKg = 15 };
                break;
            case "capacity_volume":
                title = "남은 적재 부피";
                situation = "물품 20L/30L를 비교합니다. C1 남은 부피 25L, C2는 40L이며 중량 조건은 같습니다.";
                volume = variant == 1 ? 20 : 30;
                a = a with { CapacityLiters = 25 }; b = b with { CapacityLiters = 40 };
                break;
            case "receiving_toggle":
                title = "신규 배차 수신 의사";
                situation = "장기 대기 C1의 신규 수신을 켜짐/꺼짐으로 바꿉니다. 수신을 끈 후보를 대기 점수로 복원하지 않습니다.";
                a = a with { Receiving = variant == 1 };
                break;
            case "stale_location":
                title = "위치 관측이 오래된 경우";
                situation = "C1 위치 관측 나이만 1분/8분으로 바뀝니다. 5분 기준은 이 검토용 fixture의 가정이며 실제 운영 정책이 아닙니다.";
                a = a with { LocationAgeMinutes = variant == 1 ? 1 : 8 };
                b = b with { LocationAgeMinutes = 2 };
                break;
            case "return_direction":
                title = "기존 약속을 지키면서 복귀 방향 배려";
                situation = "양쪽 ETA·대기는 같고 가상 복귀 방향 표시를 교환합니다. 실제 복귀 경로는 확인하지 않았습니다.";
                a = a with { IdleWaitMinutes = 10, ReturnDirection = variant == 1 };
                b = b with { IdleWaitMinutes = 10, ReturnDirection = variant == 2 };
                break;
            case "missing_eta":
                title = "도착 예상 근거 누락";
                situation = "장기 대기 C1의 가상 이동 시간이 미확인/4분입니다. 누락값을 0분으로 간주하지 않습니다.";
                a = a with { TravelAfterAvailableMinutes = variant == 1 ? null : 4 };
                b = b with { TravelAfterAvailableMinutes = 6 };
                break;
            case "combined_deadline_capacity":
                title = "보류 평가: 기한과 적재 조건 결합";
                situation = "C1은 8분·잔여 10kg, C2는 4분·잔여 15kg입니다. 물품 8kg/12kg와 기한 10분/6분을 함께 바꾸는 미검토 평가 사례입니다.";
                a = a with { TravelAfterAvailableMinutes = 8, CapacityKg = 10 };
                b = b with { TravelAfterAvailableMinutes = 4, CapacityKg = 15 };
                deadline = variant == 1 ? 10 : 6; weight = variant == 1 ? 8 : 12;
                break;
            case "combined_busy_stale":
                title = "보류 평가: 수행 중 가용 시각과 오래된 위치";
                situation = "C1은 마지막 지역에서 4분 뒤 가용·이동 3분이며 위치 나이가 2분/9분입니다. C2는 현재 가용·8분입니다.";
                a = a with { CurrentRegion = other, AvailableRegion = pickup, AvailableAfterMinutes = 4, TravelAfterAvailableMinutes = 3, LocationAgeMinutes = variant == 1 ? 2 : 9 };
                b = b with { TravelAfterAvailableMinutes = 8 };
                break;
            case "no_candidate":
                title = "보류 평가: 적격 후보 없음";
                situation = "한 사례는 신규 수신·적재 조건이, 다른 사례는 ETA·현재 약속 조건이 부족합니다. 가상의 후보를 만들어 채우지 않습니다.";
                a = variant == 1 ? a with { Receiving = false } : a with { TravelAfterAvailableMinutes = null };
                b = variant == 1 ? b with { CapacityKg = 1 } : b with { CommitmentsFeasible = false };
                break;
            case "equal_acceptable":
                title = "보류 평가: 복수의 동등한 선택";
                situation = "후보 두 명의 수치가 같습니다. 이름 순서로 나온 엔진 선택을 유일한 정답으로 만들지 않고 사람이 허용 선택 집합을 검토합니다.";
                a = a with { IdleWaitMinutes = variant == 1 ? 10 : 20, TravelAfterAvailableMinutes = variant == 1 ? 5 : 7 };
                b = b with { IdleWaitMinutes = a.IdleWaitMinutes, TravelAfterAvailableMinutes = a.TravelAfterAvailableMinutes };
                break;
            default: throw new InvalidDataException("UnknownFamily");
        }

        var item = new CargoTrainingCase
        {
            CaseId = $"cargo-{split}-{family}-{variant:00}", FamilyId = family, Split = split,
            Title = $"{title} · {variant}", Situation = situation + " 모든 주문·기사·시간·적재·보수는 가상이며 사람 검토 전입니다.",
            PickupRegion = pickup, DropoffRegion = dropoff, PickupDeadlineMinutes = deadline,
            WeightKg = weight, VolumeLiters = volume, Fare = 5000
        };
        item = item with { Candidates = new[] { a, b }.Select(x => FixtureEligibility(x, item)).ToList() };
        item = item with { InputSha256 = InputFingerprint(item) };
        return item with { Engine = ExistingEngine(item) };
    }

    private static CargoTrainingCandidate Candidate(string id, CargoTrainingRegion region, decimal wait, decimal travel)
        => new() { Id = id, CurrentRegion = region, AvailableRegion = region, IdleWaitMinutes = wait, TravelAfterAvailableMinutes = travel, LocationAgeMinutes = 1 };

    /// <summary>검토용 가정만 검사한다. 실제 차량 자격·포장·구간 적재·현재 원장 guard를 대체하지 않는다.</summary>
    private static CargoTrainingCandidate FixtureEligibility(CargoTrainingCandidate candidate, CargoTrainingCase item)
    {
        var reasons = new List<string>();
        if (!candidate.Receiving) reasons.Add("receiving_off");
        if (!candidate.CommitmentsFeasible) reasons.Add("existing_commitment_conflict");
        if (candidate.CapacityKg < item.WeightKg) reasons.Add("insufficient_remaining_weight");
        if (candidate.CapacityLiters < item.VolumeLiters) reasons.Add("insufficient_remaining_volume");
        if (candidate.LocationAgeMinutes > 5) reasons.Add("fixture_location_age_over_5_minutes");
        decimal? eta = candidate.TravelAfterAvailableMinutes.HasValue
            ? candidate.AvailableAfterMinutes + candidate.TravelAfterAvailableMinutes.Value : null;
        if (eta is null) reasons.Add("pickup_eta_unknown");
        else if (eta > item.PickupDeadlineMinutes) reasons.Add("pickup_deadline_not_feasible");
        var parts = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["fixture_base"] = 50,
            ["fixture_wait_minutes_capped_40"] = Math.Min(40, candidate.IdleWaitMinutes),
            ["fixture_eta_penalty"] = -(eta ?? 0) * 2,
            ["fixture_return_direction_bonus"] = candidate.ReturnDirection ? 5 : 0
        };
        return candidate with
        {
            Eligible = reasons.Count == 0, Eligibility = "educational_fixture_gate_not_production_guard",
            PickupEtaMinutes = eta, Score = reasons.Count == 0 ? parts.Values.Sum() : 0,
            ScoreParts = parts, ExclusionReasons = reasons
        };
    }

    private static CargoTrainingDecision ExistingEngine(CargoTrainingCase item)
    {
        var eligible = item.Candidates.Where(x => x.Eligible).ToArray();
        var request = new 운송의뢰조율입력(1, item.CaseId, "synthetic-small-quick", "가상 소형 물품", "상온",
            item.WeightKg, item.Fare, "region:kr:bjd:" + item.PickupRegion.LegalDongCode, item.PickupRegion.DisplayName,
            null, null, FixtureClock, FixtureClock.AddMinutes((double)item.PickupDeadlineMinutes), null, null, 0, FixtureClock);
        var drivers = eligible.Select(x => new 기사후보조율입력(x.Id, x.Vehicle,
            x.AvailableAfterMinutes > 0 ? "가상수행중" : "가상대기", x.AvailableAfterMinutes > 0 ? 1 : 0,
            "region:kr:bjd:" + x.AvailableRegion.LegalDongCode, x.AvailableRegion.DisplayName, null,
            x.IdleWaitMinutes, FixtureClock, null, FixtureClock.AddMinutes(-(double)x.LocationAgeMinutes))).ToArray();
        var evaluations = eligible.Select(x => new 운송의뢰기사조합평가(
            의뢰Id: item.CaseId, 기사Id: x.Id, 추천가능여부: true,
            상차지거리Km: null, 상차지이동시간분: x.PickupEtaMinutes, 화물운송시간분: 10,
            총예상시간분: x.PickupEtaMinutes + 10, 총예상거리Km: null, 예상톨비: null,
            예상운임: item.Fare, 예상총비용: null, 예상순이익: null,
            일정삽입가능여부: x.CommitmentsFeasible, 전체일정완수가능여부: x.CommitmentsFeasible,
            최적삽입인덱스: null, 경로변경이점여부: false, 경로변경절감분: null, 총추가지연분: null,
            동일배달권여부: x.AvailableRegion.LegalDongCode == item.PickupRegion.LegalDongCode,
            인접배달권여부: false, 하차후복귀거리Km: null, 복귀시간대부담점수: 0,
            퇴근시간대복귀부담여부: false, 추천점수: x.Score,
            추천사유: "교육용 수치 투영; 제품 적격성·실경로·최적해 검증 아님",
            배지: ["가상 검토 자료"], 경고: ["인접·경계·실제 ETA·운영 손익 미검증"], 제외사유: [])).ToArray();
        var result = new 국내화물배차조율Service().조율(new 국내화물배차조율입력(
            FixtureClock, 1, [request], drivers, evaluations));
        var proposal = result.추천배정목록.SingleOrDefault();
        return new CargoTrainingDecision
        {
            Source = "current_cargo_rule", Status = proposal is null ? "hold" : "selected", CandidateId = proposal?.기사Id,
            InputSha256 = item.InputSha256,
            ReasonCodes = ["synthetic_input_projection", "educational_fixture_gate_not_production_guard", "current_policy_baseline_not_optimal_label", "algorithm:" + result.적용알고리즘]
        };
    }

    /// <summary>입력 필드만 고정 순서로 직렬화하며 모델·엔진 결과·검토·시계를 섞지 않는다.</summary>
    public static string InputFingerprint(CargoTrainingCase item)
    {
        var legacy = LegacyInputFingerprint(item);
        return item.Candidates.All(x => x.Plan is null) ? legacy : Digest(JsonSerializer.SerializeToUtf8Bytes(new
        {
            legacyInputSha256 = legacy, item.Source, item.PlanSetSha256,
            plans = item.Candidates.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new { x.Id, x.Plan })
        }, CargoTrainingStore.Json));
    }

    private static string LegacyInputFingerprint(CargoTrainingCase item) => Digest(JsonSerializer.SerializeToUtf8Bytes(new
    {
        item.CaseId, item.FamilyId, item.Split, item.Title, item.Situation, item.TaskType,
        item.PickupRegion, item.DropoffRegion, item.WeightKg, item.VolumeLiters, item.PickupDeadlineMinutes, item.Fare,
        candidates = item.Candidates.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => new
        {
            x.Id, x.Vehicle, x.CurrentRegion, x.AvailableRegion, x.AvailableAfterMinutes,
            x.TravelAfterAvailableMinutes, x.IdleWaitMinutes, x.CapacityKg, x.CapacityLiters,
            x.LocationAgeMinutes, x.Receiving, x.CommitmentsFeasible, x.ReturnDirection,
            x.Eligible, x.Eligibility, x.PickupEtaMinutes, x.Score,
            scoreParts = x.ScoreParts.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value),
            exclusionReasons = x.ExclusionReasons.OrderBy(x => x, StringComparer.Ordinal).ToArray()
        })
    }));

    private static Dictionary<string, CargoTrainingRegion> ReadRegions(string regionsPath)
    {
        var absolute = Path.GetFullPath(regionsPath);
        var root = FindRoot(absolute);
        Require(string.Equals(absolute, WithinRoot(root, RegionRelativePath), StringComparison.OrdinalIgnoreCase), "UnexpectedRegionPath");
        var regionBytes = File.ReadAllBytes(absolute);
        Require(Digest(regionBytes) == RegionJsonSha256, "RegionProjectionHash");
        using var doc = JsonDocument.Parse(regionBytes);
        var value = doc.RootElement;
        Require(Text(value, "schemaVersion") == "cargo-regional-training-legal-dong-regions.v1", "RegionSchema");
        Require(Text(value, "revision") == "cargo-regional-training-legal-dong-regions.r1", "RegionRevision");
        var scope = value.GetProperty("scope");
        Require(Text(scope, "regionType") == "LegalDong" && scope.GetProperty("selectedRegionCount").GetInt32() == 15, "RegionScope");
        foreach (var flag in new[] { "coordinateDataIncluded", "streetAddressDataIncluded", "customerDataIncluded", "administrativeDongCodesIncluded" })
            Require(!scope.GetProperty(flag).GetBoolean(), "RegionScopeProtection");
        var source = value.GetProperty("source");
        Require(Text(source, "sourceId") == "mois-standard-codes" && Text(source, "datasetId") == "korea-legal-dong-codes", "RegionSource");
        Require(Text(source, "archiveSha256") == ArchiveSha256 && Text(source, "encoding") == "cp949", "RegionArchiveIdentity");
        Require(Text(source, "retrievedDate") == "2026-08-11" && source.GetProperty("sourceAsOfDate").ValueKind == JsonValueKind.Null, "RegionSnapshotDate");
        var archiveBytes = File.ReadAllBytes(WithinRoot(root, Text(source, "archivePath")));
        Require(archiveBytes.Length == source.GetProperty("archiveByteLength").GetInt32() && Digest(archiveBytes) == ArchiveSha256, "RegionArchiveHash");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        // 공급처 ZIP의 파일명도 CP949다. UTF-8 기본 해석으로 원문 entry 이름을 잃지 않는다.
        using var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: encoding);
        var member = archive.Entries.SingleOrDefault(x => x.FullName == Text(source, "memberName")) ?? throw new InvalidDataException("RegionMemberMissing");
        Require(member.Length == source.GetProperty("memberByteLength").GetInt64() && member.Length < 10_000_000, "RegionMemberLength");
        using var bytes = new MemoryStream();
        using (var stream = member.Open()) stream.CopyTo(bytes);
        Require(Digest(bytes.ToArray()) == Text(source, "memberSha256"), "RegionMemberHash");
        var lines = encoding.GetString(bytes.ToArray()).TrimStart('\uFEFF').Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
        Require(lines[0] == "법정동코드\t법정동명\t폐지여부", "RegionSourceHeader");
        var rawRows = lines.Skip(1).Where(x => x.Length > 0).ToArray();
        Require(rawRows.Length == source.GetProperty("sourceRowCount").GetInt32(), "RegionSourceCount");
        var original = rawRows.Select(x => x.Split('\t')).ToDictionary(x => x[0], StringComparer.Ordinal);
        Require(original.Values.All(x => x.Length == 3 && Regex.IsMatch(x[0], "^[0-9]{10}$") && x[2] is "존재" or "폐지"), "RegionSourceRows");
        Require(original.Values.Count(x => x[2] == "존재") == source.GetProperty("activeSourceRowCount").GetInt32(), "RegionActiveCount");
        var regions = new Dictionary<string, CargoTrainingRegion>(StringComparer.Ordinal);
        foreach (var item in value.GetProperty("regions").EnumerateArray())
        {
            var code = Text(item, "legalDongCode");
            Require(RegionCodes.Contains(code, StringComparer.Ordinal), "UnexpectedLegalDong");
            Require(Text(item, "regionType") == "LegalDong" && Text(item, "regionId") == "region:kr:bjd:" + code, "AdministrativeLegalConfusion");
            Require(Text(item, "sidoCode") == code[..2] && Text(item, "sigunguCode") == code[..5], "RegionHierarchy");
            Require(original.TryGetValue(code, out var row) && row.Length == 3 && row[2] == "존재" && row[1] == Text(item, "displayName"), "RegionCodeNameSourceMismatch");
            var lineNumber = item.GetProperty("sourceLineNumber").GetInt32();
            Require(lineNumber > 1 && lineNumber <= lines.Length, "RegionLineNumber");
            var line = lines[lineNumber - 1];
            Require(line == string.Join('\t', row!) && Digest(encoding.GetBytes(line)) == Text(item, "sourceRowSha256"), "RegionRowHash");
            Require(Text(item, "statusInSnapshot") == "active", "RegionNotActiveInSnapshot");
            Require(regions.TryAdd(code, new CargoTrainingRegion(code, row![1])), "RegionDuplicate");
        }
        Require(regions.Count == RegionCodes.Length, "RegionCount");
        return regions;
    }

    private static string FindRoot(string source)
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Ssalddel", "Ssalddel.csproj"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidDataException("RepositoryRootMissing");
    }

    private static string WithinRoot(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative), "AbsoluteSourceReference");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Require(path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "SourceOutsideRepository");
        var cursor = new FileInfo(path).Directory;
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "SourceLinkNotAllowed");
        while (cursor is not null && cursor.FullName.Length >= root.Length)
        {
            Require((cursor.Attributes & FileAttributes.ReparsePoint) == 0, "SourceDirectoryLinkNotAllowed");
            if (string.Equals(cursor.FullName, root, StringComparison.OrdinalIgnoreCase)) break;
            cursor = cursor.Parent;
        }
        return path;
    }

    private static string Text(JsonElement value, string property) => value.GetProperty(property).GetString() ?? throw new InvalidDataException("Missing:" + property);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
}
