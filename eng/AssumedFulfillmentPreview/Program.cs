using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using AssumedFulfillmentPreview;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Components.Fulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;
using Ssalddel.Services.Community;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("r28 화면 샘플은 개발 환경의 loopback에서만 실행합니다.");
var port = builder.Configuration.GetValue<int?>("AssumedPort") ?? 5400;
if (port is < 1024 or > 65535) throw new InvalidOperationException("검토 호스트 포트를 확인해 주세요.");
builder.WebHost.UseUrls("http://127.0.0.1:" + port).UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<검토생활User>();
builder.Services.AddScoped<ISsalddel현재사용자Context>(sp => sp.GetRequiredService<검토생활User>());
builder.Services.AddScoped<검토생활Client>();
builder.Services.AddScoped<INeighborhoodDeliveryClient>(sp => sp.GetRequiredService<검토생활Client>());
builder.Services.AddScoped<NeighborhoodDeliveryAuthoringViewModel>();
builder.Services.AddScoped<I통신판매보호Client, 검토거래안내Client>();
builder.Services.AddTransient<통신판매안내ViewModel>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
    { context.Response.StatusCode = 404; return; }
    context.Response.Headers.CacheControl = "no-store"; await next();
});
app.UseStaticFiles(); app.UseAntiforgery(); app.MapStaticAssets();
app.MapGet("/", () => new RazorComponentResult<App>());
app.MapGet("/community/exchange/deliveries/{**path}", () => new RazorComponentResult<App>());
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

namespace AssumedFulfillmentPreview
{
    /// <summary>실제 보호 DTO와 공통 작성/조건/상태 컴포넌트의 판독만 위한 메모리 샘플입니다.</summary>
    public sealed class AssumedPreviewPanel : ComponentBase, IDisposable
    {
        [Inject] public NavigationManager Navigation { get; set; } = null!;
        [Inject] public NeighborhoodDeliveryAuthoringViewModel Writer { get; set; } = null!;
        [Inject] public 검토생활Client Client { get; set; } = null!;
        [Inject] public 검토생활User User { get; set; } = null!;
        private string state = "create";
        private AutoFulfillmentSnapshot? status;

        protected override async Task OnInitializedAsync()
        {
            Writer.PropertyChanged += WriterChanged;
            var uri = new Uri(Navigation.Uri);
            state = QueryHelpers.ParseQuery(uri.Query).TryGetValue("state", out var value) ? value.ToString() : "create";
            if (uri.AbsolutePath != "/" && !uri.Query.Contains("state=", StringComparison.Ordinal)) state = "accepted";
            if (state == "logout") User.Current = 현재사용자Snapshot.익명;
            Client.EmptyProviders = state == "empty";
            Writer.Initialize(null);
            if (state is "logout" or "consent") return;
            Writer.CollectionConsent = true; Writer.AgeConfirmed = true; await Writer.AcceptConsentAsync();
            if (state == "input-blank") return;
            Writer.Draft.CargoName = "공유할 책 한 상자 · 합성 표본"; Writer.Draft.Quantity = 1; Writer.Draft.WeightKg = 2;
            Writer.PackageSizeCode = state == "missing-size" ? null : NeighborhoodPackageSizes.Small;
            var locations = new[] { Writer.Draft.Pickup, Writer.Draft.Dropoff };
            for (var i = 0; i < locations.Length; i++)
            {
                locations[i].주소.도로명주소 = i == 0 ? "합성 픽업 장소" : "합성 전달 장소";
                locations[i].연락처.이름 = "검토 이용자"; locations[i].연락처.전화번호 = "00000000000";
            }
            if (state.StartsWith("input-", StringComparison.Ordinal))
            {
                Writer.SimpleInput.TimeReviewed = true;
                var steps = state switch { "input-places" => 1, "input-time" => 2, "input-confirm" => 3, _ => 0 };
                for (var i = 0; i < steps; i++) Writer.SimpleInput.Next();
                return;
            }
            // r28 상세 입력 상태의 기존 회귀 검증을 유지합니다.
            Writer.SimpleInput.ToggleDetailed();
            Writer.SimpleInput.TimeReviewed = true;
            await Writer.QuoteAsync();
            Writer.AutoConditions.MaximumUserChargeKrw = 6000;
            Writer.AutoConditions.DesiredCompletionLocal = DateTime.Now.AddHours(3);
            if (!Client.EmptyProviders) Writer.AutoConditions.SetAllowed(AssumedFulfillmentProviderCatalog.Baemin, true);
            await Writer.QuoteAsync();
            if (state is "create" or "missing-size" or "empty") return;
            status = Client.Status(state switch
            {
                "unknown" => AutoFulfillmentStates.ResultUnknown,
                "assigned" => AutoFulfillmentStates.DriverAssigned,
                "in-transit" => AutoFulfillmentStates.InTransit,
                "completed" => AutoFulfillmentStates.Completed,
                "paused" => AutoFulfillmentStates.NeedsReview,
                _ => AutoFulfillmentStates.Accepted
            });
            status.ExecutionPaused = state == "paused";
            if (status.ExecutionPaused) { status.TotalCostKrw = null; status.UserChargeKrw = null; status.EstimatedCompletionUtc = null; }
        }

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            if (status is null)
            {
                if (state.StartsWith("input-", StringComparison.Ordinal))
                {
                    b.OpenElement(0, "small"); b.AddAttribute(1, "data-input-counters", "true");
                    b.AddContent(2, $"검토 호출 수 · 미리보기 {Client.ImportCalls} / 견적 {Client.QuoteCalls} / 등록 {Client.CreateCalls}"); b.CloseElement();
                }
                b.OpenComponent<NeighborhoodDeliveryCreate>(3); b.CloseComponent(); return;
            }
            b.OpenElement(1, "main"); b.AddAttribute(2, "class", "preview-state");
            b.OpenElement(3, "h1"); b.AddContent(4, "내 생활 배송"); b.CloseElement();
            b.OpenElement(5, "small"); b.AddContent(6, "우리 앱에서 입력하고 가정 외부 수행처에서 처리하는 샘플입니다. 전달 증거와 약속의 양측 완료 확인·지급은 별도입니다."); b.CloseElement();
            b.OpenComponent<AutoFulfillmentStatus>(7);
            b.AddAttribute(8, nameof(AutoFulfillmentStatus.Value), status);
            b.AddAttribute(9, nameof(AutoFulfillmentStatus.Pending), status.StateCode is AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending);
            b.AddAttribute(10, nameof(AutoFulfillmentStatus.RefreshRequested), EventCallback.Factory.Create(this, Refresh));
            b.AddAttribute(11, nameof(AutoFulfillmentStatus.CancelRequested), EventCallback.Factory.Create(this, Cancel));
            b.AddAttribute(12, nameof(AutoFulfillmentStatus.RevokeRequested), EventCallback.Factory.Create(this, Revoke));
            b.CloseComponent(); b.CloseElement();
        }

        private void WriterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => _ = InvokeAsync(StateHasChanged);
        public void Dispose() => Writer.PropertyChanged -= WriterChanged;

        private void Refresh()
        {
            if (status is null || status.ExecutionPaused) return;
            // '불명확' 확인은 같은 표본 시도만 갱신하고 새 업체나 새 요청을 생성하지 않습니다.
            if (status.StateCode == AutoFulfillmentStates.ResultUnknown) status.StateCode = AutoFulfillmentStates.Accepted;
            status.Revision++; status.UpdatedAtUtc = DateTime.UtcNow;
            status.CanCancel = status.StateCode is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned;
        }
        private void Cancel()
        {
            if (status?.CanCancel != true) return;
            status.StateCode = AutoFulfillmentStates.Cancelled; status.CanCancel = false; status.CanRenewConsent = true;
            status.Revision++; status.UpdatedAtUtc = DateTime.UtcNow; status.TotalCostKrw = 0; status.UserChargeKrw = 0;
        }
        private void Revoke()
        {
            if (status is null) return;
            status.ReviewCode = "ConsentRevoked"; status.SelectionReason = "새 접수 동의 철회 표본 · 현재 수행의 결과와 취소는 별도로 확인합니다.";
            status.ConsentRevision++; status.Revision++; status.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public sealed class 검토생활User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Current { get; set; } = new("simulation-preview-owner", "합성 검토 이용자", []);
        public 현재사용자Snapshot 현재사용자 => Current;
    }

    public sealed class 검토거래안내Client : I통신판매보호Client
    {
        public Task<통신판매안내Response?> 안내Async(CancellationToken ct)
            => Task.FromResult<통신판매안내Response?>(new()
            {
                Version = "assumed-fulfillment-r28-preview", ExecutionModeCode = "Simulation", IsOperationalReady = false,
                IntermediaryNotice = "화면 확인용 합성 거래 안내입니다. 실제 거래 당사자·계약·배송·결제 확인을 대신하지 않습니다."
            });
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new NotSupportedException("이 화면 샘플은 거래 안내 조회만 제공합니다."));
        public Task<판매자확인Response?> 판매자Async(CancellationToken ct) => Unavailable<판매자확인Response?>();
        public Task<판매자확인Response?> 등록Async(판매자등록Request request, CancellationToken ct) => Unavailable<판매자확인Response?>();
        public Task<판매자확인Response?> 확인Async(판매자확인Request request, CancellationToken ct) => Unavailable<판매자확인Response?>();
        public Task<판매자공개정보Response?> 공개판매자Async(string? sellerId, long? restaurantId, CancellationToken ct) => Unavailable<판매자공개정보Response?>();
        public Task<판매자공개정보Response?> 공개거래판매자Async(long? postId, string? collaborationId, CancellationToken ct) => Unavailable<판매자공개정보Response?>();
        public Task<보호지원ListResponse> 목록Async(bool privacy, int page, CancellationToken ct) => Unavailable<보호지원ListResponse>();
        public Task<보호지원CaseResponse?> 상세Async(bool privacy, string caseId, CancellationToken ct) => Unavailable<보호지원CaseResponse?>();
        public Task<보호지원CaseResponse?> 분쟁Async(거래분쟁접수Request request, CancellationToken ct) => Unavailable<보호지원CaseResponse?>();
        public Task<보호지원CaseResponse?> 권리Async(개인정보권리접수Request request, CancellationToken ct) => Unavailable<보호지원CaseResponse?>();
        public Task<보호지원CaseResponse?> 변경Async(bool privacy, string caseId, 보호지원CommandRequest request, CancellationToken ct) => Unavailable<보호지원CaseResponse?>();
    }

    public sealed class 검토생활Client : INeighborhoodDeliveryClient
    {
        public bool EmptyProviders { get; set; }
        public int ImportCalls { get; private set; }
        public int QuoteCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public Task<NeighborhoodDeliveryInputTemplateResponse?> GetInputTemplateAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using var stream = typeof(생활배송엑셀초안Service).Assembly.GetManifestResourceStream(NeighborhoodDeliveryInputSchema.TemplateResourceName)
                ?? throw new InvalidOperationException("정적 입력 양식을 찾지 못했습니다.");
            using var buffer = new MemoryStream(); stream.CopyTo(buffer);
            return Task.FromResult<NeighborhoodDeliveryInputTemplateResponse?>(new() { ContentBase64 = Convert.ToBase64String(buffer.ToArray()) });
        }
        public Task<NeighborhoodDeliveryInputPreviewResponse?> PreviewInputAsync(NeighborhoodDeliveryInputPreviewRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ImportCalls++;
            // 권한·동의는 제품 서비스 시험에서 검증합니다. 여기서는 실제 순수 파서와 공통 UI만 결속합니다.
            return Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(생활배송엑셀초안Service.Parse(Convert.FromBase64String(request.ContentBase64), ct));
        }
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
            => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        {
            QuoteCalls++;
            return Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new()
            {
                FulfillmentRoutingEnabled = true,
                AutomaticProviders = EmptyProviders ? [] : AutoFulfillmentNotice.Providers.Concat(AssumedFulfillmentProviderCatalog.Providers).ToArray(),
                Fare = new() { 최종운임 = 4000, 예상거리Km = 3, 거리계산방식 = "합성 표본 경로 · 실제 견적 아님" },
                SettlementNotice = "실제 결제·기사 지급이 없는 화면 샘플입니다."
            });
        }
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        {
            CreateCalls++;
            return Task.FromResult<NeighborhoodDeliveryResponse?>(new()
            { RequestId = "simulation-request", CargoName = request.CargoName, PackageSizeCode = request.PackageSizeCode, AutoFulfillment = Status(AutoFulfillmentStates.Accepted) });
        }
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct)
            => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = requestId, AutoFulfillment = Status(AutoFulfillmentStates.Accepted) });
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
        public AutoFulfillmentSnapshot Status(string state) => new()
        {
            SourceKind = FulfillmentSourceKinds.NeighborhoodDelivery, SourceId = "simulation-request", StateCode = state,
            AttemptId = "simulation-same-attempt", ProviderId = AssumedFulfillmentProviderCatalog.Baemin,
            ProviderName = "배민 연동 가정 · 모의", Revision = 3, ConsentRevision = 1,
            MaxUserChargeKrw = 6000, TotalCostKrw = 4600, UserChargeKrw = 4600,
            EstimatedCompletionUtc = DateTime.UtcNow.AddMinutes(34), UpdatedAtUtc = DateTime.UtcNow,
            CanCancel = state is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned,
            CanRenewConsent = state is AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed,
            IsSimulation = true, IsOperationalSettlementEligible = false,
            SelectionReason = "동의한 목록의 비용·시간 조건으로 선택한 합성 표본 · 실제 업체 비교 정보 아님"
        };
    }
}
