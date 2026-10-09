namespace 살뜰.Services.Dispatch.Recommendation
{
    public sealed partial class 운송일정삽입평가Service
    {
        private async Task<IReadOnlyList<운송삽입시도결과>> CreateSplitInsertionAttemptsAsync(
            기사운송일정계획 계획,
            IReadOnlyList<기사운송일정항목> 후보항목목록,
            decimal? 기준총소요시간분,
            decimal? 단순이어가기총소요시간분,
            CancellationToken cancellationToken)
        {
            if (후보항목목록.Count != 2)
            {
                return [];
            }

            var attempts = new List<운송삽입시도결과>();
            var pickup = 후보항목목록[0];
            var dropoff = 후보항목목록[1];
            var baseCount = 계획.항목목록.Count;

            for (var pickupIndex = 0; pickupIndex <= baseCount; pickupIndex++)
            {
                for (var dropoffIndex = pickupIndex + 1; dropoffIndex <= baseCount + 1; dropoffIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (dropoffIndex == pickupIndex + 1)
                    {
                        continue;
                    }

                    var simulatedItems = 계획.항목목록.ToList();
                    simulatedItems.Insert(pickupIndex, pickup);
                    simulatedItems.Insert(dropoffIndex, dropoff);
                    var reordered = simulatedItems
                        .Select((x, index) => x with { 순서 = index })
                        .ToList();

                    var simulation = await 시뮬레이션Async(계획.시작좌표, reordered, 계획.기준시각, cancellationToken);
                    var extraDelay = simulation.총소요시간분.HasValue && 기준총소요시간분.HasValue
                        ? Math.Max(0m, simulation.총소요시간분.Value - 기준총소요시간분.Value)
                        : simulation.총소요시간분;
                    var savedMinutes = simulation.총소요시간분.HasValue && 단순이어가기총소요시간분.HasValue
                        ? Math.Round(단순이어가기총소요시간분.Value - simulation.총소요시간분.Value, 2)
                        : (decimal?)null;

                    attempts.Add(new 운송삽입시도결과(
                        pickupIndex,
                        "분리삽입",
                        true,
                        simulation.전체완수가능여부,
                        simulation.총소요시간분,
                        simulation.총거리Km,
                        extraDelay,
                        savedMinutes,
                        simulation.최대시간위반분,
                        simulation.위반사유,
                        BuildRouteOrder(reordered),
                        simulation.도착예상목록)
                    {
                        종료좌표 = simulation.종료좌표,
                        전체실제경로확인됨 = simulation.전체실제경로확인됨,
                        경로옵션 = simulation.경로옵션
                    });
                }
            }

            return attempts;
        }
    }
}
