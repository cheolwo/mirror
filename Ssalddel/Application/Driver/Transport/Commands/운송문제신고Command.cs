using FluentResults;
using Ssalddel.Application.Abstractions;
using 살뜰.도메인.사용자;

namespace Ssalddel.Application.Driver.Transport;

public sealed record 운송문제신고Command : 살뜰CommandBase, IRequest<Result<Ssalddel.Contracts.Driver.Transport.기사운송요약응답>>
{
    public 운송문제신고Command(string driverId, long id, string 사유, string? 메모)
        : this(driverId, id, null, null, 사유, 메모, null, null, false)
    {
    }

    public 운송문제신고Command(
        string driverId,
        long id,
        string? 단계,
        string? 예외코드,
        string? 사유,
        string? 메모,
        string? 증빙ObjectName,
        string? 증빙Url,
        bool 관리자확인요청,
        int? 정상확인수량 = null,
        int? 영향수량 = null,
        bool 현장진행불가 = false)
    {
        기사Id = string.IsNullOrWhiteSpace(driverId) ? string.Empty : driverId;
        Id = id;
        this.단계 = 단계;
        this.예외코드 = 예외코드;
        this.사유 = 사유 ?? string.Empty;
        this.메모 = 메모;
        this.증빙ObjectName = 증빙ObjectName;
        this.증빙Url = 증빙Url;
        this.관리자확인요청 = 관리자확인요청;
        this.정상확인수량 = 정상확인수량;
        this.영향수량 = 영향수량;
        this.현장진행불가 = 현장진행불가;
        참여자Id = 기사Id;
        실행역할 = 살뜰역할유형.기사;
    }

    public string 기사Id { get; init; } = string.Empty;
    public long Id { get; init; }
    public string? 단계 { get; init; }
    public string? 예외코드 { get; init; }
    public string 사유 { get; init; } = string.Empty;
    public string? 메모 { get; init; }
    public string? 증빙ObjectName { get; init; }
    public string? 증빙Url { get; init; }
    public bool 관리자확인요청 { get; init; }
    public int? 정상확인수량 { get; init; }
    public int? 영향수량 { get; init; }
    public bool 현장진행불가 { get; init; }
}
