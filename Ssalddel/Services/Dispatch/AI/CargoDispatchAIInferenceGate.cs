using Microsoft.Extensions.Options;

namespace 살뜰.Services.Dispatch.AI;

/// <summary>프로세스의 공유 추론 한도다. 대기열 없이 혼잡·연속 실패·시간창 예산은 즉시 규칙으로 복귀한다.</summary>
public sealed class CargoDispatchAIInferenceGate(IOptions<CargoDispatchOllamaOptions> options, TimeProvider? clock = null)
{
    private readonly object sync = new();
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private DateTimeOffset? windowStart;
    private DateTimeOffset? failedAt;
    private int calls;
    private bool busy;

    public Lease? TryAcquire(out string status)
    {
        lock (sync)
        {
            var now = time.GetUtcNow();
            var settings = options.Value;
            if (!settings.Validate()) { status = "InvalidConfiguration"; return null; }
            if (busy) { status = "Busy"; return null; }
            if (failedAt is { } failed && now - failed < TimeSpan.FromSeconds(settings.FailureCooldownSeconds))
            { status = "Cooldown"; return null; }
            if (windowStart is null || now - windowStart.Value >= TimeSpan.FromSeconds(settings.WindowSeconds))
            { windowStart = now; calls = 0; }
            if (calls >= settings.MaxCallsPerWindow) { status = "BudgetExhausted"; return null; }
            calls++; busy = true; status = "Acquired"; return new(this);
        }
    }

    public sealed class Lease : IDisposable
    {
        private CargoDispatchAIInferenceGate? owner;
        private bool failed;
        internal Lease(CargoDispatchAIInferenceGate owner) => this.owner = owner;
        public void RecordFailure() => failed = true;
        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref owner, null);
            if (gate is null) return;
            lock (gate.sync)
            {
                if (failed) gate.failedAt = gate.time.GetUtcNow();
                gate.busy = false;
            }
        }
    }
}
