$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tests = @(Get-ChildItem -LiteralPath (Join-Path $root 'eng/planning-inquiries/app-production') -Filter '*.test.mjs' | Sort-Object Name | ForEach-Object FullName)
if ($tests.Count -eq 0) { throw 'PlanningAppProductionTestsMissing' }
& node --test @tests
if ($LASTEXITCODE -ne 0) { throw "PlanningAppProductionTestsFailed:$LASTEXITCODE" }
