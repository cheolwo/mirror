[CmdletBinding()]
param([string]$EvidencePath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$testRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'MobileFieldTest.Common.ps1')
$testTemp=Join-Path ([IO.Path]::GetTempPath()) ('ssalddel-firebase-packaging-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testTemp)|Out-Null
$testFile=Join-Path $testTemp 'google-services.json'
$testApp=Get-MobileFieldTestApps|Where-Object Name -eq 'shipper'
$testResults=[Collections.Generic.List[object]]::new()
$testPrevious=[Environment]::GetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON','Process')
function Check([string]$Name,[scriptblock]$Body){& $Body;$testResults.Add([pscustomobject]@{name=$Name;status='Passed'})}
function MustReject([scriptblock]$Body,[string]$Pattern){
    $caught=$null;try{& $Body|Out-Null}catch{$caught=$_}
    if($null -eq $caught -or $caught.Exception.Message -notmatch $Pattern){throw 'Expected Firebase packaging guard was not observed.'}
}
function Write-Fixture([string]$Package='com.companyname.ssalddelapp',[string]$Project='fixture-member-project'){
    $payload=@{project_info=@{project_id=$Project;project_number='1234567890'};client=@(@{client_info=@{mobilesdk_app_id='1:1234567890:android:fixture';android_client_info=@{package_name=$Package}};api_key=@(@{current_key='fixture-key-must-not-be-recorded'})})}
    [IO.File]::WriteAllText($testFile,($payload|ConvertTo-Json -Depth 9),[Text.UTF8Encoding]::new($false))
}
try{
    [Environment]::SetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON',$null,'Process')
    Check 'missing optional configuration preserves offline packaging and freezes SDK absence'{
        $config=Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo
        $publishArgs=@(Get-MobileFieldTestPublishArguments $testApp.Project 'out' '0.1.1' 1 'http://127.0.0.1:5363/' 'external.keystore' 'alias' -FirebaseConfiguration $config)
        if($config.Configured -or '-p:MemberFirebaseGoogleServicesJson=' -notin $publishArgs){throw 'Unconfigured member packaging must remain explicit.'}
    }
    Check 'missing configured file fails closed'{MustReject {Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson $testFile} 'not found'}
    Check 'repository config cannot be used as an external member SDK file'{MustReject {Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson (Join-Path $testRepo 'DriverApp/google-services.json')} 'outside the repository'}
    Write-Fixture -Package 'kr.hongdal.driver'
    Check 'another Android app cannot be repackaged as the member app'{MustReject {Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson $testFile} 'package does not match'}
    Write-Fixture
    Check 'explicit project comparison refuses another project without disclosing values'{MustReject {Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson $testFile -ExpectedProjectId 'another-project'} 'project does not match'}
    $config=Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson $testFile -ExpectedProjectId 'fixture-member-project'
    Check 'validated package and project are bound by hashes without JSON key values'{
        $evidence=Get-MobileMemberFirebaseEvidence $config -VerifiedUnchanged
        $text=$evidence|ConvertTo-Json -Depth 4
        if(-not $evidence.configured -or $evidence.projectComparison -ne 'MatchedExplicitProject' -or
            $evidence.projectIdSha256 -notmatch '^[a-f0-9]{64}$' -or $evidence.googleServicesSha256Before -ne $evidence.googleServicesSha256After -or
            $text.Contains('fixture-member-project') -or $text.Contains('fixture-key-must-not-be-recorded') -or $text.Contains($testFile)){throw 'Firebase evidence must contain only reviewed metadata and hashes.'}
    }
    Check 'unchanged external bytes pass recheck'{Confirm-MobileMemberFirebaseConfigurationUnchanged -App $testApp -RepositoryRoot $testRepo -Before $config -ExpectedProjectId 'fixture-member-project'}
    [IO.File]::AppendAllText($testFile,"`n")
    Check 'even metadata-preserving byte changes prevent handoff'{MustReject {Confirm-MobileMemberFirebaseConfigurationUnchanged -App $testApp -RepositoryRoot $testRepo -Before $config -ExpectedProjectId 'fixture-member-project'} 'changed during packaging'}
    Write-Fixture
    Check 'USB plan preserves Debug origin and isolates member Firebase from cargo'{
        $plan=& (Join-Path $PSScriptRoot 'publish-mobile-usb-test.ps1') -AppNames @('shipper','cargo-driver') -MemberFirebaseGoogleServicesJson $testFile -ExpectedMemberFirebaseProjectId 'fixture-member-project' -PlanOnly|ConvertFrom-Json
        $member=$plan.apps|Where-Object name -eq 'shipper';$cargo=$plan.apps|Where-Object name -eq 'cargo-driver'
        if($plan.configuration -ne 'Debug' -or $plan.serverBaseAddress -ne 'http://127.0.0.1:5363/' -or
            -not $member.memberFirebase.configured -or $member.memberFirebase.configurationUnchanged -or
            ('-p:MemberFirebaseGoogleServicesJson='+$testFile) -notin $member.arguments -or $cargo.memberFirebase.configured -or
            @($cargo.arguments|Where-Object{$_ -like '-p:MemberFirebaseGoogleServicesJson=*'}).Count){throw 'USB role/configuration boundary changed.'}
    }
    Check 'HTTPS plan reuses the same external configuration and separate project comparison'{
        $plan=@(& (Join-Path $PSScriptRoot 'publish-mobile-field-test.ps1') -ServerBaseAddress 'https://example.test/' -AppNames shipper -MemberFirebaseGoogleServicesJson $testFile -ExpectedMemberFirebaseProjectId 'fixture-member-project' -PlanOnly -InformationAction SilentlyContinue)
        if($plan.Count -ne 1 -or -not $plan[0].MemberFirebase.configured -or $plan[0].MemberFirebase.projectComparison -ne 'MatchedExplicitProject'){throw 'HTTPS Firebase plan mismatch.'}
    }
    Check 'explicit member config requires the member role'{MustReject {& (Join-Path $PSScriptRoot 'publish-mobile-usb-test.ps1') -AppNames driver -MemberFirebaseGoogleServicesJson $testFile -PlanOnly} 'requires the shipper'}
    Check 'environment fallback is validated and frozen as an explicit build argument'{
        [Environment]::SetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON',$testFile,'Process')
        $publishArgs=@(Get-MobileFieldTestPublishArguments $testApp.Project 'out' '0.1.1' 1 'https://example.test/' 'external.keystore' 'alias')
        if(('-p:MemberFirebaseGoogleServicesJson='+$testFile) -notin $publishArgs){throw 'Reviewed SDK input was not pinned to the build.'}
        [Environment]::SetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON',$null,'Process')
    }
    [IO.File]::WriteAllText($testFile,'{invalid JSON',[Text.UTF8Encoding]::new($false))
    Check 'invalid JSON is rejected with a redacted error'{MustReject {Resolve-MobileMemberFirebaseConfiguration -App $testApp -RepositoryRoot $testRepo -GoogleServicesJson $testFile} 'JSON or Android metadata is invalid'}
    $summary=[pscustomobject]@{schemaVersion='mobile-firebase-packaging-tests.r1';status='Passed';count=$testResults.Count;checks=$testResults;syntheticConfigurationOnly=$true;realApkBuilt=$false;actualPushSent=$false}
    if($EvidencePath){$target=[IO.Path]::GetFullPath($EvidencePath);[IO.Directory]::CreateDirectory((Split-Path $target -Parent))|Out-Null;[IO.File]::WriteAllText($target,($summary|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))}
    $summary|ConvertTo-Json -Depth 5
}finally{
    [Environment]::SetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON',$testPrevious,'Process')
    # Only synthetic input is created here. Preserve it for inspection; no user credential file is copied or removed.
}
