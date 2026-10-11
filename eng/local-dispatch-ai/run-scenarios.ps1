[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $PSScriptRoot 'ScenarioRunner/ScenarioRunner.csproj'
$output = Join-Path $repoRoot 'artifacts/local/local-dispatch-scenarios-r45/campaign'

# 이 경로는 .NET 합성 시나리오 도구만 빌드·실행한다. 모델 시작·네트워크·서버 설정 변경 없음.
& dotnet build $project --configuration Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw '시나리오 도구 빌드에 실패했습니다.' }
& dotnet (Join-Path $PSScriptRoot 'ScenarioRunner/bin/Debug/net10.0/ScenarioRunner.dll') --output $output
if ($LASTEXITCODE -ne 0) { throw '시나리오 불변식 또는 재현 검사에 실패했습니다. 결과를 확인하세요.' }
