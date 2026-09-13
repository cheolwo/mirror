$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$managerPath = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-subject-interaction-development.ps1'
$fixtureRoot = Join-Path $repositoryRoot 'artifacts/local/subject-interaction-development-tests'

if (-not (Test-Path -LiteralPath $fixtureRoot)) {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
}

function Write-Json([string] $Path, [object] $Value) {
    [IO.File]::WriteAllText(
        $Path,
        (($Value | ConvertTo-Json -Depth 40) + "`n"),
        [Text.UTF8Encoding]::new($false))
}

function Invoke-Manager([string] $PolicyRelativePath, [string] $Mode = 'Validate') {
    $output = & pwsh -NoProfile -File $managerPath -Mode $Mode -PolicyPath $PolicyRelativePath 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Text = ($output | Out-String)
    }
}

function Require-Failure([string] $Name, [object] $Policy, [string] $ExpectedCode) {
    $policyPath = Join-Path $fixtureRoot "$Name.policy.json"
    Write-Json $policyPath $Policy
    $relativePath = [IO.Path]::GetRelativePath($repositoryRoot, $policyPath).Replace('\', '/')
    $result = Invoke-Manager $relativePath
    if ($result.ExitCode -eq 0 -or -not $result.Text.Contains($ExpectedCode)) {
        throw "SubjectInteractionExpectedFailureMissing:${Name}:${ExpectedCode}:$($result.Text)"
    }
}

$basePolicy = Get-Content -LiteralPath (Join-Path $repositoryRoot 'eng/execution-ledgers/subject-interaction-development.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseSubjects = Get-Content -LiteralPath (Join-Path $repositoryRoot 'eng/execution-ledgers/gameplay-subjects.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseInteractions = Get-Content -LiteralPath (Join-Path $repositoryRoot 'eng/execution-ledgers/world-interactions.json') -Raw -Encoding UTF8 | ConvertFrom-Json

$current = Invoke-Manager 'eng/execution-ledgers/subject-interaction-development.json'
$legacyGoals = Get-Content -LiteralPath (Join-Path $repositoryRoot $basePolicy.legacyGoalLedgerPath) -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedSubjects = @($baseSubjects.items | Where-Object statusCode -eq 'Ready').Count
$expectedInteractions = @($baseInteractions.items).Count
$expectedGoals = @($legacyGoals.items).Count + @($basePolicy.nativeInteractionGoals).Count
if ($current.ExitCode -ne 0 -or -not $current.Text.Contains("SubjectInteractionDevelopmentValid:Subjects=$expectedSubjects;WorldInteractions=$expectedInteractions;Goals=$expectedGoals;")) {
    throw "SubjectInteractionCurrentValidationFailed:$($current.Text)"
}

$positivePolicy = $basePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$positiveWi = @($baseInteractions.items)[0]
$positiveBinding = @($positivePolicy.controlPolicySubjectBindings | Where-Object controlPolicyCode -eq $positiveWi.controlPolicyCode)[0]
$positiveDesignRef = 'docs/Architecture/주체상호작용중심개발체계.md'
$positiveDesignHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot $positiveDesignRef) -Algorithm SHA256).Hash
$positivePolicy.nativeInteractionGoals = @([pscustomobject]@{
    goalStableId = 'interaction-goal:test.loopless.v1'
    goalStateCode = 'Queued'
    worldInteractionId = [string] $positiveWi.id
    subjectBindingRefs = @([string] $positiveBinding.subjectStableId, [string] $positivePolicy.directTargetSubjectStableId)
    optionalPlayableLoopValidationRef = ''
    planningGate = [pscustomobject]@{
        topicStableId = 'topic:test-loopless-interaction.v1'
        designDocumentRef = $positiveDesignRef
        designRevision = 'test-loopless-interaction.design.r1'
        designHashSha256 = $positiveDesignHash
        statusCode = 'Approved'
        approvalEvidenceRef = $positiveDesignRef
    }
    derivedEffectBinding = [pscustomobject]@{
        applicabilityCode = 'NotApplicable'
        statusCode = 'NotApplicable'
        effectCodes = @()
        hopDepthCode = 'None'
        causalEvidenceRefs = @()
        idempotencyRefs = @()
        saveReplayRefs = @()
        notApplicableReason = '직접 결과만 검증하는 독립 WI 시험 fixture다.'
    }
})
$positivePolicy.outputJsonPath = 'artifacts/local/subject-interaction-development-tests/positive.output.json'
$positivePolicy.outputMarkdownPath = 'artifacts/local/subject-interaction-development-tests/positive.output.md'
$positivePolicyPath = Join-Path $fixtureRoot 'positive.policy.json'
Write-Json $positivePolicyPath $positivePolicy
$positiveRelative = [IO.Path]::GetRelativePath($repositoryRoot, $positivePolicyPath).Replace('\', '/')
$positiveFirst = Invoke-Manager $positiveRelative 'Write'
if ($positiveFirst.ExitCode -ne 0 -or -not $positiveFirst.Text.Contains('Loopless=1')) {
    throw "SubjectInteractionLooplessGoalFailed:$($positiveFirst.Text)"
}
$firstJson = Get-Content -LiteralPath (Join-Path $repositoryRoot $positivePolicy.outputJsonPath) -Raw -Encoding UTF8
$firstMarkdown = Get-Content -LiteralPath (Join-Path $repositoryRoot $positivePolicy.outputMarkdownPath) -Raw -Encoding UTF8
$positiveSecond = Invoke-Manager $positiveRelative 'Write'
$secondJson = Get-Content -LiteralPath (Join-Path $repositoryRoot $positivePolicy.outputJsonPath) -Raw -Encoding UTF8
$secondMarkdown = Get-Content -LiteralPath (Join-Path $repositoryRoot $positivePolicy.outputMarkdownPath) -Raw -Encoding UTF8
if ($positiveSecond.ExitCode -ne 0 -or $firstJson -cne $secondJson -or $firstMarkdown -cne $secondMarkdown) {
    throw 'SubjectInteractionOutputNotDeterministic'
}

$blockedSubjects = $baseSubjects | ConvertTo-Json -Depth 40 | ConvertFrom-Json
@($blockedSubjects.items | Where-Object subjectStableId -eq $positiveBinding.subjectStableId)[0].statusCode = 'Blocked'
$blockedSubjectsPath = Join-Path $fixtureRoot 'blocked-subjects.json'
Write-Json $blockedSubjectsPath $blockedSubjects
$blockedPolicy = $basePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$blockedPolicy.subjectCatalogPath = [IO.Path]::GetRelativePath($repositoryRoot, $blockedSubjectsPath).Replace('\', '/')
Require-Failure 'blocked-subject' $blockedPolicy 'WorldInteractionSubjectNotReady'

$missingMappingPolicy = $basePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$removedPolicyCode = [string] $positiveWi.controlPolicyCode
$missingMappingPolicy.controlPolicySubjectBindings = @($missingMappingPolicy.controlPolicySubjectBindings | Where-Object controlPolicyCode -ne $removedPolicyCode)
Require-Failure 'missing-control-policy' $missingMappingPolicy 'WorldInteractionSubjectPolicyMissing'

$missingDirectInteractions = $baseInteractions | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$missingDirectInteractions.items[0].completionStateCodes = @()
$missingDirectInteractionsPath = Join-Path $fixtureRoot 'missing-direct-result.json'
Write-Json $missingDirectInteractionsPath $missingDirectInteractions
$missingDirectPolicy = $basePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$missingDirectPolicy.worldInteractionCatalogPath = [IO.Path]::GetRelativePath($repositoryRoot, $missingDirectInteractionsPath).Replace('\', '/')
Require-Failure 'missing-direct-result' $missingDirectPolicy 'WorldInteractionDirectCompletionMissing'

$twoHopPolicy = $positivePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$twoHopPolicy.nativeInteractionGoals[0].goalStableId = 'interaction-goal:test.two-hop.v1'
$twoHopPolicy.nativeInteractionGoals[0].derivedEffectBinding = [pscustomobject]@{
    applicabilityCode = 'Required'
    statusCode = 'Ready'
    effectCodes = @('effect:test-derived')
    hopDepthCode = 'TwoHop'
    causalEvidenceRefs = @()
    idempotencyRefs = @('eng/execution-ledgers/world-interactions.json')
    saveReplayRefs = @('Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationSaveReplay.cs')
    notApplicableReason = ''
}
Require-Failure 'two-hop-causality' $twoHopPolicy 'NativeDerivedEffectTwoHopCausalityMissing'

$unknownLoopPolicy = $positivePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$unknownLoopPolicy.nativeInteractionGoals[0].goalStableId = 'interaction-goal:test.unknown-loop.v1'
$unknownLoopPolicy.nativeInteractionGoals[0].optionalPlayableLoopValidationRef = 'playable-loop:not-registered.v1'
Require-Failure 'unknown-loop' $unknownLoopPolicy 'NativeOptionalLoopUnknown'

$missingPlanningPolicy = $positivePolicy | ConvertTo-Json -Depth 40 | ConvertFrom-Json
$missingPlanningPolicy.nativeInteractionGoals[0].goalStableId = 'interaction-goal:test.missing-planning.v1'
$missingPlanningPolicy.nativeInteractionGoals[0].PSObject.Properties.Remove('planningGate')
Require-Failure 'missing-planning' $missingPlanningPolicy 'NativeGoalPlanningGateMissing'

Write-Output 'SubjectInteractionDevelopmentTests:Passed'
