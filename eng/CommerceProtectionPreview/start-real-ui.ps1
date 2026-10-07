Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskArtifacts=Join-Path $taskRoot 'artifacts/local/apk-completion-r26'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:COMMERCE_PREVIEW_REAL_ACCOUNTS=Join-Path $taskArtifacts 'server/.private/accounts.json'
$env:COMMERCE_PREVIEW_REAL_TOKENS=Join-Path $taskArtifacts 'server/.private/ui-sessions.json'
if(!(Test-Path $env:COMMERCE_PREVIEW_REAL_TOKENS)){throw 'Prepare UI sessions first'}
if(!(Test-Path (Join-Path $taskRoot 'eng/CommerceProtectionPreview/bin/Debug/net10.0/verification-shared-ui.css'))){throw 'Copy the compiled product scoped CSS into the independent preview output first'}
if(Get-NetTCPConnection -LocalPort 5396 -State Listen -ErrorAction SilentlyContinue){throw 'Preview port is already occupied; do not replace another process'}
$taskProcess=Start-Process -FilePath 'dotnet' -ArgumentList 'eng/CommerceProtectionPreview/bin/Debug/net10.0/CommerceProtectionPreview.dll' -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskArtifacts 'real-preview.stdout.log') -RedirectStandardError (Join-Path $taskArtifacts 'real-preview.stderr.log')
[IO.File]::WriteAllText((Join-Path $taskArtifacts 'real-preview-process.json'),(@{processId=$taskProcess.Id;endpoint='http://127.0.0.1:5396/';serverEndpoint='http://127.0.0.1:5362/';startedAtUtc=[DateTime]::UtcNow.ToString('o');fakeFallback=$false;tokensLogged=$false}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output "Real shared UI preview started: process $($taskProcess.Id), port 5396"
