using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Services;

public sealed class ShipperCargoDispatchExplanationClient : AuthenticatedCargoDispatchExplanationClient
{
    private readonly IAuthSession session;
    private readonly AuthApiService auth;
    private CargoDispatchExplanationIdentity lastIdentity;
    private readonly object sessionGate = new();
    public ShipperCargoDispatchExplanationClient(HttpClient http, IAuthSession session, AuthApiService auth) : base(http)
    {
        this.session = session; this.auth = auth; lastIdentity = Identity; session.Changed += OnChanged;
    }
    public override CargoDispatchExplanationIdentity Identity => new(session.UserId, session.SessionRevision, session.IsLoggedIn);
    protected override string? AccessToken => session.AccessToken;
    public override Task RestoreAsync(CancellationToken cancellationToken = default) => session.RestoreAsync(cancellationToken);
    protected override async Task EnsureAuthorizedAsync(CancellationToken ct)
    {
        var error = await auth.EnsureAccessTokenAsync(cancellationToken: ct);
        if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("로그인 정보를 다시 확인해 주세요.");
    }
    private void OnChanged()
    {
        lock (sessionGate)
        {
            var current = Identity;
            if (current == lastIdentity) return;
            lastIdentity = current;
        }
        NotifySessionChanged();
    }
    public override void Dispose() { session.Changed -= OnChanged; base.Dispose(); }
}
