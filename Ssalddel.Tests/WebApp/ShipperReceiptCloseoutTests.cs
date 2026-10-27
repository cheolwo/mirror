using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using WebModel = Ssalddel.WebApp.ViewModels.ShipperRequestDetailPageViewModel;

namespace Ssalddel.Tests.WebApp;

/// <summary>제품 Web VM·인증·서비스를 통한 합성 HTTP 검증. 운영·금융·실기기 증거가 아닙니다.</summary>
public sealed class ShipperReceiptCloseoutTests
{
    [Theory]
    [InlineData("인수완료", "인수증대기", true)]
    [InlineData("인수완료", "후불승인완료", true)]
    [InlineData("인수완료", "정산완료", true)]
    [InlineData("하차완료", "인수증대기", false)]
    [InlineData("운송중", "후불승인완료", false)]
    [InlineData("인수완료", "결제대기", false)]
    public async Task 기존서버준비조건과명시검수만등록을허용한다(string transport, string settlement, bool allowed)
    {
        using var f = await Fixture.CreateAsync();
        f.Http.Current = Request(transport: transport, settlement: settlement);
        await f.StartAsync();
        Assert.False(f.Model.CanRegisterReceipt);
        f.Confirm(); Assert.Equal(allowed, f.Model.CanRegisterReceipt);
        if (!allowed) { await f.Model.RegisterReceiptAsync(); Assert.Equal(0, f.Http.Posts); }
    }

    [Fact]
    public async Task 등록은한번POST하고동일의뢰GET으로확인하며금융상태는보존한다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync(); f.Confirm();
        f.Model.SetReceiptMemo("합성 검수 메모"); Assert.False(f.Model.ReceiptAcceptanceConfirmed);
        f.Model.SetReceiptAcceptanceConfirmed(true);
        await f.Model.RegisterReceiptAsync();
        Assert.Equal(1, f.Http.Posts); Assert.Equal(2, f.Http.Reads);
        Assert.Equal("receipt-1", f.Model.State.Request?.ReceiptNumber);
        Assert.Equal("인수증대기", f.Model.State.Request?.SettlementStatus);
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt);
        Assert.Empty(f.Model.ReceiptNumber); Assert.Empty(f.Model.ReceiptMemo);
        Assert.Contains("입금·정산은 별도", f.Model.State.StatusMessage);
        Assert.Null(f.Store.Record);
        Assert.Equal("합성 검수 메모", f.Http.LastBody?.등록메모);
        Assert.Contains("/request%2Fa%2001/settlement/receipt", f.Http.PostPath);
    }

    [Fact]
    public async Task 응답유실뒤정본의등록사실로만완료하고재POST하지않는다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => { f.Http.RecordReceipt(); throw new IOException("synthetic lost response"); };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.Equal(1, f.Http.Posts);
        Assert.Equal("receipt-1", f.Model.State.Request?.ReceiptNumber);
    }

    [Fact]
    public async Task POST성공본문만으로완료하지않고미등록GET은계속보류한다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt);
        Assert.Contains("다시 전송하지", f.Model.State.StatusMessage);
        await f.Model.RegisterReceiptAsync(); await f.Model.ConfirmReceiptResultAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.Equal(1, f.Http.Posts);
        Assert.Null(f.Store.Record?.PayloadJson);
    }

    [Fact]
    public async Task 조회실패는미확정기록을보존하고결과확인으로복구한다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => { f.Http.FailRead = true; f.Http.RecordReceipt(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.NotNull(f.Store.Record);
        f.Http.FailRead = false; await f.Model.ConfirmReceiptResultAsync();
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.Null(f.Store.Record); Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task 서버거절과등록권한없음은성공으로표시하지않는다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Null(f.Model.State.Request?.ReceiptRegisteredAt);
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt);
        Assert.DoesNotContain("등록 확인", f.Model.State.StatusMessage);
    }

    [Fact]
    public async Task 중복눌림은첫POST하나만보내고취소뒤에는GET만한다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Http.Post = (_, _) => response.Task;
        using var cancel = new CancellationTokenSource(); f.Confirm();
        var running = f.Model.RegisterReceiptAsync(cancel.Token);
        await f.Model.RegisterReceiptAsync(); Assert.Equal(1, f.Http.Posts);
        cancel.Cancel(); response.SetResult(new(HttpStatusCode.OK)); await running;
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.State.IsBusy);
        f.Http.RecordReceipt(); await f.Model.ConfirmReceiptResultAsync();
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.Equal(1, f.Http.Posts);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("logout")]
    [InlineData("same-account-relogin")]
    [InlineData("leave")]
    [InlineData("dispose")]
    public async Task 늦은POST는다른대상과세션및이탈한화면을덮지않는다(string change)
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Http.Post = (_, _) => response.Task;
        f.Confirm(); var running = f.Model.RegisterReceiptAsync();
        if (change == "target") { f.Http.Current = Request("request-b"); await f.Model.InitializeReceiptAsync("request-b"); }
        else if (change == "logout") await f.ClearAuthAsync();
        else if (change == "same-account-relogin") { f.SetOwner("owner-a", "replacement"); await f.RestoreAsync(); }
        else if (change == "leave") f.Model.LeaveReceipt();
        else f.Model.Dispose();
        response.SetResult(new(HttpStatusCode.OK)); await running;
        Assert.True(f.Http.PostToken.IsCancellationRequested);
        Assert.Equal(1, f.Http.Posts); Assert.Empty(f.Model.ReceiptNumber); Assert.Empty(f.Model.ReceiptMemo);
        Assert.DoesNotContain("등록 확인", f.Model.State.StatusMessage);
        Assert.NotNull(f.Store.Record);
        if (change == "target") Assert.Equal("request-b", f.Model.State.Request?.RequestId);
    }

    [Fact]
    public async Task 늦은GET도새대상의정보를덮지않는다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Http.AfterPostRead = (_, _) => delayed.Task;
        f.Confirm(); var registering = f.Model.RegisterReceiptAsync();
        f.Http.AfterPostRead = null; f.Http.Current = Request("request-b");
        await f.Model.InitializeReceiptAsync("request-b");
        delayed.SetResult(Json(Request(receipt: "receipt-1"))); await registering;
        Assert.Equal("request-b", f.Model.State.Request?.RequestId);
        Assert.Null(f.Model.State.Request?.ReceiptRegisteredAt);
        Assert.NotNull(f.Store.Record);
    }

    [Theory]
    [InlineData("post")]
    [InlineData("get")]
    public async Task 취소된이전문맥의일반예외도새대상으로누출하지않는다(string stage)
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (stage == "post") f.Http.Post = (_, _) => delayed.Task;
        else f.Http.AfterPostRead = (_, _) => delayed.Task;
        f.Confirm(); var old = f.Model.RegisterReceiptAsync();
        f.Http.AfterPostRead = null; f.Http.Current = Request("request-b");
        await f.Model.InitializeReceiptAsync("request-b");
        delayed.SetException(new InvalidOperationException("synthetic late general error"));
        await old;
        Assert.Equal("request-b", f.Model.State.Request?.RequestId);
        Assert.False(f.Model.State.IsBusy); Assert.Null(f.Model.State.QueryError);
        Assert.DoesNotContain("synthetic", f.Model.State.StatusMessage);
        Assert.Equal(1, f.Http.Posts); Assert.NotNull(f.Store.Record);
    }

    [Fact]
    public async Task 같은scope재진입은미확정번호만복원하고조회로확인한다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => throw new TaskCanceledException("synthetic timeout");
        f.Confirm(); await f.Model.RegisterReceiptAsync(); Assert.True(f.Model.ReceiptNeedsResultCheck);
        f.Model.Dispose(); f.NewModel(); await f.StartAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.Empty(f.Model.ReceiptNumber); Assert.Empty(f.Model.ReceiptMemo);
        f.Http.RecordReceipt(); await f.Model.ConfirmReceiptResultAsync();
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.Equal(1, f.Http.Posts);
        Assert.Equal(ShipperRequestDetailMessageTone.Warning, f.Model.State.StatusTone);
        Assert.Contains("이전 요청과 일치 여부 미확인", f.Model.State.StatusMessage);
    }

    [Fact]
    public async Task 재진입뒤다른번호의인수증은이전요청성공으로표현하지않는다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Http.Post = (_, _) => throw new TaskCanceledException("synthetic timeout");
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        f.Model.Dispose(); f.NewModel(); f.Http.Current = Request(receipt: "someone-else-receipt");
        await f.StartAsync();
        Assert.False(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt);
        Assert.Equal("someone-else-receipt", f.Model.State.Request?.ReceiptNumber);
        Assert.Equal(ShipperRequestDetailMessageTone.Warning, f.Model.State.StatusTone);
        Assert.Contains("이전 요청과 일치 여부 미확인", f.Model.State.StatusMessage);
        Assert.DoesNotContain("인수증 등록 확인", f.Model.State.StatusMessage);
        Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task 보호저장실패와사라진기록은새등록의근거가아니다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Store.FailSave = true; f.Confirm(); await f.Model.RegisterReceiptAsync(); Assert.Equal(0, f.Http.Posts);
        f.Store.FailSave = false; await f.StartAsync(); f.Confirm();
        f.Http.Post = (_, _) => throw new TaskCanceledException("synthetic timeout");
        await f.Model.RegisterReceiptAsync(); Assert.True(f.Model.ReceiptNeedsResultCheck);
        f.Store.HideReads = true; await f.Model.ConfirmReceiptResultAsync();
        Assert.True(f.Model.ReceiptNeedsResultCheck); Assert.False(f.Model.CanRegisterReceipt); Assert.Equal(1, f.Http.Posts);
    }

    [Fact]
    public async Task POST인증복원중계정전환은새계정토큰으로이전명령을보내지않는다()
    {
        using var f = await Fixture.CreateAsync(); await f.StartAsync();
        f.Store.AfterSave = () => f.Js.BeforeRead = () => { f.Js.BeforeRead = null; f.SetOwner("owner-b"); };
        f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Equal(0, f.Http.Posts); Assert.NotNull(f.Store.Record);
        Assert.Null(f.Model.State.Request); Assert.Empty(f.Model.ReceiptNumber);
    }

    [Fact]
    public async Task 다른의뢰응답과기능비활성은등록을열지않는다()
    {
        using var f = await Fixture.CreateAsync(); f.Http.Current = Request("other-request");
        await f.StartAsync(); f.Confirm(); await f.Model.RegisterReceiptAsync();
        Assert.Null(f.Model.State.Request); Assert.Equal(0, f.Http.Posts);
        f.Http.Current = Request(); f.Metadata.Enabled = false; await f.StartAsync();
        Assert.False(f.Model.State.IsWorkflowEnabled); Assert.False(f.Model.CanRegisterReceipt);
    }

    [Theory]
    [InlineData("/shipper/request/REQ-1/proofs", true)]
    [InlineData("/shipper/request/REQ%2Fa%2001/proofs?returnPath=%2Fshipper", true)]
    [InlineData("/shipper/request/REQ-1", false)]
    [InlineData("/shipper/request/REQ-1/payment", false)]
    [InlineData("/shipper/request/REQ-1/timeline", false)]
    [InlineData("/shipper/request/REQ-1/proofs/extra", false)]
    [InlineData("/shipper/request/REQ-1/extra/proofs", false)]
    public void 증빙한건의정확suffix만저장경계이고일반상세와결제는조회다(string route, bool receipt)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(SsalddelPageAppCodes.IntegratedWeb, route, out var rule));
        Assert.Equal(receipt ? "shipper-request-receipt" : "shipper-request-detail", rule.PageKey);
        Assert.Equal(receipt ? PageInteractionBoundary.PlatformPersistence : PageInteractionBoundary.ReadOnly, rule.Boundary);
        Assert.True(rule.RequiresAuthentication); Assert.False(rule.HasExternalEffects);
    }

    private static 화주운송의뢰응답 Request(string id = "request/a 01", string transport = "인수완료",
        string settlement = "인수증대기", string? receipt = null) => new()
    {
        의뢰Id = id, 의뢰상태 = "생성됨", 운송상태 = transport, 정산상태 = settlement,
        결제상태 = "결제대기", 인수증번호 = receipt, 인수증등록일시 = receipt is null ? null : DateTime.UtcNow
    };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Fixture : IDisposable
    {
        public readonly StorageJs Js = new(); public readonly TestHttp Http = new(); public readonly Store Store = new();
        public readonly Context Context = new(); public MetadataProxy Metadata { get; }
        public WebModel Model { get; private set; } = null!;
        private readonly HttpClient client; private readonly object auth; private readonly object service;
        private readonly ICommunityProcurementClient metadata;
        private Fixture()
        {
            client = new(Http) { BaseAddress = new("https://synthetic.invalid/") };
            SetOwner("owner-a");
            var assembly = typeof(WebModel).Assembly;
            auth = Activator.CreateInstance(assembly.GetType("Ssalddel.WebApp.Services.WebAuthSessionService")!, client, Js, new ClientSessionGuard())!;
            service = Activator.CreateInstance(assembly.GetType("Ssalddel.WebApp.Services.화주결제정산Service")!, client, auth, new TransportRequestLedgerObserver())!;
            metadata = DispatchProxy.Create<ICommunityProcurementClient, MetadataProxy>(); Metadata = (MetadataProxy)(object)metadata;
            NewModel();
        }
        public static async Task<Fixture> CreateAsync() { var f = new Fixture(); await f.RestoreAsync(); return f; }
        public void NewModel() => Model = (WebModel)Activator.CreateInstance(typeof(WebModel), service, auth, metadata, Store, Context)!;
        public Task StartAsync() => Model.InitializeReceiptAsync("request/a 01");
        public void Confirm() { Model.SetReceiptNumber("receipt-1"); Model.SetReceiptAcceptanceConfirmed(true); }
        public void SetOwner(string owner, string? token = null)
        {
            Context.Owner = owner; Context.Revision++;
            Js.Json = JsonSerializer.Serialize(new ClientAuthTokenSnapshot(token ?? "token-" + owner,
                DateTime.UtcNow.AddHours(1), "refresh", DateTime.UtcNow.AddDays(1), owner, "합성 화주", ["화주"]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        public Task RestoreAsync() => (Task)auth.GetType().GetMethod("RestoreAsync")!.Invoke(auth, [CancellationToken.None])!;
        public Task ClearAuthAsync() { Context.Authenticated = false; return (Task)auth.GetType().GetMethod("ClearAsync")!.Invoke(auth, [CancellationToken.None])!; }
        public void Dispose() { Model.Dispose(); client.Dispose(); }
    }
    public class MetadataProxy : DispatchProxy
    {
        public bool Enabled = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "GetVersionWorkflowMetadataAsync"
            ? Task.FromResult(new VersionFeatureFlagsResponse { Flags = new Dictionary<string, bool> { ["DomesticTransportWorkflow"] = Enabled } })
            : throw new NotSupportedException();
    }
    private sealed class Context : IWorkflowRecoveryContext
    {
        public string Owner = "owner-a"; public long Revision; public bool Authenticated = true;
        public WorkflowRecoverySnapshot Capture() => Authenticated ? new(Owner, Revision, new string('a', 32)) : throw new InvalidOperationException();
        public bool IsCurrent(WorkflowRecoverySnapshot value) => Authenticated && value == Capture();
    }
    private sealed class Store : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public WorkflowRecoveryRecord? Record; public bool FailSave, HideReads; public Action? AfterSave;
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
            => HideReads ? Task.FromResult<WorkflowRecoveryRecord?>(null) : inner.LoadAsync(owner, scope, ct);
        public async Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        {
            if (FailSave) throw new IOException("synthetic storage");
            await inner.SaveAsync(record, ct); Record = record; AfterSave?.Invoke();
        }
        public async Task RemoveAsync(string owner, string scope, Guid requestId, CancellationToken ct)
        { await inner.RemoveAsync(owner, scope, requestId, ct); if (Record?.RequestId == requestId) Record = null; }
    }
    private sealed class StorageJs : IJSRuntime
    {
        public string? Json; public Action? BeforeRead;
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
        {
            ct.ThrowIfCancellationRequested();
            if (identifier == "localStorage.getItem") { BeforeRead?.Invoke(); return ValueTask.FromResult((T)(object?)Json!); }
            if (identifier == "localStorage.removeItem") { Json = null; return ValueTask.FromResult(default(T)!); }
            throw new NotSupportedException(identifier);
        }
    }
    private sealed class TestHttp : HttpMessageHandler
    {
        public 화주운송의뢰응답 Current = Request(); public int Posts, Reads; public bool FailRead;
        public CancellationToken PostToken; public string? PostPath; public 화주운송의뢰인수증등록요청? LastBody;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Post, AfterPostRead;
        public void RecordReceipt() => Current = Request(Current.의뢰Id, receipt: "receipt-1");
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                Posts++; PostToken = ct; PostPath = request.RequestUri!.AbsolutePath;
                LastBody = await request.Content!.ReadFromJsonAsync<화주운송의뢰인수증등록요청>(ct);
                if (Post is not null) return await Post(request, ct);
                RecordReceipt(); return new(HttpStatusCode.OK);
            }
            Assert.Equal(HttpMethod.Get, request.Method); Reads++;
            if (FailRead) throw new IOException("synthetic GET failure");
            if (Posts > 0 && AfterPostRead is not null) return await AfterPostRead(request, ct);
            return Json(Current);
        }
    }
}
