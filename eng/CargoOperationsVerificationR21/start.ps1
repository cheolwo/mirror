[CmdletBinding()]
param([switch]$NoBuild, [string]$ArtifactsPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskHostDirectory = $PSScriptRoot
$taskRepoRoot = [IO.Path]::GetFullPath((Join-Path $taskHostDirectory '../..'))
$taskEnvironmentFile = Join-Path $taskRepoRoot 'artifacts/local/usb-device-verification-r74/.private/environment.json'
if (-not (Test-Path -LiteralPath $taskEnvironmentFile -PathType Leaf)) { throw 'R114PrivateEnvironmentMissing' }
$taskSettings = Get-Content -LiteralPath $taskEnvironmentFile -Raw | ConvertFrom-Json
$taskNames = @('USB_R74_SQL_PASSWORD','USB_R74_SQL_ROOT_PASSWORD','USB_R74_ACCOUNT_PASSWORD','USB_R74_JWT_SECRET','USB_R74_AES_KEY','USB_R74_HASH_SALT','USB_R74_ACCESS_KEY')
$taskSaved = @{}
try {
    foreach ($taskName in $taskNames) {
        $taskProperty = $taskSettings.PSObject.Properties[$taskName]
        if ($null -eq $taskProperty -or [string]::IsNullOrWhiteSpace([string]$taskProperty.Value)) { throw "R114PrivateValueMissing:$taskName" }
        $taskSaved[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, [string]$taskProperty.Value, 'Process')
    }
    Push-Location $taskRepoRoot
    try {
        $taskProject = Join-Path $taskHostDirectory 'CargoOperationsVerificationR21.csproj'
        $taskRunArguments = @('run', '--project', $taskProject)
        if ($NoBuild) { $taskRunArguments += '--no-build' }
        if ($ArtifactsPath) { $taskRunArguments += @('--artifacts-path', $ArtifactsPath) }
        & dotnet @taskRunArguments -- --serve
        if ($LASTEXITCODE -ne 0) { throw "R114HostExited:$LASTEXITCODE" }
    } finally { Pop-Location }
} finally {
    foreach ($taskName in $taskSaved.Keys) { [Environment]::SetEnvironmentVariable($taskName, $taskSaved[$taskName], 'Process') }
    $taskSettings = $null
}
