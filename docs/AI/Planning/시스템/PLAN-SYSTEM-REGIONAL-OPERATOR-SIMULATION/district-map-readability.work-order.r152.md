# 구 지도 이름 판독 보정 작업 명세 r152

- 기획 정본: [구 지도 정밀화 이어받기 r152](district-map-readability-continuation.design.r152.md)
- 수행자/작업실: Codex / 개발 작업실 이어받기. 사용자 요청일 2026-11-13.
- 기존 주체: 구 지도 View와 현재 공간 팩, 커널의 읽기 전용 영역 상태 사본. 준비 근거는 기존 `player-20261113-080818`·현재 소스/팩·실제 화면이다.
- 상호작용: 지도 전체 보기→확대/이동→실제 지명·큰길 판독→기존 동/표식 선택 또는 동네로 귀환.
- 직접 결과: 이름의 가시성·배치. 대가/실패는 가려짐·충돌·화면 밖 배치이며 다른 앵커 검토 또는 축척 변경으로 회복한다.
- 권위 상태 변경·행위 원장·경제 성장: `NotApplicable` — 기존 상태를 읽는 표시 보정이다. 다른 Goal·WI·E 원장의 상태를 변경하지 않는다.

## 허용 경로

Unity checkout `C:/Users/user/ssalddel`:

- `Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchGu.cs`: 라벨 배치·판독 연결.
- `Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchGuLabels.cs`: 기존 지도 이름의 측정·우선순위·점유 준비와 그리기.
- `Assets/Ssalddel/WindowsGame/Runtime/구지도이름배치Policy.cs`: 순수 화면 기하 점유.
- `Assets/Ssalddel/WindowsGame/Checks~/`: 해당 기하 정책의 독립 의미 검증.
- `Assets/Ssalddel/WindowsGame/README.md`: 변경·검증 안내.

검토용 빌드·측정/캡처 도구·결과는 Hongdal의 `artifacts/local/hongdal-gu-precision-20261113/` 아래에만 쓴다. 실제 소스의 Kernel·Territory·SimulationBridge·Editor·Resources·Tools~와 기존 실행판·원본 자료·사용자 저장·다른 작업 변경은 보존한다. 검토 전용 캡처 코드는 복사 프로젝트에만 넣고 제품 입력·저장 계약을 변경하지 않는다.

## E1~E7 상호작용 수직 영향·확인

`presentationE4Preparation`: 판독 순간은 구 지도에서 구/동과 큰길을 찾을 때다. `VisualKey=ui.gu-map.existing-names.r152`, `H=NotApplicable`(기존 2D 화면 글자), 자산은 기존 `font/boldFont`와 그 그림자이며 별도 Synty·Prefab·애니메이션이 없다. 연결 지점은 공간 팩의 기존 이름 앵커→`GuPoint`→실제 폰트 측정 사각형이다. 기본 표현은 회전 글자+짧은 그림자, 대체는 충돌 시 생략 후 다른 기존 앵커/축척에서 판독이다. 후보는 baseline 지문과 수정 소스 지문으로 결속하며 실제 소비판 렌더 전에는 E5 준비로만 기록한다.

| 단계 | 확인할 것 | 증거와 상한 |
| --- | --- | --- |
| E1 의미 | 실제 이름/좌표는 그대로, 화면 배치만 보정 | 정본·소스 차이 |
| E2 주체 | 기존 구 지도·영역 상태 사본·공간 팩 | 입력 지문·빌드 기준 |
| E3 계약 | 저장/명령/고유 ID/공간 팩 불변 | 변경 경로·지문 대조 |
| E4 표현 준비 | 실제 글자 폭·회전 경계·패널 영역·축척별 우선순위 | 기존 제안 시안·현재 화면·배치 정책 검증 |
| E5 연결 | 정책이 실제 `DrawGu*`에서 호출되고 기존 선택 영역과 일치 | 소비판 컴파일·실제 렌더 |
| E6 동작 | 회전 충돌/불필요한 거짓 충돌·화면 이탈·같은 이름 간격·확대/이동 | 독립 기하 검증·Runtime 확인 |
| E7 귀환 | 전체/중간/근접 판독 뒤 기존 동/표식 선택·지도 귀환 | Windows 실행판 실제 입력·화면, 사용자 저장 보존 |

Logic은 기존 커널 동작을 변경하지 않는다. Presentation의 위 확인을 독립 기록하며 이 표 자체나 단위 시험만으로 등록된 공용 E를 승격하지 않는다. 실패하면 같은 기획의 가장 이른 영향 항목을 다시 열고 원인을 남긴다.

## 완료 조건

- 회전 글자·긴 이름·UI 영역·뷰포트 경계의 실제 충돌과, 겹치지 않는 대각선 이름을 구별한다.
- 핵심 도로의 우선 검토·동/구/표식 점유 보호가 실제 소비판에 연결된다.
- 기존 커널 검사 결과와 빌드 성공을 별도 판정하고 전체/중간/근접 장면을 대조한다.
- 원본 공간/영역/규칙/사용자 저장 지문을 보존하며 source·검토 복사본·실행 증거를 구별해 반환한다.
