[CmdletBinding()]
param([string]$BashPath, [string]$EvidencePath)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'AzureMobileHardening.Common.ps1')
. (Join-Path $repoRoot 'eng/release/MobileFieldTest.Common.ps1')
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('ssalddel-infra-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [scriptblock]$Body) {
    try { & $Body } catch { throw "Fixture failed: $Name. $($_.Exception.Message)" }
    $results.Add([pscustomobject]@{ name = $Name; result = 'Passed' })
}
function MustReject([scriptblock]$Body, [string]$Pattern) {
    $caught = $null
    try { & $Body } catch { $caught = $_ }
    if ($null -eq $caught -or $caught.Exception.Message -notmatch $Pattern) { throw "Expected guard failure was not observed: $($caught.Exception.Message)" }
}
try {
    foreach ($engine in @('MySQL', 'MongoDB')) {
        Check "$engine same source destination rejected" {
            MustReject { Assert-RecoveryDatabaseTargets 'fixture_restore_verify' 'fixture_restore_verify' $engine } 'different databases'
        }
        Check "$engine case variant rejected" {
            MustReject { Assert-RecoveryDatabaseTargets 'FIXTURE_restore_verify' 'fixture_restore_verify' $engine } 'different databases'
        }
        Check "$engine distinct restore accepted" { Assert-RecoveryDatabaseTargets 'fixture' 'fixture_restore_verify' $engine }
        Check "$engine invalid restore rejected" {
            MustReject { Assert-RecoveryDatabaseTargets 'fixture' 'fixture;drop_restore_verify' $engine } 'valid source'
        }
    }
    Check 'initializer and rehearsal use four contexts' {
        $contexts = @(Get-StagingMigrationContextNames)
        $initializer = Get-Content -LiteralPath (Join-Path $repoRoot 'Ssalddel/Startup/DatabaseCompatibilityInitializer.cs') -Raw
        if ($contexts.Count -ne 4 -or @($contexts | Where-Object { $initializer -notmatch [regex]::Escape($_) }).Count) {
            throw 'Rehearsal context set does not match the initializer.'
        }
    }
    Check 'migration connection and backup source must match' {
        Assert-StagingMySqlMigrationTarget 'Server=fixture;Database=fixture;User ID=fixture;Password=fixture' 'fixture' 'fixture' '3306' 'fixture'
        MustReject { Assert-StagingMySqlMigrationTarget 'Server=fixture;Database=other;User ID=fixture;Password=fixture' 'fixture' 'fixture' '3306' 'fixture' } 'must match'
        MustReject { Assert-StagingMySqlMigrationTarget 'Server=other;Database=fixture;User ID=fixture;Password=fixture' 'fixture' 'fixture' '3306' 'fixture' } 'must match'
    }
    Check 'current MySQL provider accepts a 44-character migration source' {
        $database = 'a' * 44
        Assert-StagingMySqlMigrationTarget "Server=fixture;Database=$database;User=fixture;Password=fixture" $database 'fixture' '3306' 'fixture'
    }
    Check 'current MySQL provider rejects a 45-character migration source' {
        $database = 'a' * 45
        MustReject { Assert-StagingMySqlMigrationTarget "Server=fixture;Database=$database;User=fixture;Password=fixture" $database 'fixture' '3306' 'fixture' } 'at most 44'
    }
    Check 'non-migrated restore identifier accepts 64 characters and rejects 65' {
        Assert-RecoveryDatabaseTargets 'fixture' (('r' * 49) + '_restore_verify') 'MySQL'
        MustReject { Assert-RecoveryDatabaseTargets 'fixture' (('r' * 50) + '_restore_verify') 'MySQL' } 'valid source'
    }
    # Invoke the actual entry script with dummy environment values and intercepted
    # commands: both same-target failures must occur before any external command.
    $environment = @{
        SSALDDEL_STAGING_MYSQL_HOST = 'fixture'; SSALDDEL_STAGING_MYSQL_PORT = '3306'
        SSALDDEL_STAGING_MYSQL_USER = 'fixture'; SSALDDEL_STAGING_MYSQL_PASSWORD = 'fixture'
        SSALDDEL_STAGING_MYSQL_DATABASE = 'fixture'; SSALDDEL_STAGING_MYSQL_RESTORE_DATABASE = 'fixture_restore_verify'
        SSALDDEL_STAGING_MYSQL_CONNECTION = 'fixture'; SSALDDEL_STAGING_MONGODB_URI = 'fixture'
        SSALDDEL_STAGING_MONGODB_DATABASE = 'fixture'; SSALDDEL_STAGING_MONGODB_RESTORE_DATABASE = 'fixture_restore_verify'
        SSALDDEL_STAGING_REDIS_HOST = 'fixture'; SSALDDEL_STAGING_REDIS_PORT = '6379'
    }
    $previous = @{}
    foreach ($name in $environment.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $environment[$name])
    }
    function dotnet { $script:externalCalls++; throw 'Fixture must never reach an external command.' }
    function mysqldump { $script:externalCalls++; throw 'Fixture must never reach an external command.' }
    try {
        foreach ($engine in @('MYSQL', 'MONGODB')) {
            Check "$engine entry script prevents destructive commands" {
                $sourceName = "SSALDDEL_STAGING_$($engine)_DATABASE"
                [Environment]::SetEnvironmentVariable($sourceName, 'fixture_restore_verify')
                $script:externalCalls = 0
                MustReject {
                    & (Join-Path $PSScriptRoot 'Invoke-StagingReadiness.ps1') -EnvironmentName staging -Confirmation REHEARSE_STAGING_RECOVERY -EvidenceDirectory (Join-Path $temporaryRoot $engine)
                } 'different databases'
                if ($script:externalCalls -ne 0) { throw 'Rejected recovery still invoked an external command.' }
                [Environment]::SetEnvironmentVariable($sourceName, 'fixture')
            }
        }
        Check '45-character migration source entry guard precedes backups and external commands' {
            [Environment]::SetEnvironmentVariable('SSALDDEL_STAGING_MYSQL_DATABASE', 'a' * 45)
            $script:externalCalls = 0
            MustReject {
                & (Join-Path $PSScriptRoot 'Invoke-StagingReadiness.ps1') -EnvironmentName staging -Confirmation REHEARSE_STAGING_RECOVERY -EvidenceDirectory (Join-Path $temporaryRoot 'long-source')
            } 'at most 44'
            if ($script:externalCalls -ne 0) { throw 'Invalid migration lock name still reached an external command.' }
            [Environment]::SetEnvironmentVariable('SSALDDEL_STAGING_MYSQL_DATABASE', 'fixture')
        }
    } finally {
        foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
        Remove-Item Function:\dotnet, Function:\mysqldump
    }
    $apps = @(Get-MobileFieldTestApps)
    Check 'all seven existing role apps retain their package IDs' {
        if ($apps.Count -ne 7 -or @($apps.PackageId | Sort-Object -Unique).Count -ne 7) { throw 'Seven distinct apps are required.' }
        foreach ($app in $apps) {
            [xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot $app.Project) -Raw
            if ($app.PackageId -notin @($project.Project.PropertyGroup.ApplicationId)) { throw "Package ID mismatch: $($app.Name)" }
        }
    }
    Check 'APK-only signing uses environment references' {
        $arguments = @(Get-MobileFieldTestPublishArguments 'fixture.csproj' 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        if ('-p:AndroidPackageFormats=apk' -notin $arguments -or ($arguments -join ' ') -match 'aab' -or
            '-p:AndroidSigningStorePass=env:SSALDDEL_ANDROID_KEYSTORE_PASSWORD' -notin $arguments -or
            '-p:AndroidSigningKeyPass=env:SSALDDEL_ANDROID_KEY_PASSWORD' -notin $arguments -or
            '-p:RuntimeIdentifiers="android-arm64;android-x64"' -notin $arguments) { throw 'APK-only environment signing or runtime list quoting was not preserved.' }
    }
    Check 'actual MSBuild item expansion receives two separate runtime identities' {
        $arguments = @(Get-MobileFieldTestPublishArguments 'fixture.csproj' 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $runtimeArgument = @($arguments | Where-Object { $_.StartsWith('-p:RuntimeIdentifiers=') })
        $probeProject = Join-Path $temporaryRoot 'RuntimeIdentifiers.proj'
        '<Project><ItemGroup><RuntimeIdentifierProbe Include="$(RuntimeIdentifiers)" /></ItemGroup></Project>' |
            Set-Content -LiteralPath $probeProject -Encoding utf8
        # Real MSBuild item semantics distinguish a list from the escaped scalar
        # that -getProperty prints identically. No target/build/restore is run.
        $itemOutput = @(& dotnet msbuild $probeProject $runtimeArgument[0] '-getItem:RuntimeIdentifierProbe' '-nologo' 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'MSBuild runtime item probe failed.' }
        $identities = @(($itemOutput -join [Environment]::NewLine | ConvertFrom-Json).Items.RuntimeIdentifierProbe.Identity)
        if ($identities.Count -ne 2 -or $identities[0] -cne 'android-arm64' -or $identities[1] -cne 'android-x64') {
            throw 'APK runtimes must expand into two distinct NuGet runtime items.'
        }
        $scalarOutput = @(& dotnet msbuild $probeProject '-p:RuntimeIdentifiers=android-arm64%3Bandroid-x64' '-getItem:RuntimeIdentifierProbe' '-nologo' 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'MSBuild counterexample probe failed.' }
        $scalarIdentities = @(($scalarOutput -join [Environment]::NewLine | ConvertFrom-Json).Items.RuntimeIdentifierProbe.Identity)
        if ($scalarIdentities.Count -ne 1 -or $scalarIdentities[0] -cne 'android-arm64;android-x64') {
            throw 'The known escaped scalar counterexample was not reproduced.'
        }
    }
    Check 'actual MSBuild CLI reads both APK runtimes without splitting arguments' {
        $arguments = @(Get-MobileFieldTestPublishArguments $apps[0].Project 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $runtimeArgument = @($arguments | Where-Object { $_.StartsWith('-p:RuntimeIdentifiers=') })
        if ($runtimeArgument.Count -ne 1) { throw 'APK runtime property must be passed once.' }
        # -getProperty evaluates the existing project without build, restore,
        # signing, Android tools or contacting an external service.
        $property = @(& dotnet msbuild (Join-Path $repoRoot $apps[0].Project) $runtimeArgument[0] '-getProperty:RuntimeIdentifiers' '-nologo' 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($property -join [Environment]::NewLine).Trim() -cne 'android-arm64;android-x64') {
            throw 'MSBuild did not receive the expected two runtime identifiers.'
        }
    }
    Check 'portable reference policy is release opt-in and preserves app ABI and signing arguments' {
        $arguments = @(Get-MobileFieldTestPublishArguments $apps[0].Project 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $hooks = @($arguments | Where-Object { $_.StartsWith('-p:CustomAfterMicrosoftCommonTargets=') })
        $expected = [IO.Path]::GetFullPath((Join-Path $repoRoot 'eng/release/MobileFieldTest.PortableReferences.targets'))
        if ($hooks.Count -ne 1 -or [IO.Path]::GetFullPath($hooks[0].Substring('-p:CustomAfterMicrosoftCommonTargets='.Length)) -ne $expected -or
            '-p:SsalddelMobileFieldTestPortableReferences=true' -notin $arguments -or
            '-p:RuntimeIdentifiers="android-arm64;android-x64"' -notin $arguments -or
            @($arguments | Where-Object { $_ -match '^-p:(IsRidAgnostic|DirectoryBuildPropsPath|DirectoryBuildTargetsPath)=' }).Count) {
            throw 'The portable policy must be opt-in without overriding app ABI or repository build rules.'
        }
    }
    Check 'portable reference XML scope excludes executables nonstandard libraries and explicit RID policy' {
        [xml]$policy = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/release/MobileFieldTest.PortableReferences.targets') -Raw
        $groups = @($policy.Project.PropertyGroup)
        $targets = @($policy.Project.Target | Where-Object Name -CEQ 'SsalddelMobileFieldTestClassifyPortableReference')
        if ($groups.Count -ne 1 -or $groups[0]._SsalddelMobileDeclaredRidPolicy -cne '$(IsRidAgnostic)' -or
            $groups[0].Condition.Contains('@(') -or $targets.Count -ne 1 -or
            $targets[0].BeforeTargets -cne 'GetTargetFrameworksWithPlatformForSingleTargetFramework' -or
            $targets[0].DependsOnTargets -or $targets[0].AfterTargets -or
            @($targets[0].ChildNodes | Where-Object { $_ -is [Xml.XmlElement] -and $_.Name -notin @('ItemGroup', 'PropertyGroup') }).Count) {
            throw 'The hook must only classify project-reference metadata, never invoke a build target.'
        }
        foreach ($required in @("'`$(SsalddelMobileFieldTestPortableReferences)' == 'true'", "'`$(OutputType)' == 'Library'",
            "'`$(_SsalddelMobileDeclaredRidPolicy)' == ''", "'`$(TargetFrameworkIdentifier)' == '.NETStandard'",
            "'`$(TargetFramework)' == 'net10.0'", "'`$(TargetPlatformIdentifier)' == ''", "'`$(UseMaui)' != 'true'")) {
            if (-not $targets[0].Condition.Contains($required)) { throw 'The portable metadata scope is missing an exclusion boundary.' }
        }
        if ($targets[0].PropertyGroup.IsRidAgnostic -cne 'true') { throw 'The metadata classification did not preserve portable references.' }
        foreach ($required in @("'@(NativeReference)' == ''", "'@(NativeLibrary)' == ''", "'@(_SsalddelMobileUnsupportedPortablePackage)' == ''")) {
            if (-not $targets[0].PropertyGroup.Condition.Contains($required)) { throw 'Native and unknown packages must remain outside the portable policy.' }
        }
    }
    Check 'allowlisted net10 shared libraries contain only reviewed managed package references' {
        [xml]$policy = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/release/MobileFieldTest.PortableReferences.targets') -Raw
        $portableTarget = @($policy.Project.Target | Where-Object Name -CEQ 'SsalddelMobileFieldTestClassifyPortableReference')[0]
        $guard = $portableTarget.ItemGroup._SsalddelMobileUnsupportedPortablePackage
        $allowedPackages = @($guard.Exclude.Split(';'))
        if ($guard.Include -cne '@(PackageReference)' -or $allowedPackages.Count -ne 8) { throw 'Unknown or native package references must disable the portable profile.' }
        foreach ($projectName in @('Ssalddel.Contracts', 'Ssalddel.Client.Infrastructure', 'Ssalddel.Ui.Common', 'Ssalddel.BackOffice.Client')) {
            [xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot "$projectName/$projectName.csproj") -Raw
            if ('net10.0' -notin @($project.Project.PropertyGroup.TargetFramework) -or
                @($project.SelectNodes('//NativeReference|//NativeLibrary')).Count -or
                @($project.Project.ItemGroup.PackageReference.Include | Where-Object { $_ -and $_ -notin $allowedPackages }).Count) {
                throw 'An allowlisted shared project acquired unreviewed runtime or native dependencies.'
            }
            $condition = $portableTarget.Condition
            if (-not $condition.Contains("'`$(MSBuildProjectName)' == '$projectName'")) { throw 'The core-library profile must use an explicit reviewed project allowlist.' }
            $assetsPath = Join-Path $repoRoot "$projectName/obj/project.assets.json"
            if (Test-Path -LiteralPath $assetsPath) {
                $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
                foreach ($target in $assets.targets.PSObject.Properties) {
                    foreach ($library in $target.Value.PSObject.Properties) {
                        if ($library.Value.PSObject.Properties.Name -contains 'native' -or $library.Value.PSObject.Properties.Name -contains 'runtimeTargets') {
                            throw 'The existing resolved shared-library package assets are runtime specific and must not be treated as portable.'
                        }
                    }
                }
            }
        }
    }
    Check 'actual project-reference metadata keeps portable libraries RID agnostic while Android app retains both ABIs' {
        $arguments = @(Get-MobileFieldTestPublishArguments $apps[0].Project 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $runtime = @($arguments | Where-Object { $_.StartsWith('-p:RuntimeIdentifiers=') })[0]
        $hook = @($arguments | Where-Object { $_.StartsWith('-p:CustomAfterMicrosoftCommonTargets=') })[0]
        $optIn = '-p:SsalddelMobileFieldTestPortableReferences=true'
        foreach ($project in @('Ssalddel.WorkflowRules.Contracts/Ssalddel.WorkflowRules.Contracts.csproj', 'Ssalddel.CodeMetadata/Ssalddel.CodeMetadata.csproj',
            'Ssalddel.Contracts/Ssalddel.Contracts.csproj', 'Ssalddel.Client.Infrastructure/Ssalddel.Client.Infrastructure.csproj', 'Ssalddel.Ui.Common/Ssalddel.Ui.Common.csproj',
            'Ssalddel.BackOffice.Client/Ssalddel.BackOffice.Client.csproj')) {
            # GetTargetFrameworks returns the exact IsRidAgnostic metadata used by
            # project-reference negotiation. No compile/restore/signing target.
            $output = @(& dotnet msbuild (Join-Path $repoRoot $project) $runtime $hook $optIn '-target:GetTargetFrameworks' '-getTargetResult:GetTargetFrameworks' '-nologo' 2>&1)
            if ($LASTEXITCODE -ne 0) { throw 'Portable project-reference metadata query failed.' }
            $items = @(($output -join [Environment]::NewLine | ConvertFrom-Json).TargetResults.GetTargetFrameworks.Items)
            if ($items.Count -ne 1 -or $items[0].IsRidAgnostic -ine 'true' -or $items[0].TargetFrameworks -notin @('netstandard2.1', 'net10.0')) {
                throw 'Portable project-reference metadata must remove RuntimeIdentifier and SelfContained flow.'
            }
        }
        $output = @(& dotnet msbuild (Join-Path $repoRoot $apps[0].Project) $runtime $hook $optIn '-p:TargetFramework=net10.0-android' '-getProperty:IsRidAgnostic,RuntimeIdentifiers,TargetFrameworkIdentifier' '-nologo' 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'Android app evaluation query failed.' }
        $properties = ($output -join [Environment]::NewLine | ConvertFrom-Json).Properties
        if ($properties.IsRidAgnostic -ine 'false' -or $properties.RuntimeIdentifiers -cne 'android-arm64;android-x64' -or $properties.TargetFrameworkIdentifier -cne '.NETCoreApp') {
            throw 'Android app runtime identity or ABI selection changed.'
        }
        # These actual metadata counterexamples exercise the native/package and
        # explicit-policy gates without a package restore or compile target.
        $cases = @(
            @{ name = 'executable'; output = 'Exe'; item = ''; project = 'Ssalddel.Contracts'; extra = @(); optIn = $true },
            @{ name = 'native'; output = 'Library'; item = '<NativeReference Include="fixture-native.so" />'; project = 'Ssalddel.Contracts'; extra = @(); optIn = $true },
            @{ name = 'unknown-package'; output = 'Library'; item = '<PackageReference Include="RuntimeSpecificFixture" />'; project = 'Ssalddel.Contracts'; extra = @(); optIn = $true },
            @{ name = 'explicit-policy'; output = 'Library'; item = ''; project = 'Ssalddel.Contracts'; extra = @('-p:IsRidAgnostic=false'); optIn = $true },
            @{ name = 'unrelated-project'; output = 'Library'; item = ''; project = 'UnreviewedPortableLibrary'; extra = @(); optIn = $true },
            @{ name = 'not-opted-in'; output = 'Library'; item = ''; project = 'Ssalddel.Contracts'; extra = @(); optIn = $false }
        )
        foreach ($case in $cases) {
            $directory = Join-Path $temporaryRoot $case.name
            New-Item -ItemType Directory -Path $directory | Out-Null
            $project = Join-Path $directory ($case.project + '.csproj')
            "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>$($case.output)</OutputType></PropertyGroup><ItemGroup>$($case.item)</ItemGroup></Project>" |
                Set-Content -LiteralPath $project -Encoding utf8
            $probeArguments = @('msbuild', $project, $runtime, $hook)
            if ($case.optIn) { $probeArguments += $optIn }
            $probeArguments += $case.extra
            $probeArguments += @('-target:GetTargetFrameworks', '-getTargetResult:GetTargetFrameworks', '-nologo')
            $output = @(& dotnet @probeArguments 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "Metadata counterexample failed: $($case.name)" }
            $items = @(($output -join [Environment]::NewLine | ConvertFrom-Json).TargetResults.GetTargetFrameworks.Items)
            if ($items.Count -ne 1 -or $items[0].IsRidAgnostic -ine 'false') { throw "A nonportable metadata boundary was overridden: $($case.name)" }
        }
    }
    Check 'all seven Android app evaluated reference closures match the reviewed portable library set' {
        $arguments = @(Get-MobileFieldTestPublishArguments $apps[0].Project 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $runtime = @($arguments | Where-Object { $_.StartsWith('-p:RuntimeIdentifiers=') })[0]
        $hook = @($arguments | Where-Object { $_.StartsWith('-p:CustomAfterMicrosoftCommonTargets=') })[0]
        $queue = [Collections.Generic.Queue[string]]::new()
        foreach ($app in $apps) { $queue.Enqueue([IO.Path]::GetFullPath((Join-Path $repoRoot $app.Project))) }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $portable = [Collections.Generic.List[string]]::new()
        $androidAppCount = 0
        $androidLibraryCount = 0
        while ($queue.Count) {
            $projectPath = $queue.Dequeue()
            if (-not $seen.Add($projectPath)) { continue }
            [xml]$project = Get-Content -LiteralPath $projectPath -Raw
            $frameworks = @($project.SelectNodes('//TargetFramework|//TargetFrameworks') | ForEach-Object { $_.InnerText })
            $framework = if (@($frameworks | Where-Object { $_ -match 'net10\.0-android' }).Count) { 'net10.0-android' } else { $frameworks[0] }
            # Evaluate actual imported references and Android conditions. This
            # query performs no restore, compilation or packaging targets.
            $queryArguments = @('msbuild', $projectPath, $runtime, $hook, '-p:SsalddelMobileFieldTestPortableReferences=true', "-p:TargetFramework=$framework",
                '-getProperty:MSBuildProjectName,TargetFramework,TargetFrameworkIdentifier,TargetPlatformIdentifier,OutputType,UseMaui,AndroidApplication,IsRidAgnostic,RuntimeIdentifiers',
                '-getItem:ProjectReference', '-nologo')
            $output = @(& dotnet @queryArguments 2>&1)
            if ($LASTEXITCODE -ne 0) { throw 'Seven-app evaluated reference closure query failed.' }
            $evaluation = $output -join [Environment]::NewLine | ConvertFrom-Json
            $properties = $evaluation.Properties
            if ($properties.TargetFramework -ceq 'net10.0' -and $properties.OutputType -ceq 'Library' -and
                -not $properties.TargetPlatformIdentifier -and $properties.UseMaui -ine 'true') {
                $portable.Add($properties.MSBuildProjectName)
            } elseif ($properties.TargetFramework -ceq 'net10.0-android') {
                # Android SDK normalizes application OutputType to Library;
                # AndroidApplication and UseMaui identify the executable apps.
                if ($properties.IsRidAgnostic -ine 'false' -or $properties.RuntimeIdentifiers -cne 'android-arm64;android-x64') {
                    throw 'An Android app or native binding lost its runtime-specific ABI selection.'
                }
                if ($properties.AndroidApplication -ieq 'true' -and $properties.UseMaui -ieq 'true') { $androidAppCount++ } else { $androidLibraryCount++ }
            } elseif ($properties.TargetFrameworkIdentifier -cne '.NETStandard') {
                throw 'A new unreviewed platform or runtime-specific project entered the seven-app closure.'
            }
            foreach ($reference in @($evaluation.Items.ProjectReference)) {
                if (-not (Test-Path -LiteralPath $reference.FullPath -PathType Leaf)) { throw 'Evaluated project reference is missing.' }
                $queue.Enqueue($reference.FullPath)
            }
        }
        $expected = @('Ssalddel.BackOffice.Client', 'Ssalddel.Client.Infrastructure', 'Ssalddel.Contracts', 'Ssalddel.Ui.Common')
        $actual = @($portable | Sort-Object)
        if ($androidAppCount -ne 7 -or $androidLibraryCount -ne 1 -or $seen.Count -ne 22 -or
            ($actual -join ';') -cne ($expected -join ';')) {
            throw "The complete evaluated seven-app closure needs an explicit new portable-library or platform review. Apps=$androidAppCount AndroidLibraries=$androidLibraryCount Projects=$($seen.Count) Portable=$($actual -join ';')"
        }
    }
    Check 'reviewed Android binding metadata preserves managed DLL without removing app or JNI ABI selection' {
        [xml]$policy = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/release/MobileFieldTest.PortableReferences.targets') -Raw
        $targets = @($policy.Project.Target)
        $bindingTarget = @($targets | Where-Object Name -CEQ 'SsalddelMobileFieldTestClassifyNaverBindingReference')
        if ($targets.Count -ne 2 -or $bindingTarget.Count -ne 1 -or
            $bindingTarget[0].BeforeTargets -cne 'GetTargetFrameworksWithPlatformForSingleTargetFramework' -or
            @($bindingTarget[0].ChildNodes | Where-Object { $_ -is [Xml.XmlElement] -and $_.Name -notin @('ItemGroup', 'PropertyGroup') }).Count) {
            throw 'The native binding exception must only classify exact reference metadata.'
        }
        foreach ($required in @("'`$(MSBuildProjectName)' == 'DriverApp.NaverMaps.Android'", "'`$(TargetFramework)' == 'net10.0-android'",
            "'`$(AndroidApplication)' == 'false'", "'`$(UseMaui)' != 'true'", "'`$(_SsalddelMobileDeclaredRidPolicy)' == ''")) {
            if (-not $bindingTarget[0].Condition.Contains($required)) { throw 'An Android application or explicit policy entered the binding exception.' }
        }
        $arguments = @(Get-MobileFieldTestPublishArguments $apps[0].Project 'fixture-output' '1.2.3' 10 'https://field.example.test/' 'fixture.keystore' 'fixture')
        $runtime = @($arguments | Where-Object { $_.StartsWith('-p:RuntimeIdentifiers=') })[0]
        $hook = @($arguments | Where-Object { $_.StartsWith('-p:CustomAfterMicrosoftCommonTargets=') })[0]
        $metadataArguments = @($runtime, $hook, '-p:SsalddelMobileFieldTestPortableReferences=true', '-p:TargetFramework=net10.0-android',
            '-target:GetTargetFrameworks', '-getTargetResult:GetTargetFrameworks', '-nologo')
        $output = @(& dotnet msbuild (Join-Path $repoRoot 'DriverApp.NaverMaps.Android/DriverApp.NaverMaps.Android.csproj') @metadataArguments 2>&1)
        if ($LASTEXITCODE -ne 0) { throw 'Actual Android binding reference metadata probe failed.' }
        $items = @(($output -join [Environment]::NewLine | ConvertFrom-Json).TargetResults.GetTargetFrameworks.Items)
        if ($items.Count -ne 1 -or $items[0].IsRidAgnostic -ine 'true' -or $items[0].TargetFrameworks -cne 'net10.0-android') {
            throw 'The reviewed binding DLL must resolve from its shared Android framework output.'
        }
        foreach ($app in $apps) {
            $output = @(& dotnet msbuild (Join-Path $repoRoot $app.Project) @metadataArguments 2>&1)
            if ($LASTEXITCODE -ne 0) { throw 'Actual Android application metadata probe failed.' }
            $items = @(($output -join [Environment]::NewLine | ConvertFrom-Json).TargetResults.GetTargetFrameworks.Items)
            if ($items.Count -ne 1 -or $items[0].IsRidAgnostic -ine 'false') { throw 'Android application ABI packaging must remain runtime specific.' }
        }
        $maven = '<AndroidMavenLibrary Include="com.naver.maps:map-sdk" Version="3.23.2" /><AndroidMavenLibrary Include="com.naver.maps:geometry" Version="1.3.0" /><AndroidMavenLibrary Include="com.getkeepsafe.relinker:relinker" Version="1.4.4" /><AndroidMavenLibrary Include="com.squareup.okhttp3:okhttp" Version="4.10.0" /><AndroidMavenLibrary Include="com.squareup.okhttp3:okhttp-brotli" Version="4.10.0" /><AndroidMavenLibrary Include="org.brotli:dec" Version="0.1.2" />'
        $cases = @(
            @{ name = 'native-reference'; item = '<NativeReference Include="fixture-native.so" />'; maven = $maven; property = ''; extra = @() },
            @{ name = 'native-library'; item = '<NativeLibrary Include="fixture-native.so" />'; maven = $maven; property = ''; extra = @() },
            @{ name = 'native-android-library'; item = '<AndroidNativeLibrary Include="fixture-native.so" />'; maven = $maven; property = ''; extra = @() },
            @{ name = 'embedded-native-library'; item = '<EmbeddedNativeLibrary Include="fixture-native.so" />'; maven = $maven; property = ''; extra = @() },
            @{ name = 'unreviewed-package'; item = '<PackageReference Include="RuntimeSpecificFixture" />'; maven = $maven; property = ''; extra = @() },
            @{ name = 'changed-maven-version'; item = ''; maven = $maven.Replace('Version="3.23.2"', 'Version="99.0.0"'); property = ''; extra = @() },
            @{ name = 'missing-maven-coordinate'; item = ''; maven = $maven.Replace('<AndroidMavenLibrary Include="org.brotli:dec" Version="0.1.2" />', ''); property = ''; extra = @() },
            @{ name = 'application'; item = ''; maven = $maven; property = '<AndroidApplication>true</AndroidApplication>'; extra = @() },
            @{ name = 'explicit-false'; item = ''; maven = $maven; property = ''; extra = @('-p:IsRidAgnostic=false') }
        )
        foreach ($case in $cases) {
            $directory = Join-Path $temporaryRoot ('binding-' + $case.name)
            New-Item -ItemType Directory -Path $directory | Out-Null
            $project = Join-Path $directory 'DriverApp.NaverMaps.Android.csproj'
            "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net10.0-android</TargetFramework><OutputType>Library</OutputType>$($case.property)</PropertyGroup><ItemGroup>$($case.maven)$($case.item)</ItemGroup></Project>" |
                Set-Content -LiteralPath $project -Encoding utf8
            $probeArguments = @('msbuild', $project) + $metadataArguments + $case.extra
            $output = @(& dotnet @probeArguments 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "Binding metadata counterexample failed: $($case.name)" }
            $items = @(($output -join [Environment]::NewLine | ConvertFrom-Json).TargetResults.GetTargetFrameworks.Items)
            if ($items.Count -ne 1 -or $items[0].IsRidAgnostic -ine 'false') { throw "An unreviewed binding boundary was classified RID agnostic: $($case.name)" }
        }
    }
    $expectedApp = $apps[0]
    $certificate = 'a' * 64
    $apk = Join-Path $temporaryRoot 'fixture.apk'
    $archive = [IO.Compression.ZipFile]::Open($apk, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($abi in @('arm64-v8a', 'x86_64')) {
            $entry = $archive.CreateEntry("lib/$abi/libmonosgen-2.0.so").Open()
            try { $entry.WriteByte(0) } finally { $entry.Dispose() }
        }
    } finally { $archive.Dispose() }
    # Synthetic APK metadata fixture: this verifies guard behavior, not a real
    # signature, real APK build or physical-device installation.
    $invoker = {
        param($Tool, $ToolArguments)
        $output = switch ($Tool) {
            signer { 'Signer #1 certificate SHA-256 digest: ' + $(if ($script:failure -eq 'certificate') { 'b' * 64 } else { $certificate }) }
            align { 'aligned' }
            aapt {
                "package: name='$($expectedApp.PackageId)' versionCode='10' versionName='1.2.3'" +
                    $(if ($script:failure -eq 'debuggable') { [Environment]::NewLine + 'application-debuggable' } else { '' })
            }
        }
        if ($script:failure -eq 'package' -and $Tool -eq 'aapt') { $output = "package: name='other.app' versionCode='10' versionName='1.2.3'" }
        if ($script:failure -eq 'version' -and $Tool -eq 'aapt') { $output = "package: name='$($expectedApp.PackageId)' versionCode='9' versionName='1.2.3'" }
        $failed = ($script:failure -eq 'signature' -and $Tool -eq 'signer') -or ($script:failure -eq 'alignment' -and $Tool -eq 'align')
        [pscustomobject]@{ ExitCode = $(if ($failed) { 1 } else { 0 }); Output = $output }
    }
    $verify = @{ Path = $apk; ExpectedApp = $expectedApp; Version = '1.2.3'; VersionCode = 10
        Tools = @{ apksigner = 'signer'; zipalign = 'align'; aapt = 'aapt' }
        ExpectedCertificateSha256 = $certificate; ToolInvoker = $invoker }
    foreach ($failure in @('signature', 'certificate', 'alignment', 'package', 'version', 'debuggable', 'none')) {
        $script:failure = $failure
        Check "final APK guard: $failure" {
            if ($failure -eq 'none') {
                $verification = Confirm-MobileSignedApk @verify
                if (-not $verification.signatureVerified -or $verification.physicalDeviceVerified -or $verification.serverCommunicationVerified) {
                    throw 'Verification status overstates the synthetic fixture.'
                }
            } else { MustReject { Confirm-MobileSignedApk @verify } 'Final APK' }
        }
    }
    Check 'missing native ABI blocks APK handoff' {
        $script:failure = 'none'
        $archive = [IO.Compression.ZipFile]::Open($apk, [IO.Compression.ZipArchiveMode]::Update)
        try { $archive.GetEntry('lib/x86_64/libmonosgen-2.0.so').Delete() } finally { $archive.Dispose() }
        MustReject { Confirm-MobileSignedApk @verify } 'native runtime'
    }
    Check 'isolated deployment bundle binds archive and config hashes' {
        $serverArchive = Join-Path $temporaryRoot 'image.tar'
        $webArchive = Join-Path $temporaryRoot 'web.tar.gz'
        'synthetic-image' | Set-Content -LiteralPath $serverArchive
        'synthetic-web' | Set-Content -LiteralPath $webArchive
        $bundle = Join-Path $temporaryRoot 'bundle'
        & (Join-Path $repoRoot 'deploy/azure-vm/package-mobile-field-test.ps1') -ServerImageArchive $serverArchive -WebArchive $webArchive -OutputDirectory $bundle
        $manifest = Get-Content -LiteralPath (Join-Path $bundle 'deployment-manifest.json') -Raw | ConvertFrom-Json
        if ($manifest.environment -ne 'Staging' -or $manifest.runtimeMode -ne 'Simulation' -or $manifest.automaticCloudDeployment) {
            throw 'Field-test bundle boundary changed.'
        }
        foreach ($line in Get-Content -LiteralPath (Join-Path $bundle 'deployment.sha256')) {
            $hash, $file = $line -split '  ', 2
            if ((Get-FileHash -LiteralPath (Join-Path $bundle $file) -Algorithm SHA256).Hash -ne $hash) { throw 'Bundle bytes are not bound to the manifest.' }
        }
        MustReject {
            & (Join-Path $repoRoot 'deploy/azure-vm/package-mobile-field-test.ps1') -ServerImageArchive $serverArchive -WebArchive $webArchive -OutputDirectory $bundle
        } 'previous deployment evidence'
    }
    if ([string]::IsNullOrWhiteSpace($BashPath)) {
        $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
        $gitBash = if ($programFiles) { Join-Path $programFiles 'Git/bin/bash.exe' } else { $null }
        $BashPath = if ($gitBash -and (Test-Path -LiteralPath $gitBash)) { $gitBash } else { (Get-Command bash -ErrorAction Stop).Source }
    }
    Check 'Bash secret and deployment rollback fixtures' {
        & $BashPath (Join-Path $PSScriptRoot 'test-azure-deployment-rollback.sh').Replace('\', '/')
        if ($LASTEXITCODE -ne 0) { throw 'Offline Bash deployment fixtures failed.' }
    }
    if ($EvidencePath) {
        New-Item -ItemType Directory -Path (Split-Path -Parent ([IO.Path]::GetFullPath($EvidencePath))) -Force | Out-Null
        [pscustomobject]@{ externalServicesUsed = $false; cases = $results } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    }
    Write-Host "Offline PowerShell fixtures passed: $($results.Count)"
} finally {
    # This directory was created above with a fixed prefix and random ID.
    $resolved = [IO.Path]::GetFullPath($temporaryRoot)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolved) -eq $expectedParent -and [IO.Path]::GetFileName($resolved).StartsWith('ssalddel-infra-fixture-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
