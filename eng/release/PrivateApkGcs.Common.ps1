# Local preparation only. This module never calls gcloud, changes IAM, uploads,
# issues URLs, creates keys, or marks an APK ready for external use.
. (Join-Path $PSScriptRoot 'MobileFieldTest.Common.ps1')

function Assert-PrivateApkNonLinkedPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    while ($null -ne $item) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'PrivateApk:LinkedPathNotAllowed'
        }
        $item = if ($item -is [IO.FileInfo]) { $item.Directory } else { $item.Parent }
    }
}

function Resolve-PrivateApkManifestFile {
    param([Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath)
    if ($RelativePath -notmatch '^[a-zA-Z0-9._/-]+$' -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        @($RelativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        throw 'PrivateApk:UnsafeArtifactPath'
    }
    $path = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $relative = [IO.Path]::GetRelativePath($Root, $path)
    if ([IO.Path]::IsPathRooted($relative) -or $relative -eq '..' -or
        $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
        throw 'PrivateApk:UnsafeArtifactPath'
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'PrivateApk:ArtifactMissing' }
    Assert-PrivateApkNonLinkedPath $path
    $path
}

function Assert-PrivateApkExternalServer {
    param([Parameter(Mandatory = $true)][string]$Server)
    $uri = $null
    if (-not [Uri]::TryCreate($Server, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -cne 'https' -or $uri.IsLoopback -or
        $uri.UserInfo -or $uri.Query -or $uri.Fragment) { throw 'PrivateApk:ExternalHttpsServerRequired' }
    $hostName = $uri.IdnHost.TrimEnd('.').ToLowerInvariant()
    if ($hostName -notmatch '\.' -or $hostName -match '(^|\.)(localhost|local|lan|internal|invalid|test|example)$' -or
        $hostName -match '(^|\.)example\.(com|net|org)$') { throw 'PrivateApk:FixtureServerNotAllowed' }
    $address = $null
    if ([Net.IPAddress]::TryParse($hostName.Trim('[', ']'), [ref]$address)) {
        # External service hostnames are required. This also rejects private,
        # link-local, documentation, mapped IPv6 and unusual numeric IP forms.
        throw 'PrivateApk:ServerHostnameRequired'
    }
    $uri.AbsoluteUri
}

function New-PrivateApkGcsPreparation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$BucketName,
        [Parameter(Mandatory = $true)][string]$ExpectedCertificateSha256,
        [string]$ObjectPrefix = 'private-apk',
        [Parameter(Mandatory = $true)]$AndroidTools,
        [scriptblock]$ApkToolInvoker = { param($Tool, $ToolArguments) Invoke-MobileApkTool $Tool $ToolArguments }
    )
    if ($ExpectedCertificateSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'PrivateApk:TrustedSigningFingerprintRequired' }
    $trustedCertificate = $ExpectedCertificateSha256.ToLowerInvariant()
    if ($BucketName -notmatch '^[a-z0-9][a-z0-9._-]{1,61}[a-z0-9]$' -or
        $BucketName -match '\.\.|\.-|-\.|^goog|google' -or
        $BucketName -match '^\d+\.\d+\.\d+\.\d+$') { throw 'PrivateApk:BucketNameInvalid' }
    if ($ObjectPrefix -notmatch '^[a-z0-9][a-z0-9-]*(/[a-z0-9][a-z0-9-]*)*$' -or $ObjectPrefix.Length -gt 160) {
        throw 'PrivateApk:ObjectPrefixInvalid'
    }
    $manifest = (Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path
    Assert-PrivateApkNonLinkedPath $manifest
    $manifestItem = Get-Item -LiteralPath $manifest
    if ($manifestItem.PSIsContainer -or $manifestItem.Length -gt 262144) { throw 'PrivateApk:ManifestFileInvalid' }
    $manifestHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
    $artifacts = @(Get-Content -LiteralPath $manifest -Raw -Encoding utf8 | ConvertFrom-Json -ErrorAction Stop)
    if ($artifacts.Count -lt 1 -or $artifacts.Count -gt 7) { throw 'PrivateApk:ArtifactCountInvalid' }
    $knownApps = @(Get-MobileFieldTestApps)
    $seenApps = @{}
    $root = Split-Path -Parent $manifest
    $result = @()
    $releaseVersion = $null
    $releaseCode = $null
    $releaseServer = $null
    foreach ($artifact in $artifacts) {
        $app = @($knownApps | Where-Object { $_.Name -ceq $artifact.app })
        if ($app.Count -ne 1 -or $seenApps.ContainsKey([string]$artifact.app)) { throw 'PrivateApk:UnknownOrDuplicateApp' }
        $seenApps[[string]$artifact.app] = $true
        if ($artifact.packageId -cne $app[0].PackageId -or
            $artifact.version -notmatch '^\d+\.\d+\.\d+$' -or
            [string]$artifact.versionCode -notmatch '^[1-9]\d{0,9}$' -or
            [long]$artifact.versionCode -gt 2100000000) { throw 'PrivateApk:ReleaseMetadataInvalid' }
        $server = Assert-PrivateApkExternalServer ([string]$artifact.server)
        if ($null -eq $releaseVersion) {
            $releaseVersion = [string]$artifact.version
            $releaseCode = [int]$artifact.versionCode
            $releaseServer = $server
        } elseif ($releaseVersion -cne $artifact.version -or $releaseCode -ne $artifact.versionCode -or
            $releaseServer -cne $server) { throw 'PrivateApk:MixedReleaseManifest' }
        $expectedDownloadPath = "downloads/mobile/$releaseVersion-$releaseCode/$($artifact.app).apk"
        if ($artifact.downloadPath -cne $expectedDownloadPath) { throw 'PrivateApk:DownloadPathMismatch' }
        if ($artifact.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or [string]$artifact.bytes -notmatch '^[1-9]\d*$') {
            throw 'PrivateApk:SourceIntegrityMissing'
        }
        $path = Resolve-PrivateApkManifestFile -Root $root -RelativePath ([string]$artifact.file)
        if ([IO.Path]::GetExtension($path) -cne '.apk') { throw 'PrivateApk:ApkFileRequired' }
        $sourceItem = Get-Item -LiteralPath $path
        $sourceHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sourceItem.Length -ne [long]$artifact.bytes -or $sourceHash -cne $artifact.sha256.ToLowerInvariant()) {
            throw 'PrivateApk:SourceIntegrityMismatch'
        }
        if ($artifact.verification.signatureVerified -ne $true -or $artifact.verification.alignmentVerified -ne $true -or
            $artifact.verification.releaseMetadataVerified -ne $true -or $artifact.verification.configuration -cne 'Release' -or
            $artifact.verification.certificateSha256 -ine $trustedCertificate) { throw 'PrivateApk:ReleaseVerificationMissing' }
        # Re-run verification against the actual file and an independently supplied
        # trusted fingerprint. A manifest is not itself proof of APK signing.
        $verification = Confirm-MobileSignedApk -Path $path -ExpectedApp $app[0] -Version $releaseVersion `
            -VersionCode $releaseCode -Tools $AndroidTools -ExpectedCertificateSha256 $trustedCertificate `
            -ToolInvoker $ApkToolInvoker -Configuration Release
        # A tool invocation can take time. Refuse source drift during verification.
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sourceHash -or
            (Get-Item -LiteralPath $path).Length -ne $sourceItem.Length) { throw 'PrivateApk:SourceChangedDuringVerification' }
        $objectName = "$ObjectPrefix/$releaseVersion-$releaseCode/$($artifact.app)/$sourceHash/$($artifact.app).apk"
        $result += [ordered]@{
            app = $artifact.app; packageId = $app[0].PackageId; version = $releaseVersion; versionCode = $releaseCode
            sourceFile = [string]$artifact.file; sha256 = $sourceHash; bytes = $sourceItem.Length
            certificateSha256 = $verification.certificateSha256; objectName = $objectName
            objectUri = "gs://$BucketName/$objectName"; contentType = 'application/vnd.android.package-archive'
            contentDisposition = "attachment; filename=`"$($artifact.app)-$releaseVersion-$releaseCode.apk`""
            cacheControl = 'private,max-age=0,no-store'
            uploadIfGenerationMatch = 0; sourceValidation = 'Passed_ActualApkRechecked'
        }
    }
    if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $manifestHash) {
        throw 'PrivateApk:ManifestChangedDuringVerification'
    }
    [ordered]@{
        schema = 'private-apk-gcs-preparation.r1'; status = 'LocalPrepared_CloudNotAttempted'
        manifestPath = $manifest; manifestSha256 = $manifestHash; version = $releaseVersion; versionCode = $releaseCode
        server = $releaseServer; serverConfigurationBasis = 'ManifestDeclaration_NotEmbeddedApkConfigurationProof'
        embeddedEndpointVerified = $false
        bucketName = $BucketName; objectPrefix = $ObjectPrefix; artifacts = $result
        readyForSharing = $false; cloudCallAttempted = $false; apkUploadCompleted = $false
        bucketPrivacyVerified = $false; remoteByteHashVerified = $false; downloadUrlIssued = $false
        externalServerCommunicationVerified = $false; physicalDeviceInstallVerified = $false
        constraints = [ordered]@{
            createBucketAllowed = $false; changeAclOrIamAllowed = $false; publicAccessAllowed = $false
            createSigningKeyAllowed = $false; overwriteObjectAllowed = $false
            signedUrlOutput = 'PrivateLocalFileOnly_WhenSeparatelyAuthorized'; signedUrlMaximumMinutes = 60
            objectGenerationMustBeRecorded = $true
        }
        nextSteps = @(
            'VerifyExistingBucketPrivacyAndCurrentIam_NoMutation',
            'RecheckLocalInputsThenUploadNewObjectOnly_IfGenerationMatch0_RequiresSeparateRequest',
            'DownloadExactRecordedGenerationAndCompareSha256',
            'IssueExpiringUrlIntoPrivateLocalFile_RequiresSeparateRequest',
            'VerifyExternalHttpsServerAndInvitedAccountAccess',
            'VerifyPhysicalPhoneInstallUpdateAndWorkflow'
        )
    }
}

function Write-PrivateApkGcsPreparation {
    param([Parameter(Mandatory = $true)]$Preparation,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [string]$OutputRoot = 'artifacts/local/private-apk-gcs')
    # This helper can be dot-sourced independently of the validator. Validate all
    # fields used in an output path instead of trusting a caller-supplied object.
    if ($Preparation.schema -cne 'private-apk-gcs-preparation.r1' -or
        $Preparation.status -cne 'LocalPrepared_CloudNotAttempted' -or
        [string]$Preparation.version -notmatch '\A\d+\.\d+\.\d+\z' -or
        [string]$Preparation.versionCode -notmatch '\A[1-9]\d{0,9}\z' -or
        [long]$Preparation.versionCode -gt 2100000000) { throw 'PrivateApk:PreparationMetadataInvalid' }
    if ($OutputRoot -notmatch '\Aartifacts/local/[a-zA-Z0-9_-]+(/[a-zA-Z0-9_-]+)*\z') {
        throw 'PrivateApk:OutputMustBePrivateLocalArtifacts'
    }
    $json = $Preparation | ConvertTo-Json -Depth 12
    $encoded = [Text.UTF8Encoding]::new($false).GetBytes($json + "`n")
    $digest = [Security.Cryptography.SHA256]::HashData($encoded)
    $planHash = [Convert]::ToHexString($digest).ToLowerInvariant()
    $relative = "$OutputRoot/$($Preparation.version)-$($Preparation.versionCode)/$planHash/gcs-upload-plan.json"
    $resolvedRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    $privateRoot = [IO.Path]::GetFullPath((Join-Path $resolvedRoot 'artifacts/local'))
    $path = [IO.Path]::GetFullPath((Join-Path $resolvedRoot $relative))
    $privateRelative = [IO.Path]::GetRelativePath($privateRoot, $path)
    if ([IO.Path]::IsPathRooted($privateRelative) -or $privateRelative -in @('.', '..') -or
        $privateRelative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
        throw 'PrivateApk:OutputOutsidePrivateLocalArtifacts'
    }
    # Walk existing ancestors before creating any new directory; never follow a
    # junction out of the ignored local artifact area.
    $ancestor = Split-Path -Parent $path
    while (-not (Test-Path -LiteralPath $ancestor)) { $ancestor = Split-Path -Parent $ancestor }
    Assert-PrivateApkNonLinkedPath $ancestor
    if (Test-Path -LiteralPath $path) {
        Assert-PrivateApkNonLinkedPath $path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $planHash) {
            throw 'PrivateApk:ImmutablePreparationConflict'
        }
        $writeState = 'ExistingIdentical'
    } else {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
        $stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($encoded, 0, $encoded.Length) } finally { $stream.Dispose() }
        $writeState = 'Created'
    }
    [ordered]@{ status = $Preparation.status; writeState = $writeState; planPath = $relative; planSha256 = $planHash
        apps = @($Preparation.artifacts.app); readyForSharing = $false; cloudCallAttempted = $false }
}
