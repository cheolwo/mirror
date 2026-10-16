using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Tests.Services.Dispatch.Common;

// 기존 배차 시험이 필요로 하는 명시적인 수신 동의다. 제품 기본값을 바꾸지 않는다.
internal sealed class 운영배차시험수신UseCase : I운영배차공통UseCase
{
    public static 운영배차시험수신UseCase 허용 => new();
    public bool 수신On { get; set; } = true;
    public bool 자동중지 { get; set; }
    public bool 서버허용 { get; set; } = true;
    public Task<운영배차수신상태Dto> 수신상태조회Async(string id, CancellationToken ct = default)
        => Task.FromResult(new 운영배차수신상태Dto
        {
            주체Id = id,
            수신의사Code = 수신On ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
            실효상태Code = 서버허용 ? 운영배차실효상태Code.배차가능 : 운영배차실효상태Code.서버일시정지,
            수신의사변경시각Utc = DateTimeOffset.UtcNow.AddMinutes(-1),
            실효상태변경시각Utc = DateTimeOffset.UtcNow.AddMinutes(-1),
            서버관측시각Utc = DateTimeOffset.UtcNow,
            미응답자동중지여부 = 자동중지
        });
    public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid requestId, string state, string reason, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> 활동사건기록Async(운영배차활동사건Dto item, CancellationToken ct = default) => throw new NotSupportedException();
}
