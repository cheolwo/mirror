[CmdletBinding()]
param(
    [ValidateSet('Write', 'Check', 'Query')]
    [string] $Mode = 'Check',
    [string] $PolicyPath = 'eng/execution-ledgers/operational-unity-transfer-policy.json',
    [string] $MachineOutputPath = 'docs/AI/generated/operational-unity-transfer-catalog.json',
    [string] $OutputPath = 'docs/AI/generated/operational-unity-transfer-catalog.md',
    [ValidateSet('Version', 'Workflow', 'Classification', 'H1', 'H2', 'Area', 'PageKey', 'RoleObject')]
    [string] $QueryKind = 'PageKey',
    [string] $QueryValue = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $repositoryRoot 'eng/Ssalddel.OperationalUnityTransfer/Ssalddel.OperationalUnityTransfer.csproj'

$arguments = @(
    'run',
    '--project', $projectPath,
    '--',
    '--policy', $PolicyPath,
    '--output-json', $MachineOutputPath,
    '--output-markdown', $OutputPath
)

switch ($Mode) {
    'Write' {
        $arguments += '--write'
    }
    'Query' {
        if ([string]::IsNullOrWhiteSpace($QueryValue)) {
            throw 'OperationalUnityTransferQueryValueMissing'
        }
        $arguments += @('--query-kind', $QueryKind.ToLowerInvariant(), '--query-value', $QueryValue)
    }
}

Push-Location $repositoryRoot
try {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "OperationalUnityTransferCatalogFailed:ExitCode=$LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
