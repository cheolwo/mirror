using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelAdmin.Components.Shared;
using AdminDtos = SsalddelAdmin.Services;
using ShipperContract = Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답;

namespace CargoAiClientProbeR114;

/// <summary>격리 R114 서버의 같은 의뢰를 조회하여 기존 역할 화면을 정적으로 렌더합니다. 업무 명령은 보내지 않습니다.</summary>
internal static class RoleCloseoutScreens
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private const string PlanPath = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.design.r70.md";
    private const string PlanHash = "6e5a55c98bbaea9906bdcef0cb1667db6598012ea2c90bec8d98caeef1540491";
    private const string ShipperId = "cargo-journey-shipper";
    private const string AdminId = "cargo-journey-admin";

    public static async Task<int> RunAsync(string inputPath)
    {
        string? output = null;
        var reads = new List<ReadEvidence>();
        var files = new List<object>();
        try
        {
            var repository = FindRepository();
            Require(Hash(Path.Combine(repository, PlanPath)) == PlanHash, "CloseoutPlanChanged");
            var workOrder = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(repository,
                "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.r70.work-order.json")));
            using (workOrder)
            {
                Require(workOrder.RootElement.GetProperty("status").GetString() == "ReadyToDispatch"
                    && workOrder.RootElement.GetProperty("planSha256").GetString() == PlanHash
                    && workOrder.RootElement.GetProperty("writePaths").EnumerateArray()
                        .Any(p => p.GetString() == "eng/CargoOrderCloseoutR70/"), "CloseoutWorkOrderMismatch");
            }
            var artifactRoot = Path.Combine(repository, "artifacts", "local", "cargo-order-closeout-r70");
            var privateRoot = Path.Combine(artifactRoot, ".private");
            var path = Path.GetFullPath(inputPath);
            Require(Under(path, privateRoot) && File.Exists(path) && new FileInfo(path).Length <= 32768,
                "CloseoutRolePrivateInputRequired");
            NoLinkedAncestors(path);
            var input = JsonSerializer.Deserialize<RoleInput>(await File.ReadAllBytesAsync(path), Json)
                ?? throw new BoundaryException("CloseoutRoleInputMissing");
            Require(input.BaseAddress.TrimEnd('/') == "http://127.0.0.1:5364"
                && Guid.TryParseExact(input.RequestId, "D", out _)
                && Regex.IsMatch(input.Stage, "^[a-z0-9][a-z0-9-]{0,63}$")
                && TokenIsFor(input.ShipperAccessToken, ShipperId)
                && TokenIsFor(input.AdminAccessToken, AdminId), "CloseoutRoleInputOutsideBoundary");
            var expectedOutput = Path.GetFullPath(Path.Combine(artifactRoot, "roles", input.Stage));
            Require(Path.GetFullPath(input.OutputRoot).Equals(expectedOutput, StringComparison.OrdinalIgnoreCase)
                && !Directory.Exists(expectedOutput), "CloseoutRoleOutputMustBeFreshStage");
            NoLinkedAncestors(Path.GetDirectoryName(expectedOutput)!);
            Directory.CreateDirectory(expectedOutput);
            output = expectedOutput;

            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5364/"), Timeout = TimeSpan.FromSeconds(30) };
            var requestPath = "api/v1/shipper/requests/" + input.RequestId;
            var ledgerPath = "api/v1/transport-request-ledgers/" + input.RequestId + "/events";
            var eventsPath = "api/v1/admin/transports/events?requestId=" + input.RequestId;
            var paymentsPath = "api/v1/payments?" + Uri.EscapeDataString("의뢰Id") + "=" + input.RequestId;
            var podsPath = "api/v1/admin/files/pod?requestId=" + input.RequestId;
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                requestPath, ledgerPath, eventsPath, paymentsPath, podsPath,
                "api/v1/admin/transports", "api/v1/dispatch/wait"
            };
            var shipper = await GetAsync<ShipperContract>("shipper", requestPath);
            Require(shipper.의뢰Id == input.RequestId && shipper.화주Id == ShipperId
                && shipper.요약?.화물종류.Contains("R114", StringComparison.Ordinal) == true,
                "CloseoutRoleSyntheticRequestRequired");
            // Use the same shared-contract read and product projection as the
            // repaired back-office list/detail/cancel-response paths.
            var adminSource = await GetAsync<ShipperContract>("admin", requestPath);
            var adminRequest = AdminDtos.AdminShipperRequestProjection.ToView(adminSource);
            var allTransports = await GetAsync<List<AdminDtos.운송진행응답>>("admin", "api/v1/admin/transports");
            var transports = allTransports.Where(t => t.운송번호 == input.RequestId).ToArray();
            var events = await GetAsync<List<AdminDtos.운송이벤트로그응답>>("admin", eventsPath);
            var ledger = await GetAsync<운송원장이벤트응답>("admin", ledgerPath);
            var payments = await GetAsync<List<AdminDtos.결제목록응답>>("admin", paymentsPath);
            var allWaiting = await GetAsync<List<AdminDtos.배차대기응답>>("admin", "api/v1/dispatch/wait");
            var waiting = allWaiting.Where(w => w.의뢰Id == input.RequestId).ToArray();
            var pods = await GetAsync<List<AdminDtos.파일POD응답>>("admin", podsPath);
            IReadOnlyList<AdminDtos.기사월정산관리응답> settlements = [];
            var transport = transports.SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(transport?.기사_운송자))
            {
                Require(transport.기사_운송자 is "cargo-journey-driver" or "cargo-journey-driver-b",
                    "CloseoutRoleForeignDriver");
                var settlementPath = "api/v1/admin/driver-settlements?driverId=" + Uri.EscapeDataString(transport.기사_운송자);
                allowed.Add(settlementPath);
                settlements = await GetAsync<List<AdminDtos.기사월정산관리응답>>("admin", settlementPath);
            }
            Require(adminRequest.의뢰Id == input.RequestId && ledger.의뢰Id == input.RequestId
                && events.All(e => e.의뢰Id == input.RequestId)
                && payments.All(p => p.의뢰Id == input.RequestId)
                && pods.All(p => p.RequestId == input.RequestId), "CloseoutRoleCrossRequestDataRejected");
            Require(transports.Length == 1 && transport is not null && ledger.운송Id == transport.Id
                && ledger.Rdb운송실행투영존재 && ledger.Mongo원장존재
                && ledger.Mongo원장Id == "transport:" + input.RequestId, "CloseoutRoleLedgerLinkageMismatch");
            Require(shipper.의뢰상태 == adminRequest.의뢰상태 && shipper.의뢰상태 == ledger.의뢰상태
                && shipper.결제상태 == ledger.결제상태 && shipper.정산상태 == ledger.정산상태
                && transport.상태 == ledger.운송상태
                && (string.IsNullOrEmpty(shipper.운송상태) || shipper.운송상태 == transport.상태),
                "CloseoutRoleCurrentStateMismatch");

            var state = new ShipperRequestDetailPageState
            {
                Request = ShipperRequestDetailSnapshot.FromContract(shipper),
                SourceBoundaryMessage = "같은 합성 의뢰의 현재 API 응답을 표시한 로컬 검토본입니다."
            };
            var shipperParameters = new Dictionary<string, object?>
            {
                [nameof(ShipperRequestSummaryScreen.State)] = state,
                [nameof(ShipperRequestSummaryScreen.Context)] = new ShipperRequestDetailNavigationContext { ReturnPath = "/shipper/home" },
                [nameof(ShipperRequestSummaryScreen.HomeHref)] = "/shipper/home",
                [nameof(ShipperRequestSummaryScreen.NewRequestHref)] = "/shipper/request/cargo"
            };
            await RenderAsync<ShipperRequestSummaryScreen>("shipper-summary.html", shipperParameters);
            await RenderAsync<ShipperRequestTimelineScreen>("shipper-timeline.html", shipperParameters);
            var detail = AdminDtos.운송워크플로우관제상세Factory.Build(input.RequestId, adminRequest, payments,
                waiting, transports, events, pods, settlements)
                ?? throw new BoundaryException("CloseoutAdminFactoryReturnedNull");
            await RenderAsync<AdminTransportWorkflowSummaryCard>("admin-summary.html", new()
            {
                [nameof(AdminTransportWorkflowSummaryCard.Detail)] = detail,
                [nameof(AdminTransportWorkflowSummaryCard.ShowTransportMeta)] = true,
                [nameof(AdminTransportWorkflowSummaryCard.ShowNextAction)] = true
            });
            var result = new
            {
                schemaVersion = "cargo-order-closeout-role-screens.r70.v1", passed = true,
                atUtc = DateTime.UtcNow, input.Stage, input.RequestId, synthetic = true,
                planSha256 = PlanHash, sameRequestVerified = true, transportId = transport.Id,
                mongo = new { id = ledger.Mongo원장Id, exists = ledger.Mongo원장존재, state = ledger.Mongo원장상태,
                    step = ledger.Mongo원장현재단계Key, sqlProjectionExists = ledger.Rdb운송실행투영존재 },
                requestState = shipper.의뢰상태, dispatchState = shipper.배차상태, transportState = transport.상태,
                paymentState = shipper.결제상태, settlementState = shipper.정산상태,
                receiptRecorded = shipper.인수증등록일시.HasValue,
                eventCount = events.Count, legacyPodCount = pods.Count,
                adminFactorySteps = detail.단계목록.Select(s => new { key = s.단계코드, title = s.제목, state = s.상태, done = s.완료됨, attention = s.확인필요 }),
                adminCheckKinds = detail.운영확인목록.Select(c => c.구분),
                evidenceBoundary = "운송 첨부 사진 영수증과 기존 POD 표의 조회·검수는 별개이며 POD를 만들어 채우지 않았습니다.",
                upperOrderLedgerConnected = false,
                upperOrderBoundary = "transport:{requestId}는 운송 전문원장입니다. 일반 퀵 의뢰의 상위 주문 원장 자동 연결을 검증하거나 생성하지 않았습니다.",
                httpReads = reads, files, mutationsSent = 0,
                actualProductComponentsRendered = true, staticSsrOnly = true,
                adminProductProjectionUsed = typeof(AdminDtos.AdminShipperRequestProjection).FullName,
                adminBoundary = "관리자 GET은 제품 백오피스와 같은 공유 API 계약 및 제품 투영을 사용합니다. Factory/카드 정적 렌더와 실제 관리자 브라우저 조작은 별개입니다.",
                androidVerified = false, actualWindowsAppVerified = false, realPaymentVerified = false,
                physicalCameraVerified = false, externalDeployment = false,
                assemblies = new[] { typeof(RoleCloseoutScreens).Assembly, typeof(ShipperRequestSummaryScreen).Assembly,
                    typeof(AdminTransportWorkflowSummaryCard).Assembly }.Distinct()
                    .Select(a => new { name = a.GetName().Name, sha256 = Hash(a.Location) })
            };
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, Json), new UTF8Encoding(false));
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, stage = input.Stage, requestId = input.RequestId,
                output = Path.GetRelativePath(repository, output), httpReads = reads.Count, renderedFiles = files.Count, mutationsSent = 0 }));
            return 0;

            async Task<T> GetAsync<T>(string role, string relativePath)
            {
                Require(allowed.Contains(relativePath) && (role is "shipper" or "admin")
                    && (role != "shipper" || relativePath == requestPath), "CloseoutRoleGetNotAllowed");
                using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", role == "shipper" ? input.ShipperAccessToken : input.AdminAccessToken);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await BoundedReadAsync(response.Content);
                reads.Add(new(role, "GET", relativePath, (int)response.StatusCode, bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
                Require(response.StatusCode == HttpStatusCode.OK, "CloseoutRoleGetFailed");
                return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new BoundaryException("CloseoutRoleEmptyJson");
            }

            async Task RenderAsync<T>(string filename, Dictionary<string, object?> parameters) where T : IComponent
            {
                var services = new ServiceCollection();
                services.AddLogging(b => b.ClearProviders()); services.AddMudServices();
                services.AddSingleton<IJSRuntime, NoScriptRuntime>();
                var navigation = new ProbeNavigation(input.BaseAddress.TrimEnd('/') + "/"); navigation.Prepare();
                services.AddSingleton<NavigationManager>(navigation);
                await using var provider = services.BuildServiceProvider();
                await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
                var markup = await renderer.Dispatcher.InvokeAsync(async () =>
                {
                    await renderer.RenderComponentAsync<RoleAppProviders>();
                    return (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString();
                });
                Require(markup.Contains(input.RequestId, StringComparison.Ordinal), "CloseoutRoleRenderedRequestMissing");
                var document = "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>같은 퀵 의뢰 · 역할별 읽기 검토</title></head><body><p>격리 서버의 합성 의뢰를 기존 제품 컴포넌트로 표시한 정적 검토본입니다. 버튼 조작·기기·지도·실제 결제는 검증하지 않았습니다.</p>" + markup + "</body></html>";
                var htmlPath = Path.Combine(output, filename);
                await File.WriteAllTextAsync(htmlPath, document, new UTF8Encoding(false));
                files.Add(new { filename, component = typeof(T).FullName, sha256 = Hash(htmlPath), actualStaticRender = true });
            }
        }
        catch (Exception ex)
        {
            var failure = new { schemaVersion = "cargo-order-closeout-role-screens.r70.v1", passed = false,
                code = ex is BoundaryException boundary ? boundary.Code : "CloseoutRoleProbeFailed",
                exceptionType = ex.GetType().Name, jsonPath = ex is JsonException json ? SafeJsonPath(json.Path) : null,
                httpReads = reads, files, mutationsSent = 0 };
            if (output is not null && Directory.Exists(output) && !File.Exists(Path.Combine(output, "result.json")))
                await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(failure, Json), new UTF8Encoding(false));
            Console.Error.WriteLine(JsonSerializer.Serialize(new { failure.passed, failure.code, failure.exceptionType, failure.jsonPath }));
            return 1;
        }
    }

    private static string? SafeJsonPath(string? path)
        => path is { Length: <= 256 } && Regex.IsMatch(path, @"^\$((\.[\p{L}\p{N}_-]+)|(\[\d+\])){0,12}$") ? path : null;

    private static async Task<byte[]> BoundedReadAsync(HttpContent content)
    {
        const int maximum = 8 * 1024 * 1024;
        Require(content.Headers.ContentLength is not > maximum, "CloseoutRoleResponseTooLarge");
        await using var stream = await content.ReadAsStreamAsync();
        using var output = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer); if (read == 0) break;
            Require(output.Length + read <= maximum, "CloseoutRoleResponseTooLarge"); output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
    private static bool TokenIsFor(string token, string expectedId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16384) return false;
        try
        {
            var segments = token.Split('.'); if (segments.Length != 3) return false;
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
            // 이 검사는 합성 fixture 범위 확인용이며 JWT 서명 검증·권한 부여는 실제 서버가 수행합니다.
            return json.RootElement.EnumerateObject().Any(p => (p.Name is "sub" or "nameid"
                or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == expectedId);
        }
        catch { return false; }
    }
    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Ssalddel.Ui.Common"))) return dir.FullName;
        throw new BoundaryException("CloseoutRoleRepositoryMissing");
    }
    private static bool Under(string path, string root) => path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
        + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static void NoLinkedAncestors(string path)
    {
        FileSystemInfo? current = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
        while (current is not null)
        {
            if (current.Exists) Require((current.Attributes & FileAttributes.ReparsePoint) == 0, "CloseoutRoleLinkedPathDenied");
            current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent;
        }
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Require([DoesNotReturnIf(false)] bool passed, string code) { if (!passed) throw new BoundaryException(code); }
    private sealed class BoundaryException(string code) : Exception { public string Code { get; } = code; }
    private sealed record ReadEvidence(string Role, string Method, string Path, int Status, long Bytes, string Sha256);
    private sealed record RoleInput
    {
        public string BaseAddress { get; init; } = "http://127.0.0.1:5364/";
        public string RequestId { get; init; } = "";
        public string ShipperAccessToken { get; init; } = "";
        public string AdminAccessToken { get; init; } = "";
        public string OutputRoot { get; init; } = "";
        public string Stage { get; init; } = "";
    }
}
