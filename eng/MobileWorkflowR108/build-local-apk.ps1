param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][int]$VersionCode,
    [string]$PrivateBuildInput = 'artifacts/local/local-operations-r108/mobile/.private/apk-build-input.json',
    [string]$OutputRoot = 'artifacts/local/local-operations-r108/mobile/apk',
    [string]$NaverMapSdkKeyIdFile,
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$inputPath=[IO.Path]::GetFullPath((Join-Path $repoRoot $PrivateBuildInput))
if(!$inputPath.Contains([IO.Path]::DirectorySeparatorChar+'.private'+[IO.Path]::DirectorySeparatorChar)) {throw 'Private build input required.'}
$configuration=Get-Content -LiteralPath $inputPath -Raw|ConvertFrom-Json
if($configuration.serverBaseAddress -ne 'http://127.0.0.1:5363/' -or $configuration.app -ne 'shipper' -or $configuration.install -or $configuration.upload){
    throw 'Existing local-only APK contract required.'
}
$actualHash=(Get-FileHash -LiteralPath $configuration.firebaseFile -Algorithm SHA256).Hash.ToLowerInvariant()
if($actualHash -ne $configuration.firebaseSha256){throw 'Preserved Firebase SDK configuration changed.'}
$javaHome='C:/Program Files/Android/openjdk/jdk-21.0.8'
if(!(Test-Path -LiteralPath (Join-Path $javaHome 'bin/keytool.exe'))){throw 'Reviewed existing Android JDK is unavailable.'}
$names=@('JAVA_HOME','JavaSdkDirectory','ANDROID_HOME','SSALDDEL_NAVER_MAP_SDK_NCP_KEY_ID')
$saved=@{}
foreach($name in $names){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$naverInput=$null
$naverInputHash=$null
try {
    if(-not [string]::IsNullOrWhiteSpace($NaverMapSdkKeyIdFile)) {
        $naverInput=[IO.Path]::GetFullPath($NaverMapSdkKeyIdFile)
        $insideRepo=$naverInput.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
        if($insideRepo -or !(Test-Path -LiteralPath $naverInput -PathType Leaf)){throw 'Existing external Naver mobile SDK key ID file required.'}
        $naverSdkId=([IO.File]::ReadAllText($naverInput)).Trim()
        if($naverSdkId -notmatch '^[a-zA-Z0-9_-]{5,120}$'){throw 'Naver mobile SDK key ID has an invalid format.'}
        $naverInputHash=(Get-FileHash -LiteralPath $naverInput -Algorithm SHA256).Hash
        $env:SSALDDEL_NAVER_MAP_SDK_NCP_KEY_ID=$naverSdkId
    }
    $env:JAVA_HOME=$javaHome
    $env:JavaSdkDirectory=$javaHome
    $env:ANDROID_HOME=$configuration.androidSdkDirectory
    $parameters=@{
        ServerBaseAddress=$configuration.serverBaseAddress
        Version=$Version
        VersionCode=$VersionCode
        KeystorePath=$configuration.keystorePath
        OutputRoot=$OutputRoot
        AppNames=@('shipper')
        AndroidSdkDirectory=$configuration.androidSdkDirectory
        MemberFirebaseGoogleServicesJson=$configuration.firebaseFile
        ExpectedMemberFirebaseProjectId=$configuration.expectedProjectId
        PlanOnly=[bool]$PlanOnly
    }
    Push-Location $repoRoot
    try {
        & (Join-Path $repoRoot 'eng/release/publish-mobile-usb-test.ps1') @parameters
        if($naverInput -and (Get-FileHash -LiteralPath $naverInput -Algorithm SHA256).Hash -cne $naverInputHash){
            throw 'Naver SDK key input changed during packaging; do not install this APK.'
        }
    }
    finally { Pop-Location }
} finally {
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}
}
