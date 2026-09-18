[기획 · 월드·공간·표현 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r31]

# 사가정역 1~4번 출구 Blender 상세화 순서

- 상태: `PlanningSequenceConfirmed / ExitOrderOneToFour / PerExitEvidenceGateRequired / BlenderPlanningPaused / ImplementationNotAuthorized`
- 대상: `station:kr:kric:s1107:0722`
- 선행 통합: [사가정역 출구 Blender 상세화 기존 기획 통합 r30](sagajeong-station-blender-integration.r30.md)

## 확정

사가정역 출구 상세화는 `1번 → 2번 → 3번 → 4번` 순서로 진행한다.

- 각 출구는 독립 `StationLandmarkEvidence`, 모델 브리프, Blender 원본·FBX·Prefab revision을 가진다.
- 앞 출구가 완료됐다는 이유로 다음 출구의 외관·방향·치수·권리 결손을 복사하지 않는다.
- 출구 1·2는 기존 정확 사진 후보의 CC BY-SA·내용·현행성 관문을 통과해야 한다.
- 출구 3·4는 정확 외관 근거를 새로 확보하기 전 r26 일반화 표식을 유지한다.
- 출구마다 안전 교체·LOD·bounds·hash·원상복구를 확인한 뒤 다음 번호로 넘어간다.
- 네 출구의 모델링 완료는 통행 가능성·NavMesh·Collider·운영 권위나 E 승격을 뜻하지 않는다.

## 완료 순환

```text
출구 N 근거·권리 승인
  → 모델 브리프 동결
  → Blender LOD 제작·검증
  → Unity 후보 Prefab 검증
  → 기존 일반화 표식과 안전 교체
  → 실패 시 즉시 원상복구
  → 실제 Game View 별도 확인
  → 다음 출구 N+1
```

현재는 순서만 확정했다. CC BY-SA 정책, 출구별 승인 자료, Blender·FBX·Prefab·Unity 실행은 열지 않았다. 사용자의 요청에 따라 Blender 관련 추가 문답은 여기서 일단 멈춘다.
