# Codex → Claude: 행정동 모듈 반환 보완 r55

- 날짜/역할: 2026-11-13, Codex 반환 검토·독립 검사.
- 정본: [행정동 디오라마 r55](../Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/windows-dong-return-hardening.review.r55.md). r54·첫 반환과의 차이는 정본에 기록했다.
- 사용자 경계: **Claude 작업 중 — 겹치는 파일은 남겨 두기**. 이번에 `ssalddel/`의 제품/자원/검사/빌드는 변경하지 않았다. 작업 중인 붙여/띄워/이 동만 보기를 덮어쓰지 않는다.
- 이번 산출물: Hongdal의 읽기 전용 [감사기](../../../eng/neighborhood/audit_windows_dong_module_resources.py), [회귀 검사](../../../eng/neighborhood/test_audit_windows_dong_module_resources.py), 실제 결과 (로컬 검토 자료 `artifacts/local/dong-resource-audit-20261113-codex/resource-audit.json`), 검토 소스 지문 (로컬 검토 자료 `artifacts/local/dong-resource-audit-20261113-codex/reviewed-source-fingerprints.json`).

## 검토 후 이어 할 것

사용자의 후속 요청으로 **자료 수집을 먼저 수행했다**. [실제 취득·기존 원본 재사용](../DataCollection/Research/2026-11-13-dong-map-source-intake.md)에 새 노원 도보망·행안부 관할코드, 지번 후보61,897건과 소비13,383건 대조를 연결했다. 최신 행정동65개와 과거OA 목록64개의 차이, 도보망2020년/경계노드 누락과 도로명주소·출입구 미취득을 먼저 확인한다. 새 자료를 이번 진행 중인 게임에 자동 교체하지 않는다.

1. 최신 소스에서 **색인 바이트 hash 검증 및 좌표/동 귀속 결속**이 이미 보완됐는지 먼저 확인한다. 검토 소스에서는 `buildingIndexSha256`가 선언만 있고 건물 색인을 바로 읽었다.
2. **목록 없음과 목록 손상을 별도 처리**한다. 목록 제거/옛 proof는 OSM 경로를 유지하지만, 존재하는 손상 목록은 오류를 드러내고 OSM 메시/색인으로 조용히 대체하지 않는다.
3. 이번 독립 검사를 빌드 전 별도 검사로 실행하고, A/B 수정 후 실제 게임의 정상·변조·누락 입력 결과를 반환한다. 독립 감사 통과가 게임의 자원 실패 처리 성공을 뜻하지 않는다.

검토된 수정 위치는 `Runtime/SagajeongDispatchDongModules.cs`, `SagajeongDispatchBuildings.cs`, `SagajeongDispatchDiorama.cs`다. 이 문서는 해당 파일의 현재 쓰기 소유를 Codex로 옮기거나 별도 Goal을 활성화하지 않는다. 코드·동 보기 설계·표현 자원을 추가 변경할 때는 현재 수용 범위와 파일 소유를 그대로 따른다.

## 실행

Hongdal 루트에서 Python 3.10 이상, 외부 패키지 없이 실행한다. 이 PC에서 `python` 명령이 없으면 `python`를 사용한다.

```powershell
python -B -m unittest discover -s eng/neighborhood -p test_audit_windows_dong_module_resources.py -v
python -B eng/neighborhood/audit_windows_dong_module_resources.py --resources ssalddel/Assets/Ssalddel/WindowsGame/Resources --report artifacts/local/dong-resource-audit-20261113-codex/claude-recheck.json
```

종료 code는 0=`Passed`, 1=`Failed`, 2=`InputChangedDuringAudit`다. 입력 폴더 안의 보고서 경로는 거절한다. 파일 수정 중에는 안정된 묶음으로 다시 검사하고 2를 손상 확정이나 통과로 해석하지 않는다.

현재 실제 입력 결과는 모듈6·건물13,383·상징높이1,108·주소0·역할4, 배포/통행/게임 준비 모두 false다. 여섯 동이 두 순회 정거장과 기존 역할 공간을 덮는 범위는 유지한다. 4구 목록/준비도 우선은 **후속 제안**이고 이번에 구현됐다는 뜻이 아니다. 현행 경계/r16, 500m 타일/기존 Interpreter 미결속도 남는다.

정본 SHA256: `653CBAA06C6F92AEE0782CED6BB231A0B2E8B54DC1AC23B1568601397545557C`.

## 반환 확인

수정된 파일/지문, 실패 입력별 실제 동작, 새 모듈 경로의 실제 실행판/화면, 동 보기 전환 전후 업무 상태 보존을 구분해 적는다. 기존 OSM proof 결과나 이전 `200502` 캡처를 새 소스의 실행 증거로 재사용하지 않는다. commit/push·운영/배포·예약·E 승격은 이번 보완에 포함하지 않았다.
