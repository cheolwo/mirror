using Ssalddel.Contracts.Admin.DispatchTraining;

namespace SsalddelAdminApp.Services;

/// <summary>같은 관리자 계정의 페이지 왕복에서 불확실한 명령 번호를 유지합니다. 자동 재전송하지 않습니다.</summary>
public sealed class DispatchTrainingPendingSession
{
    public string? Owner { get; private set; }
    public string? Action { get; private set; }
    public DispatchTrainingCommandRequest? Request { get; private set; }
    public void Bind(string? owner) { if (Owner == owner) return; Owner = owner; Clear(); }
    public void Prepare(string action, DispatchTrainingCommandRequest request) { Action = action; Request = request; }
    public void Clear() { Action = null; Request = null; }
}
