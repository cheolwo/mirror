[CmdletBinding()]
param(
    [ValidateSet('Build', 'Audit', 'SelfTest')]
    [string] $Mode = 'Build',
    [string] $SourcePath = 'artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string] $OutputDirectory = 'artifacts/local/sagajeong-mobility-graph',
    [string] $ExpectedSourceSha256 = '3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3',
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$localArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local'))

function Resolve-RepositoryPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Value))
}

function Resolve-LocalOutputPath([string] $Value) {
    $resolved = Resolve-RepositoryPath $Value
    $prefix = $localArtifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "MobilityGraphOutputMustRemainUnderArtifactsLocal:$resolved"
    }
    return $resolved
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $pythonCommand) {
        $pythonCommand.Source
    } else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
if (-not [IO.File]::Exists($PythonExecutable)) {
    [Console]::Out.WriteLine('{"networkRequested":false,"reasonCode":"MobilityGraphPythonUnavailable","runtimeAuthorized":false,"status":"Blocked"}')
    exit 2
}

Push-Location $repositoryRoot
$scriptExitCode = 0
try {
    if ($Mode -eq 'SelfTest') {
        & $PythonExecutable -m unittest discover -s eng/neighborhood -p test_sagajeong_mobility_graph.py -v
        $scriptExitCode = $LASTEXITCODE
    } else {
        $source = Resolve-RepositoryPath $SourcePath
        $output = Resolve-LocalOutputPath $OutputDirectory
        $script = if ($Mode -eq 'Build') {
            Join-Path $PSScriptRoot 'build-sagajeong-mobility-graph.py'
        } else {
            Join-Path $PSScriptRoot 'audit-sagajeong-mobility-graph.py'
        }
        $arguments = @(
            $script,
            '--source', $source,
            '--expected-source-sha256', $ExpectedSourceSha256
        )
        if ($Mode -eq 'Build') {
            $arguments += @('--output-directory', $output)
        } else {
            $arguments += @('--package-directory', $output)
        }
        & $PythonExecutable @arguments
        $scriptExitCode = $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
exit $scriptExitCode
