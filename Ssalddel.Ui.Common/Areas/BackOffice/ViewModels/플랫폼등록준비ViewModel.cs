using Ssalddel.Contracts.Common.Operations;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public sealed class 플랫폼등록준비ViewModel(Func<CancellationToken, Task<FreightPlatformReadinessDto>> read) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public event Action? Changed;
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public FreightPlatformReadinessDto? Current { get; private set; }
    public async Task LoadAsync()
    {
        if (Busy || disposed) return;
        Busy = true; Error = null; Current = null; Changed?.Invoke();
        try { var value = await read(lifetime.Token); if (!disposed) Current = value; }
        catch (OperationCanceledException) when (disposed) { }
        catch { if (!disposed) Error = "등록 준비 현황을 확인하지 못했습니다. 다시 확인해 주세요."; }
        finally { if (!disposed) { Busy = false; Changed?.Invoke(); } }
    }
    public static string Status(string code) => code switch
    { "Prepared" => "자료 준비", "Submitted" => "접수 기록", "Confirmed" => "확인 기록", "Pending" => "확인 중", "Expired" => "효력 만료", "Rejected" => "보완 필요", _ => "미확인" };
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
}
