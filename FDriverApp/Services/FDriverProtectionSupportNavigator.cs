namespace FDriverApp.Services;

public interface IFDriverProtectionSupportNavigator
{
    Task OpenAsync(string orderNo);
    Task BackAsync();
    Task LoginAsync();
    Task<bool> ResumeAfterLoginAsync();
}

public sealed class FDriverProtectionSupportNavigator(IFDriverAuthSession session, FDriverSupportReturnContext returnContext) : IFDriverProtectionSupportNavigator
{
    public const string RouteName = "food-delivery-support";
    public const string OrderQueryKey = "orderNo";
    private bool _navigating;
    public Task OpenAsync(string orderNo) => NavigateAsync(async shell =>
    {
        if (string.IsNullOrWhiteSpace(orderNo)) return;
        returnContext.Capture(orderNo, session.UserId);
        await shell.GoToAsync(RouteName, new ShellNavigationQueryParameters { [OrderQueryKey] = orderNo });
    });
    public Task BackAsync() => NavigateAsync(shell =>
    {
        returnContext.Clear();
        return shell.Navigation.NavigationStack.Count > 1 ? shell.GoToAsync("..")
            : shell.GoToAsync("//" + FDriverWorkspaceNavigator.WorkspaceRoute);
    });
    public Task LoginAsync() => NavigateAsync(async shell =>
    {
        returnContext.Arm();
        // 서버가 인증을 거절했어도 로컬 토큰의 만료 시각은 남아 있을 수 있습니다.
        // 재인증 화면으로 이동할 때 업무 상태를 완료하지 않고 로컬 인증만 정리합니다.
        var revision = session.SessionRevision;
        try { await session.TryClearAsync(revision); }
        catch (Exception ex) when (ex is not OperationCanceledException
            && session.CurrentState == Ssalddel.Client.Infrastructure.Security.ClientAuthSessionRestoreState.Anonymous) { }
        await shell.GoToAsync("//" + FDriverWorkspaceNavigator.WorkspaceRoute);
    });
    public Task<bool> ResumeAfterLoginAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (_navigating || Shell.Current is null) return false;
        var source = returnContext.Take(session.UserId, session.IsAuthenticated
            && (session.Roles.Contains("기사", StringComparer.OrdinalIgnoreCase)
                || session.Roles.Contains("Driver", StringComparer.OrdinalIgnoreCase)));
        if (source is null) return false;
        await OpenAsync(source); return true;
    });
    private Task NavigateAsync(Func<Shell, Task> action) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (_navigating || Shell.Current is not { } shell) return;
        _navigating = true;
        try { await action(shell); }
        finally { _navigating = false; }
    });
}
