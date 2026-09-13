# [기획·구현 · 면목역 공간 표현 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r5]

- 판본: `myeonmok-station-spatial-presentation.r1`
- 상태: `Approved / ImplementationAuthorized / LocalPrivateReviewOnly`
- 승인 근거: 2026-09-13 사용자 요청 “사가정역과 유사하게 면목역도 1km × 1km로 표현하고, 자료가 없으면 수집한 뒤에도 없으면 남긴 채 구현한다.”
- 상위 기준: [역세권 디오라마 모듈 표준 r5](README.md)
- 첫 비교 기준: [사가정 공간 밀도 표현 r18](../PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/spatial-density-presentation.r18.md)

## 구현 약속

면목역 공식 기준점 `37.588671, 127.087503`을 중심으로 정북 방향 1,000m × 1,000m를 하나의 선택 가능한 역세권 모듈로 표현한다. 사가정역의 자료·화면 구조를 공통화해 재사용하되 사가정 도형, 지도 판본, Region ID를 면목역 자료로 복사하거나 재명명하지 않는다.

## 자료와 결손

1. 이미 동결한 서울 GIS 건물, 전국 표준노드링크, 서울 행정동 경계를 정확한 면목역 창에서 다시 자른다.
2. 면목 전용 표면이 없으면 OSM 공식 bbox `GET`으로 이 한 범위만 제한 수집하고 URL·시각·바이트·SHA-256·ODbL 귀속을 보존한다.
3. 수집 응답에 포함되지 않은 relation member, 높이, 도로 폭·보도, 통행 가능성, 건물·표면 coverage는 추정하지 않는다.
4. 자료가 남아 있지 않은 구역은 `MissingCoverage` 또는 더 구체적인 품질 코드로 보존하고 실제 공터·공원·통행로로 해석하지 않는다.
5. 건물 원천 이용조건 충돌이 풀리기 전 모든 파생은 `LocalPrivateReview`, `distributionApproved=false`다.

## 서버 경계

- 역 metadata, 지역 패키지, 행정동 manifest/tile의 기존 읽기 경로를 재사용한다.
- 면목 전용 Region과 실제 사본을 사가정 Region에 섞지 않는다.
- tile summary의 격자 교차만으로 부분 coverage를 인정하지 않는다. 실제 tile payload의 schema·행정동/투영/tile ID·hash·index·bounds와 창 안 건물 또는 도로 교차를 검증한 뒤에만 `PartialCoverage` 후보가 된다.
- 새 사본이 없거나 검증이 실패하면 면목역은 `WaitingForSpatialCoverage`를 유지한다.
- 운영 상태, GPS, 주문, 배차, 개인정보를 공간 사본에 넣지 않는다.

## Unity 표현 경계

- canonical `SimulationWorldShell` 안에서 역 descriptor/profile로 면목 모듈을 조립·해제한다. 새 공식 Scene은 만들지 않는다.
- 공통 카메라·chunk·건물 massing·도로 위계·표면·결손 판독과 자원 회수 규칙을 사용한다.
- 사가정 로컬 자산이 없거나 면목 입력이 거부되어도 사가정 도형으로 대체하지 않는다.
- 첫 화면은 자료 기반 공간 관찰만 검증한다. 전투·몬스터·NPC 이동·NavMesh·옥상 배치 상태 변경은 포함하지 않는다.

## 완료 증거의 분리

- 원천 수신·hash와 권리 기록
- 로컬 MySQL 원천 계보 저장·동일 입력 중복 방지·독립 재조회
- 면목 `LocalPrivateReview` 공간 산출물과 독립 감사
- 서버 계약·실제 tile payload 검증·집중 시험
- Unity 컴파일·EditMode 조립/해제 시험
- 가능할 때 Play Mode·Game View의 1km 범위·밀도·도로·높이·결손 판독

한 단계의 성공을 다른 단계의 완료로 바꾸지 않는다. 실제 Game View가 없으면 코드·시험 완료까지만 보고하고, 권리 검토 전 산출물과 화면은 공개 자산으로 승격하지 않는다.
