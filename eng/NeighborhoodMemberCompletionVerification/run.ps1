[CmdletBinding()]
param(
    [ValidateSet('Probe','Run')][string]$Action = 'Probe',
    [string]$OutputRoot,
    [string]$BuiltDllPath,
    [string]$MongoContainerName = 'hongdal-mongo-1',
    [switch]$CreateDedicatedMongo,
    [int]$DedicatedMongoPort = 27046
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

function Invoke-TaskDocker {
    param([string[]]$Arguments, [int]$TimeoutSeconds = 15, [switch]$AllowMissing)
    $taskStart = [Diagnostics.ProcessStartInfo]::new('docker')
    $taskStart.UseShellExecute = $false
    $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true
    $taskStart.RedirectStandardError = $true
    foreach ($taskArgument in $Arguments) { $taskStart.ArgumentList.Add($taskArgument) }
    $taskProcess = [Diagnostics.Process]::Start($taskStart)
    try {
        $taskStdout = $taskProcess.StandardOutput.ReadToEndAsync()
        $taskStderr = $taskProcess.StandardError.ReadToEndAsync()
        if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) {
            # 진단 CLI만 종료한다. Docker 서비스·컨테이너·다른 작업 프로세스는 건드리지 않는다.
            $taskProcess.Kill()
            throw 'DockerCommandTimedOut'
        }
        if ($taskProcess.ExitCode -ne 0) {
            if ($AllowMissing) { return $null }
            throw 'DockerCommandFailed'
        }
        return $taskStdout.GetAwaiter().GetResult()
    } finally { $taskProcess.Dispose() }
}

function Get-TaskContainer {
    param([string]$Name, [switch]$AllowMissing)
    $taskRaw = Invoke-TaskDocker -Arguments @('inspect', $Name) -AllowMissing:$AllowMissing
    if ($null -eq $taskRaw) { return $null }
    return @($taskRaw | ConvertFrom-Json)[0]
}

function Get-TaskEnvironmentValue {
    param($Container, [string]$Name)
    $taskPrefix = $Name + '='
    $taskValue = @($Container.Config.Env | Where-Object { $_.StartsWith($taskPrefix, [StringComparison]::Ordinal) })
    if ($taskValue.Count -ne 1) { throw 'ContainerCredentialUnavailable' }
    return $taskValue[0].Substring($taskPrefix.Length)
}

if ($Action -eq 'Probe') {
    $taskRows = @()
    $taskAvailable = $true
    try {
        $taskInventory = Invoke-TaskDocker -Arguments @('ps', '-a', '--format', '{{.Names}}|{{.Status}}|{{.Ports}}') -TimeoutSeconds 8
        $taskRows = @($taskInventory -split "`r?`n" | Where-Object {
            $_ -match '^(hongdal-(?:mysql|mongo)-1|hongdal-member-r46-mongo-[a-f0-9]{32})\|'
        })
    } catch { $taskAvailable = $false }
    [pscustomobject]@{ status = $(if ($taskAvailable) {'DockerInventoryAvailable'} else {'DockerUnavailable'});
        containers = $taskRows; readOnly = $true; databaseChanged = $false; dockerReset = $false } | ConvertTo-Json -Depth 3
    return
}

if (-not $BuiltDllPath) { $BuiltDllPath = Join-Path $PSScriptRoot 'bin/Debug/net10.0/NeighborhoodMemberCompletionVerification.dll' }
$taskDll = [IO.Path]::GetFullPath($BuiltDllPath)
if (-not (Test-Path -LiteralPath $taskDll -PathType Leaf)) { throw 'BuildTheVerificationProjectBeforeRun' }
$taskRun = [Guid]::NewGuid().ToString('N')
$taskDatabase = 'neighborhood_member_r46_' + $taskRun
if (-not $OutputRoot) { $OutputRoot = 'artifacts/local/neighborhood-member-completion-r46/' + $taskRun }
$taskOutput = [IO.Path]::GetFullPath((Join-Path $taskRepo $OutputRoot))
$taskAllowed = [IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts/local')) + [IO.Path]::DirectorySeparatorChar
if (-not $taskOutput.StartsWith($taskAllowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $taskOutput)) {
    throw 'FreshEvidenceDirectoryInsideArtifactsLocalRequired'
}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskStage = 'VerifyLocalMySql'
$taskCreatedMongo = $false
$taskOldSql = $env:SSALDDEL_MEMBER_R46_MYSQL
$taskOldMongo = $env:SSALDDEL_MEMBER_R46_MONGO
try {
    $taskMySql = Get-TaskContainer -Name 'hongdal-mysql-1'
    if (-not $taskMySql.State.Running -or $taskMySql.Config.Labels.'com.docker.compose.project' -ne 'hongdal' -or
        $taskMySql.Config.Labels.'com.docker.compose.service' -ne 'mysql' -or
        @($taskMySql.NetworkSettings.Ports.'3306/tcp' | Where-Object { $_.HostPort -eq '13306' }).Count -ne 1) {
        throw 'LocalMySqlContainerBoundaryMismatch'
    }
    $taskPassword = Get-TaskEnvironmentValue $taskMySql 'MYSQL_ROOT_PASSWORD'
    if ($taskDatabase -notmatch '^neighborhood_member_r46_[a-f0-9]{32}$') { throw 'TaskDatabaseIdentifierInvalid' }
    $taskStage = 'CreateOnlyFreshTaskDatabase'
    Invoke-TaskDocker -Arguments @('exec', '-e', ('MYSQL_PWD=' + $taskPassword), 'hongdal-mysql-1', 'mysql', '-uroot',
        '-e', "CREATE DATABASE ``$taskDatabase`` CHARACTER SET utf8mb4;") | Out-Null
    # 값을 파일·로그·보고서에 쓰지 않고 실행 프로세스 메모리로만 전달한다.
    $taskQuotedPassword = '"' + $taskPassword.Replace('"', '""') + '"'
    $env:SSALDDEL_MEMBER_R46_MYSQL = "Server=127.0.0.1;Port=13306;Database=$taskDatabase;User ID=root;Password=$taskQuotedPassword;Allow User Variables=true;Connection Timeout=10;Default Command Timeout=30"

    $taskStage = 'VerifyLoopbackMongo'
    if ($taskOldMongo) {
        $env:SSALDDEL_MEMBER_R46_MONGO = $taskOldMongo
        $taskMongoSource = 'ExplicitLoopbackEnvironment'
    } else {
        if ($MongoContainerName -notmatch '^(hongdal-mongo-1|hongdal-member-r46-mongo-[a-f0-9]{32})$') { throw 'MongoContainerIdentifierInvalid' }
        $taskMongo = Get-TaskContainer -Name $MongoContainerName -AllowMissing
        if ($null -eq $taskMongo -or -not $taskMongo.State.Running) {
            if (-not $CreateDedicatedMongo) { throw 'LocalMongoUnavailableUseExplicitDedicatedFixtureOption' }
            if ($DedicatedMongoPort -lt 1024 -or $DedicatedMongoPort -gt 65535 -or
                @(Get-NetTCPConnection -LocalPort $DedicatedMongoPort -State Listen -ErrorAction SilentlyContinue).Count -gt 0) {
                throw 'DedicatedMongoLoopbackPortUnavailable'
            }
            # 기존 Mongo를 시작·변경하지 않고 이 실행 소유의 별도 컨테이너만 만든다.
            Invoke-TaskDocker -Arguments @('image', 'inspect', 'mongo:8.0') | Out-Null
            $MongoContainerName = 'hongdal-member-r46-mongo-' + $taskRun
            $taskMongoPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            Invoke-TaskDocker -Arguments @('run', '-d', '--name', $MongoContainerName,
                '-p', "127.0.0.1:$DedicatedMongoPort`:27017", '-e', 'MONGO_INITDB_ROOT_USERNAME=member_r46_fixture',
                '-e', ('MONGO_INITDB_ROOT_PASSWORD=' + $taskMongoPassword), 'mongo:8.0') -TimeoutSeconds 30 | Out-Null
            $taskCreatedMongo = $true
            $taskMongo = Get-TaskContainer -Name $MongoContainerName
        }
        $taskMongoPorts = @($taskMongo.NetworkSettings.Ports.'27017/tcp' | Where-Object { $_.HostIp -in @('127.0.0.1','::1') })
        if (-not $taskMongo.State.Running -or $taskMongoPorts.Count -ne 1) { throw 'MongoLoopbackBindingMismatch' }
        $taskMongoUser = Get-TaskEnvironmentValue $taskMongo 'MONGO_INITDB_ROOT_USERNAME'
        $taskMongoPassword = Get-TaskEnvironmentValue $taskMongo 'MONGO_INITDB_ROOT_PASSWORD'
        $env:SSALDDEL_MEMBER_R46_MONGO = 'mongodb://' + [Uri]::EscapeDataString($taskMongoUser) + ':' +
            [Uri]::EscapeDataString($taskMongoPassword) + '@127.0.0.1:' + $taskMongoPorts[0].HostPort +
            '/?authSource=admin&serverSelectionTimeoutMS=15000'
        $taskMongoSource = $MongoContainerName
    }
    $taskStage = 'RunProductControllerHttp'
    $taskStartOptions = @{
        FilePath = 'dotnet'; WindowStyle = 'Hidden'; PassThru = $true; WorkingDirectory = $taskRepo
        ArgumentList = @($taskDll, '--evidence-root', $taskOutput)
        RedirectStandardOutput = (Join-Path $taskOutput 'runner.stdout.log')
        RedirectStandardError = (Join-Path $taskOutput 'runner.stderr.log')
    }
    $taskProcess = Start-Process @taskStartOptions
    try {
        if (-not $taskProcess.WaitForExit(480000)) { $taskProcess.Kill(); throw 'OwnVerificationHostTimedOut' }
        if ($taskProcess.ExitCode -ne 0) { throw 'ProductControllerVerificationFailedSeeSanitizedFailure' }
    } finally { $taskProcess.Dispose() }
    [pscustomobject]@{ status='Passed'; database=$taskDatabase; evidence=$taskOutput; mongoSource=$taskMongoSource;
        createdDedicatedMongo=$taskCreatedMongo; buildPerformed=$false; operationalDatabaseChanged=$false;
        installedApk=$false; dockerReset=$false; secretsRecorded=$false } | ConvertTo-Json -Depth 3
} catch {
    $taskFailure = [pscustomobject]@{status='Failed';stage=$taskStage;database=$taskDatabase;evidence=$taskOutput;
        createdDedicatedMongo=$taskCreatedMongo;container=$MongoContainerName;existingResourcesDeleted=$false;secretsRecorded=$false}
    $taskFailure | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $taskOutput 'pipeline-failure.json') -Encoding utf8
    $taskFailure | ConvertTo-Json -Depth 3
    throw 'TaskVerificationPipelineFailedSeeSanitizedLocalEvidence'
} finally {
    $env:SSALDDEL_MEMBER_R46_MYSQL = $taskOldSql
    $env:SSALDDEL_MEMBER_R46_MONGO = $taskOldMongo
}
