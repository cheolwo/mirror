using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace RestaurantDeskApp;

// 합성 메뉴 미리보기의 circuit 전용 메모리 세션. 실제 인증·HTTP의 대체 실패 경로가 아니다.
internal static class 메뉴미리보기인증
{
    public static RestaurantAuthService Create()
    {
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        session.ApplyAsync(new("preview-only-access", DateTime.UtcNow.AddHours(1), "preview-only-refresh",
            DateTime.UtcNow.AddHours(2), "preview-only-restaurant", "화면 검토용 가게", ["음식점"])).GetAwaiter().GetResult();
        return new RestaurantAuthService(new HttpClient(new NoNetwork()) { BaseAddress = new("http://preview.invalid/") }, session);
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("합성 메뉴 미리보기 인증은 외부 HTTP를 사용하지 않습니다.");
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
