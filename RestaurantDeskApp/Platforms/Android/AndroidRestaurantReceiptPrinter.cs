using Android.Print;
using Android.Webkit;
using RestaurantDeskApp.Services;
using WebView = global::Android.Webkit.WebView;

namespace RestaurantDeskApp.Platforms.Android;

// 요청 결과는 Android 인쇄 대화상자의 진입이며 물리 프린터 완료 증거가 아니다.
public sealed class AndroidRestaurantReceiptPrinter : IRestaurantReceiptPrinter
{
    private const string DiagnosticTag = "SSALDDEL-RECEIPT";
    private readonly List<(PrintJob Job, WebView View, PrintDocumentAdapter Adapter)> _requests = [];
    public bool UsesNativeDialog => true;

    public Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var phase = "native-start";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                phase = "activity";
                var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("인쇄 화면을 열 수 없습니다.");
                phase = "print-service";
                var manager = activity.GetSystemService(global::Android.Content.Context.PrintService) as PrintManager
                    ?? throw new InvalidOperationException("이 기기에서 인쇄 서비스를 사용할 수 없습니다.");
                phase = "release-terminal";
                LogDiagnostic(phase);
                for (var index = _requests.Count - 1; index >= 0; index--)
                {
                    var request = _requests[index];
                    if (!request.Job.IsCompleted && !request.Job.IsCancelled && !request.Job.IsFailed) continue;
                    // Java peer를 해제하기 전에 index로 제거해 disposed 객체의 Equals 호출을 피한다.
                    _requests.RemoveAt(index);
                    request.View.Destroy();
                    request.View.Dispose();
                    request.Adapter.Dispose();
                }
                phase = "request-capacity";
                if (_requests.Count >= 4) throw new InvalidOperationException("열린 인쇄 요청을 완료하거나 취소한 뒤 다시 시도해 주세요.");
                phase = "create-webview";
                var view = new WebView(activity);
                try
                {
                    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    view.SetWebViewClient(new ReceiptPageClient(ready));
                    phase = "load-page";
                    LogDiagnostic(phase);
                    view.LoadDataWithBaseURL(null, html, "text/html", "UTF-8", null);
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    phase = "create-adapter";
                    LogDiagnostic(phase);
                    var adapter = view.CreatePrintDocumentAdapter(title);
                    phase = "request-dialog";
                    LogDiagnostic(phase);
                    var job = manager.Print(title, adapter, new PrintAttributes.Builder().Build())
                        ?? throw new InvalidOperationException("인쇄 요청을 시작하지 못했습니다.");
                    _requests.Add((job, view, adapter));
                    LogDiagnostic("dialog-requested");
                }
                catch (Exception ex)
                {
                    LogDiagnosticFailure(phase, ex);
                    phase = "release-failed-webview";
                    view.Destroy();
                    view.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                LogDiagnosticFailure(phase, ex);
                throw;
            }
        });

    // 전표 HTML/제목/주문번호/주소나 예외 메시지를 기록하지 않는다.
    // 프레임은 코드의 형식과 메서드명만 기록하며 파일 경로와 입력값은 제외한다.
    internal static void LogDiagnosticFailure(string phase, Exception exception)
    {
        try
        {
            var types = new List<string>();
            for (Exception? current = exception; current is not null && types.Count < 4; current = current.InnerException)
                types.Add(current.GetType().FullName ?? current.GetType().Name);
            var frames = new System.Diagnostics.StackTrace(exception, false).GetFrames() ?? [];
            var methods = frames.Take(12).Select(frame => frame.GetMethod())
                .Where(method => method is not null)
                .Select(method => $"{method!.DeclaringType?.FullName}.{method.Name}");
            global::Android.Util.Log.Error(DiagnosticTag, $"phase={phase}; types={string.Join(" -> ", types)}; methods={string.Join(" -> ", methods)}");
        }
        catch
        {
            // 진단 기록 실패가 인쇄 요청의 원래 결과를 바꾸지 않도록 한다.
        }
    }

    private static void LogDiagnostic(string phase)
    {
        try { global::Android.Util.Log.Info(DiagnosticTag, $"phase={phase}"); }
        catch { }
    }

    private sealed class ReceiptPageClient(TaskCompletionSource ready) : WebViewClient
    {
        public override void OnPageFinished(WebView? view, string? url)
        {
            LogDiagnostic("page-finished");
            ready.TrySetResult();
        }
    }
}
