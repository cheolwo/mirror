# 주행 영상 근거와 건물별 외관 보완 첫 구현

[기획 · 영상·디오라마 외관 연결 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r109]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / FirstSliceImplemented / PrivateStudyVerified / CanonicalBindingPending / OperationalDisabled`
- 승인: 사가정·면목제3·8동 현황과 건물별 외관 보완 제안에 대한 사용자 “한번 그렇게 작업을 진행해 줘봐.”
- 계보: [영상·공간 연결 r2](video-diorama-play-experience.proposal.r2.md), [기존 자료 제안 r44](../PLAN-SYSTEM-FRANCHISE-OPERATIONS/diorama-public-data-refinement.proposal.r44.md), [역세권 증거 관리](../../../../Architecture/역세권디오라마증거진화체계.md).

## 승인 범위와 책임

이번 요청은 기존 Unity 보류를 **비공개 영상 근거·외관 표현의 좁은 범위**에서 재개한다. 운영 지도 전체·경영 게임·학습·실제 배송·새 Scene·배포·commit/push를 재개하지 않는다. 주 책임은 Unity 표현, 원본 관측과 표현 입력 연결은 Integration이다.

배달 작업공간은 원본 SHA·실제 PTS·프레임·보호 근거·공개 장소/건물 연결 후보를, Hongdal은 기획·근거 기준을, 별도 Unity checkout은 같은 공간 ID·판본의 외관 표시를 소유한다. 원본 경로·고객 정보는 Unity용 계약에 내리지 않는다. 기존 지도 승인 DB·Premiere 마커를 변경하지 않는다.

## 확인한 공백

1. 사가정 View의 Synty 분기는 공간 composition을 null로 만들고 후속 상세 overlay를 표시하지 않는다. 기존 602건물 바탕과 후속 4,062 화면 대상은 다른 판본이므로 ID 대조 없이 대체하지 않는다.
2. 지도 JN 선분·영상 SHA/PTS와 Unity 도로/건물 ID의 직접 대응은 미완성이다. 13개 Synty 시안도 개별 건물 외관 동기화 완료가 아니다.
3. 기존 0053~0055 사가정 촬영 준비 자료는 후보이며 건물 결속·외관 사용 승인이 완료된 자료가 아니다.

## 실행 순서와 완료 기준

| 순서 | 실행 | 완료 기준 |
| --- | --- | --- |
| 1 | 공개 거리 외관 후보 최대 3개 선정·실제 프레임 대조 | 원본·실제 PTS·프레임 hash·보호 대조·장소/건물 연결 강도·미확인 기록. 확인 못한 수량은 보류 |
| 2 | Synty와 상세 공간 조합 보완 | overlay 배치·ID 보존. 대응 없는 옛 Synty 대체는 보류하고 원문 표현 유지. 기존 경로 회귀 없음 |
| 3 | 건물별 외관 입력·국소 표시·복구 | 정확 ID·공간 hash·비공개 검토 상태·허용 모듈/자산 키·유효 transform 검사. 후보·오래된 판본·보호 겹침·없는 대상 거부. 원본 mass·선택 ID 불변 |
| 4 | 확보 수준에 맞는 비공개 비교 결과 | 실제 장면·현재 모델·보완 후보 구분. 컴파일/시험과 실제 Play Mode/Game View 별도. 건물 결속 미해결이면 해당 적용만 보류 |

층수·높이·필지·통행 권위를 만들지 않는다. 앞선 참고 경로의 임의 목적지 점은 건물 배치에 사용하지 않는다. 보이지 않는 뒷면·지붕·정밀 치수를 채우지 않는다. 기존 사람의 지도 승인·공개 권리를 비공개 외관 검토가 대신하지 않는다.

## 허용 경로와 검증

- 허용: 배달 `premiere-control/diorama_facade_evidence.py`·집중 시험, 새 `edit-kit/private/diorama-facade-refinement/20261026-r1/`, Unity 사가정 View·단일 책임 외관 helper/계약·비공개 모듈 wrapper·집중 EditMode 시험, 이 기획·목차·현재 작업·변경 기록.
- 금지: 원본·기존 지도·승인 DB·사용자 컷/마커 덮기, Synty 원본 Prefab/Material/GUID 변경, 운영 DB·권위 상태 반영, 새 package·학습·공개 배포.
- 검증: 입력/프레임 hash와 판본, 후보 적용 거부, 원문 배치 불변, 부재/중복/오래된 입력·복구·cleanup, 관련 사가정 표현·공간 선택 회귀. 실제 Editor 연결과 CLI 결과를 별도로 남긴다.

## E1~E7 영향과 마감

새 Goal·WI·E 승격을 만들지 않는다. E1은 표현/권위 분리, E2는 helper·입력, E3은 판본·거부/복구 시험, E4는 허용 외관 결속, E5는 같은 공간 ID/판본 실제 대상, E6은 원본 시각·개인정보·권리 비교, E7은 실제 실행한 입력·반환·cleanup으로 나눈다. 코드/시험만으로 E5~E7을 완료 처리하지 않는다.

종료 시 별도 변경 기록에 실제 결과·미검증을 연결한다. 반복 가능한 새 규칙을 확인하지 못하면 `새 디오라마 규칙 후보 없음`으로 마감한다.

## 첫 구현 마감 (2026-10-26)

관련 Unity EditMode 98/98·Python 집중 시험 25/25를 통과했다. 실제 공개 원형을 사용한 비공개 연구 3건의 전후·복구 PNG 9장을 대조했고, 제거 뒤 세 이미지 모두 보완 전 SHA와 일치했다. 관측한 상호는 정적 간판 이미지로 표시했다. 이는 별도 카메라 렌더이며 Game View·Play Mode 입력·성능 검증이 아니다.

현재 Overture 2,247건물과 세 GIS 대상의 직접 ID 대응은 0건이다. 과거 ALD 사본은 조사한 로컬 위치에 없어 정본 적용은 보류했다. 연구 높이 4m·최장면 정면은 기호/가설이며 실측하지 않았다. 원본·Scene·원본 Synty·사용자 기록·운영 DB는 보존했다. [변경·검증 기록](../../../../Changes/2026-10-26-sagajeong-video-facade-r109.md), 로컬 결과는 `배달/edit-kit/private/diorama-facade-refinement/current-review.json`을 따른다. 새 디오라마 규칙 후보 없음.
