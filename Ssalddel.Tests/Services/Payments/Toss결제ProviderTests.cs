using Microsoft.Extensions.Options;
using 살뜰.Services.External.Toss;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;

namespace Ssalddel.Tests.Services.Payments;

public sealed class Toss결제ProviderTests
{
    [Fact]
    public async Task Simulation에서는_Toss_승인을_호출하지_않는다()
    {
        var toss = new 기록용TossPaymentsService();
        var provider = new Toss결제Provider(toss, CreatePolicy(SsalddelExecutionMode.Simulation));

        var result = await provider.결제승인Async(new 결제승인요청("payment-key", "order-id", 1000));

        Assert.False(result.IsSuccess);
        Assert.Contains("OperationalModeRequired", result.ResponseJson, StringComparison.Ordinal);
        Assert.Equal(0, toss.CallCount);
    }

    [Fact]
    public async Task Operational에서는_Toss_승인을_호출한다()
    {
        var toss = new 기록용TossPaymentsService();
        var provider = new Toss결제Provider(toss, CreatePolicy(SsalddelExecutionMode.Operational));

        var result = await provider.결제승인Async(new 결제승인요청("payment-key", "order-id", 1000));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, toss.CallCount);
    }

    private static ISsalddelExecutionModePolicy CreatePolicy(SsalddelExecutionMode mode)
        => new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode }));

    private sealed class 기록용TossPaymentsService : ITossPaymentsService
    {
        public int CallCount { get; private set; }

        public Task<TossConfirmResult> ConfirmAsync(TossConfirmApiRequest request)
        {
            CallCount++;
            var json = System.Text.Json.JsonSerializer.Serialize(new { status = "DONE", paymentKey = request.PaymentKey,
                orderId = request.OrderId, totalAmount = request.Amount, currency = "KRW", method = "CARD" });
            return Task.FromResult(new TossConfirmResult(true, json, "CARD"));
        }
    }
}
