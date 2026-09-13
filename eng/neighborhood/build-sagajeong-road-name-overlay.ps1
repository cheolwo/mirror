[CmdletBinding()]
param(
    [string]$RawPath='artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string]$MapPath='C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json',
    [string]$OutputPath='C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongRoadNameOverlay.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$raw=Join-Path $root $RawPath
$rawHash='3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3'
$mapHash='4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3'
if((Get-FileHash $raw -Algorithm SHA256).Hash -ne $rawHash){throw 'SagajeongRoadNameRawHashMismatch'}
if((Get-FileHash $MapPath -Algorithm SHA256).Hash -ne $mapHash){throw 'SagajeongRoadNameMapHashMismatch'}
[xml]$osm=Get-Content $raw -Raw -Encoding UTF8
$map=Get-Content $MapPath -Raw -Encoding UTF8|ConvertFrom-Json
if($map.revision -ne 'sagajeong-reference.r3'){throw 'SagajeongRoadNameMapRevisionMismatch'}
$wayTags=@{}
foreach($way in $osm.osm.way){
    $tags=@{};foreach($tag in $way.tag){$tags[[string]$tag.k]=[string]$tag.v}
    if($tags.ContainsKey('highway') -and $tags.ContainsKey('name') -and -not [string]::IsNullOrWhiteSpace($tags.name)){
        $wayTags['osm:way:'+[string]$way.id]=[pscustomobject]@{name=$tags.name;kind=$tags.highway}
    }
}
$segments=@()
foreach($road in $map.roads){
    $wayId=([string]$road.id) -replace ':segment:\d+$',''
    if(!$wayTags.ContainsKey($wayId)){continue}
    $tag=$wayTags[$wayId]
    $dx=[double]$road.x2-[double]$road.x1;$dz=[double]$road.z2-[double]$road.z1
    $segments += [pscustomobject]@{wayId=$wayId;name=$tag.name;kind=[string]$road.kind;x1=[double]$road.x1;z1=[double]$road.z1;x2=[double]$road.x2;z2=[double]$road.z2;length2=$dx*$dx+$dz*$dz}
}
$items=@()
foreach($group in @($segments|Group-Object name|Sort-Object Name)){
    $anchor=@($group.Group|Sort-Object @{Expression='length2';Descending=$true},@{Expression='wayId';Descending=$false}|Select-Object -First 1)[0]
    $yaw=[Math]::Atan2($anchor.z2-$anchor.z1,$anchor.x2-$anchor.x1)*180/[Math]::PI
    if($yaw -gt 90){$yaw-=180};if($yaw -lt -90){$yaw+=180}
    $kinds=@($group.Group.kind|Sort-Object -Unique)
    $importance=if(@($kinds|Where-Object {$_ -in @('primary','secondary','tertiary')}).Count){'Major'}else{'Local'}
    $items += [ordered]@{
        name=$group.Name;importanceCode=$importance
        x=[Math]::Round(($anchor.x1+$anchor.x2)/2,5);z=[Math]::Round(($anchor.z1+$anchor.z2)/2,5);yaw=[Math]::Round($yaw,3)
        segmentCount=$group.Count;buildingCount=@($map.buildings|Where-Object street -eq $group.Name).Count
        wayIds=@($group.Group.wayId|Sort-Object -Unique)
    }
}
$major=@($items|Where-Object importanceCode -eq 'Major').Count
$linkedBuildings=($items.buildingCount|Measure-Object -Sum).Sum
if($items.Count -ne 78 -or $segments.Count -ne 1109 -or $major -ne 4 -or $linkedBuildings -ne 568){throw 'SagajeongRoadNameFrozenCountsMismatch'}
$output=[ordered]@{
    schema='sagajeong-road-name-overlay.v1';revision='sagajeong-road-address-presentation.r1'
    mapRevision=$map.revision;mapHash=$mapHash;rawSourceHash=$rawHash;sourceUrl=$map.sourceUrl
    attribution=$map.attribution;presentationOnly=$true;namedSegmentCount=$segments.Count;linkedBuildingCount=$linkedBuildings
    roads=@($items|Sort-Object @{Expression={if($_.importanceCode -eq 'Major'){0}else{1}}},name)
    boundary='PublicOpenData;PresentationOnly;NoNavigation;NoAddressAuthority'
}
$json=$output|ConvertTo-Json -Depth 8 -Compress
[IO.File]::WriteAllText($OutputPath,$json,[Text.UTF8Encoding]::new($false))
$again=Get-Content $OutputPath -Raw -Encoding UTF8|ConvertFrom-Json
if($again.roads.Count -ne 78 -or $again.namedSegmentCount -ne 1109 -or $again.linkedBuildingCount -ne 568){throw 'SagajeongRoadNameReadbackMismatch'}
"roadNames=$($again.roads.Count) namedSegments=$($again.namedSegmentCount) linkedBuildings=$($again.linkedBuildingCount) output=$OutputPath"
