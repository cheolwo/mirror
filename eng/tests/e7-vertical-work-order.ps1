$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manager = Join-Path $repositoryRoot `
    "eng/execution-ledgers/manage-e7-vertical-work-order.ps1"
$result = & $manager
if ([string] $result -ne
    "E7VerticalWorkOrderValid:E7-WO-TEMPLATE;Logic=E0;Presentation=E0;Integrated=E0;Pass=DownwardImpactReview;PromotionEligible=False") {
    throw "E7VerticalWorkOrderValidationFailed:$result"
}

$template = Get-Content -LiteralPath (Join-Path $repositoryRoot `
    "eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json") `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$template.trackPlans.logic.downwardPlan[0].code = "E6"
$artifactDirectory = Join-Path $repositoryRoot `
    "artifacts/local/validation/e7-vertical-work-order"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$invalidPath = Join-Path $artifactDirectory "invalid-order.json"
[IO.File]::WriteAllText($invalidPath, ($template | ConvertTo-Json -Depth 20),
    [Text.UTF8Encoding]::new($false))
$relative = $invalidPath.Substring($repositoryRoot.Length + 1).Replace('\', '/')
$rejected = $false
try { & $manager -InputPath $relative | Out-Null }
catch { $rejected = $_.Exception.Message.Contains("DownwardOrderInvalid") }
if (-not $rejected) { throw "E7VerticalInvalidOrderWasAccepted" }

$invalidGate = Get-Content -LiteralPath (Join-Path $repositoryRoot `
    "eng/execution-ledgers/work-orders/interaction-e1-e7-validation.template.json") `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$invalidGate.trackPlans.presentation.currentEvidenceStage = "E5"
$invalidGate.currentEvidenceStage = "E0"
$invalidGate.integratedGate.currentEvidenceStage = "E0"
$gatePath = Join-Path $artifactDirectory "presentation-e5-before-logic.json"
[IO.File]::WriteAllText($gatePath, ($invalidGate | ConvertTo-Json -Depth 30),
    [Text.UTF8Encoding]::new($false))
$gateRelative = $gatePath.Substring($repositoryRoot.Length + 1).Replace('\', '/')
$gateRejected = $false
try { & $manager -InputPath $gateRelative | Out-Null }
catch { $gateRejected = $_.Exception.Message.Contains("PresentationE5RequiresLogicE5") }
if (-not $gateRejected) { throw "E7VerticalPresentationLogicGateWasAccepted" }

$playableLoops = Get-Content -LiteralPath (Join-Path $repositoryRoot `
    "eng/execution-ledgers/playable-loops.json") -Raw -Encoding UTF8 |
    ConvertFrom-Json
$registeredWorkOrderRefs = @($playableLoops.items |
    ForEach-Object {
        $property = $_.PSObject.Properties['workOrderRefs']
        if ($null -ne $property) { @($property.Value) }
    } |
    Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) } |
    Sort-Object -Unique)
foreach ($workOrderRef in $registeredWorkOrderRefs) {
    $workOrderPath = Join-Path $repositoryRoot ([string] $workOrderRef)
    if (-not (Test-Path -LiteralPath $workOrderPath)) {
        throw "RegisteredE7VerticalWorkOrderMissing:$workOrderRef"
    }
    $workOrderResult = & $manager -InputPath ([string] $workOrderRef)
    if ([string] $workOrderResult -notmatch '^E7VerticalWorkOrderValid:') {
        throw "RegisteredE7VerticalWorkOrderValidationFailed:${workOrderRef}:$workOrderResult"
    }
}

Write-Output "E7VerticalWorkOrderTestsPassed:Stages=7;Tracks=2;Target=E7;Registered=$($registeredWorkOrderRefs.Count)"
