#requires -Version 7.4
# Synthetic byte/archive fixtures only. Never builds .NET, connects to a DB, or deploys.
[CmdletBinding()]
param([string]$EvidencePath, [string]$BashPath, [string]$PythonPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Formats.Tar
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Join-Path ([IO.Path]::GetTempPath()) ('mirror-field-package-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$checker = Join-Path $PSScriptRoot 'Test-IntegratedMobileFieldPackage.ps1'
$packer = Join-Path $repo 'deploy/azure-vm/package-mobile-field-test.ps1'
$profile = Join-Path $repo 'deploy/azure-vm/compose.integrated-mobile-field-test.override.yaml'
$checks = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [scriptblock]$Body) {
    try { & $Body | Out-Null } catch { throw "Fixture failed: $Name. $($_.Exception.Message)" }
    $checks.Add([ordered]@{ name=$Name; status='Passed' })
}
function Reject([scriptblock]$Body) {
    $failed=$false; try { & $Body | Out-Null } catch { $failed=$true }
    if (-not $failed) { throw 'Expected rejection was not observed.' }
}
function WriteTar([string]$Path, [Collections.IDictionary]$Files, [switch]$Gzip) {
    $file = [IO.File]::Create($Path)
    $stream = if ($Gzip) { [IO.Compression.GZipStream]::new($file,[IO.Compression.CompressionLevel]::Fastest,$true) } else { $file }
    $tar = [System.Formats.Tar.TarWriter]::new($stream,[System.Formats.Tar.TarEntryFormat]::Pax,$true)
    try {
        foreach ($name in $Files.Keys) {
            [byte[]]$bytes = if ($Files[$name] -is [byte[]]) { $Files[$name] } else { [Text.Encoding]::UTF8.GetBytes([string]$Files[$name]) }
            $entry=[System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile,[string]$name)
            $memory=[IO.MemoryStream]::new($bytes,$false)
            try { $entry.DataStream=$memory; $tar.WriteEntry($entry) } finally { $memory.Dispose() }
        }
    } finally { $tar.Dispose(); if ($Gzip) { $stream.Dispose() }; $file.Dispose() }
}
function Web([string]$Name, [Collections.IDictionary]$Extra=@{}, [switch]$Legacy) {
    $path=Join-Path $root ($Name+'.tar.gz')
    $files=[ordered]@{ 'index.html'='<base href="/"><link href="Ssalddel.WebApp.styles.css"><div id="app"></div>'; '_framework/fixture.txt'='synthetic web fixture'; 'appsettings.Production.json'='{"SsalddelEndpoints":{"ServerBaseAddress":""}}' }
    if ($Legacy) { $files['index.html']='<html>old role launcher</html>' }
    foreach($key in $Extra.Keys){$files[$key]=$Extra[$key]}
    WriteTar $path $files -Gzip; $path
}
function Image([string]$Name, [Collections.IDictionary]$Extra=@{}, [switch]$WrongTag, [switch]$DeletedLater, [switch]$GzipLayer,
    [Collections.IDictionary]$Configuration=@{}, [string[]]$AdditionalTags=@(), [switch]$MissingConfig) {
    $layer=Join-Path $root ($Name+'-layer.tar')
    $files=[ordered]@{ 'app/Ssalddel.dll'='synthetic non-executable fixture'; 'app/appsettings.json'='{}'; 'etc/ssl/certs/fixture.pem'='public CA fixture' }
    foreach($key in $Extra.Keys){$files[$key]=$Extra[$key]}
    WriteTar $layer $files -Gzip:$GzipLayer
    $layers=@('layer-1.tar'); $outer=[ordered]@{ 'layer-1.tar'=[IO.File]::ReadAllBytes($layer) }
    if (-not $MissingConfig) { $outer['config.json']=$Configuration|ConvertTo-Json -Depth 15 -Compress }
    if ($DeletedLater) {
        $deleted=Join-Path $root ($Name+'-deleted.tar'); WriteTar $deleted @{ 'app/.wh.appsettings.Local.json'='' }
        $layers += 'layer-2.tar'; $outer['layer-2.tar']=[IO.File]::ReadAllBytes($deleted)
    }
    $tag=if($WrongTag){'unrelated:latest'}else{'ssalddel-server:azure-preview'}
    $outer['manifest.json']=ConvertTo-Json -Depth 4 -Compress -InputObject @(@{ Config='config.json'; RepoTags=(@($tag)+$AdditionalTags); Layers=$layers })
    $path=Join-Path $root ($Name+'.tar'); WriteTar $path $outer; $path
}
function Package([string]$Name,[string]$Image,[string]$Web) {
    $out=Join-Path $root $Name
    & $packer -Profile integrated-mobile-field-test -ServerImageArchive $Image -WebArchive $Web -OutputDirectory $out -SourceSnapshotSha256 ('a'*64) | Out-Null
    $out
}
function ProfileReject([string]$Name,[scriptblock]$Change) {
    $copy=Get-Content -LiteralPath $profile -Raw|ConvertFrom-Json -AsHashtable
    & $Change $copy
    $path=Join-Path $root ($Name+'.yaml');$copy|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $path -Encoding utf8NoBOM
    Reject { & $checker -RepositoryRoot $repo -ProfilePath $path -AsObject }
}
try {
    $web=Web valid-web; $image=Image valid-image; $bundle=$null
    Check 'profile retains Simulation and explicitly does not claim all HostedServices disabled' {
        $result=& $checker -AsObject
        if($result.allHostedServicesDisabled -or -not $result.generalProjectionAndInboxWorkersMayRun -or $result.publicReleaseReady){throw 'Worker/release claims differ.'}
    }
    Check 'actual packager validates a unified web archive and nested docker-save layers offline' {
        $script:bundle=Package valid-bundle $image $web
        $result=& $checker -BundleDirectory $bundle -AsObject
        if($result.inspectedImageLayers -ne 1 -or $result.cloudDeployment -or $result.actualPushReceived){throw 'Archive checks became external completion claims.'}
    }
    Check 'legacy food packaging remains available with its original profile and root' {
        $out=Join-Path $root legacy-food
        & $packer -ServerImageArchive $image -WebArchive $web -OutputDirectory $out | Out-Null
        $m=Get-Content -LiteralPath (Join-Path $out deployment-manifest.json) -Raw|ConvertFrom-Json
        if($m.profile -ne 'food-mobile-field-test' -or $m.deploymentRoot -ne '/opt/ssalddel-mobile-field-test'){throw 'Legacy default changed.'}
    }
    Check 'gzip OCI layers are also inspected without Docker or extraction' { Package gzip-image (Image gzip -GzipLayer) $web }
    Check 'an existing bundle is never overwritten' { Reject { Package valid-bundle $image $web } }
    Check 'a source handoff fingerprint is mandatory' { Reject { & $packer -Profile integrated-mobile-field-test -ServerImageArchive $image -WebArchive $web -OutputDirectory (Join-Path $root no-source) } }
    Check 'old role launcher is not a unified web deployment' { Reject { Package old-launcher $image (Web legacy -Legacy) } }
    Check 'local web configuration is rejected' { Reject { Package web-local $image (Web local @{ 'appsettings.Local.json'='{}' }) } }
    Check 'dot-env files are rejected rather than normalized to public names' { Reject { Package web-dotenv $image (Web dotenv @{ './.env'='synthetic fixture' }) } }
    Check 'a renamed private credential JSON is rejected' { Reject { Package web-credential $image (Web credentials @{ 'renamed.json'='{"private_key":"fixture-not-a-real-key"}' }) } }
    Check 'parent path in web archive is rejected' { Reject { Package web-parent $image (Web parent @{ '../escape.txt'='fixture' }) } }
    Check 'local image configuration is rejected even when deleted in a later layer' { Reject { Package image-local (Image image-local @{ 'app/appsettings.Local.json'='{}' } -DeletedLater) $web } }
    Check 'private image evidence is rejected' { Reject { Package image-private (Image image-private @{ 'app/artifacts/customer.txt'='fixture' }) $web } }
    Check 'a renamed private credential in the image is rejected' { Reject { Package image-key (Image image-key @{ 'app/renamed.json'='{"private_key":"fixture-not-a-real-key"}' }) $web } }
    Check 'an unrelated image tag is rejected' { Reject { Package image-tag (Image image-tag -WrongTag) $web } }
    Check 'ordinary image environment labels and history remain accepted' {
        Package image-metadata (Image image-metadata -Configuration @{config=@{Env=@('ASPNETCORE_HTTP_PORTS=8080','PATH=/usr/bin','JWT_SECRET=');Labels=@{'org.opencontainers.image.title'='Mirror'}};history=@(@{created_by='ENV ASPNETCORE_HTTP_PORTS=8080'},@{created_by='RUN apt-get install curl'})}) $web
    }
    Check 'private values embedded in image config fields are rejected' { Reject { Package config-key (Image config-key -Configuration @{private_key='fixture-not-a-real-key'}) $web } }
    Check 'private values embedded in image environment are rejected' { Reject { Package config-env (Image config-env -Configuration @{config=@{Env=@('Jwt__SecretKey=fixture-not-a-real-key')}}) $web } }
    Check 'private values embedded in image labels are rejected' { Reject { Package config-label (Image config-label -Configuration @{config=@{Labels=@{'mirror.client_secret'='fixture-not-a-real-key'}}}) $web } }
    Check 'credential assignment in a general image label is rejected' { Reject { Package config-label-value (Image config-label-value -Configuration @{config=@{Labels=@{'description'='API_KEY=fixture-not-a-real-key'}}}) $web } }
    Check 'prior image history credentials are rejected even after an empty replacement' { Reject { Package config-history (Image config-history -Configuration @{config=@{Env=@('API_KEY=')};history=@(@{created_by='ENV API_KEY=fixture-not-a-real-key'},@{created_by='ENV API_KEY='})}) $web } }
    Check 'credential CLI arguments embedded in image history are rejected' { Reject { Package config-history-arg (Image config-history-arg -Configuration @{history=@(@{created_by='RUN client --password fixture-not-a-real-key'})}) $web } }
    Check 'private key material embedded in image history is rejected' { Reject { Package config-history-pem (Image config-history-pem -Configuration @{history=@(@{comment='-----BEGIN PRIVATE KEY----- synthetic fixture -----END PRIVATE KEY-----'})}) $web } }
    Check 'an image with a missing manifest config is rejected' { Reject { Package config-missing (Image config-missing -MissingConfig) $web } }
    Check 'an additional image tag cannot overwrite an unrelated tag on docker load' { Reject { Package image-extra-tag (Image image-extra-tag -AdditionalTags @('unrelated:production')) $web } }
    Check 'a duplicated deployment image tag is rejected' { Reject { Package image-duplicate-tag (Image image-duplicate-tag -AdditionalTags @('ssalddel-server:azure-preview')) $web } }
    Check 'Operational profile is rejected' { ProfileReject mode {param($p)$p.services.app.environment.SsalddelExecution__Mode='Operational'} }
    Check 'member delivery synthetic capability must stay explicitly enabled' { ProfileReject member-missing {param($p)$p.services.app.environment.Remove('NeighborhoodMemberDelivery__Enabled')} }
    Check 'member delivery Operational activation is rejected' { ProfileReject member-operational {param($p)$p.services.app.environment.NeighborhoodMemberDelivery__OperationalEnabled='true'} }
    Check 'financial execution is rejected' { ProfileReject money {param($p)$p.services.app.environment.TransportPrepayment__Enabled='true'} }
    Check 'a pasted credential in an otherwise valid profile is rejected' { ProfileReject pasted-key {param($p)$p.services.app.environment.Jwt__SecretKey='synthetic-not-a-real-key'} }
    Check 'automatic synthetic dispatch is rejected' { ProfileReject dispatch {param($p)$p.services.app.environment.MobileFieldTest__FoodDispatch__Enabled='true'} }
    Check 'weekly automatic preparation is rejected' { ProfileReject weekly {param($p)$p.services.app.environment.DriverPayoutBatch__AutomaticPreparationEnabled='true'} }
    Check 'a duplicate database connection key cannot point to a shared DB' { ProfileReject duplicate-db {param($p)$p.services.app.environment.ConnectionStrings__DefaultConnection+='Database=production;'} }
    Check 'shared volumes are rejected' { ProfileReject volumes {param($p)$p.volumes.mysql_data.name='ssalddel-preview-mysql-data'} }
    Check 'public photo storage is rejected' { ProfileReject storage {param($p)$p.services.app.environment.GoogleCloudStorage__PublicBucketName='public'} }
    Check 'credentials must stay read-only external mounts' { ProfileReject mount {param($p)$p.services.app.volumes[0].read_only=$false} }
    Check 'all-workers-disabled claim is rejected' { ProfileReject workers {param($p)$p['x-mirror-field-test'].allHostedServicesDisabled=$true} }
    Check 'a changed deployment file is rejected by its hash' {
        $path=Join-Path $bundle Caddyfile; $before=[IO.File]::ReadAllBytes($path)
        try { Add-Content -LiteralPath $path -Value '# fixture tamper'; Reject { & $checker -BundleDirectory $bundle -AsObject } } finally { [IO.File]::WriteAllBytes($path,$before) }
    }
    if (-not $BashPath) { $BashPath=Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles')) 'Git/bin/bash.exe' }
    Check 'both deployment scripts have valid Bash syntax' {
        foreach($name in @('deploy-preview-profile.sh','deploy-integrated-mobile-field-test.sh')) {
            & $BashPath -n ((Join-Path $repo ('deploy/azure-vm/'+$name)).Replace('\','/'))
            if($LASTEXITCODE -ne 0){throw 'Bash syntax failed.'}
        }
    }
    Check 'existing isolated deployment rollback fixtures still pass without Docker or DB' {
        & $BashPath ((Join-Path $repo 'eng/staging/test-azure-deployment-rollback.sh').Replace('\','/'))
        if($LASTEXITCODE -ne 0){throw 'Existing rollback fixtures failed.'}
    }
    foreach($mode in @('empty','own','other')) {
        Check "public-port owner guard with mocked Docker: $mode" {
            & $BashPath -c 'source "$1"; mode="$2"; docker(){ if [[ "$1" == ps ]]; then [[ "$mode" == empty ]] || echo fixture-container; elif [[ "$1" == inspect ]]; then if [[ "$mode" == own ]]; then echo mirror-integrated-field-test; else echo unrelated-service; fi; fi; return 0; }; if validate_integrated_public_ports; then [[ "$mode" != other ]]; else [[ "$mode" == other ]]; fi' _ ((Join-Path $repo 'deploy/azure-vm/deploy-preview-profile.sh').Replace('\','/')) $mode 2>$null
            if($LASTEXITCODE -ne 0){throw 'Gateway ownership guard failed.'}
        }
    }
    if (-not $PythonPath) { $PythonPath=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' }
    $previousPython=[Environment]::GetEnvironmentVariable('MIRROR_FIXTURE_PYTHON','Process')
    [Environment]::SetEnvironmentVariable('MIRROR_FIXTURE_PYTHON',$PythonPath.Replace('\','/'),'Process')
    try {
        Check 'the real host-side profile guard accepts the reviewed synthetic bundle without Docker or DB' {
            & $BashPath -c 'python3(){ "$MIRROR_FIXTURE_PYTHON" "$@"; }; source "$1"; validate_integrated_field_profile "$2"' _ ((Join-Path $repo 'deploy/azure-vm/deploy-preview-profile.sh').Replace('\','/')) $bundle.Replace('\','/')
            if($LASTEXITCODE -ne 0){throw 'Host-side profile guard failed.'}
        }
        Check 'the host-side guard rejects a wrong database even if environment names still look isolated' {
            $path=Join-Path $bundle 'compose.integrated-mobile-field-test.override.yaml';$before=[IO.File]::ReadAllBytes($path)
            try {
                $p=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -AsHashtable;$p.services.app.environment.ConnectionStrings__DefaultConnection+='Database=production;'
                $p|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $path -Encoding utf8NoBOM
                & $BashPath -c 'python3(){ "$MIRROR_FIXTURE_PYTHON" "$@"; }; source "$1"; validate_integrated_field_profile "$2"' _ ((Join-Path $repo 'deploy/azure-vm/deploy-preview-profile.sh').Replace('\','/')) $bundle.Replace('\','/') 2>$null
                if($LASTEXITCODE -eq 0){throw 'Host guard accepted a shared database.'}
            } finally { [IO.File]::WriteAllBytes($path,$before) }
        }
    } finally { [Environment]::SetEnvironmentVariable('MIRROR_FIXTURE_PYTHON',$previousPython,'Process') }
    $result=[ordered]@{schemaVersion='integrated-mobile-field-package-tests.r1';status='Passed';count=$checks.Count;checks=@($checks);syntheticFixturesOnly=$true;actualCloudDeployment=$false;actualDeviceVerified=$false;actualPushReceived=$false;dotnetBuildExecuted=$false}
    if($EvidencePath){$path=[IO.Path]::GetFullPath($EvidencePath);New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force|Out-Null;$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $path -Encoding utf8NoBOM}
    $result|ConvertTo-Json -Depth 7
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if([IO.Path]::GetDirectoryName($resolved) -eq $parent -and [IO.Path]::GetFileName($resolved).StartsWith('mirror-field-package-fixture-')) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
