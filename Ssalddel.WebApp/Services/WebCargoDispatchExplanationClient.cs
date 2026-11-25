using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

public sealed class WebCargoDispatchExplanationClient : AuthenticatedCargoDispatchExplanationClient
{
    private readonly WebAuthSessionService session;
    private long revision;
    private string? lastOwner, lastToken;
    private bool lastAuthenticated;
    private readonly object sessionGate = new();
    public WebCargoDispatchExplanationClient(HttpClient http, WebAuthSessionService session) : base(http)
    {
        this.session = session; lastOwner = session.UserId; lastToken = session.AccessToken;
        lastAuthenticated = session.IsLoggedIn; session.Changed += OnChanged;
    }
    public override CargoDispatchExplanationIdentity Identity => new(session.UserId, Interlocked.Read(ref revision), session.IsLoggedIn);
    protected override string? AccessToken => session.AccessToken;
    public override Task RestoreAsync(CancellationToken cancellationToken = default)
        => session.IsLoggedIn ? Task.CompletedTask : session.RestoreAsync(cancellationToken);
    protected override Task EnsureAuthorizedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!session.IsLoggedIn) throw new InvalidOperationException("로그인 정보를 다시 확인해 주세요.");
        return Task.CompletedTask;
    }
    private void OnChanged()
    {
        lock (sessionGate)
        {
            var owner = session.UserId; var token = session.AccessToken; var authenticated = session.IsLoggedIn;
            if (owner == lastOwner && token == lastToken && authenticated == lastAuthenticated) return;
            lastOwner = owner; lastToken = token; lastAuthenticated = authenticated;
            Interlocked.Increment(ref revision);
        }
        NotifySessionChanged();
    }
    public override void Dispose() { session.Changed -= OnChanged; base.Dispose(); }
}
