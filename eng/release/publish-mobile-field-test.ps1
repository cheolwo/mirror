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

    [ValidateNotNullOrEmpty()]
    [ValidateSet("orderer", "restaurant", "driver", "admin")]
    [string[]]$AppNames = @("orderer", "restaurant", "driver", "admin"),

    [switch]$PlanOnly,

    [string]$PlanningWorkItemId,

    [string]$PlanningLinksPath = "eng/planning-inquiries/app-production/role-app-links.json"
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

if ($AppNames.Count -eq 0 -or @($AppNames | Sort-Object -Unique).Count -ne $AppNames.Count) {
    throw "AppNames must contain one or more distinct supported apps."
}

$apps = @(@(
    [pscustomobject]@{ Name = "orderer"; Project = "OrdererApp/OrdererApp.csproj"; PackageId = "com.ssalddel.ordererapp" },
    [pscustomobject]@{ Name = "restaurant"; Project = "RestaurantDeskApp/RestaurantDeskApp.csproj"; PackageId = "com.ssalddel.restaurantdeskapp" },
    [pscustomobject]@{ Name = "driver"; Project = "FDriverApp/FDriverApp.csproj"; PackageId = "kr.ssalddel.fdriver" },
    [pscustomobject]@{ Name = "admin"; Project = "SsalddelAdminApp/SsalddelAdminApp.csproj"; PackageId = "com.ssalddel.adminapp" }
) | Where-Object { $_.Name -in $AppNames })

if ($PlanOnly) {
    $apps | Select-Object Name, Project, PackageId
    Write-Host "Release plan: version=$Version ($VersionCode), server=$($serverUri.AbsoluteUri), formats=APK+AAB"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    throw "KeystorePath is required unless PlanOnly is used."
}
$resolvedKeystore = (Resolve-Path -LiteralPath $KeystorePath).Path
$keystoreItem = Get-Item -LiteralPath $resolvedKeystore -Force
if ($keystoreItem.PSIsContainer) {
    throw "KeystorePath must be a file outside the repository."
}
# Refuse junction/symlink paths so lexical containment cannot hide an in-repository key.
$keyAncestor = $keystoreItem
while ($null -ne $keyAncestor) {
    if (($keyAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "KeystorePath must not traverse a symbolic link or junction."
    }
    $keyAncestor = if ($keyAncestor -is [IO.FileInfo]) { $keyAncestor.Directory } else { $keyAncestor.Parent }
}
$keyRelative = [IO.Path]::GetRelativePath($repoRoot, $resolvedKeystore)
if (-not [IO.Path]::IsPathRooted($keyRelative) -and $keyRelative -ne ".." -and
    -not $keyRelative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal)) {
    throw "KeystorePath must be outside the repository."
}
$storePassword = [Environment]::GetEnvironmentVariable("SSALDDEL_ANDROID_KEYSTORE_PASSWORD")
$keyPassword = [Environment]::GetEnvironmentVariable("SSALDDEL_ANDROID_KEY_PASSWORD")
if ([string]::IsNullOrWhiteSpace($storePassword) -or [string]::IsNullOrWhiteSpace($keyPassword)) {
    throw "Set SSALDDEL_ANDROID_KEYSTORE_PASSWORD and SSALDDEL_ANDROID_KEY_PASSWORD before publishing."
}

$releaseRoot = Join-Path $repoRoot (Join-Path $OutputRoot "$Version-$VersionCode")
$planningEvidence = $null
$planningResult = "Failed"
$planningChecks = New-Object "System.Collections.Generic.List[object]"
$planningArtifactPaths = New-Object "System.Collections.Generic.List[string]"
if (-not [string]::IsNullOrWhiteSpace($PlanningWorkItemId)) {
    . (Join-Path $repoRoot "eng/common/planning-evidence.ps1")
    $evidenceRunId = [Guid]::NewGuid().ToString("N")
    $planningEvidence = Start-PlanningEvidenceContext -RepoRoot $repoRoot `
        -WorkItemId $PlanningWorkItemId -LinksPath $PlanningLinksPath `
        -StartPath "artifacts/local/planning-evidence/apk-$evidenceRunId.start.json" -Kind "ApkPackaging" `
        -InputPaths @($apps | ForEach-Object { $_.Project }) `
        -InputRoots @($apps | ForEach-Object { (Split-Path -Parent $_.Project) -replace '\\', '/' }) `
        -ToolPaths @("eng/release/publish-mobile-field-test.ps1") -TargetFramework "net10.0-android" `
        -Scope @{ configuration = "Release"; roles = @($apps.Name); buildTargets = @($apps.Project) }
}
try {
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
    $publishExitCode = $LASTEXITCODE
    $planningChecks.Add([pscustomobject]@{
        name = "package $($app.Name)"; result = $(if ($publishExitCode -eq 0) { "Passed" } else { "Failed" }); exitCode = $publishExitCode
    })
    if ($publishExitCode -ne 0) {
        throw "Android publish failed for $($app.Name)."
    }

    $packages = Get-ChildItem -LiteralPath $destination -Recurse -File |
        Where-Object { $_.Extension -in ".apk", ".aab" }
    if (-not $packages) {
        throw "No APK or AAB was produced for $($app.Name)."
    }
    foreach ($package in $packages) {
        if ($null -ne $planningEvidence) {
            $planningArtifactPaths.Add([IO.Path]::GetRelativePath($repoRoot, $package.FullName).Replace('\', '/'))
        }
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
$planningResult = "Passed"
Write-Host "Created signed mobile field-test packages and manifest: $manifestPath"
}
finally {
    if ($null -ne $planningEvidence) {
        Complete-PlanningEvidenceContext -Context $planningEvidence -Result $planningResult `
            -Checks $planningChecks.ToArray() -ArtifactPaths $planningArtifactPaths.ToArray()
    }
}
