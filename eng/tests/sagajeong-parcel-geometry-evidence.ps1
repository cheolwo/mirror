[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manager = Join-Path $repositoryRoot 'eng/neighborhood/manage-sagajeong-parcel-geometry-evidence.ps1'
$builder = Join-Path $repositoryRoot 'eng/neighborhood/build-sagajeong-parcel-geometry-evidence.py'
$catalogManager = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-station-diorama-evidence-rules.ps1'

function Require($condition, [string] $code) {
    if (-not $condition) { throw "SagajeongParcelGeometryEvidenceTest:$code" }
}

Require (Test-Path -LiteralPath $manager -PathType Leaf) 'ManagerMissing'
Require (Test-Path -LiteralPath $builder -PathType Leaf) 'BuilderMissing'

$readiness = (& $manager -Mode Readiness | ConvertFrom-Json)
Require ([string] $readiness.status -ceq 'BlockedExternalAccess') 'BlockedStatus'
Require ([string] $readiness.blockerCode -ceq 'VWorldLoginRequiredAndNoOfficialArchiveAvailable') 'BlockerCode'
Require ([long] $readiness.presentationBuildingCount -eq 4062) 'PresentationPopulation'
Require ([long] $readiness.targetParcelCount -eq 3774) 'TargetParcelPopulation'
Require ([long] $readiness.geometryParcelCount -eq 0) 'NoInventedGeometry'
Require ([string] $readiness.collectionState -ceq 'NotCollected' -and [string] $readiness.coverageState -ceq 'Missing') 'MissingStatePreserved'
Require (-not [bool] $readiness.applicationAuthorized) 'NoApplicationAuthority'
Require (-not [bool] $readiness.networkRequested) 'NoHiddenNetworkFallback'

$selfTest = (& $manager -Mode SelfTest | ConvertFrom-Json)
Require ([string] $selfTest.status -ceq 'SelfTestPassed') 'SelfTestStatus'
Require ([long] $selfTest.tests -eq 10) 'SelfTestCount'

$summary = [string] (& $manager -Mode Summary)
Require ($summary.IndexOf('Status=BlockedExternalAccess', [StringComparison]::Ordinal) -ge 0) 'SummaryStatus'
Require ($summary.IndexOf('Target=3774;Geometry=0;Collection=NotCollected;Coverage=Missing;Authorized=False', [StringComparison]::Ordinal) -ge 0) 'SummaryBoundary'

$source = Get-Content -LiteralPath $builder -Raw -Encoding UTF8
foreach ($required in @('AL_D002_11', 'SOURCE_PNU_FIELD = "A1"', 'EPSG:5186', 'Polygon', 'MultiPolygon', 'archiveSha256', 'geometrySha256', 'applicationAuthorized')) {
    Require ($source.IndexOf($required, [StringComparison]::Ordinal) -ge 0) "BuilderContract:$required"
}

$catalogResult = [string] (& $catalogManager -Mode Validate)
Require ($catalogResult.IndexOf('Missing=1', [StringComparison]::Ordinal) -ge 0) 'CatalogStillMissingGeometry'

Write-Output 'PASS sagajeong parcel geometry readiness; official AL_D002/A1/EPSG:5186 contract, 3,774 target PNU, deterministic geometry audit, explicit external-access blocker and no invented geometry verified'
