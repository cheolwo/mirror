using FluentResults;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>한 추천 시도의 요금 준비·동결과 호출자가 요청한 현재 값 원복만 담당한다. 저장하거나 자동 원복하지 않는다.</summary>
internal sealed class 배차추천요금준비
{
    private readonly SsalddelContext _db;
    private readonly 운송원장 _queue;
    private readonly PropertyValues _beforePricing;
    private readonly I소형배송기사제안요금Service? _smallGoodsPricing;
    private readonly I음식배달기사제안요금Service? _음식배달기사제안요금Service;

    internal 배차추천요금준비(SsalddelContext db, 운송원장 queue,
        I소형배송기사제안요금Service? smallGoodsPricing,
        I음식배달기사제안요금Service? foodPricing)
    {
        _db = db;
        _queue = queue;
        _beforePricing = db.Entry(queue).CurrentValues.Clone();
        _smallGoodsPricing = smallGoodsPricing;
        _음식배달기사제안요금Service = foodPricing;
    }

    internal async Task<Result> 준비Async(string driverId, CancellationToken cancellationToken)
    {
        if (_smallGoodsPricing is not null)
        {
            var pricing = await _smallGoodsPricing.준비Async(_queue, cancellationToken);
            if (pricing.IsFailed) return pricing;
        }
        await 음식배달제안요금동결Async(_queue, driverId, cancellationToken);
        return Result.Ok();
    }

    internal void 원복() => _db.Entry(_queue).CurrentValues.SetValues(_beforePricing);

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
