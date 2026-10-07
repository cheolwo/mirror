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

function Get-MobileFieldTestPublishArguments {
    param($Project, $Destination, $Version, $VersionCode, $Server, $Keystore, $Alias)
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
    )
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
        [scriptblock]$ToolInvoker = { param($Tool, $ToolArguments) Invoke-MobileApkTool $Tool $ToolArguments })
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
    if ($badging.ExitCode -ne 0 -or -not $package.Success -or
        $package.Groups[1].Value -ne $ExpectedApp.PackageId -or
        $package.Groups[2].Value -ne [string]$VersionCode -or $package.Groups[3].Value -ne $Version -or
        $badging.Output -match '(?m)^application-debuggable') {
        throw 'Final APK package/version/Release metadata did not match the requested app.'
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
        signatureVerified = $true; alignmentVerified = $true; releaseMetadataVerified = $true
        certificateSha256 = $certificate.Groups[1].Value.ToLowerInvariant()
        abis = @('arm64-v8a', 'x86_64'); physicalDeviceVerified = $false; serverCommunicationVerified = $false
    }
}
