[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$validator = Join-Path $repoRoot "eng\validate-changes.ps1"

function Get-ValidationPlan {
    param(
        [string] $Level,
        [string[]] $Paths
    )

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $validator `
        -Level $Level `
        -PlanOnly `
        -Paths ($Paths -join ",")
    if ($LASTEXITCODE -ne 0) {
        throw "검증 계획 생성 실패: $($Paths -join ', ')"
    }

    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Assert-ContainsExactly {
    param(
        [string] $Label,
        [string[]] $Actual,
        [string[]] $Expected
    )

    $actualNormalized = @($Actual | Sort-Object -Unique)
    $expectedNormalized = @($Expected | Sort-Object -Unique)
    if (($actualNormalized -join "|") -ne ($expectedNormalized -join "|")) {
        throw "$Label 불일치. actual=[$($actualNormalized -join ', ')] expected=[$($expectedNormalized -join ', ')]"
    }
}

$simulation = Get-ValidationPlan -Level Task -Paths @(
    "Ssalddel.Simulation.Domain/SimulationSaveReplay.cs"
)
Assert-ContainsExactly "Simulation build" $simulation.BuildTargets @(
    "Ssalddel.Simulation.slnx"
)
Assert-ContainsExactly "Simulation tests" @($simulation.TestPlans | ForEach-Object { $_.Project }) @(
    "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj"
)
if (-not $simulation.CodeMapCheck) { throw "Simulation 변경은 코드 지도 검증을 포함해야 합니다." }

$unity = Get-ValidationPlan -Level Task -Paths @(
    "Ssalddel.Unity/Runtime/Application/WorldReadRuntime.cs"
)
Assert-ContainsExactly "Unity build" $unity.BuildTargets @(
    "Ssalddel.Unity.slnx"
)
Assert-ContainsExactly "Unity tests" @($unity.TestPlans | ForEach-Object { $_.Project }) @(
    "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
)
if (-not $unity.CodeMapCheck) { throw "Unity 변경은 코드 지도 검증을 포함해야 합니다." }

$mixed = Get-ValidationPlan -Level Task -Paths @(
    "Ssalddel/Program.cs",
    "Ssalddel.Simulation.Hosting/SsalddelSimulationHostingServiceCollectionExtensions.cs",
    "Ssalddel.Unity/Runtime/Application/WorldReadRuntime.cs"
)
Assert-ContainsExactly "Mixed build" $mixed.BuildTargets @(
    "Ssalddel.v0.0.slnx",
    "Ssalddel.Simulation.slnx",
    "Ssalddel.Unity.slnx"
)
Assert-ContainsExactly "Mixed tests" @($mixed.TestPlans | ForEach-Object { $_.Project }) @(
    "Ssalddel.Tests/Ssalddel.Tests.csproj",
    "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj",
    "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
)

$docs = Get-ValidationPlan -Level Task -Paths @(
    "docs/AI/CURRENT_WORK.md"
)
Assert-ContainsExactly "Docs build" $docs.BuildTargets @()
Assert-ContainsExactly "Docs tests" @($docs.TestPlans | ForEach-Object { $_.Project }) @()
if ($docs.CodeMapCheck) { throw "일반 문서 변경은 코드 지도 검증을 요구하지 않습니다." }

$metadata = Get-ValidationPlan -Level Task -Paths @(
    "Ssalddel.CodeMetadata/SsalddelCodeMetadataAttribute.cs"
)
Assert-ContainsExactly "Metadata build" $metadata.BuildTargets @(
    "Ssalddel.v3.5.slnx",
    "Ssalddel.Simulation.slnx",
    "Ssalddel.Unity.slnx"
)
Assert-ContainsExactly "Metadata tests" @($metadata.TestPlans | ForEach-Object { $_.Project }) @(
    "Ssalddel.Tests/Ssalddel.Tests.csproj",
    "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj",
    "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
)
if (-not $metadata.CodeMapCheck) { throw "공용 메타데이터 변경은 코드 지도 검증을 포함해야 합니다." }

$global = Get-ValidationPlan -Level Task -Paths @(
    "Directory.Build.props"
)
Assert-ContainsExactly "Global build" $global.BuildTargets @(
    "Ssalddel.v3.5.slnx",
    "Ssalddel.Simulation.slnx",
    "Ssalddel.Unity.slnx"
)
Assert-ContainsExactly "Global tests" @($global.TestPlans | ForEach-Object { $_.Project }) @(
    "Ssalddel.Tests/Ssalddel.Tests.csproj",
    "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj",
    "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
)

Write-Host "validate-changes routing: PASS"
