[CmdletBinding()]
param(
    [ValidateSet('Prepare','Build','Up','Status','Start','Result','Stop','NewSample')][string]$Action = 'Status',
    [ValidateRange(1024, 49151)][int]$HostPort = 5321,
    [switch]$AllowIdleSampleReplacement
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts/local/verification/food-observer'
$envFile = Join-Path $artifactRoot '.env'
$connectionFile = Join-Path $artifactRoot 'connection.json'
$composeFile = Join-Path $PSScriptRoot 'docker-compose.food-observer.yml'
$baseUrl = "http://127.0.0.1:$HostPort"
$env:FOOD_OBSERVER_HOST_PORT = $HostPort.ToString([Globalization.CultureInfo]::InvariantCulture)
function New-LocalBytes {
    $bytes = [byte[]]::new(32)
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    $bytes
}
function New-LocalSecret { [BitConverter]::ToString((New-LocalBytes)).Replace('-', '') }
if ($Action -eq 'Prepare') {
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    if (-not (Test-Path -LiteralPath $envFile)) {
        $values = [ordered]@{
            FOOD_OBSERVER_DB_PASSWORD = (New-LocalSecret)
            FOOD_OBSERVER_ROOT_PASSWORD = (New-LocalSecret)
            FOOD_OBSERVER_ACCESS_KEY = (New-LocalSecret)
            FOOD_OBSERVER_ACCOUNT_PASSWORD = ('Aa1' + (New-LocalSecret))
            FOOD_OBSERVER_JWT_KEY = (New-LocalSecret)
            FOOD_OBSERVER_AES_KEY = [Convert]::ToBase64String((New-LocalBytes))
            FOOD_OBSERVER_HASH_SALT = (New-LocalSecret)
            FOOD_OBSERVER_PROJECTION_KEY = (New-LocalSecret)
        }
        [IO.File]::WriteAllLines($envFile, @($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }), [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($connectionFile, (@{baseUrl=$baseUrl;accessKey=$values.FOOD_OBSERVER_ACCESS_KEY} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    } else {
        $lines = @(Get-Content -LiteralPath $envFile -Encoding UTF8)
        if (-not ($lines | Where-Object { $_ -match '^FOOD_OBSERVER_PROJECTION_KEY=' })) {
            $lines += "FOOD_OBSERVER_PROJECTION_KEY=$(New-LocalSecret)"
            [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
        }
        $connection = Get-Content -LiteralPath $connectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($connection.baseUrl -notmatch '^http://127\.0\.0\.1:\d+$') { throw 'Only an isolated loopback observer endpoint is allowed.' }
        $connection.baseUrl = $baseUrl
        [IO.File]::WriteAllText($connectionFile, ($connection | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    }
    Write-Output 'Prepared isolated food observer credentials (values omitted).'
    return
}
if (-not (Test-Path -LiteralPath $envFile) -or -not (Test-Path -LiteralPath $connectionFile)) { throw 'Run -Action Prepare first.' }
$composeArgs = @('compose','--project-name','ssalddel-food-observer','--env-file',$envFile,'-f',$composeFile)
switch ($Action) {
    'Build' { & docker @composeArgs build app; if ($LASTEXITCODE) { throw 'Observer image build failed.' }; return }
    'Up' {
        if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot 'menus.json') -PathType Leaf)) { throw 'Run export-food-observer-menus.ps1 first. No fixed-menu fallback.' }
        & docker @composeArgs up -d; if ($LASTEXITCODE) { throw 'Observer startup failed.' }; return
    }
    'Stop' { & docker @composeArgs stop; if ($LASTEXITCODE) { throw 'Observer stop failed.' }; return }
}
$connection = Get-Content -LiteralPath $connectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($connection.baseUrl -ne $baseUrl) { throw "Run -Action Prepare -HostPort $HostPort before using a different isolated loopback port." }
$headers = @{'X-Verification-Key'=$connection.accessKey}
$url = $connection.baseUrl + '/verification/food-delivery'
if ($Action -eq 'Start') {
    Invoke-RestMethod -Method Post -Uri ($url + '/start') -Headers $headers -TimeoutSec 15 | Out-Null
    Write-Output 'Started 300-second wall-clock verification; scenario duration is unchanged.'
    return
}
$snapshot = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 15
if ($Action -eq 'NewSample') {
    $idleReplacementAllowed = $AllowIdleSampleReplacement -and $snapshot.status -eq 'Idle' -and [string]::IsNullOrWhiteSpace($snapshot.orderNo)
    if ($snapshot.status -notin @('Failed','Completed','TimedOut','Waiting') -and -not $idleReplacementAllowed) {
        throw 'Only a terminal verification can be replaced, unless -AllowIdleSampleReplacement explicitly selects an unused observer run for external client validation.'
    }
    $sampleId = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText((Join-Path $artifactRoot "previous-$sampleId.json"), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    & docker @composeArgs stop
    if ($LASTEXITCODE) { throw 'Previous sample stop failed; volume configuration unchanged.' }
    $lines = @(Get-Content -LiteralPath $envFile | Where-Object { $_ -notmatch '^FOOD_OBSERVER_(APP|MYSQL|MONGO)_VOLUME=' })
    foreach ($kind in @('APP','MYSQL','MONGO')) { $lines += "FOOD_OBSERVER_${kind}_VOLUME=ssalddel-food-observer_${sampleId}_$($kind.ToLowerInvariant())" }
    [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
    Write-Output "Previous DB/result volumes preserved. New sample $sampleId selected; run -Action Up, then explicitly Start."
    return
}
if ($Action -eq 'Result') {
    [IO.File]::WriteAllText((Join-Path $artifactRoot 'result.json'), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
}
$snapshot | Select-Object status,elapsedSeconds,durationSeconds,orderNo,orderStatus,dispatchStatus,message | ConvertTo-Json
