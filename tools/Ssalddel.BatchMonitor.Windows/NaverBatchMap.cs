using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Ssalddel.BatchMonitor.Windows;

internal sealed class NaverBatchMap(Grid surface, Action<bool, string> showStatus,
    Action<string, string>? managementInput = null) : IDisposable
{
    private static readonly Uri PageUri = new("http://localhost:8792/");
    private const string ScriptUrl = "https://oapi.map.naver.com/openapi/v3/maps.js";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private readonly HttpListener listener = new();
    private readonly WebView2 view = new();
    private bool disposed;

    public bool IsReady { get; private set; }

    public async Task ConnectAsync(string clientId)
    {
        try
        {
            var html = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "NaverMap", "index.html"));
            if (disposed)
            {
                return;
            }

            var config = JsonSerializer.SerializeToUtf8Bytes(new { clientId, scriptUrl = ScriptUrl }, WebJson);
            listener.Prefixes.Add(PageUri.AbsoluteUri);
            listener.Start();
            _ = Task.Run(() => ServeAsync(html, config));

            surface.Children.Add(view);
            await view.EnsureCoreWebView2Async();
            if (disposed)
            {
                return;
            }

            var browser = view.CoreWebView2;
            browser.Settings.IsStatusBarEnabled = false;
            browser.NavigationStarting += (_, args) => args.Cancel = args.Uri != PageUri.AbsoluteUri;
            browser.NewWindowRequested += (_, args) => args.Handled = true;
            browser.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    ShowFallback("네이버 지도 페이지를 열지 못했습니다.");
                }
            };
            browser.ProcessFailed += (_, _) => ShowFallback("지도 표시가 중단되었습니다.");
            browser.WebMessageReceived += OnMapMessage;
            view.Source = PageUri;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            HttpListenerException or InvalidOperationException or COMException or WebView2RuntimeNotFoundException)
        {
            if (!disposed)
            {
                ShowFallback("네이버 지도 창을 시작하지 못했습니다.");
            }
        }
    }

    public void Update(BatchSimulation simulation) => Send(NaverRouteSample.Read(simulation));

    public void Update(DeliveryMapSnapshot snapshot) => Send(snapshot);

    private void Send<T>(T snapshot)
    {
        if (IsReady)
        {
            view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(snapshot, WebJson));
        }
    }

    private void OnMapMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (disposed || args.Source != PageUri.AbsoluteUri)
        {
            return;
        }

        using var document = JsonDocument.Parse(args.WebMessageAsJson);
        var message = document.RootElement;
        if (message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            return;
        }

        switch (type.GetString())
        {
            case "ready":
                IsReady = true;
                showStatus(true, "실제 지도 배경 · 가상 주문과 이동");
                break;
            case "error":
                var offline = message.TryGetProperty("code", out var code) &&
                    code.ValueKind == JsonValueKind.String && code.GetString() == "offline";
                ShowFallback(offline ? "네이버 지도 연결이 끊겼습니다." : "네이버 지도 인증 또는 연결을 확인해 주세요.");
                break;
            case "managementDriver":
            case "managementStore":
            case "managementExpansion":
                if (IsReady && message.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                    id.GetString() is { Length: > 0 and <= 100 } target)
                {
                    managementInput?.Invoke(type.GetString()!, target);
                }
                break;
        }
    }

    private async Task ServeAsync(byte[] html, byte[] config)
    {
        while (listener.IsListening)
        {
            HttpListenerContext request;
            try
            {
                request = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var response = request.Response;
            try
            {
                response.Headers["Cache-Control"] = "no-store";
                if (request.Request.RemoteEndPoint is not { Address: var address } ||
                    !IPAddress.IsLoopback(address) || request.Request.HttpMethod != "GET")
                {
                    response.StatusCode = 403;
                    continue;
                }

                var path = request.Request.Url?.AbsolutePath;
                var body = path switch { "/" => html, "/map-config" => config, _ => null };
                if (body is null)
                {
                    response.StatusCode = 404;
                    continue;
                }

                response.ContentType = path == "/" ? "text/html; charset=utf-8" : "application/json; charset=utf-8";
                response.ContentLength64 = body.Length;
                await response.OutputStream.WriteAsync(body);
            }
            catch (Exception exception) when (exception is IOException or HttpListenerException or ObjectDisposedException)
            {
                // The embedded browser may close a request during navigation or shutdown.
            }
        }
    }

    private void ShowFallback(string reason)
    {
        if (disposed)
        {
            return;
        }

        IsReady = false;
        view.Visibility = Visibility.Collapsed;
        showStatus(false, reason);
    }

    internal static string? FindClientId()
    {
        var configured = Environment.GetEnvironmentVariable("SSALDDEL_BATCH_MONITOR_NAVER_CLIENT_ID")?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            return IsBrowserId(configured) ? configured : null;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, "Ssalddel", "appsettings.Local.json");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(file);
                using var json = JsonDocument.Parse(stream, new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var id = json.RootElement.GetProperty("NaverMaps").GetProperty("ClientId").GetString()?.Trim();
                return id is not null && IsBrowserId(id) ? id : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsBrowserId(string value) =>
        value.Length is >= 1 and <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public void Dispose()
    {
        disposed = true;
        IsReady = false;
        listener.Close();
        view.Dispose();
    }
}
