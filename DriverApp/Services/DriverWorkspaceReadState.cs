namespace DriverApp.Services;

public static class DriverWorkspaceReadStatus
{
    public const string Unknown = "Unknown";
    public const string Current = "Current";
    public const string Failed = "Failed";
    public const string Sample = "Sample";
}

/// <summary>A missing/failed read is not an empty list or a zero financial amount.</summary>
public sealed record DriverWorkspaceSliceReadState(string StateCode, string Message, DateTime? VerifiedAtUtc = null)
{
    public bool IsCurrent => StateCode == DriverWorkspaceReadStatus.Current;
    public bool CanDisplayValues => IsCurrent || StateCode == DriverWorkspaceReadStatus.Sample;
    public static DriverWorkspaceSliceReadState Unknown(string label)
        => new(DriverWorkspaceReadStatus.Unknown, $"{label} 미확인");
    public static DriverWorkspaceSliceReadState Current()
        => new(DriverWorkspaceReadStatus.Current, string.Empty, DateTime.UtcNow);
    public static DriverWorkspaceSliceReadState Failed(string label)
        => new(DriverWorkspaceReadStatus.Failed, $"{label} 조회 실패 · 현재 상태는 확인되지 않았습니다.");
    public static DriverWorkspaceSliceReadState Sample()
        => new(DriverWorkspaceReadStatus.Sample, "예시 자료 · 서버 업무 상태가 아닙니다.");
}

public sealed record DriverWorkspaceReadState
{
    public DriverWorkspaceSliceReadState Work { get; init; } = DriverWorkspaceSliceReadState.Unknown("운행 상태");
    public DriverWorkspaceSliceReadState CurrentTransport { get; init; } = DriverWorkspaceSliceReadState.Unknown("현재 운송");
    public DriverWorkspaceSliceReadState TransportDetail { get; init; } = DriverWorkspaceSliceReadState.Unknown("운송 상세");
    public DriverWorkspaceSliceReadState Recommendations { get; init; } = DriverWorkspaceSliceReadState.Unknown("추천 운송");
    public DriverWorkspaceSliceReadState Settlement { get; init; } = DriverWorkspaceSliceReadState.Unknown("월 정산");
    public DriverWorkspaceSliceReadState Reservations { get; init; } = DriverWorkspaceSliceReadState.Unknown("예약");
    public DriverWorkspaceSliceReadState Realtime { get; init; } = DriverWorkspaceSliceReadState.Unknown("실시간 연결");
    public IReadOnlyList<string> Warnings => new[]
        { Work, CurrentTransport, TransportDetail, Recommendations, Settlement, Reservations, Realtime }
        .Where(x => x.StateCode == DriverWorkspaceReadStatus.Failed).Select(x => x.Message).ToArray();

    public static DriverWorkspaceReadState Sample { get; } = new()
    {
        Work = DriverWorkspaceSliceReadState.Sample(), CurrentTransport = DriverWorkspaceSliceReadState.Sample(),
        TransportDetail = DriverWorkspaceSliceReadState.Sample(), Recommendations = DriverWorkspaceSliceReadState.Sample(),
        Settlement = DriverWorkspaceSliceReadState.Sample(), Reservations = DriverWorkspaceSliceReadState.Sample(),
        Realtime = DriverWorkspaceSliceReadState.Sample()
    };
}
