[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputRoot,[Parameter(Mandatory)][string]$InputPath,[Parameter(Mandatory)][string]$Case)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$InputPath=[IO.Path]::GetFullPath($InputPath)
$taskAllowed=[IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts/local/integrated-workflow-r108'))+[IO.Path]::DirectorySeparatorChar
$taskMobileAllowed=[IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts/local/local-operations-r108/mobile/.private'))+[IO.Path]::DirectorySeparatorChar
if(!$OutputRoot.StartsWith($taskAllowed,[StringComparison]::OrdinalIgnoreCase) -or !$InputPath.StartsWith($taskMobileAllowed,[StringComparison]::OrdinalIgnoreCase) -or $Case -notmatch '^[a-z0-9][a-z0-9-]{0,47}$'){throw 'R108OwnedPathsAndCaseRequired'}
$taskReady=Get-Content -LiteralPath (Join-Path $OutputRoot 'ready.json') -Raw|ConvertFrom-Json
if($taskReady.baseUrl -ne 'http://127.0.0.1:5378' -or $taskReady.executionMode -ne 'Simulation' -or $taskReady.externalHttpBlocked -ne $true){throw 'R108SimulationBoundaryRequired'}
$taskEnv=Get-Content -LiteralPath (Join-Path $OutputRoot '.private/environment.json') -Raw|ConvertFrom-Json
$taskPrepared=Invoke-RestMethod -NoProxy -Uri ($taskReady.baseUrl+'/verification/member/prepare') -Method Post -ContentType 'application/json' -Headers @{'x-r108-key'=$taskEnv.USB_R74_ACCESS_KEY} -Body (@{case=$Case}|ConvertTo-Json)
$taskAccounts=Get-Content -LiteralPath (Join-Path $OutputRoot '.private/accounts.json') -Raw|ConvertFrom-Json
$taskAccount=@($taskAccounts.accounts|Where-Object {$_.userName -eq 'field-orderer'})[0]
$taskLogin=Invoke-RestMethod -NoProxy -Uri ($taskReady.baseUrl+'/api/v1/auth/login') -Method Post -ContentType 'application/json' -Body (@{userNameOrEmail=$taskAccount.userName;password=$taskAccount.password}|ConvertTo-Json)
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($InputPath)) -Force|Out-Null
$taskPhoto=Join-Path ([IO.Path]::GetDirectoryName($InputPath)) ($Case+'-synthetic.png')
[IO.File]::WriteAllBytes($taskPhoto,[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAeklEQVR4nO3PsQkAIBDAwN9/HhsbZ3MIi0MIZIDLrH2+brigAS1oQAsa0IIGtKABLWhACxrQgga0oAEtaEALGtCCBrSgAS1oQAsa0IIGtKABLWhACxrQgga0oAEtaEALGtCCBrSgAS1oQAsa0IIGtKABLWhACxrQgscum5qx/6CKIMgAAAAASUVORK5CYII='))
$taskInput=@{baseAddress=$taskReady.baseUrl;owner=$taskPrepared.memberUserId;accessToken=$taskLogin.accessToken;offerId=$taskPrepared.offerId;pickupLatitude=37.571;pickupLongitude=127.071;samplePhotoPath=$taskPhoto;nodeExecutable='C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'}
[IO.File]::WriteAllText($InputPath,($taskInput|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
@{status='Prepared';case=$Case;inputPath=$InputPath;expiresAtUtc=$taskPrepared.expiresAtUtc;credentialsPrinted=$false;fcmSent=$false}|ConvertTo-Json
