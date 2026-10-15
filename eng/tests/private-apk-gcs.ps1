#requires -Version 7.0
# Offline fixtures exercise rejection and preparation; no fixture is an installable APK.
[CmdletBinding()] param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'eng/release/PrivateApkGcs.Common.ps1')
$runRef = 'artifacts/local/validation/private-apk-gcs-r70/' + [Guid]::NewGuid().ToString('N')
$run = Join-Path $root $runRef
[IO.Directory]::CreateDirectory($run) | Out-Null
$apk = Join-Path $run 'cargo-driver.apk'
$manifest = Join-Path $run 'release-manifest.json'
$certificate = 'a' * 64
$tools = @{ apksigner = 'fake-apksigner'; zipalign = 'fake-zipalign'; aapt = 'fake-aapt' }
$script:toolCalls = 0
$script:toolMode = 'Valid'
$results = [Collections.Generic.List[object]]::new()
function Assert([bool]$Value, [string]$Name) {
    if (-not $Value) { throw "TestFailed:$Name" }
    $results.Add([ordered]@{ name = $Name; passed = $true })
}
function Reject([string]$Name, [scriptblock]$Action, [string]$Reason) {
    $caught = $null
    try { $null = & $Action } catch { $caught = $_.Exception.Message }
    Assert ($null -ne $caught -and $caught.Contains($Reason)) "$Name [$caught]"
}
function Write-Apk([string[]]$Abis = @('arm64-v8a', 'x86_64')) {
    if (Test-Path -LiteralPath $apk) { Remove-Item -LiteralPath $apk }
    $zip = [IO.Compression.ZipFile]::Open($apk, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($abi in $Abis) { $null = $zip.CreateEntry("lib/$abi/libdotnet.so") }
        $entry = $zip.CreateEntry('fixture.txt')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write('Offline fixture, not signed or installable.') } finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
}
Write-Apk
$base = [ordered]@{
    app = 'cargo-driver'; packageId = 'kr.hongdal.driver'; version = '0.1.0'; versionCode = 70
    server = 'https://mirror-pilot.azurewebsites.net/'; file = 'cargo-driver.apk'
    sha256 = (Get-FileHash -LiteralPath $apk).Hash.ToLowerInvariant(); bytes = (Get-Item -LiteralPath $apk).Length
    verification = @{ signatureVerified = $true; alignmentVerified = $true; releaseMetadataVerified = $true
        configuration = 'Release'; certificateSha256 = $certificate }
    downloadPath = 'downloads/mobile/0.1.0-70/cargo-driver.apk'
}
function Clone { ConvertFrom-Json ($base | ConvertTo-Json -Depth 8) -AsHashtable }
function Write-Manifest($Items) {
    [IO.File]::WriteAllText($manifest, (ConvertTo-Json -InputObject @($Items) -Depth 8), [Text.UTF8Encoding]::new($false))
}
$fakeCli = {
    param($Tool, $ToolArguments)
    $script:toolCalls++
    $exitCode = 0
    $output = ''
    switch ($Tool) {
        'fake-apksigner' {
            if ($script:toolMode -eq 'SignatureFailure') { $exitCode = 1 }
            $digest = if ($script:toolMode -eq 'CertificateMismatch') { 'b' * 64 } else { 'a' * 64 }
            $output = "Signer #1 certificate SHA-256 digest: $digest"
        }
        'fake-zipalign' { if ($script:toolMode -eq 'AlignmentFailure') { $exitCode = 1 }; $output = 'Verification successful' }
        'fake-aapt' {
            $package = if ($script:toolMode -eq 'PackageMismatch') { 'wrong.package' } else { 'kr.hongdal.driver' }
            $code = if ($script:toolMode -eq 'VersionMismatch') { '69' } else { '70' }
            $output = "package: name='$package' versionCode='$code' versionName='0.1.0'"
            if ($script:toolMode -eq 'DebugApk') { $output += "`napplication-debuggable" }
            if ($script:toolMode -eq 'SourceDrift') { [IO.File]::AppendAllText($ToolArguments[-1], 'drift') }
        }
        default { throw 'UnexpectedExternalTool' }
    }
    [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}
function Prepare([string]$Bucket = 'private-pilot-bucket', [string]$Prefix = 'private-apk', [string]$Trust = $certificate) {
    New-PrivateApkGcsPreparation -ManifestPath $manifest -BucketName $Bucket -ObjectPrefix $Prefix `
        -ExpectedCertificateSha256 $Trust -AndroidTools $tools -ApkToolInvoker $fakeCli
}
function Bad([string]$Name, [scriptblock]$Change, [string]$Reason) {
    $item = Clone
    & $Change $item
    Write-Manifest $item
    Reject $Name { Prepare } $Reason
}
function gcloud { throw 'CloudMustNeverBeCalled' }
function gsutil { throw 'CloudMustNeverBeCalled' }
Write-Manifest (Clone)
$valid = Prepare
Assert ($script:toolCalls -eq 3) '실제 파일에 기존 서명·정렬·메타데이터 검증기를 재호출'
Assert ($valid.status -ceq 'LocalPrepared_CloudNotAttempted' -and -not $valid.readyForSharing) '준비와 공유 가능 상태 분리'
Assert (-not $valid.cloudCallAttempted -and -not $valid.apkUploadCompleted -and -not $valid.downloadUrlIssued) '클라우드·업로드·URL 미실행'
Assert (-not $valid.bucketPrivacyVerified -and -not $valid.remoteByteHashVerified -and -not $valid.physicalDeviceInstallVerified) '비공개·원격 바이트·단말 증거 미승격'
Assert (-not $valid.embeddedEndpointVerified -and $valid.serverConfigurationBasis -ceq 'ManifestDeclaration_NotEmbeddedApkConfigurationProof') 'manifest 서버 선언과 APK 내장 endpoint 입증 구분'
Assert ($valid.artifacts[0].objectName -ceq "private-apk/0.1.0-70/cargo-driver/$($base.sha256)/cargo-driver.apk") '버전·앱·SHA 경로 동결'
Assert ($valid.artifacts[0].uploadIfGenerationMatch -eq 0 -and -not $valid.constraints.overwriteObjectAllowed) '덮어쓰기 금지 조건 보존'
Assert (-not $valid.constraints.changeAclOrIamAllowed -and -not $valid.constraints.publicAccessAllowed -and -not $valid.constraints.createSigningKeyAllowed) '권한·공개화·새 키 금지'
Assert (-not (Test-Path (Join-Path $run 'preparation'))) 'Validate는 파일 생성하지 않음'
$outputRoot = "$runRef/preparation"
$first = Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot $outputRoot
$firstPath = Join-Path $root $first.planPath
$firstTime = (Get-Item -LiteralPath $firstPath).LastWriteTimeUtc
$again = Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot $outputRoot
Assert ($first.writeState -ceq 'Created' -and $again.writeState -ceq 'ExistingIdentical' -and
    $again.planSha256 -ceq $first.planSha256 -and (Get-Item -LiteralPath $firstPath).LastWriteTimeUtc -eq $firstTime) '동일 준비 멱등 재사용'
[IO.File]::AppendAllText($firstPath, 'tampered')
Reject '기존 준비 변조 덮어쓰기 거부' { Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot $outputRoot } 'ImmutablePreparationConflict'
Assert ([IO.File]::ReadAllText($firstPath).EndsWith('tampered')) '충돌 파일 보존'
Reject 'Git 문서 출력 거부' { Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot 'docs/downloads' } 'OutputMustBePrivateLocalArtifacts'
Reject '출력 경로 탈출 거부' { Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot 'artifacts/local/../public' } 'OutputMustBePrivateLocalArtifacts'
foreach ($pair in @(
    @('schema', 'different-schema'), @('status', 'Uploaded'),
    @('version', '../../../../docs/writer-escape'), @('version', '..\..\..\docs\writer-escape'),
    @('version', "0.1.0`n"), @('versionCode', '../../../../docs/writer-escape'),
    @('versionCode', 2147483647), @('versionCode', 0)
)) {
    $untrusted = ConvertFrom-Json ($valid | ConvertTo-Json -Depth 12) -AsHashtable
    $untrusted[$pair[0]] = $pair[1]
    Reject ("writer 독립 입력 검증:" + $pair[0] + ':' + $pair[1]) {
        Write-PrivateApkGcsPreparation -Preparation $untrusted -RepositoryRoot $root -OutputRoot $outputRoot
    } 'PreparationMetadataInvalid'
}
Assert ([IO.Path]::GetRelativePath((Join-Path $root 'artifacts/local'), [IO.Path]::GetFullPath($firstPath)) -notmatch '^\.\.') '최종 절대 출력 경로 artifacts/local 내부'
Assert (-not (Test-Path (Join-Path $root 'docs/writer-escape'))) '악성 writer 입력이 문서 경로를 생성하지 않음'
Reject 'URL 대신 bucket 이름만 허용' { Prepare 'gs://private-pilot-bucket' } 'BucketNameInvalid'
Reject 'IP형 bucket 거부' { Prepare '192.168.1.2' } 'BucketNameInvalid'
Reject 'bucket 이름 주입 거부' { Prepare 'bucket;gcloud' } 'BucketNameInvalid'
Reject 'prefix 탈출 거부' { Prepare 'private-pilot-bucket' '../public' } 'ObjectPrefixInvalid'
Reject 'prefix URI 거부' { Prepare 'private-pilot-bucket' 'gs://elsewhere' } 'ObjectPrefixInvalid'
Reject '독립 신뢰 서명값 필수' { Prepare 'private-pilot-bucket' 'private-apk' 'not-a-hash' } 'TrustedSigningFingerprintRequired'
foreach ($pair in @(
    @('루프백 HTTPS', 'https://localhost/'), @('로컬 IP', 'https://192.168.1.1/'),
    @('에뮬레이터', 'https://10.0.2.2/'), @('내부 호스트', 'https://mirror.internal/'),
    @('invalid fixture', 'https://mirror.invalid/'), @('test fixture', 'https://mirror.test/'),
    @('example fixture', 'https://example.com/'), @('짧은 호스트', 'https://mirror/'),
    @('HTTP 서버', 'http://mirror-pilot.azurewebsites.net/'),
    @('자격증명 URL', 'https://user:secret@mirror-pilot.azurewebsites.net/'),
    @('query URL', 'https://mirror-pilot.azurewebsites.net/?token=secret'),
    @('fragment URL', 'https://mirror-pilot.azurewebsites.net/#fragment')
)) {
    $item = Clone
    $item.server = $pair[1]
    Write-Manifest $item
    Reject ("공유 준비 서버 거부:" + $pair[0]) { Prepare } 'PrivateApk:'
}
Bad '경로 상위 탈출' { param($p) $p.file = '../cargo-driver.apk' } 'UnsafeArtifactPath'
Bad '절대 경로 거부' { param($p) $p.file = 'C:/outside.apk' } 'UnsafeArtifactPath'
Bad 'URL 파일 거부' { param($p) $p.file = 'https://host/file.apk' } 'UnsafeArtifactPath'
Bad '백슬래시 우회 거부' { param($p) $p.file = '..\cargo-driver.apk' } 'UnsafeArtifactPath'
Bad '다른 확장자 거부' { param($p) $p.file = 'release-manifest.json' } 'ApkFileRequired'
Bad '없는 APK 거부' { param($p) $p.file = 'missing.apk' } 'ArtifactMissing'
Bad '출처 hash 불일치' { param($p) $p.sha256 = 'b' * 64 } 'SourceIntegrityMismatch'
Bad '출처 크기 불일치' { param($p) $p.bytes++ } 'SourceIntegrityMismatch'
Bad '0 byte 미확인 거부' { param($p) $p.bytes = 0 } 'SourceIntegrityMissing'
Bad 'manifest 서명 검증 없음' { param($p) $p.verification.signatureVerified = $false } 'ReleaseVerificationMissing'
Bad 'manifest 서명값을 신뢰값으로 대체 불가' { param($p) $p.verification.certificateSha256 = 'b' * 64 } 'ReleaseVerificationMissing'
Bad 'Debug 산출물 거부' { param($p) $p.verification.configuration = 'Debug' } 'ReleaseVerificationMissing'
Bad '패키지 정체성 변경 거부' { param($p) $p.packageId = 'new.package' } 'ReleaseMetadataInvalid'
Bad '버전 형식 거부' { param($p) $p.version = 'latest' } 'ReleaseMetadataInvalid'
Bad '버전 코드 상한 거부' { param($p) $p.versionCode = 2147483647 } 'ReleaseMetadataInvalid'
Bad '버전 코드 실수 거부' { param($p) $p.versionCode = 70.5 } 'ReleaseMetadataInvalid'
Bad 'downloadPath 불일치' { param($p) $p.downloadPath = 'downloads/latest.apk' } 'DownloadPathMismatch'
Bad '알수없는 앱 거부' { param($p) $p.app = 'new-app' } 'UnknownOrDuplicateApp'
$junctionTarget = Join-Path $run 'junction-target'
[IO.Directory]::CreateDirectory($junctionTarget) | Out-Null
Copy-Item -LiteralPath $apk -Destination (Join-Path $junctionTarget 'cargo-driver.apk')
$junction = Join-Path $run 'junction-source'
$linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
New-Item -ItemType $linkType -Path $junction -Target $junctionTarget | Out-Null
Bad '입력 junction 경로 우회 거부' { param($p) $p.file = 'junction-source/cargo-driver.apk' } 'LinkedPathNotAllowed'
Reject '출력 junction 경로 우회 거부' {
    Write-PrivateApkGcsPreparation -Preparation $valid -RepositoryRoot $root -OutputRoot "$runRef/junction-source/preparation"
} 'LinkedPathNotAllowed'
Write-Manifest @((Clone), (Clone))
Reject '중복 앱 거부' { Prepare } 'UnknownOrDuplicateApp'
Write-Manifest @()
Reject '빈 manifest 거부' { Prepare } 'ArtifactCountInvalid'
Write-Manifest (Clone)
foreach ($pair in @(
    @('SignatureFailure', 'signature verification failed'), @('CertificateMismatch', 'signer does not match'),
    @('AlignmentFailure', 'alignment verification failed'), @('PackageMismatch', 'metadata did not match'),
    @('VersionMismatch', 'metadata did not match'), @('DebugApk', 'metadata did not match'),
    @('SourceDrift', 'SourceChangedDuringVerification')
)) {
    $script:toolMode = $pair[0]
    Reject ("APK 재검증 거부:" + $pair[0]) { Prepare } $pair[1]
}
$script:toolMode = 'Valid'
Write-Apk @('arm64-v8a')
$item = Clone
$item.sha256 = (Get-FileHash -LiteralPath $apk).Hash.ToLowerInvariant()
$item.bytes = (Get-Item -LiteralPath $apk).Length
Write-Manifest $item
Reject 'x86_64 runtime 없는 ZIP 거부' { Prepare } 'runtime for x86_64 is missing'
[IO.File]::WriteAllText($manifest, ' ' * 262145)
Reject '과대 manifest 거부' { Prepare } 'ManifestFileInvalid'
$report = [ordered]@{ status = 'Passed'; cases = $results.Count; artifactRef = $runRef
    cloud = 'NotUsed'; apk = 'SyntheticZipAndFakeCli_NotInstallable'; device = 'NotUsed'; results = $results }
[IO.File]::WriteAllText((Join-Path $run 'results.json'), ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[ordered]@{ status = $report.status; cases = $report.cases; artifactRef = $runRef
    cloud = $report.cloud; apk = $report.apk; device = $report.device } | ConvertTo-Json
