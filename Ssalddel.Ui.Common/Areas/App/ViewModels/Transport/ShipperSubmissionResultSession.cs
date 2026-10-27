namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;

/// <summary>
/// 화주 접수 결과를 한 번 조회하고, 같은 로그인 수명에서만 정본 확인을 이어갑니다.
/// POST·재전송·원문 저장·동의·화면 이동 정책은 앱과 웹의 기존 접수 조율자가 소유합니다.
/// </summary>
public sealed class ShipperSubmissionResultSession(
    Func<ShipperSubmissionIdentity> currentIdentity,
    string invalidSessionMessage) : IDisposable
{
    public long Generation { get; private set; }
    public bool IsDisposed { get; private set; }

    /// <summary>현재 비동기 접수 확인에만 쓰는 메모리 참조입니다. 기록·로그·직렬화용 계약이 아닙니다.</summary>
    public sealed class Context
    {
        internal Context(ShipperSubmissionIdentity identity, long generation)
        { Identity = identity; Generation = generation; }
        internal ShipperSubmissionIdentity Identity { get; }
        internal long Generation { get; }
        public string Owner => Identity.Owner!;
        public override string ToString() => nameof(Context);
    }

    public Context Capture()
    {
        var identity = currentIdentity();
        if (!identity.IsUsable) throw new InvalidOperationException(invalidSessionMessage);
        return new(identity, Generation);
    }

    public bool IsCurrent(Context context)
    {
        if (IsDisposed || context.Generation != Generation) return false;
        var identity = currentIdentity();
        return identity.IsUsable && identity == context.Identity;
    }

    public void Invalidate() => ++Generation;

    /// <summary>
    /// 조회 미발견·조회/확인 실패·이전 계정의 응답을 구분합니다. 미발견은 미접수 확정이나 재전송 허가가 아닙니다.
    /// 앱은 원문·동의·만료를 별도로 검사해 명시 재시도하고, 웹은 기존 접수 번호의 조회만 계속합니다.
    /// </summary>
    public async Task<ShipperSubmissionResult> ReconcileAsync<T>(
        Context context,
        Func<Task<T?>> readResult,
        Func<T, Task> confirmResult) where T : class
    {
        if (!IsCurrent(context)) return new(ShipperSubmissionResultKind.Superseded);
        try
        {
            var found = await readResult();
            if (!IsCurrent(context)) return new(ShipperSubmissionResultKind.Superseded);
            if (found is null) return new(ShipperSubmissionResultKind.NotFound);
            await confirmResult(found);
            return new(IsCurrent(context) ? ShipperSubmissionResultKind.Confirmed : ShipperSubmissionResultKind.Superseded);
        }
        catch (Exception error)
        {
            return IsCurrent(context)
                ? new(ShipperSubmissionResultKind.Failed, error)
                : new(ShipperSubmissionResultKind.Superseded);
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        Invalidate();
    }
}

/// <summary>접수 확인의 메모리 전용 계정 경계. 앱의 세션 revision과 웹 token은 비공개 비교값으로만 보유합니다.</summary>
public sealed record ShipperSubmissionIdentity
{
    private readonly string? sessionVersion;
    public ShipperSubmissionIdentity(string? owner, string? sessionVersion, bool isAuthenticated)
    { Owner = owner; this.sessionVersion = sessionVersion; IsAuthenticated = isAuthenticated; }
    public string? Owner { get; }
    public bool IsAuthenticated { get; }
    internal bool IsUsable => IsAuthenticated && !string.IsNullOrWhiteSpace(Owner) && !string.IsNullOrWhiteSpace(sessionVersion);
    public override string ToString() => nameof(ShipperSubmissionIdentity);
}

public enum ShipperSubmissionResultKind { Confirmed, NotFound, Failed, Superseded }

public sealed record ShipperSubmissionResult(ShipperSubmissionResultKind Kind, Exception? Error = null);
