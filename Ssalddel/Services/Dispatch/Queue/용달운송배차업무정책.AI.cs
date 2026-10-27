using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

public sealed partial class 용달운송배차업무정책
{
    /// <summary>입력의 로컬 일회성 지문이다. 원문·주소·식별자를 모델/로그/응답에 보내지 않는다.</summary>
    private async Task<string?> AI입력판본Async(운송원장 queue, CancellationToken ct)
    {
        var canonical = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        var request = await _db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        if (canonical is null || request is null) return null;
        var cargo = await _db.화물요구조건.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        // 호출자의 저장 전 제어 상태와 DB 정본을 각각 결속한다. 추적 중 entity를 Reload/Attach하지 않는다.
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { canonical, request, cargo, input = queue })));
    }
}
