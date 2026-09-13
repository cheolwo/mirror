[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/local/neighborhood-source-acquisition/yongmasan-station-0723-r1',
    [switch] $VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sourceUrl = 'https://api.openstreetmap.org/api/0.6/map?bbox=127.0811416,37.5692469,127.0924624,37.5782568'
$bbox = [ordered]@{
    minLongitude = 127.0811416
    minLatitude = 37.5692469
    maxLongitude = 127.0924624
    maxLatitude = 37.5782568
}

function Require($condition, [string] $code) {
    if (-not $condition) { throw "YongmasanStationOsmAcquisition:$code" }
}

function Resolve-LocalOutput([string] $value) {
    $candidate = if ([IO.Path]::IsPathRooted($value)) {
        [IO.Path]::GetFullPath($value)
    } else {
        [IO.Path]::GetFullPath((Join-Path $repositoryRoot $value))
    }
    $localRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local'))
    Require ($candidate.StartsWith($localRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'OutputMustRemainUnderArtifactsLocal'
    return $candidate
}

function Read-Receipt([string] $path) {
    try {
        return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        throw "YongmasanStationOsmAcquisition:ReceiptInvalid"
    }
}

function Assert-FrozenPair([string] $rawPath, [string] $receiptPath) {
    Require ([IO.File]::Exists($rawPath)) 'RawMissing'
    Require ([IO.File]::Exists($receiptPath)) 'ReceiptMissing'
    $receipt = Read-Receipt $receiptPath
    Require ($receipt.schemaVersion -ceq 'station-osm-source-acquisition.v1') 'ReceiptSchemaMismatch'
    Require ($receipt.transitStationStableId -ceq 'station:kr:kric:s1107:0723') 'ReceiptStationMismatch'
    Require ($receipt.sourceUrl -ceq $sourceUrl) 'ReceiptUrlMismatch'
    Require ([double]$receipt.bbox.minLongitude -eq $bbox.minLongitude -and
             [double]$receipt.bbox.minLatitude -eq $bbox.minLatitude -and
             [double]$receipt.bbox.maxLongitude -eq $bbox.maxLongitude -and
             [double]$receipt.bbox.maxLatitude -eq $bbox.maxLatitude) 'ReceiptBboxMismatch'
    $raw = Get-Item -LiteralPath $rawPath
    Require ([int64]$receipt.raw.byteLength -eq $raw.Length) 'RawLengthMismatch'
    $hash = (Get-FileHash -LiteralPath $rawPath -Algorithm SHA256).Hash
    Require ($receipt.raw.sha256 -ceq $hash) 'RawHashMismatch'
    $head = Get-Content -LiteralPath $rawPath -Encoding UTF8 -TotalCount 4 | Out-String
    Require ($head -like '*<osm version="0.6"*') 'RawOsmRootMissing'
    Require ($head -like '*OpenStreetMap and contributors*') 'RawAttributionMissing'
    Require ($head -like '*odbl/1-0*') 'RawLicenseMissing'
    return [ordered]@{
        status = 'ReusedFrozenSource'
        sourceUrl = $sourceUrl
        rawPath = $rawPath
        receiptPath = $receiptPath
        byteLength = $raw.Length
        sha256 = $hash
        networkRequested = $false
    }
}

$targetDirectory = Resolve-LocalOutput $OutputDirectory
$rawPath = Join-Path $targetDirectory 'map.osm'
$receiptPath = Join-Path $targetDirectory 'receipt.json'
$rawExists = [IO.File]::Exists($rawPath)
$receiptExists = [IO.File]::Exists($receiptPath)
Require ($rawExists -eq $receiptExists) 'FrozenPairIncomplete'

if ($rawExists) {
    Assert-FrozenPair $rawPath $receiptPath | ConvertTo-Json -Depth 8
    exit 0
}
Require (-not $VerifyOnly) 'VerifyOnlySourceMissing'

[IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
$rawPending = Join-Path $targetDirectory '.map.osm.pending'
$receiptPending = Join-Path $targetDirectory '.receipt.json.pending'
Require (-not [IO.File]::Exists($rawPending) -and -not [IO.File]::Exists($receiptPending)) 'PendingFileAlreadyExists'

$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(90)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('Ssalddel-LocalSpatialReview/1.0')
try {
    $response = $client.GetAsync($sourceUrl, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    Require ($response.IsSuccessStatusCode) "HttpStatus:$([int]$response.StatusCode)"
    $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    Require ($bytes.Length -gt 100 -and $bytes.Length -le 100MB) 'ResponseLengthInvalid'
    $textHead = [Text.Encoding]::UTF8.GetString($bytes, 0, [Math]::Min($bytes.Length, 2048))
    Require ($textHead -like '*<osm version="0.6"*') 'ResponseOsmRootMissing'
    Require ($textHead -like '*OpenStreetMap and contributors*') 'ResponseAttributionMissing'
    Require ($textHead -like '*odbl/1-0*') 'ResponseLicenseMissing'
    Require ($textHead -like '*minlat="37.5692469"*' -and
             $textHead -like '*minlon="127.0811416"*' -and
             $textHead -like '*maxlat="37.5782568"*' -and
             $textHead -like '*maxlon="127.0924624"*') 'ResponseBoundsMismatch'

    [IO.File]::WriteAllBytes($rawPending, $bytes)
    $hash = (Get-FileHash -LiteralPath $rawPending -Algorithm SHA256).Hash
    $receipt = [ordered]@{
        schemaVersion = 'station-osm-source-acquisition.v1'
        transitStationStableId = 'station:kr:kric:s1107:0723'
        stationName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('7Jqp66eI7IKw7Jet'))
        scope = 'SingleOfficialApiBboxGet'
        sourceUrl = $sourceUrl
        fetchedAtUtc = [DateTime]::UtcNow.ToString('O')
        bbox = $bbox
        raw = [ordered]@{
            file = 'map.osm'
            byteLength = $bytes.Length
            sha256 = $hash
            format = 'OSM XML 0.6'
            originalCrs = 'EPSG:4326'
        }
        license = [ordered]@{
            code = 'ODbL-1.0'
            attribution = ([char]0x00A9 + ' OpenStreetMap contributors')
            copyrightUrl = 'https://www.openstreetmap.org/copyright'
            licenseUrl = 'https://opendatacommons.org/licenses/odbl/1-0/'
        }
        policy = [ordered]@{
            reviewStatus = 'LocalPrivateReview'
            distributionApproved = $false
            gameplayReady = $false
            traversalReady = $false
            periodicAcquisition = $false
            relationMembersMayBeIncomplete = $true
        }
    }
    [IO.File]::WriteAllText(
        $receiptPending,
        (($receipt | ConvertTo-Json -Depth 8) + "`n"),
        [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($rawPending, $rawPath)
    [IO.File]::Move($receiptPending, $receiptPath)
    [ordered]@{
        status = 'DownloadedFrozenSource'
        sourceUrl = $sourceUrl
        rawPath = $rawPath
        receiptPath = $receiptPath
        byteLength = $bytes.Length
        sha256 = $hash
        networkRequested = $true
    } | ConvertTo-Json -Depth 8
}
finally {
    $client.Dispose()
    if ([IO.File]::Exists($rawPending)) { [IO.File]::Delete($rawPending) }
    if ([IO.File]::Exists($receiptPending)) { [IO.File]::Delete($receiptPending) }
}
