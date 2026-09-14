[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r26]

# 사가정역 출구 방향 기준점 첫 구현 r26

- 상태: `ApprovedScopedImplementation / Implemented / AutomatedValidationPassed / CanonicalGameViewPending / EvidencePromotionNotPerformed`
- 승인 근거: 2026-09-14 사용자가 사가정역 디오라마의 첫 방향 기준점을 번호별 출구로 정하고 구현을 허용했다.
- 부모 기획: [역세권 디오라마 모듈 표준](README.md)

## 확정 범위

- 사가정역 1~4번 출구를 기존 동결 OSM 사본에서 추출하고, `sagajeong-reference.r3`과 같은 WGS84 ECEF→ENU 좌표계로 투영한다.
- 출구는 위치와 번호를 읽는 방향 기준점이다. `TraversalReady=false`, `InteractionReady=false`, `OperationalAuthority=false`를 자료와 Unity 검사에서 모두 강제한다.
- 외형은 출구 사진을 복제하지 않는 일반화된 절차적 저상 구조·차양·7호선 색상 번호 표식으로 만든다. 구조물 회전은 실제 출입 방향이 아니라 역 기준점을 향한 읽기 보조값이다.
- 기존 canonical `SimulationWorldShell`과 사가정 디오라마 조립 경로를 재사용한다. 새 Scene·Prefab·Collider·NavMesh·운영 API·Simulation Command는 만들지 않는다.
- 출구 자료 검증이 실패하면 사가정 지도는 유지하고 출구 표식만 보류한다. 다른 역 자료나 임의 좌표로 대체하지 않는다.

## 자료 계보

원천은 기존 로컬 동결 사본 `artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm`이다. 원본 SHA-256은 `3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3`이고, 공식 조회 URL은 [OpenStreetMap map API](https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854), 이용조건은 [OpenStreetMap Copyright and License](https://www.openstreetmap.org/copyright)다. 현장 실측·출입 방향·보행 가능성을 증명하지 않는다.

| 출구 | OSM node / version | WGS84 위도·경도 | Unity ENU x,z |
| ---: | --- | --- | --- |
| 1 | `3401340639 / 5` | `37.5816073, 127.0884972` | `557.649, 109.022` |
| 2 | `3401340638 / 6` | `37.5809601, 127.0882817` | `538.614, 37.190` |
| 3 | `3401340636 / 5` | `37.5806262, 127.0880397` | `517.238, 0.131` |
| 4 | `8230068953 / 4` | `37.5804926, 127.0887306` | `578.266, -14.697` |

생성기 `eng/neighborhood/build-sagajeong-station-orientation-anchors.ps1`는 원본 hash, 기존 지도 revision·hash, 출구 번호 1~4와 고유 node를 검사한 뒤 별도 `SagajeongStationOrientationAnchors.json`을 만든다. 기존 `SagajeongReference.json`을 변경하지 않아 도로·건물 overlay의 hash 결속을 깨지 않는다. 생성 결과 SHA-256은 `D1A6874F86512A67F40B96F8D054C61F51F8E6226010150F781E694223309BAB`이며 재생성 결과와 일치했다.

## 구현 경계

```text
동결 OSM map.osm
└─ 출구 node 1~4 추출 + 원본 hash 확인
   └─ 기존 사가정 WGS84→ENU 투영 재사용
      └─ SagajeongStationOrientationAnchors.json
         └─ Unity 자료·지도 hash·권위 경계 검사
            └─ Collider 없는 일반화 출구 모형 4개 + 번호 라벨
```

- 자료 모델과 검사는 `사가정역방향기준점조회`가 소유한다.
- `사가정역방향기준점Builder`는 출구당 다섯 부품을 조립하고 기존 관찰 layer만 사용한다.
- `사가정운영디오라마View`는 출구 표식의 가용 상태와 개수를 표시하고, 화면 번호 라벨을 기존 충돌 회피 배치에 넣는다.
- 출구 위치·번호를 보여 주는 정적 배경 절편이라 새 플레이어 선택, WI, 역할 객체 행위, 권위 결과가 없다. E1~E7 영향 검토 결과는 `Presentation E1~E3 자동 검사 / E4 후보 표현 / E5~E7 미승격`이며, 출구 클릭·이동·방어 배치·경로 선택을 열 때는 별도 승인 기획과 E7 v2 작업 명세를 먼저 만든다.

## 검증

- 생성기를 별도 경로에 다시 실행해 출구 4개와 resource SHA-256 완전 일치를 확인했다.
- 격리 Unity EditMode에서 자료·hash·권위 경계, 번호·좌표, 일반화 모형 4개·부품 20개·Collider 0, 사가정 View 조립을 검사한 신규 시험 `4/4`가 통과했다.
- 기존 사가정 운영 View·모형 표현·H 공간 선택 회귀 `21/21`이 통과했다.
- 검증 원본은 `C:/Users/user/ssalddel-building-h-validation/artifacts/local/validation/sagajeong-station-exit-anchors-r26/`에 있다.
- 열려 있는 원본 Unity Editor의 Pipeline은 연결 불가 상태라 canonical `SimulationWorldShell` Play Mode·Game View·Console 판독은 새로 수행하지 않았다. 저장 Scene도 변경하지 않았다.
- 디오라마 증거 대장의 기존 `OSM 공간 근거`, `역별 수치 Profile 소유`, `교차 역 fallback 금지`, `외관 근거 전 절차적 표현 유지` 규칙을 지지했다. 새로 발견한 “번호별 역 출구는 역 Profile의 방향 기준점”은 사가정 한 역에서만 확인한 `Candidate`로 등록했고 `applicationAuthorized=false`와 E 자동 승격 금지를 유지했다. 대장 검사는 `Sources=6 / Rules=11 / Candidates=11 / Provisional=0 / Accepted=0 / Stations=3`으로 통과했다.

## 미정과 재개 관문

- OSM node는 출구 위치 후보이지 현장 실측 또는 실제 출입 방향 근거가 아니다. 실제 외형·방향은 별도 공간 근거가 승인될 때만 교체한다.
- 기존 출구 사진 2건은 `CC BY-SA 4.0`이고 프로젝트 ShareAlike·사람·간판 검토가 남아 있어 이번 절차적 외형의 근거로 사용하지 않았다.
- 출구별 연결 도로·시장·공원 방향 문구, 선택 카드, 보행·방어 기능은 이번 구현 밖이다.
- 다음 재개점은 canonical Game View에서 네 번호의 판독성과 건물·도로 가림을 확인하는 Presentation 검토다. 이 검토만으로 통행·gameplay·E 단계는 승격하지 않는다.
