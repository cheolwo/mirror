# 서울·중랑구 중간 공간 표현 r26

상태: 사용자 요청 범위의 로컬 검토 표현 구현·실제 Play 확인. 정식 빌드·최신 경계 적용·전체 지형 스트리밍 완료 아님.

## 범위와 근거

- [r25](spatial-data-inventory.collection.r25.md)의 서울 공식 원본을 이용해 서울 25구와 중랑구 16행정동을 읽기 전용 메시로 만들었다. 425개 행정동 전체를 Unity에 배치한 것은 아니다.
- 자료 기준일 **2023-10-31**. [서울 행정동 경계](https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do), [서울 자치구 경계](https://data.seoul.go.kr/dataList/OA-22161/S/1/datasetView.do). 서울특별시·서울신용보증재단, 공공누리 제1유형 표기를 화면에 보존한다. 최신 행정구역·운행 가능 경로로 사용하지 않는다.
- 지구/한반도 → 서울 → 중랑구 → 사가정 역세권 → 기존 디오라마의 좁은 표현 경로다. 수도권 전체와 다른 역세권은 아직 채우지 않았다. 행정동과 역세권이 동일한 포함 단위라는 뜻이 아니다.
- 기존 `SimulationWorldShell`, `세계지구본View`, 사가정 진입·복귀를 재사용했다. 새 Scene이나 운영 상태 쓰기를 추가하지 않았다.

## 자료 생성과 구현

- Hongdal `eng/neighborhood/build-seoul-intermediate-preview.py`: 원본 ZIP hash 확인, EPSG:5181 기반 좌표 변환, 8m 위상 보존 단순화, 제약 Delaunay 삼각분할, 개별 도형 유효성과 면적 보존 확인.
- `.prj`의 ESRI datum 이름 때문에 CRS 객체 동일성 대신 공식 EPSG 선언 및 투영 매개변수·타원체·단위를 비교했다. pyproj의 `to_dict` 정보 손실 경고가 있으므로 완전한 WKT 동등성 검증으로 표현하지 않는다.
- 사가정 원점 WGS84 (127.0884106, 37.5806971), 1 Unity 단위=1km, 북쪽=+Z. 원점의 중랑구 포함 및 좌표 왕복 오차 <1e-8도 검증. 개별 도형 검증이며 인접 동 전체의 중첩·틈 전수 검증은 별도다.
- 로컬 Python 의존성: pyshp 2.3.1, pyproj 3.7.2, shapely 2.1.2. `artifacts/local/python-packages/spatial`에 격리했다.
- 출력: `artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/seoul-preview.json`, `.sha256`. 745,917 bytes, SHA256 `45161b921c33aa0df4330cdde007c391444b6d3333b25805058aea236436e11b`. 같은 입력으로 두 번 생성해 동일 hash 확인.
- Unity 변경: `서울중간공간View.cs`, `서울중간공간Loader.cs`, `서울중간공간Tests.cs` 및 각 `.meta`, 기존 `세계지구본View.cs`의 중간 표현 진입/복귀 연결. 경계 카메라가 기존 디오라마 카메라에 가려지는 문제를 depth 500으로 수정하고 재실행 확인했다.
- 로컬 검토 사본을 Play에서 메뉴 `Tools/Mirror/서울 중간 공간 검토 사본 열기`로 연다. 파일 크기·hash sidecar·계약을 확인한다. 배포 Resources 또는 Scene에 저장하지 않으며 재실행 시 다시 열어야 한다. DB 정규화 도형 저장·정식 자동 로드는 이번 변경에 없다.

## 실제 검증

| 증거 | 결과와 한계 |
| --- | --- |
| 자료 | 25구+16동=41개, 고유 코드·좌표·개별 도형 검증, 재생성 hash 동일 |
| EditMode | 신규 계약 시험 4/4, 기존 지구본 시험 22/22 통과 |
| 최종 코드 재실행 | 실제 Scene `SimulationWorldShell`, 41개, 경계 카메라 depth 500 확인 |
| 실제 Play | 서울→중랑구→사가정→디오라마 진입·복귀, 복귀 후 사가정 단계/카메라 1개 확인 |
| 입력 | CLI로 실제 View의 전환 함수를 호출. 물리 마우스 휠/버튼 입력은 미검증 |
| Console | 기존 경로의 ReplayHashMismatch, 서버 연결·세션 누락 오류 존재. 테스트 도구 TestResultCollector.SetResult 오류도 별도 관측. Console 오류 0 아님 |
| 빌드/업무 | Windows 빌드 및 운영 업무 E2E 미실행. 기존 디오라마 업무 결과 0건/ConnectionUnavailable |

실제 Game View: [서울](../../../../assets/changes/2026-09-23-seoul-hierarchy/02-seoul.png), [중랑구](../../../../assets/changes/2026-09-23-seoul-hierarchy/03-jungnang.png), [역세권](../../../../assets/changes/2026-09-23-seoul-hierarchy/04-station.png), [기존 디오라마](../../../../assets/changes/2026-09-23-seoul-hierarchy/05-diorama.png). 생성 시안이 아니라 실행 캡처이며 최종 디자인 승인은 아니다.

## 남은 보완

현재 중간 지도는 평면 경계 메시이며 산맥·하천·도로가 채워진 지형이 아니다. 중랑구 화면의 북쪽 일부 잘림, 지명 겹침과 가는 경계선 판독성이 남는다. 지구본→평면은 시점 전환이고 연속 지형 변형이 아니다. 다음 작은 개선은 중심/줌 범위 조정과 표시 밀도 정리, 실제 휠 입력 검증이다. 기존 서버·저장 오류는 이번 표현 변경과 분리해 해결해야 한다.

Play 종료, Scene 저장·commit·push 없음. 기존 변경 보존. 디오라마 증거 규칙 점검: 역사 자료와 현재 적용 권위 분리 원칙을 지지하며 최신 경계 수집 관문을 충족하지 않는다. 새 디오라마 규칙 후보 없음, E 자동 승격 없음.
