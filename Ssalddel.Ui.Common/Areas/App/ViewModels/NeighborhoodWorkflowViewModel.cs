using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>기술적 수명만 공유합니다. 업무 권한·전이는 각 서버 응답이 결정합니다.</summary>
public abstract class NeighborhoodWorkflowViewModel(ISsalddel현재사용자Context user) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private long _generation;
    protected bool Disposed { get; private set; }
    protected string? Owner { get; private set; }
    protected CancellationToken Token => _lifetime.Token;
    public bool RequiresLogin { get; protected set; }
    public bool IsAuthenticated => CurrentOwner is not null && !RequiresLogin;
    public bool IsLoading { get; protected set; }
    public bool IsSending { get; protected set; }
    public string? Error { get; protected set; }
    public string? Notice { get; protected set; }
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;

    public void SynchronizeOwner()
    {
        if (Disposed || string.Equals(Owner, CurrentOwner, StringComparison.Ordinal)) return;
        Owner = CurrentOwner; ++_generation; RequiresLogin = false; IsLoading = false; IsSending = false;
        Error = null; Notice = null; ResetForOwner(); Changed();
    }
    public void ResetAuthentication()
    {
        if (Disposed) return;
        SynchronizeOwner(); ++_generation; RequiresLogin = false; IsLoading = false; IsSending = false;
        Error = null; Notice = null; ResetForOwner(); Changed();
    }
    protected abstract void ResetForOwner();
    protected bool CheckOwner()
    {
        SynchronizeOwner();
        if (Disposed || !IsAuthenticated) { if (!Disposed) { RequiresLogin = true; Error = "로그인 후 확인해 주세요."; Changed(); } return false; }
        return true;
    }
    protected (long Generation, string? Owner) Begin() => (++_generation, Owner);
    protected bool Current((long Generation, string? Owner) operation)
    {
        if (Disposed) return false;
        if (!string.Equals(Owner, CurrentOwner, StringComparison.Ordinal)) { SynchronizeOwner(); return false; }
        return operation.Generation == _generation && operation.Owner == Owner;
    }
    protected void ApplyError(Exception exception, bool writing = false)
    {
        Error = exception is SsalddelApiException { StatusCode: 401 } ? "로그인이 만료되었습니다. 로그인 후 돌아와 처리 결과를 확인해 주세요."
            : exception is SsalddelApiException { StatusCode: 403 } ? "이 정보를 확인하거나 변경할 권한이 없습니다."
            : exception is SsalddelApiException { StatusCode: 409 } ? "상태나 조건이 바뀌었습니다. 최신 정보를 확인해 주세요."
            : exception is SsalddelApiException { StatusCode: 400 or 422 } ? "입력 내용과 현재 조건을 확인해 주세요."
            : writing ? "처리 결과를 확인하지 못했습니다. 결과를 조회한 뒤 같은 요청으로 다시 시도할 수 있습니다."
            : "정보를 불러오지 못했습니다. 연결을 확인하고 다시 시도해 주세요.";
        if (exception is SsalddelApiException { StatusCode: 401 }) RequiresLogin = true;
    }
    protected static bool DefinitiveRejection(Exception exception) => exception is SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 };
    protected void Changed() { if (!Disposed) OnPropertyChanged(string.Empty); }
    public virtual void Dispose()
    {
        if (Disposed) return;
        Disposed = true; ++_generation; _lifetime.Cancel(); _lifetime.Dispose();
    }
}
