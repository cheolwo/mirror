[CmdletBinding()]
param(
    [string]$ArtifactRoot = 'artifacts/local/food-mobile-four-role-r27',
    [string]$AccountsPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskArtifacts=[IO.Path]::GetFullPath((Join-Path $taskRoot $ArtifactRoot))
if(!$taskArtifacts.StartsWith((Join-Path $taskRoot 'artifacts/local/') ,[StringComparison]::OrdinalIgnoreCase)){throw 'Local ignored evidence directory required'}
if(!$AccountsPath){$AccountsPath=Join-Path $taskArtifacts 'server/.private/accounts.json'}
$taskPrivate=Join-Path $taskArtifacts 'server/.private'
[IO.Directory]::CreateDirectory($taskPrivate)|Out-Null
$taskAccounts=Get-Content $AccountsPath -Raw -Encoding UTF8|ConvertFrom-Json
if($taskAccounts.baseUrl -ne 'http://127.0.0.1:5363/'){throw 'r27 isolated endpoint mismatch'}
$taskBase=$taskAccounts.baseUrl
$taskHealth=Invoke-WebRequest -Uri ($taskBase+'health/ready') -UseBasicParsing
if($taskHealth.StatusCode -ne 200){throw 'Server readiness must pass before UI preparation'}
$taskSessions=@()
$taskHeaders=@{}
foreach($taskAccount in $taskAccounts.accounts){
    $taskLogin=Invoke-RestMethod -Method Post -Uri ($taskBase+'api/v1/auth/login') -ContentType 'application/json' -Body (@{userNameOrEmail=$taskAccount.userName;password=$taskAccounts.password}|ConvertTo-Json)
    if(!$taskLogin.accessToken){throw 'Test login failed'}
    $taskRole=if($taskAccount.role -eq 'admin'){'operator'}else{$taskAccount.role}
    $taskSessions+=@{role=$taskRole;accessToken=$taskLogin.accessToken}
    $taskHeaders[$taskRole]=@{Authorization='Bearer '+$taskLogin.accessToken}
}
foreach($taskRole in @('orderer','restaurant','food-driver','operator')){if(!$taskHeaders.ContainsKey($taskRole)){throw 'All four prepared roles required'}}
$taskRestaurant=Invoke-RestMethod -Uri ($taskBase+'api/v1/orderer/restaurants/101') -Headers $taskHeaders.orderer
if($taskRestaurant.음식점.Id -ne 101 -or @($taskRestaurant.메뉴목록).Count -eq 0){throw 'Scoped restaurant/menu fixture unavailable'}
$taskNotice=Invoke-RestMethod -Uri ($taskBase+'api/v1/common/commerce/notices') -Headers $taskHeaders.orderer
$taskSimulation=$taskNotice.PSObject.Properties.Name -contains 'executionModeCode' -and $taskNotice.executionModeCode -eq 'Simulation'
$taskSeller=$null
try {$taskSeller=Invoke-RestMethod -Uri ($taskBase+'api/v1/common/commerce/restaurants/101/seller/public') -Headers $taskHeaders.orderer}
catch {if([int]$_.Exception.Response.StatusCode -ne 404){throw}}
$taskSellerVerified=$null -ne $taskSeller -and $taskSeller.statusCode -eq 'Verified'
if(!$taskSellerVerified -and !$taskSimulation){throw 'Verified seller required unless the server explicitly declares Simulation'}
$taskWork=Invoke-RestMethod -Uri ($taskBase+'api/v1/driver/food-deliveries/work/status') -Headers $taskHeaders.'food-driver'
if($taskWork.status -ne '운행중'){
    $null=Invoke-RestMethod -Method Post -Uri ($taskBase+'api/v1/driver/food-deliveries/work/start') -Headers $taskHeaders.'food-driver' -ContentType 'application/json' -Body (@{시작모드='바로시작';시작시각=[DateTime]::UtcNow.ToString('o');시작위치='모바일 음식 배달 시험 기사 대기점';커뮤니티운행공개=$false;커뮤니티구단위위치공개동의=$false}|ConvertTo-Json)
}
$null=Invoke-RestMethod -Method Put -Uri ($taskBase+'api/v1/driver/operational-dispatch/availability/intent') -Headers $taskHeaders.'food-driver' -ContentType 'application/json' -Body (@{클라이언트요청Id=[Guid]::NewGuid().ToString();수신의사Code='On'}|ConvertTo-Json)
# 명시된 합성 대기점이다. 측정하지 않은 GPS 정확도를 만들지 않고 fixture heartbeat를 유지한다.
$taskLocation=@{AppKey='FoodDeliveryDriverApp';위도=37.588;경도=127.084;정확도_m=$null;상차접근허용반경Km=5;운행상태='운행중';기록시각=[DateTime]::UtcNow.ToString('o')}
$null=Invoke-RestMethod -Method Post -Uri ($taskBase+'api/v1/driver/food-deliveries/work/location') -Headers $taskHeaders.'food-driver' -ContentType 'application/json' -Body ($taskLocation|ConvertTo-Json)
$taskTokensPath=Join-Path $taskPrivate 'ui-sessions.json'
[IO.File]::WriteAllText($taskTokensPath,(@{baseUrl=$taskBase;accounts=$taskSessions}|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$taskPrepared=@{schemaVersion='food-four-role-ui-preparation.r27';status='Prepared';baseUrl=$taskBase;previewUrl='http://127.0.0.1:5397/';restaurantId=101;menuId=$taskRestaurant.메뉴목록[0].Id;menuName=$taskRestaurant.메뉴목록[0].메뉴명;pickupAddress=$taskRestaurant.음식점.공개주소;recipientAddress='모바일 음식 배달 시험 수령지';synthetic=$true;serverDeclaredSimulation=$taskSimulation;actualSellerVerified=$taskSellerVerified;sellerStatus=if($taskSellerVerified){'Verified'}else{'Unverified'};orderCreated=$false;driverPreparedByApi=$true;actualGpsVerified=$false;credentialsRecorded=$false}
[IO.File]::WriteAllText((Join-Path $taskArtifacts 'food-ui-preparation.json'),($taskPrepared|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output 'r27 accounts, published menu and driver availability prepared. No order was created; Simulation does not confirm actual seller identity.'
