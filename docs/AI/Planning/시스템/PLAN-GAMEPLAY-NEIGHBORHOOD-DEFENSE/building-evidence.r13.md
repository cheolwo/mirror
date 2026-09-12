# [기획·구현 · 건축물대장 재수집 · PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE · r13]

- 상태: `AdditionalCollectionStoredAndVerified / AddressParcelCandidates / UnityApplicationPending`.
- 승인 근거: 사용자가 건축HUB 활용신청 완료를 알리고 기존 인증키로 재시도를 요청했다.
- 이전 기준: [대장 r12](building-evidence.r12.md). 기존 지도602개 대장·과거 HTTP403 기록은 보존하고 추가 수집 결과를 별도 판본으로 연결한다. [높이 표현 r11](building-approximation.r11.md)의 자료/추정/임시 구분은 유지한다.
- 실행 범위: 앞서 동결한 동일한 3필지의 표제부 조회·공개 필드 보존·주소/필지 대조·기존 로컬 DB 저장·독립 재조회. 지역 전체 추가 수집이나 Unity 변경은 포함하지 않는다.
- Graph Map/배치 맵: 자료 검토만 진행. 새 노드·WI/H·개발 Goal·Scene·승인 hash 변경 없음.

## 재시도 결과 — 2026-09-12

기존 UserSecrets의 해당 서비스키로 인증을 재시도해 **3필지 모두 정상 응답, 각1건/총3건**을 확보했다. 첫 요청을 확인한 뒤 나머지 두 필지를 순차 조회했다. 이전403 원인을 키 오류로 단정하지 않으며 기존키를 노출·교체하지 않았다.

| 도로명주소 | 대장 높이 | 지상층수 | 처리 |
| --- | ---: | ---: | --- |
| 동일로96길 51 | 8.3m | 3 | 출처 높이 검토 후보 |
| 사가정로39길 79 | 원문0 | 5 | 높이는 결손/null, 층수 추정 정책 대기 |
| 동일로92길 57 | 19.1m | 5 | 출처 높이 검토 후보 |

- 세 응답의 `newPlatPlc` 정규화 주소와 `sigunguCd/bjdongCd/platGbCd/bun/ji` 필지가 기존 선택과 일치했다. 각 필지 응답은1건이고 기존 OSM 후보도1개다. 상태는 `RoadAndParcelAgreeCandidate`이며 건물 윤곽·공식 건물관리번호 간 교차 연결이 완료된 것은 아니다.
- 건물관리번호, 건축HUB `mgmBldrgstPk`, OSM ID는 서로 다른 식별자다. 접두어/문자열 유사성으로 동일 건물임을 확정하지 않는다.
- 공식 명세의 `heit`는 높이(m)다. 기존 수집기의 공개 필드 선택에 `heit`를 추가했다. 지하층 정보는 보존만 하며 지상 높이에 더하지 않는다.
- `crtnDay=20220813`은 원문 레코드 생성일이다. 오늘 수신한 응답을 오늘 현장 측량 자료로 표현하지 않는다. 원문 높이 역시 실측 정확도 보증이 아니다.
- 공급처: [국토교통부 건축HUB 건축물대장정보](https://www.data.go.kr/data/15134735/openapi.do), `getBrTitleInfo`. 재수집한 메타데이터에서 이용허락범위 제한 없음을 확인했다. GIS 자료의 기존 이용조건 검토보류는 별개로 남는다.

## 구현과 저장

- 기존 `사가정건물자료대장`에 `sagajeong-building-retry-approved`를 추가했다. 과거 수집 결과를 덮어쓰지 않고 `retry-after-approval-r1/`에 동일 선택·이전 응답hash·사용자 재시도 근거를 남긴다. 같은 재시도 폴더가 존재하면 재요청을 거부하며 자동 재시도하지 않는다.
- `사가정건축물대장검토.cs`의 `sagajeong-building-register-self-test/preview/apply/verify`로 새 수집 결과를 검토·저장한다. 원본 선택/메타데이터/응답 hash, 필지·중복 PK·응답건수를 검사한다.
- 저장 대상: 현재 Docker/Compose·프로젝트 경로·포트·DB·비root 사용자를 재확인한 `hongdal-mysql-1 / hongdal_dev`.
- 기존 `public_data_raw_snapshots`의 공개 선택 필드 응답 계보44~46과 `public_data_normalized_records` 3건(ID90738~90740)을 저장했다. 첫 신규3, 동일 입력 재처리 신규0/기존3/갱신0, 독립 명령·DbContext 재조회3을 확인했다.
- 정규화 수치는 8.3 / null / 19.1m, 검토 상태 `PendingHumanReview`다. 원문0과 지상층수는 payload에 보존한다. 정규화 시간은 수집 시각임을 표시하고 원본 관측 시각은 null로 남겼다.
- 기존602개 연결 대장과 건축물 표제부/사업체 Assignment 원장은 덮어쓰지 않았다. 추가 `height-review.json`이 원래 OSM 건물 후보와 새 응답을 연결하며 기존 Unity 지도 hash는 그대로다.
- 실제 파일: `artifacts/local/public-data/sagajeong-building-ledger-r1/retry-after-approval-r1/`의 `acquisition.json`, `building-lot-01~03.json`, `height-review.json`, `register-apply-*.json`, `register-verify-*.json`.

## 검증 / 남은 작업

- 도구 build 경고0/오류0. 새 검토 시험8개, 기존 대장19개·공간 결속19개 회귀 통과. 주소/필지 불일치, 복수 후보, 0높이, 검토 상태·Unity 적용 금지를 확인했다.
- 범위 Fast/Task와 문서 링크 검사, 별도 PowerShell의 응답 원문↔검토 결과3건 대조도 통과했다(`artifacts/local/validation/20260912-220551`). 공용 검사는 eng 경로를 문서류로 분류하여 build/test를 생략했으므로 위 도구 build·시험을 별도로 수행했다.
- 실제 수집과 DB 저장·재조회는 완료. Unity 높이 적용·층수 환산·Play Mode/Game View는 미실행이다. 코드 commit/push도 하지 않았다.
- 다음 구현 범위는 확보한 두 높이의 지도 건물 대응을 검토하고 기존 지도 판본/해시 소비자·옥상 표식과 함께 반영하는 작은 묶음이다. 5층/높이 미상 건물은 산식 결정 전 임시 높이를 유지한다.
