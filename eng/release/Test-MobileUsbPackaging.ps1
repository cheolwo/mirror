[CmdletBinding()]
param([string]$EvidencePath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'MobileFieldTest.Common.ps1')
$taskPublisher=Join-Path $PSScriptRoot 'publish-mobile-usb-test.ps1'
$taskTempRoot=Join-Path ([IO.Path]::GetTempPath()) ('ssalddel-usb-packaging-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskTempRoot)|Out-Null
$taskResults=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body){& $Body;$taskResults.Add([pscustomobject]@{name=$Name;status='Passed'})}
function MustReject([scriptblock]$Body,[string]$Pattern){
    $taskCaught=$null
    try{& $Body|Out-Null}catch{$taskCaught=$_}
    if($null -eq $taskCaught -or $taskCaught.Exception.Message -notmatch $Pattern){throw 'Expected packaging guard failure was not observed.'}
}
try{
    foreach($taskEndpoint in @('https://example.test/','http://example.test:5363/','http://127.0.0.1:5362/','http://10.0.2.2:5363/','http://127.0.0.1:5363/api/','http://x:y@127.0.0.1:5363/','http://127.0.0.1:5363/?token=test','http://127.0.0.1:5363/#test')){
        Check ('reject non-USB origin '+$taskEndpoint){MustReject {& $taskPublisher -ServerBaseAddress $taskEndpoint -PlanOnly} 'loopback HTTP origin'}
    }
    Check 'four food roles use standalone Debug APK arguments'{
        $taskPlan=& $taskPublisher -PlanOnly|ConvertFrom-Json
        if($taskPlan.configuration -ne 'Debug' -or $taskPlan.version -ne '0.1.27' -or $taskPlan.versionCode -ne 27 -or $taskPlan.apps.Count -ne 4){throw 'Default packaging plan mismatch.'}
        foreach($taskApp in $taskPlan.apps){
            foreach($taskRequired in @('Debug','-p:AndroidPackageFormats=apk','-p:EmbedAssembliesIntoApk=true','-p:AndroidUseSharedRuntime=false','-p:RuntimeIdentifiers="android-arm64;android-x64"','-p:AndroidKeyStore=true')){
                if($taskRequired -notin $taskApp.arguments){throw 'Required standalone/signed Debug argument missing.'}
            }
            if(@($taskApp.arguments|Where-Object{$_ -match 'Signing.*Pass=' -and $_ -notmatch '=env:'}).Count){throw 'Signing password literal leaked into command arguments.'}
        }
        if($taskPlan.automaticInstall -or $taskPlan.automaticUninstall -or $taskPlan.cloudDeployment){throw 'Packaging plan claims installation or deployment.'}
    }
    Check 'duplicate roles rejected'{MustReject {& $taskPublisher -AppNames @('orderer','orderer') -PlanOnly} 'distinct'}
    Check 'invalid version rejected'{MustReject {& $taskPublisher -Version 'r27' -PlanOnly} 'Version'}
    Check 'outside output rejected'{MustReject {& $taskPublisher -OutputRoot '../outside-apk' -PlanOnly} 'artifacts/local'}
    Check 'inside signing key rejected'{MustReject {& $taskPublisher -KeystorePath (Join-Path $taskRepoRoot 'debug.keystore') -PlanOnly} 'outside'}
    $taskApp=(Get-MobileFieldTestApps|Where-Object Name -eq 'orderer')
    $taskApk=Join-Path $taskTempRoot 'fixture.apk'
    $taskArchive=[IO.Compression.ZipFile]::Open($taskApk,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($taskAbi in @('arm64-v8a','x86_64')){[void]$taskArchive.CreateEntry("lib/$taskAbi/libdotnet.so")}}finally{$taskArchive.Dispose()}
    $taskCertificate='a'*64
    $taskInvoker={param($Tool,$ToolArguments)
        $taskText=switch($Tool){
            signer{'Signer #1 certificate SHA-256 digest: '+$(if($script:usbFailure -eq 'certificate'){'b'*64}else{$taskCertificate})}
            align{'aligned'}
            aapt{"package: name='$($taskApp.PackageId)' versionCode='27' versionName='0.1.27'"+$(if($script:usbFailure -ne 'not-debuggable'){[Environment]::NewLine+'application-debuggable'}else{''})}
        }
        [pscustomobject]@{ExitCode=$(if(($script:usbFailure -eq 'signature' -and $Tool -eq 'signer') -or ($script:usbFailure -eq 'alignment' -and $Tool -eq 'align')){1}else{0});Output=$taskText}
    }
    $taskVerify=@{Path=$taskApk;ExpectedApp=$taskApp;Version='0.1.27';VersionCode=27;Tools=@{apksigner='signer';zipalign='align';aapt='aapt'};ExpectedCertificateSha256=$taskCertificate;ToolInvoker=$taskInvoker}
    foreach($taskFailure in @('signature','certificate','alignment','not-debuggable')){
        $script:usbFailure=$taskFailure
        Check ('Debug final APK rejects '+$taskFailure){MustReject {Confirm-MobileSignedApk @taskVerify -Configuration Debug} 'Final APK'}
    }
    $script:usbFailure='none'
    Check 'explicit Debug metadata stays distinct from Release and device proof'{
        $taskResult=Confirm-MobileSignedApk @taskVerify -Configuration Debug
        if(-not $taskResult.debugMetadataVerified -or $taskResult.releaseMetadataVerified -or $taskResult.physicalDeviceVerified -or $taskResult.serverCommunicationVerified){throw 'Debug verification overstates proof.'}
    }
    Check 'default Release still rejects Debug APK'{MustReject {Confirm-MobileSignedApk @taskVerify} 'Release metadata'}
    $taskArchive=[IO.Compression.ZipFile]::Open($taskApk,[IO.Compression.ZipArchiveMode]::Update)
    try{$taskArchive.GetEntry('lib/x86_64/libdotnet.so').Delete()}finally{$taskArchive.Dispose()}
    Check 'missing native ABI blocks Debug handoff'{MustReject {Confirm-MobileSignedApk @taskVerify -Configuration Debug} 'native runtime'}
    $taskSummary=[pscustomobject]@{schemaVersion='mobile-usb-packaging-tests.r27';status='Passed';count=$taskResults.Count;checks=$taskResults;syntheticApkFixture=$true;realApkBuilt=$false;physicalDeviceVerified=$false}
    if($EvidencePath){$taskEvidence=[IO.Path]::GetFullPath($EvidencePath);[IO.Directory]::CreateDirectory((Split-Path $taskEvidence -Parent))|Out-Null;[IO.File]::WriteAllText($taskEvidence,($taskSummary|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))}
    $taskSummary|ConvertTo-Json -Depth 5
}finally{
    if(Test-Path -LiteralPath $taskTempRoot){
        $taskResolvedTemp=[IO.Path]::GetFullPath($taskTempRoot)
        if(-not $taskResolvedTemp.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($taskResolvedTemp) -notlike 'ssalddel-usb-packaging-*'){throw 'Fixture cleanup path mismatch.'}
        Remove-Item -LiteralPath $taskResolvedTemp -Recurse -Force
    }
}
