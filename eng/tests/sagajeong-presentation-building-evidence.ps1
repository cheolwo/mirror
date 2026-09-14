[CmdletBinding()]
param(
    [string] $PythonExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manager = Join-Path $repositoryRoot 'eng/neighborhood/manage-sagajeong-presentation-building-evidence.ps1'
$testRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local/validation/sagajeong-presentation-building-evidence-tests'))
$runDirectory = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
$ledgerNames = @('presentation-building-bindings.json', 'presentation-building-addresses.json')

function Require($Condition, [string] $Code) {
    if (-not $Condition) { throw "SagajeongPresentationBuildingEvidenceTest:$Code" }
}

function Require-Rejection([scriptblock] $Action, [string] $ExpectedMessage, [string] $Code) {
    $message = ''
    try {
        & $Action | Out-Null
    }
    catch {
        $message = $_.Exception.Message
    }
    Require (-not [string]::IsNullOrWhiteSpace($message)) "$Code`:Accepted"
    Require ($message -like "*$ExpectedMessage*") "$Code`:WrongFailure:$message"
}

function Copy-Ledgers([string] $Source, [string] $Destination) {
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($name in $ledgerNames) {
        [IO.File]::Copy((Join-Path $Source $name), (Join-Path $Destination $name), $false)
    }
}

function Require-ByteEqual([string] $First, [string] $Second, [string] $Code) {
    $firstBytes = [IO.File]::ReadAllBytes($First)
    $secondBytes = [IO.File]::ReadAllBytes($Second)
    Require ($firstBytes.Length -eq $secondBytes.Length) "$Code`:Length"
    Require ([Collections.StructuralComparisons]::StructuralEqualityComparer.Equals($firstBytes, $secondBytes)) "$Code`:Bytes"
}

function Remove-OwnedRunDirectory([string] $Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $prefix = $testRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SagajeongPresentationBuildingEvidenceTest:UnsafeCleanupTarget:$resolved"
    }
    if ([IO.Directory]::Exists($resolved)) {
        [IO.Directory]::Delete($resolved, $true)
    }
}

if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $PythonExecutable = if ($null -ne $pythonCommand) {
        $pythonCommand.Source
    }
    else {
        Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    }
}
Require (Test-Path -LiteralPath $PythonExecutable -PathType Leaf) 'PythonRuntimeMissing'
Require (Test-Path -LiteralPath $manager -PathType Leaf) 'ManagerMissing'

$expectedBindingContentHash = '7B47519C1FC9D4D89A04B734710F48FE64BFE8C0EEC9264C947D5DC6206D1411'
$expectedAddressContentHash = '9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33'
$expectedBindingFileHash = 'A11460EB08BA3D27188AE2BD5FFA7E3BCA22861140EB2973EE869078B5F17149'
$expectedAddressFileHash = 'E751344083E98C28E3F4D6B153FA71E7679A7C47534D71B9721DEBE467596984'
$firstDirectory = Join-Path $runDirectory 'build-a'
$secondDirectory = Join-Path $runDirectory 'build-b'
$contentTamperDirectory = Join-Path $runDirectory 'tamper-content-hash'
$authorityTamperDirectory = Join-Path $runDirectory 'tamper-consumer-authority'

[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
try {
    $outsidePath = Join-Path $repositoryRoot 'eng/forbidden-presentation-building-evidence-test'
    Require-Rejection {
        & $manager -Mode Build -OutputDirectory $outsidePath -PythonExecutable $PythonExecutable
    } 'OutputMustRemainUnderArtifactsLocal' 'OutsideOutputBoundary'
    Require (-not [IO.Directory]::Exists($outsidePath)) 'OutsideOutputWasCreated'

    $first = & $manager -Mode Build -OutputDirectory $firstDirectory -PythonExecutable $PythonExecutable | ConvertFrom-Json
    Require ($first.status -ceq 'BuiltAndValidated' -and -not $first.networkRequested) 'FirstBuild'
    $second = & $manager -Mode Build -OutputDirectory $secondDirectory -PythonExecutable $PythonExecutable | ConvertFrom-Json
    Require ($second.status -ceq 'BuiltAndValidated' -and -not $second.networkRequested) 'SecondBuild'

    Require ($first.stationStableId -ceq 'station:kr:kric:s1107:0722') 'StationStableId'
    Require ($first.bindingRevision -ceq 'sagajeong-presentation-building-binding.r1') 'BindingRevision'
    Require ($first.addressRevision -ceq 'sagajeong-presentation-building-address-assignment.r1') 'AddressRevision'
    Require ($first.bindingContentSha256 -ceq $expectedBindingContentHash) 'BindingContentHash'
    Require ($first.addressContentSha256 -ceq $expectedAddressContentHash) 'AddressContentHash'
    Require ($first.bindingFileSha256 -ceq $expectedBindingFileHash) 'BindingFileHash'
    Require ($first.addressFileSha256 -ceq $expectedAddressFileHash) 'AddressFileHash'
    Require ($first.presentationBuildingCount -eq 4062 -and $first.parcelIdentifierCount -eq 4062) 'PresentationCount'
    Require ($first.bindingCount -eq 544 -and $first.uniqueParcelIdentifierCount -eq 3774) 'BindingAndPnuCounts'
    Require (
        $first.rawParcelAddressCandidates.single -eq 3796 -and
        $first.rawParcelAddressCandidates.multiple -eq 60 -and
        $first.rawParcelAddressCandidates.none -eq 206
    ) 'CandidatePartition'
    Require (
        $first.officialPromotedCount -eq 0 -and
        $first.consumerUseAuthorizedCount -eq 0 -and
        -not $first.parcelGeometryCollected
    ) 'AuthorityBoundary'

    foreach ($name in $ledgerNames) {
        Require-ByteEqual (Join-Path $firstDirectory $name) (Join-Path $secondDirectory $name) "Determinism:$name"
    }
    $validation = & $manager -Mode Validate -OutputDirectory $firstDirectory -PythonExecutable $PythonExecutable | ConvertFrom-Json
    Require ($validation.status -ceq 'Validated') 'ValidateMode'
    $summary = & $manager -Mode Summary -OutputDirectory $firstDirectory -PythonExecutable $PythonExecutable | ConvertFrom-Json
    Require ($summary.status -ceq 'Summary' -and $summary.addressContentSha256 -ceq $expectedAddressContentHash) 'SummaryMode'

    Copy-Ledgers $firstDirectory $contentTamperDirectory
    $contentTamperPath = Join-Path $contentTamperDirectory 'presentation-building-addresses.json'
    $contentText = [IO.File]::ReadAllText($contentTamperPath, [Text.Encoding]::UTF8)
    $tamperedContentText = $contentText.Replace('"evidenceAsOf": "2026-08-31"', '"evidenceAsOf": "2026-08-30"')
    Require ($tamperedContentText -cne $contentText) 'ContentTamperFixture'
    [IO.File]::WriteAllText($contentTamperPath, $tamperedContentText, [Text.UTF8Encoding]::new($false))
    Require-Rejection {
        & $manager -Mode Validate -OutputDirectory $contentTamperDirectory -PythonExecutable $PythonExecutable
    } 'ContentHashMismatch:presentation-building-addresses.json' 'ContentHashTamper'

    Copy-Ledgers $firstDirectory $authorityTamperDirectory
    $authorityTamperPath = Join-Path $authorityTamperDirectory 'presentation-building-addresses.json'
    $authorityMutation = @'
import hashlib
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
document = json.loads(path.read_text(encoding="utf-8"))
document["assignments"][0]["distributionApproved"] = True
document.pop("contentSha256", None)
payload = json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")
document["contentSha256"] = hashlib.sha256(payload).hexdigest().upper()
with path.open("w", encoding="utf-8", newline="\n") as stream:
    json.dump(document, stream, ensure_ascii=False, indent=2, allow_nan=False)
    stream.write("\n")
'@
    & $PythonExecutable -B -c $authorityMutation $authorityTamperPath
    Require ($LASTEXITCODE -eq 0) 'AuthorityTamperFixture'
    Require-Rejection {
        & $manager -Mode Validate -OutputDirectory $authorityTamperDirectory -PythonExecutable $PythonExecutable
    } 'ConsumerAuthorityLeak:*:distributionApproved' 'ConsumerAuthorityTamper'

    Write-Output (
        'PASS Sagajeong presentation-building evidence tooling; ' +
        'modes=Build,Validate,Summary; deterministicByteBuilds=2; tamperRejections=2; ' +
        "presentationBuildings=4062; bindings=544; parcelIdentifiers=4062; uniquePnu=3774; " +
        "bindingContentSha256=$expectedBindingContentHash; addressContentSha256=$expectedAddressContentHash; " +
        'officialPromoted=0; consumerUseAuthorized=0; parcelGeometryCollected=false; networkRequested=false'
    )
}
finally {
    Remove-OwnedRunDirectory $runDirectory
}
