param(
    [Parameter(Mandatory = $true)][string] $PythonPath,
    [Parameter(Mandatory = $true)][ValidateSet('delivery', 'mirror')][string] $Mode
)
$ErrorActionPreference = 'Stop'
$pythonExecutable = (Resolve-Path -LiteralPath $PythonPath).Path.Replace('\', '/')
if ($pythonExecutable.Contains("'") -or $pythonExecutable.Contains("`n")) { throw 'Unsupported Python path.' }
$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Not a Git repository.' }
$configuredHooks = & git config --get core.hooksPath
if ($configuredHooks) { throw 'Existing custom hooks path: preserve it and integrate manually.' }
$hooksDirectory = [IO.Path]::GetFullPath((& git rev-parse --git-path hooks).Trim())
$hookPath = Join-Path $hooksDirectory 'pre-push'
$marker = '# raw-media-guard managed hook'
if ((Test-Path -LiteralPath $hookPath) -and -not (Get-Content -Raw -LiteralPath $hookPath).Contains($marker)) {
    throw 'An existing pre-push hook is present; it was not overwritten.'
}
$relativeGuard = if ($Mode -eq 'delivery') { 'premiere-control/git/raw_media_guard.py' } else { 'eng/git/raw_media_guard.py' }
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativeGuard))) { throw 'Guard script is missing.' }
$hookText = @'
#!/bin/sh
# raw-media-guard managed hook
repo_root=$(git rev-parse --show-toplevel) || exit 2
exec '__PYTHON__' "$repo_root/__GUARD__" --mode __MODE__ --pre-push
'@
$hookText = $hookText.Replace('__PYTHON__', $pythonExecutable).Replace('__GUARD__', $relativeGuard).Replace('__MODE__', $Mode)
[IO.Directory]::CreateDirectory($hooksDirectory) | Out-Null
[IO.File]::WriteAllText($hookPath, $hookText.Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Installed local pre-push media guard ($Mode)."
