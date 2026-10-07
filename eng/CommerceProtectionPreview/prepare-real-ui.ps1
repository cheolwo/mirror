Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskArtifacts=Join-Path $taskRoot 'artifacts/local/apk-completion-r26'
$taskPrivate=Join-Path $taskArtifacts 'server/.private'
$taskAccounts=Get-Content (Join-Path $taskPrivate 'accounts.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskAccounts.baseUrl -ne 'http://127.0.0.1:5362/'){throw 'r26 isolated endpoint mismatch'}
$env:FOOD_OBSERVER_BASE_URL=$taskAccounts.baseUrl
$env:FOOD_OBSERVER_ACCOUNT_PASSWORD=$taskAccounts.password
$env:FOOD_OBSERVER_PRIVATE_UI_TOKENS=Join-Path $taskPrivate 'ui-sessions.json'
$taskOutput=Join-Path $taskArtifacts 'food-ui-preparation.json'
try {
    Push-Location $taskRoot
    & dotnet 'eng/Ssalddel.RoleAppHeadlessE2E/bin/Debug/net10.0/Ssalddel.RoleAppHeadlessE2E.dll' --prepare-ui *> $taskOutput
    if($LASTEXITCODE -ne 0){throw 'Actual role clients UI preparation failed; see local output'}
    $taskPrepared=Get-Content $taskOutput -Raw -Encoding UTF8 | ConvertFrom-Json
    if($taskPrepared.status -ne 'Prepared'){throw 'Order preparation not confirmed'}
    $taskPrepared | Select-Object schemaVersion,status,orderNo,restaurantId,menuId,workStableId,synthetic,deviceUiProof | ConvertTo-Json
} finally {
    Pop-Location
    Remove-Item Env:FOOD_OBSERVER_ACCOUNT_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:FOOD_OBSERVER_PRIVATE_UI_TOKENS -ErrorAction SilentlyContinue
}
