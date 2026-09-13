[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repositoryRoot 'eng/world-seedbeds/station-landmarks/sagajeong-public-photo.collection.r2.json'
$planPath = Join-Path $repositoryRoot 'docs/AI/Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/public-photo-collection-result.r11.md'
$sourcePath = Join-Path $repositoryRoot 'eng/Ssalddel.PublicDataPortalImport/사가정공공사진자료.cs'
$privateFolder = Join-Path $repositoryRoot 'artifacts/local/public-data/sagajeong-public-photos-20260913-r2'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "StationPublicPhotoCollectionTest:$code" }
}

Require (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'ManifestMissing'
Require (Test-Path -LiteralPath $planPath -PathType Leaf) 'PlanMissing'
Require (Test-Path -LiteralPath $sourcePath -PathType Leaf) 'CollectorSourceMissing'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ($manifest.schemaVersion -ceq 'ssalddel.station-public-photo-collection.v1') 'SchemaVersion'
Require ($manifest.revision -ceq 'sagajeong-public-photo-collection.r2') 'Revision'
Require ($manifest.scope.downloadedImageCount -eq 5) 'DownloadedImageCount'
Require ($manifest.scope.exactMarketImageCount -eq 0) 'ExactMarketImageCount'
Require ($manifest.scope.koglMetadataOnlyCount -eq 3) 'KoglMetadataOnlyCount'

$images = @($manifest.imageCandidates)
Require ($images.Count -eq 5) 'ImageCandidateCount'
Require (@($images | Where-Object licenseCode -ceq 'PublicDomain').Count -eq 3) 'PublicDomainCount'
Require (@($images | Where-Object licenseCode -ceq 'CC-BY-SA-4.0').Count -eq 2) 'CcBySaCount'
Require (@($images | Where-Object { $_.modelDerivationAllowed -or $_.gameDistributionAllowed }).Count -eq 0) 'PrematureImagePromotion'
Require (@($images | Where-Object { $_.sha256 -notmatch '^[0-9a-f]{64}$' }).Count -eq 0) 'ImageHash'

Require ($manifest.exactMarketSearch.resultCode -ceq 'MissingVisualEvidence') 'MarketMissingStatus'
Require (-not $manifest.exactMarketSearch.fallbackGenerated) 'MarketFallbackWasGenerated'
Require (-not $manifest.exactMarketSearch.officialPublicationReference.itemLevelModificationAndDistributionRightsVerified) 'PublicationRightsOverclaim'
Require (@($manifest.koglRightsBlockedCandidates | Where-Object { $_.licenseCode -cne 'KOGL-Type4' -or $_.fileDownloaded }).Count -eq 0) 'KoglType4Boundary'

$gates = $manifest.handoffGates
Require ($gates.blender -ceq 'Blocked' -and $gates.unity -ceq 'Blocked') 'BlenderUnityGate'
Require (-not $gates.distributionApproved -and -not $gates.sceneReady -and -not $gates.isExecutionAuthority) 'AuthorityBoundary'
Require ($manifest.persistenceEvidence.firstApplyInserted -eq 9 -and $manifest.persistenceEvidence.secondApplyInserted -eq 0 -and $manifest.persistenceEvidence.independentVerifyCount -eq 9) 'PersistenceEvidence'

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($required in @('PrivateReviewOnly', 'NoBlender', 'NoUnity', 'NoDistribution', 'upload.wikimedia.org', 'SagajeongPhotoResponseTooLarge', 'GET_LOCK')) {
    Require ($source.Contains($required)) "CollectorGuardMissing:$required"
}

if (Test-Path -LiteralPath $privateFolder -PathType Container) {
    $receiptPath = Join-Path $privateFolder 'acquisition.json'
    Require (Test-Path -LiteralPath $receiptPath -PathType Leaf) 'LocalReceiptMissing'
    $receiptHash = (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Require ($receiptHash -ceq $manifest.acquisition.receiptSha256) 'LocalReceiptHash'
    foreach ($image in $images) {
        $path = Join-Path $privateFolder $image.fileName
        Require (Test-Path -LiteralPath $path -PathType Leaf) "LocalImageMissing:$($image.fileName)"
        Require ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $image.sha256) "LocalImageHash:$($image.fileName)"
    }
}

Write-Output 'PASS station public photo collection; five private source files, exact-market gap, KOGL Type 4 blocks, DB evidence and Blender/Unity gates verified'
