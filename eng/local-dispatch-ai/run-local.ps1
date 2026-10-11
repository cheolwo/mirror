param(
    [ValidateSet('DryRun', 'Execute')][string]$Mode = 'DryRun',
    [ValidateSet('quiet', 'balanced', 'surge')][string]$Scenario = 'balanced',
    [string]$PythonPath = '',
    [string]$RuntimeRoot = (Join-Path $env:LOCALAPPDATA 'Ssalddel\LocalDispatchAI')
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputRoot = Join-Path $repoRoot 'artifacts\local\local-dispatch-ai'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
if (-not $PythonPath) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($pythonCommand) { $PythonPath = $pythonCommand.Source }
    else { $PythonPath = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' }
}
if (-not (Test-Path -LiteralPath $PythonPath)) { throw 'Python 3.10+ 경로가 필요합니다. -PythonPath를 지정하세요.' }
$advisor = Join-Path $PSScriptRoot 'dispatch_advisor.py'
if ($Mode -eq 'DryRun') {
    & $PythonPath $advisor --scenario $Scenario
    exit $LASTEXITCODE
}
$runtimeExe = Join-Path $RuntimeRoot 'ollama-v0.40.2\ollama.exe'
if (-not (Test-Path -LiteralPath $runtimeExe)) { throw 'README의 검증된 portable 런타임이 없습니다.' }
$port = 12681
if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
    throw '시험 포트가 이미 사용 중입니다. 다른 프로세스를 종료하거나 재사용하지 않습니다.'
}
$environmentNames = @('OLLAMA_HOST', 'OLLAMA_MODELS', 'OLLAMA_NO_CLOUD', 'OLLAMA_KEEP_ALIVE')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$ownedProcess = $null
try {
    $env:OLLAMA_HOST = '127.0.0.1:12681'
    $env:OLLAMA_MODELS = Join-Path $RuntimeRoot 'models'
    $env:OLLAMA_NO_CLOUD = '1'
    $env:OLLAMA_KEEP_ALIVE = '0'
    $ownedProcess = Start-Process -FilePath $runtimeExe -ArgumentList 'serve' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $outputRoot 'server.stdout.log') -RedirectStandardError (Join-Path $outputRoot 'server.stderr.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($ownedProcess.HasExited) { throw '로컬 모델 서버가 시작 중 종료됐습니다.' }
        try {
            $null = Invoke-RestMethod -Uri 'http://127.0.0.1:12681/api/version' -MaximumRedirection 0 -TimeoutSec 1
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw '로컬 모델 서버 준비 시간이 초과됐습니다.' }
    $tags = Invoke-RestMethod -Uri 'http://127.0.0.1:12681/api/tags' -MaximumRedirection 0 -TimeoutSec 5
    if (-not ($tags.models | Where-Object { $_.name -eq 'qwen3:1.7b' })) {
        throw 'qwen3:1.7b 모델 다운로드가 필요합니다. 시험 도구는 자동 다운로드하지 않습니다.'
    }
    & $PythonPath $advisor --scenario $Scenario --execute --endpoint 'http://127.0.0.1:12681'
    if ($LASTEXITCODE -ne 0) { throw '합성 집계 시험 도구가 실패했습니다.' }
} finally {
    if ($ownedProcess -and -not $ownedProcess.HasExited) {
        # keep_alive=0으로 모델을 내린 뒤, 직접 생성한 서버 PID만 정리한다.
        Stop-Process -Id $ownedProcess.Id -ErrorAction SilentlyContinue
    }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
}
