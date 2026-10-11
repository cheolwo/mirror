using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Domain,
    "확인된 권역 수요와 잔여 업무량으로 신규 기사 참여를 권고하고 기존 활동과 장기 대기 기회를 보호한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 40,
    StepKey = "domain.food-demand-participation-policy",
    DependsOnStepKeys = ["contract.food-demand-participation"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "소프트 목표와 결정적 순서만 계산한다. 기사 ON/OFF·수행 업무·동시 수락 상한·운임·지급을 바꾸지 않고 실제 수락 관문을 우회하지 않는다.")]
public sealed class 음식배달수요참여Policy
{
    public const string 현재정책판본 = "food-demand-participation/v1";

    public 음식배달수요참여결과 평가(음식배달수요참여입력 입력, 음식배달수요참여설정? 설정 = null)
    {
        ArgumentNullException.ThrowIfNull(입력);
        ArgumentNullException.ThrowIfNull(입력.기사목록);
        설정 ??= new();
        설정검증(설정);
        if (string.IsNullOrWhiteSpace(입력.권역Key))
            throw new ArgumentException("수요를 평가할 권역이 필요합니다.", nameof(입력));

        var 기사 = 중복제거(입력.기사목록);
        var 기존 = 기사.Where(x => x.적격여부 && x.기존활동여부).ToArray();
        var 신규 = 대기순서(기사.Where(x => x.적격여부 && !x.기존활동여부)).ToArray();
        var (자료상태, 사유) = 자료판정(입력, 설정, 기사);
        var 결과 = new 음식배달수요참여결과
        {
            정책판본 = 현재정책판본,
            권역Key = 입력.권역Key,
            판정시각Utc = 입력.판정시각Utc.ToUniversalTime(),
            데이터상태Code = 자료상태,
            사유Code = 사유,
            보호활동기사수 = 기존.Length
        };

        // 미확인 수요를 주문 0건으로 치환해 신규 참여를 막지 않는다. 기존 관문은 여전히 적용된다.
        if (자료상태 != 음식배달수요자료상태Code.최신)
        {
            결과.기사판정목록 = 최종순서(기사.Select(x => 판정(x,
                x.적격여부 ? 음식배달수요참여상태Code.기존관문재검사 : 음식배달수요참여상태Code.적격조건미충족,
                x.적격여부 ? 사유 : "ExistingEligibilityNotMet", x.적격여부, false)), 기사);
            return 결과;
        }

        var 인당계획처리량 = 설정.시간당목표처리건수 * 설정.계획시간창분 / 60m;
        var 관측기반예상 = 입력.최근시간창신규주문수!.Value * 설정.계획시간창분 / 입력.관측시간창분;
        var 업무량 = 입력.대기주문수!.Value + Math.Max(관측기반예상, 입력.향후수요예측건수 ?? 0m);
        var 기존여력 = 기존.Sum(x => Math.Max(0m, 설정.계획시간창분 - x.남은수행시간분!.Value)
            * 설정.시간당목표처리건수 / 60m);
        var 필요추가인원 = 올림인원(Math.Max(0m, 업무량 - 기존여력) / 인당계획처리량);
        var 지연추가 = 입력.지연위험주문존재 ? 설정.주문지연추가인원 : 0;
        var 일반승격수 = Math.Min(신규.Length, (int)Math.Min(int.MaxValue, (long)필요추가인원 + 지연추가));
        if (입력.대기주문수 > 0 && (입력.적격활동기사부재 || 기존.Length == 0))
            일반승격수 = Math.Max(일반승격수, Math.Min(1, 신규.Length));

        var 장기 = 신규.Where(x => (입력.판정시각Utc - 대기기준(x)).TotalMinutes >= (double)설정.장기대기기회분)
            .Take(설정.장기대기추가인원).Select(x => x.기사Id).ToHashSet(StringComparer.Ordinal);
        var 일반 = 신규.Where(x => !장기.Contains(x.기사Id)).Take(일반승격수)
            .Select(x => x.기사Id).ToHashSet(StringComparer.Ordinal);
        결과.예상업무량건수 = 업무량;
        결과.기존기사가용처리건수 = 기존여력;
        // 목표보다 기존 활동 인원이 많아도 퇴출하지 않는다. 장기 대기·지연 예외는 목표를 완화한다.
        결과.목표활동인원 = (int)Math.Min(int.MaxValue,
            (long)기존.Length + 필요추가인원 + 지연추가 + 장기.Count);
        결과.추가참여인원 = 일반.Count + 장기.Count;
        결과.기사판정목록 = 최종순서(기사.Select(x =>
        {
            if (!x.적격여부)
                return 판정(x, 음식배달수요참여상태Code.적격조건미충족, "ExistingEligibilityNotMet", false, false);
            if (x.기존활동여부)
                return 판정(x, 음식배달수요참여상태Code.기존활동보호, "ExistingActivityProtected", true, false);
            if (장기.Contains(x.기사Id))
                return 판정(x, 음식배달수요참여상태Code.추가참여권고, "LongWaitOpportunity", true, true);
            if (일반.Contains(x.기사Id))
                return 판정(x, 음식배달수요참여상태Code.추가참여권고,
                    입력.지연위험주문존재 ? "OrderDelayCapacityException" : "DemandCapacityAvailable", true, true);
            return 판정(x, 음식배달수요참여상태Code.수요대기, "ExistingCapacitySufficient", false, false);
        }), 기사);
        return 결과;
    }

    private static (string 상태, string 사유) 자료판정(
        음식배달수요참여입력 입력, 음식배달수요참여설정 설정, IReadOnlyList<음식배달수요기사입력> 기사)
    {
        if (입력.대기주문수 is null or < 0 || 입력.최근시간창신규주문수 is null or < 0
            || 입력.관측시간창분 <= 0 || 입력.향후수요예측건수 < 0
            || !입력.수요관측시각Utc.HasValue)
            return (음식배달수요자료상태Code.미확인, "DemandSnapshotUnavailable");
        if (!최신(입력.수요관측시각Utc.Value, 입력.판정시각Utc, 설정.최대수요관측경과분))
            return (음식배달수요자료상태Code.만료, "DemandSnapshotStale");
        foreach (var 항목 in 기사.Where(x => x.적격여부))
        {
            if (!항목.마지막관측시각Utc.HasValue || !항목.운행시작시각Utc.HasValue
                || 항목.운행시작시각Utc > 입력.판정시각Utc || 대기기준(항목) > 입력.판정시각Utc
                || 항목.최근시간창배차수 < 0
                || (항목.기존활동여부 && 항목.남은수행시간분 is null or < 0))
                return (음식배달수요자료상태Code.미확인, "DriverCapacityUnavailable");
            if (!최신(항목.마지막관측시각Utc.Value, 입력.판정시각Utc, 설정.최대기사관측경과분))
                return (음식배달수요자료상태Code.만료, "DriverCapacityStale");
        }
        return (음식배달수요자료상태Code.최신, "DemandAndCapacityObserved");
    }

    private static 음식배달수요기사입력[] 중복제거(IReadOnlyList<음식배달수요기사입력> 기사)
    {
        if (기사.Any(x => x is null || string.IsNullOrWhiteSpace(x.기사Id)))
            throw new ArgumentException("기사 입력에는 기사 ID가 필요합니다.", nameof(기사));
        // 같은 기사의 최신 관측 한 개만 센다. 같은 시각의 충돌은 제한·큰 업무량 쪽으로 결정한다.
        return 기사.GroupBy(x => x.기사Id, StringComparer.Ordinal).Select(g => g
                .OrderByDescending(x => x.마지막관측시각Utc)
                .ThenBy(x => x.적격여부)
                .ThenByDescending(x => x.기존활동여부)
                .ThenByDescending(x => x.남은수행시간분)
                .ThenByDescending(x => x.최근시간창배차수)
                .ThenBy(x => x.운행시작시각Utc)
                .ThenBy(x => x.대기시작시각Utc).First())
            .OrderBy(x => x.기사Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<음식배달수요기사판정> 최종순서(
        IEnumerable<음식배달수요기사판정> 판정들, IReadOnlyList<음식배달수요기사입력> 기사)
    {
        var 원천 = 기사.ToDictionary(x => x.기사Id, StringComparer.Ordinal);
        var 정렬 = 판정들.OrderBy(x => !x.신규제안허용)
            .ThenBy(x => x.사유Code == "LongWaitOpportunity" ? 0 : x.기존활동여부 ? 1 : 2)
            .ThenBy(x => 원천[x.기사Id].최근시간창배차수)
            .ThenBy(x => 대기기준(원천[x.기사Id]))
            .ThenBy(x => 원천[x.기사Id].운행시작시각Utc)
            .ThenBy(x => x.기사Id, StringComparer.Ordinal).ToArray();
        for (var i = 0; i < 정렬.Length; i++) 정렬[i].우선순위 = i + 1;
        return 정렬;
    }

    private static IOrderedEnumerable<음식배달수요기사입력> 대기순서(IEnumerable<음식배달수요기사입력> 기사)
        => 기사.OrderBy(대기기준).ThenBy(x => x.운행시작시각Utc).ThenBy(x => x.기사Id, StringComparer.Ordinal);

    private static DateTimeOffset 대기기준(음식배달수요기사입력 기사)
        => 기사.대기시작시각Utc ?? 기사.운행시작시각Utc ?? DateTimeOffset.MaxValue;

    private static bool 최신(DateTimeOffset 관측, DateTimeOffset 현재, decimal 최대분)
        => 관측 <= 현재 && (현재 - 관측).TotalMinutes <= (double)최대분;

    private static int 올림인원(decimal 값) => (int)Math.Min(int.MaxValue, decimal.Ceiling(값));

    private static 음식배달수요기사판정 판정(음식배달수요기사입력 기사, string 상태, string 사유, bool 허용, bool 승격)
        => new() { 기사Id = 기사.기사Id, 기존활동여부 = 기사.기존활동여부,
            상태Code = 상태, 사유Code = 사유, 신규제안허용 = 허용, 승격여부 = 승격 };

    private static void 설정검증(음식배달수요참여설정 설정)
    {
        if (설정.시간당목표처리건수 <= 0 || 설정.계획시간창분 <= 0
            || 설정.최대수요관측경과분 <= 0 || 설정.최대기사관측경과분 <= 0
            || 설정.장기대기기회분 <= 0 || 설정.장기대기추가인원 < 0 || 설정.주문지연추가인원 < 0)
            throw new ArgumentOutOfRangeException(nameof(설정), "처리량·시간창·자료 유효시간은 양수이고 추가 인원은 음수일 수 없습니다.");
    }
}
