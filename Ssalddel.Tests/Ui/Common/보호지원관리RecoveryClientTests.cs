using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 보호지원관리RecoveryClientTests
{
    [Fact]
    public async Task 조회는원요청근거를관리자경로에결속하고복구는같은본문을전달한다()
    {
        var api = new Api(); var client = new 보호지원관리Client(api);
        var request = new NeighborhoodMemberRecoveryRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 17,
            SupportCaseId = "case &/가", ResolutionCode = "CancelBeforePickup" };
        await client.배송복구조회Async("delivery /가", request, default);
        Assert.Equal("api/v1/admin/neighborhood-member-deliveries/delivery%20%2F%EA%B0%80/recovery?supportCaseId=case%20%26%2F%EA%B0%80"
            + "&clientRequestId=" + request.ClientRequestId.ToString("D") + "&expectedRevision=17&resolutionCode=CancelBeforePickup", api.Path);
        Assert.Equal(HttpMethod.Get, api.Method); Assert.False(api.AllowNotFound); Assert.Null(api.Body);
        await client.배송복구Async("delivery /가", request, default);
        Assert.Equal("api/v1/admin/neighborhood-member-deliveries/delivery%20%2F%EA%B0%80/recover", api.Path);
        Assert.Equal(HttpMethod.Post, api.Method); Assert.Same(request, api.Body);
    }

    [Fact]
    public async Task 빈조회와빈복구응답을성공으로대체하지않는다()
    {
        var client = new 보호지원관리Client(new Api { Empty = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.배송복구조회Async("delivery", new(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.배송복구Async("delivery", new(), default));
    }

    private sealed class Api : ISsalddelJsonApiClient
    {
        public bool Empty { get; init; }
        public HttpMethod? Method; public string? Path; public object? Body; public bool AllowNotFound;
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            Method = HttpMethod.Get; Path = path; AllowNotFound = allowNotFound; Body = null;
            return Task.FromResult(Empty ? default : (TResponse?)(object)new NeighborhoodMemberRecoveryContextResponse());
        }
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request,
            string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        {
            Method = method; Path = path; Body = request; AllowNotFound = allowNotFound;
            return Task.FromResult(Empty ? default : (TResponse?)(object)new NeighborhoodMemberRecoveryResponse());
        }
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
