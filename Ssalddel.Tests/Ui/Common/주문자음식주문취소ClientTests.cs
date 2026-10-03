using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 주문자음식주문취소ClientTests
{
    [Fact]
    public async Task 취소Client는_선택한주문번호와기존계약을보호Post에그대로전달한다()
    {
        var api = new RecordingApi();
        var client = new 주문자음식주문Client(api);
        var request = new 주문자음식주문취소요청
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = 11,
            사유Code = "DuplicateOrder", 사유 = "중복 주문"
        };
        var response = await client.취소Async(" FOOD A/01 ", request);
        Assert.Equal(HttpMethod.Post, api.Method);
        Assert.Equal("api/v1/food-orders/FOOD%20A%2F01/cancellation", api.Path);
        Assert.Same(request, api.Request);
        Assert.False(api.AllowNotFound);
        Assert.Equal("취소", response.상태);
    }

    [Fact]
    public async Task 취소응답없음은_정상취소로바꾸지않는다()
    {
        var client = new 주문자음식주문Client(new RecordingApi { Empty = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.취소Async("FOOD-A", new()));
    }

    private sealed class RecordingApi : ISsalddelJsonApiClient
    {
        public bool Empty { get; init; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public object? Request { get; private set; }
        public bool AllowNotFound { get; private set; }
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request,
            string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        {
            Method = method; Path = path; Request = request; AllowNotFound = allowNotFound;
            object? response = Empty ? null : new 음식주문응답 { 주문번호 = "FOOD A/01", 상태 = "취소" };
            return Task.FromResult((TResponse?)response);
        }
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
