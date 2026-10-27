#requires -Version 7.4
# Offline archive/configuration checks only. This never starts Docker, a host, a DB or FCM.
[CmdletBinding()]
param(
    [string]$BundleDirectory,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$ProfilePath,
    [string]$CaddyfilePath,
    [switch]$AsObject
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Formats.Tar
function Need($Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function PrivatePath([string]$Name, [switch]$Image) {
    $name = $Name.Replace('\','/') -replace '^(\./)+',''
    $localFile = $name -match '(?i)(^|/)(appsettings(\.[^/]+)?\.Local\.json|google-services\.json|[^/]*service[-_]account[^/]*\.json|\.env([^/]*)?|[^/]+\.(pfx|p12|pem|key|keystore|jks))$'
    $privateDirectory = $name -match '(?i)(^|/)(private|secrets|artifacts|\.artifacts|\.codex[^/]*|App_Data)(/|$)'
    if ($Image) {
        # Linux base images legitimately contain CA PEMs and an empty /etc/ssl/private directory.
        return ($name -match '^(app|src|workspace|run/secrets)/' -and ($localFile -or $privateDirectory))
    }
    $localFile -or $privateDirectory
}
function CheckPath([string]$Name) {
    Need ($Name -notmatch '(^[\/]|^[A-Za-z]:|(^|[\/])\.\.([\/]|$))') 'Archive contains an unsafe absolute/parent path.'
}
function EntryText($Entry) {
    Need ($Entry.Length -le 10485760) 'Configuration entry is too large to inspect safely.'
    $reader = [IO.StreamReader]::new($Entry.DataStream, [Text.Encoding]::UTF8, $true, 4096, $true)
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function CheckCredentialText([string]$Text) {
    Need ($Text -notmatch '(?i)"(private_key|client_secret|refresh_token)"\s*:\s*"[^"\s]') 'Archive contains a private credential payload.'
}
function SecretMetadataName([string]$Name) {
    $name = $Name -replace '[^a-zA-Z0-9]', ''
    $name -match '(?i)(password|passwd|secret|token|privatekey|apikey|serverkey|accountkey|accesskey|connectionstring|authorization)' -or
        $name -match '(?i)^(pwd|sas|credentials?)$'
}
function CheckImageMetadata($Value) {
    if ($Value -is [Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            if (SecretMetadataName ([string]$key)) {
                Need ($null -eq $Value[$key] -or ($Value[$key] -is [string] -and [string]::IsNullOrWhiteSpace($Value[$key]))) 'Server image configuration contains a private credential value.'
            }
            CheckImageMetadata $Value[$key]
        }
    } elseif ($Value -is [string]) {
        CheckCredentialText $Value
        Need ($Value -notmatch '(?i)-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----') 'Server image configuration contains private key material.'
        # Docker ENV strings and build history retain values even if a later layer removes them.
        # Check assignments and CLI credential arguments without printing their contents.
        $assignments = [regex]::Matches($Value, '(?i)(?<name>[a-z_][a-z0-9_.:-]*)\s*(?:=|:\s+)\s*(?:"(?<quoted>[^"]*)"|''(?<single>[^'']*)''|(?<plain>[^\s;&|]+))')
        $arguments = [regex]::Matches($Value, '(?i)--(?<name>[a-z][a-z0-9_.-]*)\s+(?:"(?<quoted>[^"]*)"|''(?<single>[^'']*)''|(?<plain>[^\s;&|]+))')
        foreach ($match in @($assignments) + @($arguments)) {
            if (SecretMetadataName $match.Groups['name'].Value) {
                $content = $match.Groups['quoted'].Value + $match.Groups['single'].Value + $match.Groups['plain'].Value
                Need ([string]::IsNullOrWhiteSpace($content)) 'Server image environment, labels or history contain a private credential assignment.'
            }
        }
    } elseif ($Value -is [Collections.IEnumerable]) {
        foreach ($item in $Value) { CheckImageMetadata $item }
    }
}
function ReadWeb([string]$Path) {
    $file = [IO.File]::OpenRead($Path)
    $gzip = [IO.Compression.GZipStream]::new($file, [IO.Compression.CompressionMode]::Decompress)
    $tar = [System.Formats.Tar.TarReader]::new($gzip)
    $index = $null; $production = $null; $framework = $false
    try {
        while ($null -ne ($entry = $tar.GetNextEntry())) {
            CheckPath $entry.Name
            Need ($entry.EntryType -notin @([System.Formats.Tar.TarEntryType]::SymbolicLink,[System.Formats.Tar.TarEntryType]::HardLink)) 'Web archive links are not accepted.'
            Need (-not (PrivatePath $entry.Name)) 'Web archive contains a local setting/key/private artifact.'
            if ($entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) { continue }
            $name = $entry.Name.Replace('\','/') -replace '^(\./)+',''
            if ($name.StartsWith('_framework/')) { $framework = $true }
            if ($name -eq 'index.html') { $index = EntryText $entry }
            elseif ($name.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) {
                $text = EntryText $entry; CheckCredentialText $text
                if ($name -eq 'appsettings.Production.json') { $production = $text | ConvertFrom-Json -AsHashtable }
            }
        }
    } finally { $tar.Dispose(); $gzip.Dispose(); $file.Dispose() }
    Need ($framework -and $index -match 'Ssalddel\.WebApp\.styles\.css' -and $index -match '<base\s+href="/"') 'A published integrated WebApp at the root is required; the old role launcher is not accepted.'
    Need ($null -ne $production -and [string]::IsNullOrWhiteSpace($production.SsalddelEndpoints.ServerBaseAddress)) 'Integrated web must use its production same-origin endpoint configuration.'
}
function ReadImage([string]$Path) {
    $file = [IO.File]::OpenRead($Path); $tar = [System.Formats.Tar.TarReader]::new($file)
    $manifest = $null
    try {
        while ($null -ne ($entry = $tar.GetNextEntry())) {
            CheckPath $entry.Name
            if (($entry.Name -replace '^(\./)+','') -eq 'manifest.json') { $manifest = @( (EntryText $entry) | ConvertFrom-Json -AsHashtable ); break }
        }
    } finally { $tar.Dispose(); $file.Dispose() }
    Need ($null -ne $manifest -and $manifest.Count -eq 1 -and @($manifest[0].Layers).Count -gt 0 -and
        @($manifest[0].RepoTags).Count -eq 1 -and $manifest[0].RepoTags[0] -ceq 'ssalddel-server:azure-preview') 'A single docker-save server image with exactly the expected deployment tag is required.'
    $configName = [string]$manifest[0].Config
    Need (-not [string]::IsNullOrWhiteSpace($configName)) 'The server image configuration reference is required.'
    CheckPath $configName
    $layers = @($manifest[0].Layers); $scanned = 0; $configScanned = 0
    $file = [IO.File]::OpenRead($Path); $tar = [System.Formats.Tar.TarReader]::new($file)
    try {
        while ($null -ne ($entry = $tar.GetNextEntry())) {
            CheckPath $entry.Name
            if ($entry.Name -ceq $configName) {
                Need ($entry.EntryType -in @([System.Formats.Tar.TarEntryType]::RegularFile,[System.Formats.Tar.TarEntryType]::V7RegularFile)) 'Server image configuration must be an ordinary file.'
                $configuration = (EntryText $entry) | ConvertFrom-Json -AsHashtable
                Need ($configuration -is [Collections.IDictionary]) 'Server image configuration must be a JSON object.'
                CheckImageMetadata $configuration
                $configScanned++
                continue
            }
            if ($entry.Name -cnotin $layers) { continue }
            # docker-save may contain plain or gzip OCI layers. Buffer one bounded layer,
            # never extract image contents to disk and never depend on a Docker daemon.
            Need ($entry.Length -le 1073741824) 'An image layer exceeds the 1 GiB offline inspection bound.'
            $memory = [IO.MemoryStream]::new(); $entry.DataStream.CopyTo($memory); $memory.Position=0
            $first=$memory.ReadByte(); $second=$memory.ReadByte(); $memory.Position=0
            $compressed = $first -eq 31 -and $second -eq 139
            $stream = if ($compressed) { [IO.Compression.GZipStream]::new($memory,[IO.Compression.CompressionMode]::Decompress,$true) } else { $memory }
            $inner = [System.Formats.Tar.TarReader]::new($stream, $true)
            try {
                while ($null -ne ($payload = $inner.GetNextEntry())) {
                    CheckPath $payload.Name
                    if ($payload.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) { continue }
                    Need (-not (PrivatePath $payload.Name -Image)) 'Server image contains a local setting/key/private artifact in a layer.'
                    $name = $payload.Name.Replace('\','/') -replace '^(\./)+',''
                    Need (-not ($name -match '^(app|src|workspace)/' -and $payload.EntryType -in @([System.Formats.Tar.TarEntryType]::SymbolicLink,[System.Formats.Tar.TarEntryType]::HardLink))) 'Application links in the image are not accepted.'
                    if ($name -match '^(app|src|workspace)/.*\.json$' -and $payload.DataStream) { CheckCredentialText (EntryText $payload) }
                }
            } finally { $inner.Dispose(); if ($compressed) { $stream.Dispose() }; $memory.Dispose() }
            $scanned++
        }
    } finally { $tar.Dispose(); $file.Dispose() }
    Need ($configScanned -eq 1) 'The declared server image configuration must be inspected exactly once.'
    Need ($scanned -eq $layers.Count) 'Some declared server image layers were not inspected.'
    $scanned
}

$repo = [IO.Path]::GetFullPath($RepositoryRoot)
$bundle = if ($BundleDirectory) { [IO.Path]::GetFullPath($BundleDirectory) } else { $null }
if (-not $ProfilePath) { $ProfilePath = if ($bundle) { Join-Path $bundle 'compose.integrated-mobile-field-test.override.yaml' } else { Join-Path $repo 'deploy/azure-vm/compose.integrated-mobile-field-test.override.yaml' } }
if (-not $CaddyfilePath) { $CaddyfilePath = if ($bundle) { Join-Path $bundle 'Caddyfile' } else { Join-Path $repo 'deploy/azure-vm/Caddyfile.integrated-mobile-field-test' } }
$profile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json -AsHashtable
$meta = $profile['x-mirror-field-test']; $env = $profile.services.app.environment
Need ($profile.name -ceq 'mirror-integrated-field-test' -and $meta.stage -ceq 'IsolatedSyntheticFieldTest' -and
    $meta.executionMode -ceq 'Simulation' -and $env.SsalddelExecution__Mode -ceq 'Simulation' -and $env.ASPNETCORE_ENVIRONMENT -ceq 'Staging') 'Field test must remain isolated Staging/Simulation.'
Need ($meta.apiInstances -eq 1 -and $profile.services.app.deploy.replicas -eq 1 -and $meta.availability -ceq '24h' -and $meta.autoShutdown -ceq $false) 'Field test requires one server and no automatic shutdown.'
Need ($meta.automaticDispatchEnabled -ceq $false -and $meta.financialExecutionEnabled -ceq $false -and $meta.weeklyPreparationEnabled -ceq $false -and
    $meta.allHostedServicesDisabled -ceq $false -and $meta.generalProjectionAndInboxWorkersMayRun -ceq $true -and $meta.publicReleaseReady -ceq $false) 'Do not claim all workers disabled or public release ready.'
Need ($env.NeighborhoodMemberDelivery__Enabled -ceq 'true' -and $env.NeighborhoodMemberDelivery__OperationalEnabled -ceq 'false') 'Member delivery field testing must remain explicitly enabled only within Simulation.'
foreach ($key in @('TransportPrepayment__Enabled','DriverPayoutBatch__Enabled','DriverPayoutBatch__AutomaticPreparationEnabled',
    'MobileFieldTest__FoodDispatch__Enabled','MobileFieldTest__FoodDispatch__BootstrapSyntheticDriverState','MobileFieldTest__FoodDispatch__RefreshSyntheticDriverLocation',
    'FoodObserverVerification__Enabled','ObservableOperationsVerification__Enabled','FulfillmentRouting__Enabled','FreightContinuity__Enabled',
    'DistributedFulfillment__Enabled','NeighborhoodSettlement__Enabled','SharedDriverWork__Enabled','OperationsAlerts__ExternalSendEnabled',
    'IdentitySeed__BootstrapAdmin__Enabled','DatabaseInitialization__RunAtStartup','SalesChannelOrderSync__Enabled')) {
    Need ($env[$key] -ceq 'false') 'Financial/automatic execution or startup initialization is enabled.'
}
Need ([string]::IsNullOrWhiteSpace($env.TossPayments__ClientKey) -and [string]::IsNullOrWhiteSpace($env.TossPayments__SecretKey) -and
    [string]::IsNullOrWhiteSpace($env.PushNotifications__ServerKey)) 'Keys must not be embedded in the profile.'
foreach ($key in $env.Keys) {
    if ($key -match '(?i)(Password|SecretKey|ServerKey|ClientSecret|HashSalt|Aes256GcmKeyBase64)$') {
        Need ([string]::IsNullOrWhiteSpace($env[$key]) -or $env[$key] -match '^\$\{[A-Z0-9_]+[^}]*\}$') 'A credential value is embedded in the profile instead of supplied externally.'
    }
}
$connection = [Data.Common.DbConnectionStringBuilder]::new(); $connection.set_ConnectionString($env.ConnectionStrings__DefaultConnection)
Need ($connection['server'] -ceq 'mysql' -and $connection['database'] -ceq 'mirror_integrated_field_test' -and
    $profile.services.mysql.environment.MYSQL_DATABASE -ceq 'mirror_integrated_field_test' -and
    $profile.services.mongo.environment.MONGO_INITDB_DATABASE -ceq 'mirror_integrated_field_test' -and
    $env.MongoDb__Database -ceq 'mirror_integrated_field_test') 'The connection and both databases must use the isolated field-test targets.'
foreach ($volume in @('mysql_data','mongo_data','app_data','app_uploads','caddy_data','caddy_config')) {
    Need ($profile.volumes[$volume].name -ceq ('mirror-integrated-field-test-' + $volume.Replace('_','-'))) 'A volume points outside the dedicated field-test project.'
}
Need ($env.ObjectStorage__Provider -ceq 'GoogleCloud' -and $env.GoogleCloudStorage__PrivateBucketName -match '^\$\{MIRROR_FIELD_PRIVATE_BUCKET:\?' -and
    $env.GoogleCloudStorage__BucketName -ceq $env.GoogleCloudStorage__PrivateBucketName -and
    [string]::IsNullOrWhiteSpace($env.GoogleCloudStorage__PublicBucketName) -and [string]::IsNullOrWhiteSpace($env.GoogleCloudStorage__PublicBaseUrl)) 'Explicit private GCS storage is required.'
foreach ($target in @('/run/secrets/mirror-storage-service-account.json','/run/secrets/mirror-push-service-account.json')) {
    $mount = @($profile.services.app.volumes | Where-Object { $_.target -ceq $target })
    Need ($mount.Count -eq 1 -and $mount[0].read_only -ceq $true -and $mount[0].bind.create_host_path -ceq $false -and
        $mount[0].source -match '^\$\{MIRROR_(STORAGE|PUSH)_SERVICE_ACCOUNT_FILE:\?') 'Credential files must be external read-only mounts without automatic path creation.'
}
$caddy = Get-Content -LiteralPath $CaddyfilePath -Raw
Need ($caddy -match 'path /api/\* /hubs/\* /health/\*' -and $caddy -match 'reverse_proxy app:8080' -and
    $caddy -match 'try_files \{path\} /index\.html' -and $caddy -notmatch '/roles/0[1-5]|redir') 'Integrated Caddy must serve the root SPA and API/hubs without legacy role redirects.'
$ignore = Get-Content -LiteralPath (Join-Path $repo '.dockerignore') -Raw
foreach ($pattern in @('**/appsettings.Local.json','**/artifacts/**','**/*.pfx','**/*service-account*.json')) {
    Need ($ignore.Contains($pattern)) 'Existing Docker context exclusions for local settings/credentials/artifacts are missing.'
}
$project = Get-Content -LiteralPath (Join-Path $repo 'Ssalddel/Ssalddel.csproj') -Raw
Need ($project -match 'appsettings\.Local\.json' -and $project -match 'CopyToPublishDirectory="Never"') 'Existing server publish exclusion for local configuration is missing.'
$layers = $null
if ($bundle) {
    $manifest = Get-Content -LiteralPath (Join-Path $bundle 'deployment-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    Need ($manifest.profile -ceq 'integrated-mobile-field-test' -and $manifest.runtimeMode -ceq 'Simulation' -and
        $manifest.composeProject -ceq 'mirror-integrated-field-test' -and $manifest.deploymentRoot -ceq '/opt/mirror-integrated-field-test' -and
        $manifest.sourceSnapshotSha256 -match '^[a-f0-9]{64}$' -and $manifest.automaticCloudDeployment -ceq $false -and
        $manifest.physicalDeviceVerified -ceq $false -and $manifest.serverCommunicationVerified -ceq $false -and $manifest.actualPushReceived -ceq $false -and
        $manifest.publicReleaseReady -ceq $false -and $manifest.allHostedServicesDisabled -ceq $false) 'Package manifest must retain field-test scope and an explicit build handoff fingerprint.'
    $expected = @('ssalddel-server.tar','web.tar.gz','compose.yaml','compose.integrated-mobile-field-test.override.yaml','Caddyfile',
        'deploy-preview-profile.sh','deploy-integrated-mobile-field-test.sh','provision-preview-secrets.sh')
    $checked = @()
    foreach ($line in Get-Content -LiteralPath (Join-Path $bundle 'deployment.sha256')) {
        Need ($line -match '^([a-f0-9]{64})  ([A-Za-z0-9_.-]+)$') 'Invalid deployment checksum entry.'
        $hashValue = $Matches[1]; $name = $Matches[2]
        Need ($name -cin $expected -and $name -cnotin $checked -and (Hash (Join-Path $bundle $name)) -ceq $hashValue) 'Deployment file is unexpected, duplicated or changed.'
        $checked += $name
    }
    Need ($checked.Count -eq $expected.Count) 'Deployment files are incomplete.'
    Need ((@($manifest.files | Sort-Object) -join '|') -ceq (@($expected | Sort-Object) -join '|')) 'Manifest file set differs from the reviewed deployment set.'
    Need ((Hash (Join-Path $bundle 'ssalddel-server.tar')) -ceq $manifest.serverImageSha256 -and
        (Hash (Join-Path $bundle 'web.tar.gz')) -ceq $manifest.webSha256) 'Archive hashes differ from the build handoff.'
    foreach ($entry in Get-ChildItem -LiteralPath $bundle -Recurse -Force) {
        Need (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and -not (PrivatePath ([IO.Path]::GetRelativePath($bundle,$entry.FullName)))) 'Deployment bundle contains a link/private/local artifact.'
    }
    ReadWeb (Join-Path $bundle 'web.tar.gz')
    $layers = ReadImage (Join-Path $bundle 'ssalddel-server.tar')
}
$result = [ordered]@{ schemaVersion='integrated-mobile-field-package-check.r1'; status=$(if ($bundle) { 'LocalArchiveAndProfileChecked' } else { 'LocalProfileChecked' });
    profile='integrated-mobile-field-test'; runtimeMode='Simulation'; sourceBinding='DeclaredBuildHandoffWithArtifactHashes';
    inspectedImageLayers=$layers; allHostedServicesDisabled=$false; generalProjectionAndInboxWorkersMayRun=$true;
    cloudDeployment=$false; serverCommunicationVerified=$false; physicalDeviceVerified=$false; actualPushReceived=$false;
    financialExecutionEnabled=$false; publicReleaseReady=$false; filesWritten=$false; networkCalls=$false }
if ($AsObject) { [pscustomobject]$result } else { $result | ConvertTo-Json -Depth 5 }
