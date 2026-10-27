using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CargoAiClientProbeR114;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace CargoOrderCloseoutR70;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--roles") return await RoleCloseoutScreens.RunAsync(args[1]);
        if (args.Length != 2 || args[0] != "--input") { Console.Error.WriteLine("Use --input <private-input-json>"); return 2; }
        string? output = null;
        try
        {
            var repository = FindRepository();
            var artifactRoot = Path.GetFullPath(Path.Combine(repository, "artifacts", "local", "cargo-order-closeout-r70"));
            var inputPath = Path.GetFullPath(args[1]);
            if (!inputPath.StartsWith(artifactRoot + Path.DirectorySeparatorChar + ".private" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || new FileInfo(inputPath).Length > 32768) throw new InvalidOperationException("CloseoutPrivateInputRequired");
            var input = JsonSerializer.Deserialize<ProbeInput>(await File.ReadAllBytesAsync(inputPath), Json) ?? throw new InvalidOperationException("CloseoutInputMissing");
            if (input.BaseAddress.TrimEnd('/') != "http://127.0.0.1:5364" || string.IsNullOrWhiteSpace(input.AccessToken)
                || input.DriverId is not ("cargo-journey-driver" or "cargo-journey-driver-b") || !Guid.TryParseExact(input.RequestId, "D", out _)
                || input.Mode is not ("inspect" or "accept") || input.TransportId is <= 0)
                throw new InvalidOperationException("CloseoutInputOutsideLocalBoundary");
            var requestedOutput = Path.GetFullPath(input.OutputRoot);
            if (!requestedOutput.StartsWith(Path.Combine(artifactRoot, "client") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("CloseoutOutputOutsideLocalBoundary");
            output = requestedOutput;
            input = input with { OutputRoot = output };
            Directory.CreateDirectory(output);
            if (File.Exists(Path.Combine(output, "result.json")) || input.Mode == "accept" && File.Exists(Path.Combine(output, "accept-command.json")))
                throw new InvalidOperationException("CloseoutPriorResultPreserved");

            var checks = new List<Check>(); var errors = new List<object>(); var files = new List<object>(); var snapshots = new Dictionary<string, object?>();
            var access = new ProbeAccess(input.DriverId);
            using var reads = new ReadOnlyProbeApi(input);
            using var api = new AcceptanceApi(input, reads, repository);
            var client = new CargoDriverWorkspaceApiClient(api);
            var workspaceBefore = await client.WorkspaceAsync(CancellationToken.None);
            var offers = await client.RecommendationsAsync(CancellationToken.None);
            var offer = offers.FirstOrDefault(o => o.의뢰Id == input.RequestId);
            var saved = false; long? transportId = input.TransportId;
            decimal? confirmedOfferPayout = null, frozenTransportPayout = null;
            bool? frozenPayoutHasEvidence = null, frozenPayoutMatchesConfirmedOffer = null;
            var preCard = offer is null ? null : CargoDriverWorkspaceAdapter.MapOffer(offer);
            if (offer is not null)
            {
                using var model = new CargoOfferActionViewModel(client, access);
                await model.LoadAsync(input.RequestId);
                var before = OfferState(model);
                snapshots["beforeConfirmation"] = before;
                checks.Add(new("offerLoadedWithoutError", !model.Lifetime.HasError && model.Source?.의뢰Id == input.RequestId));
                checks.Add(new("requiresExplicitConfirmation", !model.CanSubmit));
                checks.Add(new("serverRecommendationStillAvailable", model.ServerCandidateAvailable));
                checks.Add(new("driverPayoutHasEvidence", CargoDriverPayoutPresentation.OfferAmount(model.Offer).HasValue));
                await RenderAsync<CargoOfferActionScreen>("offer-before.html", new() { [nameof(CargoOfferActionScreen.ItemId)] = input.RequestId }, reads, null);
                model.ConditionsConfirmed = true;
                var afterConditions = model.CanSubmit;
                model.AcceptConfirmed = true;
                snapshots["afterConfirmation"] = OfferState(model);
                checks.Add(new("bothConfirmationsRequired", !afterConditions && model.CanSubmit));
                if (input.Mode == "accept")
                {
                    if (!model.CanSubmit) checks.Add(new("acceptanceWasReady", false));
                    else
                    {
                        confirmedOfferPayout = CargoDriverPayoutPresentation.OfferAmount(model.Offer);
                        await model.SubmitAsync();
                        saved = model.Saved;
                        snapshots["afterSubmission"] = OfferState(model);
                        if (long.TryParse(model.TransportId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) transportId = parsed;
                        checks.Add(new("viewModelSavedCanonicalTransport", saved && transportId is > 0 && !model.Lifetime.HasError));
                        checks.Add(new("singleConfirmedAcceptancePost", api.PostAttempts == 1 && api.JournalState == "Confirmed" && api.HttpStatus == 200 && api.ResponseMatchesRequest == true));
                    }
                }
            }
            else if (input.Mode == "accept" || transportId is null) checks.Add(new("targetRecommendationPresent", false));

            RoleWorkspaceItem? currentCard = preCard;
            if (transportId is { } id)
            {
                using var afterReads = new ReadOnlyProbeApi(input with { TransportId = id, Mode = "inspect" });
                var afterClient = new CargoDriverWorkspaceApiClient(afterReads);
                var detail = await afterClient.TransportAsync(id, CancellationToken.None);
                var canonical = await afterClient.WorkspaceAsync(CancellationToken.None);
                checks.Add(new("transportMatchesRequestAndOwner", detail.Id == id && detail.운송번호 == input.RequestId && detail.기사_운송자 == input.DriverId && !detail.개인정보제공보류));
                if (detail.Id != id || detail.운송번호 != input.RequestId || detail.기사_운송자 != input.DriverId || detail.개인정보제공보류)
                    throw new InvalidOperationException("CloseoutCanonicalTransportMismatch");
                if (input.Mode == "accept") checks.Add(new("acceptedRequestInCurrentWorkspace", canonical.활성운송목록.Any(t => t.Id == id && t.운송번호 == input.RequestId)));
                frozenTransportPayout = CargoDriverPayoutPresentation.FrozenAmount(detail);
                frozenPayoutHasEvidence = frozenTransportPayout is > 0m
                    && detail.기사지급예정액근거 == "운송원장.기사지급예정액";
                checks.Add(new("frozenDriverPayoutHasEvidence", frozenPayoutHasEvidence == true));
                if (input.Mode == "accept")
                {
                    frozenPayoutMatchesConfirmedOffer = confirmedOfferPayout is > 0m
                        && frozenPayoutHasEvidence == true && frozenTransportPayout == confirmedOfferPayout;
                    checks.Add(new("acceptedFrozenPayoutMatchesConfirmedOffer", frozenPayoutMatchesConfirmedOffer == true));
                }
                currentCard = CargoDriverWorkspaceAdapter.Map(detail, !CargoWorkspacePresentation.IsClosed(detail.상태));
                snapshots["transport"] = new { id, requestId = detail.운송번호, status = detail.상태, detail.가능한행동, detail.기사지급예정액, detail.기사지급예정액근거,
                    frozenPayout = CargoDriverPayoutPresentation.FrozenAmount(detail), customerFare = detail.운임, card = currentCard,
                    payrollIsNotDepositConfirmation = true, canonical.RuleRevision, canonical.다음행동 };
                foreach (var action in new[] { "pickup", "dropoff" })
                {
                    using var model = new CargoTransportActionViewModel(afterClient, afterReads, access);
                    await model.LoadAsync(id, action);
                    snapshots[action] = new { model.StageAllowsInput, model.CanSubmit, model.CompletionConfirmed, model.SubmissionReadinessMessage, error = model.Lifetime.HasError };
                    checks.Add(new(action + "ScreenReadWithoutError", !model.Lifetime.HasError));
                    await RenderAsync<CargoTransportActionScreen>(action + ".html", new() { [nameof(CargoTransportActionScreen.ItemId)] = id, [nameof(CargoTransportActionScreen.ActionKey)] = action }, afterReads, null);
                }
                if (currentCard is not null) await RenderWorkspaceAsync(currentCard, afterReads);
                snapshots["afterHttpReads"] = afterReads.Reads;
                checks.Add(new("afterReadsHadNoMutation", afterReads.MutationsDenied == 0));
            }
            else if (currentCard is not null) await RenderWorkspaceAsync(currentCard, reads);
            checks.Add(new("readonlyProbeHadNoMutation", reads.MutationsDenied == 0));
            checks.Add(new("inspectDoesNotPost", input.Mode != "inspect" || api.PostAttempts == 0));
            var assemblies = new[] { typeof(Program).Assembly, typeof(CargoOfferActionViewModel).Assembly, typeof(기사운송상세응답).Assembly }.Distinct()
                .Select(a => new { name = a.GetName().Name, path = a.Location, sha256 = Hash(a.Location) }).ToArray();
            var sources = new[]
            {
                "eng/CargoOrderCloseoutR70/Program.cs", "eng/CargoOrderCloseoutR70/AcceptanceApi.cs", "eng/CargoAiClientProbeR114/ReadOnlyProbeApi.cs",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs", "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionScreen.razor",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionViewModel.cs", "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionScreen.razor",
                "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs", "Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoDriverPayoutPresentation.cs",
                "Ssalddel.Contracts/Driver/Recommendation/기사배차추천항목Dtos.cs", "Ssalddel.Contracts/Driver/Recommendation/기사추천시각Policy.cs"
            }.Select(p => new { path = p, sha256 = Hash(Path.Combine(repository, p)) }).ToArray();
            var passed = checks.All(c => c.Passed) && errors.Count == 0 && (input.Mode != "accept" || saved);
            var result = new
            {
                schema = "cargo-order-closeout-client.r70.v1", recordedAtUtc = DateTimeOffset.UtcNow, input.Mode, input.Stage, passed, saved,
                requestId = input.RequestId, transportId, input.DriverId, snapshots, checks, errors, files,
                driverPayoutComparison = new { confirmedOfferPayout, frozenTransportPayout, frozenPayoutHasEvidence,
                    frozenPayoutMatchesConfirmedOffer, comparisonApplies = input.Mode == "accept", actualDepositConfirmed = false },
                httpReads = reads.Reads, postAttempts = api.PostAttempts, commandState = api.JournalState,
                initialWorkspaceRule = workspaceBefore.RuleRevision, initialWorkspaceCount = workspaceBefore.활성운송목록.Count, assemblies, sources,
                evidenceBoundary = new { localSimulationOnly = true, currentSharedClient = true, currentOfferViewModelSubmit = input.Mode == "accept" && api.PostAttempts == 1,
                    currentSharedComponentStaticRender = true, interactiveBrowserClick = false, androidDevice = false, nativeBanner = false,
                    screenshots = false, notificationSound = false, gps = false, actualPhotoCapture = false, operationalDispatch = false, actualPayment = false }
            };
            await WriteNewAsync(Path.Combine(output, "result.json"), result);
            Console.WriteLine(JsonSerializer.Serialize(new { passed, saved, requestId = input.RequestId, transportId, resultFile = Path.Combine(output, "result.json") }));
            return passed ? 0 : 1;

            async Task RenderWorkspaceAsync(RoleWorkspaceItem card, IRoleWorkspaceApi renderApi)
            {
                var snapshot = new RoleWorkspaceSnapshot(RoleWorkspaceCatalog.CargoDriver, [card], SelectedId: card.Id);
                await RenderAsync<RoleMapWorkspace>("workspace.html", new() { [nameof(RoleMapWorkspace.RoleKey)] = RoleWorkspaceCatalog.CargoDriver, [nameof(RoleMapWorkspace.SelectedId)] = card.Id }, renderApi, snapshot);
            }
            async Task RenderAsync<TComponent>(string filename, Dictionary<string, object?> parameters, IRoleWorkspaceApi renderApi, RoleWorkspaceSnapshot? snapshot) where TComponent : IComponent
            {
                try
                {
                    var services = new ServiceCollection(); services.AddLogging(b => b.ClearProviders()); services.AddMudServices();
                    services.AddSingleton<IRoleWorkspaceApi>(renderApi); services.AddSingleton(new CargoDriverWorkspaceApiClient(renderApi)); services.AddSingleton<IRoleWorkspaceAccess>(access);
                    services.AddSingleton<IJSRuntime, NoScriptRuntime>(); services.AddSingleton<INeighborhoodMapHost, NoMapHost>();
                    var navigation = new ProbeNavigation(input.BaseAddress.TrimEnd('/') + "/"); navigation.Prepare(); services.AddSingleton<NavigationManager>(navigation);
                    if (snapshot is not null)
                    {
                        var state = new RoleWorkspaceState(); state.BindOwner(input.DriverId); state.Save(RoleWorkspaceCatalog.CargoDriver, new(snapshot.SelectedId, true, null, ListOnly: true));
                        services.AddSingleton(new RoleWorkspaceViewModel([new ProjectionAdapter(snapshot)], access, state));
                    }
                    await using var provider = services.BuildServiceProvider();
                    await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
                    var markup = await renderer.Dispatcher.InvokeAsync(async () =>
                    {
                        var theme = await renderer.RenderComponentAsync<MudThemeProvider>(ParameterView.Empty);
                        var component = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
                        return theme.ToHtmlString() + component.ToHtmlString();
                    });
                    var html = "<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>퀵 한 건 · 공통 앱 화면 검토</title><body class=\"role-app\"><p>현재 제품 공통 화면의 정적 렌더입니다. 화면 버튼 조작·Android·알림음·지도는 검증하지 않았습니다.</p>" + markup + "</body></html>";
                    await WriteTextNewAsync(Path.Combine(output!, filename), html);
                    files.Add(new { filename, component = typeof(TComponent).FullName, sha256 = Hash(Path.Combine(output!, filename)), actualStaticRender = true, screenshot = false });
                }
                catch (Exception ex) { errors.Add(new { scope = filename, code = "CurrentComponentRenderFailed", exceptionType = ex.GetType().Name }); }
            }
        }
        catch (Exception ex)
        {
            if (output is not null && Directory.Exists(output) && !File.Exists(Path.Combine(output, "failure.json")))
                await WriteNewAsync(Path.Combine(output, "failure.json"), new { passed = false, saved = false, code = "CloseoutProbeFailed", exceptionType = ex.GetType().Name });
            Console.Error.WriteLine(JsonSerializer.Serialize(new { passed = false, saved = false, code = "CloseoutProbeFailed", exceptionType = ex.GetType().Name }));
            return 1;
        }
    }
    private static object OfferState(CargoOfferActionViewModel model) => new
    {
        model.ServerCandidateAvailable, model.CanSubmit, model.ConditionsConfirmed, model.AcceptConfirmed, model.Saved, model.TransportId,
        error = model.Lifetime.HasError, model.Lifetime.Message, payout = CargoDriverPayoutPresentation.OfferAmount(model.Offer),
        payoutSource = model.Offer?.기사지급예정운임근거, offerStartUtc = model.Offer?.추천시작시각, offerExpiresUtc = model.Offer?.추천만료시각,
        offerStartKind = model.Offer?.추천시작시각?.Kind.ToString(), offerExpiryKind = model.Offer?.추천만료시각?.Kind.ToString(),
        sourceVehicleCompatible = model.Source?.차량적합여부, offerVehicleCompatible = model.Offer?.차량적합여부, sourceDispatchStatus = model.Source?.배차상태,
        customerCharge = model.Source?.결제예정금액, payoutIsNotDepositConfirmation = true
    };
    private static async Task WriteNewAsync(string path, object value) => await WriteTextNewAsync(path, JsonSerializer.Serialize(value, Json));
    private static async Task WriteTextNewAsync(string path, string text)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(new UTF8Encoding(false).GetBytes(text));
        await stream.FlushAsync();
    }
    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Ssalddel.Ui.Common"))) return dir.FullName;
        throw new InvalidOperationException("CloseoutRepositoryMissing");
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private sealed record Check(string Name, bool Passed);
}
