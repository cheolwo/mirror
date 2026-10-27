#requires -Version 7.0
# Synthetic local counterexamples only. No Azure, databases, devices, APK signing or payment calls.
[CmdletBinding()]
param([string]$EvidencePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskValidator = Join-Path $PSScriptRoot 'Test-IntegratedPlatformReadiness.ps1'
$taskTemplate = Join-Path $PSScriptRoot 'integrated-platform-release.json'
$taskRepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskTemp = Join-Path ([IO.Path]::GetTempPath()) ('mirror-integrated-readiness-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($taskTemp)
$taskChecks = [Collections.Generic.List[object]]::new()
function WriteJson([string]$Path, $Value) {
    [void][IO.Directory]::CreateDirectory((Split-Path $Path -Parent))
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
}
function HashFile([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Fixture {
    $root = Join-Path $taskTemp ([Guid]::NewGuid().ToString('N')); [void][IO.Directory]::CreateDirectory((Join-Path $root 'src'))
    [IO.File]::WriteAllText((Join-Path $root 'src/Source.cs'), 'sealed class LocalFixture {}')
    $contract = Get-Content -LiteralPath $taskTemplate -Raw | ConvertFrom-Json -AsHashtable
    $contract.sourceRoots = @('src'); $contract.additionalSourcePaths = @(); $contract.releaseVersion = '0.2.0'; $contract.versionCode = 205
    foreach ($path in $contract.existingTools.Values) {
        $file = Join-Path $root $path; [void][IO.Directory]::CreateDirectory((Split-Path $file -Parent))
        [IO.File]::WriteAllText($file, 'LOCAL FORMAT FIXTURE; NO EXECUTION')
    }
    $profile = Get-Content -LiteralPath (Join-Path $taskRepoRoot $contract.preparationProfile) -Raw | ConvertFrom-Json -AsHashtable
    WriteJson (Join-Path $root 'manifest.json') $contract
    WriteJson (Join-Path $root $contract.preparationProfile) $profile
    $evidence = Join-Path $root 'evidence'; [void][IO.Directory]::CreateDirectory($evidence)
    [pscustomobject]@{ root = $root; contract = $contract; profile = $profile; evidence = $evidence }
}
function Readiness($Fixture) {
    & $taskValidator -RepositoryRoot $Fixture.root -ManifestPath 'manifest.json' -EvidenceDirectory $Fixture.evidence -AsObject
}
function Check([string]$Name, [scriptblock]$Body) { & $Body; $taskChecks.Add([ordered]@{ name = $Name; status = 'Passed' }) }
function Require($Value, [string]$Message) { if (-not $Value) { throw $Message } }
function LocalEvidence($Fixture) {
    $bindings = Readiness $Fixture
    $proof = [ordered]@{ releaseRevision = $Fixture.contract.releaseRevision; sourceSnapshotSha256 = $bindings.sourceSnapshotSha256;
        profileSha256 = $bindings.profileSha256; kind = 'local'; runId = 'local-format-fixture'; result = 'Passed'; synthetic = $false }
    WriteJson (Join-Path $Fixture.evidence 'build.json') $proof
    [IO.File]::WriteAllText((Join-Path $Fixture.evidence 'test.apk'), 'LOCAL FORMAT FIXTURE NOT AN INSTALLABLE APK')
    [IO.File]::WriteAllText((Join-Path $Fixture.evidence 'web.zip'), 'LOCAL FORMAT FIXTURE NOT A WEB RELEASE')
    $record = [ordered]@{
        schemaVersion = 'integrated-release-evidence.r1'; requirementId = 'local-release'; kind = 'local'
        releaseRevision = $Fixture.contract.releaseRevision; releaseVersion = $Fixture.contract.releaseVersion; versionCode = $Fixture.contract.versionCode
        sourceSnapshotSha256 = $bindings.sourceSnapshotSha256; contractSha256 = $bindings.contractSha256; profileSha256 = $bindings.profileSha256
        result = 'Passed'; runId = 'local-format-fixture'; synthetic = $false; executionMode = 'NoExecution'; roleBindings = $Fixture.contract.roles
        assertions = @{ sameReleaseBuilt = $true; allRoleChecksPassed = $true; signedApkChecked = $true; webBundleChecked = $true }
        artifacts = @(
            @{ kind = 'build-report'; path = 'build.json'; sha256 = HashFile (Join-Path $Fixture.evidence 'build.json') },
            @{ kind = 'signed-apk'; path = 'test.apk'; sha256 = HashFile (Join-Path $Fixture.evidence 'test.apk') },
            @{ kind = 'web-bundle'; path = 'web.zip'; sha256 = HashFile (Join-Path $Fixture.evidence 'web.zip') }
        )
    }
    $record
}
function IndexRecord($Fixture, $Record) {
    WriteJson (Join-Path $Fixture.evidence 'record.json') $Record
    WriteJson (Join-Path $Fixture.evidence 'index.json') @{
        schemaVersion = 'integrated-release-evidence-index.r1'
        records = @(@{ requirementId = $Record.requirementId; path = 'record.json'; sha256 = HashFile (Join-Path $Fixture.evidence 'record.json') })
    }
}
function Rejected($Result, [string]$Id, [string]$Pattern) {
    $item = @($Result.evidence | Where-Object { $_.id -ceq $Id })[0]
    Require ($item.status -ceq 'Rejected' -and $item.reason -match $Pattern -and -not $Result.releaseReady) 'The expected evidence rejection was not observed.'
}
try {
    Check 'default disabled preparation cannot become public release' {
        $f = Fixture; $result = Readiness $f
        Require ($result.status -ceq 'NotReady' -and -not $result.releaseReady -and $result.preparationProfileStatus -ceq 'SafeDisabledPreparation') 'Default boundary changed.'
        Require (@($result.evidence | Where-Object status -ceq 'Pending').Count -eq 6) 'Missing actual evidence was not retained.'
    }
    Check 'hash-bound local formats still do not certify devices or payment' {
        $f = Fixture; $record = LocalEvidence $f; IndexRecord $f $record; $result = Readiness $f
        Require ($result.evidence[0].status -ceq 'HashBoundForReview' -and -not $result.releaseReady) 'Local file matching was confused with public readiness.'
        Require (-not $result.networkCalls -and -not $result.filesWritten -and -not $result.moneySent) 'Validator declared an execution effect.'
    }
    Check 'true assertions without hashed artifacts are rejected' {
        $f = Fixture; $record = LocalEvidence $f; $record.artifacts = @(); IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'Assertions alone'
    }
    Check 'local evidence cannot stand in for service-fee real collection' {
        $f = Fixture; $record = LocalEvidence $f; $record.requirementId = 'service-cost-collection'; IndexRecord $f $record
        Rejected (Readiness $f) 'service-cost-collection' 'Local evidence cannot replace'
    }
    Check 'synthetic evidence cannot stand in for actual completed observations' {
        $f = Fixture; $record = LocalEvidence $f; $record.synthetic = $true; IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'non-synthetic'
    }
    Check 'version mismatch is rejected' {
        $f = Fixture; $record = LocalEvidence $f; $record.versionCode = 204; IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'versionCode mismatch'
    }
    Check 'changed source invalidates earlier role evidence' {
        $f = Fixture; $record = LocalEvidence $f; IndexRecord $f $record
        [IO.File]::WriteAllText((Join-Path $f.root 'src/Source.cs'), 'sealed class ChangedFixture {}')
        Rejected (Readiness $f) 'local-release' 'source snapshot is stale'
    }
    Check 'changed contract invalidates earlier role evidence' {
        $f = Fixture; $record = LocalEvidence $f; IndexRecord $f $record
        $f.contract.decision = 'ChangedForCounterexample'; WriteJson (Join-Path $f.root 'manifest.json') $f.contract
        Rejected (Readiness $f) 'local-release' 'release contract'
    }
    Check 'tampered record bytes are rejected before trusting claims' {
        $f = Fixture; $record = LocalEvidence $f; IndexRecord $f $record
        [IO.File]::AppendAllText((Join-Path $f.evidence 'record.json'), ' ')
        Rejected (Readiness $f) 'local-release' 'record hash mismatch'
    }
    Check 'tampered artifact bytes are rejected' {
        $f = Fixture; $record = LocalEvidence $f; IndexRecord $f $record
        [IO.File]::AppendAllText((Join-Path $f.evidence 'test.apk'), ' changed')
        Rejected (Readiness $f) 'local-release' 'artifact hash mismatch'
    }
    Check 'missing existing role is rejected' {
        $f = Fixture; $record = LocalEvidence $f; $record.roleBindings = @('shipper','cargo-driver'); IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'Missing role binding'
    }
    Check 'credentials or customer fields are rejected' {
        $f = Fixture; $record = LocalEvidence $f; $record['accessToken'] = 'LOCAL-FAKE-NOT-A-TOKEN'; IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'prohibited'
    }
    Check 'artifact traversal outside evidence root is rejected' {
        $f = Fixture; $record = LocalEvidence $f; $record.artifacts[0].path = '../manifest.json'; IndexRecord $f $record
        Rejected (Readiness $f) 'local-release' 'stay within'
    }
    foreach ($mutation in @('mode','two-workers','shutdown','shared-db','public-storage','payment-on')) {
        Check ('preparation rejects ' + $mutation) {
            $f = Fixture
            switch ($mutation) {
                'mode' { $f.profile.services.app.environment.SsalddelExecution__Mode = 'Operational' }
                'two-workers' { $f.profile['x-mirror-readiness'].workerInstances = 2 }
                'shutdown' { $f.profile['x-mirror-readiness'].autoShutdown = $true }
                'shared-db' { $f.profile.services.mysql.environment.MYSQL_DATABASE = 'ssalddel' }
                'public-storage' { $f.profile.services.app.environment.GoogleCloudStorage__PublicBucketName = 'public' }
                'payment-on' { $f.profile.services.app.environment.TransportPrepayment__Enabled = 'true' }
            }
            WriteJson (Join-Path $f.root $f.contract.preparationProfile) $f.profile
            $result = Readiness $f
            Require ($result.preparationProfileStatus -ceq 'Invalid' -and -not $result.releaseReady) 'Unsafe preparation profile was accepted.'
        }
    }
    $summary = [ordered]@{ schemaVersion = 'integrated-platform-readiness-tests.r1'; status = 'Passed'; count = $taskChecks.Count;
        checks = @($taskChecks); fixtureOnly = $true; actualApkVerified = $false; actualHttpsVerified = $false; actualDeviceVerified = $false;
        actualPaymentVerified = $false; actualPayoutVerified = $false; actualRestoreVerified = $false }
    if ($EvidencePath) { WriteJson ([IO.Path]::GetFullPath($EvidencePath)) $summary }
    $summary | ConvertTo-Json -Depth 8
} finally {
    $resolved = [IO.Path]::GetFullPath($taskTemp)
    $allowed = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'mirror-integrated-readiness-tests-*') { throw 'Fixture cleanup path mismatch.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
