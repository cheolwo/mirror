[CmdletBinding()]
param([ValidateSet('Prepare','Up','Status','Load','Serve','Stop')][string]$Action='Status')
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot=Join-Path $repoRoot 'artifacts/local/dispatch-training-r58'
$envFile=Join-Path $artifactRoot 'environment/.env'
$composeFile=Join-Path $PSScriptRoot 'docker-compose.dispatch-training.yml'
if($Action -eq 'Prepare') {
    if(Test-Path -LiteralPath $envFile){ Write-Output 'Existing training credentials preserved.'; return }
    New-Item -ItemType Directory -Path (Split-Path $envFile) -Force | Out-Null
    function New-TrainingSecret([switch]$Base64) {
        $bytes=[byte[]]::new(32)
        [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
        if($Base64){return [Convert]::ToBase64String($bytes)}
        return [Convert]::ToHexString($bytes)
    }
    $lines=@(
        "DISPATCH_TRAINING_ACCOUNT_PASSWORD=Training1!$(New-TrainingSecret)",
        "DISPATCH_TRAINING_MYSQL_PASSWORD=$(New-TrainingSecret)",
        "DISPATCH_TRAINING_MYSQL_ROOT_PASSWORD=$(New-TrainingSecret)",
        "DISPATCH_TRAINING_JWT_SECRET=$(New-TrainingSecret)",
        "DISPATCH_TRAINING_AES_KEY=$(New-TrainingSecret -Base64)",
        "DISPATCH_TRAINING_HASH_SALT=$(New-TrainingSecret)"
    )
    [IO.File]::WriteAllLines($envFile,$lines,[Text.UTF8Encoding]::new($false))
    Write-Output 'Dedicated training credentials prepared; values omitted.'
    return
}
if(-not(Test-Path -LiteralPath $envFile)){throw 'Run -Action Prepare first.'}
$composeArgs=@('compose','--project-name','mirror-dispatch-training-r58','--env-file',$envFile,'-f',$composeFile)
if($Action -in @('Up','Status','Stop')) {
    switch($Action){
        'Up' {& docker @composeArgs up -d}
        'Status' {& docker @composeArgs ps --format json}
        'Stop' {& docker @composeArgs stop}
    }
    if($LASTEXITCODE){throw 'Dedicated training Docker command failed.'}
    return
}
foreach($line in Get-Content -LiteralPath $envFile -Encoding UTF8){
    if($line -match '^(DISPATCH_TRAINING_[A-Z_]+)=(.+)$'){
        [Environment]::SetEnvironmentVariable($Matches[1],$Matches[2],'Process')
    }
}
if($Action -eq 'Load'){Write-Output 'Training process variables loaded; values omitted.';return}
$assembly=Join-Path $repoRoot 'eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll'
if(-not(Test-Path -LiteralPath $assembly)){throw 'Build the training host before Serve.'}
Push-Location $repoRoot
try {& dotnet $assembly --serve; if($LASTEXITCODE){throw 'Training host failed.'}}
finally {Pop-Location}
