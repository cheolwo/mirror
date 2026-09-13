# [기획·구현 · 용마산역 공간 표현 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r12]

- 판본: `yongmasan-station-spatial-presentation.r1`
- 상태: `Approved / ImplementationAuthorized / LocalPrivateReviewOnly`
- 승인 근거: 2026-09-13 사용자 요청 “용마산역 쪽도 1km 범위로, 면목역이나 사가정역처럼 자료 조사가 필요하면 조사해서 Unity 장면에 드러나게 한다.”
- 상위 기준: [역세권 디오라마 모듈 표준](README.md)
- 검증된 비교 구현: [면목역 공간 표현](myeonmok-spatial-presentation.r5.md)

## 구현 약속

사용자 확인 표시명 `용마산역`, 공식 원천명 `용마산(용마폭포공원)`, 공식 기준점 `37.573752, 127.086802`을 분리해 보존한다. 기준점 중심 정북 방향 1,000m × 1,000m를 독립된 역세권 공간 사본으로 만들고, 공통 역세권 조립기로 canonical `SimulationWorldShell` 안에 읽기 전용으로 표현한다.

## 자료와 결손

1. 이미 동결한 서울 GIS 건물, 전국 표준노드링크, 서울 행정동 경계를 용마산역 창에서 다시 자른다.
2. 표면 자료는 용마산역 한 범위의 OSM API bbox 응답만 한 번 수집해 URL·시각·바이트·SHA-256·ODbL 귀속을 보존한다.
3. 수집 뒤에도 없는 건물·표면·관계 member·높이·도로 폭·보도·법정동 경계·통행 권위는 합성하지 않고 `MissingCoverage` 또는 더 구체적인 결손 코드로 남긴다.
4. 건물 원천 이용조건 충돌이 해소되기 전에는 사본과 화면을 `LocalPrivateReview`, `distributionApproved=false`, `TraversalReady=false`, `GameplayReady=false`로 유지한다.

## 서버와 Unity 경계

- 기존 역 목록·manifest 계약에 용마산 사본 후보의 판본·hash·수량·결손만 연결한다. 독립 station-scoped payload 저장·조회 계약 전에는 `ServerLoadable=false`와 `WaitingForSpatialCoverage`를 유지한다.
- 운영 GPS·주문·배차·개인정보와 Simulation 권위는 포함하지 않는다.
- Unity는 용마산 descriptor가 가리키는 명시적 로컬 사본만 읽는다. 입력이 없거나 거부되면 source 도형 0건의 중립 표현을 사용하고 사가정·면목 도형으로 대체하지 않는다.
- 첫 절편은 건물 매스·도로·표면·행정동·결손 판독과 카메라 framing까지만 검증한다. 전투·몬스터·NPC 이동·NavMesh·옥상 배치·새 공식 Scene은 범위 밖이다.

## 완료 증거

- 범위 제한 원천 수집과 동결 hash
- 결정적 1km 사본 생성·독립 재구성 감사·결손 유지
- 로컬 MySQL 비공개 원천 계보 저장·멱등 재적용·별도 재조회
- 서버 manifest 후보와 집중 시험
- Unity 공통 descriptor·loader·조립·Mesh 시험
- 가능한 경우 실제 Play Mode·Game View 캡처와 canonical Scene 비변경 확인

각 증거는 별도로 보고하며, 하나의 성공을 공개·gameplay·통행 또는 운영 준비 완료로 확대하지 않는다.
