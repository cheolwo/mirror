using FluentResults;
using Hongdal.Application.Abstractions;
using 홍달.도메인.사용자;

namespace Hongdal.Application.Driver.Work;

public sealed record 운행시작Command : 홍달CommandBase, IRequest<Result<Hongdal.Contracts.Driver.Work.기사운행시작응답>>
{
    public 운행시작Command(
        string driverId,
        string 시작모드,
        DateTime? 시작시각,
        string 시작위치,
        string? 복귀지,
        string? 오늘의복귀지주소,
        decimal? 오늘의복귀지위도,
        decimal? 오늘의복귀지경도,
        bool 기본복귀지사용,
        string? 복귀지출처,
        string? 복귀콜선호,
        bool 커뮤니티운행공개,
        bool 커뮤니티구단위위치공개동의)
    {
        기사Id = string.IsNullOrWhiteSpace(driverId) ? string.Empty : driverId;
        this.시작모드 = 시작모드;
        this.시작시각 = 시작시각;
        this.시작위치 = 시작위치;
        this.복귀지 = 복귀지;
        this.오늘의복귀지주소 = 오늘의복귀지주소;
        this.오늘의복귀지위도 = 오늘의복귀지위도;
        this.오늘의복귀지경도 = 오늘의복귀지경도;
        this.기본복귀지사용 = 기본복귀지사용;
        this.복귀지출처 = 복귀지출처;
        this.복귀콜선호 = 복귀콜선호;
        this.커뮤니티운행공개 = 커뮤니티운행공개;
        this.커뮤니티구단위위치공개동의 = 커뮤니티구단위위치공개동의;
        참여자Id = 기사Id;
        실행역할 = 홍달역할유형.기사;
    }

    public string 기사Id { get; init; } = string.Empty;
    public string 시작모드 { get; init; } = string.Empty;
    public DateTime? 시작시각 { get; init; }
    public string 시작위치 { get; init; } = string.Empty;
    public string? 복귀지 { get; init; }
    public string? 오늘의복귀지주소 { get; init; }
    public decimal? 오늘의복귀지위도 { get; init; }
    public decimal? 오늘의복귀지경도 { get; init; }
    public bool 기본복귀지사용 { get; init; }
    public string? 복귀지출처 { get; init; }
    public string? 복귀콜선호 { get; init; }
    public bool 커뮤니티운행공개 { get; init; }
    public bool 커뮤니티구단위위치공개동의 { get; init; }
}
