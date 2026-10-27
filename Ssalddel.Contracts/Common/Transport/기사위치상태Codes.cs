namespace Ssalddel.Contracts.Common.Transport;

/// <summary>현재 배정 운송의 기사 위치 표시 상태입니다. 오래된 좌표를 현재 위치로 사용하지 않습니다.</summary>
public static class 기사위치상태Codes
{
    public const string Available = "Available";
    public const string Delayed = "Delayed";
    public const string Unavailable = "Unavailable";
    public const string Closed = "Closed";
}
