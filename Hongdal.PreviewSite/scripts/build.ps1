$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sourceRoot = Join-Path $projectRoot "src"
$workerRoot = Join-Path $projectRoot "worker"
$distRoot = Join-Path $projectRoot "dist"

if (Test-Path -LiteralPath $distRoot) {
    $resolvedDist = (Resolve-Path -LiteralPath $distRoot).Path
    if (-not $resolvedDist.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Build output escaped project root: $resolvedDist"
    }

    Remove-Item -LiteralPath $resolvedDist -Recurse -Force
}

$clientRoot = Join-Path $distRoot "client"
$assetRoot = Join-Path $clientRoot "assets"
$imageRoot = Join-Path $assetRoot "images"
$serverRoot = Join-Path $distRoot "server"

New-Item -ItemType Directory -Path $imageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $serverRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $sourceRoot "index.html") -Destination (Join-Path $clientRoot "index.html")
Copy-Item -LiteralPath (Join-Path $sourceRoot "styles.css") -Destination (Join-Path $assetRoot "styles.css")
Copy-Item -LiteralPath (Join-Path $sourceRoot "community-sales.js") -Destination (Join-Path $assetRoot "community-sales.js")
Copy-Item -LiteralPath (Join-Path $sourceRoot "app.js") -Destination (Join-Path $assetRoot "app.js")
Copy-Item -Path (Join-Path $sourceRoot "images\*") -Destination $imageRoot -Force
Copy-Item -LiteralPath (Join-Path $workerRoot "index.js") -Destination (Join-Path $serverRoot "index.js")

$publicRoutes = @(
    "community",
    "community/write",
    "community/posts/101",
    "community/group-purchase",
    "community/group-import",
    "information/public-data",
    "global",
    "global/suppliers/apply",
    "global/products/indonesian-rattan-storage-basket",
    "community/global-trade/101",
    "shipper/request",
    "shipper/inbound/dashboard",
    "shipper/sales/pages/new",
    "shipper/sales/channels",
    "shipper/international/fcl-lcl",
    "driver/home",
    "driver/recommendations",
    "driver/transports/current",
    "driver/transport/proof",
    "warehouse/work-board",
    "warehouse/work/inbound/products",
    "warehouse/work/inbound/inspection",
    "warehouse/mart/picking"
)

foreach ($route in $publicRoutes) {
    $routeRoot = Join-Path $clientRoot $route
    New-Item -ItemType Directory -Path $routeRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot "index.html") -Destination (Join-Path $routeRoot "index.html")
}

Write-Output "Hongdal Preview Site build completed: $distRoot"
