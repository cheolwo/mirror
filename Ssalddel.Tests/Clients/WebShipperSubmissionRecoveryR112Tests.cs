using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;
using WebModel = Ssalddel.WebApp.ViewModels.ShipperRequestAuthoringPageViewModel;

namespace Ssalddel.Tests.Clients;

public sealed class WebShipperSubmissionRecoveryR112Tests
{
    [Theory]
    [InlineData("ShipperRequestPage.razor", "ShipperRequestAuthoringScreen")]
    [InlineData("ShipperRequestReviewPage.razor", "ShipperRequestReviewScreen")]
    [InlineData("ShipperRequestCargoPage.razor", "ShipperRequestCargoScreen")]
    [InlineData("ShipperRequestTransportPage.razor", "ShipperRequestTransportScreen")]
    [InlineData("ShipperRequestProcedurePage.razor", "ShipperRequestProcedureScreen")]
    public void 모든작성Route는_미확정또는전송중에_편집Screen대신복구만렌더한다(string file, string screen)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "Ssalddel.WebApp", "Pages", file));
        var guard = source.IndexOf("@if (!PageModel.HasSubmission && !PageModel.IsBusy)", StringComparison.Ordinal);
        Assert.True(guard >= 0);
        Assert.True(source.IndexOf("<" + screen, StringComparison.Ordinal) > guard);
        Assert.Contains("<WebShipperSubmissionRecoveryPanel", source);
        Assert.DoesNotContain("<fieldset", source);
    }

    [Theory]
    [InlineData("Ssalddel.WebApp/Program.cs")]
    [InlineData("eng/web-role-app/Program.cs")]
    public void 통합웹과화주WASM호스트가_동일등록서비스인스턴스를새포트에연결한다(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
        Assert.Contains("AddScoped<화주운송의뢰등록Service>()", source);
        Assert.Contains("AddScoped<I화주운송의뢰등록Service>(provider => provider.GetRequiredService<화주운송의뢰등록Service>())", source);
    }

    [Fact]
    public async Task 응답유실후_원장조회로정본을찾고_새POST를보내지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.LoseResponse = true;
        using var model = await f.ModelAsync();
        await model.SubmitAsync();
        Assert.Equal(1, f.Http.PostCount);
        Assert.Equal(1, f.Http.GetCount);
        Assert.Equal("등록 정본 확인", model.SubmissionState);
        Assert.True(model.CanStartNewRequest);
        Assert.Contains(f.Http.Existing!.의뢰Id, f.Navigation.LastPath);
        await model.SubmitAsync();
        Assert.Equal(1, f.Http.PostCount);
    }

    [Fact]
    public async Task 조회404는_미접수확정이아니며_초안변경초기화재접수가원문을덮지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.LoseResponse = f.Http.NotFound = true;
        using var model = await f.ModelAsync();
        model.State.상차상세주소 = "원래 합성 상차장";
        await model.SubmitAsync();
        var originalDraft = f.Js.StoredDraft();
        var number = model.SubmissionNumber;
        Assert.True(model.SubmissionNotFound);
        Assert.False(model.CanStartNewRequest);
        model.State.상차상세주소 = "바뀐 입력";
        await model.AutoSaveAsync(); await model.SaveAsync(); await model.ResetAsync(); await model.SubmitAsync();
        Assert.Equal(1, f.Http.PostCount);
        Assert.Equal(number, model.SubmissionNumber);
        Assert.Equal(originalDraft, f.Js.StoredDraft());
        Assert.Equal("원래 합성 상차장", f.Http.Posts.Single().픽업!.주소.상세주소);
    }

    [Fact]
    public async Task 브라우저재진입은_동일번호와동일초안을복원하고_GET만수행한다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.LoseResponse = f.Http.NotFound = true;
        string? number;
        using (var first = await f.ModelAsync())
        {
            first.State.화물수량 = 7;
            first.State.상차연락처전화번호 = "synthetic-contact";
            await first.SubmitAsync();
            number = first.SubmissionNumber;
        }
        using var restored = await f.ModelAsync(new());
        Assert.Equal(number, restored.SubmissionNumber);
        Assert.Equal(7, restored.State.화물수량);
        Assert.Equal("synthetic-contact", restored.State.상차연락처전화번호);
        Assert.Equal(1, f.Http.PostCount);
        f.Http.NotFound = false;
        await restored.RecoverSubmissionAsync();
        Assert.True(restored.CanStartNewRequest);
        Assert.Equal(1, f.Http.PostCount);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    public async Task 명시거절과_정본조회404는서로다르며_새번호를해제하지않는다(int status)
    {
        using var f = await Fixture.CreateAsync();
        f.Http.RejectStatus = status; f.Http.NotFound = true;
        using var model = await f.ModelAsync();
        await model.SubmitAsync();
        Assert.Equal(status, model.SubmissionRejectedStatus);
        Assert.True(model.SubmissionNotFound);
        Assert.Contains("거절", model.SubmissionState);
        Assert.False(model.CanStartNewRequest);
        await model.ResetAsync(); await model.SubmitAsync();
        Assert.Equal(1, f.Http.PostCount);
    }

    [Fact]
    public async Task 조회실패는_404로위장하지않으며_원래거절응답도지우지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.RejectStatus = 400; f.Http.GetStatus = 503;
        using var model = await f.ModelAsync();
        await model.SubmitAsync();
        Assert.Equal("등록 결과 조회 실패", model.SubmissionState);
        Assert.False(model.SubmissionNotFound);
        Assert.Equal(400, model.SubmissionRejectedStatus);
        Assert.True(model.HasSubmission);
        Assert.Equal(1, f.Http.PostCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 브라우저잠금이나저장실패는_POST보다먼저중단한다(bool failLock)
    {
        using var f = await Fixture.CreateAsync();
        using var model = await f.ModelAsync();
        f.Js.FailLock = failLock; f.Js.FailWrite = !failLock;
        await model.SubmitAsync();
        Assert.Equal(0, f.Http.PostCount);
        Assert.Equal("접수 보관 확인 필요", model.SubmissionState);
        Assert.True(model.HasSubmission);
    }

    [Fact]
    public async Task 같은브라우저의두작성화면은_한번호를공유하고_둘째는조회만한다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.DelayPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = await f.ModelAsync();
        using var second = await f.ModelAsync();
        var sending = first.SubmitAsync();
        await f.Http.PostStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await second.SubmitAsync();
        Assert.Equal(first.SubmissionNumber, second.SubmissionNumber);
        Assert.Equal(1, f.Http.PostCount);
        f.Http.DelayPost.SetResult();
        await sending;
    }

    [Fact]
    public async Task 계정전환은_이전초안번호와늦은성공응답을새화면에노출하지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.DelayPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = await f.ModelAsync();
        model.State.상차상세주소 = "이전 계정 합성 주소";
        var sending = model.SubmitAsync();
        await f.Http.PostStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.SetOwner("owner-b");
        await f.Auth.RestoreAsync();
        f.Http.DelayPost.SetResult();
        await sending;
        Assert.Null(model.SubmissionNumber);
        Assert.Null(model.CreatedRequestId);
        Assert.Empty(model.State.상차상세주소 ?? string.Empty);
        Assert.Null(f.Navigation.LastPath);
        await model.InitializeAsync(new());
        Assert.False(model.HasSubmission);
        Assert.Empty(model.State.상차도로명주소);
        f.SetOwner("owner-a"); await f.Auth.RestoreAsync();
        await model.InitializeAsync(new());
        Assert.True(model.CanStartNewRequest);
        Assert.Equal("이전 계정 합성 주소", model.State.상차상세주소);
    }

    [Fact]
    public async Task 같은계정의토큰전환도_이전전송의늦은응답을현재화면에적용하지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.DelayPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = await f.ModelAsync();
        var sending = model.SubmitAsync();
        await f.Http.PostStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.SetOwner("owner-a", "renewed-token");
        await f.Auth.RestoreAsync();
        f.Http.DelayPost.SetResult();
        await sending;
        Assert.Null(model.CreatedRequestId);
        Assert.Null(model.SubmissionNumber);
        Assert.Null(f.Navigation.LastPath);
        Assert.Equal(1, f.Http.PostCount);
        await model.InitializeAsync(new());
        Assert.True(model.CanStartNewRequest);
        Assert.Equal(1, f.Http.PostCount);
        Assert.Null(f.Navigation.LastPath);
    }

    [Fact]
    public async Task 정본확인후에도_명시적으로새의뢰작성을눌러야번호와초안을초기화한다()
    {
        using var f = await Fixture.CreateAsync();
        using var model = await f.ModelAsync();
        await model.SubmitAsync();
        Assert.True(model.HasSubmission);
        await model.ResetAsync();
        Assert.False(model.HasSubmission);
        Assert.Null(model.SubmissionNumber);
        Assert.Empty(model.State.화물종류);
        Assert.Null(await f.Registration.접수기록조회Async("owner-a"));
    }

    [Fact]
    public async Task 만료된초안은_개인정보를복원하지않고_접수번호로만계속조회한다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.LoseResponse = f.Http.NotFound = true;
        using (var first = await f.ModelAsync()) await first.SubmitAsync();
        var key = f.Js.DraftKey;
        var draft = JsonSerializer.Deserialize<운송모델작성Draft>(f.Js.Data[key], Json)!;
        draft.작성일시 = DateTime.Now.AddDays(-4);
        f.Js.Data[key] = JsonSerializer.Serialize(draft, Json);
        using var restored = await f.ModelAsync(new());
        Assert.True(restored.HasSubmission);
        Assert.Empty(restored.State.상차도로명주소);
        Assert.False(f.Js.Data.ContainsKey(key));
        Assert.Equal(1, f.Http.PostCount);
    }

    [Fact]
    public async Task 다른의뢰나계정의조회응답은_정본확인으로승격하지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Http.LoseResponse = true; f.Http.ForeignGet = true;
        using var model = await f.ModelAsync();
        await model.SubmitAsync();
        Assert.False(model.CanStartNewRequest);
        Assert.Null(model.CreatedRequestId);
        Assert.Equal("등록 결과 조회 실패", model.SubmissionState);
        Assert.Null(f.Navigation.LastPath);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel.WebApp", "Ssalddel.WebApp.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("저장소를 찾을 수 없습니다.");
    }

    private sealed class Fixture : IDisposable
    {
        public StorageJs Js { get; } = new();
        public HttpStub Http { get; } = new();
        public Nav Navigation { get; } = new();
        public WebAuthSessionService Auth { get; }
        public 화주운송의뢰등록Service Registration { get; }
        private readonly HttpClient http;
        private Fixture()
        {
            http = new(Http) { BaseAddress = new("https://test.invalid/") };
            SetOwner("owner-a");
            Auth = new(http, Js, new ClientSessionGuard());
            Registration = new(http, Auth, new TransportRequestLedgerObserver(), Js);
        }
        public void SetOwner(string owner, string? token = null) => Js.Data["ssalddel.web.auth.v1"] = JsonSerializer.Serialize(new ClientAuthTokenSnapshot(
            token ?? "token-" + owner, DateTime.UtcNow.AddHours(2), "refresh-" + owner, DateTime.UtcNow.AddDays(1), owner, "합성 화주", ["화주"]), Json);
        public static async Task<Fixture> CreateAsync() { var f = new Fixture(); await f.Auth.RestoreAsync(); return f; }
        public async Task<WebModel> ModelAsync(운송의뢰작성ViewModel? state = null)
        {
            var model = new WebModel(state ?? ShipperRequestContactWindowTests.CompleteState(), new(Js), new(), Registration, Auth,
                DispatchProxy.Create<ICommunityPostClient, MetadataStub>(),
                DispatchProxy.Create<ICommunityProcurementClient, MetadataStub>(), Navigation);
            await model.InitializeAsync(new());
            return model;
        }
        public void Dispose() => http.Dispose();
    }

    public class MetadataStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetVersionWorkflowMetadataAsync"
                ? Task.FromResult(new VersionFeatureFlagsResponse { Flags = new Dictionary<string, bool> { ["DomesticTransportWorkflow"] = true } })
                : throw new NotSupportedException();
    }

    private sealed class HttpStub : HttpMessageHandler
    {
        public List<화주운송의뢰생성요청> Posts { get; } = [];
        public int PostCount => Posts.Count;
        public int GetCount;
        public bool LoseResponse, NotFound, ForeignGet;
        public int? RejectStatus, GetStatus;
        public 화주운송의뢰응답? Existing;
        public TaskCompletionSource? DelayPost;
        public TaskCompletionSource PostStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadFromJsonAsync<화주운송의뢰생성요청>(ct);
                Posts.Add(body!); PostStarted.TrySetResult();
                if (DelayPost is not null) await DelayPost.Task;
                if (RejectStatus is { } rejected) return new((HttpStatusCode)rejected) { Content = new StringContent("synthetic-rejection") };
                var id = Guid.ParseExact(body!.클라이언트요청Id![12..], "N");
                var record = new 화주웹접수기록(body.화주Id!, id, DateTime.UtcNow);
                Existing = new() { 의뢰Id = record.ExpectedRequestId, 주문자UserId = record.Owner, 화주Id = record.Owner, 의뢰상태 = "생성됨", 결제상태 = "결제대기" };
                if (LoseResponse) throw new IOException("synthetic lost response");
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(Existing) };
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("/by-client-request/shipper-app-", request.RequestUri!.AbsolutePath);
            GetCount++;
            if (GetStatus is { } failed) return new((HttpStatusCode)failed);
            if (NotFound || Existing is null) return new(HttpStatusCode.NotFound);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(ForeignGet
                ? new 화주운송의뢰응답 { 의뢰Id = Guid.NewGuid().ToString(), 주문자UserId = "foreign" } : Existing) };
        }
    }

    private sealed class StorageJs : IJSRuntime, IJSObjectReference
    {
        public Dictionary<string, string> Data { get; } = [];
        public bool FailLock, FailWrite;
        public string DraftKey => Data.Keys.Single(k => k.StartsWith("ssalddel.web.shipper-request-draft.v1.", StringComparison.Ordinal));
        public string StoredDraft() => Data[DraftKey];
        private readonly SemaphoreSlim gate = new(1, 1);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, default, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            object? result = null;
            if (identifier == "import") return (TValue)(object)this;
            var key = (string)args![0]!;
            if (identifier == "localStorage.getItem") result = Data.GetValueOrDefault(key);
            else if (identifier == "localStorage.removeItem") Data.Remove(key);
            else
            {
                if (FailLock) throw new JSException("synthetic lock unavailable");
                await gate.WaitAsync(ct);
                try
                {
                    if (FailWrite) throw new JSException("synthetic storage full");
                    if (identifier == "begin")
                    {
                        var exists = Data.TryGetValue(key, out var old);
                        if (!exists) { Data[(string)args[2]!] = (string)args[3]!; Data[key] = (string)args[1]!; }
                        return JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(new { Json = exists ? old : Data[key], Created = !exists }, Json), Json)!;
                    }
                    if (identifier == "update")
                    {
                        var old = JsonSerializer.Deserialize<화주웹접수기록>(Data[key], Json)!;
                        var next = JsonSerializer.Deserialize<화주웹접수기록>((string)args[2]!, Json)!;
                        Assert.Equal(old.RequestId.ToString(), args[1]);
                        result = Data[key] = JsonSerializer.Serialize(next with { ConfirmedRequestId = old.ConfirmedRequestId ?? next.ConfirmedRequestId, CreatedAtUtc = old.CreatedAtUtc }, Json);
                    }
                    else if (identifier == "removeConfirmed")
                    {
                        if (args.Length > 3) Data.Remove((string)args[3]!);
                        Data.Remove(key);
                    }
                    else if (identifier == "saveDraft")
                    {
                        if (Data.ContainsKey(key)) throw new JSException("pending exists");
                        Data[(string)args[1]!] = (string)args[2]!;
                    }
                    else if (identifier != "update") throw new NotSupportedException(identifier);
                }
                finally { gate.Release(); }
            }
            return result is null ? default! : (TValue)result;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public sealed class Nav : NavigationManager
    {
        public string? LastPath;
        public Nav() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastPath = uri;
    }
}
