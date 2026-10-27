namespace CargoAiClientProbeR114;

/// <summary>기존 읽기 전용 probe의 transport를 같은 입력으로 소비하기 위한 로컬 도구 계약입니다.</summary>
internal sealed record ProbeInput
{
    public string BaseAddress { get; init; } = "http://127.0.0.1:5364/";
    public string AccessToken { get; init; } = "";
    public string DriverId { get; init; } = "";
    public string RequestId { get; init; } = "";
    public long? TransportId { get; init; }
    public string OutputRoot { get; init; } = "";
    public string Mode { get; init; } = "inspect";
    public string Stage { get; init; } = "current";
}
