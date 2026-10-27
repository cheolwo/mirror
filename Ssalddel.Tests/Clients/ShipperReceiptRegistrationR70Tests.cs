using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Payments;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

/// <summary>로그인·서버 대역의 입력/결과 수명 시험이며 실제 운송·입금 증거가 아닙니다.</summary>
public sealed class ShipperReceiptRegistrationR70Tests
{
    [Theory]
    [InlineData("운송중", "인수증대기", false)]
    [InlineData("하차완료", "인수증대기", false)]
    [InlineData("인수완료", "결제대기", false)]
    [InlineData("인수완료", "후불승인완료", true)]
    [InlineData("인수완료", "입금대기", true)]
    [InlineData("인수완료", "정산완료", true)]
    public async Task 등록버튼은_확인된운송과허용정산상태및명시확인이필요하다(string transport, string settlement, bool eligible)
    {
        using var f = new Fixture();
        f.Operations.Current.운송상태 = transport;
        f.Operations.Current.정산상태 = settlement;
        await f.StartAsync();
        Assert.Equal(eligible, f.Model.CanPrepareReceipt);
        f.Model.SetReceiptNumber("receipt-a");
        Assert.False(f.Model.CanRegisterReceipt);
        f.Model.SetReceiptAcceptanceConfirmed(true);
        Assert.Equal(eligible, f.Model.CanRegisterReceipt);
        if (!eligible) { await f.Model.RegisterReceiptAsync(); Assert.Empty(f.Operations.Posts); }
    }

    [Fact]
    public async Task 명시등록후_같은의뢰정본의번호시각을확인하고_금융상태를보존한다()
    {
        using var f = new Fixture(); await f.StartAsync();
        f.Confirm(" receipt-a ", " 검수 완료 ");
        await f.Model.RegisterReceiptAsync();
        var post = Assert.Single(f.Operations.Posts);
        Assert.Equal("request-a", post.Id); Assert.Equal("receipt-a", post.Payload.인수증번호);
        Assert.Equal("검수 완료", post.Payload.등록메모);
        Assert.Equal(2, f.Operations.Reads);
        Assert.Equal("receipt-a", f.Model.State.Request?.ReceiptNumber);
        Assert.NotNull(f.Model.State.Request?.ReceiptRegisteredAt);
        Assert.Equal("입금대기", f.Model.State.Request?.SettlementStatus);
        Assert.Contains("입금·정산은 별도 확인", f.Model.State.StatusMessage);
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt);
    }

    [Fact]
    public async Task 응답유실이어도_이미등록된정본이면_재전송없이성공을확인한다()
    {
        using var f = new Fixture(); await f.StartAsync();
        f.Operations.Post = (_, request, _) =>
        { f.Operations.Record(request); throw new IOException("response lost"); };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Single(f.Operations.Posts); Assert.Equal(2, f.Operations.Reads);
        Assert.Contains("인수증 등록 완료", f.Model.State.StatusMessage);
        Assert.False(f.Model.ReceiptNeedsResultCheck);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("wrong-id")]
    [InlineData("failed")]
    public async Task 결과조회실패는_입력을잠그고_결과확인전재전송하지않는다(string fault)
    {
        using var f = new Fixture(); await f.StartAsync();
        f.Operations.Read = (_, _) => fault switch
        {
            "null" => Task.FromResult<ShipperRequestItem?>(null),
            "wrong-id" => Task.FromResult<ShipperRequestItem?>(Request("other-request")),
            _ => throw new IOException("read unavailable")
        };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.True(f.Model.ReceiptInputLocked);
        Assert.Equal("request-a", f.Model.State.Request?.RequestId);
        f.Model.SetReceiptNumber("replacement");
        Assert.Equal("receipt-a", f.Model.ReceiptNumber);
        await f.Model.RegisterReceiptAsync(); Assert.Single(f.Operations.Posts);
        f.Operations.Read = (_, _) => Task.FromResult<ShipperRequestItem?>(f.Operations.Current);
        await f.Model.ConfirmReceiptResultAsync();
        Assert.Single(f.Operations.Posts); Assert.False(f.Model.ReceiptNeedsResultCheck);
        Assert.Equal("receipt-a", f.Model.State.Request?.ReceiptNumber);
    }

    [Fact]
    public async Task 서버거부후미등록을확인해도_자동재전송하지않고_내용재확인을요구한다()
    {
        using var f = new Fixture(); await f.StartAsync();
        f.Operations.Post = (_, _, _) => throw new InvalidOperationException("CargoCompletionHandoffPending");
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Single(f.Operations.Posts); Assert.False(f.Model.ReceiptNeedsResultCheck);
        Assert.False(f.Model.ReceiptAcceptanceConfirmed); Assert.False(f.Model.CanRegisterReceipt);
        Assert.Contains("CargoCompletionHandoffPending", f.Model.State.StatusMessage);
        await f.Model.RegisterReceiptAsync(); Assert.Single(f.Operations.Posts);
    }

    [Fact]
    public async Task 이미다른번호가등록됐으면_기존원장을유지하고덮어쓰지않는다()
    {
        using var f = new Fixture(); await f.StartAsync();
        f.Operations.Post = (_, _, _) =>
        { f.Operations.Record(new() { 인수증번호 = "other-receipt" }); throw new InvalidOperationException("conflict"); };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Equal("other-receipt", f.Model.State.Request?.ReceiptNumber);
        Assert.Contains("다른 인수증 기록", f.Model.State.StatusMessage);
        Assert.False(f.Model.CanRegisterReceipt); Assert.False(f.Model.ReceiptNeedsResultCheck);
    }

    [Fact]
    public async Task 빠른중복클릭은_한번만등록하고_진행중입력을변경하지않는다()
    {
        using var f = new Fixture(); await f.StartAsync();
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Operations.Post = (_, _, _) => response.Task;
        f.Confirm(); var first = f.Model.RegisterReceiptAsync();
        await f.Model.RegisterReceiptAsync();
        f.Model.SetReceiptNumber("replacement"); Assert.Equal("receipt-a", f.Model.ReceiptNumber);
        Assert.Single(f.Operations.Posts); Assert.True(f.Model.ReceiptSubmitting);
        response.SetResult(); await first;
        Assert.False(f.Model.ReceiptSubmitting); Assert.False(f.Model.State.IsBusy);
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("logout")]
    [InlineData("relogin")]
    [InlineData("dispose")]
    public async Task 늦은등록응답은_다른의뢰계정또는이탈한화면에적용하지않는다(string change)
    {
        using var f = new Fixture(); await f.StartAsync();
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken sentToken = default;
        f.Operations.Post = (_, _, token) => { sentToken = token; return response.Task; };
        f.Confirm(); var running = f.Model.RegisterReceiptAsync();
        if (change == "selection") await f.Model.LoadAsync("request-b");
        else if (change == "dispose") f.Model.Dispose();
        else { await f.Auth.ClearAsync(); if (change == "relogin") await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(token: "new-login")); }
        Assert.True(sentToken.IsCancellationRequested);
        response.SetResult(); await running;
        Assert.Empty(f.Model.ReceiptNumber); Assert.Empty(f.Model.ReceiptMemo);
        Assert.False(f.Model.ReceiptNeedsResultCheck);
        if (change == "selection") Assert.Equal("request-b", f.Model.State.Request?.RequestId);
        else Assert.Null(f.Model.State.Request);
        Assert.DoesNotContain("인수증 등록을 확인", f.Model.State.StatusMessage);
    }

    [Fact]
    public async Task 등록중외부취소는_미확인상태를유지하고_명시조회로복구한다()
    {
        using var f = new Fixture(); await f.StartAsync(); using var cancel = new CancellationTokenSource();
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Operations.Post = (_, request, _) => { f.Operations.Record(request); return response.Task; };
        f.Confirm(); var running = f.Model.RegisterReceiptAsync(cancel.Token);
        cancel.Cancel(); response.SetResult(); await running;
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.State.IsBusy);
        await f.Model.ConfirmReceiptResultAsync(); Assert.Single(f.Operations.Posts);
        Assert.Equal("receipt-a", f.Model.State.Request?.ReceiptNumber);
    }

    [Fact]
    public async Task 인수증조회는_sample조회가아닌전용서버조회만사용한다()
    {
        using var f = new Fixture(); await f.StartAsync();
        Assert.Equal(0, f.Operations.OrdinaryReads); Assert.Equal(1, f.Operations.Reads);
        f.Model.SetReceiptNumber("receipt-a"); f.Model.SetReceiptAcceptanceConfirmed(true);
        f.Model.SetReceiptMemo("changed"); Assert.False(f.Model.ReceiptAcceptanceConfirmed);
        Assert.False(f.Model.CanRegisterReceipt);
    }

    [Fact]
    public async Task 첫조회가다른의뢰이면_표시와등록을차단한다()
    {
        using var f = new Fixture();
        f.Operations.Read = (_, _) => Task.FromResult<ShipperRequestItem?>(Request("other-request"));
        await f.StartAsync();
        Assert.Null(f.Model.State.Request); Assert.False(f.Model.CanPrepareReceipt);
        Assert.Contains("선택한 의뢰와 다릅니다", f.Model.State.StatusMessage);
        f.Confirm(); await f.Model.RegisterReceiptAsync(); Assert.Empty(f.Operations.Posts);
    }

    [Fact]
    public async Task 첫조회중로그아웃하면_늦은의뢰를표시하거나등록하지않는다()
    {
        using var f = new Fixture();
        var read = new TaskCompletionSource<ShipperRequestItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Operations.Read = (_, _) => read.Task;
        var starting = f.StartAsync();
        await f.Auth.ClearAsync(); read.SetResult(Request()); await starting;
        Assert.Null(f.Model.State.Request); Assert.True(f.Model.State.RequiresLogin);
        f.Confirm(); await f.Model.RegisterReceiptAsync(); Assert.Empty(f.Operations.Posts);
    }

    [Fact]
    public async Task 같은의뢰의로컬FakePG기록이있어도_인수증화면은서버금융상태를보존한다()
    {
        using var f = new Fixture();
        var draft = new PaymentRequestDraft(1, "request-a", 1, 5000, "운송 의뢰", PaymentMethodCode.TossCard, SettlementModeCode.Prepaid);
        await f.Payments.ConfirmAsync(Request(), PaymentRequestPlanner.Plan(draft), null);
        Assert.NotNull(f.Payments.GetReceipt("request-a"));
        f.Operations.Current.결제상태 = "입금대기";
        f.Operations.Current.결제수단 = "계좌이체";
        await f.StartAsync();
        Assert.Equal("입금대기", f.Model.State.Request?.PaymentStatus);
        Assert.Equal("계좌이체", f.Model.State.Request?.PaymentMethod);
        Assert.Null(f.Model.Receipt);
    }

    private static ShipperRequestItem Request(string id = "request-a") => new()
    { 의뢰Id = id, 화물종류 = "상자", 운송상태 = "인수완료", 정산상태 = "입금대기", 배차상태 = "하차완료" };

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        public Operations Operations { get; } = new();
        public ShipperRequestDetailPageViewModel Model { get; }
        public FakeShipperPaymentService Payments { get; }
        private readonly HttpClient _http = new() { BaseAddress = new("https://test.invalid/") };
        public Fixture()
        {
            var options = Options.Create(new ClientDataModeOptions { AllowSampleFallback = true });
            Payments = new FakeShipperPaymentService(_http, Auth, options);
            Model = new(Operations, new TransportRequestLedgerObserver(), Payments, options, Auth);
        }
        public async Task StartAsync() { await Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot()); await Model.InitializeReceiptAsync("request-a"); }
        public void Confirm(string number = "receipt-a", string memo = "검수 완료")
        { Model.SetReceiptNumber(number); Model.SetReceiptMemo(memo); Model.SetReceiptAcceptanceConfirmed(true); }
        public void Dispose() { Model.Dispose(); _http.Dispose(); }
    }

    private sealed class Operations : IShipperOperationsService
    {
        public ShipperRequestItem Current { get; private set; } = Request();
        public Func<string, CancellationToken, Task<ShipperRequestItem?>>? Read;
        public Func<string, 화주운송의뢰인수증등록요청, CancellationToken, Task>? Post;
        public List<(string Id, 화주운송의뢰인수증등록요청 Payload)> Posts { get; } = [];
        public int Reads; public int OrdinaryReads;
        public Task<ShipperRequestItem?> GetRequestForReceiptAsync(string id, CancellationToken ct = default)
        { Reads++; return Read?.Invoke(id, ct) ?? Task.FromResult<ShipperRequestItem?>(id == Current.의뢰Id ? Current : Request(id)); }
        public Task<ShipperRequestItem?> GetRequestAsync(string id, CancellationToken ct = default)
        { OrdinaryReads++; throw new InvalidOperationException("sample read must not be used"); }
        public Task RegisterReceiptAsync(string id, 화주운송의뢰인수증등록요청 payload, CancellationToken ct = default)
        { Posts.Add((id, payload)); if (Post is not null) return Post(id, payload, ct); Record(payload); return Task.CompletedTask; }
        public void Record(화주운송의뢰인수증등록요청 payload)
        { Current = Request(Current.의뢰Id); Current.인수증번호 = payload.인수증번호; Current.인수증등록일시 = DateTime.UtcNow; }
        public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<decimal> EstimateFareAsync(string v, decimal d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteRequestAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
