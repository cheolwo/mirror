#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ExportCondition {
    param(
        [bool]$Condition,
        [string]$ErrorCode
    )
    if (-not $Condition) { throw $ErrorCode }
}

function Get-SafeEnvironmentValue {
    param(
        [string]$Path,
        [string]$Name
    )
    $prefix = $Name + '='
    $matches = @(Get-Content -LiteralPath $Path -Encoding UTF8 |
        Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) })
    Assert-ExportCondition ($matches.Count -eq 1) 'RestaurantIconExportEnvironmentInvalid'
    return $matches[0].Substring($prefix.Length)
}

function ConvertTo-CategoryCode {
    param([string]$SourceIndustry)

    Assert-ExportCondition (-not [string]::IsNullOrWhiteSpace($SourceIndustry)) `
        'RestaurantIconExportSourceIndustryMissing'
    if ($SourceIndustry -match '카페|커피|제과|제빵|빵|도넛|빙수|아이스크림') { return 'CafeBakery' }
    if ($SourceIndustry -match '김밥|분식|만두|떡볶이') { return 'Snack' }
    if ($SourceIndustry -match '치킨') { return 'Chicken' }
    if ($SourceIndustry -match '닭|오리|고기|구이|바비큐') { return 'Grill' }
    if ($SourceIndustry -match '국|탕|찌개|해장국') { return 'Soup' }
    if ($SourceIndustry -match '호프|주점|바') { return 'Pub' }
    if ($SourceIndustry -match '한식') { return 'Korean' }
    return 'Other'
}

function Test-ZeroFlag {
    param([object]$Value)
    return [int]$Value -eq 0
}

function Test-OneFlag {
    param([object]$Value)
    return [int]$Value -eq 1
}

function Get-Sha256Text {
    param([string]$Value)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-BindingHash {
    param(
        [object]$Binding,
        [string]$ObjectStableId,
        [string]$MenuId
    )
    $canonical = @(
        [string]$Binding.fixturePackStableId,
        $ObjectStableId,
        [string]$Binding.objectKindCode,
        [string]$Binding.restaurantProfileId,
        $MenuId,
        [string]$Binding.publicBusinessObservationStableId,
        [string]$Binding.locationAnchorStableId,
        [string]$Binding.buildingStableId,
        [string]$Binding.semanticPlaceStableId,
        [string]$Binding.bindingPurposeCode,
        [string]$Binding.affiliationCode,
        [string]$Binding.displayDisclosureCode,
        $(if (Test-OneFlag $Binding.scenarioOrderAllowed) { 'true' } else { 'false' }),
        $(if (Test-OneFlag $Binding.actualOrderAllowed) { 'true' } else { 'false' }),
        $(if (Test-OneFlag $Binding.distributionApproved) { 'true' } else { 'false' }),
        [string]$Binding.reviewStatusCode,
        ([long]$Binding.revision).ToString([Globalization.CultureInfo]::InvariantCulture)
    ) -join '|'
    return Get-Sha256Text $canonical
}

function Invoke-IsolatedMysqlJson {
    param(
        [string]$Sql,
        [string[]]$ComposeArguments
    )

    $dockerArguments = @($ComposeArguments) + @(
        'exec', '-T', 'mysql', 'sh', '-c',
        'exec env MYSQL_PWD="$MYSQL_PASSWORD" mysql --user="$MYSQL_USER" "$MYSQL_DATABASE" --batch --raw --silent --skip-column-names --default-character-set=utf8mb4'
    )
    $previousOutputEncoding = $OutputEncoding
    try {
        $OutputEncoding = [Text.UTF8Encoding]::new($false)
        $raw = @($Sql | & docker @dockerArguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $OutputEncoding = $previousOutputEncoding
    }
    Assert-ExportCondition ($exitCode -eq 0) 'RestaurantIconExportDatabaseReadFailed'
    $lines = @($raw | ForEach-Object { [string]$_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-ExportCondition ($lines.Count -eq 1) 'RestaurantIconExportDatabaseResultInvalid'
    try {
        return $lines[0] | ConvertFrom-Json
    }
    catch {
        throw 'RestaurantIconExportDatabaseJsonInvalid'
    }
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts/local/verification/observable-operations'
$environmentFile = Join-Path $artifactRoot '.env'
$connectionFile = Join-Path $artifactRoot 'connection.json'
$composeFile = Join-Path $PSScriptRoot 'docker-compose.observable-operations.yml'
$directoryPath = Join-Path $repoRoot `
    'artifacts/local/public-data/sagajeong-restaurant-directory-20260913-r1/restaurant-directory.v1.json'

Assert-ExportCondition (Test-Path -LiteralPath $environmentFile -PathType Leaf) `
    'RestaurantIconExportEnvironmentMissing'
Assert-ExportCondition (Test-Path -LiteralPath $connectionFile -PathType Leaf) `
    'RestaurantIconExportConnectionMissing'
Assert-ExportCondition (Test-Path -LiteralPath $composeFile -PathType Leaf) `
    'RestaurantIconExportComposeMissing'
Assert-ExportCondition (Test-Path -LiteralPath $directoryPath -PathType Leaf) `
    'RestaurantIconExportDirectoryMissing'

$connection = Get-Content -LiteralPath $connectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-ExportCondition ($connection.baseUrl -ceq 'http://127.0.0.1:53216') `
    'RestaurantIconExportLoopbackRequired'

$configuredDirectoryPath = Get-SafeEnvironmentValue $environmentFile `
    'OBSERVABLE_OPERATIONS_RESTAURANT_DIRECTORY_PATH'
$configuredDirectoryHash = Get-SafeEnvironmentValue $environmentFile `
    'OBSERVABLE_OPERATIONS_RESTAURANT_DIRECTORY_SHA256'
$configuredDirectoryRevision = Get-SafeEnvironmentValue $environmentFile `
    'OBSERVABLE_OPERATIONS_RESTAURANT_DIRECTORY_REVISION'
$configuredDirectoryFullPath = [IO.Path]::GetFullPath(
    $configuredDirectoryPath.Replace('/', [IO.Path]::DirectorySeparatorChar))
Assert-ExportCondition ($configuredDirectoryFullPath -ceq [IO.Path]::GetFullPath($directoryPath)) `
    'RestaurantIconExportDirectoryPathMismatch'

$directoryHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $directoryPath).Hash.ToLowerInvariant()
Assert-ExportCondition ($configuredDirectoryHash -ceq $directoryHash) `
    'RestaurantIconExportDirectoryHashMismatch'
$directory = Get-Content -LiteralPath $directoryPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-ExportCondition ($directory.schemaVersion -ceq 'sagajeong-restaurant-directory.v1') `
    'RestaurantIconExportDirectorySchemaInvalid'
Assert-ExportCondition ($directory.dataRevision -ceq $configuredDirectoryRevision) `
    'RestaurantIconExportDirectoryRevisionMismatch'
Assert-ExportCondition ($directory.projectionKind -ceq 'DerivedProjectionFromStoredObservation') `
    'RestaurantIconExportDirectoryProjectionInvalid'
Assert-ExportCondition ($directory.scope.kind -ceq 'StationCenteredUnitySquare' -and
    $directory.scope.worldRegionStableId -ceq 'world-region:kr:seoul:jungnang:sagajeong.r1' -and
    $directory.scope.legalAreaStableId -ceq 'region:kr:bjd:1126010100' -and
    $directory.scope.coordinateSpace -ceq 'SagajeongReference.UnityXZ' -and
    [double]$directory.scope.centerX -eq 550d -and [double]$directory.scope.centerZ -eq 8d -and
    [double]$directory.scope.minX -eq 50d -and [double]$directory.scope.maxX -eq 1050d -and
    [double]$directory.scope.minZ -eq -492d -and [double]$directory.scope.maxZ -eq 508d) `
    'RestaurantIconExportDirectoryScopeInvalid'
Assert-ExportCondition ($directory.readiness.displayReviewStatus -ceq 'PendingHumanReview' -and
    $directory.readiness.orderParticipationStatus -ceq 'Disabled' -and
    -not [bool]$directory.readiness.publicDisplayEnabled -and
    -not [bool]$directory.readiness.distributionApproved -and
    -not [bool]$directory.readiness.orderScenarioEligible) `
    'RestaurantIconExportDirectoryReadinessInvalid'

$directoryByStableId = [Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::Ordinal)
foreach ($restaurant in @($directory.restaurants)) {
    $stableId = [string]$restaurant.directoryStableId
    Assert-ExportCondition (-not [string]::IsNullOrWhiteSpace($stableId) -and
        -not $directoryByStableId.ContainsKey($stableId)) `
        'RestaurantIconExportDirectoryStableIdInvalid'
    $directoryByStableId.Add($stableId, $restaurant)
}

$composeArguments = @(
    'compose', '--project-name', 'ssalddel-observable-operations',
    '--env-file', $environmentFile, '-f', $composeFile
)
$runningMysql = @(& docker @composeArguments ps --status running --quiet mysql 2>$null)
Assert-ExportCondition ($LASTEXITCODE -eq 0 -and $runningMysql.Count -eq 1 -and
    -not [string]::IsNullOrWhiteSpace([string]$runningMysql[0])) `
    'RestaurantIconExportIsolatedDatabaseNotRunning'

# SQL은 실제 상호·주소·전화·위경도를 선택하지 않는다. 프로필의 비공개 조건은 boolean으로만 읽는다.
$sql = @'
SET SESSION TRANSACTION ISOLATION LEVEL REPEATABLE READ;
SET TRANSACTION READ ONLY;
START TRANSACTION WITH CONSISTENT SNAPSHOT;
SET @fixture_pack_id := (
  SELECT `FixturePackStableId`
  FROM `관찰운영검증_Fixture묶음`
  ORDER BY `CreatedAtUtc` DESC, `FixturePackStableId` DESC
  LIMIT 1
);
SELECT JSON_OBJECT(
  'pack', (
    SELECT JSON_OBJECT(
      'fixturePackStableId', `FixturePackStableId`,
      'fixtureRevision', `FixtureRevision`,
      'fixtureHashSha256', `FixtureHashSha256`,
      'inputHashSha256', `InputHashSha256`,
      'sourceRevision', `SourceRevision`,
      'deterministicSeed', `DeterministicSeed`,
      'sourceKindCode', `SourceKindCode`,
      'environmentCode', `EnvironmentCode`,
      'distributionApproved', `DistributionApproved`,
      'operationalEffectsAllowed', `OperationalEffectsAllowed`,
      'generatorVersion', `GeneratorVersion`,
      'createdAtUtc', DATE_FORMAT(`CreatedAtUtc`, '%Y-%m-%dT%H:%i:%s.%fZ'),
      'expiresAtUtc', DATE_FORMAT(`ExpiresAtUtc`, '%Y-%m-%dT%H:%i:%s.%fZ')
    )
    FROM `관찰운영검증_Fixture묶음`
    WHERE `FixturePackStableId` = @fixture_pack_id
  ),
  'profiles', COALESCE((
    SELECT JSON_ARRAYAGG(JSON_OBJECT(
      'fixturePackStableId', b.`FixturePackStableId`,
      'profileStableId', b.`FixtureObjectStableId`,
      'objectKindCode', b.`ObjectKindCode`,
      'restaurantProfileId', CAST(b.`RestaurantProfileId` AS CHAR),
      'publicBusinessObservationStableId', b.`PublicBusinessObservationStableId`,
      'locationAnchorStableId', b.`LocationAnchorStableId`,
      'buildingStableId', b.`BuildingStableId`,
      'semanticPlaceStableId', b.`SemanticPlaceStableId`,
      'bindingPurposeCode', b.`BindingPurposeCode`,
      'affiliationCode', b.`AffiliationCode`,
      'displayDisclosureCode', b.`DisplayDisclosureCode`,
      'scenarioOrderAllowed', b.`ScenarioOrderAllowed`,
      'actualOrderAllowed', b.`ActualOrderAllowed`,
      'distributionApproved', b.`DistributionApproved`,
      'reviewStatusCode', b.`ReviewStatusCode`,
      'revision', b.`Revision`,
      'bindingHashSha256', b.`BindingHashSha256`,
      'profileCategory', p.`카테고리`,
      'sampleNameValid', IF(p.`상호명` = CONCAT('[샘플] 사가정 음식점 ', RIGHT(b.`SemanticPlaceStableId`, 2)), 1, 0),
      'sampleDescriptionValid', IF(p.`소개` = '샘플 메뉴 · 실제 판매 메뉴 아님 · 해당 업체와 제휴 관계 없음 · 로컬 검토용/비배포', 1, 0),
      'expectedCookingMinutes', p.`예상조리분`,
      'privateAddressEmpty', IF(p.`공개주소` = '', 1, 0),
      'privateCoordinatesZero', IF(p.`위도` = 0 AND p.`경도` = 0, 1, 0),
      'businessReferenceEmpty', IF(p.`업체_id` IS NULL, 1, 0),
      'profileImageEmpty', IF(p.`대표이미지_url` IS NULL, 1, 0),
      'minimumOrderZero', IF(p.`최소주문금액` = 0, 1, 0),
      'publicDisplayDisabled', IF(p.`공개여부` = 0, 1, 0),
      'actualOrderingDisabled', IF(p.`주문가능여부` = 0, 1, 0)
    ))
    FROM `관찰운영검증_Fixture결속` b
    INNER JOIN `음식점공개프로필` p ON p.`id` = b.`RestaurantProfileId`
    WHERE b.`FixturePackStableId` = @fixture_pack_id
      AND b.`ObjectKindCode` = 'RestaurantProfile'
  ), JSON_ARRAY()),
  'menus', COALESCE((
    SELECT JSON_ARRAYAGG(JSON_OBJECT(
      'fixturePackStableId', b.`FixturePackStableId`,
      'menuStableId', b.`FixtureObjectStableId`,
      'objectKindCode', b.`ObjectKindCode`,
      'restaurantProfileId', CAST(b.`RestaurantProfileId` AS CHAR),
      'menuId', CAST(b.`MenuId` AS CHAR),
      'publicBusinessObservationStableId', b.`PublicBusinessObservationStableId`,
      'locationAnchorStableId', b.`LocationAnchorStableId`,
      'buildingStableId', b.`BuildingStableId`,
      'semanticPlaceStableId', b.`SemanticPlaceStableId`,
      'bindingPurposeCode', b.`BindingPurposeCode`,
      'affiliationCode', b.`AffiliationCode`,
      'displayDisclosureCode', b.`DisplayDisclosureCode`,
      'scenarioOrderAllowed', b.`ScenarioOrderAllowed`,
      'actualOrderAllowed', b.`ActualOrderAllowed`,
      'distributionApproved', b.`DistributionApproved`,
      'reviewStatusCode', b.`ReviewStatusCode`,
      'revision', b.`Revision`,
      'bindingHashSha256', b.`BindingHashSha256`,
      'menuName', m.`메뉴명`,
      'price', CAST(m.`판매가` AS CHAR),
      'displayOrder', m.`표시순서`,
      'sampleDescriptionValid', IF(m.`설명` = 'SyntheticValue=true; 실제 판매 메뉴·가격이 아닌 로컬 생명주기 검증값', 1, 0),
      'menuImageEmpty', IF(m.`대표이미지_url` IS NULL, 1, 0),
      'publicDisplayDisabled', IF(m.`공개여부` = 0, 1, 0),
      'soldOutDisabled', IF(m.`품절여부` = 0, 1, 0)
    ))
    FROM `관찰운영검증_Fixture결속` b
    INNER JOIN `음식점메뉴` m ON m.`id` = b.`MenuId`
    WHERE b.`FixturePackStableId` = @fixture_pack_id
      AND b.`ObjectKindCode` = 'RestaurantMenu'
  ), JSON_ARRAY())
);
COMMIT;
'@

$snapshot = Invoke-IsolatedMysqlJson $sql $composeArguments
$pack = $snapshot.pack
Assert-ExportCondition ($null -ne $pack) 'RestaurantIconExportFixturePackMissing'
Assert-ExportCondition ($pack.fixtureRevision -ceq 'sagajeong-restaurant-fixture.v2' -and
    $pack.inputHashSha256 -ceq $directoryHash -and
    $pack.sourceRevision -ceq [string]$directory.dataRevision -and
    $pack.deterministicSeed -ceq 'sagajeong-sample-menu-seed-20260913-r1' -and
    $pack.sourceKindCode -ceq 'SyntheticFixture' -and
    $pack.environmentCode -ceq 'DevelopmentSimulation' -and
    (Test-ZeroFlag $pack.distributionApproved) -and
    (Test-ZeroFlag $pack.operationalEffectsAllowed) -and
    $pack.generatorVersion -ceq 'observable-operations-fixture.v2' -and
    [string]$pack.fixtureHashSha256 -cmatch '^[0-9a-f]{64}$' -and
    $pack.fixturePackStableId -ceq ('fixture-pack:sagajeong-restaurants:' +
        ([string]$pack.fixtureHashSha256).Substring(0, 16))) `
    'RestaurantIconExportFixturePackPolicyInvalid'
$createdAtUtc = [DateTimeOffset]::Parse([string]$pack.createdAtUtc,
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$expiresAtUtc = [DateTimeOffset]::Parse([string]$pack.expiresAtUtc,
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
Assert-ExportCondition ($createdAtUtc -le [DateTimeOffset]::UtcNow -and
    $expiresAtUtc -gt [DateTimeOffset]::UtcNow -and
    $expiresAtUtc -eq $createdAtUtc.AddDays(30)) `
    'RestaurantIconExportFixturePackLifetimeInvalid'

$profiles = @($snapshot.profiles)
$menus = @($snapshot.menus)
Assert-ExportCondition ($profiles.Count -eq 12) 'RestaurantIconExportProfileCountInvalid'
Assert-ExportCondition ($menus.Count -ge 24 -and $menus.Count -le 36) `
    'RestaurantIconExportMenuCountInvalid'

$profileById = [Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::Ordinal)
$profileStableIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$outputEntries = @()
foreach ($profile in $profiles) {
    $profileId = [string]$profile.restaurantProfileId
    $profileStableId = [string]$profile.profileStableId
    Assert-ExportCondition (-not [string]::IsNullOrWhiteSpace($profileId) -and
        -not $profileById.ContainsKey($profileId) -and
        $profileStableId -cmatch '^[A-Za-z0-9:_.-]{1,128}$' -and
        -not $profileStableIds.Contains($profileStableId)) `
        'RestaurantIconExportProfileIdentityInvalid'
    Assert-ExportCondition ($profile.fixturePackStableId -ceq $pack.fixturePackStableId -and
        $profile.objectKindCode -ceq 'RestaurantProfile' -and
        $profile.bindingPurposeCode -ceq 'LocationPresentationAnchorOnly' -and
        $profile.affiliationCode -ceq 'NoBusinessAffiliation' -and
        $profile.displayDisclosureCode -ceq 'SampleMenuNotActualOffering' -and
        (Test-OneFlag $profile.scenarioOrderAllowed) -and
        (Test-ZeroFlag $profile.actualOrderAllowed) -and
        (Test-ZeroFlag $profile.distributionApproved) -and
        $profile.reviewStatusCode -ceq 'PendingHumanReview' -and
        [long]$profile.revision -eq 1L -and
        [string]$profile.bindingHashSha256 -cmatch '^[0-9a-f]{64}$' -and
        $profile.bindingHashSha256 -ceq (Get-BindingHash $profile $profileStableId '')) `
        'RestaurantIconExportProfileBindingPolicyInvalid'
    Assert-ExportCondition ((Test-OneFlag $profile.sampleNameValid) -and
        (Test-OneFlag $profile.sampleDescriptionValid) -and
        (Test-OneFlag $profile.privateAddressEmpty) -and
        (Test-OneFlag $profile.privateCoordinatesZero) -and
        (Test-OneFlag $profile.businessReferenceEmpty) -and
        (Test-OneFlag $profile.profileImageEmpty) -and
        (Test-OneFlag $profile.minimumOrderZero) -and
        (Test-OneFlag $profile.publicDisplayDisabled) -and
        (Test-OneFlag $profile.actualOrderingDisabled)) `
        'RestaurantIconExportProfilePrivacyInvalid'

    $anchorStableId = [string]$profile.locationAnchorStableId
    Assert-ExportCondition ($directoryByStableId.ContainsKey($anchorStableId)) `
        'RestaurantIconExportLocationAnchorMissing'
    $source = $directoryByStableId[$anchorStableId]
    $distance = [Math]::Sqrt([Math]::Pow([double]$source.unityX - 550d, 2) +
        [Math]::Pow([double]$source.unityZ - 8d, 2))
    Assert-ExportCondition ($source.parentStableId -ceq $profile.publicBusinessObservationStableId -and
        $source.buildingCandidateStableId -ceq $profile.buildingStableId -and
        ([string]$source.sourceIndustry).Trim() -ceq $profile.profileCategory -and
        $source.displayReviewStatus -ceq 'PendingHumanReview' -and
        $source.orderParticipationStatus -ceq 'Disabled' -and
        -not [bool]$source.distributionApproved -and
        -not [bool]$source.orderScenarioEligible -and
        $source.dataRevision -ceq [string]$directory.dataRevision -and
        $source.projectionKind -ceq 'DerivedProjectionFromStoredObservation' -and
        -not [string]::IsNullOrWhiteSpace([string]$source.buildingCandidateStableId) -and
        $distance -le 250d) `
        'RestaurantIconExportLocationAnchorPolicyInvalid'
    Assert-ExportCondition ([double]$source.unityX -ge 50d -and [double]$source.unityX -le 1050d -and
        [double]$source.unityZ -ge -492d -and [double]$source.unityZ -le 508d) `
        'RestaurantIconExportCoordinateInvalid'

    $profileById.Add($profileId, $profile)
    $null = $profileStableIds.Add($profileStableId)
    $outputEntries += [ordered]@{
        stableId = $profileStableId
        categoryCode = ConvertTo-CategoryCode ([string]$source.sourceIndustry)
        unityX = [double]$source.unityX
        unityZ = [double]$source.unityZ
        sampleMenuDisplayNames = @()
    }
}

$menusByProfile = @{}
$menuStableIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$menuIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($menu in $menus) {
    $profileId = [string]$menu.restaurantProfileId
    $menuId = [string]$menu.menuId
    $menuStableId = [string]$menu.menuStableId
    Assert-ExportCondition ($profileById.ContainsKey($profileId) -and
        -not [string]::IsNullOrWhiteSpace($menuId) -and
        -not $menuIds.Contains($menuId) -and
        $menuStableId -cmatch '^[A-Za-z0-9:_.-]{1,196}$' -and
        -not $menuStableIds.Contains($menuStableId)) `
        'RestaurantIconExportMenuIdentityInvalid'
    $profile = $profileById[$profileId]
    Assert-ExportCondition ($menu.fixturePackStableId -ceq $pack.fixturePackStableId -and
        $menu.objectKindCode -ceq 'RestaurantMenu' -and
        $menuStableId.StartsWith(([string]$profile.profileStableId + ':menu:'), [StringComparison]::Ordinal) -and
        $menu.publicBusinessObservationStableId -ceq $profile.publicBusinessObservationStableId -and
        $menu.locationAnchorStableId -ceq $profile.locationAnchorStableId -and
        $menu.buildingStableId -ceq $profile.buildingStableId -and
        $menu.semanticPlaceStableId -ceq $profile.semanticPlaceStableId -and
        $menu.bindingPurposeCode -ceq 'LocationPresentationAnchorOnly' -and
        $menu.affiliationCode -ceq 'NoBusinessAffiliation' -and
        $menu.displayDisclosureCode -ceq 'SampleMenuNotActualOffering' -and
        (Test-OneFlag $menu.scenarioOrderAllowed) -and
        (Test-ZeroFlag $menu.actualOrderAllowed) -and
        (Test-ZeroFlag $menu.distributionApproved) -and
        $menu.reviewStatusCode -ceq 'PendingHumanReview' -and
        [long]$menu.revision -eq 1L -and
        [string]$menu.bindingHashSha256 -cmatch '^[0-9a-f]{64}$' -and
        $menu.bindingHashSha256 -ceq (Get-BindingHash $menu $menuStableId $menuId)) `
        'RestaurantIconExportMenuBindingPolicyInvalid'
    $menuName = [string]$menu.menuName
    Assert-ExportCondition ($menuName.StartsWith('[샘플] ', [StringComparison]::Ordinal) -and
        $menuName.Length -le 48 -and $menuName.IndexOfAny([char[]]@("`r", "`n")) -lt 0 -and
        [int]$menu.displayOrder -gt 0 -and
        (Test-OneFlag $menu.sampleDescriptionValid) -and
        (Test-OneFlag $menu.menuImageEmpty) -and
        (Test-OneFlag $menu.publicDisplayDisabled) -and
        (Test-OneFlag $menu.soldOutDisabled)) `
        'RestaurantIconExportSampleMenuPolicyInvalid'
    if (-not $menusByProfile.ContainsKey($profileId)) { $menusByProfile[$profileId] = @() }
    $menusByProfile[$profileId] += $menu
    $null = $menuIds.Add($menuId)
    $null = $menuStableIds.Add($menuStableId)
}

foreach ($entry in $outputEntries) {
    $matchingProfiles = @($profiles | Where-Object { $_.profileStableId -ceq $entry.stableId })
    Assert-ExportCondition ($matchingProfiles.Count -eq 1) `
        'RestaurantIconExportProfileProjectionInvalid'
    $profile = $matchingProfiles[0]
    $profileMenus = @($menusByProfile[[string]$profile.restaurantProfileId] |
        Sort-Object @{ Expression = { [int]$_.displayOrder } }, @{ Expression = { [string]$_.menuStableId } })
    Assert-ExportCondition ($profileMenus.Count -ge 2 -and $profileMenus.Count -le 3) `
        'RestaurantIconExportMenusPerProfileInvalid'
    $expectedOrders = @(1..$profileMenus.Count)
    $actualOrders = @($profileMenus | ForEach-Object { [int]$_.displayOrder })
    Assert-ExportCondition (-not (Compare-Object $expectedOrders $actualOrders)) `
        'RestaurantIconExportMenuOrderInvalid'
    $entry.sampleMenuDisplayNames = @($profileMenus | ForEach-Object { [string]$_.menuName })
}

$fixtureRows = [Collections.Generic.List[string]]::new()
$orderedProfiles = @($profiles | Sort-Object { [string]$_.semanticPlaceStableId })
for ($profileIndex = 0; $profileIndex -lt $orderedProfiles.Count; $profileIndex++) {
    $profile = $orderedProfiles[$profileIndex]
    $sequence = $profileIndex + 1
    $expectedSemanticPlace = 'synthetic-place:sagajeong-restaurant-anchor-{0:d2}' -f $sequence
    Assert-ExportCondition ($profile.semanticPlaceStableId -ceq $expectedSemanticPlace -and
        [int]$profile.expectedCookingMinutes -ge 10 -and
        [int]$profile.expectedCookingMinutes -le 25) `
        'RestaurantIconExportFixtureHashProfileInvalid'
    $sampleDisplayName = '[샘플] 사가정 음식점 {0:d2}' -f $sequence
    $fixtureRows.Add((@(
        [string]$profile.restaurantProfileId,
        [string]$profile.profileStableId,
        $sampleDisplayName,
        [string]$profile.publicBusinessObservationStableId,
        [string]$profile.locationAnchorStableId,
        [string]$profile.buildingStableId,
        [string]$profile.semanticPlaceStableId,
        [string]$profile.profileCategory,
        ([int]$profile.expectedCookingMinutes).ToString([Globalization.CultureInfo]::InvariantCulture)
    ) -join '|'))
    $profileMenus = @($menusByProfile[[string]$profile.restaurantProfileId] |
        Sort-Object @{ Expression = { [int]$_.displayOrder } }, @{ Expression = { [string]$_.menuStableId } })
    foreach ($menu in $profileMenus) {
        $price = [decimal]::Parse([string]$menu.price, [Globalization.CultureInfo]::InvariantCulture)
        Assert-ExportCondition ($price -ge [decimal]6000 -and $price -le [decimal]14000 -and
            $price % [decimal]1000 -eq [decimal]0) `
            'RestaurantIconExportFixtureHashMenuInvalid'
        $fixtureRows.Add((@(
            [string]$menu.menuId,
            [string]$menu.menuStableId,
            [string]$menu.menuName,
            $price.ToString('0.############################', [Globalization.CultureInfo]::InvariantCulture),
            ([int]$menu.displayOrder).ToString([Globalization.CultureInfo]::InvariantCulture)
        ) -join '|'))
    }
}
$fixtureCanonical = (@(
    'sagajeong-restaurant-fixture.v2',
    $directoryHash,
    [string]$directory.dataRevision,
    'sagajeong-sample-menu-seed-20260913-r1',
    'SyntheticFixture',
    'DevelopmentSimulation',
    'distributionApproved=false',
    'operationalEffectsAllowed=false',
    'observable-operations-fixture.v2'
) + @($fixtureRows)) -join "`n"
Assert-ExportCondition ((Get-Sha256Text $fixtureCanonical) -ceq $pack.fixtureHashSha256) `
    'RestaurantIconExportFixtureHashMismatch'

$outputEntries = @($outputEntries | Sort-Object { [string]$_.stableId })
$fixture = [ordered]@{
    schema = 'sagajeong-restaurant-icon-fixture.v1'
    fixtureRevision = [string]$pack.fixtureRevision
    sourceKindCode = 'LocalPrivateReview'
    distributionApproved = $false
    operationalEffectsAllowed = $false
    disclosure = '샘플 메뉴 · 실제 판매 메뉴 아님 · 제휴 관계 없음 · 로컬 검토용/비배포'
    entries = $outputEntries
}
$json = ($fixture | ConvertTo-Json -Depth 8) + "`n"
$forbiddenFields = @(
    '"businessName"', '"restaurantName"', '"displayName"', '"address"',
    '"roadAddress"', '"lotAddress"', '"phone"', '"telephone"',
    '"latitude"', '"longitude"', '"상호"', '"주소"', '"전화"'
)
Assert-ExportCondition (-not @($forbiddenFields | Where-Object {
    $json.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0
}).Count) 'RestaurantIconExportPrivateFieldForbidden'

$unityRoot = [IO.Path]::GetFullPath($UnityProjectPath)
Assert-ExportCondition (Test-Path -LiteralPath (Join-Path $unityRoot 'Assets') -PathType Container) `
    'RestaurantIconExportUnityProjectInvalid'
$tempRoot = [IO.Path]::GetFullPath((Join-Path $unityRoot 'Temp'))
if (Test-Path -LiteralPath $tempRoot) {
    $tempItem = Get-Item -LiteralPath $tempRoot -Force
    Assert-ExportCondition (-not ($tempItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) `
        'RestaurantIconExportUnityTempReparsePointForbidden'
}
else {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
}
$targetPath = [IO.Path]::GetFullPath((Join-Path $tempRoot 'sagajeong-restaurant-icons.local.json'))
$tempPrefix = $tempRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
Assert-ExportCondition ($targetPath.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) `
    'RestaurantIconExportMustBeUnderUnityTemp'

$bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
$contentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$temporaryPath = Join-Path $tempRoot ('.sagajeong-restaurant-icons.' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    [IO.File]::WriteAllBytes($temporaryPath, $bytes)
    Move-Item -LiteralPath $temporaryPath -Destination $targetPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

[ordered]@{
    status = 'ExportedLocalPrivateReviewFixture'
    schema = [string]$fixture.schema
    fixtureRevision = [string]$fixture.fixtureRevision
    restaurantCount = $outputEntries.Count
    menuCount = $menus.Count
    distributionApproved = $false
    operationalEffectsAllowed = $false
    privateFieldsIncluded = $false
    contentHashSha256 = $contentHash
    targetPath = $targetPath
} | ConvertTo-Json
