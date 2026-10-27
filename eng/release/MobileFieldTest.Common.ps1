function Get-MobileFieldTestApps {
    @(
        [pscustomobject]@{ Name = 'orderer'; Project = 'OrdererApp/OrdererApp.csproj'; PackageId = 'com.ssalddel.ordererapp' },
        [pscustomobject]@{ Name = 'restaurant'; Project = 'RestaurantDeskApp/RestaurantDeskApp.csproj'; PackageId = 'com.ssalddel.restaurantdeskapp' },
        [pscustomobject]@{ Name = 'driver'; Project = 'FDriverApp/FDriverApp.csproj'; PackageId = 'kr.ssalddel.fdriver' },
        [pscustomobject]@{ Name = 'admin'; Project = 'SsalddelAdminApp/SsalddelAdminApp.csproj'; PackageId = 'com.ssalddel.adminapp' },
        [pscustomobject]@{ Name = 'shipper'; Project = 'SsalddelApp/SsalddelApp.csproj'; PackageId = 'com.companyname.ssalddelapp' },
        [pscustomobject]@{ Name = 'cargo-driver'; Project = 'DriverApp/DriverApp.csproj'; PackageId = 'kr.hongdal.driver' },
        [pscustomobject]@{ Name = 'warehouse'; Project = 'WarehouseManagerApp/WarehouseManagerApp.csproj'; PackageId = 'com.companyname.warehousemanagerapp' }
    )
}

# SDK input stays outside Git. Evidence records only package and hashes, never JSON/key values.
function Resolve-MobileMemberFirebaseConfiguration {
    param($App, [string]$RepositoryRoot, [string]$GoogleServicesJson, [string]$ExpectedProjectId)
    if ($App.Name -ne 'shipper') {
        return [pscustomobject]@{ Configured=$false; PackageId=$App.PackageId; FilePath=$null; Sha256=$null; ProjectIdSha256=$null; ProjectComparison='NotApplicable' }
    }
    $configurationPath = if ([string]::IsNullOrWhiteSpace($GoogleServicesJson)) {
        [Environment]::GetEnvironmentVariable('SSALDDEL_MEMBER_FIREBASE_GOOGLE_SERVICES_JSON', 'Process')
    } else { $GoogleServicesJson }
    if ([string]::IsNullOrWhiteSpace($configurationPath)) {
        if (-not [string]::IsNullOrWhiteSpace($ExpectedProjectId)) { throw 'Firebase project comparison requires an external member configuration file.' }
        return [pscustomobject]@{ Configured=$false; PackageId=$App.PackageId; FilePath=$null; Sha256=$null; ProjectIdSha256=$null; ProjectComparison='NotConfigured' }
    }
    $configurationPath = [Environment]::ExpandEnvironmentVariables($configurationPath)
    if (-not [IO.Path]::IsPathRooted($configurationPath)) { throw 'Member Firebase configuration must use an absolute external file path.' }
    $resolvedFile = [IO.Path]::GetFullPath($configurationPath)
    if ($resolvedFile -match '[;,%%"\r\n]' -or $resolvedFile.Contains('$(')) { throw 'Member Firebase configuration path contains unsupported MSBuild separators.' }
    $resolvedRepo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    if ($resolvedFile -eq $resolvedRepo -or $resolvedFile.StartsWith($resolvedRepo+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Member Firebase configuration must remain outside the repository.'
    }
    if (-not (Test-Path -LiteralPath $resolvedFile -PathType Leaf) -or [IO.Path]::GetExtension($resolvedFile) -ne '.json') {
        throw 'Member Firebase configuration JSON file was not found.'
    }
    $ancestor = Get-Item -LiteralPath $resolvedFile -Force
    while ($null -ne $ancestor) {
        if (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Member Firebase configuration must not traverse a symbolic link or junction.' }
        $ancestor = if ($ancestor -is [IO.FileInfo]) { $ancestor.Directory } else { $ancestor.Parent }
    }
    try {
        $bytes = [IO.File]::ReadAllBytes($resolvedFile)
        $document = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json -ErrorAction Stop
        $projectId = [string]$document.project_info.project_id
        $packages = @($document.client | ForEach-Object { [string]$_.client_info.android_client_info.package_name })
    } catch { throw 'Member Firebase configuration JSON or Android metadata is invalid.' }
    if ([string]::IsNullOrWhiteSpace($projectId) -or $projectId -ne $projectId.Trim() -or $projectId -match '[\x00-\x20]') {
        throw 'Member Firebase project metadata is missing or invalid.'
    }
    if (@($packages | Where-Object { $_ -ceq $App.PackageId }).Count -ne 1) {
        throw 'Firebase Android package does not match the selected member app.'
    }
    $projectComparison = 'NotRequested'
    if (-not [string]::IsNullOrWhiteSpace($ExpectedProjectId)) {
        if ($projectId -cne $ExpectedProjectId) { throw 'Member Firebase project does not match the explicitly expected project.' }
        $projectComparison = 'MatchedExplicitProject'
    }
    [pscustomobject]@{
        Configured=$true; PackageId=$App.PackageId; FilePath=$resolvedFile
        Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        ProjectIdSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($projectId))).ToLowerInvariant()
        ProjectComparison=$projectComparison
    }
}

function Confirm-MobileMemberFirebaseConfigurationUnchanged {
    param($App, [string]$RepositoryRoot, $Before, [string]$ExpectedProjectId)
    if (-not $Before.Configured) { return }
    $current = Resolve-MobileMemberFirebaseConfiguration -App $App -RepositoryRoot $RepositoryRoot -GoogleServicesJson $Before.FilePath -ExpectedProjectId $ExpectedProjectId
    if (-not $current.Configured -or $current.PackageId -cne $Before.PackageId -or $current.Sha256 -cne $Before.Sha256 -or
        $current.ProjectIdSha256 -cne $Before.ProjectIdSha256) {
        throw 'Member Firebase configuration changed during packaging; no completed handoff may be created.'
    }
}

function Get-MobileMemberFirebaseEvidence {
    param($Configuration, [switch]$VerifiedUnchanged)
    [pscustomobject]@{
        scope='OrdinaryMemberFirebaseSdk'; applicable=$Configuration.ProjectComparison -ne 'NotApplicable'
        configured=$(if ($Configuration.ProjectComparison -eq 'NotApplicable') { $null } else { [bool]$Configuration.Configured }); packageId=$Configuration.PackageId
        googleServicesSha256Before=$Configuration.Sha256
        googleServicesSha256After=$(if ($VerifiedUnchanged) { $Configuration.Sha256 } else { $null })
        projectIdSha256=$Configuration.ProjectIdSha256; projectComparison=$Configuration.ProjectComparison
        configurationUnchanged=$(if ($Configuration.ProjectComparison -eq 'NotApplicable') { $null } else { [bool]$VerifiedUnchanged }); actualPushReceived=$false
    }
}

function Get-MobileFieldTestPublishArguments {
    param($Project, $Destination, $Version, $VersionCode, $Server, $Keystore, $Alias, $FirebaseConfiguration)
    $mobileApp = Get-MobileFieldTestApps | Where-Object { [IO.Path]::GetFileName($_.Project) -eq [IO.Path]::GetFileName([string]$Project) } | Select-Object -First 1
    $firebaseArguments = @()
    if ($null -ne $mobileApp -and $mobileApp.Name -eq 'shipper') {
        if ($null -eq $FirebaseConfiguration) {
            $FirebaseConfiguration = Resolve-MobileMemberFirebaseConfiguration -App $mobileApp -RepositoryRoot (Join-Path $PSScriptRoot '../..')
        }
        if ($FirebaseConfiguration.PackageId -cne $mobileApp.PackageId) { throw 'Member Firebase configuration belongs to a different app.' }
        # Freeze absence too: a changed process environment must not silently add SDK input mid-build.
        $firebaseArguments += '-p:MemberFirebaseGoogleServicesJson='+$(if ($FirebaseConfiguration.Configured) { $FirebaseConfiguration.FilePath } else { '' })
    }
    $portableReferences = Join-Path $PSScriptRoot 'MobileFieldTest.PortableReferences.targets'
    @(
        'publish', $Project, '-f', 'net10.0-android', '-c', 'Release', '-o', $Destination,
        '-p:UseSharedCompilation=false', "-p:ApplicationDisplayVersion=$Version", "-p:ApplicationVersion=$VersionCode",
        "-p:SsalddelServerBaseAddress=$Server", '-p:AndroidPackageFormats=apk',
        # Keep literal inner quotes for MSBuild's CLI list parser. Percent-escaping
        # this separator yields one scalar RID during NuGet restore.
        '-p:RuntimeIdentifiers="android-arm64;android-x64"', '-p:AndroidCreatePackagePerAbi=false',
        # Keep ABI selection on the Android app while portable project references
        # explicitly opt out of ABI-specific compiler-skipping output paths.
        "-p:CustomAfterMicrosoftCommonTargets=$portableReferences", '-p:SsalddelMobileFieldTestPortableReferences=true',
        '-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$Keystore", "-p:AndroidSigningKeyAlias=$Alias",
        '-p:AndroidSigningStorePass=env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD',
        '-p:AndroidSigningKeyPass=env:SSALDDEL_ANDROID_KEY_PASSWORD'
    ) + $firebaseArguments
}

function Resolve-MobileAndroidBuildTools {
    param([string]$AndroidSdkDirectory)
    if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) {
        $AndroidSdkDirectory = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
    }
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory) -and $programFilesX86) {
        $AndroidSdkDirectory = Join-Path $programFilesX86 'Android/android-sdk'
    }
    if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) { throw 'Android SDK path is required for final APK verification.' }
    $directory = Get-ChildItem -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools') -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($null -eq $directory) { throw 'Android build-tools are missing.' }
    $windows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
    $tools = @{
        apksigner = Join-Path $directory.FullName $(if ($windows) { 'apksigner.bat' } else { 'apksigner' })
        zipalign = Join-Path $directory.FullName $(if ($windows) { 'zipalign.exe' } else { 'zipalign' })
        aapt = Join-Path $directory.FullName $(if ($windows) { 'aapt2.exe' } else { 'aapt2' })
    }
    foreach ($tool in $tools.Values) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw 'An Android APK verification tool is missing.' }
    }
    $tools
}

function Invoke-MobileApkTool {
    param([string]$Tool, [string[]]$ToolArguments)
    $output = @(& $Tool @ToolArguments 2>&1)
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join [Environment]::NewLine) }
}

function Get-MobileSigningCertificateFingerprint {
    param([string]$KeystorePath, [string]$Alias)
    $keytool = if ($env:JAVA_HOME -and (Test-Path -LiteralPath (Join-Path $env:JAVA_HOME 'bin/keytool.exe'))) {
        Join-Path $env:JAVA_HOME 'bin/keytool.exe'
    } elseif ($env:JAVA_HOME -and (Test-Path -LiteralPath (Join-Path $env:JAVA_HOME 'bin/keytool'))) {
        Join-Path $env:JAVA_HOME 'bin/keytool'
    } else { (Get-Command keytool -ErrorAction Stop).Source }
    $result = Invoke-MobileApkTool $keytool @('-J-Duser.language=en', '-J-Duser.country=US', '-list', '-v',
        '-keystore', $KeystorePath, '-alias', $Alias, '-storepass:env', 'SSALDDEL_ANDROID_KEYSTORE_PASSWORD')
    $fingerprint = [regex]::Match($result.Output, 'SHA256:\s*((?:[a-fA-F0-9]{2}:){31}[a-fA-F0-9]{2})')
    if ($result.ExitCode -ne 0 -or -not $fingerprint.Success) { throw 'The requested keystore alias certificate could not be verified.' }
    $fingerprint.Groups[1].Value.Replace(':', '').ToLowerInvariant()
}

function Confirm-MobileSignedApk {
    param([string]$Path, $ExpectedApp, [string]$Version, [int]$VersionCode, $Tools,
        [Parameter(Mandatory = $true)][string]$ExpectedCertificateSha256,
        [scriptblock]$ToolInvoker = { param($Tool, $ToolArguments) Invoke-MobileApkTool $Tool $ToolArguments },
        [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release')
    if ([IO.Path]::GetExtension($Path) -ne '.apk' -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'Only an existing APK can be handed over.'
    }
    $signature = & $ToolInvoker $Tools.apksigner @('verify', '--verbose', '--print-certs', $Path)
    if ($signature.ExitCode -ne 0) { throw 'Final APK signature verification failed; no handoff manifest was published.' }
    $certificate = [regex]::Match($signature.Output, 'certificate SHA-256 digest:\s*([a-fA-F0-9]{64})')
    if (-not $certificate.Success) { throw 'Final APK signer certificate fingerprint is missing.' }
    if ($ExpectedCertificateSha256 -notmatch '^[a-fA-F0-9]{64}$' -or
        $certificate.Groups[1].Value -ne $ExpectedCertificateSha256) {
        throw 'Final APK signer does not match the requested keystore alias.'
    }
    $alignment = & $ToolInvoker $Tools.zipalign @('-c', '-P', '16', '-v', '4', $Path)
    if ($alignment.ExitCode -ne 0) { throw 'Final APK alignment verification failed.' }
    $badging = & $ToolInvoker $Tools.aapt @('dump', 'badging', $Path)
    $package = [regex]::Match($badging.Output, "package:\s+name='([^']+)'\s+versionCode='([^']+)'\s+versionName='([^']+)'")
    $debuggable = $badging.Output -match '(?m)^application-debuggable'
    if ($badging.ExitCode -ne 0 -or -not $package.Success -or
        $package.Groups[1].Value -ne $ExpectedApp.PackageId -or
        $package.Groups[2].Value -ne [string]$VersionCode -or $package.Groups[3].Value -ne $Version -or
        ($Configuration -eq 'Release' -and $debuggable) -or ($Configuration -eq 'Debug' -and -not $debuggable)) {
        throw "Final APK package/version/$Configuration metadata did not match the requested app."
    }
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries.FullName)
        foreach ($abi in @('arm64-v8a', 'x86_64')) {
            if (-not @($entries | Where-Object { $_ -in @("lib/$abi/libmonosgen-2.0.so", "lib/$abi/libdotnet.so") }).Count) {
                throw "Final APK embedded native runtime for $abi is missing."
            }
        }
    } finally { $archive.Dispose() }
    [pscustomobject]@{
        signatureVerified = $true; alignmentVerified = $true; releaseMetadataVerified = $Configuration -eq 'Release'
        debugMetadataVerified = $Configuration -eq 'Debug'; configuration = $Configuration
        certificateSha256 = $certificate.Groups[1].Value.ToLowerInvariant()
        abis = @('arm64-v8a', 'x86_64'); physicalDeviceVerified = $false; serverCommunicationVerified = $false
    }
}
