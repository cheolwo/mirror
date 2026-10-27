using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoTransportActionUsabilityTests
{
    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    public async Task 사진과_현장확인_누락을_순서대로_안내하되_완료조건을_완화하지_않는다(string action)
    {
        var api = new Api(action == "pickup" ? "상차지도착" : "하차지도착");
        using var model = Model(api);
        await model.LoadAsync(1, action);
        Assert.Contains("화물과 수량", model.SubmissionReadinessMessage);
        model.CargoConfirmed = true;
        Assert.Contains("작업 장소", model.SubmissionReadinessMessage);
        model.LocationConfirmed = true;
        Assert.Contains("인계 담당자", model.SubmissionReadinessMessage);
        model.RecipientConfirmed = true;
        Assert.Contains("현장 사진", model.SubmissionReadinessMessage);
        Assert.False(model.CanSubmit);
        model.SetPhoto(new Photo());
        if (action == "dropoff")
        {
            Assert.Contains("결제·증빙 조건", model.SubmissionReadinessMessage);
            model.PaymentConfirmed = true;
        }
        Assert.Contains("확인한 내용으로 저장", model.SubmissionReadinessMessage);
        model.SaveConfirmed = true;
        Assert.Null(model.SubmissionReadinessMessage);
        Assert.True(model.CanSubmit);
        Assert.Equal(0, api.Writes); Assert.Equal(0, api.Uploads);
    }

    [Theory]
    [InlineData(true, "document")]
    [InlineData(true, "signatures")]
    [InlineData(false, "omission")]
    public async Task 인수증_필요상태의_허용된_기존_대안만_완료로_안내한다(bool signatureRequired, string method)
    {
        var api = new Api("상차지도착") { ReceiptRequired = true, SignatureRequired = signatureRequired };
        using var model = Model(api);
        await model.LoadAsync(1, "pickup"); Ready(model);
        Assert.False(model.CanSubmit);
        Assert.Contains(signatureRequired ? "인수자명·인수자 서명·기사 서명" : "서명 생략 확인과 사유", model.SubmissionReadinessMessage);
        if (method == "document") model.DocumentPhotoConfirmed = true;
        else if (method == "signatures")
        {
            model.RecipientName = "현장 담당자"; model.RecipientSignature = "본인 직접 서명";
            Assert.False(model.CanSubmit);
            model.DriverSignature = "기사 직접 서명";
        }
        else
        {
            model.ReceiptOmissionConfirmed = true;
            Assert.False(model.CanSubmit);
            model.ReceiptOmissionReason = "현장 합의로 생략";
        }
        Assert.True(model.CanSubmit); Assert.Null(model.SubmissionReadinessMessage);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 서명필수_상차는_생략확인만으로_제출하지_못한다()
    {
        var api = new Api("상차지도착") { ReceiptRequired = true, SignatureRequired = true };
        using var model = Model(api);
        await model.LoadAsync(1, "pickup"); Ready(model);
        model.ReceiptOmissionConfirmed = true; model.ReceiptOmissionReason = "서명 생략 요청";
        Assert.False(model.CanSubmit);
        Assert.Contains("서명된 인수증 사진", model.SubmissionReadinessMessage);
        await model.SubmitAsync();
        Assert.Equal(0, api.Writes); Assert.Equal(0, api.Uploads);
    }

    [Theory]
    [InlineData("pickup", "상차완료")]
    [InlineData("pickup", "운송중")]
    [InlineData("pickup", "하차지도착")]
    [InlineData("pickup", "인수완료")]
    [InlineData("dropoff", "인수완료")]
    public async Task 새_입력화면도_정본의_이미완료된단계를_재저장하지_않고_다음업무로_안내한다(string action, string state)
    {
        var api = new Api(state);
        using var model = Model(api);
        await model.LoadAsync(1, action);
        Assert.True(model.CompletionConfirmed);
        Assert.False(model.Saved); // 현재 화면이 저장한 사실과 정본에서 읽은 완료 사실은 다릅니다.
        Assert.False(model.CanSubmit); Assert.Null(model.SubmissionReadinessMessage);
        Assert.Contains(action == "pickup" ? "상차 완료" : "하차·인수 완료", model.CompletionMessage);
        Assert.Equal(state == "인수완료" ? "/workspace/cargo-driver" : "/workspace/cargo-driver?selected=1", model.CompletionHref);
        await model.SubmitAsync(); Assert.Equal(0, api.Writes); Assert.Equal(0, api.Uploads);
    }

    [Theory]
    [InlineData("pickup", "상차지도착")]
    [InlineData("pickup", "운송취소")]
    [InlineData("pickup", "완료")]
    [InlineData("dropoff", "상차완료")]
    [InlineData("dropoff", "하차지도착")]
    [InlineData("dropoff", "배송완료")]
    public async Task 취소나_추정완료와_이전단계를_실제_상하차완료로_표시하지_않는다(string action, string state)
    {
        using var model = Model(new Api(state));
        await model.LoadAsync(1, action);
        Assert.False(model.CompletionConfirmed);
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    public async Task 완료응답이_끊겨도_정본재조회로_완료를_복구하며_사진과_명령을_중복전송하지_않는다(string action)
    {
        var api = new Api(action == "pickup" ? "상차지도착" : "하차지도착") { LoseCompletionResponse = true };
        using var model = Model(api);
        await model.LoadAsync(1, action); Ready(model);
        await model.SubmitAsync();
        Assert.True(model.Lifetime.HasError); Assert.False(model.CompletionConfirmed);
        Assert.Contains("다시 저장하기 전에 현재 상태 다시 확인", model.Lifetime.Message);
        Assert.Contains("처리 결과를 먼저 확인", model.SubmissionReadinessMessage);
        await model.SubmitAsync(); Assert.Equal(1, api.Writes);
        await model.LoadAsync(1, action);
        Assert.False(model.Lifetime.HasError); Assert.True(model.CompletionConfirmed); Assert.False(model.CanSubmit);
        await model.SubmitAsync(); Assert.Equal(1, api.Writes); Assert.Equal(1, api.Uploads);
    }

    [Fact]
    public async Task 조회실패나_개인정보보류는_기존완료_화면을_새확인성공으로_승격하지_않는다()
    {
        var api = new Api("인수완료"); var access = new Access();
        using var model = Model(api, access);
        await model.LoadAsync(1, "dropoff"); Assert.True(model.CompletionConfirmed);
        api.FailRead = true;
        await model.LoadAsync(1, "dropoff");
        Assert.True(model.Lifetime.HasError); Assert.False(model.CompletionConfirmed);
        api.FailRead = false; api.PrivacyHold = true;
        await model.LoadAsync(1, "dropoff");
        Assert.False(model.CompletionConfirmed); Assert.Null(model.Source);
        api.PrivacyHold = false; await model.LoadAsync(1, "dropoff");
        Assert.True(model.CompletionConfirmed);
        access.Switch("another-driver");
        Assert.Null(model.Source); Assert.False(model.CompletionConfirmed);
    }

    [Fact]
    public async Task 문제신고는_기존_신고존재만으로_이번신고_완료를_추정하지_않는다()
    {
        using var model = Model(new Api("상차지도착") { ReportedIssue = true });
        await model.LoadAsync(1, "issue");
        Assert.False(model.CompletionConfirmed);
        Assert.Contains("문제 사유", model.SubmissionReadinessMessage);
        model.IssueReason = "현재 문제";
        Assert.Contains("확인한 내용으로 저장", model.SubmissionReadinessMessage);
    }

    [Theory]
    [InlineData("pickup", "상차완료", true)]
    [InlineData("dropoff", "인수완료", false)]
    public async Task 실제_컴포넌트는_정본완료와_다음업무_링크를_표시하고_오해하는_비활성저장을_숨긴다(string action, string state, bool held)
    {
        var api = new Api(state) { WorkHeld = held };
        var html = await RenderAsync(api, action);
        Assert.Contains("확인된 완료 상태", html);
        Assert.Contains("지도에서 다음 업무 확인", html);
        Assert.DoesNotContain("현재 단계에서는 이 입력을 저장할 수 없습니다", html);
        Assert.DoesNotContain("현장 사진 선택", html);
        Assert.DoesNotContain("현재 상태 다시 확인", html);
        Assert.Contains(held ? "운송이 보류되어 있습니다" : "정산과 입금 상태는 별도로 확인", html);
        Assert.Equal(0, api.Writes); Assert.Equal(0, api.Uploads);
    }

    [Theory]
    [InlineData("pickup", "상차지도착")]
    [InlineData("dropoff", "하차지도착")]
    public async Task 실제_저장버튼은_부족이유를_접근성설명으로_연결한다(string action, string state)
    {
        var html = await RenderAsync(new Api(state), action);
        Assert.Contains("화물과 수량을 확인해 주세요", html);
        Assert.Contains("aria-describedby=\"cargo-action-readiness-", html);
        Assert.Contains("role=\"status\"", html);
        Assert.Contains("disabled", html);
    }

    private static CargoTransportActionViewModel Model(Api api, Access? access = null) => new(new(api), api, access ?? new());
    private static void Ready(CargoTransportActionViewModel model)
    {
        model.SetPhoto(new Photo());
        model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.PaymentConfirmed = model.SaveConfirmed = true;
    }
    private static async Task<string> RenderAsync(Api api, string action)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, Js>(); services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<IRoleWorkspaceAccess, Access>(); services.AddSingleton<IRoleWorkspaceApi>(api);
        services.AddSingleton(new CargoDriverWorkspaceApiClient(api));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var rendered = await renderer.RenderComponentAsync<CargoTransportActionScreen>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CargoTransportActionScreen.ItemId)] = 1L, [nameof(CargoTransportActionScreen.ActionKey)] = action }));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
    }
    private sealed class Photo : IBrowserFile
    {
        public string Name => "현장.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3;
        public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
    private sealed class Api(string state) : IRoleWorkspaceApi
    {
        public int Writes { get; private set; }
        public int Uploads { get; private set; }
        public bool ReceiptRequired { get; init; }
        public bool SignatureRequired { get; init; }
        public bool WorkHeld { get; init; }
        public bool ReportedIssue { get; init; }
        public bool LoseCompletionResponse { get; init; }
        public bool FailRead { get; set; }
        public bool PrivacyHold { get; set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, roleKey);
            if (FailRead) throw new HttpRequestException("Synthetic read interruption");
            return Task.FromResult((T)(object)new 기사운송상세응답
            {
                Id = 1, 상태 = state, 출발지 = "상차 장소", 도착지 = "하차 장소", 인수증필요 = ReceiptRequired,
                인수증서명필수 = SignatureRequired, 운송진행보류 = WorkHeld, 개인정보제공보류 = PrivacyHold,
                예외신고됨 = ReportedIssue, 관리자확인필요 = ReportedIssue, 최근예외메시지 = "이전 신고"
            });
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        {
            Writes++;
            state = path.EndsWith("/pickup-complete", StringComparison.Ordinal) ? "상차완료" : "인수완료";
            if (LoseCompletionResponse) throw new HttpRequestException("Synthetic successful response loss");
            return Task.FromResult((T?)(object)new 기사운송상태변경응답 { Id = 1, 상태 = state });
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        { Uploads++; return Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("test-photo", null)); }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("driver", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) { Switch(null); return Task.CompletedTask; }
        public void Switch(string? owner) { identity = new(owner, identity.Revision + 1, owner is not null); Changed?.Invoke(); }
    }
    private sealed class Js : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/workspace/cargo-driver/transports/1/pickup");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
