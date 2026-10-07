[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("staging")]
    [string]$EnvironmentName,

    [Parameter(Mandatory)]
    [ValidatePattern("^REHEARSE_STAGING_RECOVERY$")]
    [string]$Confirmation,

    [string]$EvidenceDirectory = "artifacts/staging-readiness",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'AzureMobileHardening.Common.ps1')
$startedAtUtc = [DateTimeOffset]::UtcNow
$runId = $startedAtUtc.ToString("yyyyMMddHHmmss")
$evidencePath = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($EvidenceDirectory)) {
    $EvidenceDirectory
} else { Join-Path (Get-Location) $EvidenceDirectory }))
$redisContainerName = "ssalddel-redis-restore-$runId"
$redisContainerStarted = $false
$mysqlPasswordBefore = $env:MYSQL_PWD
$redisPasswordBefore = $env:REDISCLI_AUTH
$defaultConnectionBefore = $env:ConnectionStrings__DefaultConnection

function Get-RequiredEnvironmentValue([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is missing."
    }

    return $value
}

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath' failed with exit code $LASTEXITCODE."
    }
}

try {
    if ($Confirmation -ne "REHEARSE_STAGING_RECOVERY") {
        throw "Explicit recovery rehearsal confirmation is required."
    }

    New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null

    $mysqlHost = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_HOST"
    $mysqlPort = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_PORT"
    $mysqlUser = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_USER"
    $mysqlPassword = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_PASSWORD"
    $mysqlDatabase = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_DATABASE"
    $mysqlRestoreDatabase = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_RESTORE_DATABASE"
    $mysqlConnectionString = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MYSQL_CONNECTION"
    Assert-RecoveryDatabaseTargets -Source $mysqlDatabase -Restore $mysqlRestoreDatabase -Engine 'MySQL'

    $mongoUri = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MONGODB_URI"
    $mongoDatabase = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MONGODB_DATABASE"
    $mongoRestoreDatabase = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_MONGODB_RESTORE_DATABASE"
    Assert-RecoveryDatabaseTargets -Source $mongoDatabase -Restore $mongoRestoreDatabase -Engine 'MongoDB'
    Assert-StagingMySqlMigrationTarget -ConnectionString $mysqlConnectionString -Database $mysqlDatabase -Server $mysqlHost -Port $mysqlPort -User $mysqlUser

    $redisHost = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_REDIS_HOST"
    $redisPort = Get-RequiredEnvironmentValue "SSALDDEL_STAGING_REDIS_PORT"
    $redisPassword = [Environment]::GetEnvironmentVariable("SSALDDEL_STAGING_REDIS_PASSWORD")

    Get-Command dotnet, mysqldump, mysql, mongodump, mongorestore, redis-cli, docker -ErrorAction Stop | Out-Null

    # Retain a pre-migration backup before changing any source schema.
    $mysqlPreMigrationBackup = Join-Path $evidencePath "mysql-before-migration.sql"
    $env:MYSQL_PWD = $mysqlPassword
    Invoke-Checked "mysqldump" @(
        "--host=$mysqlHost", "--port=$mysqlPort", "--user=$mysqlUser",
        "--single-transaction", "--routines", "--events",
        "--result-file=$mysqlPreMigrationBackup", $mysqlDatabase
    )
    $env:ConnectionStrings__DefaultConnection = $mysqlConnectionString
    $migrationContexts = @(Get-StagingMigrationContextNames)
    foreach ($contextName in $migrationContexts) {
        Invoke-Checked "dotnet" @(
            "ef", "database", "update",
            "--project", "Ssalddel/Ssalddel.csproj",
            "--startup-project", "Ssalddel/Ssalddel.csproj",
            "--context", $contextName, "--configuration", $Configuration, "--no-build"
        )
    }
    $mysqlBackup = Join-Path $evidencePath "mysql.sql"
    $env:MYSQL_PWD = $mysqlPassword
    Invoke-Checked "mysqldump" @(
        "--host=$mysqlHost",
        "--port=$mysqlPort",
        "--user=$mysqlUser",
        "--single-transaction",
        "--routines",
        "--events",
        "--result-file=$mysqlBackup",
        $mysqlDatabase
    )
    Invoke-Checked "mysql" @(
        "--host=$mysqlHost",
        "--port=$mysqlPort",
        "--user=$mysqlUser",
        "--execute=DROP DATABASE IF EXISTS ``$mysqlRestoreDatabase``; CREATE DATABASE ``$mysqlRestoreDatabase`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
    )
    Invoke-Checked "mysql" @(
        "--host=$mysqlHost",
        "--port=$mysqlPort",
        "--user=$mysqlUser",
        "--database=$mysqlRestoreDatabase",
        "--execute=source $($mysqlBackup.Replace('\', '/'));"
    )
    Invoke-Checked "mysql" @(
        "--host=$mysqlHost",
        "--port=$mysqlPort",
        "--user=$mysqlUser",
        "--database=$mysqlRestoreDatabase",
        "--execute=SELECT COUNT(*) FROM community_post_email_notification_outbox; SELECT COUNT(*) FROM public_data_ingestion_runs;"
    )

    $mongoBackupRoot = Join-Path $evidencePath "mongo"
    Invoke-Checked "mongodump" @("--uri=$mongoUri", "--db=$mongoDatabase", "--out=$mongoBackupRoot")
    Invoke-Checked "mongorestore" @(
        "--uri=$mongoUri",
        "--drop",
        "--nsFrom=$mongoDatabase.*",
        "--nsTo=$mongoRestoreDatabase.*",
        (Join-Path $mongoBackupRoot $mongoDatabase)
    )

    $redisBackupDirectory = Join-Path $evidencePath "redis"
    New-Item -ItemType Directory -Path $redisBackupDirectory -Force | Out-Null
    $redisBackup = Join-Path $redisBackupDirectory "dump.rdb"
    if (-not [string]::IsNullOrWhiteSpace($redisPassword)) {
        $env:REDISCLI_AUTH = $redisPassword
    }
    Invoke-Checked "redis-cli" @("-h", $redisHost, "-p", $redisPort, "--rdb", $redisBackup)
    Invoke-Checked "docker" @(
        "run", "--detach", "--name", $redisContainerName,
        "--volume", "${redisBackupDirectory}:/data",
        "redis:7.4-alpine",
        "redis-server", "--appendonly", "no"
    )
    $redisContainerStarted = $true
    Start-Sleep -Seconds 2
    Invoke-Checked "docker" @("exec", $redisContainerName, "redis-cli", "PING")
    Invoke-Checked "docker" @("exec", $redisContainerName, "redis-cli", "DBSIZE")

    $evidence = [ordered]@{
        environment = $EnvironmentName
        startedAtUtc = $startedAtUtc
        completedAtUtc = [DateTimeOffset]::UtcNow
        migration = "applied"
        migrationContexts = $migrationContexts
        mysqlPreMigrationBackupSha256 = (Get-FileHash -LiteralPath $mysqlPreMigrationBackup -Algorithm SHA256).Hash
        mysqlRestoreDatabase = $mysqlRestoreDatabase
        mongoRestoreDatabase = $mongoRestoreDatabase
        redisRestoreContainer = $redisContainerName
        mysqlBackupSha256 = (Get-FileHash -LiteralPath $mysqlBackup -Algorithm SHA256).Hash
        redisBackupSha256 = (Get-FileHash -LiteralPath $redisBackup -Algorithm SHA256).Hash
    }
    $evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidencePath "evidence.json") -Encoding utf8
}
finally {
    if ($redisContainerStarted -and (Get-Command docker -ErrorAction SilentlyContinue)) {
        & docker rm --force $redisContainerName 2>$null | Out-Null
    }
    $env:MYSQL_PWD = $mysqlPasswordBefore
    $env:REDISCLI_AUTH = $redisPasswordBefore
    $env:ConnectionStrings__DefaultConnection = $defaultConnectionBefore
}
