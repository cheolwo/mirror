[CmdletBinding()]
param([string] $Version = '0.1.46', [int] $VersionCode = 146)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $repo
. (Join-Path $repo 'eng/release/MobileFieldTest.Common.ps1')
$evidence = Join-Path $repo 'artifacts/local/member-completion-r46-20261009/apk'
$output = Join-Path $evidence 'publish'
$handoff = Join-Path $evidence 'handoff'
New-Item -ItemType Directory -Path $output, $handoff -Force | Out-Null
$app = Get-MobileFieldTestApps | Where-Object Name -eq shipper
$keystore = 'C:/Users/user/AppData/Local/Xamarin/Mono for Android/debug.keystore'
if (-not (Test-Path -LiteralPath $keystore)) { throw 'Existing Android debug keystore missing.' }
$sourcePaths = @(& rg --files SsalddelApp Ssalddel.Ui.Common Ssalddel.Contracts Ssalddel.Client.Infrastructure eng/role-workspace) + @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','eng/release/MobileFieldTest.PortableReferences.targets')
$sourcePaths = @($sourcePaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Sort-Object -Unique)
function Get-PublishSourceHashes { @($sourcePaths | ForEach-Object { [pscustomobject]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } }) }
$before = Get-PublishSourceHashes
$before | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'source-before.json') -Encoding utf8
$oldStorePass = $env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD
$oldKeyPass = $env:SSALDDEL_ANDROID_KEY_PASSWORD
$oldFirebase = $env:SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON
$oldJava = $env:JAVA_HOME
try {
    $env:JAVA_HOME = 'C:/Program Files/Android/OpenJDK/jdk-21.0.8'
    $env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD = 'android'
    $env:SSALDDEL_ANDROID_KEY_PASSWORD = 'android'
    $env:SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON = $null
    $certificate = Get-MobileSigningCertificateFingerprint $keystore 'androiddebugkey'
    $publishArgs = @(Get-MobileFieldTestPublishArguments $app.Project $output $Version $VersionCode 'http://127.0.0.1:5363/' $keystore 'androiddebugkey')
    $publishArgs[5] = 'Debug'
    $publishArgs += @('-p:EmbedAssembliesIntoApk=true','-p:AndroidUseAssemblyStore=false','-p:AndroidUseSharedRuntime=false','-p:AndroidDebuggable=true',
        '-p:UseArtifactsOutput=true',"-p:ArtifactsPath=$repo/artifacts/local/r46-apk", "-p:DirectoryBuildPropsPath=$repo/Directory.Build.props", '-v:q')
    & dotnet @publishArgs *> (Join-Path $evidence 'publish.log')
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $evidence 'publish.log') -Tail 25; throw 'Standalone APK publish failed.' }
    $apks = @(Get-ChildItem -LiteralPath $output -Filter '*Signed.apk' -File)
    if ($apks.Count -ne 1) { throw 'Expected exactly one signed general app APK.' }
    $verify = Confirm-MobileSignedApk -Path $apks[0].FullName -ExpectedApp $app -Version $Version -VersionCode $VersionCode -Tools (Resolve-MobileAndroidBuildTools) -ExpectedCertificateSha256 $certificate -Configuration Debug
    $archive = [IO.Compression.ZipFile]::OpenRead($apks[0].FullName)
    try {
        $entries = @($archive.Entries.FullName)
        foreach ($abi in @('arm64-v8a','x86_64')) {
            foreach ($assembly in @('SsalddelApp.dll','Ssalddel.Ui.Common.dll','Ssalddel.Contracts.dll','Ssalddel.Client.Infrastructure.dll')) {
                if (-not @($entries | Where-Object { $_ -eq "assemblies/$abi/$assembly" -or $_ -like "lib/$abi/lib_$assembly.so" }).Count) { throw "Embedded assembly missing: $abi/$assembly" }
            }
        }
        if (@($entries | Where-Object { $_ -match 'google-services|firebase-compile-only-fixture' }).Count) { throw 'Unexpected external Firebase fixture in user APK.' }
    } finally { $archive.Dispose() }
    $after = Get-PublishSourceHashes
    $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'source-after.json') -Encoding utf8
    if (Compare-Object ($before | ConvertTo-Json -Depth 5) ($after | ConvertTo-Json -Depth 5)) { throw 'APK source changed during publish; handoff withheld.' }
    $target = Join-Path $handoff "ssalddel-member-local-$Version.apk"
    Copy-Item -LiteralPath $apks[0].FullName -Destination $target
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($hash -ne (Get-FileHash -LiteralPath $apks[0].FullName -Algorithm SHA256).Hash) { throw 'Handoff copy hash mismatch.' }
    [pscustomobject]@{ app = $app.Name; packageId = $app.PackageId; version = $Version; versionCode = $VersionCode; apk = $target; sha256 = $hash;
        bytes = (Get-Item -LiteralPath $target).Length; verification = $verify; embeddedAssembliesVerified = $true; sourceUnchanged = $true;
        firebaseConfigured = $false; actualPushReceived = $false; physicalDeviceVerified = $false; azureVerified = $false;
        serverBaseAddress = 'http://127.0.0.1:5363/'; connectionRequirement = 'Future USB adb reverse or separately configured reachable local server';
        memberFeatureDefaultEnabled = $false; generatedAtUtc = [DateTime]::UtcNow.ToString('O') } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $evidence 'verification.json')
} finally {
    $env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD = $oldStorePass
    $env:SSALDDEL_ANDROID_KEY_PASSWORD = $oldKeyPass
    $env:SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON = $oldFirebase
    $env:JAVA_HOME = $oldJava
}
