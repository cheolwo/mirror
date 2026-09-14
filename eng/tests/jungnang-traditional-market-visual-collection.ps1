[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repositoryRoot 'eng/world-seedbeds/station-landmarks/jungnang-traditional-market-visual.collection.r1.json'
$planPath = Join-Path $repositoryRoot 'docs/AI/Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/jungnang-traditional-market-visual-collection.implementation.r24.md'
$reportPath = Join-Path $repositoryRoot 'docs/Reports/중랑구-전통시장-시각자료-첫수집-2026-09-14.md'
$sourcePath = Join-Path $repositoryRoot 'eng/Ssalddel.PublicDataPortalImport/중랑구전통시장시각자료.cs'
$privateRelativeFolder = 'artifacts/local/public-data/jungnang-traditional-market-visuals-20260914-r1'
$privateFolder = Join-Path $repositoryRoot $privateRelativeFolder

function Require($condition, [string] $code) {
    if (-not $condition) { throw "JungnangTraditionalMarketVisualCollectionTest:$code" }
}

foreach ($path in @($manifestPath, $planPath, $reportPath, $sourcePath)) {
    Require (Test-Path -LiteralPath $path -PathType Leaf) "RequiredFileMissing:$path"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ($manifest.schemaVersion -ceq 'ssalddel.station-landmark-visual-collection.v1') 'SchemaVersion'
Require ($manifest.revision -ceq 'jungnang-traditional-market-visual-collection.r1') 'Revision'
Require ($manifest.status -ceq 'PrivateReviewCollectedIdentityAndRightsPending') 'Status'
Require (@($manifest.marketPackets).Count -eq 5) 'MarketPacketCount'
Require ((@($manifest.marketPackets.currentOfficialRowCandidates).Count) -eq 7) 'OfficialRowCandidateCount'
Require (@($manifest.marketPackets | Where-Object { $_.identityReviewCode -notlike '*Pending*' }).Count -eq 0) 'IdentityPrematurelyResolved'

$myeonmok = @($manifest.marketPackets | Where-Object historicalDisplayName -ceq '면목시장')
$dongwon = @($manifest.marketPackets | Where-Object historicalDisplayName -ceq '동원시장')
Require ($myeonmok.Count -eq 1 -and @($myeonmok[0].currentOfficialRowCandidates).Count -eq 2) 'MyeonmokIdentityCandidates'
Require ($dongwon.Count -eq 1 -and @($dongwon[0].currentOfficialRowCandidates).Count -eq 2) 'DongwonIdentityCandidates'

$rights = $manifest.rightsBoundary
Require ($rights.officialPublication) 'OfficialPublicationMissing'
Require (-not $rights.itemLevelCommercialUseVerified -and -not $rights.itemLevelModificationVerified) 'ItemRightsOverclaim'
Require ($rights.privateReviewOnly -and -not $rights.blenderWorkAuthorized -and -not $rights.unityRuntimeAuthorized -and -not $rights.distributionApproved) 'RightsGate'
Require ($manifest.handoffGates.blender -ceq 'Blocked' -and $manifest.handoffGates.unity -ceq 'Blocked') 'HandoffGate'
Require ($manifest.persistenceEvidence.firstApplyInserted -eq 11) 'FirstApplyCount'
Require ($manifest.persistenceEvidence.secondApplyInserted -eq 0 -and $manifest.persistenceEvidence.secondApplyExisting -eq 11) 'IdempotentApply'
Require ($manifest.persistenceEvidence.independentVerifyCount -eq 11) 'IndependentVerifyCount'

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($required in @('ItemLevelRightsUnverified', 'NoBlender', 'NoUnity', 'NoDistribution', 'GET_LOCK', 'JungnangMarketVisualResponseTooLarge')) {
    Require ($source.Contains($required)) "CollectorGuardMissing:$required"
}

$trackedRaw = @(& git -C $repositoryRoot ls-files -- $privateRelativeFolder)
Require ($LASTEXITCODE -eq 0 -and $trackedRaw.Count -eq 0) 'PrivateRawTrackedByGit'

if (Test-Path -LiteralPath $privateFolder -PathType Container) {
    $receiptPath = Join-Path $privateFolder 'acquisition.json'
    Require (Test-Path -LiteralPath $receiptPath -PathType Leaf) 'LocalReceiptMissing'
    Require ((Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $manifest.acquisition.receiptSha256) 'LocalReceiptHash'
    foreach ($packet in @($manifest.marketPackets)) {
        $panelPath = Join-Path $repositoryRoot ([string] $packet.panel.privateRelativePath)
        Require (Test-Path -LiteralPath $panelPath -PathType Leaf) "LocalPanelMissing:$($packet.historicalDisplayName)"
        Require ((Get-FileHash -LiteralPath $panelPath -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $packet.panel.sha256) "LocalPanelHash:$($packet.historicalDisplayName)"
    }
}

Write-Output 'PASS Jungnang traditional market visual collection; five private panels, seven current identity candidates, DB idempotency and Blender/Unity rights gates verified'
