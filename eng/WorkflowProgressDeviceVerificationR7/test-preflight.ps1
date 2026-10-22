param(
    [Parameter(Mandatory)][string]$DllPath,
    [Parameter(Mandatory)][string]$FirebaseConfigurationPath
)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runName='preflight-'+[Guid]::NewGuid().ToString('N')
$runRoot=Join-Path $repository 'artifacts/local/workflow-progress-device/20261022-r1'
$private=Join-Path $runRoot ('.private/'+$runName)
$output=Join-Path $runRoot $runName
[IO.Directory]::CreateDirectory($private)|Out-Null
[IO.Directory]::CreateDirectory($output)|Out-Null
$syntheticToken='r7-preflight-only-synthetic-token-never-sent'
$tokenHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($syntheticToken))).ToLowerInvariant()
$template=@{
    schemaVersion='workflow-progress-device-input.r7';sampleId=$runName;ownerUserId='synthetic-owner';
    appKey='ssalddel.shipper';packageId='com.companyname.ssalddelapp';installationId='synthetic-installation';
    installationOwnerUserId='synthetic-owner';installationAppKey='ssalddel.shipper';installationPlatform='Android';
    installationActive=$true;activeInstallationCount=1;installationAppVersion='preflight-only';
    registrationVerifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');token=$syntheticToken;tokenSha256=$tokenHash;
    firebaseConfigurationPath=[IO.Path]::GetFullPath($FirebaseConfigurationPath);sourceKind='SyntheticDeviceSample';
    stageCode='warehouse.picked';eventId=('a'*64);sourceId='synthetic-source';deepLink='/workspace/warehouse';
    occurredAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
}
$cases=@(
    @{name='valid';success=$true;changes=@{}},
    @{name='owner';success=$false;changes=@{installationOwnerUserId='other-owner'}},
    @{name='count';success=$false;changes=@{activeInstallationCount=2}},
    @{name='inactive';success=$false;changes=@{installationActive=$false}},
    @{name='app';success=$false;changes=@{installationAppKey='other-app'}},
    @{name='platform';success=$false;changes=@{installationPlatform='android'}},
    @{name='hash';success=$false;changes=@{tokenSha256=('b'*64)}},
    @{name='route';success=$false;changes=@{deepLink='https://example.test/'}},
    @{name='stage';success=$false;changes=@{stageCode='warehouse.unknown'}},
    @{name='event';success=$false;changes=@{eventId='invalid'}},
    @{name='registration-time';success=$false;changes=@{registrationVerifiedAtUtc=[DateTimeOffset]::UtcNow.AddHours(-1).ToString('O')}},
    @{name='event-time';success=$false;changes=@{occurredAtUtc=[DateTimeOffset]::UtcNow.AddHours(-1).ToString('O')}},
    @{name='source-kind';success=$false;changes=@{sourceKind='Operational'}}
)
$results=@()
foreach($case in $cases){
    $input=$template.Clone()
    foreach($key in $case.changes.Keys){$input[$key]=$case.changes[$key]}
    $inputPath=Join-Path $private ($case.name+'.json')
    $stream=[IO.File]::Open($inputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
    try{$bytes=[Text.Encoding]::UTF8.GetBytes(($input|ConvertTo-Json));$stream.Write($bytes)}finally{$stream.Dispose()}
    $lines=@(& dotnet $DllPath --validate $inputPath (Join-Path $output $case.name) 2>&1)
    $actual=$LASTEXITCODE -eq 0
    if($actual -ne $case.success){throw ('PreflightMismatch:'+$case.name)}
    $results+=@{case=$case.name;passed=$true;networkRequested=$false;status=($lines -join ' ')}
}
$existingResult=Join-Path $output 'valid'
$priorHash=(Get-FileHash -LiteralPath (Join-Path $existingResult 'validation.json') -Algorithm SHA256).Hash
$reuse=@(& dotnet $DllPath --validate (Join-Path $private 'valid.json') $existingResult 2>&1)
if($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath (Join-Path $existingResult 'validation.json') -Algorithm SHA256).Hash -ne $priorHash){throw 'ExistingOutputChanged'}
$results+=@{case='existing-output-preserved';passed=$true;networkRequested=$false;status=($reuse -join ' ')}
@{schemaVersion='workflow-progress-device-preflight-tests.r7';passed=$results.Count;failed=0;actualSendExecuted=$false;results=$results}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
Write-Output ('WorkflowProgressPreflightPassed:'+ $results.Count+'; no send command or network request executed; '+$output)
