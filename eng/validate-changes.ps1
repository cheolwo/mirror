[CmdletBinding()]
param(
    [ValidateSet("Fast", "Task", "Release")]
    [string] $Level = "Fast",

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string[]] $Paths,

    [string] $BaseRef,

    [string] $TestFilter,

    [switch] $NoRestore,

    [switch] $PlanOnly,

    [string] $PlanningWorkItemId,

    [string] $PlanningLinksPath = "eng/planning-inquiries/app-production/role-app-links.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$originalLocation = Get-Location
$planningEvidence = $null
$planningResult = "Failed"
$planningChecks = New-Object "System.Collections.Generic.List[object]"

. (Join-Path $PSScriptRoot "validation/ChangedFileValidationPlan.ps1")

function Show-FailureSummary {
    param([string] $LogPath)

    $matches = @(
        Select-String -LiteralPath $LogPath -Pattern (
            "error [A-Z]{2,}[0-9]+",
            "Build FAILED",
            "Failed!",
            [regex]::Escape("실패"),
            "Exception"
        ) -Encoding UTF8 |
            Select-Object -First 30 |
            ForEach-Object { $_.Line.Trim() }
    )

    if ($matches.Count -gt 0) {
        $matches | ForEach-Object { Write-Host "  $_" }
        return
    }

    Get-Content -LiteralPath $LogPath -Encoding UTF8 -Tail 40 |
        ForEach-Object { Write-Host "  $_" }
}

function Invoke-LoggedCommand {
    param(
        [string] $Name,
        [string] $Executable,
        [string[]] $Arguments,
        [string] $LogPath
    )

    Write-Host "[run] $Name"
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $Executable @Arguments *> $LogPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($null -ne $planningEvidence) {
        $planningChecks.Add([pscustomobject]@{
            name = $Name
            result = $(if ($exitCode -eq 0) { "Passed" } else { "Failed" })
            exitCode = $exitCode
        })
    }

    if ($exitCode -ne 0) {
        Write-Host "[fail] $Name (exit $exitCode)"
        Show-FailureSummary -LogPath $LogPath
        throw "$Name failed. Full log: $LogPath"
    }

    Write-Host "[pass] $Name"
}

try {
    Set-Location -LiteralPath $repoRoot

    if ($Level -eq "Release" -and -not $PSBoundParameters.ContainsKey("Configuration")) {
        $Configuration = "Release"
    }

    $changedFiles = @(Get-ChangedFiles)
    $guidanceOnly = Test-IsGuidanceOnly -Files $changedFiles
    $codeMapCheck = Test-RequiresCodeMapCheck -Files $changedFiles
    $foodDriverCodeMapCheck = Test-RequiresFoodDriverCodeMapCheck -Files $changedFiles
    $evidenceMapCheck = Test-RequiresEvidenceMapCheck -Files $changedFiles
    $buildTargets = @()
    $testPlans = @()

    if (-not $guidanceOnly) {
        $testDefinitions = @(Get-TestProjectDefinitions -Files $changedFiles)
        switch ($Level) {
            "Fast" {
                $buildTargets = @(Get-DirectBuildTargets -Files $changedFiles)
                if ($buildTargets.Count -gt 3) {
                    $buildTargets = @(Get-TaskSlices -Files $changedFiles)
                }
                $testPlans = @(
                    foreach ($definition in $testDefinitions) {
                        $filter = Get-RelatedTestFilter `
                            -Files $changedFiles `
                            -TestRoot $definition.TestRoot
                        if (-not [string]::IsNullOrWhiteSpace($filter)) {
                            [pscustomobject]@{
                                Project = $definition.Project
                                Solution = $definition.Solution
                                Mode = "Targeted"
                                Filter = $filter
                            }
                        }
                    }
                )
            }
            "Task" {
                $buildTargets = @(Get-TaskSlices -Files $changedFiles)
                $testPlans = @(
                    foreach ($definition in $testDefinitions) {
                        [pscustomobject]@{
                            Project = $definition.Project
                            Solution = $definition.Solution
                            Mode = "Full"
                            Filter = $null
                        }
                    }
                )
            }
            "Release" {
                $buildTargets = @(
                    "Ssalddel.v0.0.slnx",
                    "Ssalddel.v0.5.slnx",
                    "Ssalddel.v1.0.slnx",
                    "Ssalddel.v1.5.slnx",
                    "Ssalddel.v3.5.slnx",
                    "Ssalddel.Simulation.slnx",
                    "Ssalddel.Unity.slnx"
                )
                $testPlans = @(
                    [pscustomobject]@{
                        Project = "Ssalddel.Tests/Ssalddel.Tests.csproj"
                        Solution = "Ssalddel.v3.5.slnx"
                        Mode = "Full"
                        Filter = $null
                    },
                    [pscustomobject]@{
                        Project = "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj"
                        Solution = "Ssalddel.Simulation.slnx"
                        Mode = "Full"
                        Filter = $null
                    },
                    [pscustomobject]@{
                        Project = "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
                        Solution = "Ssalddel.Unity.slnx"
                        Mode = "Full"
                        Filter = $null
                    }
                )
            }
        }
    }

    $validationPlan = [pscustomobject]@{
        Level = $Level
        Configuration = $Configuration
        ChangedPaths = @($changedFiles)
        GuidanceOnly = $guidanceOnly
        CodeMapCheck = $codeMapCheck
        FoodDriverCodeMapCheck = $foodDriverCodeMapCheck
        EvidenceMapCheck = $evidenceMapCheck
        BuildTargets = @($buildTargets)
        TestPlans = @($testPlans | ForEach-Object {
            [pscustomobject]@{
                Project = $_.Project
                Mode = $_.Mode
                Filter = $_.Filter
            }
        })
    }

    if ($PlanOnly) {
        $validationPlan | ConvertTo-Json -Depth 6
        exit 0
    }

    Write-Host "Validation plan"
    Write-Host "  Level: $Level"
    Write-Host "  Configuration: $Configuration"
    Write-Host "  Changed paths: $($changedFiles.Count)"
    Write-Host "  Guidance/docs only: $guidanceOnly"
    Write-Host "  Code map check: $codeMapCheck"
    Write-Host "  E responsibility map check: $evidenceMapCheck"
    if ($buildTargets.Count -eq 0) {
        Write-Host "  Build: skipped"
    }
    else {
        Write-Host "  Build: $($buildTargets -join ', ')"
    }
    if ($testPlans.Count -eq 0) {
        Write-Host "  Tests: skipped"
    }
    else {
        foreach ($testPlan in $testPlans) {
            if ($testPlan.Mode -eq "Full") {
                Write-Host "  Tests: $($testPlan.Project) (full)"
            }
            else {
                $filterCount = @($testPlan.Filter -split "\|").Count
                Write-Host "  Tests: $($testPlan.Project) (targeted, $filterCount filters)"
            }
        }
    }

    $runId = Get-Date -Format "yyyyMMdd-HHmmss"
    if (-not [string]::IsNullOrWhiteSpace($PlanningWorkItemId)) {
        # Fast/Task may finish in the same second. Never reuse another evidence run.
        $runId += "-" + [Guid]::NewGuid().ToString("N")
    }
    $runDirectory = Join-Path $repoRoot "artifacts\local\validation\$runId"
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($PlanningWorkItemId)) {
        . (Join-Path $PSScriptRoot "common/planning-evidence.ps1")
        $selectionJson = ConvertTo-Json -InputObject @($testPlans) -Depth 5 -Compress
        $selectionBytes = [Text.Encoding]::UTF8.GetBytes($selectionJson)
        $selectionHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($selectionBytes)).ToLowerInvariant()
        $planningInputPaths = @($changedFiles + $buildTargets + @($testPlans | ForEach-Object { $_.Project }) | Sort-Object -Unique)
        $planningInputRoots = @($planningInputPaths | Where-Object { $_ -match '\.csproj$' } | ForEach-Object {
            (Split-Path -Parent $_) -replace '\\', '/'
        } | Sort-Object -Unique)
        $planningEvidence = Start-PlanningEvidenceContext -RepoRoot $repoRoot `
            -WorkItemId $PlanningWorkItemId -LinksPath $PlanningLinksPath `
            -StartPath "artifacts/local/validation/$runId/planning-evidence.start.json" -Kind "Validation" `
            -InputPaths $planningInputPaths -InputRoots $planningInputRoots -ToolPaths @("eng/validate-changes.ps1", "eng/validation/ChangedFileValidationPlan.ps1") `
            -Scope @{
                level = $Level; configuration = $Configuration; noRestore = [bool]$NoRestore
                buildTargets = @($buildTargets); testProjects = @($testPlans | ForEach-Object { $_.Project })
                testModes = @($testPlans | ForEach-Object { $_.Mode }); testSelectionSha256 = $selectionHash
            }
    }

    Invoke-LoggedCommand `
        -Name "git diff --check" `
        -Executable "git" `
        -Arguments @("diff", "--check") `
        -LogPath (Join-Path $runDirectory "diff-check.log")

    if ($codeMapCheck) {
        $codeMapArguments = @(
            "run",
            "--project",
            "eng/Ssalddel.CodeMap/Ssalddel.CodeMap.csproj",
            "--configuration",
            $Configuration,
            "--no-launch-profile"
        )
        if ($NoRestore) {
            $codeMapArguments += "--no-restore"
        }
        $codeMapArguments += @("--", "--check")

        Invoke-LoggedCommand `
            -Name "Simulation Unity code map check" `
            -Executable "dotnet" `
            -Arguments $codeMapArguments `
            -LogPath (Join-Path $runDirectory "code-map-check.log")
    }

    if ($foodDriverCodeMapCheck) {
        $foodDriverCodeMapArguments = @(
            "run", "--project", "eng/Ssalddel.CodeMap/Ssalddel.CodeMap.csproj",
            "--configuration", $Configuration, "--no-launch-profile"
        )
        if ($NoRestore) { $foodDriverCodeMapArguments += "--no-restore" }
        $foodDriverCodeMapArguments += @("--", "--work-area", "food-driver", "--check")
        Invoke-LoggedCommand `
            -Name "Food driver code map check" `
            -Executable "dotnet" `
            -Arguments $foodDriverCodeMapArguments `
            -LogPath (Join-Path $runDirectory "food-driver-code-map-check.log")
    }

    if ($evidenceMapCheck) {
        $evidenceMapArguments = @(
            "run",
            "--project",
            "eng/Ssalddel.EvidenceMap/Ssalddel.EvidenceMap.csproj",
            "--configuration",
            $Configuration,
            "--no-launch-profile"
        )
        if ($NoRestore) {
            $evidenceMapArguments += "--no-restore"
        }
        $evidenceMapArguments += @("--", "--strict")

        Invoke-LoggedCommand `
            -Name "E responsibility code map check" `
            -Executable "dotnet" `
            -Arguments $evidenceMapArguments `
            -LogPath (Join-Path $runDirectory "evidence-map-check.log")
    }

    $buildIndex = 0
    foreach ($target in $buildTargets) {
        $buildIndex++
        $arguments = @(
            "build",
            $target,
            "--configuration",
            $Configuration,
            "--nologo",
            "--verbosity",
            "quiet"
        )
        if ($NoRestore) {
            $arguments += "--no-restore"
        }

        Invoke-LoggedCommand `
            -Name "build $target" `
            -Executable "dotnet" `
            -Arguments $arguments `
            -LogPath (Join-Path $runDirectory ("build-{0:D2}.log" -f $buildIndex))
    }

    $testIndex = 0
    foreach ($testPlan in $testPlans) {
        $testIndex++
        $testName = [System.IO.Path]::GetFileNameWithoutExtension($testPlan.Project)
        $testArguments = @(
            "test",
            $testPlan.Project,
            "--configuration",
            $Configuration,
            "--nologo",
            "--logger",
            "trx;LogFileName=$testName.trx",
            "--results-directory",
            $runDirectory
        )
        if ($NoRestore) {
            $testArguments += "--no-restore"
        }
        $testsAlreadyBuilt = @(
            $buildTargets | Where-Object {
                $_ -eq $testPlan.Solution -or
                $_ -eq $testPlan.Project
            }
        ).Count -gt 0
        if ($Level -ne "Fast" -or $testsAlreadyBuilt) {
            $testArguments += "--no-build"
        }
        if ($testPlan.Mode -eq "Targeted") {
            $testArguments += @("--filter", $testPlan.Filter)
        }

        Invoke-LoggedCommand `
            -Name "$($testPlan.Mode.ToLowerInvariant()) tests $($testPlan.Project)" `
            -Executable "dotnet" `
            -Arguments $testArguments `
            -LogPath (Join-Path $runDirectory ("tests-{0:D2}.log" -f $testIndex))
    }

    $planningResult = "Passed"
    Write-Host "Validation complete"
    Write-Host "  Detailed logs: $runDirectory"
}
finally {
    try {
        if ($null -ne $planningEvidence) {
            $planningArtifacts = @(Get-ChildItem -LiteralPath $runDirectory -File | Where-Object {
                $_.Extension -in @('.log', '.trx')
            } | ForEach-Object { [IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace('\', '/') })
            Complete-PlanningEvidenceContext -Context $planningEvidence -Result $planningResult `
                -Checks $planningChecks.ToArray() -ArtifactPaths $planningArtifacts
        }
    }
    finally {
        Set-Location -LiteralPath $originalLocation
    }
}
