#requires -Version 7.0
# Input is the root release-manifest.json produced by publish-mobile-field-test.ps1.
# Supply a previously trusted signing certificate SHA-256 independently of the
# manifest. Validate writes no files; Prepare writes an ignored local intent only.
# Neither mode uploads files or issues a usable download URL.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$BucketName,
    [Parameter(Mandatory = $true)][string]$ExpectedCertificateSha256,
    [string]$ObjectPrefix = 'private-apk',
    [string]$OutputRoot = 'artifacts/local/private-apk-gcs',
    [string]$AndroidSdkDirectory,
    [ValidateSet('Validate', 'Prepare')][string]$Mode = 'Validate'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'PrivateApkGcs.Common.ps1')
$parameters = @{
    ManifestPath = $ManifestPath; BucketName = $BucketName; ExpectedCertificateSha256 = $ExpectedCertificateSha256
    ObjectPrefix = $ObjectPrefix; AndroidTools = (Resolve-MobileAndroidBuildTools -AndroidSdkDirectory $AndroidSdkDirectory)
}
$preparation = New-PrivateApkGcsPreparation @parameters
if ($Mode -eq 'Prepare') {
    Write-PrivateApkGcsPreparation -Preparation $preparation -RepositoryRoot $repositoryRoot -OutputRoot $OutputRoot |
        ConvertTo-Json -Depth 4
} else {
    [ordered]@{ status = 'Validated_NoFilesWritten'; apps = @($preparation.artifacts.app)
        readyForSharing = $false; cloudCallAttempted = $false; bucketPrivacyVerified = $false
        embeddedEndpointVerified = $false
        remoteByteHashVerified = $false; downloadUrlIssued = $false; physicalDeviceInstallVerified = $false } |
        ConvertTo-Json -Depth 4
}
