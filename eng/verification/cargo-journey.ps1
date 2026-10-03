[CmdletBinding()]
param(
    [ValidateSet('Prepare','Up','Status','Load','Serve','Stop')][string]$Action = 'Status',
    [ValidateLength(1,48)]
    [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')]
    [string]$SampleSuffix = 'r2'
)
$ErrorActionPreference = 'Stop'
if ($SampleSuffix -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') { throw 'Cargo sample suffix must use lowercase letters, digits and interior hyphens.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts/local/cargo-warehouse-journey-r1'
$environmentRoot = Join-Path $artifactRoot 'environment'
$envFile = Join-Path $environmentRoot '.env'
$selectionFile = Join-Path $environmentRoot 'sample-selection.json'
$readyFile = Join-Path $artifactRoot 'ready.json'
$composeFile = Join-Path $PSScriptRoot 'docker-compose.cargo-journey.yml'
$composeArgs = @('compose','--project-name','ssalddel-cargo-journey-r1','--env-file',$envFile,'-f',$composeFile)
$env:CARGO_JOURNEY_SAMPLE_SUFFIX = $SampleSuffix

if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $envFile -PathType Leaf) {
        Write-Output 'Existing isolated cargo credentials preserved.'
        return
    }
    New-Item -ItemType Directory -Path $environmentRoot -Force | Out-Null
    function New-CargoSecret {
        param([switch]$Base64)
        $bytes = [byte[]]::new(32)
        $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
        if ($Base64) { return [Convert]::ToBase64String($bytes) }
        return [BitConverter]::ToString($bytes).Replace('-', '')
    }
    $lines = @(
        "CARGO_JOURNEY_ACCOUNT_PASSWORD=Cargo1!$(New-CargoSecret)",
        "CARGO_JOURNEY_MYSQL_PASSWORD=$(New-CargoSecret)",
        "CARGO_JOURNEY_MYSQL_ROOT_PASSWORD=$(New-CargoSecret)",
        "CARGO_JOURNEY_JWT_SECRET=$(New-CargoSecret)",
        "CARGO_JOURNEY_AES_KEY=$(New-CargoSecret -Base64)",
        "CARGO_JOURNEY_HASH_SALT=$(New-CargoSecret)",
        "CARGO_JOURNEY_ACCESS_KEY=$(New-CargoSecret)"
    )
    [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
    Write-Output 'Prepared dedicated cargo credentials; values omitted.'
    return
}

if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { throw 'Run -Action Prepare first.' }
$selectedSuffix = 'r2'
if (Test-Path -LiteralPath $selectionFile -PathType Leaf) {
    $selection = Get-Content -LiteralPath $selectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $selectedSuffix = [string]$selection.sampleSuffix
    if ($selectedSuffix -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') { throw 'Saved cargo sample selection is invalid.' }
}
if ($Action -in @('Up','Serve') -and $SampleSuffix -cne $selectedSuffix -and (Test-Path -LiteralPath $readyFile)) {
    throw 'Stop the cargo host and archive ready.json before selecting different dedicated sample volumes.'
}
switch ($Action) {
    'Up' {
        & docker @composeArgs up -d
        if ($LASTEXITCODE) { throw 'Dedicated cargo DB startup failed.' }
        $selection = [ordered]@{
            schemaVersion = 'cargo-journey-sample-selection.r1'
            projectName = 'ssalddel-cargo-journey-r1'
            sampleSuffix = $SampleSuffix
            database = 'ssalddel_cargo_journey'
            mysqlVolume = "ssalddel-cargo-journey-r1_mysql_$SampleSuffix"
            mongoVolume = "ssalddel-cargo-journey-r1_mongo_$SampleSuffix"
            selectedAtUtc = [DateTime]::UtcNow.ToString('O')
        }
        [IO.File]::WriteAllText($selectionFile, ($selection | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        Write-Output "Dedicated cargo sample $SampleSuffix selected; previous volumes preserved."
        return
    }
    'Status' { & docker @composeArgs ps --format json; if ($LASTEXITCODE) { throw 'Cargo DB status failed.' }; return }
    'Stop' { & docker @composeArgs stop; if ($LASTEXITCODE) { throw 'Cargo DB stop failed.' }; return }
}

$requiredNames = @(
    'CARGO_JOURNEY_ACCOUNT_PASSWORD', 'CARGO_JOURNEY_MYSQL_PASSWORD',
    'CARGO_JOURNEY_MYSQL_ROOT_PASSWORD', 'CARGO_JOURNEY_JWT_SECRET',
    'CARGO_JOURNEY_AES_KEY', 'CARGO_JOURNEY_HASH_SALT', 'CARGO_JOURNEY_ACCESS_KEY'
)
$loadedNames = @()
foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
    if ($line -match '^(CARGO_JOURNEY_[A-Z_]+)=(.+)$' -and $requiredNames -contains $Matches[1]) {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
        $loadedNames += $Matches[1]
    }
}
if (@($requiredNames | Where-Object { $loadedNames -notcontains $_ }).Count) { throw 'Cargo credential file is incomplete; existing values were not replaced.' }
if ($Action -eq 'Load') { Write-Output 'Cargo process variables loaded; values omitted.'; return }
if ($SampleSuffix -cne $selectedSuffix) { throw 'Run -Action Up for the selected cargo sample before Serve.' }
$assembly = Join-Path $repoRoot 'eng/Ssalddel.CargoJourneyVerification/bin/Debug/net10.0/Ssalddel.CargoJourneyVerification.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Root must build the cargo harness before Serve.' }
Push-Location $repoRoot
try {
    & dotnet $assembly --serve
    if ($LASTEXITCODE) { throw 'Cargo verification host failed.' }
} finally { Pop-Location }
