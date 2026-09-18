# [기획 · 운영·월드 결속 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 사가정역 Blender 기획 결속 r46]

- 기준일: 2026-09-16
- 상태: `Draft / SagajeongStationFirstConfirmed / ExistingStationPlanIntegrated / NoDuplicateAssetAuthority / BlenderAndUnityBlocked`
- 표현 정본: [사가정역 출구 Blender 상세화 기존 기획 통합 r30](../PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-station-blender-integration.r30.md)
- 생활상 기준: [면목3·8 공공자료 매스·Blender 상세화 r45](myeonmok38-public-mass-blender-detail.r45.md)

## 확정

- 첫 Blender 상세화 묶음은 사가정역 출구 주변으로 한다.
- 새 사진·모델·Prefab 대장을 만들지 않고 역세권 디오라마 기획 r8·r9·r11·r26·r30을 정본으로 재사용한다.
- 운영자 Simulation은 승인된 `VisualKey`와 건물·출구 안정 식별자를 읽기 전용으로 소비한다.
- 출구 상세화 여부가 음식배달 상태, 행정동 귀속, 통행 가능성이나 주문 완료를 바꾸지 않는다.
- 권리·현행성·방향 근거가 없는 출구는 기존 일반화 표식을 유지한다.
- 사가정시장은 다음 상세화 묶음 후보로 보존하되 시장 사진·공간 관문이 닫히기 전 모델링하지 않는다.

## 생활상과의 결속

사가정역 상세 모델은 점심 정상 배달의 출발·도착 건물이 아니라 우선 방향 기준점으로 사용한다. 기사가 역 주변을 지날 때 위치를 읽기 쉽게 하지만 경로 graph의 node나 자동 배차 기준이 되지 않는다.

상세 모델이 준비되지 않아도 공공데이터 매스와 r26 일반화 출구 표식으로 정상 7단계 검증을 계속할 수 있다. 상세 모델이 승인되면 같은 안정 식별자의 `VisualRoot`만 교체한다.

## 미정

- CC BY-SA 파생 자산의 공개·고지 단위
- 출구 1·2 사진의 현행성·사람·간판 검토
- 출구별 실제 방향·치수와 모델 예산
- 첫 기술 표본을 1번 출구 하나로 제한할지

## 다음 질문 하나

첫 Blender 기술 표본을 `1번 출구 하나`로 제한하고, 2번 출구는 같은 권리 검토 묶음의 다음 후보로 둘까?

추천은 그렇다. 출구 1·2의 권리 관문을 함께 정리하되 첫 안전 교체·fallback·LOD 검증은 한 구조물로 닫을 수 있다.
