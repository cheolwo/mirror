using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Web.WebView2.Core;
using RestaurantDeskApp.Services;

namespace RestaurantDeskApp.Platforms.Windows;

/// <summary>
/// 전표 전용 숨긴 WebView2에서 OS 기본프린터로 한 작업만 보냅니다.
/// 성공은 WebView2 출력 요청 결과이며 종이 출력·절단 완료나 자동 재출력의 근거가 아닙니다.
/// </summary>
public sealed class WindowsRestaurantReceiptPrinter : IRestaurantReceiptPrinter
{
    private readonly SemaphoreSlim queue = new(1, 1);
    private static readonly TimeSpan QueueTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan DocumentTimeout = TimeSpan.FromSeconds(20);
    private const string Uncertain = "출력 결과를 확인하지 못했습니다. 이미 장치로 전달됐을 수 있으니 출력 장치를 확인한 뒤 필요하면 전표 출력으로 다시 요청해 주세요.";
    private const string NotPrepared = "전표 인쇄를 준비하지 못했습니다. 출력 장치를 확인한 뒤 필요하면 전표 출력으로 다시 요청해 주세요.";

    // 기존 native 분기와 호출 이름은 Android/브라우저 소비자의 호환 경계다.
    public bool UsesNativeDialog => true;
    public bool PrintsWithoutDialog => true;

    public async Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(html) || Encoding.UTF8.GetByteCount(html) > 2_000_000)
            throw new ReceiptPrintException(NotPrepared);
        if (!await queue.WaitAsync(QueueTimeout, cancellationToken).ConfigureAwait(false))
            throw new ReceiptPrintException("이전 출력 요청의 정리를 기다리고 있습니다. 출력 장치를 확인한 뒤 필요하면 전표 출력으로 다시 요청해 주세요.");

        var request = new PrintRequest(cancellationToken);
        Task? worker = null;
        try
        {
            worker = MainThread.InvokeOnMainThreadAsync(() => PrintOnUiThreadAsync(html, request));
            await worker.WaitAsync(RequestTimeout, request.Stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.Stop.IsCancellationRequested)
        {
            if (request.WasDispatched) throw new ReceiptPrintException(Uncertain);
            throw;
        }
        catch (TimeoutException)
        {
            request.Stop.Cancel();
            throw new ReceiptPrintException(request.WasDispatched ? Uncertain : NotPrepared);
        }
        catch (ReceiptPrintException) { throw; }
        catch
        {
            request.Stop.Cancel();
            throw new ReceiptPrintException(request.WasDispatched ? Uncertain : NotPrepared);
        }
        finally
        {
            if (worker is null || worker.IsCompleted)
            {
                if (worker?.IsFaulted == true) _ = worker.Exception;
                request.Stop.Dispose();
                queue.Release();
            }
            else
            {
                // WinRT 생성/출력에는 취소 후에도 늦은 완료가 올 수 있다. 원 작업의 Close까지
                // 큐를 유지하여 다른 작업을 겹치거나 미확정 작업을 자동 재시도하지 않는다.
                _ = ReleaseAfterCleanupAsync(worker, request);
            }
        }
    }

    private async Task ReleaseAfterCleanupAsync(Task worker, PrintRequest request)
    {
        try { await worker.ConfigureAwait(false); }
        catch { /* 원 호출에는 이미 안전한 실패/미확정 결과를 전달했다. HTML이나 오류 원문을 로그로 남기지 않는다. */ }
        finally { request.Stop.Dispose(); queue.Release(); }
    }

    private static async Task PrintOnUiThreadAsync(string html, PrintRequest request)
    {
        request.Stop.Token.ThrowIfCancellationRequested();
        var owner = global::Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault(window => window.Handler?.PlatformView is global::Microsoft.UI.Xaml.Window)
            ?? throw new ReceiptPrintException(NotPrepared);
        var nativeWindow = (global::Microsoft.UI.Xaml.Window)owner.Handler!.PlatformView!;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        if (handle == IntPtr.Zero) throw new ReceiptPrintException(NotPrepared);

        CoreWebView2Controller? controller = null;
        CoreWebView2? core = null;
        var finished = false;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var marker = Guid.NewGuid().ToString("N");
        owner.Destroying += OnOwnerDestroying;
        try
        {
            // 별도 HTML 파일/고객별 경로를 만들지 않는다. 쓰기 가능한 앱 전용 위치와
            // InPrivate controller로 일반 앱의 쿠키·인증·방문 기록과 분리한다.
            var profileFolder = Path.Combine(FileSystem.AppDataDirectory, "ReceiptPrinting", "WebView2");
            Directory.CreateDirectory(profileFolder);
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, profileFolder, null);
            request.Stop.Token.ThrowIfCancellationRequested();
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = "ReceiptPrinting";
            options.IsInPrivateModeEnabled = true;
            var parent = CoreWebView2ControllerWindowReference.CreateFromWindowHandle(unchecked((ulong)handle.ToInt64()));
            controller = await environment.CreateCoreWebView2ControllerAsync(parent, options);
            request.Stop.Token.ThrowIfCancellationRequested();
            controller.IsVisible = false;
            controller.Bounds = new global::Windows.Foundation.Rect(-10000, -10000, 800, 1000);
            core = controller.CoreWebView2;
            core.Settings.IsScriptEnabled = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.NavigationStarting += OnNavigationStarting;
            core.FrameNavigationStarting += OnFrameNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.NewWindowRequested += OnNewWindowRequested;
            core.DownloadStarting += OnDownloadStarting;
            core.PermissionRequested += OnPermissionRequested;
            core.WebResourceRequested += OnWebResourceRequested;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.NavigateToString(PrepareHtml(html, marker));
            await ready.Task.WaitAsync(DocumentTimeout, request.Stop.Token);
            request.Stop.Token.ThrowIfCancellationRequested();

            // 부수는 한 번으로 고정하고 프린터/용지/방향/여백의 OS 설정은 변경하지 않는다.
            // 조리/포장 두 장분은 이미 전표 HTML 안에 구성되어 있다.
            var settings = environment.CreatePrintSettings();
            settings.Copies = 1;
            settings.PrinterName = string.Empty;
            request.Stop.Token.ThrowIfCancellationRequested();
            Volatile.Write(ref request.Dispatched, 1);
            // 이 native 작업을 ct로 중간에 버리면 실제 전송 여부를 잃는다. 호출자 타임아웃과
            // 별도로 완료를 관찰한 뒤 controller를 닫고 단일 큐를 해제한다.
            var status = await core.PrintAsync(settings);
            request.Stop.Token.ThrowIfCancellationRequested();
            switch (status)
            {
                case CoreWebView2PrintStatus.Succeeded: return;
                case CoreWebView2PrintStatus.PrinterUnavailable:
                    throw new ReceiptPrintException("기본 출력 장치를 사용할 수 없습니다. 출력 장치를 확인한 뒤 필요하면 전표 출력으로 다시 요청해 주세요.");
                case CoreWebView2PrintStatus.OtherError:
                    throw new ReceiptPrintException("출력 요청을 처리하지 못했습니다. 일부 출력 여부를 장치에서 확인한 뒤 필요하면 전표 출력으로 다시 요청해 주세요.");
                default: throw new ReceiptPrintException(Uncertain);
            }
        }
        catch (OperationCanceledException) when (request.Stop.IsCancellationRequested) { throw; }
        catch (ReceiptPrintException) { throw; }
        catch { throw new ReceiptPrintException(request.WasDispatched ? Uncertain : NotPrepared); }
        finally
        {
            finished = true;
            owner.Destroying -= OnOwnerDestroying;
            try
            {
                if (core is not null)
                {
                    core.NavigationStarting -= OnNavigationStarting;
                    core.FrameNavigationStarting -= OnFrameNavigationStarting;
                    core.NavigationCompleted -= OnNavigationCompleted;
                    core.NewWindowRequested -= OnNewWindowRequested;
                    core.DownloadStarting -= OnDownloadStarting;
                    core.PermissionRequested -= OnPermissionRequested;
                    core.WebResourceRequested -= OnWebResourceRequested;
                }
            }
            catch { /* 이미 끝난 runtime에서도 Close 정리는 계속한다. */ }
            finally
            {
                try { controller?.Close(); }
                catch { /* 창 종료/런타임 종료 뒤 재출력하거나 다른 UI로 이동하지 않는다. */ }
            }
        }

        void OnOwnerDestroying(object? sender, EventArgs args) { if (!finished) request.Stop.Cancel(); }
        void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        { if (!string.Equals(args.Uri, "about:blank", StringComparison.Ordinal)) args.Cancel = true; }
        void OnFrameNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args) => args.Cancel = true;
        void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;
        void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args) => args.Cancel = true;
        void OnPermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
        { args.State = CoreWebView2PermissionState.Deny; args.SavesInProfile = false; }
        void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var uri = args.Request.Uri;
            if (string.Equals(uri, "about:blank", StringComparison.Ordinal)
                || uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                || uri.StartsWith("data:font/", StringComparison.OrdinalIgnoreCase)) return;
            args.Response = sender.Environment.CreateWebResourceResponse(null, 403, "Blocked", "Cache-Control: no-store\r\nContent-Type: text/plain\r\n");
        }
        async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (finished || !args.IsSuccess) return;
            try
            {
                // 초기 about:blank 완료를 전표 로드로 오인하지 않는다. 고정 표식만 읽고 본문은 추출하지 않는다.
                var value = await sender.ExecuteScriptAsync("document.querySelector('meta[name=\"ssalddel-receipt-print\"]')?.getAttribute('content') ?? null");
                if (!finished && JsonSerializer.Deserialize<string?>(value) == marker) ready.TrySetResult();
            }
            catch { if (!finished) ready.TrySetException(new ReceiptPrintException(NotPrepared)); }
        }
    }

    private static string PrepareHtml(string html, string marker)
    {
        var head = Regex.Match(html, "<head(?:\\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!head.Success) throw new ReceiptPrintException(NotPrepared);
        var policy = "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:; font-src data:; script-src 'none'; connect-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'\">";
        return html.Insert(head.Index + head.Length, policy + "<meta name=\"ssalddel-receipt-print\" content=\"" + marker + "\">");
    }

    private sealed class PrintRequest(CancellationToken token)
    {
        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
        public int Dispatched;
        public bool WasDispatched => Volatile.Read(ref Dispatched) != 0;
    }

    private sealed class ReceiptPrintException(string message) : InvalidOperationException(message) { }
}
