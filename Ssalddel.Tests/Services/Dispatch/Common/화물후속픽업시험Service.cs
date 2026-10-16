using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Common;

// 수락 handler의 개별 경계를 시험할 때만 주입한다. 실제 경로 평가는 CargoNextPickupR64Tests가 별도로 검사한다.
internal sealed class 화물후속픽업시험Service : I화물후속픽업기준Service
{
    public bool 확인됨 { get; set; } = true;
    public bool 현재판본 { get; set; } = true;
    public Task<화물후속픽업기준결과> 조회Async(string id, 배차경로좌표 current, DateTime now, CancellationToken ct = default)
        => Task.FromResult(new 화물후속픽업기준결과(true, 확인됨, current, now.AddMinutes(5), "trafast", "VerifiedFixture") { 원장판본 = "fixture" });
    public Task<bool> 현재근거확인Async(string id, 화물후속픽업기준결과 basis, CancellationToken ct = default) => Task.FromResult(현재판본);
}

internal sealed class 화물현재위치시험Store : IDriverLocationStore
{
    public bool TryGetLatest(string id, out DriverLocationSnapshot location)
    {
        var now = DateTime.UtcNow;
        location = new(id, 37.58m, 127.08m, 1m, 상태값.기사운행상태.운행중, now, now);
        return true;
    }
    public void Upsert(DriverLocationSnapshot item) => throw new NotSupportedException();
}

internal sealed class 용달대상후보시험Service : I용달운송대상후보Service
{
    public Task<배차추천후보?> 대상후보조회Async(운송원장 queue, string driverId, CancellationToken ct = default)
        => Task.FromResult<배차추천후보?>(new(driverId, 100m, "fixture target"));
}
