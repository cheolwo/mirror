# 건물 높이 근거 연결과 쇼윈도 투과 보완

[기획 · 건물 외형·유리 보완 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r115]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / ScopedImplementation / PrivateBuildingStudy / NoVideoRequested / OperationalDisabled`
- 직접 요청: “영상은 만들 필요가 없고 그냥 다음 보완을 한번 진행”하도록 요청했다.
- 이전 결과: [건물별 상세 외형 r114](detailed-building-representation.r114.md)의 같은 세 점포·원본 근거·검토 경계를 재사용한다.

## 이번 단위

현재 높이 4m 기호값·전면 한정 모델·불투명 유리의 후속이다. 먼저 기존 공공자료에서 세 건물의 정확 ID에 결속 가능한 높이·층수·지붕 근거를 확인한다. 인접 건물이나 근사 겹침의 높이를 대신 붙이지 않고, 미확보일 때 기존 기호 높이를 보존한다. 관측 높이·층수만의 환산·사진 비율 추정·미확인은 구분하며 실제 값과 출처를 하나의 표시 높이로 덮어쓰지 않는다.

기존 공간 BuildingData·원형과 ID/Profile를 유지하면서 상세 몸체의 표시 높이·검토 경계가 같은 근거로 조립되는 좁은 외형 adapter를 마련한다. 실측이 없으면 고급 모델이 생겼다는 이유로 실제 높이·층수·지붕 형태를 확정하지 않는다.

쇼윈도는 opt-in 재질·기하로 빛 투과와 시야각 반사를 보완한다. 기존 완전한 뒷벽이 투과를 막는지 대조하고 전면 벽의 창 구획과 유리/틀을 분리한다. 자료에 없는 실제 실내·사람·메뉴·상품은 재현하지 않는다. 필요하면 비관측 중립 배경 공간임을 기록하고 관측 실내로 표시하지 않는다. 원본 사진을 텍스처로 복사하지 않는다.

## 허용 경로와 보존

- Unity `사가정상세점포ModelBuilder.cs`, `사가정상세표면MaterialLibrary.cs`와 각 집중 시험, 필요한 새 전용 shader/`.meta`.
- 새 `Assets/Ssalddel/Presentation/World/사가정상세건물외형Basis*` 및 대응 집중 시험/`.meta`, 기존 `사가정상세건물PreviewCapture.cs`와 DemoCapture opt-in 최소 연결/회귀.
- 배달 `edit-kit/private/building-envelope-refinement/20261026-r1/`의 현재 근거·입력·실행 기록·PNG·이미지 비교 HTML/JSON만 생성한다. 이전 자료·MP4는 보존하며 새 영상 인코딩·MP4 생성·편집·업로드는 실행하지 않는다.
- 본 PLAN, Changes와 기존 문서 목차/현재 작업 포인터.

원본 영상·현재 분석/지도/승인·Scene·Synty/원래 Material·URP/Graphics/Quality·package·운영 DB·게시 상태는 보존한다. 새 공식 Scene·HDRP/Addressables 설치·전체 자동 3D 복원·근거 없는 높이 적용·운영/공개·commit/push는 포함하지 않는다.

## 검증

높이 출처·ID·기호 fallback·불명 층수/지붕의 비승격·몸체/경계 일치와 유리 재질/기하·원참조/해제를 집중 시험한다. 기존 교체/거리 전환·외관·공간·재질 회귀도 실행한다. 같은 원본 stat/기존 SHA·PTS·현재 보호·원본 설정 보존을 다시 확인한다.

새 Unity PNG의 같은 카메라 전후·실제 제거 복구·글자 방향·투과·가림·자원 정리를 검사한다. 실제 Editor/Pipeline 도구 연결이 없어 그래픽 CLI PreviewScene 카메라로 검증하며 Game View·Play Mode·운행 FPS와 구분한다. 이번 결과는 PNG와 이미지 전용 비교 자료로만 보여준다. 공통 규칙·E 단계는 자동 승격하지 않으며 후보 유무를 종료 시 기록한다.

## 현재 진행 — 절차 제안으로 전환

아래는 r116으로 전환할 당시의 기록이다. 후속 [r118 실제 연결 검증](../../../../Changes/2026-10-26-video-diorama-integration-r118.md)에서 새 높이/유리 입력의 graphics 종료0·35PNG·5시점 제거 복구와 현재 보호/원본별 환류를 확인했다. Unity 관련 시험도186/186을 통과했다. 정본 반영0·새 영상 생성0·Game View 미검증은 유지한다.

정확 GIS 원자료 행에서 뚜레쥬르 전체 높이31.5m/지상8층, 와플대학·맥주아비 높이0/지상2층을 확인했다. 높이0은 실제 높이 미확정으로4m를 유지하고 층수에서 환산하지 않았다. 기존 BuildingData 기반의 표현용 외형 근거/수직 본체·점포4m 분리와 opt-in 투명 유리·중립 셸을 구현했다. 실내/지붕 재구성이나 현장 실측 주장은 하지 않는다.

graphics EditMode r1은 새 시험의 미참조 JSON 의존성으로 컴파일이 중단돼 문자열 fixture로 고쳤다. r2는175중174통과/1실패였으며 Bounds.min 부동소수점의 exact 비교를 허용 오차 비교로 수정했다. 최종 `ssalddel/artifacts/local/building-envelope-r3/editmode.xml`은175/175통과, 실패·생략0이다. 후속 작업 중 사용자가 구현보다 일일 처리 절차 제안을 먼저 요청했다. 따라서 새 graphics PNG·비교 보고 실행·Game View·실제 투과 표현 검증은 아직 미실행이며 기존 r114의 화면 검증을 승계하지 않는다. 새 영상 생성0·정본 적용0·Scene/운영/공개 변경0이며 [r116 제안](daily-video-three-stage-handoff.proposal.r116.md)으로 이어간다.
