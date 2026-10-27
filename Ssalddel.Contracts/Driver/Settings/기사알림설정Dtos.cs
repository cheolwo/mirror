namespace Ssalddel.Contracts.Driver.Settings;

public sealed class 기사알림설정응답
{
    public string DriverId { get; set; } = string.Empty;
    public bool 배차추천알림사용 { get; set; }
    public bool 음식배달추천알림사용 { get; set; }
    public bool 화물운송추천알림사용 { get; set; }
    public bool 운전중푸시만사용 { get; set; }
    public bool 소리사용 { get; set; }
    public bool 진동사용 { get; set; }
    public bool 야간알림제한 { get; set; }
    public bool 정차후모아보기 { get; set; }
}

public sealed class 기사알림설정수정요청
{
    public bool 배차추천알림사용 { get; set; }
    public bool 음식배달추천알림사용 { get; set; }
    public bool 화물운송추천알림사용 { get; set; }
    public bool 운전중푸시만사용 { get; set; }
    public bool 소리사용 { get; set; }
    public bool 진동사용 { get; set; }
    public bool 야간알림제한 { get; set; }
    public bool 정차후모아보기 { get; set; }
}
