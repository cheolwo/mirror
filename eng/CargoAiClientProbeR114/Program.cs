using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace CargoAiClientProbeR114;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--theme-output") return await ExportThemeAsync(args[1]);
        if (args.Length != 2 || args[0] != "--input") { Console.Error.WriteLine("Use --input <private-input-json>"); return 2; }
        string? output = null;
        try
        {
            var repository = FindRepository();
            var inputPath = Path.GetFullPath(args[1]);
            if (!inputPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".private", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("ProbeInputMustBePrivate");
            var input = JsonSerializer.Deserialize<ProbeInput>(await File.ReadAllBytesAsync(inputPath), Json) ?? throw new InvalidOperationException("ProbeInputMissing");
            if (input.BaseAddress.TrimEnd('/') != "http://127.0.0.1:5364" || string.IsNullOrWhiteSpace(input.AccessToken)
                || !input.DriverId.StartsWith("cargo-journey-driver", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(input.RequestId)
                || input.RequestId.Any(c => char.IsControl(c) || c is '/' or '\\' or '?' or '#') || input.TransportId is <= 0)
                throw new InvalidOperationException("ProbeInputOutsideR114Boundary");
            var allowedOutput = Path.GetFullPath(Path.Combine(repository, "artifacts", "local", "cargo-ai-device-r114")) + Path.DirectorySeparatorChar;
            output = Path.GetFullPath(input.OutputRoot);
            if (!output.StartsWith(allowedOutput, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ProbeOutputOutsideR114Boundary");
            Directory.CreateDirectory(output);
            if (File.Exists(Path.Combine(output, "result.json"))) throw new InvalidOperationException("ProbeOutputAlreadyExists");

            using var api = new ReadOnlyProbeApi(input);
            var client = new CargoDriverWorkspaceApiClient(api);
            var access = new ProbeAccess(input.DriverId);
            var checks = new List<object>();
            var errors = new List<object>();
            var files = new List<object>();
            var offers = await client.RecommendationsAsync(CancellationToken.None);
            var workspace = await client.WorkspaceAsync(CancellationToken.None);
            var target = offers.FirstOrDefault(o => o.의뢰Id == input.RequestId);
            var projections = new List<RoleWorkspaceItem>();
            object? offerResult = null, transportResult = null;
            if (target is not null)
            {
                var projection = CargoDriverWorkspaceAdapter.MapOffer(target);
                projections.Add(projection);
                var pricing = DriverOfferPresentation.Pricing(target.Pricing);
                var visual = DriverOfferPresentation.Resolve(target.WorkKind, target.AssignedActivities);
                using var model = new CargoOfferActionViewModel(client, access);
                await model.LoadAsync(input.RequestId);
                offerResult = new
                {
                    requestId = target.의뢰Id, recommendationRound = target.추천라운드,
                    startsAtUtc = target.추천시작시각, expiresAtUtc = target.추천만료시각,
                    visual, pricing, serverCandidateAvailable = model.ServerCandidateAvailable,
                    requiresConditionsAndAcceptance = !model.CanSubmit, error = model.Lifetime.HasError,
                    message = model.Lifetime.Message, card = projection,
                    nativeBannerProjectionOnly = true, actualNativeBannerRendered = false,
                    payoutScheduledKrw = target.Pricing?.TotalPayoutKrw, payoutIsNotDepositConfirmation = true
                };
                checks.Add(new { name = "currentOfferViewModelRead", passed = !model.Lifetime.HasError && model.Offer?.의뢰Id == input.RequestId });
                checks.Add(new { name = "offerHasPricingEvidence", passed = pricing.HasEvidence });
                await RenderAsync<CargoOfferActionScreen>("offer.html", new() { [nameof(CargoOfferActionScreen.ItemId)] = input.RequestId }, null);
                await RenderAsync<DriverOfferBadge>("offer-badge.html", new() { [nameof(DriverOfferBadge.Visual)] = visual }, null);
                await RenderAsync<DriverOfferPricingSummary>("offer-pricing.html", new() { [nameof(DriverOfferPricingSummary.Pricing)] = target.Pricing }, null);
            }
            else if (input.TransportId is null) checks.Add(new { name = "targetRecommendationPresent", passed = false });
            if (input.TransportId is { } id)
            {
                var detail = await client.TransportAsync(id, CancellationToken.None);
                CargoWorkspacePresentation.RequireTransport(detail, id);
                checks.Add(new { name = "selectedTransportOwner", passed = detail.기사_운송자 == input.DriverId });
                checks.Add(new { name = "selectedTransportRequest", passed = detail.운송번호 == input.RequestId });
                if (detail.기사_운송자 != input.DriverId || detail.개인정보제공보류) throw new InvalidOperationException("ProbeTransportOwnerOrPrivacyMismatch");
                var projection = CargoDriverWorkspaceAdapter.Map(detail, true);
                projections.Insert(0, projection);
                var stages = new List<object>();
                foreach (var action in new[] { "pickup", "dropoff" })
                {
                    using var model = new CargoTransportActionViewModel(client, api, access);
                    await model.LoadAsync(id, action);
                    stages.Add(new { action, model.Title, model.StageAllowsInput, model.CanSubmit, model.CompletionConfirmed, model.CompletionMessage,
                        model.CompletionNextStep, model.SubmissionReadinessMessage, error = model.Lifetime.HasError, model.Lifetime.Message });
                    await RenderAsync<CargoTransportActionScreen>(action + ".html", new() { [nameof(CargoTransportActionScreen.ItemId)] = id, [nameof(CargoTransportActionScreen.ActionKey)] = action }, null);
                }
                transportResult = new { id, requestId = detail.운송번호, status = detail.상태, serverAvailableActions = detail.가능한행동,
                    displayedActions = projection.Actions, card = projection, transportFareKrw = detail.운임,
                    fareIsNotPayoutOrDepositConfirmation = true, detail.운송진행보류, detail.정산보류, detail.다음행동안내, stages };
            }
            var selected = projections.FirstOrDefault()?.Id;
            if (selected is not null)
            {
                var snapshot = new RoleWorkspaceSnapshot(RoleWorkspaceCatalog.CargoDriver, projections, "현재 API 자료의 읽기 전용 표시 검증", selected);
                await RenderAsync<RoleMapWorkspace>("workspace.html", new() { [nameof(RoleMapWorkspace.RoleKey)] = RoleWorkspaceCatalog.CargoDriver, [nameof(RoleMapWorkspace.SelectedId)] = selected }, snapshot);
            }
            checks.Add(new { name = "readonlyMethodsOnly", passed = api.MutationsDenied == 0 });
            var assemblies = new[] { typeof(Program).Assembly, typeof(CargoDriverWorkspaceApiClient).Assembly, typeof(기사운송상세응답).Assembly }.Distinct()
                .Select(a => new { name = a.GetName().Name, path = a.Location, sha256 = Hash(a.Location) }).ToArray();
            var sources = new[]
            {
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceApiClients.cs",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionViewModel.cs",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionScreen.razor",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionScreen.razor",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/DriverOffers/DriverOfferPresentation.cs",
                "DriverApp/NativeDriverHomePage.xaml", "DriverApp/NativeDriverHomePage.xaml.cs"
            }.Select(p => new { path = p, sha256 = Hash(Path.Combine(repository, p)) }).ToArray();
            var passed = errors.Count == 0 && checks.All(check => JsonSerializer.SerializeToElement(check).GetProperty("passed").GetBoolean());
            var result = new { schema = "cargo-ai-client-probe-r114.v1", recordedAtUtc = DateTimeOffset.UtcNow, input.Stage, passed,
                requestId = input.RequestId, input.TransportId, driverId = input.DriverId, recommendationCount = offers.Count, targetRecommendationPresent = target is not null,
                workspace = new { workspace.RuleRevision, activeTransportCount = workspace.활성운송목록.Count, workspace.다음행동 }, offer = offerResult, transport = transportResult,
                checks, errors, files, httpReads = api.Reads, api.MutationsDenied, assemblies, sources,
                evidenceBoundary = new { actualHttpRead = true, currentSharedClient = true, currentViewModels = true, currentSharedRazorSsr = files.Count > 0,
                    nativeBanner = false, notificationSound = false, fcmDelivery = false, gps = false, mapSdk = false, androidApp = false, screenshots = false,
                    locationPostingAdapterInvoked = false, actorMutationsPerformedByThisProbe = false, approvedOperationalUse = false } };
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, Json), new UTF8Encoding(false));
            Console.WriteLine(JsonSerializer.Serialize(new { passed, file = Path.Combine(output, "result.json"), renders = files.Count, errors = errors.Count }));
            return passed ? 0 : 1;

            async Task RenderAsync<TComponent>(string filename, Dictionary<string, object?> parameters, RoleWorkspaceSnapshot? snapshot) where TComponent : IComponent
            {
                try
                {
                    var services = new ServiceCollection();
                    services.AddLogging(b => b.ClearProviders()); services.AddMudServices();
                    services.AddSingleton<IRoleWorkspaceApi>(api); services.AddSingleton(client); services.AddSingleton<IRoleWorkspaceAccess>(access);
                    services.AddSingleton<IJSRuntime, NoScriptRuntime>(); services.AddSingleton<INeighborhoodMapHost, NoMapHost>();
                    var navigation = new ProbeNavigation(input.BaseAddress.TrimEnd('/') + "/"); navigation.Prepare(); services.AddSingleton<NavigationManager>(navigation);
                    if (snapshot is not null)
                    {
                        var state = new RoleWorkspaceState(); state.BindOwner(input.DriverId);
                        state.Save(RoleWorkspaceCatalog.CargoDriver, new(snapshot.SelectedId, true, null, ListOnly: true));
                        services.AddSingleton(new RoleWorkspaceViewModel([new ProjectionAdapter(snapshot)], access, state));
                    }
                    await using var provider = services.BuildServiceProvider();
                    await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
                    var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters))).ToHtmlString());
                    var document = "<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>화물 기사 현재 화면 · 읽기 전용</title><body><p>현재 서버 조회를 제품 공통 컴포넌트로 표시한 정적 검토본입니다. 지도·버튼 조작·알림 소리·Android 실행은 검증하지 않았습니다.</p>" + html + "</body></html>";
                    var path = Path.Combine(output!, filename); await File.WriteAllTextAsync(path, document, new UTF8Encoding(false));
                    files.Add(new { filename, component = typeof(TComponent).FullName, sha256 = Hash(path), actualStaticRender = true });
                }
                catch (Exception ex) { errors.Add(new { scope = filename, code = "ProductComponentSsrFailed", exceptionType = ex.GetType().Name }); }
            }
        }
        catch (Exception ex)
        {
            // Never include HTTP payloads, token-bearing input or exception messages.
            if (output is not null && Directory.Exists(output) && !File.Exists(Path.Combine(output, "result.json")))
                await File.WriteAllTextAsync(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new { passed = false, code = "ProbeFailed", exceptionType = ex.GetType().Name }, Json));
            Console.Error.WriteLine(JsonSerializer.Serialize(new { passed = false, code = "ProbeFailed", exceptionType = ex.GetType().Name })); return 1;
        }
    }
    private static async Task<int> ExportThemeAsync(string requestedPath)
    {
        try
        {
            var allowed = Path.GetFullPath(Path.Combine(FindRepository(), "artifacts", "local", "cargo-ai-device-r114")) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(requestedPath);
            if (!path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || File.Exists(path)) throw new InvalidOperationException("ThemeOutputOutsideBoundaryOrAlreadyExists");
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders()); services.AddMudServices(); services.AddSingleton<IJSRuntime, NoScriptRuntime>();
            await using var provider = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            var markup = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<MudThemeProvider>(ParameterView.Empty)).ToHtmlString());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, markup, new UTF8Encoding(false));
            Console.WriteLine(JsonSerializer.Serialize(new { file = path, sha256 = Hash(path), component = typeof(MudThemeProvider).FullName, actualStaticRender = true, serverOrDatabaseAccess = false }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { passed = false, code = "ThemeRenderFailed", exceptionType = ex.GetType().Name }));
            return 1;
        }
    }
    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Ssalddel.Ui.Common"))) return dir.FullName;
        throw new InvalidOperationException("ProbeRepositoryMissing");
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

internal sealed record ProbeInput
{
    public string BaseAddress { get; init; } = "http://127.0.0.1:5364/";
    public string AccessToken { get; init; } = "";
    public string RequestId { get; init; } = "";
    public long? TransportId { get; init; }
    public string DriverId { get; init; } = "";
    public string OutputRoot { get; init; } = "";
    public string Stage { get; init; } = "current";
}
