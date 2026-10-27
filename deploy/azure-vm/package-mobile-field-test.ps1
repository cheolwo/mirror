[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerImageArchive,
    [Parameter(Mandatory = $true)][string]$WebArchive,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [ValidateSet('food-mobile-field-test','integrated-mobile-field-test')]
    [string]$Profile = 'food-mobile-field-test',
    [string]$SourceSnapshotSha256
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($Profile -eq 'integrated-mobile-field-test') {
    if ($SourceSnapshotSha256 -cnotmatch '^[a-f0-9]{64}$') { throw 'Integrated packaging requires the reviewed build handoff source fingerprint.' }
    & (Join-Path $repoRoot 'eng/staging/Test-IntegratedMobileFieldPackage.ps1') -RepositoryRoot $repoRoot -AsObject | Out-Null
}
foreach ($archive in @($ServerImageArchive, $WebArchive)) {
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or (Get-Item -LiteralPath $archive).Length -eq 0) {
        throw 'A nonempty built server image archive and published web archive are required.'
    }
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new bundle directory; previous deployment evidence must be preserved.' }
New-Item -ItemType Directory -Path $output | Out-Null
$wrapper = if ($Profile -eq 'integrated-mobile-field-test') { 'deploy-integrated-mobile-field-test.sh' } else { 'deploy-food-mobile-field-test.sh' }
$files = @('compose.yaml', "compose.$Profile.override.yaml", 'Caddyfile',
    'deploy-preview-profile.sh', $wrapper, 'provision-preview-secrets.sh')
Copy-Item -LiteralPath $ServerImageArchive -Destination (Join-Path $output 'ssalddel-server.tar')
Copy-Item -LiteralPath $WebArchive -Destination (Join-Path $output 'web.tar.gz')
foreach ($file in $files) {
    $source = if ($Profile -eq 'integrated-mobile-field-test' -and $file -eq 'Caddyfile') { 'Caddyfile.integrated-mobile-field-test' } else { $file }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $source) -Destination (Join-Path $output $file)
}
$checksums = @()
foreach ($file in @('ssalddel-server.tar', 'web.tar.gz') + $files) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksums += "$hash  $file"
}
$checksums | Set-Content -LiteralPath (Join-Path $output 'deployment.sha256') -Encoding utf8NoBOM
$integrated = $Profile -eq 'integrated-mobile-field-test'
[pscustomobject]@{
    profile = $Profile; environment = 'Staging'; runtimeMode = 'Simulation'
    composeProject = $(if ($integrated) { 'mirror-integrated-field-test' } else { 'ssalddel-mobile-field-test' })
    deploymentRoot = $(if ($integrated) { '/opt/mirror-integrated-field-test' } else { '/opt/ssalddel-mobile-field-test' })
    confirmation = $(if ($integrated) { 'DEPLOY_ISOLATED_INTEGRATED_FIELD_TEST' } else { 'DEPLOY_ISOLATED_MOBILE_FIELD_TEST' })
    dedicatedHostRequired = $true
    automaticCloudDeployment = $false; physicalDeviceVerified = $false
    serverCommunicationVerified = $false; actualPushReceived = $false; publicReleaseReady = $false
    automaticDispatchEnabled = (-not $integrated); financialExecutionEnabled = $false; weeklyPreparationEnabled = $false
    allHostedServicesDisabled = $false; generalProjectionAndInboxWorkersMayRun = $true
    sourceSnapshotSha256 = $SourceSnapshotSha256; sourceBinding = $(if ($integrated) { 'DeclaredBuildHandoffWithArtifactHashes' } else { 'LegacyPackage' })
    serverImageSha256 = (Get-FileHash -LiteralPath (Join-Path $output 'ssalddel-server.tar') -Algorithm SHA256).Hash.ToLowerInvariant()
    webSha256 = (Get-FileHash -LiteralPath (Join-Path $output 'web.tar.gz') -Algorithm SHA256).Hash.ToLowerInvariant()
    externalConfigurationRequired = @('public HTTPS domain', 'isolated DB credentials', 'JWT and AES/salt',
        'persistent key ring and readable PFX', 'synthetic field-test role accounts', 'private storage and external credential mounts')
    files = @('ssalddel-server.tar', 'web.tar.gz') + $files
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'deployment-manifest.json') -Encoding utf8
if ($integrated) {
    # A failed check preserves this incomplete bundle for diagnosis and prevents deployment handoff.
    & (Join-Path $repoRoot 'eng/staging/Test-IntegratedMobileFieldPackage.ps1') -BundleDirectory $output -RepositoryRoot $repoRoot -AsObject | Out-Null
}
Write-Host "Packaged isolated Staging/Simulation bundle: $output (not deployed)"
