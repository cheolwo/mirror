using Hongdal.Application.CommandProcessing;
using 홍달.Services.Sales;

namespace Hongdal.Tests.Services.Sales;

public sealed class SalesChannelService사용자경계Tests
{
    [Theory]
    [InlineData("accounts")]
    [InlineData("products")]
    [InlineData("listings")]
    public async Task 목록조회는_현재사용자Id가없으면_전체자료를조회하지않고거부한다(string resource)
    {
        var service = new SalesChannelService(
            null!,
            new TestCurrentUserAccessor(null, null),
            null!);

        Func<Task> action = resource switch
        {
            "accounts" => async () => await service.GetAccountsAsync(CancellationToken.None),
            "products" => async () => await service.GetProductsAsync(CancellationToken.None),
            "listings" => async () => await service.GetListingsAsync(CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.Equal("로그인 사용자를 확인할 수 없습니다.", exception.Message);
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;
}
