[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Prepare','Serve','InspectSchema')][string]$Action,
 [string]$OutputRoot,
 [ValidateSet('CurrentModel','MigrationFirst')][string]$SchemaMode='CurrentModel',
 [switch]$ConfirmFreshEmptySchema
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskSource=Join-Path $taskRepo 'artifacts/local/usb-device-verification-r74/.private/environment.json'
if(!(Test-Path -LiteralPath $taskSource -PathType Leaf)){throw 'ExistingR74PrivateDatabaseAccessRequired'}
$taskOriginal=Get-Content -LiteralPath $taskSource -Raw | ConvertFrom-Json -AsHashtable
if($Action -eq 'Prepare'){
 if($OutputRoot){throw 'PrepareUsesFreshGuidDirectoryOnly'}
 if($ConfirmFreshEmptySchema){throw 'R108ConfirmationIsServeOnly'}
 $taskRun=[Guid]::NewGuid().ToString('N')
 $OutputRoot=Join-Path $taskRepo ('artifacts/local/integrated-workflow-r108/'+$taskRun)
 New-Item -ItemType Directory -Path (Join-Path $OutputRoot '.private') | Out-Null
 $taskDatabasePrefix=if($SchemaMode -eq 'MigrationFirst'){'r108m_'}else{'mirror_workflow_r108_'}
 $taskEnv=@{R108_DATABASE=$taskDatabasePrefix+$taskRun;R108_ARTIFACT_ROOT=$OutputRoot;R108_SCHEMA_MODE=$SchemaMode}
 foreach($name in @('USB_R74_SQL_PASSWORD','USB_R74_SQL_ROOT_PASSWORD')){$taskEnv[$name]=$taskOriginal[$name]}
 foreach($name in @('USB_R74_ACCOUNT_PASSWORD','USB_R74_JWT_SECRET','USB_R74_HASH_SALT','USB_R74_ACCESS_KEY')){
  $taskEnv[$name]='Aa1'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
 }
 $taskEnv['USB_R74_AES_KEY']=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
 $taskSql='CREATE DATABASE `'+$taskEnv.R108_DATABASE+'` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; GRANT ALL PRIVILEGES ON `'+$taskEnv.R108_DATABASE+'`.* TO ''usb_r74''@''%'';'
 # Secret exists only in the docker client environment and process memory, never command text/output.
 $taskSavedPwd=$env:MYSQL_PWD
 try {
  $env:MYSQL_PWD=$taskEnv.USB_R74_SQL_ROOT_PASSWORD
  & docker exec -e MYSQL_PWD mirror-usb-r74-mysql-1 mysql -uroot -e $taskSql *> (Join-Path $OutputRoot '.private/database-create.log')
  if($LASTEXITCODE -ne 0){throw 'R108FreshDatabaseCreateFailed'}
 }finally{$env:MYSQL_PWD=$taskSavedPwd}
 [IO.File]::WriteAllText((Join-Path $OutputRoot '.private/environment.json'),($taskEnv|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
 $taskPreparation=@{
  schemaVersion='integrated-workflow-r108.preparation.r1';status='FreshDedicatedDatabasePrepared'
  database=$taskEnv.R108_DATABASE;outputRoot=$OutputRoot;runStableId='integrated-r108:'+$taskRun
  schemaMode=$SchemaMode;preparedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
  existingDatabaseChanged=$false;buildPerformed=$false
 }
 [IO.File]::WriteAllText((Join-Path $OutputRoot 'preparation.json'),($taskPreparation|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
 $taskPreparation|ConvertTo-Json
 return
}
$taskAllowed=[IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts/local/integrated-workflow-r108'))+[IO.Path]::DirectorySeparatorChar
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(!$OutputRoot.StartsWith($taskAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'R108OutputBoundaryMismatch'}
if(@(Get-NetTCPConnection -LocalPort 5378 -State Listen -ErrorAction SilentlyContinue).Count -gt 0){throw 'R108PortOccupiedPreserveExistingHost'}
$taskEnv=Get-Content -LiteralPath (Join-Path $OutputRoot '.private/environment.json') -Raw | ConvertFrom-Json -AsHashtable
 $taskPreparedMode=if($taskEnv.ContainsKey('R108_SCHEMA_MODE')){$taskEnv.R108_SCHEMA_MODE}else{'CurrentModel'}
 if($taskPreparedMode -notin @('CurrentModel','MigrationFirst')){throw 'R108PreparedSchemaModeInvalid'}
 $taskDatabaseMatch=[regex]::Match($taskEnv.R108_DATABASE,'^(?<prefix>mirror_workflow_r108_|r108m_)(?<id>[a-f0-9]{32})$')
 if($taskEnv.R108_ARTIFACT_ROOT -ne $OutputRoot -or !$taskDatabaseMatch.Success){throw 'R108EnvironmentMismatch'}
 $taskShortName=$taskDatabaseMatch.Groups['prefix'].Value -eq 'r108m_'
 if($taskShortName -and $taskPreparedMode -ne 'MigrationFirst'){throw 'R108ShortDatabaseRequiresMigrationFirst'}
 if(!$taskShortName -and $taskPreparedMode -eq 'MigrationFirst' -and $Action -ne 'InspectSchema'){throw 'R108LegacyMigrationDatabaseIsReadOnly'}
 $taskExpectedRoot=Join-Path $taskAllowed $taskDatabaseMatch.Groups['id'].Value
 if(![string]::Equals($OutputRoot,[IO.Path]::GetFullPath($taskExpectedRoot),[StringComparison]::OrdinalIgnoreCase)){throw 'R108ExactGuidOutputBoundaryRequired'}
 if($PSBoundParameters.ContainsKey('SchemaMode') -and $SchemaMode -ne $taskPreparedMode){throw 'R108PreparedSchemaModeCannotChange'}
 if($ConfirmFreshEmptySchema -and ($Action -ne 'Serve' -or $taskPreparedMode -ne 'MigrationFirst')){throw 'R108ConfirmationRequiresMigrationFirstServe'}
$taskDll=Join-Path $OutputRoot 'host/bin/Ssalddel.IntegratedWorkflowR108.dll'
if(Test-Path -LiteralPath (Join-Path $OutputRoot 'host/bin-final/Ssalddel.IntegratedWorkflowR108.dll')){
 $taskDll=Join-Path $OutputRoot 'host/bin-final/Ssalddel.IntegratedWorkflowR108.dll'
}
if(Test-Path -LiteralPath (Join-Path $OutputRoot 'host/bin-r108-final/Ssalddel.IntegratedWorkflowR108.dll')){
 $taskDll=Join-Path $OutputRoot 'host/bin-r108-final/Ssalddel.IntegratedWorkflowR108.dll'
}
if(!(Test-Path -LiteralPath $taskDll -PathType Leaf)){throw 'RootCoordinatedBuildRequired'}
$taskSaved=@{}
try {
 foreach($name in $taskEnv.Keys){$taskSaved[$name]=[Environment]::GetEnvironmentVariable($name,'Process');[Environment]::SetEnvironmentVariable($name,$taskEnv[$name],'Process')}
 foreach($name in @('R108_SCHEMA_MODE','R108_CONFIRM_FRESH_EMPTY_SCHEMA')){
  if(!$taskSaved.ContainsKey($name)){$taskSaved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
 }
 $env:R108_SCHEMA_MODE=$taskPreparedMode
 $env:R108_CONFIRM_FRESH_EMPTY_SCHEMA=if($ConfirmFreshEmptySchema){'1'}else{'0'}
 Push-Location $taskRepo
 try {
  $taskArgument=if($Action -eq 'InspectSchema'){'--inspect-schema'}else{'--serve'}
  & dotnet $taskDll $taskArgument
  if($LASTEXITCODE -ne 0){throw 'R108HostFailedSeeSafeEvidence'}
 } finally {Pop-Location}
}finally{foreach($name in $taskSaved.Keys){[Environment]::SetEnvironmentVariable($name,$taskSaved[$name],'Process')}}
