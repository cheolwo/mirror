[CmdletBinding()]
param(
    [ValidateSet('Prepare','Build','Up','Status','Start','Pause','Resume','Retry','Result','Stop','NewSample')]
    [string]$Action = 'Status',
    [string]$RunStableId = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts/local/verification/observable-operations'
$envFile = Join-Path $artifactRoot '.env'
$connectionFile = Join-Path $artifactRoot 'connection.json'
$composeFile = Join-Path $PSScriptRoot 'docker-compose.observable-operations.yml'

if ($Action -eq 'Prepare') {
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    if (-not (Test-Path -LiteralPath $envFile)) {
        function New-LocalSecret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
        $values = [ordered]@{
            OBSERVABLE_OPERATIONS_DB_PASSWORD = (New-LocalSecret)
            OBSERVABLE_OPERATIONS_ROOT_PASSWORD = (New-LocalSecret)
            OBSERVABLE_OPERATIONS_ACCESS_KEY = (New-LocalSecret)
            OBSERVABLE_OPERATIONS_JWT_KEY = (New-LocalSecret)
            OBSERVABLE_OPERATIONS_AES_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            OBSERVABLE_OPERATIONS_HASH_SALT = (New-LocalSecret)
        }
        [IO.File]::WriteAllLines($envFile, @($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }), [Text.UTF8Encoding]::new($false))
    }
    $preparedValues = @{}
    foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
        $pair = $line -split '=', 2
        if ($pair.Count -eq 2) { $preparedValues[$pair[0]] = $pair[1] }
    }
    $preparedAccessKey = $preparedValues['OBSERVABLE_OPERATIONS_ACCESS_KEY']
    if ([string]::IsNullOrWhiteSpace($preparedAccessKey)) { throw 'Prepared access key is missing.' }
    [IO.File]::WriteAllText($connectionFile, (@{baseUrl='http://127.0.0.1:53216';accessKey=$preparedAccessKey} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Output 'Prepared isolated observable-operations credentials (values omitted).'
    return
}

if (-not (Test-Path -LiteralPath $envFile) -or -not (Test-Path -LiteralPath $connectionFile)) {
    throw 'Run -Action Prepare first.'
}
$composeArgs = @('compose','--project-name','ssalddel-observable-operations','--env-file',$envFile,'-f',$composeFile)
switch ($Action) {
    'Build' { & docker @composeArgs build app; if ($LASTEXITCODE) { throw 'Observable operations image build failed.' }; return }
    'Up' { & docker @composeArgs up -d; if ($LASTEXITCODE) { throw 'Observable operations startup failed.' }; return }
    'Stop' { & docker @composeArgs stop; if ($LASTEXITCODE) { throw 'Observable operations stop failed.' }; return }
}

$connection = Get-Content -LiteralPath $connectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($connection.baseUrl -ne 'http://127.0.0.1:53216') { throw 'Only the isolated loopback endpoint is allowed.' }
$headers = @{'X-Verification-Key'=$connection.accessKey}
$url = $connection.baseUrl + '/verification/observable-operations'
if ($Action -eq 'NewSample') {
    $snapshot = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 15
    if ($snapshot.statusCode -notin @('Completed','Idle')) { throw 'Only a terminal verification can be replaced by a new isolated sample.' }
    $sampleId = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText((Join-Path $artifactRoot "previous-$sampleId.json"), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    & docker @composeArgs stop
    if ($LASTEXITCODE) { throw 'Previous sample stop failed; volume configuration unchanged.' }
    $lines = @(Get-Content -LiteralPath $envFile | Where-Object { $_ -notmatch '^OBSERVABLE_OPERATIONS_(APP|MYSQL|MONGO)_VOLUME=' })
    foreach ($kind in @('APP','MYSQL','MONGO')) {
        $lines += "OBSERVABLE_OPERATIONS_${kind}_VOLUME=ssalddel-observable-operations_${sampleId}_$($kind.ToLowerInvariant())"
    }
    [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
    Write-Output "Previous DB/result volumes preserved. New sample $sampleId selected; run Up, then explicitly Start."
    return
}

$method = 'Get'
$body = $null
switch ($Action) {
    'Start' {
        $method = 'Post'
        $url += '/start'
        $body = @{runStableId=if ($RunStableId) {$RunStableId} else {$null}} | ConvertTo-Json
    }
    'Pause' { $method = 'Post'; $url += '/pause' }
    'Resume' { $method = 'Post'; $url += '/resume' }
    'Retry' { $method = 'Post'; $url += '/outbox/retry' }
}
$invoke = @{Method=$method;Uri=$url;Headers=$headers;TimeoutSec=20}
if ($null -ne $body) { $invoke.ContentType = 'application/json'; $invoke.Body = $body }
$snapshot = Invoke-RestMethod @invoke
if ($Action -eq 'Result') {
    [IO.File]::WriteAllText((Join-Path $artifactRoot 'result.json'), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
}
$snapshot | Select-Object runStableId,statusCode,areaStableId,elapsedSeconds,durationSeconds,publishedCaseCount,pendingOutboxCount,failedOutboxCount | ConvertTo-Json
