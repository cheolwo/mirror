namespace DriverApp.Services;

public interface I기사위치송신Service
{
    bool IsRunning { get; }
    string? Notice => null;
    event Action? Changed;
    Task StartAsync(기사위치송신시작요청 request, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class 기사위치송신시작요청
{
    public int 권장위치전송간격초 { get; set; } = 30;
    public decimal 상차접근허용반경Km { get; set; } = 10m;
    public string 운행상태 { get; set; } = "운행중";
}
