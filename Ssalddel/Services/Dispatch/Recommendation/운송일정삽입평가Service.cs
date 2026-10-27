using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public interface I운송일정삽입평가Service
    {
        Task<운송기존일정평가결과> 기존일정평가Async(기사운송일정계획 계획, CancellationToken cancellationToken = default)
            => Task.FromResult(new 운송기존일정평가결과(false, null, null, null, "ExistingScheduleServiceUnsupported"));
        Task<운송삽입평가결과> 평가Async(기사운송일정계획 계획, 화주운송의뢰 후보의뢰, CancellationToken cancellationToken = default);
    }

    public sealed partial class 운송일정삽입평가Service : I운송일정삽입평가Service
    {
        private readonly I배차추천경로Service _routeService;

        public 운송일정삽입평가Service(I배차추천경로Service routeService)
        {
            _routeService = routeService;
        }

        public async Task<운송기존일정평가결과> 기존일정평가Async(기사운송일정계획 계획, CancellationToken cancellationToken = default)
        {
            if (계획.시작좌표 is null || 계획.항목목록.Count == 0
                || 계획.항목목록.Any(x => x.좌표 is null || !x.시간창종료일시.HasValue))
                return new(false, null, null, null, "ActiveScheduleEvidenceMissing");
            var baseline = await 시뮬레이션Async(계획.시작좌표, 계획.항목목록, 계획.기준시각, cancellationToken);
            var last = baseline.도착예상목록.OrderBy(x => x.순서).LastOrDefault();
            var lastNode = 계획.항목목록.OrderBy(x => x.순서).LastOrDefault();
            var departure = last?.예상도착시각;
            if (departure.HasValue && lastNode?.작업시간분 is >= 0m
                && lastNode.작업시간분 <= (decimal)(DateTime.MaxValue - departure.Value).TotalMinutes)
                departure = departure.Value.AddMinutes((double)lastNode.작업시간분.Value);
            return baseline.전체완수가능여부 && baseline.전체실제경로확인됨 && baseline.종료좌표 is not null
                   && departure is { } eta && eta >= 계획.기준시각 && !string.IsNullOrWhiteSpace(baseline.경로옵션)
                ? new(true, baseline.종료좌표, eta, baseline.경로옵션, "VerifiedActiveScheduleEnd")
                : new(false, null, null, null, "ActiveScheduleRouteUnverified");
        }

        public async Task<운송삽입평가결과> 평가Async(기사운송일정계획 계획, 화주운송의뢰 후보의뢰, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baseline = await 시뮬레이션Async(계획.시작좌표, 계획.항목목록, 계획.기준시각, cancellationToken);
            var candidateItems = CreateCandidateItems(계획.항목목록.Count, 후보의뢰)
                .Select(x => x with
                {
                    작업시간분 = 계획.상하차작업시간?.GetValueOrDefault(x.의뢰Id) is { } times
                        ? x.단계유형 == "pickup" ? times.상차분 : times.하차분
                        : null
                }).ToArray();
            var attempts = new List<운송삽입시도결과>();
            운송삽입시도결과? simpleAppendAttempt = null;

            for (var insertIndex = 0; insertIndex <= 계획.항목목록.Count; insertIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var simulatedItems = 계획.항목목록.ToList();
                simulatedItems.InsertRange(insertIndex, candidateItems.Select((x, offset) => x with { 순서 = insertIndex + offset }));
                var reordered = simulatedItems
                    .Select((x, index) => x with { 순서 = index })
                    .ToList();

                var simulation = await 시뮬레이션Async(계획.시작좌표, reordered, 계획.기준시각, cancellationToken);
                var extraDelay = simulation.총소요시간분.HasValue && baseline.총소요시간분.HasValue
                    ? Math.Max(0m, simulation.총소요시간분.Value - baseline.총소요시간분.Value)
                    : simulation.총소요시간분;

                var attempt = new 운송삽입시도결과(
                    insertIndex,
                    insertIndex == 계획.항목목록.Count ? "하차후이어가기" : "연속삽입",
                    false,
                    simulation.전체완수가능여부,
                    simulation.총소요시간분,
                    simulation.총거리Km,
                    extraDelay,
                    null,
                    simulation.최대시간위반분,
                    simulation.위반사유,
                    BuildRouteOrder(reordered),
                    simulation.도착예상목록)
                {
                    종료좌표 = simulation.종료좌표,
                    전체실제경로확인됨 = simulation.전체실제경로확인됨,
                    경로옵션 = simulation.경로옵션
                };
                attempts.Add(attempt);

                if (insertIndex == 계획.항목목록.Count)
                {
                    simpleAppendAttempt = attempt;
                }
            }

            if (simpleAppendAttempt?.총소요시간분 is not null)
            {
                attempts = attempts
                    .Select(x => x.시도유형 == "하차후이어가기"
                        ? x
                        : x with
                        {
                            경로변경시도여부 = true,
                            단순이어가기대비절감분 = x.총소요시간분.HasValue
                                ? Math.Round(simpleAppendAttempt.총소요시간분.Value - x.총소요시간분.Value, 2)
                                : null
                        })
                    .ToList();
            }

            var splitAttempts = await CreateSplitInsertionAttemptsAsync(
                계획,
                candidateItems,
                baseline.총소요시간분,
                simpleAppendAttempt?.총소요시간분,
                cancellationToken);
            attempts.AddRange(splitAttempts);

            return 운송삽입시도Selector.Select(attempts,
                (baseline.종료좌표, baseline.전체실제경로확인됨, baseline.경로옵션));
        }

    }
}
