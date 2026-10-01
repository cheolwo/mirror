using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Ui.Common;

public sealed class 판매채널ClientCrudTests
{
    [Fact]
    public async Task 수정삭제는_업무리소스별PutDelete경로를사용한다()
    {
        var api = new RecordingJsonApiClient();
        var client = new 판매채널Client(api);

        await client.계정수정Async(11, new 판매채널계정저장요청());
        await client.상품수정Async(22, new 판매상품저장요청());
        await client.출품수정Async(33, new 채널출품저장요청());
        await client.출품삭제Async(33);
        await client.상품삭제Async(22);
        await client.계정삭제Async(11);

        Assert.Equal(
        [
            (HttpMethod.Put, "api/v1/sales-channels/accounts/11"),
            (HttpMethod.Put, "api/v1/sales-channels/products/22"),
            (HttpMethod.Put, "api/v1/sales-channels/listings/33"),
            (HttpMethod.Delete, "api/v1/sales-channels/listings/33"),
            (HttpMethod.Delete, "api/v1/sales-channels/products/22"),
            (HttpMethod.Delete, "api/v1/sales-channels/accounts/11")
        ], api.Requests);
    }

    private sealed class RecordingJsonApiClient : IHongdalJsonApiClient
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TResponse?>(default);

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((method, path));
            return Task.FromResult<TResponse?>(default);
        }

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((method, path));
            return Task.FromResult<TResponse?>(default);
        }

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((method, path));
            return Task.CompletedTask;
        }

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((method, path));
            return Task.CompletedTask;
        }
    }
}
