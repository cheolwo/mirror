# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r2]

## 목표

서버가 검토·게시한 행정동별 공간 사본을 Unity가 읽기 전용으로 받아, `SimulationWorldShell`에서 행정동별 디오라마를 선택·관찰할 수 있게 한다. 첫 범위는 면목제3·8동(`region:kr:hjd:1126057500`)이다.

면목동 실제 자료 완결은 기능 생활권 구현보다 먼저 닫아야 하는 필수 선행 관문이다. 기능 생활권은 이 관문이 동결한 행정동 귀속·투영 hash를 입력으로만 읽으며 행정동 경계나 건물 귀속을 다시 추측하지 않는다.

## 확정

- 기존 면목동 법정동 공간 패키지(`region:kr:bjd:1126010100`)는 이름과 권위를 유지한다. 행정동으로 바꾸거나 덮어쓰지 않는다.
- 공식 행정동 경계의 출처 판본과 SHA-256이 일치할 때만 500m 타일 투영을 게시한다. 경계가 없으면 `WaitingForAdministrativeBoundary`이며 임의 도형을 만들지 않는다.
- Graph Map은 고유 식별자·지역 포함·출처 관계를, 배치 Map/디오라마 투영은 좌표·경계·건물·도로·타일을 소유한다.
- MongoDB에는 hash별 불변 manifest·tile·공개 표시 사본을 저장하고 현재 판본 포인터만 갱신한다.
- RDB의 공개 인허가 사업장과 검토 상태가 표시 후보의 근거다. 실제 사업장 주장·운영자 검토·후원 캠페인은 별도 원장으로 두며 공공자료와 합치지 않는다.
- 후원은 명시적인 `광고` 또는 `후원` 배지와 상세 카드만 허용한다. 건물 크기, NPC 수, 주문·배차·퀘스트·평판·순위·오행 분류에 영향을 주지 않는다.
- Unity는 인증 GET만 사용하며 로컬 저장·Command·업무 확정을 하지 않는다. 행정동에 명시적으로 연결된 `SemanticPlaceStableId`만 운영 상태와 결합하고, 나머지는 복제하지 않고 미해결 수로 남긴다.
- 기능 플래그 `AdministrativeDongDioramaObservation`, `LocalDioramaSponsorship`은 기본 비활성이다. 후원 플래그는 디오라마 관찰 플래그가 함께 켜져야 열린다.
- `ActualAdministrativeDongDataGate`는 공식 경계 원본 hash·행정안전부 코드/법정동 관할 대조·면목동 공간 후보 전수 귀속·동일 입력 결정성·Mongo 불변 게시·독립 재조회가 모두 통과해야 `Closed`다. 이 관문과 무관하게 광고 승인, Unity Scene 배치, 기능 생활권 플레이 규칙은 자동 승인되지 않는다.

## 현재 구현

- 공용 계약: manifest, 500m tile, 건물·도로, 출처·품질, 공개/후원 표시 overlay.
- 서버: GeoJSON 행정동 코드·경계 판독, 사가정 ENU 좌표 변환, 경계 자르기, 결정적 hash, Mongo 불변 게시·현재 판본 조회, ETag 포함 인증 API.
- API:
  - `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-manifest`
  - `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-tiles/{tileStableId}`
  - `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/display-overlays`
- Unity 데이터 계층: GET 전용 Client, Unity JSON decoder, manifest/tile/overlay 판본 해석, 명시적 의미 장소 기준 운영 상태 결합, 메모리 삭제.
- 공개 사업장 표시와 후원 표시를 분리했고, 광고 고지·활성 기간을 충족하지 않는 후원 항목은 서버에서 제외하며 Unity도 다시 거절한다.
- RDB에는 공개 인허가 사업장과 연결되는 표시 Claim 및 후원 캠페인 원장을 추가했다. 신청자와 운영자 검토를 분리하고 예상 revision으로 경쟁 변경을 막으며, 검증된 Claim·승인된 기간·명시적 광고 고지를 모두 충족한 항목만 표시 조회 결과가 된다.
- 동결된 서울시 행정동 경계와 기존 사가정 참고 지도를 사용하는 읽기 전용 `preview`에서 면목동의 6개 행정동 경계를 판독하고 건물 602개를 미해결·경계 중복 없이 귀속했다. 첫 대상 면목제3·8동에는 건물 250개, 경계 안으로 자른 도로 선분 722개, 500m 타일 3개가 생성됐다. 공개 사업장 관측 5,411개도 좌표 판정까지 마쳤지만 표시 승인을 만들지 않아 지도 표시는 0개다.
- 서울시 `OA-22160` 경계 원본은 `EPSG:5181`, 자료갱신일 `2026-06-11`, SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`, 공공누리 제1유형으로 동결했다. Shapefile Adapter가 서울시 8자리 코드를 행정안전부 10자리 코드와 대조해 사가정 ENU 좌표로 변환한다.
- 로컬 `ssalddel_dev`에 면목제3·8동 manifest·3개 tile·빈 overlay와 6개 동 전수 귀속 감사자료를 실제 저장했다. 투영 hash는 `d1357bea67e649bd7a930cdac2de51f4a716f423181d917da3aafadba9780f2a`, 귀속 감사 hash는 `BA174BE2E7713D5BBE65F5C1B41DEF8FA6629B285F443FF2FEE9CCF2734110F0`이며 동일 입력 재적용과 별도 Mongo 연결 재조회를 통과했다. OSM 후보는 공식 건축물대장 행으로 쓰지 않았고 MySQL은 코드·상권 관측을 읽기만 했다.

## 미정·후속

- 사업장 Claim·운영자 검토·후원 캠페인의 사용자/관리자 HTTP API와 별도 감사 Event. RDB Entity·검토 Service·migration은 준비됐지만 실제 DB에는 적용하지 않았다.
- 실제 결제·환불·세금계산·광고 집행 계약. 현재 후원 원장은 표시 승인만 소유하며 금전 효과는 없다.
- canonical `SimulationWorldShell` 조립, 행정동 선택 UI, 실제 Prefab/Scene·Play Mode·Game View 검증.
- 중화동·장안동 등 다음 행정동 확대. 첫 행정동의 공식 경계·타일·표시 검토가 끝난 뒤 같은 계약으로 확장한다.

## 검증 상한

`ActualAdministrativeDongDataGate=Closed`다. 공식 경계·6개 행정동 코드 관할·건물 602개 전수 귀속·도로 2,397개 입력·상권 관측 5,411개 좌표 분석·면목제3·8동 Mongo 판본·귀속 감사자료의 재적용과 독립 재조회까지가 증거 범위다. Unity Scene·실제 Game View, RDB 표시 원장 migration 적용, 운영 서버 HTTP, 광고/후원 표시는 아직 검증하지 않았다.

## 다음 질문 하나

없음. 기능 생활권은 동결된 투영 hash를 선행 입력으로 명시한 좁은 기획부터 진행할 수 있다. 광고·후원 표시는 Claim 검증과 별도 운영 승인이 생기기 전까지 0건을 유지한다.
