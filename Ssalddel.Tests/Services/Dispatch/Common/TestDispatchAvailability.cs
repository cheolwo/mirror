using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Tests.Services.Dispatch.Common;

internal sealed class TestDispatchAvailability(string initialIntent = 운영배차수신의사Code.On) : I운영배차공통UseCase
{
    private readonly Dictionary<string, string> _intent = new(StringComparer.Ordinal);
    public bool Unavailable { get; set; }
    public Task<운영배차수신상태Dto> 수신상태조회Async(string 주체Id, CancellationToken cancellationToken = default)
        => Unavailable ? throw new InvalidOperationException("availability unavailable") : Task.FromResult(new 운영배차수신상태Dto
        {
            주체Id = 주체Id, 수신의사Code = _intent.GetValueOrDefault(주체Id, initialIntent),
            수신의사변경시각Utc = DateTimeOffset.UtcNow
        });
    public Task<운영배차수신상태Dto> 기사의사변경Async(string 기사Id, 운영배차수신의사변경요청 요청, CancellationToken cancellationToken = default)
    {
        _intent[기사Id] = 요청.수신의사Code;
        return 수신상태조회Async(기사Id, cancellationToken);
    }
    public Task<운영배차수신상태Dto> 서버실효상태변경Async(string 주체Id, Guid 요청Id, string 실효상태Code, string 실효사유Code, CancellationToken cancellationToken = default)
        => 수신상태조회Async(주체Id, cancellationToken);
    public Task<운영배차단기지표Dto> 단기지표조회Async(string 주체Id, CancellationToken cancellationToken = default)
        => Task.FromResult(new 운영배차단기지표Dto());
    public Task<bool> 활동사건기록Async(운영배차활동사건Dto 사건, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
