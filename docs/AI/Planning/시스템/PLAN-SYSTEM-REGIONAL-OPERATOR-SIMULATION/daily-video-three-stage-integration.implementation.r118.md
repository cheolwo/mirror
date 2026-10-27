# 영상 일괄 실행·정확 건물 대응·Unity 결과 환류 구현

[기획 · 일일 영상 자료 연결 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r118]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / PrivateIntegrationImplemented / Python214Passed / Unity186Passed / ActualHandoffVerified / CanonicalApplied0`
- 직접 요청: 사용자가 남은 세 부분인 일괄 실행·재개, 정확 건물 대응, 반영 결과 환류를 구현하도록 지시했다.
- 기준: [공통 절차 r117](daily-video-common-procedure.r117.md), [세 단계 인계 r116](daily-video-three-stage-handoff.proposal.r116.md). 기존 원본·판독·지도·사용자 승인과 서로 다른 단계의 완료 의미는 유지한다.

## 허용한 세 부분

1. 기존 접수/표본/OCR/일일 연결 도구를 고정 명령으로 실행하고 결과를 회수하는 원본별 일괄 실행기. 고정 목록·도구 판본·원본 stat·작업 수/시간 예산·단일 원본 작업자·부분 실패·살아 있는 잠금·중단 재개를 다룬다. 실제 의미/위치 판독이 없으면 해당 단계는 보류하며 OCR 성공을 경로 확정으로 만들지 않는다.
2. 공개 건물 근거와 현재 공간 판본의 정확 대상 결속. 현재 정본의 정확 ID/검증된 동일성만 정본 대응으로 인정한다. 현재 정본에 없는 정확 GIS 건물은 동일 좌표계의 명시적 `SourceBackedPrivateLayer` 대상으로 보존해 비공개 시범에서 조립할 수 있으나 정본 동일성·적용으로 승격하지 않는다. 인접/겹침만으로 기존 건물에 대신 배정하지 않는다.
3. 새 상세 건물/영상선분 Unity 결과를 실제 원본 SHA·관측·PTS·대상·판본별 일일 기록에 연결한다. 입력/PNG/제거 복구/cleanup과 현재 보호 근거를 재검사하고, 기존 구형 consumer는 유지한다. 연결 성공·비공개 표시·정본 반영·사용자 승인은 분리한다.

## 쓰기 경로와 담당

- 배달 `premiere-control/video_diorama_batch_runner.py`·대응 시험: 일괄 실행·재개 담당.
- 배달 `premiere-control/diorama_building_binding.py`·대응 시험 및 `edit-kit/private/video-diorama-integration/20261026-r1/building-binding/`: 건물 대응 담당.
- 배달 `premiere-control/video_diorama_unity_handoff.py`·대응 시험, `video_diorama_daily_batch.py`·대응 시험: Unity 결과 환류 담당.
- Unity 기존 DemoCapture/상세 PreviewCapture의 opt-in 결속, 필요한 새 `사가정영상건물Binding*`/집중 시험·meta, 기존 Demo 시험: 통합 담당.
- 배달 `edit-kit/private/video-diorama-integration/20261026-r1/`의 새 입력·실행/검증 기록·PNG·이미지 보고·성공 포인터. 공통 안내·정책 JSON은 현재 실제 구현 범위를 반영하는 최소 변경만 허용한다.
- 본 PLAN·Changes·문서 목차/현재 작업. 관련 없는 dirty 변경은 보존한다.

## 실행과 검증

기존 실제 근거를 재사용한 3개 정확 GIS 결속과 신규 출력/재개·변조/중복/타 원본·잠금/실패·보류·현재 보호·잘못된 target/판본·PNG 복구를 집중 시험한다. 실제 소규모/합성 subprocess 실행과 기존 실자료의 읽기 연결을 구분한다. 대용량 원본을 전수 재해시/재표본하지 않는다. 시험용 짧은 합성 미디어는 시험에만 쓰며 완성 영상·MP4 검토본은 만들지 않는다.

새 Unity 입력은 같은 기존 영상 근거·현재 공간·정확 결속 manifest를 고정하고, graphics CLI PreviewScene의 PNG·전후/복구·정리와 일일 원본별 환류를 확인한다. Pipeline 연결이 없는 대체 검증이며 실제 Game View·Play Mode·FPS를 대신하지 않는다. 정본 Scene 저장·자동 사람 승인·원본 삭제·새 유료 서비스·무인 감시·운영 DB·게시·commit/push는 실행하지 않는다.

## 마감 기록

구현/집중 시험·실제 명령 실행·원본별 보류·비공개 적용·정본 적용을 따로 기록한다. 정확 정본 대응이 없는 대상은 결손을 숨기지 않는다. 실제 화면·환류 결과가 미완료면 개별 도구 시험만으로 전체 완료를 선언하지 않는다. 기존 디오라마 근거 규칙을 재사용하고 새 공통 규칙 후보 유무를 확인한다.

실제 마감은 [변경 기록](../../../../Changes/2026-10-26-video-diorama-integration-r118.md)을 따른다. Python214/214·Unity186/186·실제35PNG/5시점 정확복구와 원본1/대상3/단계6·최종 재검증 문제0을 확인했다. 실제11개 명령은 모두 종료0·자식 정리 성공이다. runner는 나머지 의미 판독 때문에 Held이며 예산 초과/실행 실패는 없다. 현재 정본 동일성3보류/정본 반영0·새 원본 판독0을 유지한다. 새 공통 디오라마 규칙 후보 없음.
