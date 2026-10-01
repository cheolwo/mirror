$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$chrome = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chrome)) {
    throw "Chrome executable was not found: $chrome"
}

$captureRoot = Join-Path $repoRoot "docs\ProjectOverview\assets\app-pages"
$minimumCaptureHeight = 1800

function Wait-HttpReady {
    param(
        [Parameter(Mandatory = $true)][int] $Port
    )

    $deadline = (Get-Date).AddSeconds(40)
    do {
        $client = $null
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $task = $client.ConnectAsync("127.0.0.1", $Port)
            if ($task.Wait(1000) -and $client.Connected) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
        finally {
            if ($client) {
                $client.Dispose()
            }
        }
    } while ((Get-Date) -lt $deadline)

    throw "Capture host on port $Port did not become ready."
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return [int]$listener.LocalEndpoint.Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-DevToolsReady {
    param(
        [Parameter(Mandatory = $true)][int] $Port
    )

    $deadline = (Get-Date).AddSeconds(20)
    do {
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/version" -TimeoutSec 1 | Out-Null
            return
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    } while ((Get-Date) -lt $deadline)

    throw "Chrome DevTools on port $Port did not become ready."
}

function Receive-CdpMessage {
    param(
        [Parameter(Mandatory = $true)][System.Net.WebSockets.ClientWebSocket] $Socket
    )

    $buffer = New-Object byte[] 65536
    $stream = [System.IO.MemoryStream]::new()

    try {
        do {
            $segment = [System.ArraySegment[byte]]::new($buffer)
            $result = $Socket.ReceiveAsync($segment, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

            if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                throw "Chrome DevTools websocket closed unexpectedly."
            }

            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)

        $json = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
        return $json | ConvertFrom-Json
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-CdpCommand {
    param(
        [Parameter(Mandatory = $true)][System.Net.WebSockets.ClientWebSocket] $Socket,
        [Parameter(Mandatory = $true)][ref] $CommandId,
        [Parameter(Mandatory = $true)][string] $Method,
        [hashtable] $Params
    )

    $CommandId.Value++
    $id = $CommandId.Value
    $message = @{
        id = $id
        method = $Method
    }

    if ($Params) {
        $message.params = $Params
    }

    $json = $message | ConvertTo-Json -Depth 20 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $Socket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null

    while ($true) {
        $response = Receive-CdpMessage -Socket $Socket
        if ($response.id -ne $id) {
            continue
        }

        if ($response.error) {
            throw "Chrome DevTools command failed: $Method - $($response.error.message)"
        }

        return $response.result
    }
}

function Wait-CdpPageReady {
    param(
        [Parameter(Mandatory = $true)][System.Net.WebSockets.ClientWebSocket] $Socket,
        [Parameter(Mandatory = $true)][ref] $CommandId
    )

    $deadline = (Get-Date).AddSeconds(12)
    do {
        $readyState = Invoke-CdpCommand -Socket $Socket -CommandId $CommandId -Method "Runtime.evaluate" -Params @{
            expression = "document.readyState"
            returnByValue = $true
        }

        if ($readyState.result.value -eq "complete") {
            break
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    Invoke-CdpCommand -Socket $Socket -CommandId $CommandId -Method "Runtime.evaluate" -Params @{
        expression = "new Promise(resolve => { if (document.fonts && document.fonts.ready) { document.fonts.ready.then(() => resolve(true)); } else { resolve(true); } })"
        awaitPromise = $true
        returnByValue = $true
    } | Out-Null

    Start-Sleep -Milliseconds 750
}

function Resolve-CaptureDimensions {
    param(
        [Parameter(Mandatory = $true)][object] $Metrics,
        [int] $DocumentHeight = 0
    )

    $contentSize = $Metrics.cssContentSize
    if (-not $contentSize) {
        $contentSize = $Metrics.contentSize
    }

    $width = [Math]::Max(1440, [Math]::Ceiling([double]$contentSize.width))
    $contentHeight = [Math]::Ceiling([double]$contentSize.height)
    $height = [Math]::Max($minimumCaptureHeight, [Math]::Max($contentHeight, $DocumentHeight))

    [PSCustomObject]@{
        Width = [int]$width
        Height = [int]$height
    }
}

function Get-CdpDocumentHeight {
    param(
        [Parameter(Mandatory = $true)][System.Net.WebSockets.ClientWebSocket] $Socket,
        [Parameter(Mandatory = $true)][ref] $CommandId
    )

    $expression = @"
(() => {
  const values = [
    document.documentElement?.scrollHeight || 0,
    document.body?.scrollHeight || 0,
    document.documentElement?.offsetHeight || 0,
    document.body?.offsetHeight || 0
  ];

  for (const el of document.querySelectorAll('*')) {
    const rect = el.getBoundingClientRect();
    if (Number.isFinite(rect.top)) {
      values.push(rect.bottom);
      values.push(rect.top + el.scrollHeight);
    }
  }

  return Math.ceil(Math.max(...values.filter(value => Number.isFinite(value))));
})()
"@

    $result = Invoke-CdpCommand -Socket $Socket -CommandId $CommandId -Method "Runtime.evaluate" -Params @{
        expression = $expression
        returnByValue = $true
    }

    if ($result.result.value) {
        return [int][Math]::Ceiling([double]$result.result.value)
    }

    return 0
}

function Invoke-PageCapture {
    param(
        [Parameter(Mandatory = $true)][string] $AppName,
        [Parameter(Mandatory = $true)][string] $PageId,
        [Parameter(Mandatory = $true)][string] $Url
    )

    $appDir = Join-Path $captureRoot $AppName
    New-Item -ItemType Directory -Force -Path $appDir | Out-Null
    $output = Join-Path $appDir "$PageId.png"

    $debugPort = Get-FreeTcpPort
    $userDataDir = Join-Path ([System.IO.Path]::GetTempPath()) "hongdal-capture-$([Guid]::NewGuid().ToString("N"))"
    $chromeProcess = $null
    $socket = $null

    try {
        $chromeProcess = Start-Process -FilePath $chrome `
            -ArgumentList @(
                "--headless=new",
                "--disable-gpu",
                "--hide-scrollbars",
                "--no-first-run",
                "--no-default-browser-check",
                "--window-size=1440,1600",
                "--remote-debugging-port=$debugPort",
                "--user-data-dir=$userDataDir",
                "about:blank"
            ) `
            -WindowStyle Hidden `
            -PassThru

        Wait-DevToolsReady -Port $debugPort

        $target = Invoke-RestMethod -Method Put -Uri "http://127.0.0.1:$debugPort/json/new?about:blank"
        $socket = [System.Net.WebSockets.ClientWebSocket]::new()
        $socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null

        $commandId = [ref]0
        Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Page.enable" | Out-Null
        Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Runtime.enable" | Out-Null
        Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Page.navigate" -Params @{ url = $Url } | Out-Null
        Wait-CdpPageReady -Socket $socket -CommandId $commandId

        $metrics = Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Page.getLayoutMetrics"
        $documentHeight = Get-CdpDocumentHeight -Socket $socket -CommandId $commandId
        $dimensions = Resolve-CaptureDimensions -Metrics $metrics -DocumentHeight $documentHeight

        Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Emulation.setDeviceMetricsOverride" -Params @{
            width = $dimensions.Width
            height = $dimensions.Height
            deviceScaleFactor = 1
            mobile = $false
        } | Out-Null

        Start-Sleep -Milliseconds 300

        $metrics = Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Page.getLayoutMetrics"
        $documentHeight = Get-CdpDocumentHeight -Socket $socket -CommandId $commandId
        $dimensions = Resolve-CaptureDimensions -Metrics $metrics -DocumentHeight $documentHeight

        $screenshot = Invoke-CdpCommand -Socket $socket -CommandId $commandId -Method "Page.captureScreenshot" -Params @{
            format = "png"
            fromSurface = $true
            captureBeyondViewport = $true
            clip = @{
                x = 0
                y = 0
                width = $dimensions.Width
                height = $dimensions.Height
                scale = 1
            }
        }

        [System.IO.File]::WriteAllBytes($output, [Convert]::FromBase64String($screenshot.data))
    }
    finally {
        if ($socket) {
            $socket.Dispose()
        }

        if ($chromeProcess -and -not $chromeProcess.HasExited) {
            Stop-Process -Id $chromeProcess.Id -Force
            $chromeProcess.WaitForExit(2000) | Out-Null
        }

        Get-CimInstance Win32_Process |
            Where-Object { $_.Name -eq "chrome.exe" -and $_.CommandLine -like "*$userDataDir*" } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

        Start-Sleep -Milliseconds 200

        if (Test-Path $userDataDir) {
            try {
                Remove-Item -LiteralPath $userDataDir -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Verbose "Chrome temporary profile cleanup skipped: $userDataDir"
            }
        }
    }

    if (-not (Test-Path $output)) {
        throw "Screenshot was not created: $output"
    }

    $item = Get-Item $output
    if ($item.Length -le 0) {
        throw "Screenshot is empty: $output"
    }

    [PSCustomObject]@{
        App = $AppName
        PageId = $PageId
        Url = $Url
        File = $output
        Bytes = $item.Length
    }
}

$hosts = @(
    @{
        App = "HongdalApp"
        Project = "artifacts\capture-hosts\ShipperSourceCaptureHost\ShipperSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\ShipperSourceCaptureHost\bin\Debug\net10.0\ShipperSourceCaptureHost.dll"
        Port = 5211
        Pages = @(
            @{ Id = "HongdalApp-P01"; Path = "/shipper" },
            @{ Id = "HongdalApp-P01-1"; Path = "/shipper/settings/profile" },
            @{ Id = "HongdalApp-P01-2"; Path = "/shipper/settings/views" },
            @{ Id = "HongdalApp-P01-3"; Path = "/shipper/public-cargo" },
            @{ Id = "HongdalApp-P01-4"; Path = "/shipper/exploration/inbox" },
            @{ Id = "HongdalApp-P02"; Path = "/shipper/request" },
            @{ Id = "HongdalApp-P02-1"; Path = "/shipper/request/bulk" },
            @{ Id = "HongdalApp-P02-2"; Path = "/dispatch/address-form" },
            @{ Id = "HongdalApp-P03"; Path = "/shipper/request/SHP-1001" },
            @{ Id = "HongdalApp-P04"; Path = "/shipper/inbound/dashboard" },
            @{ Id = "HongdalApp-P04-1"; Path = "/shipper/inbound/requests" },
            @{ Id = "HongdalApp-P05"; Path = "/shipper/warehouse/workspace" },
            @{ Id = "HongdalApp-P05-1"; Path = "/shipper/warehouse/inventory" },
            @{ Id = "HongdalApp-P05-2"; Path = "/shipper/warehouse/scan" },
            @{ Id = "HongdalApp-P05-3"; Path = "/shipper/warehouse/work/inbound" },
            @{ Id = "HongdalApp-P06"; Path = "/shipper/sales/channels" },
            @{ Id = "HongdalApp-P06-1"; Path = "/shipper/sales/listings" },
            @{ Id = "HongdalApp-P06-2"; Path = "/shipper/sales/orders" },
            @{ Id = "HongdalApp-P07"; Path = "/shipper/international/fcl-lcl" },
            @{ Id = "HongdalApp-P07-1"; Path = "/shipper/customs/hs-reviews" },
            @{ Id = "HongdalApp-P08"; Path = "/shipper/reconsignment/orders" },
            @{ Id = "HongdalApp-P90"; Path = "/weather" },
            @{ Id = "HongdalApp-P91"; Path = "/counter" },
            @{ Id = "HongdalApp-P99"; Path = "/not-found" }
        )
    },
    @{
        App = "DriverApp"
        Project = "artifacts\capture-hosts\DriverSourceCaptureHost\DriverSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\DriverSourceCaptureHost\bin\Debug\net10.0\DriverSourceCaptureHost.dll"
        Port = 5212
        Pages = @(
            @{ Id = "DriverApp-P00"; Path = "/" },
            @{ Id = "DriverApp-P01"; Path = "/login" },
            @{ Id = "DriverApp-P02"; Path = "/driver/menu" },
            @{ Id = "DriverApp-P02-1"; Path = "/driver/settings/views" },
            @{ Id = "DriverApp-P03"; Path = "/driver/reservations" },
            @{ Id = "DriverApp-P04"; Path = "/driver/exploration/campaigns" },
            @{ Id = "DriverApp-P05"; Path = "/driver/transports/history" },
            @{ Id = "DriverApp-P06"; Path = "/driver/work/start" },
            @{ Id = "DriverApp-P06-1"; Path = "/driver/work/settings" },
            @{ Id = "DriverApp-P07"; Path = "/driver/home" },
            @{ Id = "DriverApp-P07-1"; Path = "/driver/home/summary" },
            @{ Id = "DriverApp-P08"; Path = "/driver/recommendations" },
            @{ Id = "DriverApp-P09"; Path = "/driver/recommendations/1" },
            @{ Id = "DriverApp-P10"; Path = "/driver/recommendations/1/decision" },
            @{ Id = "DriverApp-P11"; Path = "/driver/transports/current" },
            @{ Id = "DriverApp-P12"; Path = "/driver/transports/1/pickup" },
            @{ Id = "DriverApp-P13"; Path = "/driver/transports/1/dropoff" },
            @{ Id = "DriverApp-P14"; Path = "/driver/settlements/current-month" },
            @{ Id = "DriverApp-P14-1"; Path = "/driver/settlements/info" },
            @{ Id = "DriverApp-P14-2"; Path = "/driver/account/bank" },
            @{ Id = "DriverApp-P15"; Path = "/driver/notifications" },
            @{ Id = "DriverApp-P15-1"; Path = "/driver/notifications/settings" },
            @{ Id = "DriverApp-P15-2"; Path = "/driver/notifications/push" }
        )
    },
    @{
        App = "WarehouseManagerApp"
        Project = "artifacts\capture-hosts\WarehouseSourceCaptureHost\WarehouseSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\WarehouseSourceCaptureHost\bin\Debug\net10.0\WarehouseSourceCaptureHost.dll"
        Port = 5213
        Pages = @(
            @{ Id = "WarehouseManagerApp-P01"; Path = "/" },
            @{ Id = "WarehouseManagerApp-P02"; Path = "/work-board" },
            @{ Id = "WarehouseManagerApp-P02-1"; Path = "/work/inbound" },
            @{ Id = "WarehouseManagerApp-P02-2"; Path = "/work/inbound/workbench" },
            @{ Id = "WarehouseManagerApp-P02-3"; Path = "/scan" },
            @{ Id = "WarehouseManagerApp-P03"; Path = "/work/inbound/inspection" },
            @{ Id = "WarehouseManagerApp-P03-1"; Path = "/work/inbound/products" },
            @{ Id = "WarehouseManagerApp-P04"; Path = "/work/picking-batch" },
            @{ Id = "WarehouseManagerApp-P05"; Path = "/mart" },
            @{ Id = "WarehouseManagerApp-P05-1"; Path = "/mart/work-board" },
            @{ Id = "WarehouseManagerApp-P05-2"; Path = "/mart/work/mart-picking" },
            @{ Id = "WarehouseManagerApp-P99"; Path = "/not-found" }
        )
    },
    @{
        App = "OrdererApp"
        Project = "artifacts\capture-hosts\OrdererSourceCaptureHost\OrdererSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\OrdererSourceCaptureHost\bin\Debug\net10.0\OrdererSourceCaptureHost.dll"
        Port = 5214
        Pages = @(
            @{ Id = "OrdererApp-P01"; Path = "/" },
            @{ Id = "OrdererApp-P02"; Path = "/group-purchase" },
            @{ Id = "OrdererApp-P03"; Path = "/cargo" },
            @{ Id = "OrdererApp-P04"; Path = "/food" },
            @{ Id = "OrdererApp-P04-1"; Path = "/food/restaurants" },
            @{ Id = "OrdererApp-P04-2"; Path = "/food/mart" },
            @{ Id = "OrdererApp-P05"; Path = "/orders" },
            @{ Id = "OrdererApp-P99"; Path = "/not-found" }
        )
    },
    @{
        App = "RestaurantDeskApp"
        Project = "artifacts\capture-hosts\RestaurantDeskSourceCaptureHost\RestaurantDeskSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\RestaurantDeskSourceCaptureHost\bin\Debug\net10.0\RestaurantDeskSourceCaptureHost.dll"
        Port = 5215
        Pages = @(
            @{ Id = "RestaurantDeskApp-P01"; Path = "/" },
            @{ Id = "RestaurantDeskApp-P02"; Path = "/restaurants/nearby" },
            @{ Id = "RestaurantDeskApp-P02-1"; Path = "/restaurants/popular" },
            @{ Id = "RestaurantDeskApp-P03"; Path = "/reviews/moderation" },
            @{ Id = "RestaurantDeskApp-P04"; Path = "/dispatch/address-form" }
        )
    },
    @{
        App = "HumanResourcesManagerApp"
        Project = "artifacts\capture-hosts\HumanResourcesSourceCaptureHost\HumanResourcesSourceCaptureHost.csproj"
        Dll = "artifacts\capture-hosts\HumanResourcesSourceCaptureHost\bin\Debug\net10.0\HumanResourcesSourceCaptureHost.dll"
        Port = 5216
        Pages = @(
            @{ Id = "HumanResourcesManagerApp-P01"; Path = "/" }
        )
    }
)

$results = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[object]

foreach ($hostConfig in $hosts) {
    $project = Join-Path $repoRoot $hostConfig.Project
    dotnet build $project -v:minimal | Out-Host

    $dll = Join-Path $repoRoot $hostConfig.Dll
    $hostDirectory = Split-Path -Parent $project
    $port = [int]$hostConfig.Port
    $baseUrl = "http://127.0.0.1:$port"
    $process = $null

    try {
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList @($dll, "--urls", $baseUrl) `
            -WorkingDirectory $hostDirectory `
            -WindowStyle Hidden `
            -PassThru

        Wait-HttpReady -Port $port

        foreach ($page in $hostConfig.Pages) {
            $url = "$baseUrl$($page.Path)"
            try {
                $result = Invoke-PageCapture -AppName $hostConfig.App -PageId $page.Id -Url $url
                $results.Add($result)
                Write-Host "captured $($page.Id) -> $($result.File)"
            }
            catch {
                $failure = [PSCustomObject]@{
                    App = $hostConfig.App
                    PageId = $page.Id
                    Url = $url
                    Error = $_.Exception.Message
                }
                $failures.Add($failure)
                Write-Warning "failed $($page.Id): $($failure.Error)"
            }
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

$summary = [PSCustomObject]@{
    Captured = $results.Count
    Failed = $failures.Count
    Results = $results
    Failures = $failures
}

$summaryPath = Join-Path $PSScriptRoot "capture-client-pages-result.json"
$summary | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $summaryPath
$summary
