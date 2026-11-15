# [기획 · 로컬 Blender 제작과 Unity 왕복 과정 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r192]

- 기획 ID: `PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION` · 분야: 모델 제작 절차 · 판본: r192.
- 기준일: 2026-11-14 · 상태: `LocalBlenderChosen / InstallationAndVersionVerified / AuthoringWorkflowDocumented / ModelAndVisualReviewPending / NotReadyToDispatch / ProductUnchanged`.
- 사용자 선택: 로컬 Blender를 활용하는 과정을 기존3D 모델 제작 계획에 명시적으로 넣는다. 모델/시안·Unity 적용을 이번에 실행하라는 뜻으로 확대하지 않는다.
- 상위/계승: [3D 기사·스쿠터 제작 기준 r191](3d-rider-motorcycle-modeling-and-unity-preparation.design.r191.md). r191의 도구 미정 가운데 **로컬 Blender 사용**을 답변 완료로 정밀화하며 외형·치수·모델링 담당·시안/적용 범위 등 나머지 미정은 유지한다.
- 관련 결정·WI·PlayableLoop: 기존 표현/권위/소유 기준 유지, 새 D/WI/Goal 등록 없음. Graph Map: `NoImpact` — 제작 도구/절차의 보충이며 기존 플레이 관계/원본·생성물을 바꾸지 않는다.
- 플레이 순서: 자유. 기존 기사/주문의3D 외형을 준비하는 제작 절차이며 추가 게임 단계/자원·숙련 보상을 만들지 않는다.
- 인계 상태: 제작 절차 준비. 시안/모델 제작물·Unity 적용 명세와 담당 수용은 별도다. 기존 Claude 카메라/지도 개편을 인수하지 않는다.

## 1. 확인한 로컬 Blender

- 실행 파일: `C:/Users/user/AppData/Local/Programs/Blender Foundation/blender-5.2.1-windows-x64/blender.exe`.
- 실제 `--version` 응답: **Blender5.2.1 LTS**, build date2026-08-25, build hash `9e2066aef7ef`.
- 확인 근거: [로컬 버전 조회 기록](../../../../../artifacts/local/blender-authoring-20261114/local-blender.json).
- 설치 경로는 이 PC의 확인값이다. 다른 작업실/PC에서 같은 절대 경로를 가정하거나 프로젝트 설정으로 고정하지 않는다.
- 이번에는 버전 조회만 실행했다. Blender 편집 창·기존 `.blend`·사용자의 작업 세션을 열거나 수정하지 않았다.

## 2. 기존 과정에 넣는 Blender 단계

**외형 시안 → Blender 원본/모델·탑승 자세 → 선별 FBX 출력 → Unity 조립/대조 → 필요 시 Blender 수정·재출력**을 기본으로 한다.

| 단계 | Blender/전달 과정 | 남길 결과·확인 기준 |
| --- | --- | --- |
| 작업 사본 | 승인한 한 조합의 제작용 `.blend`를 별도로 준비. 원본 Synty Prefab/FBX/atlas는 보존 | 원본 출처/판본과 신규/수정 범위, 작업 사본. 기존 파일을 덮어쓰지 않음 |
| 차체 모델링 | 생활형 스쿠터의 차체·안장·발판·핸들/앞바퀴 조향부·바퀴를 분리해 제작 | 위/옆/사선에서 읽히는 실루엣, 회전 부품의 중심/피벗 |
| 배송함·헬멧 | 차체 뒤 보온 배송함과 주행용 헬멧을 별도 소품으로 제작 | 부착/분리 위치와 가림/크기, 기존 범례에 맞는 색/재질 |
| 기사 탑승 | 기존 기사 몸체 후보를 작업 사본에서 검토하고 좌석·손잡이·발판에 맞춰 자세 조정 | 손/발/안장 정렬·관절/차체 관통, 정적 탑승 자세 또는 검증할 Rig/Clip 구분 |
| 치수/변환 | 미터 치수·전방/상방·배율·원점·피벗을 기록하고 필요한 변환을 검토 | 비리깅 메시와 Armature/스킨을 구분. 리깅/애니메이션에 무조건 일괄 Apply하지 않음 |
| FBX 출력 | 필요한 Mesh/Armature만 선별하고 차체/정적 소품·기사/동작의 전달 범위를 구분 | 카메라/조명·무관한 객체/Action 제외. 실제 내보내기 옵션·축/Scale/Unit·대상 기록 |
| Unity 대조 | 기존 프로젝트 wrapper에서 FBX·재질/텍스처·기사/배송함을 조립 | 실측 크기·접지·방향·손/발/좌석·Avatar/Clip·재질·선택/업무 ID 일치 |
| 수정 왕복 | 차이가 있으면 `.blend` 사본으로 돌아가 수정하고 기록한 설정으로 재출력 | 원인/수정/새 파일 지문, 같은 대상/카메라에서 전후 비교 |

첫 결과물은 r191의 **스쿠터 한 종·기사 한 명·배송함**이다. Blender 사용 선택만으로 모든 차량·사람·건물의 일괄 모델링을 추가하지 않는다. 손·발 연결 지점의 실제 이름/치수와 탑승 Clip은 아직 제작/확정되지 않았다.

## 3. 원본과 전달물을 나눈다

`.blend`는 편집 원본으로 보관하고 **FBX를 Unity 전달물로 사용하는 안**이다. 예시 산출물은 차체/배송함 FBX, 기사/정적 탑승 자세 또는 별도 동작 FBX, 재질/텍스처와 치수·피벗/연결 지점 표, export 설정 기록, 위/옆/사선 검토 이미지다. 실제 파일명/위치·보존 정책은 제작 수용 때 정하며 이번에 파일이 생성된 것으로 표시하지 않는다.

Unity에서 수정할 때도 유료 원본은 보존하고 기존 `VisualRoot`/표현 연결의 프로젝트 변형을 사용한다. 이동은 기존 게임 위치/경로 상태가 맡으며 RootMotion·바퀴/조향 표현이 별도 이동이나 픽업/전달 완료를 만들지 않는다. `.blend` 저장·FBX 생성·Unity import·실제 World 배치/입력 검증은 각각 다른 완료다.

Blender의 [변환 적용](https://docs.blender.org/manual/en/latest/scene_layout/object/editing/apply.html)과 [FBX 전달 참고](https://docs.blender.org/manual/en/5.0/addons/import_export/scene_fbx.html)는 설정 검토의 진입이다. 후자는5.0 문서이므로 확인한 로컬5.2.1의 기본값/호환성을 대신하지 않는다. 축·Scale·Apply Unit·Armature 처리값은 현재 설치판과 시험 산출물의 왕복 결과로 고정한다. 이번에 가져오기/내보내기 동작이나 구체 옵션값을 검증한 것은 아니다.

## 확정·미정·다음 질문

**확정:** 모델 편집/제작 과정에 로컬 Blender를 사용한다. 설치 경로·실제5.2.1 LTS 버전 조회를 확인했다.

**미정:** r191의 외형/시안·제작 담당·원본/전달물 경로·치수/피벗/연결·탑승 Rig/Clip·실제 export/import 설정·Unity 적용/검증.

**다음 질문:** 현재 없음. 모델 제작/적용은 기존 기획·시안/명세 수용 절차를 유지한다. 이번은 과정 추가와 설치 확인이며 새 모델/FBX/Prefab·시안·Unity/제품 코드·게임 실행·자료/DB/수집예약·Goal/WI/E·Graph Map·commit/push는 변경하지 않았다.
