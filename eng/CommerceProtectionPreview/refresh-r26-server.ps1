Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskServer=Join-Path $taskRoot 'artifacts/local/apk-completion-r26/server'
$taskResultFile=Join-Path $taskServer 'result.json'
$taskResult=Get-Content $taskResultFile -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskResult.composeProject -ne 'mirror-r26-food-check' -or $taskResult.baseUrl -ne 'http://127.0.0.1:5362/'){throw 'Unexpected project ownership'}
$taskEnv=Join-Path $taskServer '.private/.env'
$taskCompose=Join-Path $taskServer 'compose.yml'
$taskApp='mirror-r26-food-check-app-1'
if((& docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $taskApp).Trim() -ne $taskResult.composeProject){throw 'App container ownership mismatch'}
Copy-Item $taskResultFile (Join-Path $taskServer 'result-initial-image.json')
$taskResult.imageTag='mirror-r26-food-check:'+$taskResult.runId+'-final'
$taskSourceFiles=@(Get-ChildItem (Join-Path $taskRoot 'Ssalddel'),(Join-Path $taskRoot 'Ssalddel.Contracts') -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.json' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Sort-Object FullName)
$taskResult.sourcesBeforeBuild=@($taskSourceFiles | ForEach-Object { @{path=[IO.Path]::GetRelativePath($taskRoot,$_.FullName).Replace('\','/');sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
[IO.File]::WriteAllText($taskResultFile,($taskResult|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
$taskEnvText=[IO.File]::ReadAllText($taskEnv)
$taskEnvText=[regex]::Replace($taskEnvText,'(?m)^R26_FOOD_IMAGE=.*$',('R26_FOOD_IMAGE='+$taskResult.imageTag))
[IO.File]::WriteAllText($taskEnv,$taskEnvText,[Text.UTF8Encoding]::new($false))
& (Join-Path $taskServer 'build-image.ps1')
if($LASTEXITCODE -ne 0){throw 'Final source Docker build failed'}
$taskResult=Get-Content $taskResultFile -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskResult.build.changedDuringBuild.Count -gt 0){throw 'Server source changed during final build'}
& docker compose --project-name $taskResult.composeProject --env-file $taskEnv -f $taskCompose up -d --no-build --no-deps --force-recreate app gateway *> (Join-Path $taskServer 'final-image-replacement.log')
if($LASTEXITCODE -ne 0){throw 'Own app/gateway replacement failed'}
$taskDeadline=[DateTime]::UtcNow.AddSeconds(90)
$taskReady=$false
while([DateTime]::UtcNow -lt $taskDeadline){
 try { $taskHealth=Invoke-WebRequest 'http://127.0.0.1:5362/verification/health' -TimeoutSec 5 -SkipHttpErrorCheck; if($taskHealth.StatusCode -eq 204){$taskReady=$true;break} } catch {}
 Start-Sleep -Seconds 2
}
if(!$taskReady){throw 'Final server health timeout'}
$taskResult.status='FinalImageReady_ExistingSyntheticVolumesPreserved'
$taskResult | Add-Member -Force -NotePropertyName finalImage -NotePropertyValue @{readyAtUtc=[DateTime]::UtcNow.ToString('o');imageId=$taskResult.build.imageId;sourceChangedDuringBuild=$taskResult.build.changedDuringBuild;databaseVolumesPreserved=$true;onlyOwnAppGatewayReplaced=$true;healthStatus=204;existingLoginSessionsReused=$true}
[IO.File]::WriteAllText($taskResultFile,($taskResult|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
$taskResult | Select-Object status,baseUrl,imageTag,finalImage | ConvertTo-Json -Depth 5
