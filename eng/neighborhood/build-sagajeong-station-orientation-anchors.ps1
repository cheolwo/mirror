[CmdletBinding()]
param(
    [string]$RawPath = 'artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string]$MapPath = 'C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json',
    [string]$OutputPath = 'C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongStationOrientationAnchors.json'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$raw = Join-Path $root $RawPath
$expectedRawHash = '3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3'
$expectedMapRevision = 'sagajeong-reference.r3'
$stationStableId = 'station:kr:kric:s1107:0722'

function Require-AnchorData($condition, [string]$code) {
    if (-not $condition) { throw "SagajeongStationOrientationAnchorInvalid:$code" }
}

Require-AnchorData (Test-Path -LiteralPath $raw) 'RawSourceMissing'
Require-AnchorData (Test-Path -LiteralPath $MapPath) 'MapMissing'
$rawHash = (Get-FileHash -LiteralPath $raw -Algorithm SHA256).Hash
Require-AnchorData ($rawHash -eq $expectedRawHash) 'RawSourceHashMismatch'

$mapBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $MapPath).Path)
$mapHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($mapBytes))
$map = [Text.Encoding]::UTF8.GetString($mapBytes) | ConvertFrom-Json
Require-AnchorData ($map.revision -eq $expectedMapRevision) 'MapRevisionMismatch'
Require-AnchorData ($map.rawSha256 -eq $expectedRawHash) 'MapSourceMismatch'

[xml]$osm = Get-Content -LiteralPath $raw -Raw -Encoding UTF8
$sourceNodes = @()
foreach ($node in $osm.osm.node) {
    $tags = @{}
    foreach ($tag in @($node.tag)) { $tags[[string]$tag.k] = [string]$tag.v }
    if ($tags.railway -eq 'subway_entrance' -and $tags.ref -in @('1', '2', '3', '4')) {
        $sourceNodes += [pscustomobject]@{
            id = [string]$node.id
            version = [int]$node.version
            exitNumber = [int]$tags.ref
            latitude = [double]$node.lat
            longitude = [double]$node.lon
        }
    }
}

Require-AnchorData ($sourceNodes.Count -eq 4) 'ExpectedFourExits'
Require-AnchorData (@($sourceNodes.exitNumber | Sort-Object -Unique) -join ',' -eq '1,2,3,4') 'ExitNumberCoverage'
Require-AnchorData (@($sourceNodes.id | Sort-Object -Unique).Count -eq 4) 'SourceNodeIdentity'

. (Join-Path $root 'eng/world-seedbeds/NeighborhoodGeographicProjection.ps1')
$projection = [pscustomobject]@{
    coordinateMethod = [string]$map.coordinateMethod
    originLatitude = [double]$map.originLatitude
    originLongitude = [double]$map.originLongitude
    offsetX = [double]$map.offsetX
    offsetZ = [double]$map.offsetZ
    halfExtentMeters = [double]$map.halfExtent
    metersPerUnit = 1
    rawSha256 = $rawHash
    features = @($sourceNodes | ForEach-Object {
        [pscustomobject]@{
            id = 'osm:node:' + $_.id
            name = [string]$_.exitNumber
            latitude = $_.latitude
            longitude = $_.longitude
        }
    })
}
$projectedById = @{}
foreach ($item in @(Get-NeighborhoodMarketPoints $projection)) { $projectedById[$item.id] = $item }

$anchors = @($sourceNodes | Sort-Object exitNumber | ForEach-Object {
    $sourceId = 'osm:node:' + $_.id
    Require-AnchorData ($projectedById.ContainsKey($sourceId)) 'ProjectedExitMissing'
    $point = $projectedById[$sourceId]
    [ordered]@{
        stableId = "orientation-anchor:station:kr:kric:s1107:0722:exit:$($_.exitNumber)"
        kindCode = 'SubwayExit'
        displayNameKo = "사가정역 $($_.exitNumber)번 출구"
        exitNumber = $_.exitNumber
        sourceNodeId = $sourceId
        sourceNodeVersion = $_.version
        latitude = $_.latitude
        longitude = $_.longitude
        x = $point.x
        z = $point.z
        directionBasisCode = 'TowardStationReferenceOnly'
        placementQualityCode = 'OsmSourceGeometryNotSurveyed'
        traversalReady = $false
        interactionReady = $false
        operationalAuthority = $false
    }
})

$output = [ordered]@{
    schemaVersion = 'station-diorama-orientation-anchors.v1'
    revision = 'sagajeong-station-exits.r1'
    stationStableId = $stationStableId
    mapRevision = $expectedMapRevision
    mapSha256 = $mapHash
    sourceUrl = 'https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854'
    sourceRawSha256 = $rawHash
    attribution = '© OpenStreetMap contributors · ODbL 1.0'
    licenseUrl = 'https://www.openstreetmap.org/copyright'
    coordinateMethod = [string]$map.coordinateMethod
    originLatitude = [double]$map.originLatitude
    originLongitude = [double]$map.originLongitude
    offsetX = [double]$map.offsetX
    offsetZ = [double]$map.offsetZ
    authorityBoundary = 'ReferenceOnly;NoNavigation;NoTraversal;NoInteraction;NoOperationalAuthority;GeneralizedProceduralAppearance'
    anchors = $anchors
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) { $null = New-Item -ItemType Directory -Force -Path $outputDirectory }
[IO.File]::WriteAllText($OutputPath, ($output | ConvertTo-Json -Depth 8 -Compress), [Text.UTF8Encoding]::new($false))
"anchors=$($anchors.Count) revision=$($output.revision) mapSha256=$mapHash output=$OutputPath"
