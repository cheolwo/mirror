# 2026-11-14 Codex 반환 — Blender 배달 스쿠터 첫 모델

- 요청/수용: 사용자가 r191/r192의 제작 과정을 보고 “한번 만들어 봐”라고 해 로컬 Blender 모델 초안 제작을 진행했다. Unity 게임 적용은 수용하지 않았다.
- 제작 기준: [3D 모델 r191](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/3d-rider-motorcycle-modeling-and-unity-preparation.design.r191.md)·[Blender 과정 r192](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/blender-authoring-and-unity-roundtrip.workflow.r192.md).
- 상태: `BlenderPrototypeCreated / FbxExported / BlenderRoundTripVerified / FourViewsRenderedAndInspected / UserVisualReviewPending / UnityImportPending`.
- 범위: 기존 작업을 덮지 않는 새 로컬 초안. 스쿠터·기사·헬멧·배송함을 새 원형/색 재질로 제작했다. Synty 등 유료 원본을 가져오거나 수정하지 않았다.

## 실제 전달물

보관함/안내 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/README.md`) · 파일 지문/검증 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/verification.json`)

| 파일 | 의미 |
| --- | --- |
| Blender 원본 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/models/hongdal-delivery-scooter-r1.blend`) | 편집 가능한 부품/재질, 별도 검토용 조명/바닥/4카메라. 열면 사선 카메라와 재질 색으로 검토하도록 저장 |
| 전체 조합 FBX (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/exports/hongdal-delivery-scooter-r1.fbx`) | 모델 Mesh/Empty만. 검토 바닥/카메라/조명 제외 |
| 차체 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/exports/scooter-vehicle.fbx`) · 정적 기사 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/exports/courier-static-pose.fbx`) · 배송함 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/exports/insulated-delivery-box.fbx`) | 나눠 조립/검토할 전달물. 정적 기사에 Armature/동작 Clip은 없음 |
| 사선 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/previews/hero.png`) · 측면 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/previews/side.png`) · 정면 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/previews/front.png`) · 지도 시점 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/previews/map.png`) | Blender 렌더4장. 실제 Unity 지도/게임 화면이 아님 |

차체·앞/뒤 바퀴 축·조향부·배송함/뚜껑·정적 탑승 기사·헬멧·좌석/그립/발판/배송함 연결 지점이 분리돼 있다. 편집 Mesh127개/수정자 적용 후 삼각형13,034개이며 게임용 병합/LOD·성능 최적화 완료가 아니다. 가상 조합의 크기는 거울 포함 폭1.04m·길이2.104m·기사 포함 높이1.837m다.

## 검증한 것

실제 Blender5.2.1 LTS의 별도 background 프로세스로 원본 저장·FBX4개 export·렌더4장을 실행했다. 사용자 Blender 창/기존 편집 파일은 열지 않았다. 전체 FBX 왕복 (로컬 검토 자료 `artifacts/local/delivery-scooter-model-20261114/fbx-roundtrip-verification.json`)은 새 Blender 세션으로 다시 읽었다. Mesh127개와 분리 구성·Bounds를 확인했고, 원본과 치수 차이는 최대0.000000235m, 두 타이어의 지면 Z는 약0m, 장갑 중심과 그립 기준점 차이는 약0.013m였다. 검토 바닥/조명/카메라가 FBX에 들어가지 않았고 Armature/Clip도 없음을 확인했다.

네 렌더를 직접 봐서 실루엣·프레임·보이는 손/발/안장/배송함 지지·중앙 정렬을 확인했다. 첫 사선 렌더의 앞바퀴 크롭은 카메라 구도를 바꿔 수정했다. PNG/파일 지문·크기와 실제 산출물도 검사했다. 이는 모델/Blender 검증이며 Unity에서의 적용/앞방향/재질·입력·이동/업무·성능·E 승격을 뜻하지 않는다.

## Claude가 이어받기 전 남은 것

1. 사용자 외형 검토. 첫 색·비율·헬멧/배송함과 정적 탑승 자세의 채택 여부는 아직이다.
2. 게임용 변형에서 움직일 부품만 분리하고 정적 부분/재질을 정리한다. 단순 선택 Collider·원거리 요약/LOD·URP 재질/그림자를 현재 소비판에서 측정한다.
3. 승인 범위·정확한 입력 지문/허용 경로/담당·기존 주체/WI·E1~E7 명세를 결속한 뒤 한 기사 ID의 외형으로 적용한다. 현재 원본/모델 존재만으로 작업을 자동 활성화하지 않는다.
4. 이동은 기존 위치/경로가 소유한다. 현재 정적 기사를 Humanoid/탑승 Clip 준비 완료로 해석하지 않으며 이후 리깅/동작은 별도다.

이 반환은 파일 인계/검토 준비다. 기존 Unity 프로젝트·Scene·현재 실행 창·사용자 세이브·게임/서버 코드·공공자료/DB·예약·Graph Map/Goal/WI/E·commit/push를 변경하지 않았다. 새 실제 World 배치/운송 판독 규칙 후보 없음.
