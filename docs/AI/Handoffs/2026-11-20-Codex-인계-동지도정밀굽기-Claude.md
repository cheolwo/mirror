# 전국 동 지도 굽기 · Codex → Claude 인계

**상태는 인계 준비다.** 현재 경기 굽기는 Codex가 시작한 실행에 이어진다. Claude는 같은 Windows PC에서 진행 기록을 먼저 인수할 수 있다. 굽기 실행을 맡을 때는 기존 실행의 종료를 확인하고 남은 묶음부터 시작한다.

| 항목 | 작업본 |
| --- | --- |
| 저장소 | `C:/Users/user/source/repos/Hongdal` |
| 기준 | `dev/mirror-integration` · `058513f733e17673e14cc873800ce586dcc5d642` |
| 굽기 작업 폴더 | `artifacts/local/hongdal-unity-claude-20261112/dong-bake-national-20261120-r1` |
| 실행 환경 | 같은 PC의 `dong-bake-gyeonggi-20261118-r1/codex-python/Scripts/python.exe` |
| 인계 시점 기록 | 작업 폴더의 `claude-handoff-p2-20261120.json`. 고정 파일 지문과 진행 장부의 당시 지문이 들어 있다. |

[원래 전국 작업 순서](2026-11-20-Claude-인계-동지도정밀굽기-전국-Codex.md)와 [P1 검증 반환](2026-11-20-Codex-동지도정밀굽기-P1-반환.md)을 기준으로 한다. [같은 PC 작업본 확인](Codex-Claude-혼용운영안-2026-11-12.md#같은-pc와-다른-pc의-교대)은 아래 경로와 판본으로 대조한다.

## 준비할 때 확인한 결과

- 경기 **7/47 시군구, 88/603동**이 검증을 마쳐 `quality/dong-quality-production.csv`와 `status.json`에 반영됐다.
- 덕양구 `41281`는 작성 시점에 **10묶음 중 4묶음**을 저장했다. 다음 묶음은 `41281-5`였고, 관산동 경계 처리 보완을 포함한 실행이 진행 중이었다. 교대할 때는 이 숫자보다 최신 `district-checkpoint.json`을 기준으로 삼는다.
- 덕양구의 작업 기록은 `codex-official-kr41-41281-p2/work/kr41-41281-r2/41281/district-checkpoint.json`이다. 검증 영수증은 해당 작업의 `out/kr41-41281-r2/41281/<묶음>/group-receipt.json`에 있다. 현재 연속 실행 로그는 `continuation-resume-20261120-223112.log`다.

## 1. Claude가 먼저 인수하는 것

1. 같은 Windows PC에서 저장소 경로·브랜치·기준 커밋을 대조한다. 기존 작업본에는 다른 작업의 미커밋 변경도 있다. 굽기 폴더와 이번 인계 문서, 대기열·현재 작업 문서의 해당 항목만 인계 범위로 삼는다.
2. 위 시점 기록의 고정 파일 SHA-256과 실제 파일을 비교한다. `status.json`, 품질표, 시군구 작업 기록은 실행이 끝난 뒤 최신 지문을 다시 기록한다. 완료 작업물의 영수증과 지문을 실행기가 재검사하도록 그대로 둔다.
3. 실행 중에는 기존 연속 실행 로그, 시군구 체크포인트와 `STATUS.md`를 읽어 진행을 추적한다. 묶음 저장은 체크포인트와 묶음 영수증에서, 시군구 완료는 품질표와 `status.json`에서 확인한다.

주소 원문과 이 작업의 가공물은 같은 PC 안에 둔다. Unity 작업본 `C:/Users/user/ssalddel`은 기존 굽기 입력과 썸네일 도구를 읽는 데 쓰며, 게임 자원 적용과 배포는 별도 작업이다.

## 2. 실제로 교대하는 순서

1. 기존 실행이 이어지고 있으면 그대로 검증 결과를 쌓는다. 중간 교대가 필요할 때는 실행을 시작한 쪽과 종료 시점을 맞춘다. 현재 실행기에는 묶음 경계에서 일시중지하는 옵션이 없다. 기존 콘솔에서 종료하면 진행 중이던 묶음은 재개 시 다시 굽는 범위가 될 수 있다.
2. 연속 실행기와 하위 작업이 **모두 종료됐는지** 확인한다. 시군구 잠금은 품질표와 전국 장부의 동시 갱신까지 보호하지 않는다. `.district.lock` 파일의 존재 여부는 실행 여부의 증거가 아니다.
3. 아래 명령으로 기존 P2 차례를 한 번 재개한다. 완료 시군구는 넘어가고, 진행 중이던 시군구는 기존 설정·입력 지문과 완료 묶음 영수증을 대조한 뒤 남은 묶음부터 이어 간다.

~~~powershell
Set-Location -LiteralPath 'C:/Users/user/source/repos/Hongdal'
$bakeRoot = 'artifacts/local/hongdal-unity-claude-20261112/dong-bake-national-20261120-r1'
$bakePython = 'artifacts/local/hongdal-unity-claude-20261112/dong-bake-gyeonggi-20261118-r1/codex-python/Scripts/python.exe'
$bakeWorkers = 'run_gyeonggi_queue|finish_district|run_district|prepare_official|supplement_building_footprints|extract_dong_buildings|make_dong_inputs|make_dong_modules|route_halo_inputs|route_piece_diagnostics|clip_roadlines|pack_dong_resources|thumb_inputs|make_dong_thumbs|verify_bake|append_verified_district|update_national_status'
$bakeActive = @(Get-CimInstance Win32_Process -Filter "name='python.exe'" | Where-Object {
    $_.CommandLine -match 'dong-bake-national-20261120-r1' -and $_.CommandLine -match "($bakeWorkers)\.py"
})
if ($bakeActive.Count) { throw '기존 굽기 작업이 실행 중입니다. 현재 로그와 장부를 확인하세요.' }
if ((Get-PSDrive C).Free -lt 15000000000) { throw '디스크 여유가 15 GB 미만입니다.' }
$bakeLog = Join-Path $bakeRoot ("continuation-claude-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
& $bakePython -I -X utf8 -B (Join-Path $bakeRoot 'tools-r2/run_gyeonggi_queue.py') *>&1 | Tee-Object -FilePath $bakeLog
$bakeExit = $LASTEXITCODE
if ($bakeExit -ne 0) { throw "굽기가 중지됐습니다. 종료 코드 $bakeExit. 로그 $bakeLog" }
~~~

한 시군구에서 교대를 검증하려면 위 실행 줄에 `--district <현재 5자리 시군구 코드>`를 붙인다. 숫자는 교대 시점 장부에서 읽는다. 완료 묶음에 지문 차이가 생기거나 입력 판본이 바뀌면 실행 로그와 기존 영수증을 대조하고, 검증을 마친 결과를 보존한다.

## 3. Claude가 남길 기록

- 인수 직후 **시각, 실행 여부, 시군구·묶음, 완료 수, 남은 디스크, 새 로그 경로**를 적는다.
- 시군구 단위로 포장 검증, 동별 품질표, 전국 장부가 모두 갱신됐는지 확인한다. 15GB 하한이나 검증 실패가 발생하면 마지막 완료 묶음과 멈춘 원인을 기록한다.
- 경기 47개 시군구가 처리되면 경기 반환을 작성하고 다음 단계로 간다. 현재 P2 연속 실행기의 범위는 경기다. 인천 158동의 입력 검사 기록은 `incheon-source-r2/incheon-preflight.json`, 인천부터의 원천·실행 준비는 `p3-p5-source-runbook.md`에 있다.

### Claude에 전달할 문구

> 이 인계 문서와 같은 Windows PC 작업본을 확인해 전국 동 지도 굽기를 이어받아 줘. 기존 실행이 살아 있으면 로그와 장부를 확인하고, 종료 후에는 마지막으로 검증된 체크포인트부터 경기 차례를 한 번만 재개해 줘. 처음에는 현재 시군구·묶음과 로그 경로를 알려 줘.
