# Changed-file discovery and validation selection only.
# Dot-sourced by validate-changes.ps1; caller variables and execution semantics stay in the entry script.

function Add-ChangedFiles {
    param(
        [System.Collections.Generic.List[string]] $Destination,
        [string[]] $GitArguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $lines = @(& git -c core.quotepath=false @GitArguments 2>$null)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "git $($GitArguments -join ' ') failed."
    }

    foreach ($line in $lines) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $Destination.Add(($line.Trim() -replace "\\", "/"))
        }
    }
}

function Get-ChangedFiles {
    if ($null -ne $Paths -and $Paths.Count -gt 0) {
        return @(
            $Paths |
                Where-Object {
                    -not [string]::IsNullOrWhiteSpace($_)
                } |
                ForEach-Object {
                    $_ -split ","
                } |
                ForEach-Object {
                    $_.Trim() -replace "\\", "/"
                } |
                Where-Object {
                    $_ -notmatch "^(artifacts|bin|obj|\.vs)/"
                } |
                Sort-Object -Unique
        )
    }

    $files = New-Object "System.Collections.Generic.List[string]"

    if (-not [string]::IsNullOrWhiteSpace($BaseRef)) {
        Add-ChangedFiles -Destination $files -GitArguments @(
            "diff",
            "--name-only",
            "--diff-filter=ACMRTUXB",
            "$BaseRef...HEAD"
        )
    }

    Add-ChangedFiles -Destination $files -GitArguments @(
        "diff",
        "--name-only",
        "--diff-filter=ACMRTUXB",
        "HEAD"
    )
    Add-ChangedFiles -Destination $files -GitArguments @(
        "ls-files",
        "--others",
        "--exclude-standard"
    )

    $result = @(
        $files |
            Where-Object {
                $_ -notmatch "^(artifacts|bin|obj|\.vs)/"
            } |
            Sort-Object -Unique
    )

    if ($result.Count -eq 0) {
        & git rev-parse --verify "HEAD^" *> $null
        if ($LASTEXITCODE -eq 0) {
            Add-ChangedFiles -Destination $files -GitArguments @(
                "diff",
                "--name-only",
                "--diff-filter=ACMRTUXB",
                "HEAD^",
                "HEAD"
            )
            $result = @(
                $files |
                    Where-Object {
                        $_ -notmatch "^(artifacts|bin|obj|\.vs)/"
                    } |
                    Sort-Object -Unique
            )
        }
    }

    return $result
}

function Test-IsGuidanceOnly {
    param([string[]] $Files)

    if ($Files.Count -eq 0) {
        return $true
    }

    $workFiles = @(
        $Files | Where-Object {
            $_ -notmatch "^(docs/|eng/|\.codex/)" -and
            $_ -notmatch "(^|/)AGENTS\.md$" -and
            $_ -notmatch "\.(md|txt)$" -and
            $_ -notin @(".gitignore", ".gitattributes")
        }
    )

    return $workFiles.Count -eq 0
}

function Get-DirectBuildTargets {
    param([string[]] $Files)

    $targets = New-Object "System.Collections.Generic.HashSet[string]" (
        [System.StringComparer]::OrdinalIgnoreCase
    )

    foreach ($file in $Files) {
        $segments = $file -split "/"
        $topLevel = $segments[0]
        $candidate = Join-Path $repoRoot "$topLevel\$topLevel.csproj"

        if (Test-Path -LiteralPath $candidate) {
            [void] $targets.Add("$topLevel/$topLevel.csproj")
            continue
        }

        if ($file -match "\.slnx?$") {
            [void] $targets.Add($file)
            continue
        }

        if ($file -match "^(Directory\.(Build|Packages)\.(props|targets)|global\.json)$") {
            [void] $targets.Add("Ssalddel.v3.5.slnx")
            [void] $targets.Add("Ssalddel.Simulation.slnx")
            [void] $targets.Add("Ssalddel.Unity.slnx")
        }
    }

    return @($targets | Sort-Object)
}

function Test-IsSimulationPath {
    param([string] $File)

    return $File -match "^(Ssalddel\.Simulation\.|Ssalddel\.WorkflowRules(?:\.|/))"
}

function Test-IsUnityPath {
    param([string] $File)

    return $File -match "^Ssalddel\.Unity(?:\.|/)"
}

function Test-IsCodeMetadataSharedPath {
    param([string] $File)

    return $File -match "^Ssalddel\.CodeMetadata/" -or
        $File -eq "Ssalddel.Contracts/Common/Metadata/SsalddelCodeMetadataAttribute.cs"
}

function Test-RequiresCodeMapCheck {
    param([string[]] $Files)

    return @(
        $Files | Where-Object {
            (Test-IsCodeMetadataSharedPath -File $_) -or
            (Test-IsSimulationPath -File $_) -or
            (Test-IsUnityPath -File $_) -or
            $_ -match "^eng/Ssalddel\.CodeMap/" -or
            $_ -eq "eng/work-areas/simulation-unity.json" -or
            $_ -match "^docs/AI/generated/simulation-unity-code-map\.(json|md)$"
        }
    ).Count -gt 0
}

function Test-RequiresEvidenceMapCheck {
    param([string[]] $Files)

    return @(
        $Files | Where-Object {
            (Test-IsCodeMetadataSharedPath -File $_) -or
            (Test-IsSimulationPath -File $_) -or
            (Test-IsUnityPath -File $_) -or
            $_ -match "^eng/Ssalddel\.EvidenceMap/" -or
            $_ -eq "eng/execution-ledgers/e9-refactor-module-catalog.json" -or
            $_ -match "^docs/AI/generated/evidence-responsibility-code-map\.(json|md)$"
        }
    ).Count -gt 0
}

function Test-RequiresFoodDriverCodeMapCheck {
    param([string[]] $Files)

    $manifestPath = Join-Path $repoRoot "eng/work-areas/food-driver.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($file in $Files) {
        if ((Test-IsCodeMetadataSharedPath -File $file) -or
            $file -match "^eng/Ssalddel\.CodeMap/" -or
            $file -eq "eng/work-areas/food-driver.json" -or
            $file -match "^docs/AI/generated/food-driver-code-map\.(json|md)$" -or
            $file -match "^Ssalddel.Tests/Architecture/FoodDriverCode.*Tests\.cs$") { return $true }
        $segments = @($file -split "/")
        $excluded = @($manifest.excludedRoots | Where-Object {
            $segments -contains $_ -or $file -eq $_ -or $file.StartsWith($_.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        if ($excluded) { continue }
        if (@($manifest.sourceRoots | Where-Object {
            $file -eq $_ -or $file.StartsWith($_.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0) { return $true }
    }
    return $false
}

function Test-IsProductPath {
    param([string] $File)

    if (Test-IsSimulationPath -File $File) { return $false }
    if (Test-IsUnityPath -File $File) { return $false }
    if ($File -match "^(docs/|eng/|\.codex/)" -or $File -match "(^|/)AGENTS\.md$") {
        return $false
    }

    return $true
}

function Get-ProductTaskSlice {
    param([string[]] $Files)

    $requiresFullProductSlice = @(
        $Files | Where-Object {
            $_ -match "^(DriverApp|WarehouseManagerApp|FDriverApp|RestaurantDeskApp|SsalddelRestaurantDesktop|HumanResourcesManagerApp|Ssalddel\.FoodApi)/" -or
            $_ -match "(DomesticTransport|Warehouse|Fulfillment|FoodDelivery|Restaurant|SsalddelMart|Mart)"
        }
    )

    $requiresLaterSlice = @(
        $Files | Where-Object {
            $_ -match "^(OrdererApp|SsalddelAdmin|SsalddelAdminApp|Ssalddel\.BackOffice\.Client)/" -or
            $_ -match "(GroupPurchase|Orderer|Trade|Customs|Import|Export)"
        }
    )

    $sharedChange = @(
        $Files | Where-Object {
            $_ -match "^(Ssalddel\.Contracts|Ssalddel\.Domain|Ssalddel\.Infrastructure|Ssalddel\.Ui\.Common)/"
        }
    )

    if ($requiresFullProductSlice.Count -gt 0 -or $sharedChange.Count -gt 0) {
        return "Ssalddel.v3.5.slnx"
    }

    if ($requiresLaterSlice.Count -gt 0) {
        return "Ssalddel.v1.5.slnx"
    }

    return "Ssalddel.v0.0.slnx"
}

function Get-TaskSlices {
    param([string[]] $Files)

    $targets = New-Object "System.Collections.Generic.HashSet[string]" (
        [System.StringComparer]::OrdinalIgnoreCase
    )

    $globalChange = @(
        $Files | Where-Object {
            $_ -match "^(Directory\.(Build|Packages)\.(props|targets)|global\.json)$" -or
            (Test-IsCodeMetadataSharedPath -File $_)
        }
    ).Count -gt 0

    if ($globalChange -or @($Files | Where-Object { Test-IsSimulationPath -File $_ }).Count -gt 0) {
        [void] $targets.Add("Ssalddel.Simulation.slnx")
    }

    if ($globalChange -or @($Files | Where-Object { Test-IsUnityPath -File $_ }).Count -gt 0) {
        [void] $targets.Add("Ssalddel.Unity.slnx")
    }

    $productFiles = @($Files | Where-Object { Test-IsProductPath -File $_ })
    if ($globalChange) {
        [void] $targets.Add("Ssalddel.v3.5.slnx")
    }
    elseif ($productFiles.Count -gt 0) {
        [void] $targets.Add((Get-ProductTaskSlice -Files $productFiles))
    }

    return @($targets | Sort-Object)
}

function Get-RelatedTestFilter {
    param(
        [string[]] $Files,
        [string] $TestRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        return $TestFilter
    }

    $testClasses = New-Object "System.Collections.Generic.HashSet[string]" (
        [System.StringComparer]::OrdinalIgnoreCase
    )

    foreach ($file in $Files) {
        if ($file -match "^$([regex]::Escape($TestRoot))/.+Tests\.cs$") {
            [void] $testClasses.Add([System.IO.Path]::GetFileNameWithoutExtension($file))
        }
    }

    $rg = Get-Command rg -ErrorAction SilentlyContinue
    if ($null -ne $rg) {
        $terms = @(
            $Files |
                Where-Object {
                    $_ -match "\.(cs|razor)$" -and
                    $_ -notmatch "^$([regex]::Escape($TestRoot))/"
                } |
                ForEach-Object {
                    [System.IO.Path]::GetFileNameWithoutExtension($_)
                } |
                Where-Object {
                    $_.Length -ge 5
                } |
                Sort-Object -Unique |
                Select-Object -First 16
        )

        foreach ($term in $terms) {
            $matches = @(
                & rg -l --fixed-strings --glob "*Tests.cs" --glob "!bin/**" --glob "!obj/**" -- $term $TestRoot 2>$null
            )
            foreach ($match in $matches) {
                [void] $testClasses.Add([System.IO.Path]::GetFileNameWithoutExtension($match))
            }
        }
    }

    $selected = @($testClasses | Sort-Object | Select-Object -First 24)
    if ($selected.Count -eq 0) {
        return $null
    }

    return (($selected | ForEach-Object { "FullyQualifiedName~$_" }) -join "|")
}

function Get-TestProjectDefinitions {
    param([string[]] $Files)

    $definitions = New-Object "System.Collections.Generic.List[object]"
    $globalChange = @(
        $Files | Where-Object {
            $_ -match "^(Directory\.(Build|Packages)\.(props|targets)|global\.json)$" -or
            (Test-IsCodeMetadataSharedPath -File $_)
        }
    ).Count -gt 0

    if ($globalChange -or @($Files | Where-Object { Test-IsSimulationPath -File $_ }).Count -gt 0) {
        $definitions.Add([pscustomobject]@{
            Project = "Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj"
            TestRoot = "Ssalddel.Simulation.Tests"
            Solution = "Ssalddel.Simulation.slnx"
        })
    }

    if ($globalChange -or @($Files | Where-Object { Test-IsUnityPath -File $_ }).Count -gt 0) {
        $definitions.Add([pscustomobject]@{
            Project = "Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj"
            TestRoot = "Ssalddel.Unity.Tests"
            Solution = "Ssalddel.Unity.slnx"
        })
    }

    if ($globalChange -or @($Files | Where-Object { Test-IsProductPath -File $_ }).Count -gt 0) {
        $productSolution = if ($globalChange) {
            "Ssalddel.v3.5.slnx"
        }
        else {
            Get-ProductTaskSlice -Files @(
                $Files | Where-Object { Test-IsProductPath -File $_ }
            )
        }
        $definitions.Add([pscustomobject]@{
            Project = "Ssalddel.Tests/Ssalddel.Tests.csproj"
            TestRoot = "Ssalddel.Tests"
            Solution = $productSolution
        })
    }

    return $definitions.ToArray()
}
