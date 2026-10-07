[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerImageArchive,
    [Parameter(Mandatory = $true)][string]$WebArchive,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
foreach ($archive in @($ServerImageArchive, $WebArchive)) {
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or (Get-Item -LiteralPath $archive).Length -eq 0) {
        throw 'A nonempty built server image archive and published web archive are required.'
    }
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new bundle directory; previous deployment evidence must be preserved.' }
New-Item -ItemType Directory -Path $output | Out-Null
$files = @('compose.yaml', 'compose.food-mobile-field-test.override.yaml', 'Caddyfile',
    'deploy-preview-profile.sh', 'deploy-food-mobile-field-test.sh', 'provision-preview-secrets.sh')
Copy-Item -LiteralPath $ServerImageArchive -Destination (Join-Path $output 'ssalddel-server.tar')
Copy-Item -LiteralPath $WebArchive -Destination (Join-Path $output 'web.tar.gz')
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $output $file) }
$checksums = @()
foreach ($file in @('ssalddel-server.tar', 'web.tar.gz') + $files) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksums += "$hash  $file"
}
$checksums | Set-Content -LiteralPath (Join-Path $output 'deployment.sha256') -Encoding utf8NoBOM
[pscustomobject]@{
    profile = 'food-mobile-field-test'; environment = 'Staging'; runtimeMode = 'Simulation'
    composeProject = 'ssalddel-mobile-field-test'; deploymentRoot = '/opt/ssalddel-mobile-field-test'
    confirmation = 'DEPLOY_ISOLATED_MOBILE_FIELD_TEST'; dedicatedHostRequired = $true
    automaticCloudDeployment = $false; physicalDeviceVerified = $false
    externalConfigurationRequired = @('public HTTPS domain', 'isolated DB credentials', 'JWT and AES/salt',
        'persistent key ring and readable PFX', 'synthetic field-test role accounts')
    files = @('ssalddel-server.tar', 'web.tar.gz') + $files
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'deployment-manifest.json') -Encoding utf8
Write-Host "Packaged isolated Staging/Simulation bundle: $output (not deployed)"
