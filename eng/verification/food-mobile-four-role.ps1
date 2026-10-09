[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare','Build','Initialize','Start','Probe')][string]$Action,
    [string]$OutputRoot = 'artifacts/local/food-mobile-four-role-r27/server'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = [IO.Path]::GetFullPath((Join-Path $taskRepo $OutputRoot))
$taskAllowed = [IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts/local')) + [IO.Path]::DirectorySeparatorChar
if (-not $taskOutput.StartsWith($taskAllowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputRoot must be inside repository artifacts/local.' }
$taskProject = 'mirror-r27-food-mobile-check'
$taskCompose = Join-Path $PSScriptRoot 'docker-compose.food-mobile-test.yml'
$taskPrivate = Join-Path $taskOutput '.private'
$taskEnv = Join-Path $taskPrivate '.env'
$taskResultPath = Join-Path $taskOutput 'result.json'
function Save-Result($Value) {
    [IO.File]::WriteAllText($taskResultPath, ($Value | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
}
function Invoke-Compose([string[]]$Arguments, [string]$LogName) {
    & docker compose --project-name $taskProject --env-file $taskEnv -f $taskCompose @Arguments *> (Join-Path $taskPrivate $LogName)
    if ($LASTEXITCODE -ne 0) { throw "Isolated compose operation failed; see private $LogName." }
}
function Get-SourceFingerprint {
    Push-Location $taskRepo
    try {
        $taskPaths = @(& git -c core.quotepath=false ls-files -c -o --exclude-standard | Where-Object {
            $_ -match '^Ssalddel(?:\.[^/]+)?/' -and $_ -match '\.(cs|csproj|json|props|targets)$' -and
            $_ -notmatch '/(?:bin|obj|artifacts)/' -and $_ -notmatch '^Ssalddel\.Tests/'
        }) + @('Ssalddel/Dockerfile','.dockerignore','Directory.Packages.props','Directory.Build.props')
        @($taskPaths | Sort-Object -Unique | ForEach-Object {
            @{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $taskRepo $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
    } finally { Pop-Location }
}
if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $taskEnv) { throw 'Private environment already exists. Use subsequent actions to preserve it.' }
    $taskCollision = @(& docker ps -a --filter "label=com.docker.compose.project=$taskProject" --format '{{.Names}}')
    if ($LASTEXITCODE -ne 0 -or $taskCollision.Count -gt 0) { throw 'Docker inventory unavailable or project already exists.' }
    if (@(Get-NetTCPConnection -LocalPort 5363 -State Listen -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Port 5363 is occupied.' }
    function New-Secret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
    $taskRun = [Guid]::NewGuid().ToString('N')
    New-Item -ItemType Directory -Path $taskPrivate -Force | Out-Null
    $taskCertPath = Join-Path $taskPrivate 'data-protection.pfx'
    $taskValues = [ordered]@{
        FOOD_MOBILE_IMAGE = "$taskProject`:$taskRun"
        FOOD_MOBILE_MYSQL_VOLUME = "$taskProject`_$taskRun`_mysql"
        FOOD_MOBILE_MONGO_VOLUME = "$taskProject`_$taskRun`_mongo"
        FOOD_MOBILE_APP_VOLUME = "$taskProject`_$taskRun`_app"
        FOOD_MOBILE_DB_PASSWORD = (New-Secret)
        FOOD_MOBILE_ROOT_PASSWORD = (New-Secret)
        FOOD_MOBILE_ACCOUNT_PASSWORD = ('Aa1' + (New-Secret))
        FOOD_MOBILE_JWT_KEY = (New-Secret)
        FOOD_MOBILE_AES_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        FOOD_MOBILE_HASH_SALT = (New-Secret)
        FOOD_MOBILE_CERT_PASSWORD = (New-Secret)
        FOOD_MOBILE_CERT_PATH = $taskCertPath.Replace('\','/')
    }
    $taskRsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $taskRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=FoodMobileTest.Local',$taskRsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $taskCertificate = $taskRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddYears(1))
        try { [IO.File]::WriteAllBytes($taskCertPath,$taskCertificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$taskValues.FOOD_MOBILE_CERT_PASSWORD)) }
        finally { $taskCertificate.Dispose() }
    } finally { $taskRsa.Dispose() }
    [IO.File]::WriteAllLines($taskEnv,@($taskValues.GetEnumerator() | ForEach-Object { $_.Key + '=' + $_.Value }),[Text.UTF8Encoding]::new($false))
    $taskAccounts = @{ baseUrl='http://127.0.0.1:5363/'; password=$taskValues.FOOD_MOBILE_ACCOUNT_PASSWORD; accounts=@(
        @{role='orderer';userName='field-orderer'},@{role='restaurant';userName='restaurant101'},
        @{role='food-driver';userName='driver1@ssalddel.local'},@{role='admin';userName='qkrcjfdn79'}) }
    [IO.File]::WriteAllText((Join-Path $taskPrivate 'accounts.json'),($taskAccounts|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    $taskResult = @{ schemaVersion='food-mobile-four-role.r27';runId=$taskRun;status='Prepared';
        baseUrl=$taskAccounts.baseUrl;composeProject=$taskProject;logicalDatabase='ssalddel_food_mobile_test';imageTag=$taskValues.FOOD_MOBILE_IMAGE;
        sourceCommit=(& git -C $taskRepo rev-parse HEAD).Trim();executionMode='Simulation';environment='Staging';
        existingResourcesReused=$false;privateDataRecordedInReport=$false;sourceFingerprint=@();checks=@() }
    Save-Result $taskResult
    Invoke-Compose -Arguments @('config','--quiet') -LogName 'compose-validation.log'
} else {
    if (-not (Test-Path -LiteralPath $taskResultPath) -or -not (Test-Path -LiteralPath $taskEnv)) { throw 'Prepare must complete first.' }
    $taskResult = Get-Content -LiteralPath $taskResultPath -Raw | ConvertFrom-Json -AsHashtable
    if ($taskResult.composeProject -ne $taskProject -or $taskResult.logicalDatabase -ne 'ssalddel_food_mobile_test' -or $taskResult.baseUrl -ne 'http://127.0.0.1:5363/') { throw 'Isolated project ownership mismatch.' }
    switch ($Action) {
        Build {
            $taskBefore = @(Get-SourceFingerprint)
            Push-Location $taskRepo
            try { & docker build --progress=plain -f Ssalddel/Dockerfile -t $taskResult.imageTag . *> (Join-Path $taskOutput 'image-build.log'); $taskExit=$LASTEXITCODE }
            finally { Pop-Location }
            $taskAfter = @(Get-SourceFingerprint)
            $taskStartHashes = @{}; foreach ($entry in $taskBefore) { $taskStartHashes[$entry.path]=$entry.sha256 }
            $taskEndHashes = @{}; foreach ($entry in $taskAfter) { $taskEndHashes[$entry.path]=$entry.sha256 }
            $taskChanged = @($taskAfter | Where-Object { -not $taskStartHashes.ContainsKey($_.path) -or $taskStartHashes[$_.path] -ne $_.sha256 })
            $taskChanged += @($taskBefore | Where-Object { -not $taskEndHashes.ContainsKey($_.path) })
            $taskResult.sourceFingerprint=$taskAfter
            $taskResult.build=@{exitCode=$taskExit;changedDuringBuild=@($taskChanged | ForEach-Object { $_.path });completedAtUtc=[DateTime]::UtcNow.ToString('o')}
            if ($taskExit -eq 0 -and $taskChanged.Count -eq 0) {
                $taskResult.imageId=(& docker image inspect --format '{{.Id}}' $taskResult.imageTag).Trim()
                $taskResult.status='Built'
            } else { $taskResult.status='BuildFailedOrSourcesChanged' }
            Save-Result $taskResult
            if ($taskResult.status -ne 'Built') { throw 'Build or source stability check failed.' }
        }
        Initialize {
            if ($taskResult.status -notin @('Built','Initialized')) { throw 'Stable build required before initialization.' }
            Invoke-Compose -Arguments @('up','-d','--wait','mysql','mongo') -LogName 'databases-start.log'
            Invoke-Compose -Arguments @('run','--rm','--no-deps','app','--initialize-database') -LogName 'database-initialization.log'
            $taskResult.status='Initialized'; Save-Result $taskResult
        }
        Start {
            if ($taskResult.status -notin @('Initialized','Started','Ready')) { throw 'Successful explicit initialization required.' }
            Invoke-Compose -Arguments @('up','-d','--no-build','app','gateway') -LogName 'app-start.log'
            $taskResult.status='Started'; Save-Result $taskResult
        }
        Probe {
            $taskHealth = Invoke-WebRequest ($taskResult.baseUrl+'health/ready') -SkipHttpErrorCheck -TimeoutSec 15
            $taskResult.checks=@($taskResult.checks)+@(@{name='health/ready';status=[int]$taskHealth.StatusCode;atUtc=[DateTime]::UtcNow.ToString('o')})
            $taskResult.status=if($taskHealth.StatusCode -eq 200){'Ready'}else{'ReadinessFailed'}
            Save-Result $taskResult
            if ($taskHealth.StatusCode -ne 200) { throw 'Full persistence readiness did not pass.' }
        }
    }
}
$taskResult | Select-Object status,baseUrl,composeProject,logicalDatabase,imageTag | ConvertTo-Json
