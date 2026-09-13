[CmdletBinding()]
param([string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Require($ok, $code) { if (-not $ok) { throw "BuildingLedgerAudit:$code" } }
$folder = Join-Path $RepositoryRoot 'artifacts/local/public-data/sagajeong-building-ledger-r1'
$mapPath = 'C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json'
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $folder 'manifest.json') | ConvertFrom-Json
foreach ($file in $manifest.files) {
    Require ($file.name -in @('ledger.json','gis-candidates.json','selection.json')) 'ManifestPath'
    Require ((Get-FileHash (Join-Path $folder $file.name) -Algorithm SHA256).Hash -ceq $file.sha256) 'FrozenHash'
}
$map = Get-Content -Raw -Encoding UTF8 $mapPath | ConvertFrom-Json
$ledger = Get-Content -Raw -Encoding UTF8 (Join-Path $folder 'ledger.json') | ConvertFrom-Json
$gis = Get-Content -Raw -Encoding UTF8 (Join-Path $folder 'gis-candidates.json') | ConvertFrom-Json
$sourcePath = Join-Path $RepositoryRoot 'artifacts/local/public-data/myeonmok-business-20260908-r1/connection.json'
Require ((Get-FileHash $sourcePath -Algorithm SHA256).Hash -eq 'D109A0AF26608E9627551600B37FE8B22F473893D5F6FEA542E83E5D1FD176E2') 'SourceDrift'
$source = Get-Content -Raw -Encoding UTF8 $sourcePath | ConvertFrom-Json
Require ((Get-FileHash $mapPath -Algorithm SHA256).Hash -ceq $ledger.mapHash) 'MapDrift'
Require ($map.revision -ceq $ledger.mapRevision) 'MapRevision'
Require ($ledger.buildings.Count -eq 602 -and $map.buildings.Count -eq 602) 'Count'
$mapById = @{}; foreach ($b in $map.buildings) { Require (-not $mapById.ContainsKey($b.id)) 'MapDuplicate'; $mapById[$b.id] = $b }
$sourceById = @{}; foreach ($link in $source.links) { $sourceById[$link.item.id] = $link }
$gisByIndex = @{}; foreach ($g in $gis.Rows) { Require (-not $gisByIndex.ContainsKey($g.Index)) 'GisDuplicate'; $gisByIndex[$g.Index] = $g }
$seen = @{}; $linked = 0; $withGis = 0; $sourceHeight = 0
foreach ($row in $ledger.buildings) {
    Require ($mapById.ContainsKey($row.buildingId) -and -not $seen.ContainsKey($row.buildingId)) 'BuildingIdentity'
    $seen[$row.buildingId] = $true; $original = $mapById[$row.buildingId]
    Require ($row.currentHeight -eq $original.height -and $row.currentHeightKind -ceq $original.heightKind) 'HeightChanged'
    Require ($row.rawLevels -ceq $original.levelsText) 'LevelsChanged'
    Require (-not $row.unityApplyAllowed -and $null -eq $row.proposedHeight) 'ApplicationLeak'
    $expectedRefs = @($source.links | Where-Object { $_.buildingIds -ccontains $row.buildingId } | ForEach-Object { $_.item.id } | Sort-Object)
    Require (($expectedRefs -join '|') -ceq (($row.sourceRecordIds | Sort-Object) -join '|')) 'AddressLinkCoverage'
    if ($row.sourceRecordIds.Count) { $linked++ }
    if ($row.currentHeightKind -ceq 'OsmHeightTag') { $sourceHeight++ }
    if ($row.gisCandidates.Count) { $withGis++ }
    $expectedGis = @($gis.Rows | Where-Object { $row.lotCandidates -ccontains $_.Fields.A2 } | ForEach-Object { $_.Index } | Sort-Object)
    $actualGis = @($row.gisCandidates | ForEach-Object { $_.sourceRecordIndex } | Sort-Object)
    Require (($expectedGis -join '|') -ceq ($actualGis -join '|')) 'GisCoverage'
    foreach ($candidate in $row.gisCandidates) {
        Require ($gisByIndex.ContainsKey($candidate.sourceRecordIndex)) 'GisRowMissing'
        $g = $gisByIndex[$candidate.sourceRecordIndex]
        Require ($candidate.gisId -ceq $g.Fields.A1 -and $candidate.lotKey -ceq $g.Fields.A2) 'GisIdentity'
        Require ($candidate.rawHeight -ceq $g.Fields.A16 -and $candidate.rawAboveGroundFloors -ceq $g.Fields.A26) 'GisFields'
        Require ($candidate.relation -ceq 'SameParcelCandidateNotBuildingIdentity' -and $candidate.rightsStatus -ceq $gis.RightsStatus) 'CandidateBoundary'
    }
}
Require ($linked -eq $ledger.summary.withCollectedCandidates -and $withGis -eq $ledger.summary.withGisParcelCandidates -and $sourceHeight -eq $ledger.summary.sourceHeight) 'Summary'
[pscustomobject]@{ Passed=$true; Buildings=$seen.Count; AddressCandidateBuildings=$linked; GisParcelCandidateBuildings=$withGis; SourceHeightBuildings=$sourceHeight; HeightChanges=0; DatabaseVerified=$false; UnityExecuted=$false }
