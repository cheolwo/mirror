[기획 · 월드·공간·표현 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r30]

# 사가정역 출구 Blender 상세화 기존 기획 통합

- 상태: `PlanningIntegrated / SagajeongStationFirstConfirmed / ExistingEvidenceAndBindingReused / CCBYSAProfilePending / BlenderWorkBlocked / UnityReplacementBlocked`
- 첫 대상: `station:kr:kric:s1107:0722` 사가정역 출구·방향 기준 구조물
- 상위 기획: [역세권 디오라마 모듈 표준 r30](README.md)
- 소비 기획: [가상 권역 운영자 시뮬레이션 r46](../PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/README.md)

## 통합 결론

새 Blender 체계를 만들지 않는다. 다음 기존 기획과 구현 결과를 한 파이프라인으로 재사용한다.

| 기존 자료 | 그대로 소유하는 책임 | 현재 상한 |
| --- | --- | --- |
| [실제 외관 근거 기반 건물 교체 r8](building-visual-replacement-proposal.r8.md) | `StationLandmarkEvidence`, `StationLandmarkBinding`, LOD·hash·bounds 검증, `KeepProceduralMass` 복귀 | 제안, 첫 교체 건물 미정 |
| [공공 사진 이용 정책 r9](photo-source-and-building-model-use-policy.r9.md) | 사진·파생 모델·게임 배포·홍보 권리의 분리 판정 | 개별 자산 승인 필요 |
| [공공 사진 첫 수집 r11](public-photo-collection-result.r11.md) | 사가정역 정확 사진 5건의 원본·라이선스·hash와 비공개 원장 | 출구 1·2 사진은 CC BY-SA, 내용 검토 대기 |
| [출구 방향 기준점 r26](sagajeong-station-exit-orientation-anchors.implementation.r26.md) | 출구 1~4 위치·번호·ENU 좌표와 일반화 절차적 표식 | 위치 후보, 실제 방향·통행 권위 없음 |
| [사가정시장 랜드마크 r6](sagajeong-market-landmark-collection.r6.md) | 시장 입구·대표 골목의 별도 공간·권리 관문 | Blender·Unity 차단 |

## 첫 대상과 순서

첫 상세화 묶음은 사가정역 출구 주변이다. 시장 입구·대표 골목은 기존 후보를 폐기하지 않고 두 번째 묶음으로 유지한다.

```text
r26 출구 1~4 위치·번호 기준점
  + r11 정확 사진·권리 영수증
  + r8 공간·외관 근거 및 안전 교체 계약
  + r9 자산별 배포 권리 판정
      ↓
사가정역 출구 모델 브리프 후보
      ↓ 권리·현행성·방향 관문
Blender LOD 원본과 FBX
      ↓ 기술·배포 관문
기존 출구 절차적 표식의 VisualRoot 선택 교체
```

출구 1·2는 정확 외부 사진 후보가 있지만 `CC BY-SA 4.0`의 출처·변경 고지·동일조건변경허락 범위를 먼저 확정해야 한다. 출구 3·4는 정확 외관 근거가 아직 없으므로 1·2의 사진이나 다른 역 외관을 복제하지 않고 r26의 일반화 표식을 유지한다.

## 소유권과 중복 금지

- 역세권 디오라마 기획이 사진·모델 근거, Blender 브리프, `VisualKey`, Prefab 안전 교체와 fallback을 소유한다.
- 면목제3·8동 운영 기획은 건물·역 기준점을 생활상 관찰에서 소비할 뿐 별도 사진 대장이나 Blender 계약을 만들지 않는다.
- Graph Map은 기존 `CandidateLandmarkFor`, `LocatedIn`, `DerivedFrom`, `PresentationOf` 관계 후보를 재사용한다.
- r26의 출구 안정 식별자와 좌표를 유지하며 모델 파일명·FBX node 이름을 업무 식별자로 쓰지 않는다.
- 시장은 여러 필지·점포의 공간 overlay 후보이고 역 출구 구조물과 같은 단일 건물 교체로 취급하지 않는다.

## Blender·Unity 불변 조건

- `1 Unity unit = 1m`, 지면 pivot, 북쪽·정면 방향, LOD0~2와 원본/FBX hash를 manifest에 기록한다.
- 확인하지 못한 뒤편·옥상·재질은 `Estimated` 또는 `MissingVisualEvidence`로 남긴다.
- 사람·차량 번호판·상가 간판·로고·벽화는 원본 텍스처로 복제하지 않고 승인 범위에 따라 제거·일반화한다.
- 기술 검증과 배포 권리 승인을 분리한다.
- 자원 누락, hash·판본·bounds 불일치, 권리 철회 때 r26 일반화 절차적 표식으로 즉시 복귀한다.
- 상세 모델은 Collider·NavMesh·통행·운영 권위를 자동 생성하지 않는다.
- 새 Scene을 만들지 않고 canonical `SimulationWorldShell`의 기존 역세권 모듈에서만 검증한다.

## 현재 차단과 가장 이른 재개점

1. 출구 1·2 CC BY-SA 파생 자산의 공개·출처·동일조건변경허락 단위를 승인한다.
2. 사진의 현행성, 사람·간판 내용과 실제 출입 방향을 검토한다.
3. `StationLandmarkEvidence`와 모델 브리프를 출구별로 동결한다.
4. 그 뒤에만 Blender 작업을 `WorkAuthorized`로 연다.

현재는 기획 통합만 완료했다. Blender 제작·FBX·Prefab·Scene·Play Mode·Game View·E 승격·배포 승인은 수행하지 않았다.

## 다음 질문 하나

첫 Blender 기술 표본은 어떻게 잡을까?

1. `출구 1·2를 한 쌍으로 검토하되 1번 출구 하나로 교체 파이프라인을 먼저 닫기` — 추천. 같은 라이선스 관문을 함께 검토하면서 실패 범위를 한 구조물로 제한한다.
2. `출구 1~4를 모두 한 번에 제작`: 통일감은 좋지만 3·4번 외관 근거 결손을 추정으로 메울 위험이 있다.
3. `출구 모델링보다 주변 독립 건물 한 동을 먼저 교체`: r8 기술 검증에는 유리하지만 사용자가 정한 사가정역 우선순위와 어긋난다.
