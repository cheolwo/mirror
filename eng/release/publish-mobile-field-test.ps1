[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerBaseAddress,

    [string]$Version = "0.1.0",

    [int]$VersionCode = 1,

    [string]$KeystorePath,

    [Parameter(Mandatory = $false)]
    [string]$KeyAlias = "ssalddel-mobile",

    [string]$OutputRoot = "artifacts/local/mobile-field-test",

    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$serverUri = $null
if (-not [Uri]::TryCreate($ServerBaseAddress, [UriKind]::Absolute, [ref]$serverUri) -or
    $serverUri.Scheme -ne "https" -or
    $serverUri.IsLoopback -or
    $serverUri.Host -eq "10.0.2.2" -or
    -not [string]::IsNullOrEmpty($serverUri.UserInfo) -or
    -not [string]::IsNullOrEmpty($serverUri.Query) -or
    -not [string]::IsNullOrEmpty($serverUri.Fragment)) {
    throw "ServerBaseAddress must be a public HTTPS origin or base path without credentials, query, or fragment."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$' -or $VersionCode -lt 1) {
    throw "Version must use x.y.z and VersionCode must be at least 1."
}

$apps = @(
    [pscustomobject]@{ Name = "orderer"; Project = "OrdererApp/OrdererApp.csproj"; PackageId = "com.ssalddel.ordererapp" },
    [pscustomobject]@{ Name = "restaurant"; Project = "RestaurantDeskApp/RestaurantDeskApp.csproj"; PackageId = "com.ssalddel.restaurantdeskapp" },
    [pscustomobject]@{ Name = "driver"; Project = "FDriverApp/FDriverApp.csproj"; PackageId = "kr.ssalddel.fdriver" },
    [pscustomobject]@{ Name = "admin"; Project = "SsalddelAdminApp/SsalddelAdminApp.csproj"; PackageId = "com.ssalddel.adminapp" }
)

if ($PlanOnly) {
    $apps | Select-Object Name, Project, PackageId
    Write-Host "Release plan: version=$Version ($VersionCode), server=$($serverUri.AbsoluteUri), formats=APK+AAB"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    throw "KeystorePath is required unless PlanOnly is used."
}
$resolvedKeystore = (Resolve-Path -LiteralPath $KeystorePath).Path
$storePassword = [Environment]::GetEnvironmentVariable("SSALDDEL_ANDROID_KEYSTORE_PASSWORD")
$keyPassword = [Environment]::GetEnvironmentVariable("SSALDDEL_ANDROID_KEY_PASSWORD")
if ([string]::IsNullOrWhiteSpace($storePassword) -or [string]::IsNullOrWhiteSpace($keyPassword)) {
    throw "Set SSALDDEL_ANDROID_KEYSTORE_PASSWORD and SSALDDEL_ANDROID_KEY_PASSWORD before publishing."
}

$releaseRoot = Join-Path $repoRoot (Join-Path $OutputRoot "$Version-$VersionCode")
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$artifacts = @()
foreach ($app in $apps) {
    $project = Join-Path $repoRoot $app.Project
    $destination = Join-Path $releaseRoot $app.Name
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $arguments = @(
        "publish", $project,
        "-f", "net10.0-android",
        "-c", "Release",
        "-o", $destination,
        "-p:UseSharedCompilation=false",
        "-p:ApplicationDisplayVersion=$Version",
        "-p:ApplicationVersion=$VersionCode",
        "-p:SsalddelServerBaseAddress=$($serverUri.AbsoluteUri)",
        "-p:AndroidPackageFormats=apk;aab",
        "-p:AndroidKeyStore=true",
        "-p:AndroidSigningKeyStore=$resolvedKeystore",
        "-p:AndroidSigningKeyAlias=$KeyAlias",
        "-p:AndroidSigningStorePass=env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD",
        "-p:AndroidSigningKeyPass=env:SSALDDEL_ANDROID_KEY_PASSWORD"
    )
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Android publish failed for $($app.Name)."
    }

    $packages = Get-ChildItem -LiteralPath $destination -Recurse -File |
        Where-Object { $_.Extension -in ".apk", ".aab" }
    if (-not $packages) {
        throw "No APK or AAB was produced for $($app.Name)."
    }
    foreach ($package in $packages) {
        $artifacts += [pscustomobject]@{
            app = $app.Name
            packageId = $app.PackageId
            version = $Version
            versionCode = $VersionCode
            server = $serverUri.AbsoluteUri
            file = [IO.Path]::GetRelativePath($releaseRoot, $package.FullName).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            bytes = $package.Length
        }
    }
}

$manifestPath = Join-Path $releaseRoot "release-manifest.json"
$artifacts | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Host "Created signed mobile field-test packages and manifest: $manifestPath"
