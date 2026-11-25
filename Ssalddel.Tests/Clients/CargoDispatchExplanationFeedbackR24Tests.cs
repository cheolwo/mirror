using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

/// <summary>합성 client/보호 저장 계약/정적 Razor 회귀다. 실제 HTTP·브라우저·기기 증거와 구분한다.</summary>
public sealed partial class CargoDispatchExplanationFeedbackR24Tests
{
    [Fact]
    public async Task 과거미기록과조회실패는구분하고_의견종류는사용자가선택한다()
    {
        var f = new Fixture(); using var model = f.Model();
        f.Client.Read = (_, _) => Task.FromResult(new CargoDispatchExplanationResponse());
        await model.LoadAsync("request-a");
        Assert.Equal("unrecorded", model.Explanation?.Status); Assert.Null(model.Error); Assert.False(model.CanSubmit);
        f.Client.Read = (_, _) => throw new HttpRequestException("private original error");
        await model.LoadAsync("request-a");
        Assert.Null(model.Explanation); Assert.Contains("확인하지 못했습니다", model.Error); Assert.DoesNotContain("private", model.Error);
        f.Client.Read = (id, _) => Task.FromResult(Recorded(id));
        await model.LoadAsync("request-a");
        Assert.Equal(string.Empty, model.CategoryCode); Assert.False(model.CanSubmit);
        model.CategoryCode = "helpful"; Assert.True(model.CanSubmit);
        model.Comment = new string('x', 501); Assert.False(model.CanSubmit);
        Assert.Empty(f.Client.Posts);
    }

    [Fact]
    public async Task 접수는원배차와구분하고_저장응답과같은번호만완료로표시한다()
    {
        var f = new Fixture(); using var model = f.Model(); await model.LoadAsync("request-a");
        model.CategoryCode = "time-or-route"; model.Comment = "상차 시간 확인 요청";
        await model.SubmitAsync();
        var submitted = Assert.Single(f.Client.Posts);
        Assert.Equal(submitted.ClientRequestId, model.Receipt?.ClientRequestId);
        Assert.Equal("received", model.Receipt?.Status); Assert.False(model.HasPending);
        Assert.Contains("검토 완료를 의미하지", model.Notice); Assert.Equal(string.Empty, model.CategoryCode);
    }

    [Fact]
    public async Task 응답유실후다시열면같은요청을GET조회하고_POST를자동반복하지않는다()
    {
        var f = new Fixture();
        f.Client.Submit = (_, request, _) =>
        {
            f.Client.Receipts[request.ClientRequestId] = Receipt(request);
            throw new HttpRequestException();
        };
        Guid id;
        using (var first = f.Model())
        {
            await first.LoadAsync("request-a"); first.CategoryCode = "incorrect-information"; first.Comment = "입력 확인";
            await first.SubmitAsync();
            Assert.True(first.HasPending); Assert.False(first.CanRetry); id = first.PendingRequestId!.Value;
        }
        using var reopened = f.Model(); await reopened.LoadAsync("request-a");
        Assert.Equal(id, reopened.Receipt?.ClientRequestId); Assert.False(reopened.HasPending);
        Assert.Single(f.Client.Posts); Assert.True(f.Client.Checks > 0);
    }

    [Fact]
    public async Task 미접수조회후명시재시도는같은번호와원래입력으로보낸다()
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException();
        using var model = f.Model(); await model.LoadAsync("request-a");
        model.CategoryCode = "explanation-unclear"; model.Comment = "원래 의견";
        await model.SubmitAsync();
        var initial = Assert.Single(f.Client.Posts);
        await model.RetryAsync(); Assert.Single(f.Client.Posts); // GET 확인 전 재전송 금지
        await model.CheckPendingAsync(); Assert.True(model.CanRetry);
        model.Comment = "뒤늦은 UI 변경";
        f.Client.Submit = (_, request, _) => Task.FromResult(Receipt(request));
        await model.RetryAsync();
        Assert.Equal(2, f.Client.Posts.Count);
        Assert.All(f.Client.Posts, x => { Assert.Equal(initial.ClientRequestId, x.ClientRequestId); Assert.Equal("원래 의견", x.Comment); });
        Assert.Equal(initial.ClientRequestId, model.Receipt?.ClientRequestId);
        Assert.True(f.Client.Checks >= 2);
    }

    [Fact]
    public async Task 보호저장이실패하면POST를시작하지않는다()
    {
        var f = new Fixture(); using var model = new CargoDispatchExplanationFeedbackViewModel(f.Client, new FailingStore(), f.Context);
        await model.LoadAsync("request-a"); model.CategoryCode = "other";
        await model.SubmitAsync();
        Assert.Empty(f.Client.Posts); Assert.False(model.HasPending); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 계정A_B_A로돌아와도이전조회응답이현재화면을덮지않는다()
    {
        var f = new Fixture(); var delayed = new TaskCompletionSource<CargoDispatchExplanationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.Read = (_, _) => delayed.Task;
        using var model = f.Model(); var load = model.LoadAsync("request-a");
        f.Client.Switch("owner-b"); f.Client.Switch("owner-a");
        delayed.SetResult(Recorded("request-a")); await load;
        Assert.Null(model.Explanation); Assert.Null(model.Receipt); Assert.False(model.HasPending);
        Assert.Equal(string.Empty, model.Comment);
    }

    [Fact]
    public async Task 의뢰A_B_A의선택번호는먼저시작한A응답을버린다()
    {
        var f = new Fixture(); var delayed = new TaskCompletionSource<CargoDispatchExplanationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = 0;
        f.Client.Read = (id, _) => ++read == 1 ? delayed.Task : Task.FromResult(Recorded(id, "새 기록"));
        using var model = f.Model(); var old = model.LoadAsync("request-a");
        await model.LoadAsync("request-b"); await model.LoadAsync("request-a");
        delayed.SetResult(Recorded("request-a", "오래된 기록")); await old;
        Assert.Equal("request-a", model.RequestId); Assert.Equal("새 기록", model.SelectedItem?.MethodLabel);
    }

    [Fact]
    public async Task 새계정에는이전계정의미확정입력을노출하지않는다()
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException();
        using var model = f.Model(); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "owner-a private input"; await model.SubmitAsync();
        var id = model.PendingRequestId;
        f.Client.Switch("owner-b"); await model.LoadAsync("request-a");
        Assert.False(model.HasPending); Assert.Equal(string.Empty, model.Comment); Assert.Null(model.Receipt);
        f.Client.Switch("owner-a"); await model.LoadAsync("request-a");
        Assert.True(model.HasPending); Assert.Equal(id, model.PendingRequestId); Assert.Equal("owner-a private input", model.Comment);
        Assert.Single(f.Client.Posts);
    }

    [Fact]
    public async Task 서버가바뀌면이전서버미확정입력을다시보내지않는다()
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException();
        using var model = f.Model(); await model.LoadAsync("request-a"); model.CategoryCode = "other"; await model.SubmitAsync();
        f.Endpoint = new("https://second.test/");
        await model.LoadAsync("request-a");
        Assert.False(model.HasPending); Assert.Equal(string.Empty, model.CategoryCode); Assert.Single(f.Client.Posts);
    }

    [Fact]
    public async Task 만료뒤에도설명ID와요청번호로조회하고_원문을자동복원하지않는다()
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException();
        Guid id;
        using (var first = f.Model())
        {
            await first.LoadAsync("request-a"); first.CategoryCode = "other"; first.Comment = "보관 기한이 있는 의견";
            await first.SubmitAsync(); id = first.PendingRequestId!.Value;
        }
        f.Clock.Now = DateTimeOffset.UtcNow.AddHours(73);
        using var reopened = f.Model(); await reopened.LoadAsync("request-a");
        Assert.True(reopened.PendingInputExpired); Assert.Equal(id, reopened.PendingRequestId);
        Assert.Equal(string.Empty, reopened.Comment); Assert.False(reopened.CanRetry); Assert.True(reopened.CanDiscardExpired);
        await reopened.DiscardExpiredAsync();
        Assert.False(reopened.HasPending); Assert.Equal(string.Empty, reopened.CategoryCode); Assert.Single(f.Client.Posts);
        Assert.True(f.Client.Checks >= 2);
    }

    [Fact]
    public async Task Razor는당시근거와명시선택_500자_접수경계를표시한다()
    {
        var f = new Fixture(); using var model = f.Model();
        var html = await RenderAsync(model, exportName: "recorded-before-submit.html");
        Assert.Contains("당시 저장된 판단", html); Assert.Contains("차량 조건", html); Assert.Contains("확인됨", html);
        Assert.Contains("선택해 주세요", html); Assert.Contains("maxlength=\"500\"", html);
        Assert.Contains("자동 학습에 반영되지", html); Assert.Contains("disabled", Button(html, "의견 보내기"));
        Assert.DoesNotContain("owner-a", html); Assert.Empty(f.Client.Posts);
    }

    [Fact]
    public async Task Razor미기록_실패_접수상태를구분하며선택한경우에만검토HTML을내보낸다()
    {
        var unrecorded = new Fixture();
        unrecorded.Client.Read = (_, _) => Task.FromResult(new CargoDispatchExplanationResponse());
        using var missingModel = unrecorded.Model();
        var missingHtml = await RenderAsync(missingModel, exportName: "unrecorded.html");
        Assert.Contains("당시 배차 설명이 기록되지 않았습니다", missingHtml);
        Assert.DoesNotContain("의견 보내기", missingHtml);

        var failed = new Fixture(); failed.Client.Read = (_, _) => throw new HttpRequestException("private failure");
        using var failedModel = failed.Model();
        var failedHtml = await RenderAsync(failedModel, exportName: "failed.html");
        Assert.Contains("확인하지 못했습니다", failedHtml); Assert.DoesNotContain("private failure", failedHtml);
        Assert.DoesNotContain("당시 배차 설명이 기록되지 않았습니다", failedHtml);

        var received = new Fixture(); using var receivedModel = received.Model();
        var receivedHtml = await RenderAsync(receivedModel, async current =>
        {
            current.CategoryCode = "time-or-route"; current.Comment = "상차 시간 확인 요청";
            await current.SubmitAsync();
        }, exportName: "received.html");
        Assert.Contains("의견이 접수되었습니다", receivedHtml);
        Assert.Contains("운영자 검토 완료를 의미하지 않습니다", receivedHtml);
    }

    [Fact]
    public async Task HTTP인증갱신중계정이바뀌면이전의견을새계정으로전송하지않는다()
    {
        var handler = new Handler(); using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        using var client = new HttpClientAdapter(http);
        client.Ensure = () => { client.Actor = new("owner-b", 2, true); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubmitAsync("request-a", new()
        { ClientRequestId = Guid.NewGuid(), EvidenceId = "e-request-a", CategoryCode = "helpful" }));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task HTTP인증갱신중서버가바뀌면이전의견을새서버로전송하지않는다()
    {
        var handler = new Handler(); using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        using var client = new HttpClientAdapter(http);
        client.Ensure = () => { http.BaseAddress = new("https://second.test/"); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubmitAsync("request-a", new()
        { ClientRequestId = Guid.NewGuid(), EvidenceId = "e-request-a", CategoryCode = "helpful" }));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task 서버가쓰기전입력이나권한을거절하면원문을유지하고수정할수있다(HttpStatusCode status)
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException("private server error", null, status);
        using var model = f.Model(); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "수정할 입력"; await model.SubmitAsync();
        var original = Assert.Single(f.Client.Posts);
        Assert.False(model.HasPending); Assert.Equal("수정할 입력", model.Comment); Assert.Equal("other", model.CategoryCode);
        Assert.True(model.CanSubmit); Assert.DoesNotContain("private", model.Error);
        f.Client.Submit = (_, request, _) => Task.FromResult(Receipt(request)); model.Comment = "수정된 의견";
        await model.SubmitAsync();
        Assert.NotEqual(original.ClientRequestId, model.Receipt?.ClientRequestId);
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("category")]
    [InlineData("evidence")]
    [InlineData("request-id")]
    [InlineData("status")]
    public async Task 접수번호나설명이나의견내용이다른응답은현재입력의접수완료로표시하지않는다(string mismatch)
    {
        var f = new Fixture();
        f.Client.Submit = (_, request, _) =>
        {
            var result = Receipt(request);
            if (mismatch == "comment") result.SubmissionFingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, "다른 의견");
            if (mismatch == "category") result.CategoryCode = "other";
            if (mismatch == "evidence") result.EvidenceId = "another-evidence";
            if (mismatch == "request-id") result.ClientRequestId = Guid.NewGuid();
            if (mismatch == "status") result.Status = "reviewed";
            return Task.FromResult(result);
        };
        using var model = f.Model(); await model.LoadAsync("request-a"); model.CategoryCode = "helpful";
        await model.SubmitAsync();
        Assert.Null(model.Receipt); Assert.True(model.HasPending); Assert.False(model.CanRetry);
        Assert.Contains("다른 접수 기록", model.Error);
    }

    [Fact]
    public async Task 같은종류라도다른문구로충돌한접수조회는현재의견성공으로승격하지않는다()
    {
        var f = new Fixture();
        f.Client.Submit = (_, request, _) =>
        {
            var other = Receipt(request); other.SubmissionFingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, "다른 입력");
            f.Client.Receipts[request.ClientRequestId] = other;
            throw new HttpRequestException("conflict", null, HttpStatusCode.Conflict);
        };
        using var model = f.Model(); await model.LoadAsync("request-a"); model.CategoryCode = "other"; model.Comment = "내 입력";
        await model.SubmitAsync(); Assert.Contains("충돌", model.Error);
        await model.CheckPendingAsync();
        Assert.Null(model.Receipt); Assert.True(model.HasPending); Assert.Equal("내 입력", model.Comment); Assert.False(model.CanRetry);
        using var reopened = f.Model(); await reopened.LoadAsync("request-a");
        Assert.Null(reopened.Receipt); Assert.True(reopened.HasPending); Assert.Contains("다른 접수 기록", reopened.Error);
    }

    [Fact]
    public async Task 조회중권한거절은이전에보낸미확정요청을삭제하지않는다()
    {
        var f = new Fixture(); f.Client.Submit = (_, _, _) => throw new HttpRequestException();
        using var model = f.Model(); await model.LoadAsync("request-a"); model.CategoryCode = "other"; await model.SubmitAsync();
        var id = model.PendingRequestId; await model.CheckPendingAsync(); Assert.True(model.CanRetry);
        f.Client.Check = (_, _, _, _) => throw new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden);
        await model.RetryAsync();
        Assert.True(model.HasPending); Assert.Equal(id, model.PendingRequestId); Assert.Single(f.Client.Posts);
    }

    [Fact]
    public async Task Dispose뒤늦은조회응답은개인설명을복원하지않는다()
    {
        var f = new Fixture(); var response = new TaskCompletionSource<CargoDispatchExplanationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.Read = (_, _) => response.Task;
        var model = f.Model(); var read = model.LoadAsync("request-a"); model.Dispose();
        response.SetResult(Recorded("request-a")); await read;
        Assert.Null(model.Explanation); Assert.Null(model.Receipt);
    }

    [Fact]
    public async Task Dispose전에캡처된늦은세션알림은폐기된토큰을다시취소하지않는다()
    {
        var f = new Fixture(); var model = f.Model(); await model.LoadAsync("request-a");
        var queued = f.Client.CaptureSessionNotification(); Assert.NotNull(queued);
        model.Dispose(); queued();
        Assert.Null(model.Explanation); Assert.Null(model.Receipt); Assert.False(model.HasPending);
        Assert.Null(model.Notice);
    }

    [Fact]
    public async Task 다른의뢰의설명응답이나제어문자입력은접수로이어지지않는다()
    {
        var f = new Fixture(); using var model = f.Model(); f.Client.Read = (_, _) => Task.FromResult(Recorded("other-request"));
        await model.LoadAsync("request-a"); Assert.Null(model.Explanation); Assert.NotNull(model.Error);
        f.Client.Read = (id, _) => Task.FromResult(Recorded(id)); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "첫줄\n다음줄";
        Assert.False(model.CanSubmit); Assert.NotNull(model.CommentValidation); await model.SubmitAsync(); Assert.Empty(f.Client.Posts);
    }

    [Fact]
    public async Task 선택문구의앞뒤공백도실제보낸원문과같은접수해시로확인한다()
    {
        var f = new Fixture(); using var model = f.Model(); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "  입력 공백 유지  "; await model.SubmitAsync();
        var request = Assert.Single(f.Client.Posts);
        Assert.Equal("  입력 공백 유지  ", request.Comment);
        Assert.Equal(CargoDispatchFeedbackFingerprint.Compute("other", request.Comment), model.Receipt?.SubmissionFingerprint);
    }

    private static CargoDispatchExplanationResponse Recorded(string id, string method = "저장된 규칙 판단") => new()
    {
        Status = "recorded", Notice = "저장된 당시 기록입니다.", Items = [new()
        {
            RequestId = id, EvidenceId = "e-" + id, RecommendationRound = 1, MethodLabel = method,
            OccurredAtUtc = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc), CanSubmitFeedback = true,
            Facts = [new("차량 조건", "확인됨")], Limitations = ["당시 확인된 정보 범위입니다."]
        }]
    };
    private static CargoDispatchFeedbackReceipt Receipt(CargoDispatchFeedbackRequest request) => new()
    {
        FeedbackId = "feedback-" + request.ClientRequestId, ClientRequestId = request.ClientRequestId,
        EvidenceId = request.EvidenceId, CategoryCode = request.CategoryCode, Status = "received", SubmittedAtUtc = DateTime.UtcNow,
        SubmissionFingerprint = CargoDispatchFeedbackFingerprint.Compute(request.CategoryCode, request.Comment)
    };
    private sealed class Fixture
    {
        public Client Client { get; } = new(); public Clock Clock { get; } = new();
        public VolatileWorkflowRecoveryStore Store { get; }
        public Uri Endpoint { get; set; } = new("https://first.test/");
        public IWorkflowRecoveryContext Context { get; }
        public Fixture()
        {
            Store = new(Clock); Context = new WorkflowRecoveryContext(() =>
                new RoleWorkspaceIdentity(Client.Identity.UserId, Client.Identity.SessionRevision, Client.Identity.IsAuthenticated), () => Endpoint);
        }
        public CargoDispatchExplanationFeedbackViewModel Model() => new(Client, Store, Context);
    }
    private sealed class Client : ICargoDispatchExplanationClient
    {
        public CargoDispatchExplanationIdentity Identity { get; private set; } = new("owner-a", 1, true);
        public event Action? SessionChanged;
        public List<CargoDispatchFeedbackRequest> Posts { get; } = [];
        public Dictionary<Guid, CargoDispatchFeedbackReceipt> Receipts { get; } = [];
        public int Checks;
        public Func<string, CancellationToken, Task<CargoDispatchExplanationResponse>> Read = (id, _) => Task.FromResult(Recorded(id));
        public Func<string, CargoDispatchFeedbackRequest, CancellationToken, Task<CargoDispatchFeedbackReceipt>> Submit = (_, r, _) => Task.FromResult(Receipt(r));
        public Func<string, string, Guid, CancellationToken, Task<CargoDispatchFeedbackReceipt?>>? Check;
        public Action? CaptureSessionNotification() => SessionChanged;
        public void Switch(string id) { Identity = new(id, Identity.SessionRevision + 1, true); SessionChanged?.Invoke(); }
        public Task RestoreAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<CargoDispatchExplanationResponse> ReadAsync(string id, CancellationToken ct = default) => Read(id, ct);
        public Task<CargoDispatchFeedbackReceipt> SubmitAsync(string id, CargoDispatchFeedbackRequest r, CancellationToken ct = default)
        {
            Posts.Add(new() { ClientRequestId = r.ClientRequestId, EvidenceId = r.EvidenceId, CategoryCode = r.CategoryCode, Comment = r.Comment });
            return Submit(id, r, ct);
        }
        public Task<CargoDispatchFeedbackReceipt?> ReadFeedbackAsync(string id, string evidenceId, Guid request, CancellationToken ct = default)
        { Checks++; return Check is null ? Task.FromResult(Receipts.GetValueOrDefault(request)) : Check(id, evidenceId, request, ct); }
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FailingStore : IWorkflowRecoveryStore
    {
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => Task.FromResult<WorkflowRecoveryRecord?>(null);
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => throw new IOException();
        public Task RemoveAsync(string owner, string scope, Guid id, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new CargoDispatchFeedbackReceipt()) }); }
    }
    private sealed class HttpClientAdapter(HttpClient http) : AuthenticatedCargoDispatchExplanationClient(http)
    {
        public CargoDispatchExplanationIdentity Actor = new("owner-a", 1, true);
        public Func<Task> Ensure = () => Task.CompletedTask;
        public override CargoDispatchExplanationIdentity Identity => Actor;
        protected override string? AccessToken => "synthetic-token";
        public override Task RestoreAsync(CancellationToken ct = default) => Task.CompletedTask;
        protected override Task EnsureAuthorizedAsync(CancellationToken ct) => Ensure();
    }
    private static string Button(string html, string text) => Assert.Single(Regex.Matches(html, "<button\\b[^>]*>(?:(?!</button>).)*</button>", RegexOptions.Singleline)
        .Cast<Match>().Select(x => x.Value).Where(x => x.Contains(text, StringComparison.Ordinal)));
    private static async Task<string> RenderAsync(CargoDispatchExplanationFeedbackViewModel model,
        Func<CargoDispatchExplanationFeedbackViewModel, Task>? afterInitialRender = null, string? exportName = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CargoDispatchExplanationFeedbackPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CargoDispatchExplanationFeedbackPanel.RequestId)] = "request-a",
                [nameof(CargoDispatchExplanationFeedbackPanel.Model)] = model,
                [nameof(CargoDispatchExplanationFeedbackPanel.InitiallyExpanded)] = true
            }));
            if (afterInitialRender is not null) await afterInitialRender(model);
            return component.ToHtmlString();
        });
        if (exportName is not null) ExportPreview(exportName, html);
        return WebUtility.HtmlDecode(html);
    }

    // 선택적 정적 화면 증거 수집이다. 파일 작성 여부를 시험의 성공 조건으로 삼지 않는다.
    private static void ExportPreview(string name, string html)
    {
        var output = Environment.GetEnvironmentVariable("SSALDDEL_CARGO_EXPLANATION_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName,
                "Ssalddel.Ui.Common", "Areas", "App", "Components", "Transport", "CargoDispatchExplanationFeedbackPanel.razor.css"))) root = root.Parent;
            if (root is null) return;
            var local = Path.Combine(root.FullName, "artifacts", "local") + Path.DirectorySeparatorChar;
            var folder = Path.GetFullPath(output);
            if (!folder.StartsWith(local, StringComparison.OrdinalIgnoreCase)) return;
            var css = File.ReadAllText(Path.Combine(root.FullName, "Ssalddel.Ui.Common", "Areas", "App", "Components", "Transport",
                "CargoDispatchExplanationFeedbackPanel.razor.css"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">"
                + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>배차 설명·의견 — 합성 화면 검토</title><style>"
                + "body{margin:0;padding:12px;background:#f4f6f8;font-family:system-ui,sans-serif}*{box-sizing:border-box}" + css
                + "</style></head><body>" + html + "</body></html>");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 내보내기 실패는 별도 증거 미생성으로 남기며 렌더 회귀 결과를 바꾸지 않는다.
        }
    }
}
