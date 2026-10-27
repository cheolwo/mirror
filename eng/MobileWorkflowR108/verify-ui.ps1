param(
    [string]$PreviewUrl = 'http://127.0.0.1:5588/',
    [string]$OutputDirectory = 'artifacts/local/local-operations-r108/mobile/ui-01'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$allowed = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/local')) + [IO.Path]::DirectorySeparatorChar
if (!$output.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) {
    throw 'A new output under artifacts/local is required.'
}
$origin = [Uri]$PreviewUrl
if ($origin.Scheme -ne 'http' -or $origin.Host -ne '127.0.0.1' -or $origin.AbsolutePath -ne '/' -or $origin.UserInfo) {
    throw 'A credential-free loopback preview origin is required.'
}
$runtime = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node'
$node = Join-Path $runtime 'bin/node.exe'
$browser = 'C:/Program Files/Google/Chrome/Application/chrome.exe'
foreach ($required in @($node, $browser)) { if (!(Test-Path -LiteralPath $required)) { throw 'Required installed runtime not found.' } }
$names = @('NODE_PATH','SSALDDEL_PLAYWRIGHT_CHROMIUM','SSALDDEL_MEMBER_DELIVERY_PREVIEW_URL','SSALDDEL_MEMBER_DELIVERY_PREVIEW_OUTPUT')
$saved = @{}
foreach($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:NODE_PATH = Join-Path $runtime 'node_modules'
    $env:SSALDDEL_PLAYWRIGHT_CHROMIUM = $browser
    $env:SSALDDEL_MEMBER_DELIVERY_PREVIEW_URL = $PreviewUrl
    $env:SSALDDEL_MEMBER_DELIVERY_PREVIEW_OUTPUT = $output
    Push-Location $repoRoot
    try {
        & $node (Join-Path $repoRoot 'eng/RoleWorkspacePreview/verify-neighborhood-member-delivery.mjs')
        if ($LASTEXITCODE -ne 0) { throw 'Product component preview verification failed. Evidence is retained.' }
    } finally { Pop-Location }
    $result = Get-Content -LiteralPath (Join-Path $output 'verification.json') -Raw | ConvertFrom-Json
    if ($result.status -ne 'PassedWithinListedScope' -or !$result.syntheticFixtureOnly -or $result.actualDatabaseVerified -or $result.physicalDeviceVerified) {
        throw 'Unexpected preview evidence boundary.'
    }
} finally {
    foreach($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
