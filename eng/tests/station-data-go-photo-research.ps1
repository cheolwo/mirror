[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repositoryRoot 'eng/world-seedbeds/station-landmarks/sagajeong-data-go-kr-photo-research.collection.r1.json'
$planPath = Join-Path $repositoryRoot 'docs/AI/Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/data-go-kr-photo-research-result.r12.md'
$sourcePath = Join-Path $repositoryRoot 'eng/Ssalddel.PublicDataPortalImport/사가정공공데이터포털사진자료.cs'
$privateFolder = Join-Path $repositoryRoot 'artifacts/local/public-data/sagajeong-data-go-kr-photo-research-20260913-r3'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "StationDataGoPhotoResearchTest:$code" }
}

Require (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'ManifestMissing'
Require (Test-Path -LiteralPath $planPath -PathType Leaf) 'PlanMissing'
Require (Test-Path -LiteralPath $sourcePath -PathType Leaf) 'CollectorSourceMissing'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ($manifest.schemaVersion -ceq 'ssalddel.data-go-kr-photo-research-collection.v1') 'SchemaVersion'
Require ($manifest.revision -ceq 'sagajeong-data-go-kr-photo-research.r1') 'Revision'
Require (@($manifest.officialSources).Count -eq 3) 'OfficialSourceCount'
Require (@($manifest.providerPreviewCandidates).Count -eq 3) 'PreviewCount'
Require (@($manifest.providerPreviewCandidates | Where-Object { $_.modelDerivationAllowed -or $_.gameDistributionAllowed }).Count -eq 0) 'PrematurePromotion'
Require (@($manifest.providerPreviewCandidates | Where-Object { $_.sha256 -notmatch '^[0-9a-f]{64}$' }).Count -eq 0) 'PreviewHash'
Require (@($manifest.providerPreviewCandidates | Where-Object rightsState -like 'Rejected*').Count -eq 2) 'RightsBlockCount'
Require ($manifest.officialSources[2].rowCount -eq 1332 -and $manifest.officialSources[2].exactScopeRowCount -eq 0) 'ArticleScopeResult'
Require ($manifest.officialSources[2].licenseCode -ceq 'KOGL-Type4') 'ArticleLicenseBoundary'

$gates = $manifest.handoffGates
Require ($gates.downloadedPreviewImageCount -eq 3 -and $gates.modelingApprovedImageCount -eq 0) 'ImageGateCounts'
Require ($gates.tourismPhotoApi -ceq 'BlockedHttp403') 'ApiAccessGate'
Require ($gates.blender -ceq 'Blocked' -and $gates.unity -ceq 'Blocked') 'BlenderUnityGate'
Require (-not $gates.distributionApproved -and -not $gates.sceneReady -and -not $gates.isExecutionAuthority) 'AuthorityBoundary'
Require ($manifest.persistenceEvidence.firstApplyInserted -eq 6 -and $manifest.persistenceEvidence.secondApplyInserted -eq 0 -and $manifest.persistenceEvidence.independentVerifyCount -eq 6) 'PersistenceEvidence'

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($required in @('PrivateResearchOnly', 'NoBlender', 'NoUnity', 'NoDistribution', 'cdn.visitkorea.or.kr', 'SagajeongDataGoPhotoResponseTooLarge', 'GET_LOCK')) {
    Require ($source.Contains($required)) "CollectorGuardMissing:$required"
}

if (Test-Path -LiteralPath $privateFolder -PathType Container) {
    $receiptPath = Join-Path $privateFolder 'acquisition.json'
    Require (Test-Path -LiteralPath $receiptPath -PathType Leaf) 'LocalReceiptMissing'
    $receiptHash = (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Require ($receiptHash -ceq $manifest.acquisition.receiptSha256) 'LocalReceiptHash'
    foreach ($image in $manifest.providerPreviewCandidates) {
        $path = Join-Path $privateFolder $image.fileName
        Require (Test-Path -LiteralPath $path -PathType Leaf) "LocalImageMissing:$($image.fileName)"
        Require ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $image.sha256) "LocalImageHash:$($image.fileName)"
    }
}

Write-Output 'PASS station data.go.kr photo research; three private previews, API access block, rights gates and DB evidence verified'
