[CmdletBinding()]
param(
    [string]$ServerBaseAddress = 'http://127.0.0.1:5363/',
    [string]$Version = '0.1.27',
    [int]$VersionCode = 27,
    [string]$KeystorePath,
    [string]$OutputRoot = 'artifacts/local/food-mobile-four-role-r27/apk',
    [ValidateNotNullOrEmpty()]
    [ValidateSet('orderer','restaurant','driver','admin')]
    [string[]]$AppNames = @('orderer','restaurant','driver','admin'),
    [string]$AndroidSdkDirectory,
    [switch]$UseRestaurantFoodTestFixture,
    [switch]$PlanOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'MobileFieldTest.Common.ps1')
$taskServer=$null
if(-not [Uri]::TryCreate($ServerBaseAddress,[UriKind]::Absolute,[ref]$taskServer) -or
    $taskServer.Scheme -ne 'http' -or -not $taskServer.IsLoopback -or $taskServer.Port -ne 5363 -or
    $taskServer.Host -notin @('127.0.0.1','localhost','[::1]','::1') -or $taskServer.AbsolutePath -ne '/' -or
    $taskServer.UserInfo -or $taskServer.Query -or $taskServer.Fragment){
    throw 'USB test requires a loopback HTTP origin on port 5363 without credentials, path, query or fragment.'
}
if($Version -notmatch '^\d+\.\d+\.\d+$' -or $VersionCode -lt 1){throw 'Version must use x.y.z and VersionCode must be at least 1.'}
if($AppNames.Count -eq 0 -or @($AppNames|Sort-Object -Unique).Count -ne $AppNames.Count){throw 'AppNames must contain distinct supported food roles.'}
if($UseRestaurantFoodTestFixture -and 'restaurant' -notin $AppNames){throw 'Restaurant food-test fixture opt-in requires the restaurant role.'}
$taskApps=@(Get-MobileFieldTestApps|Where-Object{$_.Name -in $AppNames})
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRepoRoot $OutputRoot))
$taskAllowedRoot=[IO.Path]::GetFullPath((Join-Path $taskRepoRoot 'artifacts/local'))+[IO.Path]::DirectorySeparatorChar
if(-not $taskOutput.StartsWith($taskAllowedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'USB APK output must remain in the ignored artifacts/local directory.'}
if(!$KeystorePath){
    $taskDefaultKey=Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.android/debug.keystore'
    $taskMauiKey=Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'Xamarin/Mono for Android/debug.keystore'
    $KeystorePath=if(Test-Path -LiteralPath $taskDefaultKey -PathType Leaf){$taskDefaultKey}elseif(Test-Path -LiteralPath $taskMauiKey -PathType Leaf){$taskMauiKey}else{$taskDefaultKey}
}
$taskKeystore=[IO.Path]::GetFullPath($KeystorePath)
if($taskKeystore.StartsWith($taskRepoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Debug keystore must remain outside the repository.'}

function Get-MobileUsbPublishArguments {
    param($App,$Destination)
    $taskArguments=@(Get-MobileFieldTestPublishArguments (Join-Path $taskRepoRoot $App.Project) $Destination $Version $VersionCode $taskServer.AbsoluteUri $taskKeystore 'androiddebugkey')
    $taskConfigurationIndex=[Array]::IndexOf($taskArguments,'Release')
    if($taskConfigurationIndex -lt 0){throw 'Reviewed common packaging configuration was not found.'}
    $taskArguments[$taskConfigurationIndex]='Debug'
    $taskArguments+=@('-p:EmbedAssembliesIntoApk=true','-p:AndroidUseAssemblyStore=false','-p:AndroidUseSharedRuntime=false','-p:AndroidDebuggable=true')
    if($UseRestaurantFoodTestFixture -and $App.Name -eq 'restaurant'){
        $taskArguments+='-p:SsalddelUsbFoodTestFixture=sagajeong-food-mobile-dispatch.r1'
    }
    # Keep every referenced project's intermediate files away from concurrent
    # checkout builds. Project names and ABI pivots remain SDK-controlled.
    # The reviewed CodeMetadata local props otherwise fixes a shared obj path;
    # the root props preserves exclusions while this opt-in keeps it outside UPM.
    $taskIsolated=if($Destination -eq '<per-run-output>'){'<per-run-build-artifacts>'}else{
        $taskRunName=Split-Path (Split-Path $Destination -Parent) -Leaf
        if($taskRunName -notmatch '^build-([a-f0-9]{32})$'){throw 'Reviewed per-run build directory required.'}
        # Android's native resource tools still require short Windows paths.
        # Keep unique intermediates in a shorter ignored sibling evidence root.
        Join-Path $taskRepoRoot ('artifacts/local/r27-apk-builds/'+$Matches[1]+'/'+$App.Name)
    }
    $taskArguments+=@('-p:UseArtifactsOutput=true',"-p:ArtifactsPath=$taskIsolated","-p:DirectoryBuildPropsPath=$(Join-Path $taskRepoRoot 'Directory.Build.props')")
    $taskArguments
}
if($PlanOnly){
    [pscustomobject]@{schemaVersion='mobile-usb-test-plan.r27';configuration='Debug';version=$Version;versionCode=$VersionCode;serverBaseAddress=$taskServer.AbsoluteUri;restaurantFoodTestFixtureEnabled=[bool]$UseRestaurantFoodTestFixture;apkOnly=$true;embeddedStandalone=$true;automaticInstall=$false;automaticUninstall=$false;cloudDeployment=$false;apps=@($taskApps|ForEach-Object{[pscustomobject]@{name=$_.Name;project=$_.Project;packageId=$_.PackageId;arguments=@(Get-MobileUsbPublishArguments $_ '<per-run-output>')}})}|ConvertTo-Json -Depth 6
    return
}
if(-not (Test-Path -LiteralPath $taskKeystore -PathType Leaf)){throw 'Existing external .android/debug.keystore required; this tool does not create or replace signing keys.'}
$taskKeyAncestor=Get-Item -LiteralPath $taskKeystore -Force
while($null -ne $taskKeyAncestor){
    if(($taskKeyAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Debug keystore must not traverse a symbolic link or junction.'}
    $taskKeyAncestor=if($taskKeyAncestor -is [IO.FileInfo]){$taskKeyAncestor.Directory}else{$taskKeyAncestor.Parent}
}
$taskReleaseRoot=Join-Path $taskOutput "$Version-$VersionCode"
if(Test-Path -LiteralPath (Join-Path $taskReleaseRoot 'usb-test-manifest.json')){throw 'Completed USB APK handoff already exists; use a new VersionCode or OutputRoot to preserve it.'}
$taskTools=Resolve-MobileAndroidBuildTools -AndroidSdkDirectory $AndroidSdkDirectory
$taskProjectQueue=[Collections.Generic.Queue[string]]::new()
$taskProjectSet=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($taskApp in $taskApps){$taskProjectQueue.Enqueue((Join-Path $taskRepoRoot $taskApp.Project))}
while($taskProjectQueue.Count -gt 0){
    $taskProject=[IO.Path]::GetFullPath($taskProjectQueue.Dequeue())
    if(-not $taskProjectSet.Add($taskProject)){continue}
    if(-not $taskProject.StartsWith($taskRepoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reviewed project reference escaped the repository.'}
    [xml]$taskProjectXml=Get-Content -LiteralPath $taskProject -Raw -Encoding UTF8
    foreach($taskReference in $taskProjectXml.SelectNodes("//*[local-name()='ProjectReference']")){
        $taskReferencePath=$taskReference.GetAttribute('Include')
        if($taskReferencePath -match '\$\('){throw 'A dynamic project reference requires source fingerprint review.'}
        $taskProjectQueue.Enqueue([IO.Path]::GetFullPath((Join-Path (Split-Path $taskProject -Parent) $taskReferencePath)))
    }
}
$taskSourceRoots=@($taskProjectSet|ForEach-Object{[IO.Path]::GetRelativePath($taskRepoRoot,(Split-Path $_ -Parent)).Replace('\','/')}|Sort-Object -Unique)
function Get-MobileUsbSourceSnapshot {
    $taskFiles=@(& git -C $taskRepoRoot -c core.quotepath=false ls-files -c -o --exclude-standard)
    if($LASTEXITCODE -ne 0){throw 'Git source inventory failed.'}
    $taskSelected=@($taskFiles|Where-Object{
        $taskPath=$_
        $taskPath -notmatch '(^|/)(bin|obj|artifacts|\.artifacts|\.codex-build)/' -and
        (@($taskSourceRoots|Where-Object{$taskPath.StartsWith($_+'/',[StringComparison]::Ordinal)}).Count -gt 0 -or
            $taskPath -match '^(Directory\.[^/]+|global\.json|NuGet\.config)$' -or
            $taskPath -in @('eng/release/publish-mobile-usb-test.ps1','eng/release/MobileFieldTest.Common.ps1','eng/release/MobileFieldTest.PortableReferences.targets'))
    }|Sort-Object -Unique)
    $taskHashes=@($taskSelected|ForEach-Object{
        $taskPath=$_
        if(Test-Path -LiteralPath (Join-Path $taskRepoRoot $taskPath) -PathType Leaf){[pscustomobject]@{path=$taskPath;sha256=(Get-FileHash -LiteralPath (Join-Path $taskRepoRoot $taskPath) -Algorithm SHA256).Hash.ToLowerInvariant()}}
    })
    $taskInput=[Text.Encoding]::UTF8.GetBytes(($taskHashes|ForEach-Object{$_.path+':'+$_.sha256}) -join "`n")
    $taskFingerprint=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskInput)).ToLowerInvariant()
    $taskHead=(& git -C $taskRepoRoot rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Git revision read failed.'}
    [pscustomobject]@{head=$taskHead;fingerprint=$taskFingerprint;files=$taskHashes;dirty=@(& git -C $taskRepoRoot status --porcelain)}
}
$taskBefore=Get-MobileUsbSourceSnapshot
$taskRunId=[Guid]::NewGuid().ToString('N')
[IO.Directory]::CreateDirectory($taskReleaseRoot)|Out-Null
[IO.File]::WriteAllText((Join-Path $taskReleaseRoot "$taskRunId-source-before.json"),($taskBefore|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
$taskPriorStorePassword=[Environment]::GetEnvironmentVariable('SSALDDEL_ANDROID_KEYSTORE_PASSWORD','Process')
$taskPriorKeyPassword=[Environment]::GetEnvironmentVariable('SSALDDEL_ANDROID_KEY_PASSWORD','Process')
try{
    # Android standard debug signing only. Existing user environment is restored even on failure.
    [Environment]::SetEnvironmentVariable('SSALDDEL_ANDROID_KEYSTORE_PASSWORD','android','Process')
    [Environment]::SetEnvironmentVariable('SSALDDEL_ANDROID_KEY_PASSWORD','android','Process')
    $taskCertificate=Get-MobileSigningCertificateFingerprint -KeystorePath $taskKeystore -Alias 'androiddebugkey'
    [IO.Directory]::CreateDirectory($taskReleaseRoot)|Out-Null
    $taskApks=@()
    foreach($taskApp in $taskApps){
        $taskDestination=Join-Path $taskReleaseRoot ("build-$taskRunId/"+$taskApp.Name)
        [IO.Directory]::CreateDirectory($taskDestination)|Out-Null
        $taskArguments=@(Get-MobileUsbPublishArguments $taskApp $taskDestination)
        & dotnet @taskArguments *> (Join-Path $taskReleaseRoot ("$($taskApp.Name)-$taskRunId-build.log"))
        if($LASTEXITCODE -ne 0){throw "USB Android build failed for $($taskApp.Name); inspect its local build log."}
        $taskPackages=@(Get-ChildItem -LiteralPath $taskDestination -Recurse -File|Where-Object{$_.Name -like '*-Signed.apk'})
        if($taskPackages.Count -ne 1){throw "Exactly one signed Debug APK is required for $($taskApp.Name)."}
        $taskPackage=$taskPackages[0]
        $taskVerify=Confirm-MobileSignedApk -Path $taskPackage.FullName -ExpectedApp $taskApp -Version $Version -VersionCode $VersionCode -Tools $taskTools -ExpectedCertificateSha256 $taskCertificate -Configuration Debug
        $taskArchive=[IO.Compression.ZipFile]::OpenRead($taskPackage.FullName)
        try{
            $taskAssembly=[IO.Path]::GetFileNameWithoutExtension($taskApp.Project)
            foreach($taskAbi in @('arm64-v8a','x86_64')){
                if(-not $taskArchive.GetEntry("lib/$taskAbi/lib_$taskAssembly.dll.so") -and -not $taskArchive.GetEntry("assemblies/$taskAbi/$taskAssembly.dll") -and -not $taskArchive.GetEntry("assemblies/$taskAssembly.dll")){throw 'Final USB APK embedded app assembly is missing.'}
            }
        }finally{$taskArchive.Dispose()}
        $taskApks+=[pscustomobject]@{app=$taskApp.Name;packageId=$taskApp.PackageId;version=$Version;versionCode=$VersionCode;server=$taskServer.AbsoluteUri;file=[IO.Path]::GetRelativePath($taskReleaseRoot,$taskPackage.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $taskPackage.FullName -Algorithm SHA256).Hash.ToLowerInvariant();bytes=$taskPackage.Length;embeddedStandaloneVerified=$true;verification=$taskVerify}
    }
    $taskAfter=Get-MobileUsbSourceSnapshot
[IO.File]::WriteAllText((Join-Path $taskReleaseRoot "$taskRunId-source-after.json"),($taskAfter|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    if($taskBefore.head -ne $taskAfter.head -or $taskBefore.fingerprint -ne $taskAfter.fingerprint){throw 'Selected source changed during APK builds; no completed handoff manifest was created.'}
    $taskHandoff=Join-Path $taskReleaseRoot 'handoff'
    [IO.Directory]::CreateDirectory($taskHandoff)|Out-Null
    foreach($taskApk in $taskApks){
        $taskCopy=Join-Path $taskHandoff ($taskApk.app+'-usb-debug.apk')
        if(Test-Path -LiteralPath $taskCopy){throw 'Existing APK handoff is preserved; use a new output/version.'}
        Copy-Item -LiteralPath (Join-Path $taskReleaseRoot $taskApk.file) -Destination $taskCopy
        if((Get-FileHash -LiteralPath $taskCopy -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskApk.sha256){throw 'Handoff APK bytes differ from verified build bytes.'}
        $taskApk|Add-Member -NotePropertyName handoffFile -NotePropertyValue ('handoff/'+[IO.Path]::GetFileName($taskCopy))
        $taskApk|Add-Member -NotePropertyName handoffHashVerified -NotePropertyValue $true
    }
    $taskManifest=[pscustomobject]@{schemaVersion='mobile-usb-test.r27';generatedAtUtc=[DateTime]::UtcNow.ToString('o');status='Packaged';configuration='Debug';serverBaseAddress=$taskServer.AbsoluteUri;restaurantFoodTestFixtureEnabled=[bool]$UseRestaurantFoodTestFixture;version=$Version;versionCode=$VersionCode;head=$taskBefore.head;dirtyWorktree=$taskBefore.dirty.Count -gt 0;dirtyPaths=$taskBefore.dirty;sourceRoots=$taskSourceRoots;sourceFingerprint=$taskBefore.fingerprint;sourceFingerprints=$taskBefore.files;sourceChangedDuringBuild=$false;isolatedBuildOutputs=$true;apks=$taskApks;automaticInstall=$false;automaticUninstall=$false;deviceDataRemoved=$false;usbReverseRequired=$true;adbReverseCommand="adb reverse tcp:5363 tcp:5363";physicalDeviceVerified=$false;serverCommunicationVerified=$false;cloudDeployment=$false;credentialsRecorded=$false}
    $taskJson=$taskManifest|ConvertTo-Json -Depth 9
    [IO.File]::WriteAllText((Join-Path $taskReleaseRoot 'usb-test-manifest.json'),$taskJson,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $taskHandoff 'usb-test-manifest.json'),$taskJson,[Text.UTF8Encoding]::new($false))
    Write-Output "Created $($taskApks.Count) verified local USB Debug APKs: $taskReleaseRoot. Installation and phone/server communication are not verified."
}finally{
    [Environment]::SetEnvironmentVariable('SSALDDEL_ANDROID_KEYSTORE_PASSWORD',$taskPriorStorePassword,'Process')
    [Environment]::SetEnvironmentVariable('SSALDDEL_ANDROID_KEY_PASSWORD',$taskPriorKeyPassword,'Process')
}
