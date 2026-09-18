# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 확정 r16]

## 승인 근거

2026-09-16 사용자는 r15의 다음 질문에 따라 동북서울 30개 행정동 디오라마의 첫 실제 자료 수집 절편을 고정하고, 그 고정을 디오라마 증거 체계에 포함하도록 승인했다.

## 확정한 수집 관문

- 관문 고유 식별자: `collection-gate:administrative-dong-diorama:current-boundary-building-entrance.r1`
- 대상 범위: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 행정동 30개
- 상태: `FixedPlanningGate / CollectionNotStarted / ApplicationBlocked`
- 순서:
  1. `CurrentAdministrativeBoundary`: 최신 월 주소 전자지도의 `TL_SCCO_GEMD`
  2. `AddressBuildingAndBuildingGroup`: 같은 주소정보 배포 세대의 건물·건물군 자료. 건물 도형 후보는 `TL_SPBD_BULD`이며 건물군의 정확한 제품·레이어 식별자는 실제 제공 목록에서 확정한다.
  3. `BuildingEntrance`: 주소·장소지능 출입구 자료. 정확한 제품·레이어 식별자는 실제 제공 목록에서 확정한다.

앞 단계의 원본·판본·hash·좌표계·범위 검증이 끝나지 않으면 뒤 단계를 `current` 결속으로 승격하지 않는다. 세 자료가 한 파일이라는 뜻은 아니다. 하나의 수집 영수증 묶음 아래 각 원본 영수증을 보존하고, 기준월이 다르면 불일치 진단과 사람 검토 없이 결합하지 않는다.

## 고정의 의미

- 후속 자료 수집 스레드는 임의로 필지·도로·사업장·NPC부터 시작하지 않고 이 관문을 첫 공간 정본 절편으로 대조한다.
- 공식 내려받기에서 확인한 실제 파일명·기준월·byte 길이·SHA-256·CRS·이용조건을 기록한다.
- `TL_SCCO_GEMD`는 정확 30개 행정동 코드의 존재·유일성·유효 도형·겹침·틈·코드 crosswalk를 검사한다.
- 건물·건물군은 행정동 경계와 별개 원장으로 저장하고 건물관리번호 등 공식 연결 키를 보존한다.
- 출입구는 건물 또는 건물군에 명시적으로 연결될 때만 후보가 된다. 가까운 점이라는 이유로 자동 결속하지 않는다.
- 로그인 없이 공식 원본을 받을 수 있으면 D429 자료조사 파이프라인에 따라 비공개 수집·저장·독립 재조회까지 진행할 수 있다. 로그인·승인·별도 반출이 필요하면 `BlockedExternalAccess`로 중단한다.

## 고정이 의미하지 않는 것

- 원본을 이미 수집했다는 뜻이 아니다. 현재 세 요구사항은 모두 `NotCollected`다.
- `TL_SCCO_GEMD` 하나로 건물·필지·출입구·보도·통행이 확보됐다는 뜻이 아니다.
- 웹 페이지의 갱신일이나 설명을 실제 원본 기준월·hash로 대신하지 않는다.
- 자료 수집 완료가 `current` 게시, 공개 배포, Unity 적용, 통행, 배달, Simulation 또는 gameplay 승인을 만들지 않는다.
- 건물군·출입구의 정확한 레이어 식별자가 확인되기 전에는 추정 이름을 확정 계약에 넣지 않는다.

## 증거 체계 결속

- [역세권·행정동 디오라마 증거 진화 체계](../../../../Architecture/역세권디오라마증거진화체계.md)에 `행정동 수집 관문` 기록 유형을 추가한다.
- 기계 대장 `station-diorama-evidence-rules.r4`의 `administrativeAreaCollectionGates`가 이 관문을 소유한다.
- 대장은 정확 30개 scope, 세 단계 순서, 공식 링크, 미수집 상태, null 원본 hash, `applicationAuthorized=false`, `currentPublicationAllowed=false`를 검사한다.
- 이 고정은 새 디오라마 보편 규칙 후보나 E 단계 승격이 아니다. 실제 원본을 확보하면 각 자료의 `SourceReceipt`와 별도 후보 판본을 추가한다.

## 무효화·개정 조건

다음 중 하나가 발생하면 기존 관문을 조용히 수정하지 않고 새 revision으로 검토한다.

- 주소정보누리집이 `TL_SCCO_GEMD` 제공을 종료하거나 공식 대체 정본을 지정함
- 건물·건물군·출입구 제품이 통합·분리되어 공식 연결 키나 갱신 주기가 바뀜
- 30개 행정동 scope가 변경됨
- 동일 기준월 결속이 불가능하여 독립 판본 결합 정책이 필요해짐
- 권리·개인정보·정확 위치 공개 기준이 변경됨

## 검증 상한

이번 r16은 `FixedPlanningGateRecordedAndMachineValidated`까지만 목표로 한다. 외부 다운로드, 원본 hash, DB 저장, 행정동 재투영, API, Unity, Scene·Prefab, Play Mode·Game View, E 승격, commit·push는 별도 사실이다.

## 다음 질문 하나

고정된 1단계 `TL_SCCO_GEMD`에 대해 로그인 없는 공식 다운로드 가능 여부를 확인하고, 가능할 때만 실제 원본 수집으로 진행할 것인가? 추천은 `진행`이다.
