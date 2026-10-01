namespace 홍달.Services.Options;

public enum TransientStateProvider
{
    Memory,
    Redis
}

public sealed class TransientStateOptions
{
    public const string SectionName = "TransientState";

    /// <summary>
    /// Memory는 단일 프로세스 개발용이며 재시작 시 상태가 사라집니다.
    /// Redis는 단일 VM 또는 다중 인스턴스에서 공유할 실행 상태에 사용합니다.
    /// </summary>
    public TransientStateProvider Provider { get; set; } = TransientStateProvider.Memory;
}
