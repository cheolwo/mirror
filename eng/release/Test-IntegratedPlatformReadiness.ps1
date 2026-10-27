#requires -Version 7.0
# Reads local source and sanitized evidence only. Never deploys, signs in, enables a worker, or sends money.
[CmdletBinding()]
param(
    [string]$ManifestPath = 'eng/release/integrated-platform-release.json',
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$EvidenceDirectory,
    [switch]$AsObject
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$taskIssues = [Collections.Generic.List[object]]::new()
function Issue([string]$Code, [string]$Item, [string]$Detail) {
    $taskIssues.Add([ordered]@{ code = $Code; item = $Item; detail = $Detail })
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function TextHash([string]$Value) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}
function SafeFile([string]$Base, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
        $Relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Evidence/source path must stay within its supplied root.' }
    $resolved = [IO.Path]::GetFullPath((Join-Path $Base $Relative))
    $prefix = [IO.Path]::GetFullPath($Base).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.File]::Exists($resolved)) {
        throw 'Evidence/source file is missing or outside its supplied root.'
    }
    $entry = Get-Item -LiteralPath $resolved -Force
    while ($null -ne $entry -and $entry.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked evidence/source paths are not accepted.' }
        $entry = if ($entry -is [IO.FileInfo]) { $entry.Directory } else { $entry.Parent }
    }
    $resolved
}
function Need($Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Same([string]$Actual, [string]$Expected, [string]$Message) {
    Need (-not [string]::IsNullOrWhiteSpace($Actual) -and $Actual -ceq $Expected) $Message
}
function Sanitized($Value) {
    if ($Value -is [Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            if ($key -match '(?i)^(password|secret|clientSecret|accessToken|refreshToken|connectionString|customerName|phoneNumber|fullAddress|rawScreenshot|privateEvidence|email)$') {
                throw 'Evidence contains a prohibited credential/customer/raw-capture field.'
            }
            Sanitized $Value[$key]
        }
    } elseif ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { foreach ($item in $Value) { Sanitized $item } }
}
function SourceFiles($Contract) {
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $extensions = @('.cs','.razor','.xaml','.css','.js','.json','.csproj','.props','.targets','.ps1')
    $excluded = @('bin','obj','node_modules','vendor','artifacts','App_Data','uploads')
    foreach ($relativeRoot in $Contract.sourceRoots) {
        $absolute = [IO.Path]::GetFullPath((Join-Path $taskRoot $relativeRoot))
        Need ($absolute.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and [IO.Directory]::Exists($absolute)) 'Source root is missing/outside repository.'
        $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($absolute)
        while ($pending.Count -gt 0) {
            $current = $pending.Pop()
            foreach ($directory in [IO.Directory]::EnumerateDirectories($current)) {
                $info = [IO.DirectoryInfo]::new($directory)
                if ($info.Name -notin $excluded -and -not $info.Name.StartsWith('.') -and ($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) { $pending.Push($directory) }
            }
            foreach ($file in [IO.Directory]::EnumerateFiles($current)) {
                $info = [IO.FileInfo]::new($file)
                $privateJson = $info.Extension -eq '.json' -and $info.Name -match '(?i)^appsettings\.Local\.json|^google-services\.json$|service.account|credential|secret'
                if ($info.Extension -in $extensions -and -not $privateJson) {
                    [void]$paths.Add([IO.Path]::GetRelativePath($taskRoot, $file).Replace('\','/'))
                }
            }
        }
    }
    foreach ($path in $Contract.additionalSourcePaths) { [void]$paths.Add($path.Replace('\','/')) }
    foreach ($path in $Contract.existingTools.Values) { [void]$paths.Add($path.Replace('\','/')) }
    [string[]]$sorted = @($paths); [Array]::Sort($sorted, [StringComparer]::Ordinal); $sorted
}

$taskManifestFile = SafeFile $taskRoot $ManifestPath
$taskContract = Get-Content -LiteralPath $taskManifestFile -Raw | ConvertFrom-Json -AsHashtable
Sanitized $taskContract
Same $taskContract.schemaVersion 'integrated-platform-release.r1' 'Unsupported release manifest.'
$taskRequiredKinds = @{ 'local-release' = 'local'; 'https-domestic-flows' = 'https'; 'device-domestic-flows' = 'device';
    'service-cost-collection' = 'payment'; 'weekly-service-payout' = 'payout'; 'independent-recovery' = 'restore' }
Need ($taskContract.requirements.Count -eq $taskRequiredKinds.Count -and
    @($taskContract.requirements.id | Sort-Object -Unique).Count -eq $taskRequiredKinds.Count) 'Required evidence kinds cannot be removed or merged.'
foreach ($requirement in $taskContract.requirements) {
    Need ($taskRequiredKinds.ContainsKey($requirement.id) -and $requirement.kind -ceq $taskRequiredKinds[$requirement.id]) 'Required real evidence kinds cannot be replaced with local evidence.'
}
$taskContractHash = Hash $taskManifestFile
$taskSources = @(foreach ($path in (SourceFiles $taskContract)) {
    [ordered]@{ path = $path; sha256 = Hash (SafeFile $taskRoot $path) }
})
$taskSnapshot = TextHash ((@($taskSources | ForEach-Object { $_.path + '|' + $_.sha256 }) -join "`n") + "`n")
$taskProfileFile = SafeFile $taskRoot $taskContract.preparationProfile
$taskProfileHash = Hash $taskProfileFile
# Compose accepts JSON as YAML; a fixed JSON profile avoids an additional YAML/runtime dependency.
$taskProfile = Get-Content -LiteralPath $taskProfileFile -Raw | ConvertFrom-Json -AsHashtable
$taskMeta = $taskProfile['x-mirror-readiness']; $taskEnv = $taskProfile.services.app.environment
$taskProfileValid = $true
try {
    Need ($taskMeta.stage -ceq 'PreparationOnly' -and $taskEnv.SsalddelExecution__Mode -ceq 'Simulation' -and
        $taskMeta.targetExecutionMode -ceq 'Operational' -and $taskMeta.operationalEffectsEnabled -ceq $false) 'Preparation must stay Simulation with operational effects OFF.'
    Need ($taskContract.policy.targetExecutionMode -ceq 'Operational' -and $taskContract.policy.webAndIntegratedApk -ceq $true -and
        $taskContract.policy.availability -ceq '24h' -and $taskContract.policy.apiInstances -eq 1 -and $taskContract.policy.workerInstances -eq 1 -and
        $taskContract.policy.autoShutdown -ceq $false -and $taskContract.policy.monthlyBudgetKrw -eq 500000 -and
        $taskContract.policy.goodsPaymentRoute -ceq 'PartyDirect' -and $taskContract.policy.serviceCostCollection -ceq 'Platform' -and
        $taskContract.policy.payoutSchedule -ceq 'Weekly') 'Release policy differs from the accepted integrated release boundary.'
    Need ($taskMeta.availability -ceq '24h' -and $taskMeta.apiInstances -eq 1 -and $taskMeta.workerInstances -eq 1 -and
        $taskProfile.services.app.deploy.replicas -eq 1 -and $taskMeta.autoShutdown -ceq $false -and $taskMeta.monthlyBudgetKrw -eq 500000) 'Runtime preparation must declare one 24h API/worker without auto shutdown.'
    Need (($taskProfile | ConvertTo-Json -Depth 30) -notmatch '(?i)auto.?shutdown[^,\r\n]*:\s*true|stop.?schedule|shutdown.?schedule') 'A shutdown schedule conflicts with 24h readiness.'
    Need ($taskProfile.name -ceq 'mirror-integrated-preparation' -and $taskMeta.databaseScope -ceq 'IsolatedPreparation') 'Preparation project/database isolation is missing.'
    $sql = $taskProfile.services.mysql.environment.MYSQL_DATABASE; $mongo = $taskProfile.services.mongo.environment.MONGO_INITDB_DATABASE
    Need ($sql -ceq 'mirror_integrated_preparation' -and $mongo -ceq 'mirror_integrated_preparation' -and
        $taskEnv.MongoDb__Database -ceq $mongo -and $taskEnv.ConnectionStrings__DefaultConnection -match ('(?i)database=' + [Regex]::Escape($sql) + ';')) 'Database targets are not bound to isolated preparation databases.'
    foreach ($volume in @('mysql_data','mongo_data','app_data','app_uploads','caddy_data','caddy_config')) {
        Need ($taskProfile.volumes[$volume].name -ceq ('mirror-integrated-preparation-' + $volume.Replace('_','-'))) 'Named volumes must not reuse the preview/operational project.'
    }
    Need ($taskMeta.publicStorageAllowed -ceq $false -and $taskEnv.ObjectStorage__Provider -ceq 'GoogleCloud' -and
        $taskEnv.GoogleCloudStorage__PrivateBucketName -match '^\$\{MIRROR_PRIVATE_STORAGE_BUCKET:\?' -and
        [string]::IsNullOrWhiteSpace($taskEnv.GoogleCloudStorage__PublicBucketName) -and [string]::IsNullOrWhiteSpace($taskEnv.GoogleCloudStorage__PublicBaseUrl)) 'Storage must stay private and explicitly configured.'
    foreach ($key in $taskEnv.Keys) {
        if ($key -like 'VersionFeatureFlags__*' -or $key -match '__(Enabled|AutomaticPreparationEnabled|ExternalSendEnabled)$') {
            Need ($taskEnv[$key] -ceq 'false') ('Preparation effect must stay OFF: ' + $key)
        }
    }
    Need ([string]::IsNullOrWhiteSpace($taskEnv.TossPayments__ClientKey) -and [string]::IsNullOrWhiteSpace($taskEnv.TossPayments__SecretKey)) 'Preparation must not embed payment keys.'
} catch { $taskProfileValid = $false; Issue 'PreparationProfileInvalid' 'profile' $_.Exception.Message }
if ([string]::IsNullOrWhiteSpace($taskContract.releaseVersion) -or $taskContract.versionCode -isnot [long] -and $taskContract.versionCode -isnot [int] -or $taskContract.versionCode -lt 1) {
    Issue 'ReleaseVersionPending' 'release' 'A reviewed x.y.z/versionCode and matching artifacts are not selected.'
}
$taskRequiredRoles = @('community','orderer','restaurant','food-driver','shipper','cargo-driver','warehouse','seller','worker','operator')
if (@($taskContract.roles | Sort-Object -Unique).Count -ne $taskContract.roles.Count -or
    @($taskRequiredRoles | Where-Object { $_ -cnotin $taskContract.roles }).Count -gt 0) {
    Issue 'RoleCoverageInvalid' 'roles' 'The existing domestic roles must be distinct and covered together.'
}
$taskIndexRecords = @()
if (-not [string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $taskEvidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
    try {
        $index = Get-Content -LiteralPath (SafeFile $taskEvidenceRoot 'index.json') -Raw | ConvertFrom-Json -AsHashtable
        Sanitized $index; Same $index.schemaVersion $taskContract.evidencePolicy.indexSchemaVersion 'Unsupported evidence index.'
        $taskIndexRecords = @($index.records)
    } catch { Issue 'EvidenceIndexInvalid' 'evidence' $_.Exception.Message }
}
$taskResults = [Collections.Generic.List[object]]::new(); $taskAccepted = @{}
foreach ($requirement in $taskContract.requirements) {
    $matching = @($taskIndexRecords | Where-Object { $_.requirementId -ceq $requirement.id })
    $state = 'Pending'; $reason = 'Required evidence has not been provided.'
    if ($matching.Count -gt 1) { $state = 'Rejected'; $reason = 'Multiple records for one requirement must be reconciled explicitly.' }
    elseif ($matching.Count -eq 1) {
        try {
            $entry = $matching[0]; $file = SafeFile $taskEvidenceRoot $entry.path
            Need ($entry.sha256 -match '^[a-fA-F0-9]{64}$' -and (Hash $file) -ceq $entry.sha256.ToLowerInvariant()) 'Evidence record hash mismatch.'
            $record = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -AsHashtable; Sanitized $record
            Same $record.schemaVersion $taskContract.evidencePolicy.recordSchemaVersion 'Unsupported evidence record.'
            Same $record.requirementId $requirement.id 'Wrong evidence requirement.'
            Same $record.kind $requirement.kind 'Local evidence cannot replace HTTPS/device/payment/payout/restore evidence.'
            Same $record.releaseRevision $taskContract.releaseRevision 'Evidence release revision is stale.'
            Same $record.releaseVersion $taskContract.releaseVersion 'Evidence version differs from the selected public version.'
            Need ($record.versionCode -eq $taskContract.versionCode) 'Evidence versionCode mismatch.'
            Same $record.sourceSnapshotSha256 $taskSnapshot 'Evidence source snapshot is stale.'
            Same $record.contractSha256 $taskContractHash 'Evidence was not made for this release contract.'
            Same $record.profileSha256 $taskProfileHash 'Evidence profile differs from the checked profile.'
            Need ($record.result -ceq 'Passed' -and -not [string]::IsNullOrWhiteSpace($record.runId) -and $record.synthetic -ceq $false) 'Only completed, non-synthetic evidence records may be considered.'
            Need ($record.executionMode -cin $requirement.modes) 'Evidence has the wrong execution mode.'
            if ($requirement.kind -ne 'local') {
                Need ($taskEnv.SsalddelExecution__Mode -ceq $record.executionMode) 'Operational evidence cannot be bound to the disabled Simulation preparation profile.'
                $origin = $null
                Need ([Uri]::TryCreate($record.origin, [UriKind]::Absolute, [ref]$origin) -and $origin.Scheme -ceq 'https' -and -not $origin.IsLoopback -and
                    -not $origin.Host.EndsWith('.invalid') -and [string]::IsNullOrEmpty($origin.UserInfo) -and [string]::IsNullOrEmpty($origin.Query)) 'Real environment evidence requires its public HTTPS origin.'
            }
            foreach ($role in $taskContract.roles) { Need ($role -cin $record.roleBindings) ('Missing role binding: ' + $role) }
            foreach ($assertion in $requirement.assertions) { Need ($record.assertions[$assertion] -ceq $true) ('Missing result assertion: ' + $assertion) }
            Need (@($record.artifacts).Count -ge $requirement.artifacts.Count) 'Assertions alone are not evidence; required hashed artifacts are missing.'
            foreach ($kind in $requirement.artifacts) { Need (@($record.artifacts | Where-Object { $_.kind -ceq $kind }).Count -ge 1) ('Missing artifact kind: ' + $kind) }
            foreach ($artifact in $record.artifacts) {
                $artifactFile = SafeFile $taskEvidenceRoot $artifact.path
                Need ($artifact.sha256 -match '^[a-fA-F0-9]{64}$' -and (Hash $artifactFile) -ceq $artifact.sha256.ToLowerInvariant()) 'Evidence artifact hash mismatch.'
                if ($artifact.kind -notin @('signed-apk','web-bundle')) {
                    Need ([IO.Path]::GetExtension($artifactFile) -ceq '.json') 'Only sanitized structured reports are accepted; no raw captures/logs.'
                    $proof = Get-Content -LiteralPath $artifactFile -Raw | ConvertFrom-Json -AsHashtable; Sanitized $proof
                    Same $proof.releaseRevision $record.releaseRevision 'Artifact revision mismatch.'
                    Same $proof.sourceSnapshotSha256 $taskSnapshot 'Artifact source snapshot mismatch.'
                    Same $proof.profileSha256 $taskProfileHash 'Artifact profile mismatch.'
                    Same $proof.kind $requirement.kind 'Artifact evidence kind mismatch.'
                    Same $proof.runId $record.runId 'Artifact belongs to a different execution.'
                    Need ($proof.result -ceq 'Passed' -and $proof.synthetic -ceq $false) 'Artifact result is not completed real evidence.'
                }
            }
            if ($null -ne $requirement.dependsOn) {
                Need ($taskAccepted.ContainsKey($requirement.dependsOn)) 'The immediately preceding required role/lifecycle evidence is not accepted.'
                Same $record.previousRecordSha256 $taskAccepted[$requirement.dependsOn] 'Evidence is not bound to the accepted preceding role/lifecycle record.'
            }
            $taskAccepted[$requirement.id] = $entry.sha256.ToLowerInvariant(); $state = 'HashBoundForReview'
            $reason = 'Kind, version, source, profile, role coverage and artifact hashes match. Content/authenticity still requires authorized review.'
        } catch { $state = 'Rejected'; $reason = $_.Exception.Message }
    }
    if ($state -ne 'HashBoundForReview') { Issue ('Evidence' + $state) $requirement.id $reason }
    $taskResults.Add([ordered]@{ id = $requirement.id; requiredKind = $requirement.kind; status = $state; reason = $reason })
}
# This read-only validator cannot verify bank/provider/device authenticity or grant publication.
Issue 'OperationalProfilePending' 'deployment' 'The shipped profile is disabled Simulation preparation; a reviewed production profile and its runtime evidence are still required.'
Issue 'AuthorizedReleaseDecisionPending' 'release' 'Matching files/booleans are not actual device, money or recovery certification; final authorized review is separate.'
$taskOutput = [ordered]@{
    schemaVersion = 'integrated-platform-readiness.r1'; status = 'NotReady'; releaseReady = $false
    releaseRevision = $taskContract.releaseRevision; releaseVersion = $taskContract.releaseVersion; versionCode = $taskContract.versionCode
    sourceSnapshotSha256 = $taskSnapshot; sourceFileCount = $taskSources.Count; contractSha256 = $taskContractHash; profileSha256 = $taskProfileHash
    preparationProfileStatus = $(if ($taskProfileValid) { 'SafeDisabledPreparation' } else { 'Invalid' })
    checkedProfileMode = $taskEnv.SsalddelExecution__Mode; targetExecutionMode = $taskContract.policy.targetExecutionMode
    evidence = @($taskResults); issues = @($taskIssues)
    networkCalls = $false; cloudChanges = $false; runtimeChanges = $false; moneySent = $false; filesWritten = $false
}
if ($AsObject) { [pscustomobject]$taskOutput } else { $taskOutput | ConvertTo-Json -Depth 12 }
