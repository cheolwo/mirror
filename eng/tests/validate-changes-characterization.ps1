[CmdletBinding()]
param(
    [string]$SnapshotPath = 'eng/tests/fixtures/validate-changes-plan.r2.json',
    [switch]$Capture,
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$entry = Join-Path $repoRoot 'eng/validate-changes.ps1'
$shell = (Get-Process -Id $PID).Path
$snapshot = [IO.Path]::GetFullPath((Join-Path $repoRoot $SnapshotPath))
$originalLocation = Get-Location
try {
    Set-Location -LiteralPath $repoRoot
    $cases = @(
        @{ Name='docs'; Level='Fast'; Paths='docs/README.md' },
        @{ Name='ignored'; Level='Fast'; Paths='artifacts/a.cs,bin/b.cs,obj/c.cs,.vs/d.cs' },
        @{ Name='explicit-filter-and-path-precedence'; Level='Fast'; Paths='Ssalddel/UnknownR2.cs'; TestFilter='FullyQualifiedName~ExplicitR2'; BaseRef='missing-ref-must-not-be-read' },
        @{ Name='no-related-test'; Level='Fast'; Paths='Ssalddel/UnknownR2.cs' },
        @{ Name='product-task'; Level='Task'; Paths='Ssalddel/UnknownR2.cs' },
        @{ Name='many-projects'; Level='Fast'; Paths='Ssalddel/UnknownR2.cs,Ssalddel.Ui.Common/UnknownR2.cs,OrdererApp/UnknownR2.cs,SellerApp/UnknownR2.cs'; TestFilter='FullyQualifiedName~ExplicitR2' },
        @{ Name='simulation'; Level='Task'; Paths='Ssalddel.Simulation/UnknownR2.cs' },
        @{ Name='unity'; Level='Task'; Paths='Ssalddel.Unity/UnknownR2.cs' },
        @{ Name='shared-metadata'; Level='Task'; Paths='Ssalddel.Contracts/Common/Metadata/UnknownR2.cs' },
        @{ Name='release-default'; Level='Release'; Paths='Ssalddel/UnknownR2.cs' },
        @{ Name='release-explicit-debug'; Level='Release'; Configuration='Debug'; Paths='Ssalddel/UnknownR2.cs' },
        @{ Name='normalization'; Level='Task'; Paths=' SellerApp\UnknownR2.cs ,Ssalddel/UnknownR2.cs,Ssalddel/UnknownR2.cs' }
    )
    $plans = [ordered]@{}
    foreach ($case in $cases) {
        $arguments = @('-NoProfile','-NonInteractive','-File',$entry,'-PlanOnly','-Level',$case.Level,'-Paths',$case.Paths)
        foreach ($key in @('TestFilter','BaseRef','Configuration')) {
            if ($case.ContainsKey($key)) { $arguments += @("-$key", $case[$key]) }
        }
        $output = & $shell @arguments
        if ($LASTEXITCODE -ne 0) { throw "PlanOnly failed: $($case.Name)" }
        $plans[$case.Name] = ($output -join "`n") | ConvertFrom-Json
    }

    # Check new routing semantics without rewriting the original 12-case snapshot.
    $skillPaths = '.agents/skills/probe/agents/openai.yaml,.claude/skills/probe/agents/openai.yaml'
    $skillCases = @(
        @{ Name='skills-fast'; Level='Fast'; Paths=$skillPaths; Expected='docs' },
        @{ Name='skills-task'; Level='Task'; Paths=$skillPaths; Expected='docs' },
        @{ Name='skills-and-product'; Level='Task'; Paths="$skillPaths,Ssalddel/UnknownR2.cs"; Expected='product-task' },
        @{ Name='skills-and-simulation'; Level='Task'; Paths="$skillPaths,Ssalddel.Simulation.Domain/UnknownR2.cs"; ExpectedPaths='Ssalddel.Simulation.Domain/UnknownR2.cs' },
        @{ Name='skills-and-unity'; Level='Task'; Paths="$skillPaths,Ssalddel.Unity/UnknownR2.cs"; Expected='unity' },
        @{ Name='agents-settings-outside-skills'; Level='Task'; Paths='.agents/settings.yaml'; Expected='product-task' },
        @{ Name='claude-settings-outside-skills'; Level='Task'; Paths='.claude/settings.yaml'; Expected='product-task' },
        @{ Name='agents-similar-prefix'; Level='Task'; Paths='.agents/skills-extra/agents/openai.yaml'; Expected='product-task' },
        @{ Name='claude-similar-prefix'; Level='Task'; Paths='.claude/skills-extra/agents/openai.yaml'; Expected='product-task' }
    )
    foreach ($case in $skillCases) {
        $output = & $shell -NoProfile -NonInteractive -File $entry -PlanOnly -Level $case.Level -Paths $case.Paths
        if ($LASTEXITCODE -ne 0) { throw "PlanOnly failed: $($case.Name)" }
        $actualPlan = ($output -join "`n") | ConvertFrom-Json
        $expectedPlan = if ($case.ContainsKey('ExpectedPaths')) {
            $baselineOutput = & $shell -NoProfile -NonInteractive -File $entry -PlanOnly -Level $case.Level -Paths $case.ExpectedPaths
            if ($LASTEXITCODE -ne 0) { throw "PlanOnly baseline failed: $($case.Name)" }
            ($baselineOutput -join "`n") | ConvertFrom-Json
        } else {
            $plans[$case.Expected]
        }
        foreach ($key in @('GuidanceOnly','BuildTargets','TestPlans','CodeMapCheck','FoodDriverCodeMapCheck','EvidenceMapCheck')) {
            $actualValue = ConvertTo-Json -InputObject $actualPlan.$key -Depth 8 -Compress
            $expectedValue = ConvertTo-Json -InputObject $expectedPlan.$key -Depth 8 -Compress
            if ($actualValue -cne $expectedValue) { throw "Skill routing differs: $($case.Name) / $key. actual=$actualValue expected=$expectedValue" }
        }
    }

    # Load only function definitions; never execute the validator's main block.
    $planHelper = Join-Path $repoRoot 'eng/validation/ChangedFileValidationPlan.ps1'
    $functionSources = if (Test-Path -LiteralPath $planHelper) { @($planHelper,$entry) } else { @($entry) }
    foreach ($source in $functionSources) {
        $tokens=$null; $errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
        if ($errors.Count -ne 0) { throw "PowerShell parse errors: $source" }
        foreach ($function in $ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)) {
            . ([scriptblock]::Create($function.Extent.Text))
        }
    }

    $TestFilter = ''
    $script:rgCalls = 0
    function rg {
        $script:rgCalls++
        1..30 | ForEach-Object { 'Ssalddel.Tests/Generated{0:D2}Tests.cs' -f $_ }
    }
    $terms = 1..20 | ForEach-Object { 'Ssalddel/LongTerm{0:D2}.cs' -f $_ }
    $selected = Get-RelatedTestFilter -Files $terms -TestRoot 'Ssalddel.Tests'
    if ($script:rgCalls -ne 16 -or ($selected -split '\|').Count -ne 24) { throw 'Related-test selection caps changed.' }
    Remove-Item Function:rg
    $TestFilter = 'explicit-priority'
    if ((Get-RelatedTestFilter -Files @('Ssalddel.Tests/OtherTests.cs') -TestRoot 'Ssalddel.Tests') -ne $TestFilter) { throw 'Explicit filter priority changed.' }

    $probeDirectory = if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
        Join-Path $repoRoot ('artifacts/local/validation-script-characterization/command-probes-' + $(if ($Capture) { 'before' } else { 'after' }))
    } else {
        Join-Path ([IO.Path]::GetFullPath((Join-Path $repoRoot $EvidenceDirectory))) ('command-probes-' + [Guid]::NewGuid().ToString('N'))
    }
    [void](New-Item -ItemType Directory -Path $probeDirectory -Force)
    $planningEvidence = [pscustomobject]@{ Probe = $true }
    $planningChecks = New-Object 'System.Collections.Generic.List[object]'
    $successLog = Join-Path $probeDirectory 'success.log'
    Invoke-LoggedCommand -Name 'probe-success' -Executable $shell -Arguments @('-NoProfile','-NonInteractive','-Command',"Write-Output 'probe-output'; exit 0") -LogPath $successLog
    if (-not (Select-String -LiteralPath $successLog -SimpleMatch 'probe-output' -Quiet)) { throw 'Command output was not preserved.' }
    $failureLog = Join-Path $probeDirectory 'failure.log'
    $failureObserved = $false
    try {
        Invoke-LoggedCommand -Name 'probe-failure' -Executable $shell -Arguments @('-NoProfile','-NonInteractive','-Command',"Write-Output 'Failed! probe'; exit 7") -LogPath $failureLog
    } catch {
        if ($_.Exception.Message -ne "probe-failure failed. Full log: $failureLog") { throw }
        $failureObserved = $true
    }
    if (-not $failureObserved -or $planningChecks.Count -ne 2 -or $planningChecks[0].result -ne 'Passed' -or $planningChecks[1].exitCode -ne 7) { throw 'Failure propagation/evidence changed.' }

    $result = [ordered]@{ Plans=$plans; RelatedTermsLimit=16; RelatedTestLimit=24; ExplicitFilterPriority=$true; CommandChecks=$planningChecks.ToArray(); OutputPreserved=$true; FailurePropagated=$true }
    $json = $result | ConvertTo-Json -Depth 15
    if ($Capture) {
        if (Test-Path -LiteralPath $snapshot) { throw 'Do not overwrite the original baseline.' }
        $json | Set-Content -LiteralPath $snapshot -Encoding utf8
    } else {
        $expected=(Get-Content -Raw -LiteralPath $snapshot | ConvertFrom-Json | ConvertTo-Json -Depth 15 -Compress)
        $actual=($json | ConvertFrom-Json | ConvertTo-Json -Depth 15 -Compress)
        if ($expected -cne $actual) { throw 'Validation plan/command characterization differs from baseline.' }
    }
    Write-Host "Verified $($cases.Count) baseline PlanOnly cases, $($skillCases.Count) skill-routing cases, selection caps and command success/failure evidence."
} finally {
    Set-Location -LiteralPath $originalLocation
}
