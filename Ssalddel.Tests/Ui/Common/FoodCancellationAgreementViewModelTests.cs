using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodCancellationAgreementViewModelTests
{
    [Fact]
    public async Task 제안응답유실은_같은요청GET만확인하고_동의나취소를자동실행하지않는다()
    {
        var client = new Client { LoseResponse = true };
        using var model = new 음식주문취소합의ViewModel(client);
        model.SetOrder("food", 1); await model.LoadAsync();
        model.OrdererCostKrw = 0; model.RestaurantCostKrw = 700; model.CostBasisReference = "cost-proof";
        await model.ProposeAsync();
        Assert.True(model.HasPending); Assert.False(model.CanPropose); Assert.False(model.CanConsent);
        await model.ProposeAsync(); await model.LoadAsync();
        Assert.Equal(1, client.Proposals.Count);
        await model.CheckAsync();
        Assert.False(model.HasPending); Assert.Equal(1, client.ReceiptReads);
        Assert.Equal(client.Proposals[0].ClientRequestId, model.Current!.ConfirmedClientRequestId);
        Assert.False(model.Current.OrdererAgreed); Assert.False(model.Current.RestaurantAgreed);
        Assert.Equal(0, client.Consents); Assert.Equal(0, client.Applies);
    }

    [Fact]
    public async Task 다른요청의응답은성공으로받지않고_기존요청을계속조회한다()
    {
        var client = new Client { WrongReceipt = true };
        using var model = new 음식주문취소합의ViewModel(client);
        model.SetOrder("food", 1); await model.LoadAsync();
        model.OrdererCostKrw = model.RestaurantCostKrw = 0; model.CostBasisReference = "explicit-zero";
        await model.ProposeAsync();
        Assert.True(model.HasPending); Assert.Null(model.Current);
        client.WrongReceipt = false;
        await model.CheckAsync();
        Assert.False(model.HasPending); Assert.Single(client.Proposals);
        Assert.Equal(0, client.Applies);
    }

    [Fact]
    public async Task 양측동의가있어도별도확인없이실행하지않고_주문판본이바뀌면이전확인을취소한다()
    {
        var client = new Client { Listing = [new() { AgreementId = "agreement", OrderNo = "food", Revision = 3,
            FrozenOrderRevision = 1, CurrentOrderRevision = 1, CanConsent = true, CanApply = true,
            OrdererAgreed = true, RestaurantAgreed = true, StateCode = "ReadyToApply" }] };
        using var model = new 음식주문취소합의ViewModel(client);
        model.SetOrder("food", 1); await model.LoadAsync();
        await model.ApplyAsync(); Assert.Equal(0, client.Applies);
        model.ApplyConfirmed = true; Assert.True(model.CanApply);
        model.SetOrder("food", 2);
        Assert.False(model.ApplyConfirmed); Assert.False(model.CanConsent); Assert.False(model.CanApply);
        await model.ApplyAsync(); Assert.Equal(0, client.Applies);
    }

    [Fact]
    public async Task 비용미확정은0원으로대신하지않고_명시한0원만제안할수있다()
    {
        var client = new Client(); using var model = new 음식주문취소합의ViewModel(client);
        model.SetOrder("food", 1); await model.LoadAsync(); model.CostBasisReference = "explicit-zero";
        Assert.Null(model.OrdererCostKrw); Assert.Null(model.RestaurantCostKrw); Assert.False(model.CanPropose);
        await model.ProposeAsync(); Assert.Empty(client.Proposals);
        model.OrdererCostKrw = model.RestaurantCostKrw = 0;
        await model.ProposeAsync();
        var request = Assert.Single(client.Proposals);
        Assert.Equal(0m, request.OrdererCostKrw); Assert.Equal(0m, request.RestaurantCostKrw);
    }

    private sealed class Client : I음식주문취소합의Client
    {
        public bool LoseResponse { get; set; }
        public bool WrongReceipt { get; set; }
        public IReadOnlyList<음식주문취소합의응답> Listing { get; set; } = [];
        public List<음식주문취소합의제안요청> Proposals { get; } = [];
        public int Consents { get; private set; }
        public int Applies { get; private set; }
        public int ReceiptReads { get; private set; }
        private 음식주문취소합의응답? _receipt;
        public Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct) => Task.FromResult(Listing);
        public Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid requestId, CancellationToken ct)
        { ReceiptReads++; if (_receipt is not null) _receipt.ConfirmedClientRequestId = requestId; return Task.FromResult(_receipt); }
        public Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct)
        {
            Proposals.Add(request);
            _receipt = new() { AgreementId = request.ClientRequestId.ToString("N"), OrderNo = orderNo, Revision = 1,
                FrozenOrderRevision = request.ExpectedOrderRevision, CurrentOrderRevision = request.ExpectedOrderRevision,
                StateCode = "AwaitingConsent", CanConsent = true, OrdererCostKrw = request.OrdererCostKrw!.Value,
                RestaurantCostKrw = request.RestaurantCostKrw!.Value, CostBasisReference = request.CostBasisReference,
                ConfirmedClientRequestId = WrongReceipt ? Guid.NewGuid() : request.ClientRequestId };
            return LoseResponse ? Task.FromException<음식주문취소합의응답>(new HttpRequestException("response lost")) : Task.FromResult(_receipt);
        }
        public Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        { Consents++; throw new NotSupportedException(); }
        public Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        { Applies++; throw new NotSupportedException(); }
    }
}
