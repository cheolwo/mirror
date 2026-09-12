using MediatR;

namespace Ssalddel.Application.Driver.Transport;

public sealed record 운송문제신고됨Event(
    string 기사Id,
    long 운송Id,
    string 운송번호,
    string 단계,
    string 예외코드,
    string 사유,
    string? 메모,
    string? 증빙ObjectName,
    string? 증빙Url,
    bool 관리자확인필요,
    DateTime 발생시각Utc,
    string TraceId) : INotification
{
    public int? 전체수량 { get; init; }
    public int? 정상확인수량 { get; init; }
    public int? 영향수량 { get; init; }
    public bool 현장진행불가 { get; init; }
    public string 업무통제상태Code { get; init; } = string.Empty;
    public string 보류범위Code { get; init; } = string.Empty;
}
