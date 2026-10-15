using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using 살뜰.Services.External.Toss;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;

namespace Ssalddel.Tests.Services.Payments;

public sealed class TossPaymentStateR73Tests
{
    [Theory]
    [InlineData("WAITING_FOR_DEPOSIT")]
    [InlineData("DONE")]
    [InlineData("CANCELED")]
    [InlineData("PARTIAL_CANCELED")]
    public async Task HTTP성공의정본상태를보존하고입금알림비밀은해시만전달한다(string status)
    {
        var transport = new HttpProbe((_, _) => Task.FromResult(JsonResponse(Fact(status, secret: "synthetic-deposit-secret"))));
        var provider = Provider(transport);
        var result = await provider.결제승인Async(new("key-r73", "order-r73", 6000));
        Assert.True(result.IsSuccess); Assert.Equal(status, result.ProviderStatus);
        Assert.DoesNotContain("synthetic-deposit-secret", result.ResponseJson);
        Assert.DoesNotContain("\"secret\"", result.ResponseJson);
        Assert.Equal(TossPaymentResponsePolicy.HashSecret("synthetic-deposit-secret"), result.DepositCallbackVerificationHash);
        Assert.Equal("POST", Assert.Single(transport.Methods));
        Assert.Equal("Basic", transport.AuthenticationScheme);
    }

    [Theory]
    [InlineData("paymentKey", "wrong-key")]
    [InlineData("orderId", "wrong-order")]
    [InlineData("currency", "USD")]
    [InlineData("status", "UNKNOWN")]
    [InlineData("totalAmount", "1")]
    public async Task 결제키주문금액통화상태가다르면HTTP200이어도확정하지않는다(string field, string value)
    {
        var data = new Dictionary<string, object> { ["paymentKey"] = "key-r73", ["orderId"] = "order-r73", ["currency"] = "KRW", ["status"] = "DONE", ["totalAmount"] = 6000 };
        data[field] = field == "totalAmount" ? int.Parse(value) : value;
        var result = await Provider(new((_, _) => Task.FromResult(JsonResponse(JsonSerializer.Serialize(data)))))
            .결제승인Async(new("key-r73", "order-r73", 6000));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task 비숫자금액은예외없이미확정응답으로남긴다()
    {
        foreach (var field in new[] { "totalAmount", "balanceAmount" })
        {
            var data = new Dictionary<string, object>
            {
                ["paymentKey"] = "key-r73", ["orderId"] = "order-r73", ["currency"] = "KRW",
                ["status"] = "DONE", ["totalAmount"] = 6000, ["balanceAmount"] = 6000
            };
            data[field] = "not-a-number";
            var result = await Provider(new((_, _) => Task.FromResult(JsonResponse(JsonSerializer.Serialize(data)))))
                .결제승인Async(new("key-r73", "order-r73", 6000));
            Assert.False(result.IsSuccess);
            Assert.True(result.IsUncertain);
        }
    }

    [Fact]
    public async Task 같은결제조회는인증된GET만보내고재승인POST를보내지않는다()
    {
        var probe = new HttpProbe((_, _) => Task.FromResult(JsonResponse(Fact("DONE"))));
        var result = await Provider(probe).결제조회Async(new("key-r73", "order-r73", 6000));
        Assert.True(result.IsSuccess); Assert.Equal("GET", Assert.Single(probe.Methods));
        Assert.Equal("/v1/payments/key-r73", Assert.Single(probe.Paths)); Assert.Equal("Basic", probe.AuthenticationScheme);
    }

    [Fact]
    public async Task 통신유실은불확실결과로남기고자동재전송하지않는다()
    {
        var probe = new HttpProbe((_, _) => throw new HttpRequestException("synthetic transport failure"));
        var result = await Provider(probe).결제승인Async(new("key-r73", "order-r73", 6000));
        Assert.False(result.IsSuccess); Assert.True(result.IsUncertain); Assert.Single(probe.Methods);
    }

    [Fact]
    public async Task Simulation조회와승인은HTTP를호출하지않는다()
    {
        var probe = new HttpProbe((_, _) => throw new InvalidOperationException("Must not call provider."));
        var provider = Provider(probe, simulation: true);
        Assert.False((await provider.결제승인Async(new("key-r73", "order-r73", 6000))).IsSuccess);
        Assert.False((await provider.결제조회Async(new("key-r73", "order-r73", 6000))).IsSuccess);
        Assert.Empty(probe.Methods);
    }

    private static Toss결제Provider Provider(HttpProbe probe, bool simulation = false) => new(
        new TossPaymentsService(new HttpClient(probe) { BaseAddress = new("https://toss.invalid") }, Options.Create(new TossPaymentsOptions { SecretKey = "synthetic-test-key" })),
        new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = simulation ? SsalddelExecutionMode.Simulation : SsalddelExecutionMode.Operational })));
    private static string Fact(string status, string? secret = null) => JsonSerializer.Serialize(new { status, paymentKey = "key-r73", orderId = "order-r73", totalAmount = 6000, currency = "KRW", method = "가상계좌", secret });
    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class HttpProbe(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        public List<string> Methods { get; } = []; public List<string> Paths { get; } = []; public string? AuthenticationScheme { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Methods.Add(request.Method.Method); Paths.Add(request.RequestUri!.AbsolutePath); AuthenticationScheme = request.Headers.Authorization?.Scheme; return reply(request, cancellationToken); }
    }
}
