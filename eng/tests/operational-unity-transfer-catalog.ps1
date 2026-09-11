$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manager = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-operational-unity-transfer-catalog.ps1'
$policyPath = Join-Path $repositoryRoot 'eng/execution-ledgers/operational-unity-transfer-policy.json'
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/local/validation/operational-unity-transfer/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$fixtureRelative = $fixtureRoot.Substring($repositoryRoot.Length + 1).Replace('\', '/')
$script:cases = 0

function Assert-Case([bool] $Condition, [string] $Name) {
    if (-not $Condition) { throw "OperationalUnityTransferTestFailed:$Name" }
    $script:cases++
}

$machineOutput = "$fixtureRelative/catalog.json"
$markdownOutput = "$fixtureRelative/catalog.md"
& $manager -Mode Write -MachineOutputPath $machineOutput -OutputPath $markdownOutput | Out-Null
& $manager -Mode Check -MachineOutputPath $machineOutput -OutputPath $markdownOutput | Out-Null
$catalogPath = Join-Path $repositoryRoot $machineOutput
$markdownPath = Join-Path $repositoryRoot $markdownOutput
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json

Assert-Case ([string] $catalog.schemaVersion -eq 'operational-unity-transfer-catalog.v3') 'Schema'
Assert-Case (@($catalog.pageCapabilities).Count -ge 200) 'PageCatalogIsComprehensive'
Assert-Case (@($catalog.dbSets).Count -ge 250) 'DbSetInventoryIsComprehensive'
Assert-Case (@($catalog.mongoCollections).Count -ge 20) 'MongoInventoryIsComprehensive'
Assert-Case (@($catalog.unityRepresentativeRoutes).Count -eq 18) 'RepresentativeUnityRoutesPreserved'
Assert-Case (@($catalog.pageCapabilities | Where-Object transferClassification -eq 'PlayableAction').Count -gt 0) 'PlayableActionsExist'
Assert-Case (@($catalog.pageCapabilities | Where-Object transferClassification -eq 'ReadOnlyContext').Count -gt 0) 'ReadOnlyContextsExist'
Assert-Case (@($catalog.pageCapabilities | Where-Object transferClassification -eq 'AmbientSimulation').Count -gt 0) 'AmbientSimulationsExist'
Assert-Case (@($catalog.pageCapabilities | Where-Object transferClassification -eq 'ServerOnly').Count -gt 0) 'ServerOnlyBoundariesExist'
Assert-Case (@($catalog.pageCapabilities | Where-Object { $_.appCode -eq 'SsalddelAdmin' -and $_.transferClassification -ne 'ServerOnly' }).Count -eq 0) 'AdminAlwaysServerOnly'
Assert-Case (@($catalog.pageCapabilities | Where-Object { $_.mappingRuleIds -contains 'warehouse-fulfillment' -and $_.hMappingStatus -ne 'MappedCandidate' }).Count -eq 0) 'WarehouseHasHMapping'
Assert-Case (@($catalog.pageCapabilities | Where-Object canonicalFeatureId -eq 'warehouse-inbound-inspection').Count -ge 2) 'ValidatedAliasGroup'
Assert-Case ([string] $catalog.firstSlice.playableLoopRef -eq 'playable-loop:hub-inbound-putaway.v1') 'HubFirstSlice'
Assert-Case ([string] $catalog.firstSlice.e5Status -eq 'BlockedPendingActualWorldPlacement') 'NoAutomaticE5Promotion'
$foodProfile = @($catalog.observationProfiles | Where-Object profileId -eq 'observation-profile:synthetic-neighborhood-food-life.v1')
Assert-Case ($foodProfile.Count -eq 1) 'FoodLifeObservationProfile'
Assert-Case ([string] $catalog.firstLivingSceneProfileRef -eq 'observation-profile:synthetic-neighborhood-food-life.v1') 'FirstLivingSceneProfile'
Assert-Case ([string] $foodProfile[0].sceneStableId -eq 'SimulationWorldShell') 'CanonicalObservationScene'
Assert-Case ([string] $foodProfile[0].cameraModeCode -eq 'OverviewWithManualFocus') 'ManualFocusCamera'
Assert-Case (-not [bool] $foodProfile[0].allowsOperationalActions -and [bool] $foodProfile[0].observationPresentationOnly) 'ObservationCannotMutateOperations'
Assert-Case (@($foodProfile[0].worldInteractionRefs).Count -eq 8) 'FoodLifeWorldInteractions'
$foodBindings = @($catalog.appObservationBindings | Where-Object observationProfileRef -eq $foodProfile[0].profileId)
Assert-Case ($foodBindings.Count -eq 3) 'ThreeFoodAppBindings'
Assert-Case (@($foodBindings | Where-Object relationCode -ne 'SimulationAnalog').Count -eq 0) 'FoodBindingsAreSimulationAnalogs'
Assert-Case (@($foodBindings.appCode | Sort-Object) -join ',' -eq 'FoodDeliveryDriverApp,OrdererApp,RestaurantDeskApp') 'FoodAppCodes'
$roleObjects = @($catalog.roleObjectCandidates)
Assert-Case ($roleObjects.Count -eq 25) 'RoleObjectCandidateCount'
Assert-Case (@($roleObjects.objectArchetypeId | Sort-Object -Unique).Count -eq $roleObjects.Count) 'RoleObjectCandidateIdsUnique'
Assert-Case (@($roleObjects | Where-Object { $_.isExecutionAuthority -or $_.containsPrivateData }).Count -eq 0) 'RoleObjectsHaveNoAuthorityOrPrivateData'
Assert-Case (@($roleObjects | Where-Object { $_.prefabReady -or $_.sceneReady }).Count -eq 0) 'RoleObjectsDoNotClaimPrefabOrSceneReadiness'
$requiredRoleObjectIds = @(
    'operational-object:actor:orderer.v1',
    'operational-object:actor:restaurant-owner.v1',
    'operational-object:actor:food-courier.v1',
    'operational-object:actor:cargo-driver.v1',
    'operational-object:actor:warehouse-manager.v1',
    'operational-object:actor:warehouse-worker.v1',
    'operational-object:actor:mart-operator.v1',
    'operational-object:facility:restaurant.v1',
    'operational-object:facility:warehouse.v1',
    'operational-object:facility:ssalddel-mart.v1',
    'operational-object:vehicle:cargo.v1',
    'operational-object:vehicle:food-delivery.v1',
    'operational-object:work:cargo-load.v1',
    'operational-object:work:food-order.v1',
    'operational-object:work:warehouse-handling-unit.v1'
)
Assert-Case (@($requiredRoleObjectIds | Where-Object { $_ -notin $roleObjects.objectArchetypeId }).Count -eq 0) 'RequiredRoleObjectsExist'
$platformOperator = @($roleObjects | Where-Object objectArchetypeId -eq 'operational-object:actor:platform-operator.v1')
Assert-Case ($platformOperator.Count -eq 1 -and [string] $platformOperator[0].representationDecisionCode -eq 'NoUnityRepresentation' -and [string] $platformOperator[0].spawnModeCode -eq 'NotSpawnable') 'PlatformOperatorIsNotSpawnable'
$foodRoleObjects = @($roleObjects | Where-Object { $_.observationProfileRefs -contains $foodProfile[0].profileId })
Assert-Case (@($foodRoleObjects.objectArchetypeId | Sort-Object) -join ',' -eq 'operational-object:actor:food-courier.v1,operational-object:actor:orderer.v1,operational-object:actor:restaurant-owner.v1,operational-object:facility:restaurant.v1,operational-object:vehicle:food-delivery.v1,operational-object:work:food-order.v1') 'FoodObservationRoleObjectsBound'
$forbiddenRoleObjectProperties = @('userId', 'phone', 'address', 'accountNumber', 'exactLocation', 'rating', 'acceptanceRate')
Assert-Case (@($roleObjects | ForEach-Object { $_.PSObject.Properties.Name } | Where-Object { $_ -in $forbiddenRoleObjectProperties }).Count -eq 0) 'NoSensitiveRoleObjectProperties'
$markdown = Get-Content -LiteralPath $markdownPath -Raw -Encoding UTF8
Assert-Case ($markdown.Contains('MappedCandidate') -and $markdown.Contains('E5') -and $markdown.Contains('SimulationAnalog') -and $markdown.Contains('operational-object:actor:food-courier.v1')) 'MarkdownBoundary'

$beforeJson = (Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256).Hash
$beforeMarkdown = (Get-FileHash -LiteralPath $markdownPath -Algorithm SHA256).Hash
& $manager -Mode Write -MachineOutputPath $machineOutput -OutputPath $markdownOutput | Out-Null
Assert-Case ((Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256).Hash -ceq $beforeJson) 'JsonDeterministic'
Assert-Case ((Get-FileHash -LiteralPath $markdownPath -Algorithm SHA256).Hash -ceq $beforeMarkdown) 'MarkdownDeterministic'

$hubQuery = @(& $manager -Mode Query -QueryKind H1 -QueryValue 'h1-stock:hub-receiving-storage') -join "`n"
$hubRows = $hubQuery | ConvertFrom-Json
Assert-Case (@($hubRows).Count -gt 0) 'H1Query'
Assert-Case (@($hubRows | Where-Object { $_.areaCodes -contains 'Hub' }).Count -eq @($hubRows).Count) 'H1QueryArea'

$foodOsQuery = @(& $manager -Mode Query -QueryKind RoleObject -QueryValue 'FoodDeliveryOS') -join "`n"
$foodOsRows = $foodOsQuery | ConvertFrom-Json
Assert-Case (@($foodOsRows).Count -eq 7) 'RoleObjectOperatingSystemQuery'
Assert-Case (@($foodOsRows | Where-Object { $_.operatingSystemIds -notcontains 'FoodDeliveryOS' }).Count -eq 0) 'RoleObjectOperatingSystemQueryScope'

$legacyPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$legacyPolicy.schemaVersion = 'operational-unity-transfer-policy.v1'
$legacyPolicy.PSObject.Properties.Remove('observationProfiles')
$legacyPolicy.PSObject.Properties.Remove('appObservationBindings')
$legacyPolicy.PSObject.Properties.Remove('firstLivingSceneProfileRef')
$legacyPolicy.PSObject.Properties.Remove('roleObjectCandidates')
$legacyPolicyPath = Join-Path $fixtureRoot 'legacy-v1-policy.json'
$legacyJsonOutput = "$fixtureRelative/legacy-v1-catalog.json"
$legacyMarkdownOutput = "$fixtureRelative/legacy-v1-catalog.md"
[IO.File]::WriteAllText($legacyPolicyPath, (($legacyPolicy | ConvertTo-Json -Depth 40) + "`n"), [Text.UTF8Encoding]::new($false))
& $manager -Mode Write -PolicyPath $legacyPolicyPath -MachineOutputPath $legacyJsonOutput -OutputPath $legacyMarkdownOutput | Out-Null
$legacyCatalog = Get-Content -LiteralPath (Join-Path $repositoryRoot $legacyJsonOutput) -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Case ([string] $legacyCatalog.schemaVersion -eq 'operational-unity-transfer-catalog.v3') 'LegacyPolicyGeneratesCurrentCatalog'
Assert-Case (@($legacyCatalog.observationProfiles).Count -eq 0 -and @($legacyCatalog.appObservationBindings).Count -eq 0) 'LegacyPolicyDefaultsObservationLists'
Assert-Case (@($legacyCatalog.roleObjectCandidates).Count -eq 0) 'LegacyPolicyDefaultsRoleObjectList'

function Assert-PolicyRejected([object] $Policy, [string] $FileName, [string] $Name) {
    $badPolicyPath = Join-Path $fixtureRoot $FileName
    [IO.File]::WriteAllText($badPolicyPath, (($Policy | ConvertTo-Json -Depth 40) + "`n"), [Text.UTF8Encoding]::new($false))
    $failureObserved = $false
    try {
        & $manager -Mode Check -PolicyPath $badPolicyPath -MachineOutputPath $machineOutput -OutputPath $markdownOutput | Out-Null
    }
    catch { $failureObserved = $true }
    Assert-Case $failureObserved $Name
}

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.planning.documentSha256 = '0' * 64
Assert-PolicyRejected $badPolicy 'bad-plan-hash-policy.json' 'PlanningHashDriftRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.observationProfiles = @($badPolicy.observationProfiles) + @($badPolicy.observationProfiles[0])
Assert-PolicyRejected $badPolicy 'duplicate-profile-policy.json' 'DuplicateObservationProfileRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.appObservationBindings[0].observationProfileRef = 'observation-profile:missing'
Assert-PolicyRejected $badPolicy 'missing-profile-policy.json' 'MissingObservationProfileRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.observationProfiles[0].allowsOperationalActions = $true
Assert-PolicyRejected $badPolicy 'operational-action-policy.json' 'AutonomousOperationalActionRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.roleObjectCandidates = @($badPolicy.roleObjectCandidates) + @($badPolicy.roleObjectCandidates[0])
Assert-PolicyRejected $badPolicy 'duplicate-role-object-policy.json' 'DuplicateRoleObjectRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.roleObjectCandidates[0].isExecutionAuthority = $true
Assert-PolicyRejected $badPolicy 'role-object-authority-policy.json' 'RoleObjectAuthorityRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.roleObjectCandidates[0].operatingSystemIds = @('UnknownOS')
Assert-PolicyRejected $badPolicy 'role-object-unknown-os-policy.json' 'RoleObjectUnknownOperatingSystemRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.roleObjectCandidates[0].observationProfileRefs = @('observation-profile:missing')
Assert-PolicyRejected $badPolicy 'role-object-missing-profile-policy.json' 'RoleObjectMissingObservationProfileRejected'

$badPolicy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$badPolicy.roleObjectCandidates[0].visualKey = 'Assets/Prefabs/shipper.prefab'
Assert-PolicyRejected $badPolicy 'role-object-asset-path-policy.json' 'RoleObjectAssetPathRejected'

Write-Output "OperationalUnityTransferCatalogTestsPassed:Cases=$script:cases;Pages=$(@($catalog.pageCapabilities).Count);DbSets=$(@($catalog.dbSets).Count);Mongo=$(@($catalog.mongoCollections).Count)"
