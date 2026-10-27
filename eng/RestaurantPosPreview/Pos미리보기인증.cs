using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace RestaurantDeskApp;

internal static class Pos미리보기인증
{
    public static ClientAuthSession CreateSession()
    {
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        session.ApplyAsync(new("pos-preview-access", DateTime.UtcNow.AddHours(12), "pos-preview-refresh",
            DateTime.UtcNow.AddHours(24), "pos-preview-operator", "화면 검토", ["음식점"]))
            .GetAwaiter().GetResult();
        return session;
    }

    public static RestaurantAuthService CreateAuth(ClientAuthSession session)
        => new(new HttpClient(new NoNetwork()) { BaseAddress = new("http://preview.invalid/") }, session);

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("POS 합성 검토는 외부 HTTP를 사용하지 않습니다.");
    }

    private sealed class MemoryStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { value = null; return Task.CompletedTask; }
    }
}
