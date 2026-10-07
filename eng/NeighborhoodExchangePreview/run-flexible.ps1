param([int]$Port = 5542)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$evidence = Join-Path $repo 'artifacts/local/neighborhood-flexible-delivery-r22'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$sqlInfo = (docker inspect hongdal-mysql-1 | ConvertFrom-Json)[0]
$mysqlRootPassword = ($sqlInfo.Config.Env | Where-Object { $_.StartsWith('MYSQL_ROOT_PASSWORD=') }) -replace '^MYSQL_ROOT_PASSWORD=', ''
if (!$mysqlRootPassword) { throw 'MySQL fixture root credential not available.' }
$dbFile = Join-Path $evidence 'mysql-database.txt'
$dbName = if (Test-Path $dbFile) { (Get-Content $dbFile -Raw).Trim() } else { 'neighborhood_flex_r22_' + [guid]::NewGuid().ToString('N') }
if ($dbName -notmatch '^neighborhood_flex_r22_[a-f0-9]{32}$') { throw 'Unexpected isolated database name.' }
docker exec -e "MYSQL_PWD=$mysqlRootPassword" hongdal-mysql-1 mysql -uroot -e "CREATE DATABASE IF NOT EXISTS $dbName CHARACTER SET utf8mb4;"
if ($LASTEXITCODE) { throw 'MySQL isolated database setup failed.' }
$env:SSALDDEL_FLEX_MYSQL = "Server=127.0.0.1;Port=13306;Database=$dbName;User ID=root;Password=$mysqlRootPassword;Allow User Variables=true"
# Task-specific authenticated Mongo fixture; existing Mongo security and records are untouched.
$mongoName = 'hongdal-neighborhood-flex-r22-mongo'
$exists = docker ps -a --format '{{.Names}}' | Where-Object { $_ -eq $mongoName }
if (!$exists) {
    $mongoFixturePassword = [guid]::NewGuid().ToString('N')
    docker run -d --name $mongoName -p 127.0.0.1:27028:27017 -e MONGO_INITDB_ROOT_USERNAME=flex_fixture -e "MONGO_INITDB_ROOT_PASSWORD=$mongoFixturePassword" mongo:8.0 | Out-Null
    if ($LASTEXITCODE) { throw 'Task Mongo fixture failed to start.' }
}
$mongoInfo = (docker inspect $mongoName | ConvertFrom-Json)[0]
if (!$mongoInfo.State.Running) { docker start $mongoName | Out-Null; if ($LASTEXITCODE) { throw 'Task Mongo restart failed.' } }
$mongoPassword = ($mongoInfo.Config.Env | Where-Object { $_.StartsWith('MONGO_INITDB_ROOT_PASSWORD=') }) -replace '^MONGO_INITDB_ROOT_PASSWORD=', ''
$env:SSALDDEL_PREVIEW_MONGO_CONNECTION = 'mongodb://flex_fixture:' + [uri]::EscapeDataString($mongoPassword) + '@127.0.0.1:27028/?authSource=admin&serverSelectionTimeoutMS=10000'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$proc = Start-Process dotnet -WindowStyle Hidden -PassThru -WorkingDirectory $PSScriptRoot -ArgumentList @('bin/Debug/net10.0/NeighborhoodExchangePreview.dll', '--CollaborationPreview=true', '--FlexibleDeliveryPreview=true', "--FlexiblePort=$Port", "--FlexibleMigrationProbe=$($Port -eq 5542)") -RedirectStandardOutput (Join-Path $evidence "host-$Port.log") -RedirectStandardError (Join-Path $evidence "host-$Port.error.log")
$proc.Id | Set-Content (Join-Path $evidence "host-$Port.pid")
Remove-Item Env:SSALDDEL_FLEX_MYSQL, Env:SSALDDEL_PREVIEW_MONGO_CONNECTION
Write-Output "Started isolated loopback profile on $Port; PID $($proc.Id)."
