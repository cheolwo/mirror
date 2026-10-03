using 살뜰.도메인.공통;

namespace 살뜰.Services.Dispatch.Queue
{
    // 음식 배달 제안의 산정·동결·근거 저장 전용. 화물의 기준운임/협의운임은 변경하지 않는다.
    public sealed partial class 배차대기원장전환Service
    {
        private async Task 음식배달제안요금동결Async(
            운송원장 queue,
            string driverId,
            CancellationToken cancellationToken)
        {
            if (queue.배차업무유형 != 상태값.배차업무유형.음식배달
                || _음식배달기사제안요금Service is null)
            {
                return;
            }

            // 동일 기사에게 제시한 금액은 고정한다. 다음 후보는 자신의 차량/제안 시각으로 산정한다.
            if (!string.IsNullOrWhiteSpace(queue.기사제안요금정책판본)
                && !string.IsNullOrWhiteSpace(queue.기사제안요금계산근거Json))
            {
                try
                {
                    var previous = System.Text.Json.JsonSerializer.Deserialize<살뜰.Services.Dispatch.Recommendation.음식배달기사제안요금산정결과>(queue.기사제안요금계산근거Json);
                    if (previous?.기사Id == driverId) return;
                }
                catch (System.Text.Json.JsonException) { throw new ArgumentException("FoodPricingEvidenceInvalid"); }
            }
            var decision = await _음식배달기사제안요금Service.산정Async(queue, driverId, cancellationToken);
            queue.기사기본거리지급액 = decision.요금.기본거리지급액;
            queue.기사기상할증액 = decision.요금.기상할증액;
            queue.기사한시수요할증액 = decision.요금.한시수요할증액;
            queue.기사지급예정액 = decision.요금.기사지급예정액;
            queue.기사기상할증적용여부 = decision.요금.기상할증적용여부;
            queue.기사한시수요할증적용여부 = decision.요금.한시수요할증적용여부;
            queue.픽업지기상자료상태 = decision.기상.자료상태Code;
            queue.픽업지기상코드 = decision.기상.강수형태Code;
            queue.픽업지기상기준시각Utc = decision.기상.관측기준시각Utc;
            queue.픽업지기상자료출처 = decision.기상.자료출처;
            queue.픽업지기상자료Hash = decision.기상.원본Hash;
            queue.기사제안요금정책판본 = decision.정책판본;
            queue.기사제안요금계산근거Json = System.Text.Json.JsonSerializer.Serialize(decision);
            queue.기사제안요금판정시각Utc = decision.판정시각Utc;
        }

    }
}
