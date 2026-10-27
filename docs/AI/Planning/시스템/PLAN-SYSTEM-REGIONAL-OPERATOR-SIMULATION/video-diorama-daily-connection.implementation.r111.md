# 일일 영상의 실제 시각·분석 결과·건물 대응 연결

[기획 · 영상·디오라마 자료 누적 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r111]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / ScopedConnectionImplemented / Focused160Passed / ActualPrivateEvidenceBound / CanonicalBindingPending / OperationalDisabled`
- 직접 승인: 사용자가 원본 구간의 위치는 Codex가 분석하도록 요청하고 r110의 다음 보완 순서도 진행하도록 지시했다.
- 기준: [일일 운영 r110](daily-video-diorama-accumulation.r110.md), [외관 첫 구현 r109](video-facade-refinement.implementation.r109.md).

## 허용한 첫 연결 범위

1. 배달 `longform_sign_batch.py`·집중 시험과 필요한 실제 PTS helper: 디코더 정수 PTS/time_base를 보존하고 이전 실수 초 출력과 구분한다. 기존 결과를 소급 정밀화하거나 명목 FPS로 역산하지 않는다.
2. 새 `video_diorama_daily_batch.py`·집중 시험: 기존 접수 state·표본/OCR 영수증·판독/후보 참조를 원본 SHA별로 결속하고 새 비공개 판본에 기록한다. 기존 state/DB/승인/포인터를 덮지 않는다.
3. 새 `diorama_building_correspondence.py`·집중 시험: 검증된 공개 건물 원형과 현재 지도 도형의 겹침/차이를 비교한 대응 후보를 만든다. 도형 유사성은 동일 건물 확정이나 적용 승인이 아니다. 기존 좌표 변환과 GIS 자원을 재사용한다.
4. `edit-kit/private/video-diorama-daily-batch/20261026-r2/` 아래 실제 기존 프레임 판독·도구 실행·보고·검증 자료. 원본 전체 새 해시/복사/대량 표본 추출은 하지 않는다. 번호순 다음 원본과 새 묶음은 구분한다.

단계 정책 JSON·기존 운영 안내·AGENTS·기획 목차/현재 작업은 실제 완료 범위에 맞춰 갱신한다. 코드 소유는 위 경로별로 나누고 관련 없는 dirty 작업·원본·과거 분석·Premiere·사용자 승인·현재 공간 자료를 보존한다.

## 완료 기준과 권위

정수 시계의 실패/변조/개수 불일치·구형 재사용, 일일 연결의 다른 원본/판본·부분 실패·변경·중복/재개, 건물 후보의 좌표 변환·잘못된 도형·겹침/모호/대상 충돌을 시험한다. 실제 자료에서 읽은 산출물과 합성 시험을 구분한다. 위치는 직접 판독·공개 기점·시간순 흐름으로 추론하며 미확인을 전체 경로나 GPS로 채우지 않는다.

Unity 코드/Scene/EditorPrefs 변경·정본 건물 자동 대응·자동 승인·운영 DB·추가 외부 API/게시·상시 감시·모델 가중치 학습·commit/push는 범위 밖이다. 본 요청은 기존 자료를 읽는 비공개 연결 구현이며 전체 게임/운영 재개의 승인이 아니다. 실제 결과·남은 공백은 마감 시 아래에 기록한다.

## 마감

관련 6모듈 통합 160/160(실패·생략 0)을 통과했다. 새 시계 계약은 합성 영상 실제 디코더 시험으로 검증했고, 실제 0112 원본은 header-only 시간표와 선택 12개 PTS를 대조했다. 기존 사진 12장을 직접 판독해 영화사로 진입·선분 3개 후보를 별도 비공개 자료로 추가했다. 전체 원본 경로·GPS·선분별 통과 시각은 미확정이다.

보완 후 실제 접수 13개/구형 실수 시각 표본 761개의 근거를 `daily-link-r3`에 연결하고 `r4`에서 재검사했다. 신규 원본·신규 결속 참조는 재검사에서 모두 0, 재사용 단계 26개이며 validate를 통과했다. 이 연결 실행의 새 의미 판독은 0건이고 위 0112 실제 판독과 구분한다. 예비 r1/r2도 덮지 않고 보존했다.

공개 건물 3개와 현재 Overture 2,247개를 대조한 도형 대응 후보는 0건이며 정본 적용은 보류했다. [변경·시험·한계](../../../../Changes/2026-10-26-video-diorama-daily-connection-r111.md), 최신 결과는 배달 `edit-kit/private/video-diorama-daily-batch/current-review.json`을 따른다. 새 Unity 실행·승인·Scene·운영 DB·배포·commit/push 없음. 새 공통 디오라마 규칙 후보 없음.
