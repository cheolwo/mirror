# 지구본 수산물 통계 로컬 미리보기 r6

## 승인 범위

2026-09-22 사용자 요청: 확보한 정보에 근거해 기존 지구본에 먼저 짧게 표현한다. 상태 `ApprovedScopedLocalPreview`. r5의 수출입 12행만 로컬 읽기 전용 미리보기로 사용한다. 기존 비공개 검토 자료의 공개·배포 승인은 아니며 물고기 이동·수온 규칙·새 WI Goal은 활성화하지 않는다.

## 작업 명세

- 책임: 월드·공간·배치의 로컬 표현 및 검증. canonical `SimulationWorldShell` 유지, Scene 저장 없음.
- 원본: r5 batch `20260922T080805-61216b99`. DB 독립 재조회와 receipt/원본 hash 대조 후 allowlist 상태 사본을 `artifacts/local/`에 내보낸다.
- 표시: 대한민국 국가 귀속 표식 → 수산물 통계 열기 → 3개 품목·상대국·2026년 6월 수입 중량/미화금액 → 접기. 위치는 국가 정보 진입점이며 실제 어획·항로·어군 위치가 아니다.
- 경계: 수온 인증 실패를 미확보로 표시. 운영/Simulation 상태·Tick·재고·수온·어군 수를 변경하지 않는다. 자동 외부 조회 없음.
- 쓰기 범위: 기존 CLI `국내해양자료Pipeline.cs`의 `preview` 읽기 분기, 별도 Unity `Assets/Ssalddel/Presentation/WorldMap/수산물통계미리보기View.cs`, 전용 Editor 로더·EditMode 시험과 `.meta`, 이 기획·목차·현재 작업·캡처.
- 자료는 Unity `Assets/Resources`·StreamingAssets에 넣지 않는다. Editor에서 명시적으로 로컬 사본을 열고 메모리에만 보유한다. 제품 빌드 자동 포함·서버 API 연동은 후속이다.
- 실패: schema/국가/단위/판본/값 검증 실패 시 기존 사본을 새 데이터로 위장하지 않고 로드를 거부한다. 새 조회 없이 기존 지구본으로 돌아갈 수 있다.
- 검증 상한: 읽기 전용 UI 프로토타입. E 승격·새 플레이 폐루프 성립 아님. 단위 시험, 실제 DB export, Play Mode·Game View·Console을 분리 기록한다.

## 결과

- CLI `domestic-marine-preview . 20260922T080805-61216b99`: DB 12행 및 원본/receipt 재대조 성공, `databaseWriteAttempted=false`. 허용 필드만 내보냈다. 원문·연결 문자열·인증키는 포함하지 않는다.
- 로컬 사본: `artifacts/local/public-data/domestic-marine/20260922T080805-61216b99/globe-preview.json`; SHA-256 `8238ab3dc93d01e0313b7d2fe4738047a5a45119716a8d25e758ea086b13265e`.
- CLI build 오류·경고 0. Unity 최종 재컴파일 오류 0, 전용 EditMode 6/6 통과. 일부 Pipeline 호출의 응답은 시간 초과했으나 `recompile_status`와 `test_status`에서 최종 성공을 독립 확인했다. 전체 기존 Unity 회귀 시험은 실행하지 않았다.
- 기존 PlayerView가 하위 Canvas를 비활성화하는 문제를 발견했다. 새 카드는 기존 지구본 UI처럼 독립 루트로 두고 View 종료 시 제거한다. 공용 PlayerView·지구본·Scene 코드는 변경하지 않았다.
- 실제 canonical Scene의 Play Mode에서 로컬 사본 3개 품목을 로드했다. 기본 접힘 → 열기 → 고등어류/대구류 선택 → 접기 → 대한민국 표식으로 재열기 → 연어류 복귀를 확인했다. 버튼 `onClick.Invoke` 경유 검증이며 실제 마우스 클릭·모바일 터치 시험은 아니다. Canvas는 1개였다.
- 최종 Game View: [연어류 통계 카드](../../../../assets/changes/2026-09-22-marine-globe-preview/game-view-salmon.png). `ScreenCapture.CaptureScreenshot`으로 실제 Game View 전체 프레임을 저장했다. 초기 camera-only 캡처는 Canvas와 전용 지구본 카메라를 반영하지 못해 최종 증거로 사용하지 않았다. 현재 Editor Game View는 세로 비율이며 반응형 시각 마감은 후속이다.
- 최종 컴파일 이후 Console에는 기존 `OSLifecycleValidationRuntime` 부모 변경 오류 1건이 Play 종료 시 확인됐다. 통계 미리보기의 새 오류는 관찰되지 않았지만 전체 Scene Console 0은 주장하지 않는다.
- 검증 후 Play Mode 종료. Scene 저장·신규 Scene·정기 조회·공개 배포·commit·push 없음. 수온 미확보 표시 유지. 새 디오라마 규칙 후보 없음.

## 다시 보기

기존 `SimulationWorldShell`을 Play Mode로 열고 `Tools → Mirror → 로컬 수산물 통계 사본 열기`에서 위 로컬 `globe-preview.json`을 선택한다. 처음에는 접힌 상태이며 `수산물 통계` 또는 대한민국 표식을 누르면 카드가 열린다. 정식 빌드에는 이 Editor 미리보기나 자료 파일이 포함되지 않는다. 서버의 인증된 조회 API를 통한 지속 연결은 별도 후속이다.
