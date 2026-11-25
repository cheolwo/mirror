[CmdletBinding()]
param(
    [string] $RunRoot,
    [string] $OutputRoot,
    [ValidateRange(1, 5)][int] $Samples = 3,
    [ValidateRange(0, 30)][int] $IntervalSeconds = 4,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-SafeProbeError([Exception] $ErrorObject) {
    $leaf = $ErrorObject
    for ($depth = 0; $depth -lt 8 -and $null -ne $leaf.InnerException; $depth++) { $leaf = $leaf.InnerException }
    $result = [ordered]@{ type = $leaf.GetType().FullName }
    if ($leaf.GetType().FullName -ceq 'MySqlConnector.MySqlException') {
        $result.mysqlNumber = [int] $leaf.Number
        $result.sqlState = [string] $leaf.SqlState
    }
    return $result
}

function Invoke-SafeTimedProbe([string] $Name, [scriptblock] $Action) {
    $started = [DateTimeOffset]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $value = & $Action
        $watch.Stop()
        $success = $true
        if ($value -is [Collections.IDictionary]) {
            if ($value.Contains('httpStatus')) { $success = $value.httpStatus -ge 200 -and $value.httpStatus -lt 300 }
            foreach ($requiredTrue in @('runMatches', 'databaseMatches', 'isSimulation', 'validRecordedSummary', 'matchesPreparedKnownCount', 'pingOk')) {
                if ($value.Contains($requiredTrue) -and !$value[$requiredTrue]) { $success = $false }
            }
        }
        return [ordered]@{ name = $Name; success = $success; atUtc = $started.ToString('O'); durationMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 2); result = $value }
    }
    catch {
        $watch.Stop()
        return [ordered]@{ name = $Name; success = $false; atUtc = $started.ToString('O'); durationMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 2); error = ConvertTo-SafeProbeError $_.Exception }
    }
}

function Assert-ProbeBinding($Preparation, $Ready, [string] $ExpectedRoot, [string] $Guid) {
    $expectedRun = 'integrated-r108:' + $Guid
    $expectedDatabase = 'r108m_' + $Guid
    if ($Preparation.runStableId -cne $expectedRun -or $Ready.RunStableId -cne $expectedRun -or
        $Preparation.database -cne $expectedDatabase -or $Ready.database -cne $expectedDatabase -or
        $Preparation.schemaMode -cne 'MigrationFirst' -or $Ready.executionMode -cne 'Simulation' -or
        $Preparation.status -cne 'FreshDedicatedDatabasePrepared' -or
        ![string]::Equals([IO.Path]::GetFullPath($Preparation.outputRoot), $ExpectedRoot, [StringComparison]::OrdinalIgnoreCase) -or
        !$Ready.hostedServicesRemoved -or !$Ready.externalHttpBlocked -or
        $Ready.cloudPushEnabled -or $Ready.automaticPayoutEnabled -or $Ready.transportPrepaymentEnabled) {
        throw 'ReadOnlyProbeOwnedRunBindingRequired'
    }
}

if ($SelfTest) {
    $results = [Collections.Generic.List[object]]::new()
    function Test-Probe([string] $Name, [scriptblock] $Assertion) {
        & $Assertion
        $results.Add([ordered]@{ name = $Name; passed = $true })
    }
    Test-Probe 'successful-result-is-timed-once' {
        $script:calls = 0
        $value = Invoke-SafeTimedProbe 'fake' { $script:calls++; return @{ code = 200 } }
        if (!$value.success -or $script:calls -ne 1 -or $value.durationMs -lt 0 -or $value.result.code -ne 200) { throw 'TimedSuccessFailed' }
    }
    Test-Probe 'failure-is-not-retried-or-zero-success' {
        $script:calls = 0
        $value = Invoke-SafeTimedProbe 'fake' { $script:calls++; throw [InvalidOperationException]::new('private password=do-not-copy; SELECT customer_name') }
        $encoded = $value | ConvertTo-Json -Depth 5 -Compress
        if ($value.success -or $script:calls -ne 1 -or $encoded -match 'do-not-copy|customer_name|password=') { throw 'SafeFailureFailed' }
    }
    Test-Probe 'http-failure-and-binding-mismatch-are-not-success' {
        foreach ($bad in @(@{ httpStatus = 503 }, @{ httpStatus = 200; runMatches = $false }, @{ pingOk = $false })) {
            $value = Invoke-SafeTimedProbe 'fake' { $bad }
            if ($value.success) { throw 'InvalidResultTreatedAsSuccess' }
        }
    }
    Test-Probe 'nested-exception-keeps-only-type' {
        $value = ConvertTo-SafeProbeError ([Exception]::new('raw response', [TimeoutException]::new('secret-token')))
        if ($value.type -cne 'System.TimeoutException' -or ($value | ConvertTo-Json -Compress) -match 'raw response|secret-token') { throw 'NestedFailureFailed' }
    }
    Test-Probe 'operational-or-effects-enabled-binding-is-rejected' {
        $guid = 'a' * 32
        $root = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $guid))
        $prepared = @{ runStableId = 'integrated-r108:' + $guid; database = 'r108m_' + $guid; schemaMode = 'MigrationFirst'; status = 'FreshDedicatedDatabasePrepared'; outputRoot = $root }
        foreach ($bad in @('Operational', 'EnabledPush', 'WrongRun')) {
            $ready = @{ RunStableId = $prepared.runStableId; database = $prepared.database; executionMode = 'Simulation'; hostedServicesRemoved = $true; externalHttpBlocked = $true; cloudPushEnabled = $false; automaticPayoutEnabled = $false; transportPrepaymentEnabled = $false }
            if ($bad -eq 'Operational') { $ready.executionMode = 'Operational' }
            if ($bad -eq 'EnabledPush') { $ready.cloudPushEnabled = $true }
            if ($bad -eq 'WrongRun') { $ready.RunStableId = 'other' }
            $rejected = $false
            try { Assert-ProbeBinding $prepared $ready $root $guid } catch { $rejected = $true }
            if (!$rejected) { throw 'UnsafeBindingAccepted' }
        }
    }
    [ordered]@{ status = 'Passed'; tests = $results; runtimeOrDatabaseAccessed = $false } | ConvertTo-Json -Depth 5
    exit 0
}

if (!$RunRoot -or !$OutputRoot) { throw 'RunRootAndOutputRootRequired' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$guid = [IO.Path]::GetFileName($RunRoot)
$expected = [IO.Path]::GetFullPath((Join-Path $repo ('artifacts/local/integrated-workflow-r108/' + $guid)))
$allowedOutput = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/local/communication-verification-r2')) + [IO.Path]::DirectorySeparatorChar
if ($guid -cnotmatch '^[a-f0-9]{32}$' -or ![string]::Equals($RunRoot, $expected, [StringComparison]::OrdinalIgnoreCase) -or
    !$OutputRoot.StartsWith($allowedOutput, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReadOnlyProbeArtifactBoundaryRequired' }
$prepared = Get-Content -LiteralPath (Join-Path $RunRoot 'preparation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ready = Get-Content -LiteralPath (Join-Path $RunRoot 'ready.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-ProbeBinding $prepared $ready $RunRoot $guid
$private = Get-Content -LiteralPath (Join-Path $RunRoot '.private/environment.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($private.R108_DATABASE -cne $prepared.database -or ![string]::Equals([IO.Path]::GetFullPath($private.R108_ARTIFACT_ROOT), $RunRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReadOnlyProbePrivateBindingRequired' }
$schema = Get-Content -LiteralPath (Join-Path $RunRoot 'schema-migration-attempt.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($schema.state -cne 'Succeeded') { throw 'ReadOnlyProbeSucceededSchemaRequired' }
$bin = Join-Path $RunRoot 'host/bin'
$logging = Join-Path $bin 'Microsoft.Extensions.Logging.Abstractions.dll'
if (!(Test-Path -LiteralPath $logging)) {
    $framework = @(& dotnet --list-runtimes | ForEach-Object {
        if ($_ -match '^Microsoft\.AspNetCore\.App ([0-9.]+) \[(.+)\]$') {
            [pscustomobject]@{ version = [version] $Matches[1]; root = $Matches[2] }
        }
    } | Sort-Object version -Descending | Select-Object -First 1)
    if ($framework.Count -ne 1) { throw 'ReadOnlyProbeExistingAspNetRuntimeRequired' }
    $logging = Join-Path (Join-Path $framework[0].root $framework[0].version.ToString()) 'Microsoft.Extensions.Logging.Abstractions.dll'
}
Add-Type -Path $logging
foreach ($name in @('MySqlConnector.dll', 'MongoDB.Bson.dll', 'MongoDB.Driver.dll', 'DnsClient.dll')) { Add-Type -Path (Join-Path $bin $name) }
$sql = [MySqlConnector.MySqlConnectionStringBuilder]::new()
$sql.Server = '127.0.0.1'; $sql.Port = 13310; $sql.Database = $prepared.database; $sql.UserID = 'usb_r74'
$sql.Password = $private.USB_R74_SQL_PASSWORD; $sql.AllowUserVariables = $true; $sql.CharacterSet = 'utf8mb4'
$sql.Pooling = $false; $sql.ConnectionTimeout = 5; $sql.DefaultCommandTimeout = 5
$mongo = [MongoDB.Driver.MongoClient]::new('mongodb://127.0.0.1:27021/?serverSelectionTimeoutMS=5000&connectTimeoutMS=5000')
$httpHandler = [Net.Http.HttpClientHandler]::new(); $httpHandler.UseProxy = $false; $httpHandler.AllowAutoRedirect = $false
$http = [Net.Http.HttpClient]::new($httpHandler); $http.Timeout = [TimeSpan]::FromSeconds(20)
$rows = [Collections.Generic.List[object]]::new()
$definitions = @(
    @{ name = 'mysql-main'; table = '__EFMigrationsHistory' },
    @{ name = 'mysql-traditional-markets'; table = '__EFMigrationsHistory_TraditionalMarkets' },
    @{ name = 'mysql-agricultural-fisheries'; table = '__EFMigrationsHistory_AgriculturalFisheries' },
    @{ name = 'mysql-public-data-ingestion'; table = '__EFMigrationsHistory_PublicDataIngestion' }
)
try {
    for ($sample = 0; $sample -lt $Samples; $sample++) {
        if ($sample -gt 0 -and $IntervalSeconds -gt 0) { Start-Sleep -Seconds $IntervalSeconds }
        $checks = [Collections.Generic.List[object]]::new()
        foreach ($path in @('/health/ready', '/api/v1/common/home/participation-summary', '/verification/r108/proof')) {
            $checks.Add((Invoke-SafeTimedProbe $path {
                $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, ('http://127.0.0.1:5378' + $path))
                if ($path -eq '/verification/r108/proof') { $request.Headers.Add('x-r108-key', $private.USB_R74_ACCESS_KEY) }
                try {
                    $response = $http.SendAsync($request).GetAwaiter().GetResult()
                    try {
                        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                        $value = [ordered]@{ httpStatus = [int] $response.StatusCode }
                        if ($response.IsSuccessStatusCode -and $path -eq '/verification/r108/proof') {
                            $proof = $body | ConvertFrom-Json
                            $value.runMatches = $proof.RunStableId -ceq $prepared.runStableId
                            $value.databaseMatches = $proof.database -ceq $prepared.database
                        }
                        elseif ($response.IsSuccessStatusCode -and $path -eq '/api/v1/common/home/participation-summary') {
                            $summary = $body | ConvertFrom-Json
                            $value.isSimulation = $summary.ExecutionMode -ceq 'Simulation'
                            $value.validRecordedSummary = $summary.PeriodCode -ceq 'all-time' -and $summary.StatusCode -ceq 'recorded'
                        }
                        return $value
                    } finally { $response.Dispose() }
                } finally { $request.Dispose() }
            }))
        }
        foreach ($definition in $definitions) {
            $checks.Add((Invoke-SafeTimedProbe $definition.name {
                $connection = [MySqlConnector.MySqlConnection]::new($sql.ConnectionString)
                try {
                    $connection.Open()
                    $command = $connection.CreateCommand()
                    try {
                        $command.CommandText = 'SELECT COUNT(*) FROM `' + $definition.table + '`'
                        $count = [int] $command.ExecuteScalar()
                        $known = @($schema.Contexts | Where-Object Name -CEQ $definition.name)[0].KnownMigrations
                        return [ordered]@{ appliedMigrationCount = $count; matchesPreparedKnownCount = $count -eq $known }
                    } finally { $command.Dispose() }
                } finally { $connection.Dispose() }
            }))
        }
        $checks.Add((Invoke-SafeTimedProbe 'mongo-ping' {
            $database = $mongo.GetDatabase($prepared.database)
            $command = [MongoDB.Driver.BsonDocumentCommand[MongoDB.Bson.BsonDocument]]::new([MongoDB.Bson.BsonDocument]::new('ping', 1))
            $pong = $database.RunCommand[MongoDB.Bson.BsonDocument]($command, $null, [Threading.CancellationToken]::None)
            return [ordered]@{ pingOk = $pong['ok'].AsDouble -eq 1 }
        }))
        $rows.Add([ordered]@{ sample = $sample + 1; checks = $checks })
    }
} finally { $http.Dispose(); $httpHandler.Dispose(); Remove-Variable private }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$output = Join-Path $OutputRoot ('readiness-independent-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json')
$report = [ordered]@{
    schemaVersion = 'r108-independent-readiness-probes.r2'; atUtc = [DateTimeOffset]::UtcNow.ToString('O'); sameOwnedRun = $true
    source = 'Existing binaries and same loopback gateways; independent driver connections with pooling disabled'
    notSameLiveServerConnectionPool = $true; noClaimAboutPast503Cause = $true; rawResponsesOrSecretsRetained = $false
    mutationsOrServerRestarts = $false; sqlCommands = 'Four fixed migration-history COUNT SELECTs only'; samples = $rows
}
$stream = [IO.File]::Open($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try { $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($report | ConvertTo-Json -Depth 9)); $stream.Write($bytes) } finally { $stream.Dispose() }
[ordered]@{ status = 'Observed'; sampleCount = $rows.Count; result = $output } | ConvertTo-Json
