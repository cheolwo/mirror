$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manager = Join-Path $repositoryRoot `
    "eng/execution-ledgers/manage-playable-loop-presentation-validation.ps1"
$result = @(& $manager -Mode Validate)
if (($result -join "`n") -notlike
    "*PlayableLoopPresentationValidationValid:Modules=*;Profiles=*;PlayableUnits=*") {
    throw "PresentationValidationManagerFailed:$($result -join ';')"
}

$generatedPath = Join-Path $repositoryRoot `
    "docs/AI/generated/playable-loop-presentation-validation.md"
$generated = Get-Content -LiteralPath $generatedPath -Raw -Encoding UTF8
$catalogPath = Join-Path $repositoryRoot `
    "eng/execution-ledgers/playable-loop-presentation-validation-modules.json"
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
foreach ($expected in @(
    [string] $catalog.generatedDocumentText.titleKo,
    [string] $catalog.generatedDocumentText.commonGateHeadingKo,
    [string] $catalog.generatedDocumentText.profileHeadingKo,
    "presentation-binding",
    "surface-clearance",
    "building-foundation-entry",
    "actual-camera-input-result-return",
    "playable-loop:nature-shelter-foundation.v1",
    "playable-loop:nature-workbench-foundation.v1")) {
    if (-not $generated.Contains($expected)) {
        throw "PresentationValidationGeneratedEntryMissing:$expected"
    }
}

$commonCount = @($catalog.commonModuleCodes).Count
$conditionalCount = @($catalog.modules |
    Where-Object applicabilityCode -eq "Feature").Count
$profileCount = @($catalog.loopProfiles).Count
Write-Output `
    "PlayableLoopPresentationValidationTestsPassed:Common=$commonCount;Conditional=$conditionalCount;Profiles=$profileCount"

# 연결/증거 계약 시험. 아래 임시 자료는 실제 Unity 실행이나 E 증거로 등록하지 않는다.
. (Join-Path $repositoryRoot 'eng/common/presentation-module-bindings.ps1')
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/local/validation/presentation-modules/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$fixtureRelative = $fixtureRoot.Substring($repositoryRoot.Length + 1).Replace('\','/')
function Copy-Fixture([object] $Value) { return ($Value | ConvertTo-Json -Depth 60 | ConvertFrom-Json) }
function Write-Fixture([string] $Name, [string] $Text) {
    $path = Join-Path $fixtureRoot $Name
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
    return $path
}
$script:bindingCases = 0
function Assert-Rejected([scriptblock] $Run, [string] $Expected) {
    $caught = ''
    try { & $Run | Out-Null } catch { $caught = $_.Exception.Message }
    if (-not $caught.Contains($Expected)) { throw "PresentationNegativeFailed:$Expected;actual=$caught" }
    $script:bindingCases++
}
function Assert-Case([bool] $Condition, [string] $Name) {
    if (-not $Condition) { throw "PresentationPositiveFailed:$Name" }
    $script:bindingCases++
}
$template = Get-Content -LiteralPath (Join-Path $repositoryRoot 'eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$template.playableUnitStableId = 'playable-loop:presentation-fixture.v1'
$legacy = Copy-Fixture $template
$legacy.PSObject.Properties.Remove('presentationModuleBindings')
$legacyRows = @(Test-PresentationModuleBindings $legacy $catalog $repositoryRoot)
Assert-Case ($legacyRows.Count -eq 8 -and @($legacyRows | Where-Object statusCode -ne 'Unverified').Count -eq 0) 'LegacyIsUnverified'
$originalStage = $template.currentEvidenceStage
$rows = @(Test-PresentationModuleBindings $template $catalog $repositoryRoot)
Assert-Case ($rows.Count -eq 8 -and $template.currentEvidenceStage -eq $originalStage) 'NoAutomaticPromotion'

$bad = Copy-Fixture $template
$bad.presentationModuleBindings = @($bad.presentationModuleBindings | Select-Object -Skip 1)
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'RequiredModuleMissing'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings += $bad.presentationModuleBindings[0]
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'Duplicate'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].moduleCode = 'actor-grounding-identification'
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'NotApplicableToProfile'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].statusCode = 'AutomaticallyDone'
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'StatusInvalid'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].reason = ''
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'ReasonMissing'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].statusCode = 'NotApplicable'
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'CommonNotApplicable'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].implementationRefs = @('repo:../outside.cs')
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'ReferenceTraversal'
$bad.presentationModuleBindings[0].implementationRefs = @('repo:missing-presentation-fixture.cs')
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'ReferenceMissing'
$bad.presentationModuleBindings[0].implementationRefs = @('repo:C:/Windows/file')
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'ReferenceTraversal'
$bad.presentationModuleBindings[0].implementationRefs = @('https://example.invalid/source')
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'ReferenceInvalid'
$bad.presentationModuleBindings[0].implementationRefs = @('unity:Assets/missing.cs')
Assert-Case (@(Test-PresentationModuleBindings $bad $catalog $repositoryRoot).Count -eq 8) 'ExternalReferenceRemainsUnverified'
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot $fixtureRoot } 'ReferenceMissing'
$bad = Copy-Fixture $template
$bad.presentationModuleBindings[0].PSObject.Properties.Remove('testRefs')
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot } 'FieldMissing'

$sourcePath = Write-Fixture 'source.cs' '// synthetic source; never production evidence'
$testPath = Write-Fixture 'tests.cs' '// synthetic test; never production evidence'
$proofPath = Write-Fixture 'result.json' '{"fixture":true,"passed":1}'
$passed = Copy-Fixture $template
$binding = @($passed.presentationModuleBindings | Where-Object moduleCode -eq 'presentation-projection-lifecycle')[0]
$binding.statusCode = 'Passed'
$binding.candidateRevisionOrFingerprint = 'fixture.r1'
$binding.implementationRefs = @('repo:' + $fixtureRelative + '/source.cs')
$binding.testRefs = @('repo:' + $fixtureRelative + '/tests.cs')
Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot } 'EvidenceMissing'
$binding.evidenceRefs = @('evidence:fixture:only')
$evidence = [pscustomobject]@{ packages = @([pscustomobject]@{
    evidenceId = 'evidence:fixture:only'; resultCode = 'Passed'; statusCode = 'Current'
    evidenceTrackCode = 'Presentation'; sourceRevision = 'fixture.r1'; evidenceKindCode = 'AutomatedTest'
    subjectRefs = @($passed.playableUnitStableId); evidenceStageCodes = @('E2')
    presentationModuleScope = @{moduleCodes=@($binding.moduleCode); worldInteractionIds=@()}
    artifactReferences = @(@($sourcePath,$testPath,$proofPath) | ForEach-Object { [pscustomobject]@{
        locator = $_.Substring($repositoryRoot.Length + 1).Replace('\','/')
        locationKind = 'LocalArtifact'; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    } })
}) }
Assert-Case (@(Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $evidence | Where-Object statusCode -eq 'Passed').Count -eq 1) 'BoundEvidenceAcceptedWithoutEPromotion'
Assert-Case ($passed.currentEvidenceStage -eq 'E0' -and -not $passed.promotionEligible) 'BoundEvidenceDoesNotPromote'
foreach ($case in @(
    @('statusCode','Stale','EvidenceNotCurrentPresentation'),
    @('resultCode','Failed','EvidenceNotCurrentPresentation'),
    @('evidenceTrackCode','Logic','EvidenceNotCurrentPresentation'),
    @('sourceRevision','fixture.r2','EvidenceRevisionMismatch'),
    @('subjectRefs',@('unrelated'),'EvidenceScopeMismatch'),
    @('evidenceStageCodes',@('E7'),'EvidenceScopeMismatch'),
    @('artifactReferences',@(),'EvidenceArtifactsMissing')
)) {
    $badEvidence = Copy-Fixture $evidence
    $badEvidence.packages[0].($case[0]) = $case[1]
    Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $badEvidence } $case[2]
}
$badEvidence = Copy-Fixture $evidence
$badEvidence.packages[0].artifactReferences = @($badEvidence.packages[0].artifactReferences | Select-Object -Last 1)
Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $badEvidence } 'ImplementationNotFingerprintBound'
$badEvidence = Copy-Fixture $evidence
$badEvidence.packages[0].artifactReferences[0].sha256 = 'a' * 64
Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $badEvidence } 'EvidenceArtifactChanged'
$bad = Copy-Fixture $passed
@($bad.presentationModuleBindings | Where-Object moduleCode -eq $binding.moduleCode)[0].implementationRefs = @()
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot '' $evidence } 'ImplementationMissing'
$bad = Copy-Fixture $passed
@($bad.presentationModuleBindings | Where-Object moduleCode -eq $binding.moduleCode)[0].candidateRevisionOrFingerprint = ''
Assert-Rejected { Test-PresentationModuleBindings $bad $catalog $repositoryRoot '' $evidence } 'CandidateMissing'

$play = Copy-Fixture $template
$playBinding = @($play.presentationModuleBindings | Where-Object moduleCode -eq 'actual-camera-input-result-return')[0]
$playBinding.statusCode = 'Passed'; $playBinding.candidateRevisionOrFingerprint = 'fixture.r1'
$playBinding.evidenceRefs = @('evidence:fixture:only')
$playEvidence = Copy-Fixture $evidence
$playEvidence.packages[0].subjectRefs = @($play.playableUnitStableId)
$playEvidence.packages[0].presentationModuleScope.moduleCodes = @($playBinding.moduleCode)
$playEvidence.packages[0].evidenceStageCodes = @('E7')
Assert-Rejected { Test-PresentationModuleBindings $play $catalog $repositoryRoot '' $playEvidence } 'PresentationE5RequiresLogicE5'
$play.trackPlans.logic.currentEvidenceStage = 'E5'
Assert-Rejected { Test-PresentationModuleBindings $play $catalog $repositoryRoot '' $playEvidence } 'ActualPlayEvidenceMissing'
$playEvidence.packages[0].evidenceKindCode = 'UnityPlayMode'
$second = Copy-Fixture $playEvidence.packages[0]
$second.evidenceId = 'evidence:fixture:game-view'; $second.evidenceKindCode = 'GameView'
$playEvidence.packages += $second; $playBinding.evidenceRefs += $second.evidenceId
Assert-Case (@(Test-PresentationModuleBindings $play $catalog $repositoryRoot '' $playEvidence | Where-Object statusCode -eq 'Passed').Count -eq 1) 'PlayAndViewEvidencePair'

$wiScoped = Copy-Fixture $passed
$wiScoped | Add-Member -NotePropertyName presentationModuleScope -NotePropertyValue ([pscustomobject]@{worldInteractionIds=@('WI-FARM-04')})
Assert-Rejected { Test-PresentationModuleBindings $wiScoped $catalog $repositoryRoot '' $evidence } 'EvidenceWiMismatch'
$e3 = Copy-Fixture $template
$e3Binding = @($e3.presentationModuleBindings | Where-Object moduleCode -eq 'presentation-automated-regression')[0]
$e3Binding.statusCode = 'Passed'; $e3Binding.candidateRevisionOrFingerprint = 'fixture.r1'
$e3Binding.evidenceRefs = @('evidence:fixture:only')
Assert-Rejected { Test-PresentationModuleBindings $e3 $catalog $repositoryRoot '' $evidence } 'TestsMissing'
$e3Binding.testRefs = @('repo:' + $fixtureRelative + '/tests.cs')
$e3Evidence = Copy-Fixture $evidence
$e3Evidence.packages[0].subjectRefs = @($e3.playableUnitStableId)
$e3Evidence.packages[0].presentationModuleScope.moduleCodes = @($e3Binding.moduleCode)
$e3Evidence.packages[0].evidenceStageCodes = @('E3')
$e3Evidence.packages[0].evidenceKindCode = 'Contract'
Assert-Rejected { Test-PresentationModuleBindings $e3 $catalog $repositoryRoot '' $e3Evidence } 'AutomatedEvidenceMissing'
$notSpatial = Copy-Fixture $template
$notSpatial.presentationE4Preparation.applicabilityCode = 'NotApplicable'
@($notSpatial.presentationModuleBindings | Where-Object moduleCode -eq 'visual-source-bounds')[0].statusCode = 'NotApplicable'
Assert-Case (@(Test-PresentationModuleBindings $notSpatial $catalog $repositoryRoot | Where-Object statusCode -eq 'NotApplicable').Count -eq 1) 'NonSpatialOptOutWithReason'

# v1 대장 읽기 호환과 v2 필수 모듈 검사. 테스트 출력은 전용 artifacts에만 쓴다.
$legacyCatalog = Copy-Fixture $catalog
$legacyCatalog.schemaVersion = 'playable-loop-presentation-validation-modules.v1'
$legacyCatalog.allowedEvidenceStageCodes = @('E4','E5','E6','E7')
$legacyCatalog.modules = @($legacyCatalog.modules | Where-Object evidenceStageCode -in @('E4','E5','E6','E7'))
$legacyCatalog.commonModuleCodes = @($legacyCatalog.modules | Where-Object applicabilityCode -eq 'Common' | ForEach-Object moduleCode)
foreach ($profile in $legacyCatalog.loopProfiles) { $profile.PSObject.Properties.Remove('workOrderRef') }
$legacyPath = Write-Fixture 'legacy.json' ($legacyCatalog | ConvertTo-Json -Depth 50)
$legacyOutput = $fixtureRelative + '/legacy.md'
& $manager -Mode Write -CatalogPath ($fixtureRelative + '/legacy.json') -OutputPath $legacyOutput | Out-Null
& $manager -Mode Validate -CatalogPath ($fixtureRelative + '/legacy.json') -OutputPath $legacyOutput | Out-Null
Assert-Case ($true) 'LegacyCatalogReadWriteCompatibility'
$beforeTime = (Get-Item -LiteralPath (Join-Path $repositoryRoot $legacyOutput)).LastWriteTimeUtc
& $manager -Mode Write -CatalogPath ($fixtureRelative + '/legacy.json') -OutputPath $legacyOutput | Out-Null
Assert-Case ((Get-Item -LiteralPath (Join-Path $repositoryRoot $legacyOutput)).LastWriteTimeUtc -eq $beforeTime) 'GenerationIsIdempotent'
$badCatalog = Copy-Fixture $catalog
$badCatalog.commonModuleCodes = @($badCatalog.commonModuleCodes | Where-Object { $_ -ne 'presentation-requirement-contract' })
Write-Fixture 'missing-stage.json' ($badCatalog | ConvertTo-Json -Depth 50) | Out-Null
Assert-Rejected { & $manager -Mode Write -CatalogPath ($fixtureRelative + '/missing-stage.json') -OutputPath ($fixtureRelative + '/bad.md') } 'CommonStageMissing:E1'
$badCatalog = Copy-Fixture $catalog
$badCatalog.modules[0].PSObject.Properties.Remove('outputs')
Write-Fixture 'missing-outputs.json' ($badCatalog | ConvertTo-Json -Depth 50) | Out-Null
Assert-Rejected { & $manager -Mode Write -CatalogPath ($fixtureRelative + '/missing-outputs.json') -OutputPath ($fixtureRelative + '/bad.md') } 'ModuleContractMissing'
$missingScope = Copy-Fixture $evidence
$missingScope.packages[0].PSObject.Properties.Remove('presentationModuleScope')
Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $missingScope } 'EvidenceModuleScopeMissing'
$wrongModule = Copy-Fixture $evidence
$wrongModule.packages[0].presentationModuleScope.moduleCodes = @('unrelated')
Assert-Rejected { Test-PresentationModuleBindings $passed $catalog $repositoryRoot '' $wrongModule } 'EvidenceScopeMismatch'
Write-Output "PresentationModuleBindingTestsPassed:Cases=$script:bindingCases;SyntheticEvidenceOnly=True"
