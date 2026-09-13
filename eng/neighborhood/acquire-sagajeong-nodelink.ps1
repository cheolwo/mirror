param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$Root=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
$folder=Join-Path $Root 'artifacts/local/public-data/sagajeong-nodelink-20260912-r1'
if(Test-Path $folder){throw 'Acquisition folder already exists; inspect receipt before retry.'}
New-Item -ItemType Directory -Path $folder | Out-Null
$receipt=[ordered]@{status='Started';startedAtUtc=[DateTimeOffset]::UtcNow;scope='One national archive; Sagajeong extraction only; no national DB import';files=@()}
function Save-Receipt { $receipt | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $folder 'acquisition.json') -Encoding utf8 }
function Record-File([string]$name,[string]$url){
    $path=Join-Path $folder $name
    $receipt.files+=@{file=$name;url=$url;sha256=(Get-FileHash $path -Algorithm SHA256).Hash;bytes=(Get-Item $path).Length;collectedAtUtc=[DateTimeOffset]::UtcNow}
    Save-Receipt
}
try {
    $metadataUrl='https://www.data.go.kr/catalog/15025526/fileData.json'
    Invoke-WebRequest $metadataUrl -OutFile (Join-Path $folder 'license-metadata.json') -TimeoutSec 30
    Record-File 'license-metadata.json' $metadataUrl
    $metadata=Get-Content (Join-Path $folder 'license-metadata.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if($metadata.license -ne '이용허락범위 제한 없음'){throw 'Dataset license changed'}
    $page=Invoke-WebRequest 'https://www.its.go.kr/nodelink/nodelinkRef' -SessionVariable itsSession -TimeoutSec 30
    $token=[regex]::Match($page.Content,'<meta name="_csrf" content="([^"]+)"').Groups[1].Value
    if(!$token){throw 'Anonymous request token missing'}
    $body=@{body=@{data=@{searchType=-1;workingDirectory='/nodeLink';startDate='2026-08-12';endDate='2026-09-12'}}} | ConvertTo-Json -Depth 5
    $catalogUrl='https://www.its.go.kr/opendata/getNodeLinkDataFileList'
    Invoke-WebRequest $catalogUrl -Method Post -WebSession $itsSession -Headers @{'X-XSRF-TOKEN'=$token} -ContentType 'application/json' -Body $body -OutFile (Join-Path $folder 'catalog.json') -TimeoutSec 30 | Out-Null
    Record-File 'catalog.json' $catalogUrl
    $catalog=Get-Content (Join-Path $folder 'catalog.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $entry=@($catalog.body.data.fileVOList | Where-Object {$_.atchFileId -eq 'DF_219' -and $_.nodeFileSeq -eq '0'})
    if($catalog.header.state -ne 'OK' -or $entry.Count -ne 1 -or $entry[0].nodeFileMg -gt 300){throw 'Approved archive catalog mismatch'}
    $receipt.catalogEntry=$entry[0]; Save-Receipt
    $url='https://www.its.go.kr/opendata/nodelinkFileSDownload/DF_219/0'
    # 명시 승인된 배포 파일 한 건. 자동 재시도/전국 DB 적재는 하지 않는다.
    & curl.exe --fail --show-error --location --proto '=https' --proto-redir '=https' --connect-timeout 20 --max-time 300 --max-filesize 314572800 --output (Join-Path $folder 'nodelink.zip.partial') $url
    if($LASTEXITCODE -ne 0){throw "ArchiveDownloadCurlExit:$LASTEXITCODE"}
    $partial=Join-Path $folder 'nodelink.zip.partial'
    $stream=[IO.File]::OpenRead($partial)
    try { if($stream.ReadByte() -ne 80 -or $stream.ReadByte() -ne 75){throw 'Not a ZIP archive'} } finally {$stream.Dispose()}
    Move-Item -LiteralPath $partial -Destination (Join-Path $folder 'nodelink.zip')
    Record-File 'nodelink.zip' $url
    $receipt.status='Downloaded';Save-Receipt
    Write-Output "Downloaded archive and provenance: $folder"
} catch {
    $receipt.status='Failed';$receipt.error=$_.Exception.GetType().Name;Save-Receipt
    throw
}
