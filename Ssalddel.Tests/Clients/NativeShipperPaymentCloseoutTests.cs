using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Payments;
using Ssalddel.Contracts.Shipper.Payment;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

/// <summary>Native 표시·보호 저장·서버 대역의 회귀이며 실제 결제나 Android 저장소 검증이 아닙니다.</summary>
public sealed class NativeShipperPaymentCloseoutTests
{
    [Fact]
    public async Task 승인응답은_입력원장의금융상태를변경하지않는다()
    {
        using var f = await Fixture.Create();
        var request = Request();
        var receipt = await f.Payments.ConfirmAsync(request, Plan(), null);
        Assert.Equal("결제완료", receipt.PaymentStatus);
        Assert.Equal("결제대기", request.결제상태);
        Assert.Equal("입금대기", request.정산상태);
        Assert.Equal("계좌이체", request.결제수단);
        Assert.Equal(5000, request.결제예정금액);
        Assert.False(await f.Payments.HasPendingAsync("request-a"));
    }

    [Theory]
    [InlineData("환불완료", "계좌이체", 5000)]
    [InlineData("결제완료", "계좌이체", 5000)]
    [InlineData("결제완료", "카드(FakePG)", 7000)]
    public async Task 과거영수증은_새조회금융상태금액수단을덮지않는다(string status, string method, int amount)
    {
        using var f = await Fixture.Create();
        await f.Payments.ConfirmAsync(Request(), Plan(), null);
        f.Stub.Current = Request();
        f.Stub.Current.결제상태 = status;
        f.Stub.Current.결제수단 = method;
        f.Stub.Current.결제예정금액 = amount;
        using var model = f.Model();
        await model.InitializeAsync("request-a");
        Assert.Equal(status, model.State.Request?.PaymentStatus);
        Assert.Equal(method, model.State.Request?.PaymentMethod);
        Assert.Equal(amount, model.State.Request?.ExpectedPaymentAmount);
        Assert.Equal(status, f.Stub.Current.결제상태);
        Assert.Null(model.Receipt);
    }

    [Fact]
    public async Task 같은응답영수증은_현재계정과서버에서_표시자료로만남는다()
    {
        using var f = await Fixture.Create();
        var receipt = await f.Payments.ConfirmAsync(Request(), Plan(), null);
        Assert.Equal(receipt, f.Payments.GetReceipt("request-a"));
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b", "access-b"));
        Assert.Null(f.Payments.GetReceipt("request-a"));
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        Assert.Null(f.Payments.GetReceipt("request-a"));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task 실패응답은_성공영수증으로대체하지않고_재전송을차단한다(int status)
    {
        using var f = await Fixture.Create(allowSample: true);
        f.Http.Reply = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.True(await f.Payments.HasPendingAsync("request-a"));
        Assert.Null(f.Payments.GetReceipt("request-a"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task 응답유실의보호기록은_새서비스와재로그인뒤에도_원요청을막는다()
    {
        using var f = await Fixture.Create();
        f.Http.Reply = (_, _) => throw new HttpRequestException("lost after server write");
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        await f.Auth.ClearAsync();
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var recreated = f.CreatePayments();
        Assert.True(await recreated.HasPendingAsync("request-a"));
        Assert.False(await recreated.HasPendingAsync("request-b"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => recreated.ConfirmAsync(Request(), Plan(), null));
        Assert.Equal(1, f.Http.Posts);
        Assert.Null(recreated.GetReceipt("request-a"));
    }

    [Fact]
    public async Task 미확정뒤화면재진입은_서버상태를그대로보이고_새결제를막는다()
    {
        using var f = await Fixture.Create();
        f.Http.Reply = (_, _) => throw new HttpRequestException();
        using (var first = f.Model())
        {
            await first.InitializeAsync("request-a");
            first.OpenPaymentWindow();
            Assert.True(first.PaymentWindowOpen);
            await first.CompletePaymentAsync(Plan());
            Assert.True(first.PaymentNeedsResultCheck);
            Assert.False(first.State.Request?.CanPay);
            Assert.Equal("결제대기", first.State.Request?.PaymentStatus);
            Assert.Contains("다시 전송하지 않습니다", first.State.StatusMessage);
        }
        f.Payments = f.CreatePayments();
        using var second = f.Model();
        await second.InitializeAsync("request-a");
        Assert.True(second.PaymentNeedsResultCheck);
        Assert.False(second.State.Request?.CanPay);
        second.OpenPaymentWindow();
        await second.CompletePaymentAsync(Plan());
        Assert.False(second.PaymentWindowOpen);
        Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task 서버에결제완료가있어도_이전요청성공으로보호기록을해제하지않는다()
    {
        using var f = await Fixture.Create();
        f.Http.Reply = (_, _) => throw new HttpRequestException();
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        f.Stub.Current.결제상태 = "결제완료";
        using var model = f.Model();
        await model.InitializeAsync("request-a");
        Assert.Equal("결제완료", model.State.Request?.PaymentStatus);
        Assert.True(model.PaymentNeedsResultCheck);
        Assert.Null(model.Receipt);
        Assert.True(await f.Payments.HasPendingAsync("request-a"));
        Assert.Equal(1, f.Http.Posts);
    }

    [Theory]
    [InlineData("wrong-request")]
    [InlineData("wrong-amount")]
    [InlineData("missing-number")]
    [InlineData("wrong-status")]
    public async Task 다른원장이나불완전한승인응답은_영수증과성공을만들지않는다(string variant)
    {
        using var f = await Fixture.Create();
        f.Http.Reply = (_, _) =>
        {
            var reply = Reply();
            if (variant == "wrong-request") reply.의뢰Id = "request-b";
            if (variant == "wrong-amount") reply.Amount = 1;
            if (variant == "missing-number") reply.결제Id = "";
            if (variant == "wrong-status") reply.결제상태 = "취소됨";
            return Task.FromResult(Ok(reply));
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.Null(f.Payments.GetReceipt("request-a"));
        Assert.True(await f.Payments.HasPendingAsync("request-a"));
    }

    [Fact]
    public async Task 보호저장미조립은_서버로전송하지않는다()
    {
        using var f = await Fixture.Create();
        var service = new FakeShipperPaymentService(f.Client, f.Auth, Options.Create(new ClientDataModeOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(Request(), Plan(), null));
        Assert.Equal(0, f.Http.Posts);
    }

    [Fact]
    public async Task 보호저장실패나무효저장은_POST전에중단한다()
    {
        using var f = await Fixture.Create();
        f.Store.NoOpSave = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.Equal(0, f.Http.Posts);
        using var model = f.Model();
        await model.InitializeAsync("request-a");
        Assert.False(model.State.Request?.CanPay);
        Assert.Contains("보호 기록", model.State.StatusMessage);
    }

    [Fact]
    public async Task 승인뒤보호정리실패는_미확정차단을유지한다()
    {
        using var f = await Fixture.Create();
        f.Store.NoOpRemove = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.True(await f.Payments.HasPendingAsync("request-a"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task 승인응답중계정이바뀌면_이전결과를새계정에저장하지않는다()
    {
        using var f = await Fixture.Create();
        f.Http.Reply = async (_, _) =>
        {
            await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b", "access-b"));
            return Ok(Reply());
        };
        await Assert.ThrowsAsync<OperationCanceledException>(() => f.Payments.ConfirmAsync(Request(), Plan(), null));
        Assert.Null(f.Payments.GetReceipt("request-a"));
        Assert.False(await f.Payments.HasPendingAsync("request-a"));
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        Assert.True(await f.Payments.HasPendingAsync("request-a"));
    }

    [Fact]
    public async Task 개발용무인증모의영수증도_원장입력을변경하지않는다()
    {
        using var f = await Fixture.Create(allowSample: true);
        await f.Auth.ClearAsync();
        var request = Request();
        await f.Payments.ConfirmAsync(request, Plan(), null);
        Assert.Equal("결제대기", request.결제상태);
        Assert.Equal("계좌이체", request.결제수단);
        Assert.Equal(0, f.Http.Posts);
        Assert.NotNull(f.Payments.GetReceipt("request-a"));
    }

    [Fact]
    public async Task 다른의뢰의결제계획은_창이열려있어도전송하지않는다()
    {
        using var f = await Fixture.Create();
        using var model = f.Model();
        await model.InitializeAsync("request-a");
        model.OpenPaymentWindow();
        await model.CompletePaymentAsync(Plan("request-b"));
        Assert.Equal(0, f.Http.Posts);
        Assert.False(model.PaymentWindowOpen);
    }

    private static ShipperRequestItem Request() => new()
    {
        의뢰Id = "request-a", 화물종류 = "상자", 의뢰상태 = "접수됨", 배차상태 = "상차완료", 운송상태 = "운송중",
        결제상태 = "결제대기", 정산상태 = "입금대기", 결제수단 = "계좌이체", 결제예정금액 = 5000, 정산시점 = "완료후결제"
    };
    private static PaymentRequestPlan Plan(string id = "request-a")
        => PaymentRequestPlanner.Plan(new(20, id, 990, 5000, "합성 운송", PaymentMethodCode.TossCard, SettlementModeCode.PayOnCompletion));
    private static 페이크결제승인응답 Reply() => new()
    {
        의뢰Id = "request-a", Amount = 5000, 결제Id = "synthetic-payment", OrderId = "synthetic-order",
        PaymentKey = "synthetic-key", 결제상태 = "결제완료", 승인일시Utc = DateTime.UtcNow
    };
    private static HttpResponseMessage Ok(페이크결제승인응답 reply)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(reply) };

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        public Recovery Store { get; } = new();
        public Handler Http { get; } = new();
        public HttpClient Client { get; }
        public IShipperOperationsService Operations { get; }
        public OperationsStub Stub { get; }
        public FakeShipperPaymentService Payments { get; set; } = null!;
        public WorkflowRecoveryContext Context { get; }
        private readonly IOptions<ClientDataModeOptions> options;
        private Fixture(bool allowSample)
        {
            Client = new(Http) { BaseAddress = new("https://synthetic.invalid/") };
            Context = new(() => new RoleWorkspaceIdentity(Auth.UserId, Auth.SessionRevision, Auth.IsLoggedIn), () => Client.BaseAddress);
            options = Options.Create(new ClientDataModeOptions { AllowSampleFallback = allowSample });
            Operations = DispatchProxy.Create<IShipperOperationsService, OperationsStub>();
            Stub = (OperationsStub)(object)Operations;
            Payments = CreatePayments();
        }
        public static async Task<Fixture> Create(bool allowSample = false)
        {
            var f = new Fixture(allowSample);
            await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
            return f;
        }
        public FakeShipperPaymentService CreatePayments() => new(Client, Auth, options, Store, Context);
        public ShipperRequestDetailPageViewModel Model()
            => new(Operations, new TransportRequestLedgerObserver(), Payments, options, Auth, Store, Context);
        public void Dispose() => Client.Dispose();
    }

    private sealed class Handler : HttpMessageHandler
    {
        public int Posts;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply = (_, _) => Task.FromResult(Ok(NativeShipperPaymentCloseoutTests.Reply()));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/payments/fake/confirm", request.RequestUri?.AbsolutePath);
            Posts++;
            return Reply(request, ct);
        }
    }

    public class OperationsStub : DispatchProxy
    {
        public ShipperRequestItem Current = Request();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IShipperOperationsService.GetRequestAsync)
                ? Task.FromResult<ShipperRequestItem?>(Current) : throw new NotSupportedException(method?.Name);
    }

    private sealed class Recovery : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public bool NoOpSave, NoOpRemove;
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => inner.LoadAsync(owner, scope, ct);
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => NoOpSave ? Task.CompletedTask : inner.SaveAsync(record, ct);
        public Task RemoveAsync(string owner, string scope, Guid id, CancellationToken ct) => NoOpRemove ? Task.CompletedTask : inner.RemoveAsync(owner, scope, id, ct);
    }
}
