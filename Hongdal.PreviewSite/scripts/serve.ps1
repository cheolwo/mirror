param(
    [int]$Port = 5217
)

$ErrorActionPreference = "Stop"
$clientRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\dist\client")).Path
$indexPath = Join-Path $clientRoot "index.html"
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()

$contentTypes = @{
    ".html" = "text/html; charset=utf-8"
    ".css" = "text/css; charset=utf-8"
    ".js" = "text/javascript; charset=utf-8"
    ".json" = "application/json; charset=utf-8"
    ".png" = "image/png"
    ".jpg" = "image/jpeg"
    ".jpeg" = "image/jpeg"
    ".webp" = "image/webp"
    ".ico" = "image/x-icon"
}

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $requestPath = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath).TrimStart("/")
        $candidatePath = if ([string]::IsNullOrWhiteSpace($requestPath)) {
            $indexPath
        }
        else {
            [IO.Path]::GetFullPath((Join-Path $clientRoot $requestPath))
        }

        if (-not $candidatePath.StartsWith($clientRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $context.Response.StatusCode = 400
            $context.Response.Close()
            continue
        }

        $filePath = if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            $candidatePath
        }
        else {
            $indexPath
        }

        $bytes = [IO.File]::ReadAllBytes($filePath)
        $extension = [IO.Path]::GetExtension($filePath).ToLowerInvariant()
        $context.Response.StatusCode = 200
        $context.Response.ContentType = if ($contentTypes.ContainsKey($extension)) {
            $contentTypes[$extension]
        }
        else {
            "application/octet-stream"
        }
        $context.Response.ContentLength64 = $bytes.Length
        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $context.Response.OutputStream.Close()
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}

