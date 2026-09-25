# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r42]

## 구현 목표

`SimulationWorldShell`의 기존 지구본과 행정동·역세권 디오라마 사이에 한반도 권역을 읽는 가벼운 2.5D 지형판을 추가한다. 사용자는 같은 관찰 흐름에서 `지구본 → 권역 지형판 → 디오라마` 순으로 확대하며, 각 단계는 운영·Simulation 상태를 바꾸지 않는 표현 계층으로만 동작한다.

## 구현 결과

- `세계지도표현계층`을 `Globe`, `RegionalTerrain`, `Diorama` 세 단계로 명시했다.
- 한반도 확대는 300ms 카메라 안정 뒤 `해안 윤곽 → 음영 지형 → 검증된 하천 → 거점` 순으로 열린다.
- 권역 표현은 7도 기울어진 얕은 판과 음영 지형 이미지로 구성했다. 실제 DEM 높이 Mesh라고 주장하지 않는다.
- Natural Earth 1:10m Natural Earth I 자료에서 대한민국·북한 주변 범위를 잘라 읽기 전용 미리보기를 만들었다.
- Natural Earth 하천 자료에서 확인된 한강·압록강·두만강만 별도 투명 층에 표시했다. 현재 자료에서 대동강을 확인하지 못했으므로 임의 선을 만들지 않고 결손으로 남겼다.
- 기존 교류축 2D 화면은 자동으로 열지 않고 사용자의 명시적 선택 때만 연다.
- 지형판과 하천 층에는 Collider를 두지 않았고, 카메라·정보 표시 이외의 권위 상태를 변경하지 않는다.
- 새 Scene이나 Map Manager를 만들지 않았고 canonical `SimulationWorldShell` Scene을 저장하지 않았다.

## 자료 계보

| 자료 | 범위·용도 | 이용 조건 | 동결 결과 |
| --- | --- | --- | --- |
| Natural Earth I 1:10m | `123~132E`, `32~44N` 음영 지형·수계 배경 미리보기 | Public domain | `KoreanPeninsulaTerrainPreview.png`, SHA-256 `7FDDD2892C3E1481A278D301DFA074D44502DA576608229E14BC4ADB0163E52B` |
| Natural Earth 1:10m Rivers + Lake Centerlines | 검증 가능한 하천 선형만 별도 표시 | Public domain | `KoreanPeninsulaVerifiedRiversPreview.png`, SHA-256 `2D5C54ED2AE6F9B40EEE91BE2ABE46DC514A1B342C4CED8679E5E1B6860CEC99` |

원본 링크와 범위, 생성 시각, hash, 표시 가능·결손 하천은 `KoreanPeninsulaTerrainPreviewManifest.json`에 함께 기록했다. 현재 산맥 이름, 실제 고도 Mesh, 운행 가능 경로, 업무 장소는 이 자료에서 파생하지 않는다.

## 검증

- Unity 재컴파일: 완료, 컴파일 오류 없음.
- EditMode: `Ssalddel.Unity.Tests.EditMode.세계지구본Tests` 20/20 통과.
- 실제 Play Mode 상태: `Layer=RegionalTerrain;Stage=Rivers;Terrain=True;Corridor=False`.
- Game View 카메라 증거: `Assets/Documentation/Changes/2026-09-21-korean-peninsula-terrain-preview/game-view-regional-terrain-board-r42.png`.
- 기존 장면에서 서버 세션 부재, Replay hash 불일치, 비활성화 중 부모 변경 등 이 작업과 무관한 오류가 계속 재현됐다. 따라서 Console 0 또는 통합 E5 이상은 주장하지 않고 통합 증거 상한을 E4로 유지한다.
- 화면 공간 UI는 현재 캡처 도구의 카메라 렌더에 포함되지 않으므로, 저장된 PNG는 지형판 렌더 증거이지 전체 UI 증거가 아니다.

## 남은 범위

- 실제 DEM 기반 높이 Mesh와 산맥 이름은 출처·격자·재배포 조건을 동결한 뒤 별도 작업으로 진행한다.
- 권역 거점과 배달권은 서버의 판본화된 상태 사본이 준비된 뒤 연결한다.
- 선택한 행정동·역세권에서 기존 아이소메트릭 디오라마로 넘어가는 전환 연출과 복귀 동작은 후속 수직 구현으로 남긴다.
- commit·push·배포는 수행하지 않았다.

## 디오라마 규칙 점검

이번 변경은 역세권 개별 조립 규칙이 아니라 상위 세계지도 표현 계층을 추가한 작업이다. 기존 역세권 디오라마 규칙을 지지·반박·무효화할 새 근거는 없어 `새 디오라마 규칙 후보 없음`으로 판정한다.
