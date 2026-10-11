# Mirror(거울) Current Work

## 음식 주문 수집·배차 시점 판단 r52 (2026-10-11, 로컬 검증 완료·운영 OFF)

- [수락 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.implementation.r52.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.r52.work-order.json)에 따라 첫 음식 추천 전에 짧게 주문을 모을지 현재 배차로 진행할지 서버 정책과 제한 AI 선택을 연결했다. 최초 접수 시각의 대기 상한·자료 신선도·확인된 조리 예정 시각·큐 판본을 재확인한다. 공유 기본값은 OFF다.
- [변경/검증 기록](../Changes/2026-10-11-food-dispatch-batch-timing-r52.md)·[자료 실행 안내](../../eng/local-dispatch-ai/DispatchTimingScenarioRunner/README.md)를 따른다. 신규 135개 포함 Fast 244개, Task 전체 10,753개 통과·실패 0·Mongo 환경 의존 15개 건너뜀이다. 별도 자료 도구 빌드 오류/경고 0·Python 42개 통과, 8유형의 정책 초안 4,032개·대표 24개를 준비했다. 지점 교차/입력 중복 0, ready 0행·모델 호출/학습/사람 승인/운영 변경 0이다.
- 수집 중 새 제안·요금·알림은 만들지 않는다. 기존 수요 판정 투영 복구는 가능하다. 실제 다건 공동 수락·4,000원 수렴 점수·모델 학습/운영 활성화는 별도다. 자료는 현재 순수 정책의 검토 초안이며 실제 AI 프롬프트 학습용 변환은 후속이다. 작업 명세 밖 소스 변경 0을 확인했다.

## 배차 학습자료 1차 제작 r51 (2026-10-11, 후보 제작 완료·사람 검토 대기)

- [수락 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.implementation.r51.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.r51.work-order.json)에 따라 기존 504건·339지점의 출처와 합성 상황을 결합해 현재 정책 답안 초안 4,041개를 만들었다. 중복 후속 픽업 9개를 격리하고 후보 4,032개를 학습 2,784·검증 856·평가 392로 나눴다. 지점 교차·후보 입력 중복은 0이다.
- [변경/검증 기록](../Changes/2026-10-11-dispatch-training-materials-r51.md)과 [실행 안내](../../eng/local-dispatch-ai/TrainingMaterialRunner/README.md)를 따른다. 대표 24개를 한국어 검토표로 준비했다. 별도 도구 빌드 오류 0·Python 반례 45개 통과, 생성/컴파일 동일성 재실행을 확인했다. 실제 적재·도로 경로·최적 선택 검증은 아니며 모든 답안은 Pending, ready 0행·모델 호출/학습/사람 승인/운영 변경 0이다. 제품·원본·다른 dirty 작업을 보존한다.

## 지역 배차 판단 준비 r50 (2026-10-11, 로컬 시나리오 완료·단말/운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.implementation.r50.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.r50.work-order.json)에 따라 2·3·4·5번(기사 부족/과잉·마지막 전달지 후속 픽업·단건/2건 조합)을 기존 정책과 서버 권위 안에서 구현했다. 새 후속 픽업·권역 검토 AI 옵션은 기본 OFF다.
- 중랑·동대문·광진504건·339지점/확인 메뉴488항목의 비공개 파생자료를 준비했다. 고객 위치·기사 GPS는 합성이며 역사 가격·방문 요약 메뉴를 현재 단건 값으로 승격하지 않는다. [변경/검증 기록](../Changes/2026-10-11-regional-dispatch-management-r50.md)과 [실행 안내](../../eng/local-dispatch-ai/RegionalScenarioRunner/README.md)를 따른다.
- 최종 모의27개와 대표 실제 모델2회가 포함된27개를 통과했다. 앞선 실제4회를 포함한 r50 누적6/6회, 실제 선택은 모두 기준과 동일하다. SQLite6개를 독립 재조회했다. Python22개·Fast214개, Task `20261011-171622` 전체10,618개 통과·실패0·실제 Mongo 환경 의존15개 건너뜀이다. solution 빌드의 회원/관리자 Firebase 미설정 경고6개·오류0을 기록했다. 사람 검토 Pending, 모델 가중치 학습·실제 멀티 원자적 수락·전역 미래 끝점 탐색·단말/외부 FCM·운영 활성화·Unity·commit/push는 별도다.

## 주문·Ollama 배차·기사 앱 통합 시나리오 r49 (2026-10-11, 로컬 검증 완료·단말/외부 운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.implementation.r49.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.r49.work-order.json)에 따라 실제 주문·기사 Controller의 루프백 HTTP, 격리 SQLite, 기존 후보/추천/요금 서비스와 FDriver API·수신기·PageModel을 연결했다. 주문→음식점 수락→추천→앱 내부 안내→수락·도착·픽업·전달→별도 수령 확인을 실행한다. 제품 소스·설정은 변경하지 않는다.
- 신규 통합 22개와 최종 Fast 210개가 통과했다. Task `artifacts/local/validation/20261011-155651`은 solution 빌드 오류 0·10,522개 통과·실패 0·환경 의존 15개 건너뜀이다. 새 실행 도구 최종 빌드는 경고/오류 0이다. 최초 시험 빌드 실패는 보존하고 실제 저장소 API에 맞춰 수정했다.
- [변경·실행 기록](../Changes/2026-10-11-local-dispatch-workflow-scenarios-r49.md): 같은 선택 소스의 모의 세 흐름 성공 후 실제 `20261011T065859Z-actual-077d3d06`의 세 흐름을 통과했다. 같은 권역·인접권은 Ollama 각 1회 선택 적용, 현재 수요는 단일 허용 후보여서 호출 0회다. 실제 두 선택 모두 기존 기준과 같아 판단 개선 증거는 아니다. r49 시작 예산 2/4, 이전 예산·결과는 보존했다. 각 DB를 종료 뒤 독립 재조회했다.
- 추천 저장 뒤 알림 Outbox 생성 실패와 Failed 재발송 부재 2변형을 재현했다. 공백 발견이며 제품 수정 완료가 아니다. 완료 뒤 후속 동기화 Outbox 1개씩은 Pending이다. 알림 성공은 기록용 포트이며 실제 FCM·Android 배너/소리·화면 렌더·생산 JWT·자동 이벤트 예약·운영 DB/정산·APK/USB·Azure·commit/push는 미실행이다. 사용자 요청에 따라 로컬 통합 시험까지만 진행했다.

## 음식 배차 수행 경로의 Ollama 후보 선택 r48 (2026-10-11, 로컬 검증 완료·외부 운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-application.implementation.r48.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-application.r48.work-order.json)에 따라 서버 DI→음식 후보 정책→제한 Ollama 선택→현재 후보/원장 재확인→기존 추천·요금·감사 저장을 연결했다. 실제 허용 후보·픽업 단계·최신 수요 우선권을 유지하고 실패·지연에는 현재 엔진 기준 후보로 돌아간다. 새 API·원장·UI·화물/Unity는 추가하지 않는다.
- typed 서비스 65개·정책/직접 추천/SQLite·DI 96개, 합계 161개 집중 시험이 통과했다. 새 검증 도구 빌드는 경고·오류 0이며 기본 실행은 추론·배차 0회다. 최초 Fast 166개와 최종 Fast 169개(`artifacts/local/validation/20261011-152240`)가 통과했다. 최초 Task의 메타데이터 예상 목록 누락 1개 실패를 보존하고 관련 시험 한 단계만 보완했다. 최종 Task `20261011-152313`은 solution 빌드 오류 0·전체 10,500개 통과·실패 0·환경 의존 15개 건너뜀이다.
- [변경·실행 기록](../Changes/2026-10-11-local-dispatch-candidate-application-r48.md)의 실제 실행 `20261011T061842Z-6f2e87e0`은 Ollama 1회·현재 허용 선택 적용·격리 SQLite 저장 후 재조회를 확인했다. 모델은 기존 엔진과 같은 합성 기사를 골랐고 시험 고정 요금 4,000원 동결·추천 라운드 1·감사/활동 각 1건, 중복 전환 없음이다. 전체 실행 8.923376초는 DB 준비를 포함하며 판단 개선·실제 요금 계산·제품 호스트의 10초 설정 검증은 아니다. r48 누적 시작 예산은 1/2이고 두 번째 호출은 하지 않았다.
- 공유 기본값/예시는 OFF, Git 제외 PC 설정에만 ON·timeout 10초다. 검증의 외부 알림 요청 1건은 시험 경계에서 억제했고 실제 고객 변경·외부 발송·기사 수락·제품 호스트 실행은 없다. r47 최종 0개 통과/4개 복귀와 r46 기록·소진 예산을 보존하며 소스 변경에 따른 과거 근거 만료를 정상 처리한다. 운영 배포·APK·단말·Azure·학습·commit/push는 미실행이다.

## 배달권·기사 후보 근거를 연결하는 로컬 AI r47 (2026-10-11, 로컬 검증·모델 품질 한계·운영 OFF)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-advisory.implementation.r47.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-advisory.r47.work-order.json)에 따라 기존 음식 후보 정책의 같은 권역·인접권·제한 확장·후보 관문·선정 근거를 읽기 전용 결과로 분리하고 실제 정책을 합성 입력에서 실행하는 별도 도구와 제한된 Ollama 검토를 연결한다. 운영 알고리즘·최종 서버 권위·기존 기본 OFF를 유지한다.
- 실제 후보 정책 합성 10개·내부 검사 82개, 후보 정책 집중 시험 61개(신규 21·기존 40), Python 97개(신규 53·이전 44)가 통과했다. 기본 준비 검사는 10개·모델 0회다. Fast `artifacts/local/validation/20261011-142931`에서 서버/시험 두 프로젝트 빌드와 61개 시험을 확인했고 최종 Task 근거는 작업 명세에 별도 기록한다.
- [변경 기록](../Changes/2026-10-11-local-dispatch-candidate-advisory-r47.md)의 실제 모델 실행 `20261011T052841Z-1966597c9499`은 4회 완료, 최초 1개 관문 통과·3개 복귀였다. 관련 출처 대응 검사를 보강한 뒤 원응답 재검증에서는 0개 통과·4개 기존 정책 결과 유지다. 최초 결과와 후속 재검증을 별도 보존했고 추가 추론 0회·사람 승인 0건이다. r47 누적 예산 4회는 소진됐으며 이전 r46의 36회 기록·원응답·보고서를 보존한다.
- 현재 정책·사용자 확인 방향·합성 가정·미구현 제안을 구분한다. 약 4건/h 방향을 현재 기본 3.5건/h로 덮어쓰지 않고 시간대 가점과 마지막 전달지 기반 다음 픽업을 구현 사실로 표현하지 않는다. 정책 소스 변경으로 r46 근거가 현재 검사에서 만료되는 것은 정상이며 과거 해시를 갱신해 재승인하지 않는다. 실제 배차·DB·외부 알림·새 UI·APK·Unity·Azure·학습·commit/push는 범위 밖이다.

## 배차 사례 문답 검토 (2026-10-11, 수요 우선·장기 대기 기회 방향 확인·세부 기준 미정)

- [문답 검토 절차](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md)를 정리하고 기존 실제 모델 실행의 5개 합성 시점을 첫 검토 묶음으로 재사용했다. 상황·현재 정책 결과·AI 원문·코덱스 수정 초안·사용자 질문을 분리하고 입력/응답 해시를 연결했다. 비공개 자료는 `artifacts/local/local-dispatch-ai/dialogue-review-20261011/round-01/`이다.
- 사용자 검토 대상은 코덱스의 수정 초안이며 AI 원응답 승인과 구별한다. 사람 승인·학습 후보 생성·새 모델 호출·운영 효과는 없으며 제품 코드·정책·DB를 변경하지 않았다. 현재 수요 참여 검토를 기사별 배차·묶음 경로의 AI 선택 완료로 확대하지 않는다.
- 5개 사례의 입력·응답/산출물 해시, 사용자 검토 대기·학습 후보 제외, 미확인 목표 인원의 null 보존과 문서 링크를 확인했다. 문서 범위 Fast `artifacts/local/validation/20261011-133952`·Task `20261011-133956`의 차이 검사가 통과했으며 제품 build/test는 문서 전용으로 생략했다.
- 후속 사용자 의견은 피크 진입 시 신규 참여 가점, 피크 종료 시 기존 활동 기사 할당 우선, 공통 장기 대기 고려다. 앞선 일률 대기 초안의 완전 승인으로 기록하지 않고 첫 피드백을 묶음 해시에 연결해 별도 보존했다. 시간표·수요 보정·가점·예외 기준은 미정이며 새 엔진/모델 실행·정책 변경·AI 원응답 승인·학습 후보 생성은 하지 않았다.
- 두 번째 답변에서 시간표보다 실제 주문량·기사 가용 여력을 우선하는 방향을 확인했다. 사용자 경험의 약 4건/h를 계획 가정으로 기록하고 여력 초과 시 비피크 진입에도 추가 참여를 권고한다. 세부 수치·관측 창·안전 여유는 미정이며 contract 기본 3.5건/h·기존 실행은 보존했다. 이전 피드백 해시에 연결한 별도 의견 2를 추가했으며 새 추론·실제 배차·가중치 학습·제품 코드 변경은 없다.
- 대화 가정에서 기존 기사로 주문을 처리할 수 있어도 수행 조건이 비슷하면 오래 기다린 기사에게 다음 건을 우선 제안하는 방향에 사용자가 동의했다. 가정의 30분은 임계값 확정이 아니며 기존 업무·적격/픽업 관문을 유지한다. 이전 피드백과 연결한 의견 3을 별도 보존했고 이 가정의 엔진/모델 실행·AI 원응답 승인·학습 후보 생성·운영 효과는 없다.

## 프로젝트 근거를 참고하는 배차·운영 AI r46 (2026-10-11, 로컬 검증 완료·품질 한계·운영 OFF)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/project-advisory.implementation.r46.md)에 따라 기존 합성 시나리오의 관측 기준·3.5건/h·12개 상황에서 실제 30개 시점과 규칙·출처·기존 대안을 내보내고 Docker 로컬 모델의 설명을 받았다. 제품 서버 API·DB·배차·알림·기본 OFF를 유지하며 모델 가중치 학습은 하지 않았다.
- 최종 Python 44개·C# 도구 내부 174개 검사가 통과하고 기존 288개 비교 해시를 유지했다. 실제 30개 응답 중 구조 검사 18개 통과·12개 규칙 결과 복귀이며 설명 정확도는 측정하지 않았다. 모든 응답은 사람 검토 대기, 승인 0건이다. CPU 응답 중앙값 37.227초·p95 43.672초로 실시간 배차와 분리된 사후 검토만 다룬다.
- [변경 기록](../Changes/2026-10-11-local-dispatch-project-advisory-r46.md)에 실행·반례·해시와 수치 대조 보고서를 연결했다. AI 초안과 C# 계산 수치표를 분리하고 승인된 이력만 향후 학습 후보로 만들 수 있도록 했지만 실제 승인·학습은 하지 않았다. 기존 Docker 실제 실행과 r44/r45 당시 경계·관련 없는 변경을 보존했다.

## Docker Ollama 실제 실행·합성 응답 확인 (2026-10-11, CPU 로컬 검증)

- 사용자의 후속 한 번 실행 요청에서 Docker 시작은 실행 도구가 허용했다. [실행 상태](../../eng/local-dispatch-ai/docker/README.md)에 따라 공식 `ollama/ollama:0.40.2`의 digest를 고정하고 기존 `qwen3:1.7b`를 별도 Docker volume에 복사했다. 컨테이너 안 manifest와 blob 5개의 해시가 원본과 같다. Windows 캐시·기존 다른 컨테이너는 변경하지 않는다.
- 첫 읽기 전용 캐시 초기화 실패를 별도 모델 volume으로, 내부망 호스트 접속 실패를 루프백 포트가 게시된 bridge로 수정했다. 실제 API 버전·모델과 CPU 장치, balanced 합성 한 건의 `validated_advisory_only`를 확인했다(약 12.7초). 참고 기사 수 10명은 제공된 35건/3.5건/h 계산이며 수요 예측이나 배차 최적화 결과가 아니다. 기존 모의 방어 시험 14개 통과와 실제 추론을 구분한다.
- `127.0.0.1:12681`·클라우드 OFF·자동 재시작 OFF·CPU/RAM 제한을 유지하고 이번 컨테이너는 실행 상태로 남긴다. 운영 API/상태 변경 0건, GPU·MCP 등록·제품 배차 통합·Azure·APK·commit/push는 미실행이다. 실행 근거는 Git 제외 `artifacts/local/local-dispatch-ai/docker-runtime-20261011/`이며 앞선 준비 기록과 r44/r45 및 관련 없는 변경은 보존한다. 이전 Windows 거절 원인이나 전체 권한 해제를 확인한 것은 아니다.

## AI 가정형 오프라인 운영 시나리오 r45 (2026-10-11, 차단 유지·로컬 비교 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.implementation.r45.md)와 [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.r45.work-order.json)에 따라 합성 주문·기사 시간표와 실제 수요 참여 Policy를 연결하는 개발 도구를 준비했다. r44 제품 코드·기본 OFF·실제 수락 관문은 변경하지 않는다.
- 12개 상황 × 8개 비교 × 계획 처리량 3·3.5·4건/h의 288개 비교와 내부 점검 165개를 통과했다. 빌드 경고/오류 0, 기존 서버 업무 회복 시험 61개 통과. 213개 비교는 배정 대기·완료소요·미완료 기준의 검토 대상이며 검사 통과와 시간 만족을 구분한다. 실제 경로·조리·사고 상태 기계를 복제하지 않았다.
- 입력·결과는 근거 해시별로 보존한다. 정확한 모의 예측의 배정 대기 p95 개선은 0/36개이며 기사 부족·긴 순차 수행이 남은 위험이다. 다음 우선순위는 약속 완료시간과 잔여 여력에 따른 제안·접수 용량·대안 안내의 검토다.
- r45 작업 당시에는 모델 실행 차단을 유지하고 Ollama 재시작·대체 추론·권한 완화·네트워크/업무 API·운영 DB 쓰기·APK·단말·Azure·배포·commit/push를 수행하지 않았다. 최종 수치와 제한은 [변경 기록](../Changes/2026-10-11-offline-dispatch-scenarios-r45.md)에 남긴다. 후속 사용자가 요청한 Docker 실행 결과는 상단 최신 절과 별도 실행 근거로 구분한다.

## 음식 배차 수요·참여 순서 r44 (2026-10-11, 기본 OFF 구현·로컬 검증 완료)

- [승인 범위·정책](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/demand-participation.implementation.r44.md)에 따라 음식 후보 선정에 수요·잔여 업무·기존 활동 보호·신규 대기·장기 대기/지연 예외를 연결했다. 직접 지정도 같은 선정을 확인한다. 기본 OFF, 기사 의사·유효 제안·수락 업무·동시 수행 관문·요금·지급·화물 흐름은 유지한다.
- [변경·검증 기록](../Changes/2026-10-11-food-demand-participation-r44.md). 소프트 참여 순서이며 권역 원자적 정원/영속 대기열은 아니다. r44 당시 로컬 Ollama·모델은 준비했지만 실행 도구의 시작 거절로 실추론은 미검증이었다. 후속 Docker 합성 응답 검증은 상단 최신 절로 구분하며 운영 활성화·단말·Azure·commit/push는 r44 범위 밖이다.
- Fast 89건·최종 전체 Task 10,386건 통과, 실패 0·환경 의존 Mongo 15건 건너뜀. AI 방어 시험 14건 통과. 최종 전체 실행 전후 소스 16개 해시 변화 0이며 결과와 초기 실패 수정은 변경 기록에 남겼다.

## 예외 복구 코드 책임 분리 r2 (2026-10-10, 로컬 검증 완료)

- [사용자 승인 범위](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.refactoring.r2.md)와 [작업 지시](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.r2.work-order.json)에 따라 음식 역할별 요청 준비·완료 근거와 지원 사건/배송 복구 상태·표시를 분리했다. 기존 동작·권한·계정/세대·원장 완료 근거·저장 정책을 유지한다.
- `Implemented / LocalValidated / DeviceAndOperationalDeferred`. Fast 116건·전체 소비 앱 build·Task 10,328건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀. 390×844 실제 제품 화면 37항목·브라우저 오류 0·업무/외부 HTTP 시도 0건이며 검증 전후 소스·시험·호스트 18개 지문이 일치한다. [변경·실행 근거·한계](../Changes/2026-10-10-exception-continuity-refactoring-r2.md). 초기 CSS 미적용 기준선은 보존하고 원본 DOM/CSS의 정적 참조 비교로 구분한다. 화주 부분 완료 후속·실기기·Azure·운영 효과·commit/push는 범위 밖이며 다른 작업 변경을 보존했다.

## 살뜰마트 기사 수락·픽업·전달 소프트웨어 연결 r58 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.implementation.r58.md)·[개발 반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.r58.work-order.json)에 따라 실제 마트 원천 전용 기사 수락→전표 입력 즉시 픽업→전달과 FDriver 독립 화면을 연결했다. 보호 전표·요청 영수증·SQL Event/후속 Outbox를 저장하고 지연 원장 소비·최초 수락 경합에서 과거 SQL 상태로 되돌리지 않는다. 음식 주문 위장과 기존 화물 명령의 우회를 차단한다.
- `Implemented / LocalValidated / SimulationConsumerBound / OperationalDeferred`. 최종 Fast 262건·전체 역할 앱 build·Task 10,299건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀. 승인 hash와 최종 검증 전후 소스·시험 44개 지문이 일치한다. [변경·실행 근거·한계](../Changes/2026-10-10-ssalddel-mart-driver-work-r58.md). Simulation·기본 OFF·기존 세 기능 관문을 유지하며 Migration은 준비만 했다.
- SQLite Controller/Service와 시험용 커뮤니티 저장소 증거이며 실제 MySQL/Mongo/Redis·인증 운영 HTTP·네이티브 화면 렌더·단말·카메라·프린터·결제/지급·외부 알림·commit/push·배포는 미실행이다. 다음은 창고 담당자의 실제 전표 발급 화면과 격리 인증 HTTP·DB 종단 검증이다. 다른 스레드의 기존 변경과 아래 이전 기준선을 보존한다.

## 역할 앱 예외 후 업무 지속 보완 (2026-10-10, 로컬 검증 완료)

- [승인·개발 반환](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.implementation.r1.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.r1.work-order.json)에 따라 C01~C09의 접수 보호 저장/결과 우선 확인, 분산 사진 재개, 화물 완료 확인, 지원 사건→연결 배송 복구, 음식 요청 결과 확인·문제 접수, 지도 재개와 늦은 조회 응답 보호를 보완했다. 기존 원장 권위·업무별 책임·다른 dirty 변경을 유지했다.
- `Implemented / LocalValidated / DeviceAndOperationalDeferred`. 최종 Fast 543개·전체 역할 앱 build·Task 10,180개 통과, 실패 0·Mongo 환경 시험 15개 미실행. 390×844 제품 컴포넌트 실제 렌더 10항목·현행 OS 선언 63개도 통과했다. [변경·실제 렌더·한계](../Changes/2026-10-10-exception-continuity-hardening.md)에 원시 근거를 연결한다. Task 근거는 `artifacts/local/validation/20261010-181751/`이며 이전 조사 927개를 이번 결과로 재사용하지 않는다.
- 화주 Mongo→SQL/운임·인계 부분 완료 복구, C04 사진 업로드 자체 및 재시작 원본, 웹/C06/운영자 화면 수명 경계와 실제 보호 저장·단말·운영 HTTP/DB·Outbox·배차·알림·입금은 후속 검증이다. Azure·실기기 설치·영상·MYBOX/SNS·Unity·commit/push·배포는 실행하지 않았다.

## 살뜰마트 배차 소프트웨어 연결 기반 r57 (2026-10-10, 이전 기준선)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.implementation.r57.md)·[개발 반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.r57.work-order.json)에 따라 실제 마트 원천 읽기 Adapter, 포장 완료 후속의 명시적 재확인 Event, 공통 큐/확정 기사 조기 반환의 원천 충돌 보호를 구현했다. `Implemented / LocalValidated / SoftwareConnectionFoundation / OperationalDeferred`. 아래 기존 r56 표본과 다른 작업의 증거를 보존한다.
- Fast 149건·전체 역할 앱 `Ssalddel.v3.5.slnx` build·Task 10,180건 통과, 실패 0·환경 의존 시험 15건 미실행. 실제 Service 연결은 격리 SQLite·시험 전용 준비 소비자·Mongo/Redis 기록용 경계로 검증했다. 승인 기획 hash와 검증 전후 소스/시험 14개 지문이 일치한다. [변경·실행 근거](../Changes/2026-10-10-ssalddel-mart-dispatch-connection-r57.md)에 최초 실패와 동시 작업의 시험 수정 보존도 구분한다.
- 기사 전용 소비자는 미구현이므로 제품 `MartLastMileConsumerBindingUnavailable` 보류를 유지한다. 실제 기사 콜·수락/픽업/전달·인증 운영 HTTP·MySQL/Mongo/Redis 종단·단말·프린터·자동 durable 복구·commit/push/배포는 이번에 수행하지 않았다. 기존 지도 SDK·푸시 설정과 패키지 경고는 남아 있다.

## 역할 앱·업무 OS 예외 후 업무 지속 검증 (2026-10-10)

- [조사·우선순위](../Changes/2026-10-10-cross-app-exception-continuity-audit.md): 8개 역할 진입점의 주요 복구 경로와 10개 OS·62개 책임 단계의 현행 계약을 대조했다. 화주/생활 배송 신규 접수의 미확정 번호 보존, 화물 완료 후 재조회, 운영자 사건→원 배송 복구, 분산 사진의 화면 종료 후 재개, 공통 음식 요청 결과 조회와 지도/수입 준비의 늦은 응답 처리를 보완 대상으로 남겼다. 제품 수정 요청으로 확대하지 않았다.
- 현재 소스 빌드 후 관련 시험 529+281+117=927개 서로 다른 시험명 모두 통과, 실패·건너뜀 0. 현행 카탈로그 검사 63건 통과. 소스 7,864개 해시가 빌드 전/후·보충 시험 후 같았다. 시험 통과가 이번에 발견한 미포함 실패 경로의 정상 동작을 뜻하지 않는다.
- `SourceAuditGapsFound / RelatedLocalTestsPassed / ProductRuntimeNotVerified`. 근거는 Git 제외 `artifacts/local/exception-continuity-audit-20261010/`. 실제 단말·Azure·외부 DB/기관·푸시·입금·운영 완주, APK 설치, 제품 코드 변경·commit·push·배포는 하지 않았다. 관련 없는 기존 변경을 보존했다.

## 살뜰마트 전표 출력·스캔 즉시 픽업 r56 (2026-10-10)

- [승인·개발 반환 r56](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-package-pickup.implementation.r56.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-package-pickup.r56.work-order.json)에 따라 기존 격리 표본에 포장 전 80mm 전표 출력·포장 담당자/기사 관점·QR 스캔 즉시 표본 픽업·네 자리 대체를 구현했다. 전표·요청·인수는 SQLite에 저장하며 준비·현재 판본·합성 기사·중복 관문을 유지한다. r54 조사·r55 선택과 r52/r53 이전 증거는 보존한다.
- `Implemented / LocalValidated / AndroidDeferred / OperationalDeferred`. preview build·집중 C# 69건·JS 62건·3폭 UI 21건·실제 QR 픽셀 디코딩 12건과 Fast를 통과했다. 관련 역할 앱 전체 `Ssalddel.v3.5.slnx` build와 Task 실행 9,993건 통과, 실패 0건·Mongo 환경 시험 15건 미실행이다. 늦은 인쇄 callback 결손을 수정했으며 UI 종료 후 242초의 서버 오류 0·stderr 0바이트와 소스/호스트 지문 변화 0건을 확인했다. [실제 렌더·샘플 이미지](../Changes/2026-10-10-ssalddel-mart-package-pickup-r56.md)는 합성 자료이며 확정 UI가 아니다.
- 실제 인증 HTTP·운영 MySQL/Mongo/Outbox·마트 기사 앱·Android 촬영/설치·물리 인쇄·호스트 재시작 복구는 미검증이다. 카메라·인쇄 창은 모의이며 기존 `NU1903` 의존성 경고는 남는다. 원본 입력·운영 플래그·마트 기사 차단 정책과 관련 없는 변경을 보존했고 commit·push·배포하지 않았다.

## 살뜰마트 선반 카메라 입력 r53 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-camera-scan.implementation.r53.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-camera-scan.r53.work-order.json)에 따라 기존 표본의 선반 코드 입력만 QR·Code128 카메라로 보완한다. 합성 연습 표찰과 직접 입력을 함께 제공하고 상품명·수량 확인·피킹 완료 관문은 보존한다.
- 별도 preview build와 관련 C# 39건·JS 수명 30건·가상 카메라/모의 디코더 UI 27건·수동 UI 3건·실제 생성 표찰 디코딩 12건이 통과했다. Fast/Task routing 검사는 `eng/`·문서만 대상으로 build/test 자동 실행을 생략하며 명시적 실행 증거와 구분한다. 창 종료 예외·늦은 권한 정리·Code128 축소 판독을 보완했다. [변경·실제 렌더 샘플](../Changes/2026-10-10-ssalddel-mart-camera-scan-r53.md), 확정 UI 아님.
- loopback 표본의 브라우저 입력이며 실제 Android 촬영·MAUI/운영 DB/API/Outbox 결속은 미검증이다. 기존 SQLite 의존성 `NU1903` 경고는 남는다. 마지막 브라우저 활동 이후 221초의 지연 회로 종료 로그도 서버 오류 0·stderr 0바이트로 확인했다. 기존 r52·원본 입력·관련 없는 변경을 보존했고 commit·push·배포는 하지 않았다.

## 살뜰마트 피킹·포장 표본 r52 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-picking-sample.implementation.r52.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-picking-sample.r52.work-order.json)에 따라 기록 3건의 상품명·수량은 보존하고 업무 표현은 **살뜰마트**로 정했다. 원본 사업자 명칭·원문 주문 식별자·가격은 입력의 비공개 계보로만 둔다.
- 전용 loopback·Development·Simulation·격리 SQLite에서 기존 피킹·포장 UseCase와 전체 주문 선행 관문을 구현·검증했다. 취소 작업·분할 포장·중복 Event·일반 출고 호환을 보존한다. 선반·SKU·재고·기사는 합성이며 모의 인계를 실제 배차·배송 완료로 보고하지 않는다. 기존 작업·원본 DB·운영 플래그를 보존했다.
- Fast 101건·Task 9,963건 통과, 실패 0건, Mongo 환경 시험 15건 미실행. 관련 역할 앱/미리보기 build와 5모드 × 3폭 UI 15건·캡처 51장, 소스 15개·입력·빌드 hash 불변을 확인했다. [변경·검증 기록](../Changes/2026-10-10-ssalddel-mart-picking-sample-r52.md)에 합성 상품 **샘플 이미지, 확정 UI 아님**을 보존한다. 실제 마트 기사·MAUI 앱 결속·MySQL/Mongo·실물 스캔·운영 Outbox 종단은 미검증이며 기존 SQLite 의존성 보안 경고는 후속이다. commit·push·배포는 하지 않았다.

## 전국 생활 공간 입고 재고 관리 코드 기반 r51 (2026-10-10)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.implementation.r51.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.r51.work-order.json)에 따라 기존 공간·동의·선반·입고를 재사용하고, 한 공간의 인증·페이지 재고 조회와 참고 자료의 지역 밖 장애 격리를 구현했다. 입고 예정·실수령·가용·예약·기존 불량 수량을 분리하며 관계없는 사용자와 원천 계보 불일치를 차단한다. [변경·검증 기록](../Changes/2026-10-10-nationwide-storage-foundation-r51.md).
- `Implemented / LocalValidated / OperationalDeferred`. Fast 102건·관련 앱 `Ssalddel.v3.5.slnx` build·Task 9,926건이 통과했고 실패 0건, 환경 의존 MongoDB 15건은 미실행이다. 기획 hash 일치·독립 범위/코드 검토·검증 전후 소스/시험 8개 지문 변화 0건과 문서 Fast·로컬 링크 88개·공백 검사를 확인했다. 기능 기본 OFF·Simulation을 유지하며 전국 실운영·실제 MySQL/Mongo·단말·공용 DB 변경·문의 발송·commit/push·배포는 하지 않았다. 전체 workspace 페이지화·대량 성능·실제 불량 분리 입고는 후속이며 r48~r50과 이전 작업은 보존한다.

## 5개 지역 공통 사전 문의 양식 r50 (2026-10-10)

- 사용자 선택에 따라 [공통 문의 원본](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-common-inquiry.template.r50.md)과 [범위·지역별 준비 대장](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-common-inquiry.design.r50.md)을 만들었다. 특정 단지·공간이 없는 일반 질의로 구분하며 중랑구·동대문구·광진구·노원구·구리시의 공식 수신처·회신은 별도로 관리한다. [r49 조사·특정 공간 양식](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-document-drafts.design.r49.md)과 이전 구현·검증은 보존한다.
- `CommonTemplateScopeAccepted / TemplatePrepared / ProductImplementationDeferred / SubmissionDeferred`이며 문서 준비만 승인된 범위다. 문서 범위 Fast·`git diff --check`, 신규 문서 로컬 링크 5개·공백 검사와 독립 읽기 검토를 통과했다(build/업무 시험은 생략). 제품 코드·DB·앱·Unity·이메일·민원 접수·허가/운영 설정은 변경하지 않았다. 실제 관공서 연락·발송·접수·단말·commit/push·배포는 수행하지 않았다.

## 아파트 생활권 물류 거점 적용 검토 r48 (2026-10-10)

- [후속 검토안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/apartment-shared-space.review.r48.md)에 r30/r31/r32의 보관·입고·피킹·인계·정산 코드와 아파트 사용 권한·담당 위임·공유 용량·출입 시간·검수/격리·철회 후 반출 결손을 연결했다. 기획 README·목차만 함께 갱신하며 기존 승인·구현은 보존한다.
- 상세 정책은 `ProposedDesign / ImplementationApprovalPending`이다. 현재 조문과 소스를 읽어 대조했으며 제품 코드·DB·앱·Unity·운영 설정은 변경하지 않았다. 문서 범위 Fast·`git diff --check`와 새 검토안의 로컬 링크 14개 확인을 통과했다(build/업무 시험은 생략). 실제 현장·단말·실입고·지급과 commit/push·배포는 수행하지 않았다.

## 주문자 메뉴 사진·기록 자료 연결 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-ORDERER-MENU-CATALOG/orderer-menu-catalog.r1.md)에 따라 기존 주문자 공개 메뉴의 대표 이미지 표시와 비공개 기록 메뉴 화면을 구현했다. 574주문·391지점·511메뉴 관측을 보존하고 사진 117파일·146연결·123메뉴를 검토한다. 명시 단가 없는 항목금액·주문합계는 현재 판매가로 등록하지 않는다.
- Fast 219개·공통 UI 74개·320/390/1280px 정상/미설정/손상/운영금지 12장면이 통과했다. 전체 앱 빌드와 Task 실행 9,886개가 통과했고 Mongo 환경 15개는 미실행이다. 원본·운영 DB·기존 주문 계약을 보존하며 구현·실행 증거는 [변경 기록](../Changes/2026-10-10-orderer-menu-catalog-r55.md)에 기록한다.
- 후속 사용자 요청은 기존 Azure Blob 구성 재사용이며 최종 범위는 **로컬 준비까지만**이다. 기존 어댑터·`community-public`/`platform-private` 배포 구성을 확인하고 원본 120파일을 바꾸지 않는 비공개 Blob 명세를 준비했다. 준비 시험 31개 통과·symlink 권한 시험 1개 미실행이며 정확한 계정 주소·실제 비공개 ACL·업로드는 미확인이다. 메뉴 상세 갤러리·운영 등록·단말 설치·공개·영상·commit/push는 하지 않았다.

## 실제 배달 기록 기반 로컬 검증 r54 (2026-10-10)

- [승인 r54](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/delivery-history-local-replay.r54.md)에 따라 기존 574건·391지점·510픽업을 기사 내역·음식점 관계 검증에 연결했다. 개인 자료는 Git 제외 로컬 파일이며 고객정보·원문 화면을 옮기지 않는다. 모의 상태/시각과 실제 관측값을 구별한다.
- Fast 194개·Task 실행 9,824개 통과(Mongo 환경 15개 미실행). 실제 SQLite 메모리 DB와 기사 조회 UseCase에서 17일별 대조, 320/390px 각각 574건·36페이지 및 같은 기록의 음식점 연결/복귀·메뉴·미확인값을 확인했다. 전체 솔루션 Android 컴파일과 별도 단말 배포/설치는 구별하며 이번 USB·운영 DB·외부 공유·commit/push는 없다. [변경·검증 기록](../Changes/2026-10-10-delivery-history-local-replay-r54.md).

## 기사 배달료 상세·현재 수행 안내와 USB 설치 r53 (2026-10-09)

- [승인 r53](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-detail-work-copy.r53.md)에 따라 후속 3·4번인 배달료 상세 두 열 정렬·합계/공제/정산/지급 구분, 현재 수행 단계 제목·버튼 문구·주소 분리·중복 안내 제거를 구현했다. 전체 지도·독립 카드·기존 명령·계산·개인정보 수명을 보존한다.
- 최종 Fast 341개·Task 실행 9,813개 통과(Mongo 환경 15개 미실행), 시작 스타일 중복 오류를 수정한 ARM64 0.1.53/53을 2026-10-10 00:32 KST USB 업데이트 설치했다. 단말/로컬 APK 해시 일치·약 60초 정상 유지·내 정보/배달 내역/복귀 권역/경험 공유의 조회 화면 이동·복귀를 확인했다. 기기 접근성 텍스트/버튼 근거이며 시각 배치·지도·서버 데이터는 미검증이다. Docker 오류로 서버 업무 검증은 보류한다. [변경·검증 기록](../Changes/2026-10-09-driver-detail-work-copy-r53.md). USB는 후속 요청에서 해제됐으며 이후 로컬 검증만 진행한다. 실제 업무·입금·Azure·Unity·영상·commit/push는 하지 않았다.

## 기사 내 정보 메뉴·배달 내역 r52 (2026-10-09)

- [승인 r52](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.md)에 따라 내 정보의 배달내역 우선 메뉴·독립 설정 화면·아이콘 행 버튼과 완료 목록의 행 선택·상세 복귀 위치를 구현했다. 복귀 권역/경험 공유의 기존 초안·계정/판본 보호, 지도·독립 카드·전경 자동업무·GPS/배차 수신을 유지한다.
- 관련 Fast 162개·Task 9,786개 통과, Mongo 환경 시험 15개 미실행. 최종 Android 컴파일·v2/v3 서명된 ARM64 APK를 확인했다. [변경·검증 기록](../Changes/2026-10-09-driver-profile-history-r52.md)과 [작업 범위](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.work-order.json)에 결과를 갱신한다. 실제 단말 설치·운영·서버/DB·Unity·영상·commit/push는 이번 범위에 포함하지 않는다.

## 음식 기사 책임 메타데이터·탐색 지도 r51 (2026-10-09)

- [승인 r51](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-code-metadata.r51.md)의 `food-driver-work` 핵심 45단계와 명시 소스 결속 41개를 구현했다. 계약·SQL/후속·현재 조회·정산·서버 조율·순수 표시·공통 셸 포트와 네이티브 암호화 복구를 구분한다. [탐색 안내](../Architecture/FoodDriverCodeNavigation.md)·[생성 지도](generated/food-driver-code-map.md).
- 전용 manifest와 `--work-area food-driver` 생성/검사, 필수 단계·소스·심볼·XAML/코드부·reparse 보호, scoped 검증 routing을 연결했다. Fast 264/264·Task 12,751개(서버·공통 UI 9,780 / Simulation 2,001 / Unity 970) 통과, 별도 Mongo 환경 시험 15개 미실행. 기존 기본 지도·E 책임 검사·독립 CLI 실패 분기·서명된 ARM64 기사 APK를 확인했다. 업무 본문 35개와 검증 소스 46개 보존 근거는 [변경 기록](../Changes/2026-10-09-food-driver-code-metadata-r51.md)에 남긴다.
- 기능 동작·공개 계약·DB 스키마·디자인은 변경하지 않는다. 실제 단말·GPS·지도·원격 알림·공용 DB·Azure·운영·지급·Unity·영상·commit/push는 제외하며 기존 dirty 변경을 보존한다.

## 음식 기사 DB–API–화면 리팩토링 r50 (2026-10-09)

- [승인 r50](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-vertical-refactor.r50.md)에 따라 시작 요청 ID·동시 시작 보호·SQL 시작 후속 복구, 수락/도착/픽업/전달의 영속 후속 예약, 정산 없는 현재 조회와 공통 복구/표시 정책을 구현했다. r49의 운행 시작 한 번·GPS 자동 확인·명시적 OFF·전체 지도·독립 카드와 기사 본인의 수락을 유지한다.
- 최종 Fast 1,061/1,061·Task 실행 9,701/9,701·공통 모바일 18/18장면 통과, 별도 Mongo 환경 시험 15개 미실행. 소스 3,058개와 화면 소스/실행 DLL 15개가 검증 전후 같다. 추가형 SQLite migration 시험·MySQL SQL 생성·EF 모델 일치·서명된 ARM64 기사 APK를 확인했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-vertical-refactor-r50.md).
- 거절의 기존 두 단계 저장과 종료 인덱스 정리 실패의 로그 처리 경계는 유지했다. 실제 단말·지도/GPS·원격 알림·공용 DB 적용·Azure·실운영·실지급·commit/push는 미실행이며 기존 dirty 변경을 보존한다.

## 기사 운행 시작·자동 갱신 r49 (2026-10-09)

- [페이지 책임 r13](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#운행-시작과-자동-갱신-r13--사용자-후속-기준)에 따라 운행 시작 한 번으로 기존 신규 배차 수신 ON까지 연결했다. 정상 수행의 수동·당겨서 새로고침을 제거하고 GPS·전경 주기·알림으로 확인한다. 전체 지도·독립 카드·지도 토글·내 정보 종료와 기사 본인의 배차 수락을 유지한다.
- 응답 유실은 정본을 자동 조회하고 미확정 수신 요청의 같은 ID로 복구한다. 시작 POST를 자동 재전송하지 않으며 명시적 OFF를 덮어쓰지 않는다. 미확인 운행 상태·계정 변경·늦은 응답을 보호하고 일반 위치 업로드 실패와 업무 조회를 분리했다.
- 최종 Fast 680/680·Task 실행 9,620/9,620·공통 모바일 18/18장면 통과, 환경 의존 Mongo 15개 미실행. 소스 22개와 화면 소스/실행 DLL 13개의 해시가 검증 전후 같다. 기존 로컬 네이버 SDK 설정을 포함한 ARM64 기사 APK를 생성했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-automatic-work-r49.md). 실제 단말·지도/GPS·원격 배차/알림·Azure·실운영·commit/push는 미실행이며 기존 dirty 변경을 보존한다.

## 기사 지도 배차 토글·내 정보 운행 종료 r48 (2026-10-09)

- [페이지 책임 r12](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#지도-배차-수신-토글내-정보-운행-종료-r12)에 따라 신규 배차 받기를 지도 왼쪽 위 토글로 옮기고 운행 종료는 내 정보 맨 아래에 모았다. 전체 지도·독립 카드·상태별 업무와 진행 배달 중 종료 차단을 유지한다. 정상 수동 위치 갱신 대신 전경 자동 갱신을 사용하고 오류 복구·현위치 이동은 보존한다.
- 응답 유실 시 토글은 확인 필요/비활성으로 표시하고 정본 조회 뒤 복구한다. 늦은 위치 결과·계정/운행 변경을 보호하고 HTTP 위치 보고가 DB 운행 상태를 다시 시작하지 않게 했다. 요금·지급·DB 스키마는 변경하지 않았다.
- 최종 Fast 577/577·Task 실행 9,564/9,564·공통 모바일 14/14장면 통과, 환경 의존 Mongo 15개 미실행. 소유 소스 29개와 UI 소스/실행 DLL 11개가 검증 전후 같고 최신 기사 APK를 생성했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-map-controls-r48.md). 실제 단말·지도/GPS·백그라운드·Azure·배포·commit/push는 미실행이며 기존 r47 등 dirty 변경을 보존한다.

## 음식 배달 경험 공유 r47 (2026-10-09)

- [승인 r47](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-experience-sharing.implementation.r47.md)에 따라 기사·음식점·주문자의 현재 업무에 확인된 앱 완료 건수를 보조 문구로 연결한다. 같은 장소의 동시 주문도 건수로 세며 방문 횟수나 신뢰 점수로 표현하지 않는다. 기능과 타인 공유는 기본 OFF다.
- 공통·전문 기사 화면, 음식점 진행 영역, 주문자 전달 안내 및 선택 주소 검색의 식별자 전달을 구현했다. 미확인·조회 실패·종료·재배정·공유 철회 뒤의 타인 요약은 숨긴다. 음식점은 도착 여부를 추정하지 않는 중립 안내를 사용한다. [페이지·코드·DB 책임](../ProjectOverview/page-docs/delivery-experience-sharing-r47.md).
- 구현·로컬 검증 완료 / 운영 미활성. 최종 Fast 88/88·Task 실행 9,526/9,526·공통 Razor 모바일 38/38장면 통과, 환경 의존 Mongo 15개 미실행이다. Task 전후 소스 49개와 UI 소스/실행 DLL 12개의 지문 일치를 [변경 기록](../Changes/2026-10-09-delivery-experience-sharing-r47.md)에 결속했다. EF idempotent migration SQL 생성·세 앱 APK hash 수집을 확인했으며 실제 DB 적용·인증 HTTP·APK 패키지/서명·휴대폰·Azure·원격 알림·실운영·지급·commit/push는 미실행이다. 이전 r45/r46·주문 원장 r7 기록을 보존한다.

## 주문 원장 구조 보완·총괄 관리자 명세 r7 (2026-10-09)

- [승인 r7](Planning/공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/order-ledger-admin-refactor.implementation.r7.md)의 음식·물품 업무 인계를 대조하고 음식 루트 배송 연결·legacy 읽기 호환·단계 판독/목록 페이지·연결/분리 응답 공개 보호·기사 최신 조회 실패 차단과 정본 복구를 구현했다. 별도 상태 원장 없이 각 전문 상태 권위와 전달/수령/정산/지급 구별을 유지한다.
- 총괄 관리자 주문 진행·기사별 업무·문제 처리·정산/지급 확인 명세를 완성했다. 최종 Fast 886개·Task 9,462개 통과(각각 환경 의존 Mongo 12/15개 미실행), 음식 기사 Android Debug build 성공·APK 생성, 소유 C# 21개 검증 전후 동일을 [변경 기록](../Changes/2026-10-09-order-ledger-admin-refactor-r7.md)에 결속했다. 관리자 화면·지사별 권한은 후속 범위다.
- 파기된 루트/배송의 신규 읽기 참조 복원을 차단하고 기존 명시적 관계 이력은 보존했다. 실제 SQL/Mongo HTTP·제품 화면·단말·지도/알림 작동은 미검증이며 지도 SDK ID 미설정과 기존 Android API 수준 등을 포함한 경고 37개가 남는다. 기존 r45/r46과 다른 변경을 보존하며 운영·실지급·배포·commit/push는 수행하지 않았다.

## 회원 생활 배송 한 건 완주 r46 (2026-10-09)

- [승인 r46](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/member-delivery-completion.implementation.r46.md)의 최초 사진 촬영 권한, 협업 회원 배송 접수, 실제 상대 수령 확인을 구현했다. 같은 요청의 부분 저장/동시 완료 결과를 회복하며 전달·수령·지급을 합치지 않는다. 별도 알림 r45와 기존 변경을 보존한다.
- 최종 Fast 190개·Task 실행 9,458개가 통과했고 환경 의존 Mongo 시험 15개는 미실행이다. 회원/협업 집중 198개와 별도 UI 48개도 통과했다(중복 합산하지 않음). 모의 자료의 실제 제품 Razor 32화면·두 폭의 네 단계 클릭, 일반 앱 Debug APK 0.1.46/146의 서명·내장 실행 파일·빌드 지문을 검증했다. 소유 소스 27개는 최종 검증 전후 동일하다. [화면 책임](../ProjectOverview/page-docs/neighborhood-member-completion-r46.md)·[변경·검증 기록](../Changes/2026-10-09-neighborhood-member-completion-r46.md).
- 비소유 동시 변경으로 먼저 실패한 두 빌드 기록은 보존하고 반영 후 재검증했다. Docker 응답/서비스 권한 문제로 전용 MySQL·Mongo 인증 HTTP는 실행하지 못했다. 기능/Operational 기본 OFF; 실제 단말·원격 푸시·정산/지급·Unity·배포·commit/push는 하지 않았다. 잠긴 수령 intent 뒤 동의/기한 검증 실패의 자동 해제 및 기존 Mongo 운송 투영의 회원/협업 필드 추가는 후속이다.

## 공통 운영 알림·월 50만 원 운영 준비 r45 (2026-10-09)

- [승인 구현 r45](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/operator-alerts.implementation.r45.md)에 따라 관리자 알림 목록/상세, MySQL 알림·채널 outbox·확인 요청, 음식/생활 배송/화물/지원 사건 관찰, 재발·재시도·09시 KST 요약을 구현했다. 확인은 실제 업무 해결과 분리하며 외부 문구는 개인정보를 포함하지 않는다.
- Fast 82개·Task 9,413개 통과, 환경 의존 MongoDB 14개 미실행. 320/390 폭 16개 실제 Razor 화면, Firebase 조건부 컴파일, 키 없는 관리자 Debug APK, Bicep/Compose 및 로컬 암호화 백업·변조 차단을 확인했다. [페이지·코드·DB](../ProjectOverview/page-docs/operations-alerts-r45.md)·[실행 기록](../Changes/2026-10-09-operations-alerts-r45.md).
- 감시·외부 발송·예산 제한 기본 OFF. 실제 수신·유료 Azure 배포·공용 DB migration·실기기·off-VM DB 복구·실거래/지급·commit/push는 제외한다. 월 500,000원은 배송비와 분리한 배분안이며 실제 가격/하드 상한이 아니다. 별도 r46 회원 배송 변경은 보존하며 그 구현 결과를 이번 알림 완료로 대신 보고하지 않는다.

## 지역 거래·무료 배차 운영 시나리오 대조 r44 (2026-10-09)

- [운영 시나리오 r44](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/operating-scenarios.review.r44.md)에 직접 수령·직접 전달·회원 배송과 참여자 부재·동시 수락·응답/사진 실패·취소·미수령·미지급·정보 열람을 역할 행동 → 화면 → 서버/원장 → 운영자 개입 → 결과로 정리했다. 사용자 선택은 본인이 운영자와 배송 참여자를 겸하는 작은 지역 거래다.
- 기존 집중 시험 162개 통과·실패/건너뜀 0개. 소스 대조에서 최초 촬영 권한의 순환 조건, 협업/회원 배송 접수 조건 충돌, 전달 완료와 수령/협업 완료의 미연결을 확인했다. 공통 분쟁 경로는 있으나 완료 후 회원 화면과 정산 원천의 연결은 후속이다. 현재 정보 열람은 수락 후 72시간 이내이며 완료/취소 시 즉시 숨긴다.
- 이번 변경은 운영 조사·문서화다. 제품 코드·DB·APK·정책 활성화·휴대폰·Azure·실거래/실지급은 변경하거나 검증하지 않았다. 기능/Operational 기본 OFF와 이전 r43 실행 기록을 유지하며, 개별 시험 통과를 회원 거래 전체 완료로 보고하지 않는다. 문서 4개 지정 Fast·차이 검사와 추가 영역 로컬 링크 26개 검사를 통과했다. Fast는 문서 변경으로 빌드/시험을 생략했으며 집중 162개는 별도 실행 결과다.

## 일반 회원 생활 배송 참여 구현 r43 (2026-10-09)

- [승인 r39](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/member-delivery.implementation.r39.md)에 따라 일반 계정의 선택 참여·비공개 활동 기준점·근처 동시 제안·건별 수행 권한·사진 완료·앱 알림 복구를 연결한다. 기존 의뢰/운송원장·확정 요금·수수료 0원과 음식/화물 독립 상태를 유지한다.
- 구현과 최종 로컬 검증·APK 빌드를 완료했다. Fast 369개·Task 실행 9,331개가 통과했고 환경 의존 기존 시험 14개는 실행되지 않았다. 모의 자료로 제품 화면 30개와 네 단계 클릭 흐름을 확인했다. [r43 변경 기록](../Changes/2026-10-09-neighborhood-member-delivery-r43.md)에 로그·APK·화면과 Firebase/단말/Azure 미검증 범위를 결속한다. 기능/Operational 기본 OFF이며 미성년 배차·혼합 다건 수행·실지급을 활성화하지 않는다.

## 기사 기준 위치·권역 복귀 우대 구현 r42 (2026-10-08)

- [승인 r38](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-return-area.implementation.r38.md)에 따라 기사 비공개 기준점·자동 7/6km·수동 선택·판본 저장/재조회와 실제 음식 큐·화물 추천/조율의 전체 일정 복귀 가점을 구현했다. 새 정책 선택 시 기존 복귀 가산을 중복하지 않고 현재 음식 최대 3건과 차량·시간·수락 조건을 유지한다. 공동 음식/화물 일정은 r36 후속으로 남기며 해당 진행 업무가 있으면 가점을 보류한다.
- 집중 184개·최종 Fast 157개 통과, 최종 Task 9,235개 통과·실패 0개·기존 환경 의존 Mongo 시험 13개 미실행이다. 새 설정의 실제 격리 Mongo 시험과 음식 기사 Android 빌드는 통과했다. 기존 화물 일정은 선택 경로의 구간 적재 근거를 연결하기 전까지 가점을 보류한다. [r42 변경 기록](../Changes/2026-10-08-driver-return-area-r42.md)에 이번 실행 결과를 기록하며 이전 시험 수와 실제 단말 검증을 혼용하지 않는다.

## 기사 기준 위치·권역 복귀 배차 제안 r37 (2026-10-08)

- [권역 복귀 설계 r37](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-return-area.design.r37.md)에 오늘/기본 복귀지·화물 개인 추천/시간대 조율의 차이와 음식 실제 큐 미연결을 소스 대조로 정리했다. Web의 현재/복귀 좌표 재사용·선호 코드 불일치, 음식 앱 미전달, 화물 선제 10개/음식 정렬 앞단을 우선 보완점으로 남겼다. 기사 선택 기준점·7km 예시 반경과 해제 조건·전체 일정 종료 후 복귀 개선·중복 가산 방지·후보 탐색/화면의 순서를 제안했다.
- 현재는 읽기 조사와 문서 정리다. 제품 소스·배차 정책·DB·APK·실행 환경을 바꾸지 않았고 정적 확인을 실제 화면/업무 검증으로 표현하지 않는다. 수치/가점은 미확정 후보이며 구현·시험·휴대폰은 후속 범위다. 이번 문서 경로의 차이/링크 검증을 별도 수행하고 이전 구현의 시험 수는 재사용하지 않는다.

## 기사 선택에 따른 음식·생활 물품 다건 수행 설계 r36 (2026-10-08)

- 사용자는 기사 본인의 적재량과 수행 수준에 맞춰 3·4건 등 여러 배차를 선택해 수입을 챙길 수 있는 방향을 요청했다. [설계 r36](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-flexible-multi-work.design.r36.md)에 현재 음식 고정 3건·화물의 기존 용달 일정/적재 검사·분산의 다른 업무 차단을 대조하고 기사 개인 선호 → 공동 여력/영속 수락 보호 → 지도 추가 후보·혼합 순서의 보완 순서를 정리했다.
- 음식/화물의 독립 원장·상태·정산을 유지하며 공통 여력과 선택 화면을 연결하는 제안이다. 현재 읽기 조사·문서 반영만 진행했고 제품 소스·정책·DB·APK·단말·실운영·수입 변화·Unity·영상·commit/push는 변경/실행하지 않았다. 문서 검증은 이번 소유 경로에 한정하며 r41 전체 시험 결과를 이 설계의 실행 검증으로 재사용하지 않는다.

## 원주문 선택·관계 동시 변경 보호 r41 (2026-10-08)

- [승인 r35](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/root-order-selection-concurrency.implementation.r35.md)에 따라 기존 분산 작성 화면의 보호된 원주문 선택과 Mongo 관계 변경의 다문서 동시 보호를 구현했다. 최종 Fast 308개·Task 9,147개 통과, 실패·건너뜀 0개. 실제 격리 Mongo 관계 보호 시험 19개와 제품 화면 예시 호스트의 선택·실패/재조회·빈 후보를 확인했다. [화면 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)·[반환 기록](../Changes/2026-10-08-root-order-selection-concurrency-r41.md)에 근거를 남긴다. 다른 조사 항목·APK·Unity·공용 DB·운영 활성화·commit/push는 제외한다.

## 주문 한 건의 역할·조건·다음 행동 리팩토링 r40 (2026-10-08)

- [승인 r34](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/order-ledger-role-refactor.implementation.r34.md)에 따라 기존 주문 루트와 분산 출고/운송 연결, 진행된 연결의 변경 제한, 음식 조건 갱신의 표시/실행 판정, 주문 상세의 현재 업무 안내와 문제 접수를 보완했다. 새 테이블·상태 복제 없이 독립 수령·정산·지급과 기존 OFF/Simulation 경계를 유지한다.
- Fast 323개·Task 9,078개 통과, 실제 격리 MongoDB 환경 시험 2개 미실행. 제품 Razor/공유 ViewModel 검토 호스트에서 주문 상세 4상황·두 폭과 일시 조회 실패 후 재시도를 확인했다. [주문 상세 책임](../ProjectOverview/page-docs/OrdererApp/OrdererApp-P05/README.md)·[분산 상세 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)·[검증 반환](../Changes/2026-10-08-order-ledger-role-refactor-r40.md)을 따른다. 당시 남은 원주문 선택 양식과 하위 상태 동시 변경의 원자적 보호는 위 r41에서 구현·로컬 검증했다. 실제 업무 HTTP/공용 DB·APK·단말·Azure·금전·Unity·영상·commit/push는 r40에서 진행하지 않았다.

## 건별 계약 정책 준비 서버 r39 (2026-10-08)

- [승인 r33](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/task-contract-policy-preparation.implementation.r33.md)에 따라 새 건별 의뢰의 프리랜서 계약 준비 기본값과 미성년 참여 OFF 설정 후보를 서버에 마련하고 로컬 검증했다. 인증된 정책 조회는 실행 허가를 반환하지 않으며 활성화 설정은 서버 시작과 직접 호출에서 거부한다. 전용22개를 포함한 Fast101개·Task9,008개 통과, MongoDB 환경 시험2개 미실행. 기존 정산 DB 시험의 내부 provider 격리를 보완했고 원 검증 항목을 보존했다. 소스8개는 시험 전후 동일하며 다른 기존 변경15,131개와 기존 공용 문서 본문을 보존했다. [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-task-contract-policy-r39.md)·[검증 반환](../Changes/2026-10-08-neighborhood-task-contract-policy-r39.md)에 연결한다. 실제 계약·대리인 확인·미성년 배차·앱·APK·운영 전환은 이번 범위가 아니다.

## 생활 거래 정산 서버·원장 r38 (2026-10-08)

- [승인 r32](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/neighborhood-settlement.implementation.r32.md)의 음식·보관 상품·운송 약정/실제 비용/지급 신고/수령 확인을 서버·보호 원장·인증 API에 구현하고 로컬 검증했다. 공동 배송비 부담, 실물 보관 기간, 부분 지급·환급, 중복 청구와 기존 기사 지급 중복 생성을 구별한다. Fast 48개·Task 8,986개 통과, 기존 환경 의존 MongoDB 시험 2개 건너뜀. 컴파일 대상 22개 소스가 시험 전후 동일하고 기존 다른 변경 15,105개를 보존했다. [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-settlement-r38.md)·[실제 검증](../Changes/2026-10-08-neighborhood-settlement-r38.md)에 연결한다. 앱 화면·APK는 사용자 요청으로 보류하며 실제 송금·공용 DB·휴대폰·Azure·Unity·영상·commit/push는 이번에 진행하지 않았다.

## 분산 보관 합의·사진 완료 r37 (2026-10-08)

- [r31 보관 합의·사진 완료](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/storage-agreement-photo-proof.implementation.r31.md)의 모바일·서버 구현과 로컬 검증을 완료했다. 공간/실제 물품 소유자의 같은 조건 판본 동의, 입고·피킹·전달·반품 사진 촬영/미리보기/전송과 정확한 업무·수량 결속, 비공개 열람·보존 보호를 연결했다.
- 최종 Fast 313개·Task 8,938개 통과, Mongo 환경 시험은 각 1개·2개 건너뛰었다. 최종 컴파일 대상 90개 파일의 시험 전후 소스가 같고, 작은 화면 31종·두 폭 62조합의 넘침·오류가 없었다. [페이지·코드·DB 책임](../ProjectOverview/page-docs/distributed-storage-photo-proof-r37.md), [실행·설치 파일 기록](../Changes/2026-10-08-distributed-storage-photo-proof-r37.md)을 따른다. 후속 사용자 요청으로 APK 추가 제작은 보류했으며 네 앱 최종 manifest 완료는 미확인이다. 기존 r36 default OFF·Simulation 경계와 Unity·배달 영상·다른 dirty 작업을 보존하며 공용 DB·휴대폰·Azure 검증은 별도다.

## 생활 공간의 분산 물류 모바일·서버 연결 r36 (2026-10-08)

- [승인 구현 r30](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/distributed-warehouse.implementation.r30.md)의 공간 동의·분할 입고·재고 예약·기사 직접 피킹·다거점 순회를 모바일·서버에 구현하고 로컬 검증했다. 최신 사용자 정정으로 Unity는 제외하고 기존 변경을 보존했다.
- [페이지·코드·원장 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md), [변경·검증](../Changes/2026-10-08-distributed-fulfillment-r36.md)을 따른다. 기본 OFF·정확한 Simulation profile이며 기존 DB 적용·휴대폰·Azure·실운영·실금전·commit/push는 이번에 하지 않는다. Fast 181개·Task 8,868개 통과, 기존 Mongo 환경 시험 2개 건너뜀. 공통 UI 20화면·두 폭 40조합과 네 역할 Debug APK 0.1.36/36을 확인했다. APK 설치·실행은 미확인이다.

## 합의 요금표와 배차·주문 알림 r35 (2026-10-08)

- 사용자가 우선순위 3·4번과 메시지/소리 알림을 승인했다. [구현 범위 r4](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/agreements-alerts.implementation.r4.md), [페이지 책임](../ProjectOverview/page-docs/agreed-rates-notification-settings-r1.md), [변경·검증](../Changes/2026-10-08-agreed-rates-native-alerts-r35.md)을 따른다.
- 음식점·음식기사·화주·화물기사의 요금표 제안/응답과 실제 업무의 명시 선택, 기존 음식/화물 계산·배차·정산 근거 연결을 구현했다. 합의 기사의 기존 적격 조건과 직접 배차 수락, 음식 조리 순서, 원 금액 동결과 SQL/Mongo 보류 재시도를 유지한다.
- 음식 기사와 음식점 Android에 ‘살뜰 배달’·‘살뜰 주문’ 메시지와 짧은 자체 기계음을 연결했다. 기사 내 정보에서 서버 설정을 저장/재조회하고 음식점은 계정별 기기 설정을 저장한다. 중복 발행·계정 변경·야간 무음·OS 차단/무음을 구분한다. 다른 역할의 새 알림 사건과 음식점 강제 종료 뒤 원격 푸시는 추가하지 않았다.
- 최종 Fast217개·Task8,708개 통과, 실패0·격리 Mongo 환경 시험2개 미실행이다. 전체 빌드와 소스77개 전후/인계 지문, 공통 UI42개·예시16시나리오를 확인했다. 요금표 EF snapshot과 기존 금액을 보존하는 격리 migration3개 시험도 포함한다. 두 개인 Debug APK는 `0.1.35/35`이며 실제 내장 화면/음원/계약·양 ABI와 지도/카드/아이콘·기존 서명을 대조했다. 고정 소스2,718개·현재 모바일 패치31개/기반89개와 이전 r34 소스/APK를 결속했다. 기사 APK에 Firebase 수신기가 없어 현재는 앱 열린 상태의 조회 알림이고, 식당은 실행 중 SignalR/재조회다. 백그라운드 푸시 설정·실폰 소리/업무·기존 DB 적용·Azure·실금전·commit/push는 미실행이며 다른 dirty 작업은 보존한다.

## 기사 전체 화면 지도와 독립 하단 카드 r34 (2026-10-08)

- 사용자는 지도/카드 보완 1·2번 승인 뒤 카드 높이에 맞춰 지도 크기·중심·줌을 바꾸지 않도록 정정했다. [책임 r10](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#전체-화면-지도와-독립-하단-카드-r10--최신-사용자-정정), [이번 변경과 검증](../Changes/2026-10-08-driver-fullscreen-independent-card-r34.md)을 따른다.
- 수행 지도는 같은 인스턴스로 전체 영역을 채우고 하단 카드만 겹쳐 접기/펼치기한다. 지도 공간 예약 계산은 제거했으며, header·도구의 투명한 빈 공간은 입력을 통과시키는 설정을 적용했다. 내 정보/내역·현장 중단·수명·상태별 주 행동·r33 아이콘·API/DB/GPS·경로 handler는 보존한다.
- 독립 읽기 대조에서 기존 Binding/명령/가시·권한/이벤트와 주요 subtree 보존을 확인했다. Fast 187개·Task 8,608개 통과, 실패 0·격리 Mongo 조건 2개 미실행이며 소스 4파일은 전후 지문이 일치한다. 기사 Android·전체 솔루션 빌드는 오류 0개다. 최신 개인 Debug APK는 r34(`0.1.34/34`)이며 승인 제품 3파일만 별도 보존 소스에 반영했다. 양 ABI의 전체지도/독립 카드·카드 배치의 카메라 무접촉·기존 아이콘/GPS/교통 OFF·지도 입력/서명과 Fast/Task 소스 결속을 확인했다. 실제 화면·지도 입력·카드 조작 중 native 카메라/크기·테마/좁은 폭은 다음 USB 검증이다. 기기·에뮬레이터·애저·실운행·실지급·commit/push는 실행하지 않았다.

## 내 정보·배달 내역과 상태 카드 행동 아이콘 (2026-10-08)

- 배달 내역을 내 정보 안으로 옮기고 사람 아이콘을 붙였다. 기존 목록·상세·복귀 경로와 3일 고객 정보 열람 기준을 유지한다. 상태 카드 아이콘까지 포함한 최신 개인 Debug APK는 r33(`0.1.33/33`)이며 포장·양 ABI의 내장 XAML/helper/공통 UI/폰트/CSS와 검증된 제품 10파일 지문 대조를 완료했다. 실폰 설치는 하지 않았다.
- 후속 요청으로 상태별 카드 버튼에 문구와 행동 아이콘을 함께 표시한다. 네이티브 버튼은 기존 글자 색과 확장/운행/결과 대기 상태를 따르고, 공통 역할 카드는 안정적인 행동 키의 보조 아이콘만 선택한다. API/DB·명령·허용 행동·확인 절차는 보존한다. [변경·검증과 APK 판본](../Changes/2026-10-08-driver-profile-state-action-icons.md)을 참조한다.
- Fast 관련 254개·Task 전체 8,597개 통과, 실패 0·Mongo 환경 조건 2개 건너뜀이다. 검증 전후 12파일 지문이 일치한다. 첫 HTML 구조 시험 실패는 기존 허용/차단 조건을 유지하며 실제 control에 결속했고, 이후 CLR 시험 호스트 중단은 부분 결과로 보존한 뒤 동일 설정의 새 실행에서 완주했다. 실제 휴대폰 배치·터치·테마 검증은 별도다.
- 사용자의 후속 요청으로 에뮬레이터 화면 검증을 중지했다. 소유한 에뮬레이터만 종료했고 기존 자료는 보존했다. 다음 휴대폰 USB 연결 때 최신 APK·실제 카드/아이콘·탐색을 검증하고 설치한다. 로컬 빌드/시험, APK 준비, 실제 기기 화면 검증을 구분한다.

## 실제 휴대폰 조회·메뉴 저장·연결 복구와 위치 진단 (2026-10-08)

- 잠금 해제 후 기사 r30의 네이버 지도 교통정보 선 숨김, 완료 목록→상세→복귀·새로고침, 음식점 메뉴 저장·재조회·원복, 네 음식 역할의 동일 완료 주문 표시를 실폰에서 확인했다. USB 연결 제거 후 종료/재실행의 실패 안내와 복구 후 세션/배차 대기도 확인했다. [실제 결과와 화면](../Changes/2026-10-08-physical-phone-runtime-r31.md)에 기록했다.
- SQL/Mongo 완료 주문 보존 비교 10항목·메뉴 원복 7항목이 통과했다. 정산 공제/수령액 미확정·지급 NotRequested를 유지하고 기존 서버/DB를 초기화하지 않았다. 일반 음식 원장 읽기 보완은 현재 실행 서버에 미적용이다.
- Android Debug의 위치 서비스 한 파일에 좌표 없는 단계 진단을 추가했다. Fast 68/68·Task 8,589개 통과(Mongo 환경 2개 건너뜀), r30 대비 그 1파일만 포함한 기사 `0.1.31/31`의 포장·내장 설정·단말 설치 바이트를 대조했다. 실제 단발 위치 수신 7회와 중단 조회의 취소/세대 차단·앱 복귀 재시작을 확인했다. 연속 리스너 수신·실주행/GPS 안정성은 미확인이다.
- 새 휴대폰 주문 작성/수락 전 취소·3일 만료 경계·지원 사건 처리·실제 도로 경로·애저·실지급·다른 네 역할과 commit/push는 이번 완료 범위가 아니다. 원시 단말/위치/계정 자료는 Git 제외 영역에 유지한다.

## 음식 기사 지도 교통정보 표시 끄기 (2026-10-08)

- 도로를 따라 보이는 주황색·초록색 교통정보 레이어를 숨기도록 지도 페이지와 공통 속성 기본값을 변경했다. 픽업 주황·전달 파랑·대기 회색 업무 경로/핀과 위치 복구는 유지한다. [변경과 확인 수준](../Changes/2026-10-08-driver-map-traffic-off.md)을 참조한다.
- Fast 기사 빌드·Task 전체 3.5 빌드 통과, 전체 8,589개 통과·실패 0·Mongo 환경 조건 2개 건너뜀. 소스 두 파일은 검증 전후 지문 일치다. r29 소스·APK를 보존한 별도 r30 사본의 양 ABI에서 실제 내장 교통정보 기본값/XAML false를 대조했다. 같은 서명의 `0.1.30/30`을 단말에 덮어 설치하고 판본·설치 바이트 해시 일치를 확인했다. 설치 후 기록 도구의 변수 충돌은 재설치 없이 고쳤으며 앱 자료를 초기화하지 않았다. 후속 실폰 지도/통신과 r31 위치 진단 결과는 위 최신 기록을 따른다.

## 실제 휴대폰 USB 후속 검증·위치 복구 (2026-10-08)

- USB 인증·5363 연결·격리 서버 준비 상태를 재확인했다. 기존 완료 주문의 SQL/Mongo 상태·식별 검사 12개 통과, 정산 `AwaitingDeductions`·지급 `NotRequested`·공제/수령액 null을 유지한다. [이번 변경·완료 경계](../Changes/2026-10-08-physical-usb-followup.md)는 전날 수락 이후 실폰 시험과 구별한다.
- 기사 위치 원인별 안내·조용한 리스너 재취득·단일 공유 조회·60초 실패 재시도·늦은 응답/센서 시작 정리를 보완했다. Fast 206/206 통과. frozen r27에 승인한 제품 6개만 추가한 기사 `0.1.29/29`를 기존 서명으로 덮어 설치하고 실제 설치 바이트/판본을 대조했다. 계정 자료·원본 소스는 보존했으며 새 APK의 실행·GPS·서버 통신까지 완료한 것으로 보지 않는다.
- 일반 음식 원장 목록/상세는 권한 뒤 음식 블록 상태로 표시하는 읽기 보완을 소스에 반영했다. 전달/수령을 구분하고 DB·업무 상태·Revision·시각·JSON 구조를 보존한다. Fast 35/35·독립 검토 통과. 최종 Task 전체 3.5 빌드 오류 0·8,589개 통과·Mongo 환경 조건 2개 건너뜀, 소스 13파일 전후 지문 일치. 이 서버 보완은 현재 r27 컨테이너에 아직 미적용이다.
- r29 기록 당시 휴대폰은 잠금 해제 대기였다. 이후 완료 내역·메뉴 저장/원복·앱 복귀/통신 복구를 확인한 위 최신 기록을 현재 상태로 참조한다. r29 당시 근거는 Git 제외 `artifacts/local/physical-usb-r29-20261008/`에 보존한다. Azure·실제 도로 경로·실운행·실결제/입금·다른 역할 시험 준비·commit/push는 이번에 실행하지 않았다.

## 실제 휴대폰 네이버 지도 연결·USB 시험 (2026-10-07)

- 최신 사용자 승인으로 기존 네이버 클라우드 `hongdal` 앱에 `kr.ssalddel.fdriver`를 추가하고 기존 패키지/API/한도는 보존했다. 음식 기사 Debug `0.1.28/28`을 기존 서명으로 덮어 설치했으며 실제 Samsung Android 13에서 네이버 지도 타일·현재 위치·배차 대기를 확인했다. 키는 저장소 밖에 두고 Client Secret은 모바일에 복사하지 않았다.
- 음식점의 USB 시험 설정 누락은 기본 OFF·정확한 판본·Android Debug·HTTP 루프백 5363 한정으로 보완했다. Fast 79·Task 8,540개 통과(Mongo 환경 조건 2개 건너뜀), 음식점 후속 APK도 `0.1.28/28`로 설치·해시 대조했다. 기존 PC 생성 주문 한 건을 실제 휴대폰에서 음식점 수락→기사 수락→조리/픽업/전달→주문자 수령 확인하고 운영자 완료 조회까지 대조했다. 신규 주문 작성·실운행·금융 검증과 구별하며 [변경·검증과 한계](../Changes/2026-10-07-physical-phone-naver-maps.md)를 참조한다.
- `mirror-r27-food-mobile-check`의 기존 격리 DB를 보존했다. 마지막 주문은 수령 확인·정산 `AwaitingDeductions`·지급 `NotRequested`다. 주황/파랑 핀과 카드 전환은 확인했지만 실제 도로 경로는 대기 상태이며 Azure·실운행·실지급·다른 역할 지도·commit/push는 포함하지 않는다. 원시 단말 캡처는 현재 위치를 포함하므로 Git 제외 `artifacts/local/physical-usb-r27-20261007/`에 보관한다.

## 생활 배송 간편 작성·엑셀 입력 r29 로컬 검증 완료 (2026-10-07)

- 사용자의 `어` 승인으로 생활 배송 간편 작성·엑셀을 먼저 구현했다. [승인 r29](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/simple-input.implementation.r29.md) SHA256 `1D6D6CA37CDBEC26B5D5050B968C66F461E1F45AE82D7A2BACD5B0FAB0434BDE`·[수용/반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r29.md)·[페이지 책임](../ProjectOverview/page-docs/neighborhood-simple-input-r29.md)·[실제 결과와 샘플](../Changes/2026-10-07-neighborhood-simple-input-r29.md)을 연결한다. r28과 다른 dirty 변경은 보존하며 상태는 `Implemented / LocalInputValidated / OperationalE2ePending / VoiceDeferred`다.
- 기존 한 페이지에 물품→장소→시간→확인과 상세 작성 복귀를 연결했다. 고정 판본의 빈 `.xlsx` 양식과 한 행 미리보기/적용을 추가했으며 누락값은 임의 보완하지 않는다. 인증/동의 뒤 메모리로만 파싱하고 업로드/선택은 견적·접수가 아니다. 기존 견적·동의·접수/재조회, 불명확 결과의 원 요청을 유지한다. 전달 시간창은 선택 null, 행 ID 없는 재업로드의 중복 보장은 세션 안으로 한정한다. 신규 입력 route만 4 MiB 메모리 버퍼를 사용해 디스크 임시 저장을 막는다.
- 신규 70·Fast 453·Task 8,540개 통과, 환경 조건 Mongo 2개 건너뜀. 소비 앱 포함 build 오류 0·기존 Android 패키지 경고 55, 검토 호스트 오류/경고 0. 실제 파서·공통 Razor/VM 320/390px 36장면·6흐름에서 오류·가로 넘침·외부/API 요청 시도 0을 확인했다. 화면은 합성 검토 샘플이지 확정 시안이 아니다. 검토 호스트 종료와 5400 listener 없음도 확인했다. 실제 인증 HTTP/실DB 종단·APK·휴대폰·음성/마이크·외부 업체·실금전·Unity·commit/push·배포는 실행하지 않았다.

## 생활 앱 외부 배송 가정 연결 r28 로컬 구현·검증 완료 (2026-10-07)

- [승인 r28](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/assumed-providers.implementation.r28.md)·[소유권/수용·반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r28.md)·[페이지 책임](../ProjectOverview/page-docs/neighborhood-fulfillment-r28.md)·[변경과 샘플](../Changes/2026-10-07-assumed-fulfillment-r28.md)을 따른다. 기존 r25 수행처에 카카오 문서 기반/배민·쿠팡이츠 가정 모의를 추가했고, 선택 크기를 원 의뢰·원장·SQL 재구성에 보존했다. 실제 endpoint·업체 가격은 추정하지 않았다.
- 후보 목록은 서버의 원천별 활성 목록으로 표시한다. 가정 후보는 별도 opt-in 기본 OFF·Simulation 생활 배송 한정이다. 현재 원천·동의·시도·모의 job이 일치하는 전달 증거로 완료 확인만 열며 내부 기사/큐·양측 완료·공개 이력·지급을 자동 확정하지 않는다. 기존 직접 전달·자체/음식 배송과 다른 dirty 변경을 보존한다.
- 신규 73·Fast 701·Task 8,470개 통과, 환경 조건 Mongo 2개 건너뜀. 전체 3.5 build 오류 0·경고 102, 실제 공통 UI 320/390px 22장면·4흐름·외부 요청/가로 넘침/page error 0을 확인했다. 샘플 PNG는 확정 시안이 아니다. 전용 preview는 종료했다. 상태는 `Implemented / LocalSimulationValidated / RealProviderPending`이며 공용 DB schema·현재 인증 HTTP/실DB 종단·실제 업체·APK/단말·실금전·Unity·commit/push/배포는 미실행이다.

## 첫 수평 보완 완료: 기존 역할 옵션의 적용 범위 r3 (2026-10-07)

- 사용자가 전반적인 보완을 먼저 진행하고 후속에 한 업무를 정해 수직 검증하는 순서를 수용했다. [승인 r3](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/horizontal-hardening.implementation.r3.md)와 [현 작업트리 수용·소유권](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/development-work-order.r3.md)에 기존 비금전 표시 범위·정확 경로·검증 상한을 결속했다. 현재 결과는 `FirstSliceImplemented / LocalPreviewValidated / LocalOnly`이며 승인 기획 본문/hash는 유지한다.
- 공통 역할 화면에서 조회 반경/주문 범위, 이번 주문 조리시간/기본값, 건별 사유·판정, 의뢰 예정액/실지급, 이번 현장 작업/개인 설정을 구별한다. 기존 서버 행동·금액·권한·원장과 동시 r26/r27 변경은 보존한다. 알림 소비·기본값 영속화·실제 요금표 합의 연결은 이번 표시 보완의 완료 주장이 아니다.
- [재조사 r2](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/role-options.review.r2.md)와 승인 r1 본문/hash는 보존한다. 신규 회귀 9개·Fast 집중 187개·Task 8,397개 통과(Mongo 2개 건너뜀), 전체 솔루션 오류 0·경고 111, preview 오류/경고 0. 실제 공통 Razor 320/390px 12개 장면과 입력/확인/복귀/미리보기 저장 차단 6개 흐름을 확인하고 [변경 기록](../Changes/2026-10-07-role-option-scope-r3.md)에 반환했다. 화면은 합성 예시이며 확정 시안이 아니다. 전용 preview는 종료했고 실제 업무 DB/API·APK 배포/휴대폰·실금전·Unity·운영·commit/push는 실행하지 않았다.

## 로컬 검증 완료: 음식 배달 네 역할과 USB 시험 APK r27 (2026-10-07)

- [페이지 책임](../ProjectOverview/page-docs/food-mobile-four-role-r27.md), [변경·검증](../Changes/2026-10-07-food-mobile-four-role-r27.md)을 따른다. 주문 작성 → 음식점 수락 → 서버 제안·기사 직접 수락 → 조리·픽업·전달 → 수령 확인을 격리 MySQL/Mongo와 실제 공통 화면으로 확인한다.
- r27 완료 당시 범위는 로컬 검증까지였다. 해당 기록에서 애저·실제 휴대폰·GPS/외부 경로·결제/입금·commit/push는 제외하며, 이후 사용자 승인으로 진행한 실제 휴대폰·네이버 지도는 위 후속 기록과 구별한다. 같은 checkout의 다른 r26 배송비 변경은 보존한다. 최종 검증 현황은 변경 기록을 참조한다.
- 마지막 표시 보완을 포함한 동결 소스의 Task 8,397개 통과·Mongo 환경 시험 2개 건너뜀, 공통 UI 42화면·14검사와 같은 새 주문의 SQL/Mongo 상태·식별자 검사 11개 통과. Mongo 요약 단계 일치 검증은 별도 한계로 유지한다.
- Debug 0.1.27/27 네 APK의 서명·소스 전후·사본 해시와 에뮬레이터 설치 바이트를 확인했다. 실제 로그인·역할 첫 화면 4개 성공, 원래 AVD/registry 해시 불변을 확인한 뒤 에뮬레이터를 종료했다. 네이티브 전체 업무·실폰·Azure·지도/GPS·실지급의 증거로 확대하지 않는다.

## 음식 배송비 선택 옵션과 부담 확인 r26 구현·로컬 검증 완료 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-cost-sharing.implementation.r26.md), [페이지 책임](../ProjectOverview/page-docs/delivery-cost-sharing-r26.md), [변경 기록](../Changes/2026-10-07-delivery-cost-sharing-r26.md)을 따른다. 음식점의 주문자 전액/정액/비율/음식점 전액과 주문자의 잔여 부담 상한을 연결한다.
- 원 주문 정책 전체 동결·공통 누적 배분·전액 한도 초과 보류·구형 정액과 진행 주문 보존을 구현했다. 집중 249·Fast 190·Task 8,388 통과(Task 실제 Mongo 2개 건너뜀), 공통 UI 새 40·기존 26장면과 소비 앱 포함 빌드를 확인했다. 기본 OFF Simulation이며 실제 업체·운영 DB·청구/지급·단말/Azure·외부 공유·commit/push는 별도다. 로컬 기준선은 `artifacts/local/fulfillment-cost-sharing-r26/`다.

## 현재 구현: 생활·음식 배달 자동 수행처 선정 r25 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment-routing.implementation.r25.md), [페이지 책임](../ProjectOverview/page-docs/fulfillment-routing-r25.md), [변경 기록](../Changes/2026-10-07-fulfillment-routing-r25.md)을 따른다. 기존 생활 배송 비교를 음식 배달로 확장하고 플랫폼 자동 선정·조건 내 접수/재선정을 연결한다.
- 음식점 정액 지원은 주문 전체에서 한 번만 적용하며 주문자 잔여 상한·시간·동의·유효 기사 확보와 조리 순서를 검증한다. Unknown/취소 미확정·비용 미확인은 새 수행을 막는다. 구형 경로와 다른 dirty 변경을 보존한다.
- `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 집중 138개·Fast 344개·Task 8,234개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 제품/소비 앱 빌드와 실제 공통 Razor 26개 모바일 장면을 확인했다. SQLite 서비스·대역 인증/경로·공용 MySQL 미적용을 구분하며 실제 업체·운영 DB·결제/지급·단말/Azure·커밋/푸시는 별도다. 원시 근거는 `artifacts/local/fulfillment-routing-r25/`다.

## 현재 구현: 생활 배송 수행 방식 선택 r24 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment.implementation.r24.md), [페이지 책임](../ProjectOverview/page-docs/neighborhood-fulfillment-r24.md), [변경·검증](../Changes/2026-10-07-neighborhood-fulfillment-r24.md)을 따른다. 우리 앱 접수의 자체 배차 또는 모의 외부 위탁 선택을 기존 작성·상세·사유 지도에 연결한다.
- 후보의 비용·시간 조건을 비교하고 사용자가 확정한다. 외부 선택은 자체 큐를 생성하지 않으며 접수·취소 결과 미확정에서 다른 수행을 자동 접수하지 않는다. 기본 비활성화된 Simulation 기능이며 실제 플랫폼/결제·Unity·커밋/푸시·배포는 이번 범위에서 제외한다.
- `Implemented / LocalSimulationValidated / RealProviderPending`. 최종 Fast 536개·Task 8,162개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 소비 앱 빌드 오류 0. SQLite 영속·동시 확정/재시작·실제 Mongo builder→SQL projector·loopback HTTP와 실제 공통 UI 22개 모바일 흐름을 확인했다. 실제 MySQL migration/동시성·업체 API·운영 로그인·휴대폰·Azure·지급은 별도다. 새 서버 연결 전에 원 의뢰 두 열과 세 수행 테이블의 migration이 필요하며 기본 플래그 꺼짐도 스키마 요건을 생략하지 않는다.

## 현재 보완: 생활 교류 약속과 선택 배송 r23 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/life-exchange-facilitation.implementation.r23.md), [페이지 책임](../ProjectOverview/page-docs/neighborhood-collaboration-r1.md#생활-교류-약속과-선택-배송-보완-r23), [변경·검증·화면](../Changes/2026-10-07-life-exchange-facilitation-r23.md)을 따른다. 기존 생활 지도의 선택 패널과 독립 작성 화면을 재사용하고 약속 상세의 상태/행동·조건·전달·금액을 분리했다.
- 직접 수령/전달에서는 기사 비용을 안내하지 않고, 금액 미정과 0원을 구분한다. 기사 배송은 추천/수락·전달/수금 상태를 분리한다. 배차 방식 변경 응답이 불명확해도 상세와 원 선택 결과 확인을 유지하며 기존 동의·취소·철회·반환·공개 선택을 보존한다. API·DB·요금·권한·플랫폼 결제/자동 정산은 변경하지 않았다.
- Fast 378개·Task 8,084개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 소비 앱 빌드 오류 0. 실제 공통 Razor/VM에 읽기 전용 예시 DTO를 연결한 17상태×320/390px의 표시·실패·복구를 확인했다. 이번 렌더는 실제 서버/DB의 새 업무 완료·APK 단말·Azure·운행·입금 증거가 아니다. Figma node 미연결 상태를 보존한다.

## 현재 보완: 본인 APK 업무 검증 r26 (2026-10-06)

- [화면 책임](../ProjectOverview/page-docs/apk-workflow-completion-r26.md), [변경·검증·설치 경계](../Changes/2026-10-06-apk-workflow-completion-r26.md)를 따른다. 안전한 로그인 복귀와 비공개 증빙 전송·폐기, 음식 기사 원본 주문 문제 접수, 운영자 사건 목록·처리를 기존 API에 연결했다.
- 격리된 Simulation 서버의 같은 음식 주문을 공유 UI/client/HTTP/MySQL/Mongo로 수령 확인까지 처리하고 같은 주문의 지원 사건·이의제기를 확인했다. 시험용 통지 증빙과 외부 발송·실제 입금은 구분한다. 최초 주문 생성은 실제 역할 client 준비이며 네 역할의 네이티브 APK 완주 증거로 확대하지 않는다.
- 첫 목표는 본인 APK 시험 사용이다. Fast 266개·Task 8,053개 통과(환경 조건 2개 건너뜀), 소스 2,203개와 최종 Debug APK 두 개의 설치 해시를 확인했다. Android 에뮬레이터에서 역할 홈·지원 화면 스크롤·복귀·입력 정리·완료 상세·연결 실패/재조회도 확인했다. APK는 5362 루프백 서버와 USB 연결을 대상으로 한다. 실제 휴대폰·지도/GPS·Azure HTTPS·운영 신원기관·공개 운영 준비는 별도 확인하며 서버 관문을 우회하지 않는다.

## 이전 구현: 거래와 개인정보 보호 r25 (2026-10-06)

- 사용자 승인에 따라 사업자 음식 거래와 개인 생활 거래의 공통 보호, 초기 만 19세 이상 거래 신청을 구현했다. [법령·화면·서버·저장 기준](../Architecture/CommerceIntermediaryPrivacy.md), [변경·검증](../Changes/2026-10-06-commerce-privacy-r25.md)을 기준으로 한다.
- 중개 안내·현재 판본 확인·판매자 검증·분쟁/권리/사고 지원과 보존/파기/복원 차단을 기존 업무에 연결했다. 판매자 없는 익명 판매 전환, 오래된 보존 의도·통지 증거 재사용, 불명확한 부분 저장 뒤 완료 표시를 차단한다.
- `Implemented / LocalValidated / OperationalPrerequisitesPending`. 최종 Fast 218개·Task 7,968개 통과와 최종 DLL의 실제 격리 Mongo 2개 통과, 전체 3.5 솔루션 빌드 오류 0, 실제 공유 Razor 320/390px 30장·16개 흐름의 오류/가로 넘침 0. 소스 141개 검증 전후 지문 일치, 범위 밖 파일 1,007개·HEAD·branch 보존을 변경 기록에 결속한다. 기존 APK 관찰과 이번 소비 앱 컴파일을 최종 휴대폰 설치 검증으로 합치지 않는다.
- 기본 신원기관 미연결·보존 전체 목록 미검증·운영주체 미설정 상태에서는 새 Operational 거래를 차단한다. 접수된 주문 재조회·취소·일반 게시판은 구분한다. 실제 발송·전체 사용자 삭제·백업 파기·휴대폰/Azure 검증·법적 적합성 인증·commit/push는 이번 구현 결과와 별개다.

## 현재 보완: OS 생명주기와 Android 설치 검증 r24 (2026-10-06)

- 사용자가 r23의 확인된 문제를 보완하고 APK를 휴대폰에 설치해 직접 사용할 수 있는 수준으로 조정하도록 요청했다. [승인 계획](../ProjectOverview/page-docs/os-lifecycle-hardening-r24-plan.md), [구현·남은 경계](../ProjectOverview/page-docs/os-lifecycle-hardening-r24.md), [변경·검증](../Changes/2026-10-06-os-lifecycle-hardening-r24.md)에 범위를 결속한다.
- 음식 배차 재시도의 상태/최초 시각 보존, 인증 수신자와 공개 동의·알림 transaction, 창고의 공통 상태/수량 관문, 확정 화물 조건 변경 차단, 교육 제출 CAS·영수증 보존 복구, 선적의 과거 이벤트/예외 뒤 상태 보호를 반영했다. 마트는 미지원 기사 연결을 보류하며 새 adapter 완료로 취급하지 않는다. 대장은 10개 OS·62개 책임 단계다.
- 별도 기사 앱의 최종 설치 파일로 완료 내역→상세→목록 복귀와 공제·수령액의 중복 없는 미확정 표기, 3일 상세 열람 기한을 확인했다. 실제 입금으로 표시하지 않는다. 네이티브 표기 수정 전 APK·소스 지문·화면·검증 기록도 보존한다.
- `Implemented / LocalValidated / PhysicalDevicePending`. 최종 Fast 402/402·Task 7,857/7,857, 현행 대장 63/63, 실제 격리 Mongo 32/32와 본인 DB 정리 통과. 실제 MySQL 음식 서버의 같은 주문이 기사 선행 배정→조리→픽업→전달→수령 확인과 300초 관찰을 통과했다. 초기 APK의 화면 밖 로그인 문제를 수정해 두 Debug APK를 재패키징·서명/설치 해시 검사하고 Android API 36/x86_64에서 로그인·업무 조회·목록 전환·세션 거절 복구를 확인했다. 대표 설치용 파일과 USB 안내는 로컬 `artifacts/local/os-lifecycle-hardening-r24/apk/`에 있다. 실제 휴대폰/지도/GPS/Azure/입금과 모든 역할의 운영 완주는 별도다. 마트 adapter·창고 재검수/재계수 등 잔여 경계는 보고서에 유지하며 commit/push·영상/MYBOX·Unity 형상 변경은 하지 않았다.

## 현재 조사 완료: 업무 OS 생명주기·절차·상태 r23 (2026-10-06)

- 사용자가 OS별 생명주기 안의 절차·상태를 폭넓게 정적 조사하도록 요청했다. [전체 조사](../ProjectOverview/page-docs/os-lifecycle-static-audit-r23.md)와 [기계 판독 원장](../ProjectOverview/page-docs/os-lifecycle-static-audit-r23.json)에 등록된 10개 OS, 공통 책임 단계 41개, 절차 연결 112개·상태묶음 101개를 담당·선행 조건·인계·회복·소스·관련 시험에 결속했다. 공통 생명주기가 없는 5개 OS는 관찰 절차로 구분하고 설명 순서를 실행 전이표로 사용하지 않는다.
- `SourceAuditComplete / StaticConsistencyGapsFound / RelatedTestsPassed / ProductRuntimeNotVerified`. 기존 관련 시험 1,582/1,582 통과, 새 정적 도구 4,628개 통과·화면/참여자 대장 불일치 3개 탐지, 오류 입력 21개 탐지를 확인했다. [변경·검증](../Changes/2026-10-06-os-lifecycle-static-audit-r23.md)에 원시 근거와 범위를 정리한다. 기존 시험 통과는 모든 실패 순서·동시성·재시도 경로의 안전성 증명이 아니다.
- 우선순위는 동의자·인증 actor 결속과 음식 Outbox/교육 worker의 상태 회귀 → 창고 기존 진입점의 관문·화주 핵심 조건의 판본/재동의 → 부분 저장·지연 사건 복구 → 인계 연결 미확인 → 공통 대장·문서 정리다. 제품 코드·DB·UI·정산·영상은 변경하지 않았으며 실제 단말·Azure·여러 계정 운영 완주·입금·commit/push는 별도다. 기존 r22와 동시 작업을 보존한다.

## 현재 구현: 생활 물품 전달 선택과 유연한 배차 r22 (2026-10-06)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/flexible-delivery.implementation.r22.md)에 따라 직접 수령·직접 전달·기사 배송, 건별 자동 추천/공개 콜/병행, 확정 전 방식 변경·합의 재확인과 원 요청 복구를 구현했다. [변경·화면·검증](../Changes/2026-10-06-neighborhood-flexible-delivery-r22.md)에 책임과 근거를 결속한다.
- `Implemented / ValidatedLocal / FieldRuntimePending`. 실제 전용 MySQL/Mongo·두 루프백 Operational 서버의 다섯 정상 흐름과 공통 웹의 양측 완료 확인, 10개 예외 흐름을 확인했다. 기존 Simulation·음식/화물·권한·정보 동의 경계를 유지한다. 물품 금액/배송비/실수금은 분리하며 외부 경로·기사 적격성 등 가상 입력과 실제 운행·휴대폰·Azure·입금은 구분한다. 최종 검증 수와 보존 결과는 변경 기록을 따른다.


## 현재 구현: 역할별 필수 절차와 선택 도구 r22 (2026-10-06)

- 사용자 승인에 따라 현재 절차·조건부 절차·복구는 기본 카드에 두고 독립 조회/등록은 기본 접힌 `업무 도구`에서 기존 페이지로 이동하도록 정리했다. 현재 어댑터의 37개 역할·행동 키를 분류하며 미분류/강조/서버 가능 행동은 보존한다. 종료 이력만 남은 경우에도 시작 진입을 직접 제공하고 생활 협업의 취소·거절·철회는 접힌 관리 영역 밖에 둔다. [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#필수-절차와-선택-도구-r22--구현-전-책임-카드)와 [구현·검증·화면](../Changes/2026-10-06-role-tools-r22.md)이 근거다.
- `Implemented / ValidatedLocal / DeviceRuntimePending`. Fast 집중 151/151, 최종 Task 전수 7,733/7,733(신규 64개 포함), Android·Windows 소비 앱 및 공통 미리보기 빌드 통과. 실제 공통 렌더 320/390px 30개 화면·22개 흐름에서 메뉴 접힘/조회·선택 복귀·수행/취소 가시성·넘침/터치를 확인했다. 생활은 실제 전용 지도 컴포넌트로 검토하며 지도 키/실제 타일은 이번 증거에 포함하지 않는다.
- Web/MAUI 생활 배송·공간 목록 wrapper의 안전한 지도 복귀를 보완했다. API·DB·요금/정산·역할 권한은 이번 작업에서 변경하지 않았다. 관련 없는 동시 작업의 변화는 해시 대장에 별도로 기록하고 수정/스테이지하지 않는다. Figma 동기화와 물리 단말·GPS·Azure·다중 계정 서버/DB 업무·입금·commit/push·공개 운영은 별도다.

## 이전 구현: 역할 간 업무 연결 보완 r21 (2026-10-06)

- 사용자 요청에 따라 r20의 H01~H03을 구현했다. 운영자 화물 사건 목록/독립 검토, 화주·생활 기본 보류 안내, 음식/화물 기사 완료 내역의 목록→상세→현재 업무 복귀를 기존 API에 연결했다. [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#역할-간-인계-보완-r21--구현-전-페이지-책임)와 [변경·검증·화면](../Changes/2026-10-06-role-handoff-fixes-r21.md)에 근거를 결속한다.
- 집중 136/136과 Fast·Task 통과, 예시 DTO를 소비하는 실제 공통 웹 렌더 320/390px 24개 상태·6개 이동 흐름을 확인했다. 현재 업무 카드에는 다른 배달 목록을 넣지 않는다. 원장/API/정산 정책과 관련 없는 dirty 작업은 보존한다. Figma 동기화와 실제 서버/DB 업무·휴대폰·Azure·입금은 별도다. 최종 소비 앱 빌드·보존 결과는 변경 기록에 남긴다.

## 이전 조사 완료: 여덟 역할의 업무 인계 r20 (2026-10-06)

- 사용자가 업무 인계를 우선하는 여덟 역할 조화 조사 계획의 실행을 승인했다. [조사·보완 명세](../ProjectOverview/page-docs/role-handoff-audit-r20.md)에 여덟 책임·12개 대표 인계·실패/재시도/권한·확인된 부족의 근거와 수정 책임을 기록했다.
- `SourceAuditComplete / LocalTestsPassed / GapsSpecified / DocumentationValidated / CrossRoleProductRuntimePending`. 관련 21개 시험군 261/261 통과와 현재 화주 adapter의 보류 전/후 매핑 차이를 확인했다. 운영자 화물 예외 UI 연결→의뢰자 기본 보류 안내→통합 기사 완료 내역 진입이 후속 순서다. 문서 Fast/Task와 링크 442개, 이번/이전 기준선의 범위 밖 파일 보존을 확인했다. 제품 코드·API·DB·요금/정산은 변경하지 않았으며 결과와 증거 범위는 [r20 변경 기록](../Changes/2026-10-06-role-handoff-audit-r20.md)에 결속한다.
- 실제 여러 계정의 제품 UI/HTTP/DB 완주·휴대폰/GPS/Azure·push/입금·공개 운영·commit/push는 이번 증거에 포함하지 않는다. r18·r19와 기존 dirty 작업을 보존한다.

## 이전 완료: 여덟 역할의 첫 진입 r19 (2026-10-05)

- 사용자는 중개 수수료보다 서로 협업하고 생활을 개선하는 장을 우선하며 여덟 입장의 완성도를 계속 높이려 한다. [현재 결정](DECISIONS.md#2026-10-05-수수료-중개보다-참여자의-교류와-생활-개선을-우선)에 방향과 후원/광고 후보의 미정 범위를, [역할 진입 검토](../ProjectOverview/page-docs/role-entry-review-r19.md)에 첫 활동·현재 연결·후속 질문을 기록했다.
- `Implemented / ValidatedLocal / DeviceRuntimePending`. 현재 구현 범위에 맞는 홈 설명과 Mirror 표제를 보완했다. Fast 집중 99/99·Task 전수 7,625/7,625 통과, 공통 예시 웹의 320/390px 홈 여덟 링크와 대표 세 역할 진입/복귀를 확인했다. 소비 앱과 공통 예시 웹·Android 빌드, 화면/검증 범위는 [r19 변경 기록](../Changes/2026-10-05-role-entry-review-r19.md)에 결속한다. 범위 밖 기존 dirty 파일 902개 해시와 HEAD·branch를 보존했다. 실제 제품 생활 지도·모든 상태·휴대폰/GPS/Azure·공개 운영은 별도이며 광고·후원·영상 게시·commit/push는 수행하지 않았다.

## 이전 완료: 상태별 업무 카드와 창고 연결 r18 (2026-10-05)

- 사용자가 r17 보완 우선순위에 따라 계속 구현하도록 승인했다. 화물 예외의 재조회·행동 조건, 창고 수령→검수/인계 준비→출고 검토·실제 인계, 음식 복합 상태 안내와 단계별 필수 정보를 기존 화면·API에 연결한다. [책임 기준](../ProjectOverview/page-docs/role-map-workspace-r11.md#상태별-업무-연결과-예외-복원-r18)과 [변경 기록](../Changes/2026-10-05-state-card-polish-r18.md)에 근거를 모은다.
- `Implemented / ValidatedLocal / CardReviewPending / DeviceRuntimePending`. Fast `20261005-224311` 집중 320/320·Task `20261005-224540` 전수 7,625/7,625 통과, 공통 예시 웹에서 화물 보류/재개와 음식 준비 단계, 창고 검수·출고·운송 요청·실제 인계의 독립 입력/지도 복귀를 확인했다. 마지막 19개 확인의 320/390px 가로 넘침과 브라우저 오류 0, 미리보기와 통합 앱 Android 빌드 오류/경고 0. 최종 원시는 `artifacts/local/state-card-polish-r18/`와 변경 기록에 결속한다.
- 기존 dirty 작업과 r17 조사 결과를 보존한다. 실제 휴대폰·GPS·Azure·운영 서버/DB 업무 완료·입금·commit/push는 수행하지 않았다. 이번 예시 실행은 지도 키 미설정 안내/목록 대체 상태이며 r14 타일 검증이나 단말 실행으로 확대하지 않는다. 카드 최종 배치는 사용자 검토 대상으로 남긴다.

## 이전 조사: OS 생명주기와 역할별 상태 카드 r17 (2026-10-05)

- 사용자가 OS의 생명주기·업무 흐름·단계 상태에 맞는 카드 배치를 조사해 달라고 요청했다. [상태 카드 조사](../ProjectOverview/page-docs/os-state-card-audit-r17.md)에 OS 설명 단계·저장 상태·서버 허용 행동·화면 수명을 구분하고 일곱 업무 역할의 공통 카드와 대표 전문 화면을 대조했다. 생활은 별도 동네 지도 흐름이며, 다른 OS는 대장·대표 진입 확인 범위와 미검토 세부 상태를 구분한다.
- `SourceAuditComplete / CardDesignPending / FullStateRuntimeNotVerified`. 실제 음식점·음식 기사 표시 adapter를 읽기 전용 예시 DTO로 실행한 다섯 사례에서 픽업 완료 후 인계 안내와 준비 미완료 재조리의 픽업 안내를 재현했다. 제품 코드는 변경하지 않았으며, 이번 조사에서 실제 앱·서버·DB 업무·휴대폰·GPS·Azure·입금·새 제품 전수 시험·commit/push는 수행하지 않았다. 기존 r16 화면·검증의 범위를 확대하지 않는다.
- 후속은 화물 예외의 읽기 투영·행동 조건, 창고 검수 연결, 음식의 복합 상태 안내를 먼저 보완하는 제안이다. 기본·상세 카드의 정보 배치·강조 순서는 사용자 검토 대상으로 유지한다. 원시 조사와 표시 코드 재현은 Git 제외 `artifacts/local/os-state-card-audit-r17/`에 보관한다.
- 문서 범위 Fast `20261005-212540`·Task `20261005-212553` 통과(제품 빌드·시험 생략). 네 문서의 로컬 연결 201개 누락 없음, 범위 밖 기존 변경 파일 874개 해시 유지와 HEAD·branch 불변을 확인했다.

## 이전 완료: 지도 위치·갱신과 역할별 기본 카드 r16 (2026-10-05)

- 사용자가 r15 제안 1·2·3 진행을 승인했다. [구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-map-reliability.implementation.r16.md)과 기존 [페이지 책임](../ProjectOverview/page-docs/role-map-workspace-r11.md)에 위치 근거·만료·표시 중 갱신·다음 업무 선택·기본 카드 초안을 결속했다. [상태별 카드 검토표](../ProjectOverview/page-docs/role-state-card-review-r16.md)와 [변경·캡처](../Changes/2026-10-05-role-map-reliability-r16.md)에 사용자가 후속 결정할 기본·상세·주행동을 정리한다.
- `Implemented / ValidatedLocal / CardReviewPending / DeviceRuntimePending`. Fast `20261005-203528` 집중 243/243, Task `20261005-203725` 제품 3.5 빌드 오류 0·전수 7,561/7,561 통과. 실제 공통 예시 웹의 일곱 역할 기본 카드·320/390px·위치 만료·목록/상세·다음 업무를 확인했다. 기존 dirty 작업을 보존했고 실제 휴대폰·GPS·서버 업무 완료·Azure·출시·commit/push는 수행하지 않았다. 모든 상태의 카드 디자인을 최종 승인한 결과는 아니다.
- 별도 화물 기사·통합 앱 Android 빌드 오류 0, 최종 미리보기 빌드 오류/경고 0. 새 검증 탭의 브라우저 오류 0과 작업 밖 기준선 1,066개 파일 해시 유지를 확인했다. Android 키·타일·실제 단말 실행과 카드 최종 검토는 별도로 남긴다.

## 이전 조사: 역할 앱의 지도 중심 구성 조사 r15 (2026-10-05)

- 다른 역할 앱의 지도·기본 카드·독립 업무 화면을 소스에서 대조했다. [역할별 조사와 보완 순서](../ProjectOverview/role-map-coverage-audit-r15.md)에 통합 여덟 역할과 전용 앱 진입의 차이, 화물 기사·화주·주문자·음식점·창고·운영자·생활의 조정안을 기록했다. 판매자·인적자원 관리 앱은 홈 책임만 확인했다.
- `SourceAuditComplete / Proposed / RuntimeNotVerified`. 우선순위는 화물 네이티브 현재 위치와 카메라 중심 분리 → 선택 업무 갱신·위치 만료·다음 업무 선택 → 소유 화물 운송의 기존 좌표 연결·기본 카드 → 음식 역할·운영자 → 전용 앱 진입이다. 조사 문서·목차·현재 작업 요약만 수정했으며 제품 변경·빌드·실행·APK·Azure·휴대폰·commit/push는 수행하지 않았다. r12~r14와 다른 dirty 작업을 보존한다.
- 문서 범위 Fast `20261005-195131`·Task `20261005-195138` 통과, 조사 문서 링크 42개 누락 없음, 범위 외 기존 변경 파일 841개 해시 유지와 이전 snapshot 보존을 확인했다. 제품 빌드·시험을 수행한 결과는 아니다.

## 이전 완료: 실제 Google 웹 지도 미리보기 r14 (2026-10-05)

- 사용자의 적용 화면 요청에 따라 기존 기사 공통 UI와 제품 웹 지도 host/JS를 로컬 미리보기에 연결했다. 실제 Google 타일 위의 주황 픽업·파랑 전달 핀, 수락 전/픽업 중/전달 중 핵심 카드·상세와 지도 확대/이동·핀 설정을 확인했다. [변경 기록과 화면](../Changes/2026-10-05-google-map-web-preview-r14.md)에 실행 근거를 결속한다.
- `WebMapRuntimeVerified / ExampleWorkflow / AndroidMapKeyPending`. 전용 빌드 오류/경고 0, 320/390px 넘침 없음·브라우저 오류 0, 모듈 HTTP 200/제품 바이트 일치와 Development·루프백·Origin·no-store 키 공급을 확인했다. 키는 프로세스 설정에서만 사용했고 제품/Android 설정과 r12·r13 및 다른 dirty 작업은 보존했다. 실제 GPS·도로 경로·Android SDK 실행·실배차/서버/DB·Azure·휴대폰·외부 배포는 이번 범위가 아니다.

## 이전 완료: Google 지도 SDK 연결 점검 r13 (2026-10-05)

- 기존 Android Google SDK·지도 host·핀/선 연결을 확인했고 통합 앱의 누락 coarse/fine 권한·선택적 위치 기능과 사용자 행동 시 권한 확인/요청·거절 복구를 보완했다. [변경 기록](../Changes/2026-10-05-google-map-sdk-connection-r13.md)에 키 설정·앱 ID/Debug 인증서와 검증 근거를 결속한다.
- Fast `20261005-190402` 통합 앱 빌드 오류 0, Task `20261005-190557` 0.0 slice 빌드 오류/경고 0·전수 7,447/7,447과 실제 생성 manifest의 위치 권한을 확인했다. `AndroidMapKeyPending / MapRuntimePending`: 다운로드한 항목의 종류·Android용 키 설정은 사용자 확인 대기이며 과거 브라우저 키를 임의 재사용하지 않았다. 실제 타일/GPS·기기 권한 대화상자·키 설정 내장 APK·Cloud 설정은 미완료다. r12와 다른 dirty 작업은 보존했다.

## 이전 완료: 완료 확인·기사 기본 카드 r12 (2026-10-05)

- 사용자 승인 보완 1·2에 따라 음식 행동의 실제 후속 상태 확인/조회 전용 복구와 기사 카드의 핵심 정보 기본 표시를 구현한다. [r12 구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-workspace-completion-card.implementation.r12.md)과 [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#완료-확인기사-기본-정보-r12), [변경 기록](../Changes/2026-10-05-role-workspace-completion-card-r12.md)에 검증 결과를 결속한다. r11·기존 dirty 작업·서버 계약/권한은 보존한다.
- `Implemented / ValidatedLocal / PublicDeploymentPending`. Fast 집중 221/221, Task `20261005-184125` 전체 3.5 빌드 오류 0·전수 7,447/7,447, 실제 공통 UI의 기사 세 단계/조회 전용 복구와 최신 내장 Debug APK 서명·정렬·CRC·현재 CSS를 확인했다. UI는 예시 자료이며 실제 서버/DB 업무 완료·휴대폰·Azure·지도 연결과 다음 업무 선택·자동 갱신·목록 확장은 이번 범위가 아니다.

## 이전 완료: 역할 선택 홈·역할별 지도 r11 (2026-10-05)

- 사용자가 승인한 여덟 역할의 선택 홈과 같은 통합 Web/Android 앱 안의 역할별 지도·현재 업무 카드를 구현한다. 생활 화면은 기존 r10을 재사용하고 음식·화물·창고·운영자는 기존 API와 상태별 허용 행동을 연결한다.
- [구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-map-workspace.implementation.r11.md)과 [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md), [변경 기록](../Changes/2026-10-05-role-map-workspace-r11.md)에 입력·인증·지도 복귀와 실제 검증을 결속했다. 최종 Task `20261005-173938` 전체 빌드와 7,402/7,402 시험, 실제 통합 WASM의 역할/로그인 취소/입력 직접 진입·새로고침, 예시 카드 화면과 내장 Debug APK를 검증했다. `Implemented / ValidatedLocal / PublicDeploymentPending`이며 실제 새 역할 UI의 서버 업무 완료·단말·Azure·Android 지도 타일은 별도다.

## 이전 완료: 일반 이용자 지도 작업공간 r10 (2026-10-05)

- 사용자 승인 계획에 따라 SsalddelApp Android, 통합 Web, 독립 01 Community Web의 지도 조회·선택·현재 업무를 패널로 정리한다. 글·조건·공간·배송 등록은 입력 전용 페이지로 이동하고 저장/취소 뒤 기존 지도 맥락으로 복귀한다.
- [r10 구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/map-workspace.implementation.r10.md)과 [구현 전 책임 카드](../ProjectOverview/page-docs/neighborhood-map-workspace-r1.md)에 상태 소유, 레이어·장면·플랫폼 분리, 개인정보와 검증 범위를 결속했다. 기존 업무 원장·기능 관문·전문 역할 앱·r9 흐름과 unrelated dirty 변경을 보존한다.
- 집중 시험 321/321, 최종 Task `20261005-155118` 전체 3.5 빌드(오류 0·경고 55)와 전수 시험 7,297/7,297을 통과했다. 공통 화면의 실제 HTTP/SQLite/보호 Mongo 흐름과 독립 01 WASM의 지도·입력·로그인 취소 복귀, 12개 화면 및 최신 내장 Android Debug APK의 서명·정렬·자산·해시를 확인했다. [변경 기록](../Changes/2026-10-05-map-workspace-r1.md)에 검토용 인증/배송, 실제 단말·Android 지도 키·Azure·Figma의 미검증 범위를 구별한다. `Implemented / ValidatedLocal / PublicDeploymentPending`이며 커밋·푸시는 하지 않았다.

## 이전 완료: 생활 협업·작은 보관 공간 r9 (2026-10-05)

- 사용자 승인 네 항목에 따라 지도 글·보관 능력에서 신청 → 양측 합의 → 수행 → 상대 완료 확인을 연결한다. 같은 계정의 요청·제공·참여 목록, 수량·시간 예약과 별도 인수·반환, 전원 동의 후 최소 공개 이력을 추가한다.
- 개인 합의는 별도 보호 Mongo 컬렉션에, 보관 인계 주소는 암호화해 저장한다. 전문 기사·음식 조리·창고·지급 권한은 기존 업무 경계를 유지한다. 보관 예약과 협업 취소의 교차 경쟁은 영속 intent와 공간 CAS 중단 증적으로 처리한다.
- 집중 시험 308/308, 최종 Task `20261005-140852` 전체 3.5 빌드와 전수 시험 7,256/7,256을 통과했다. 실제 공유 UI→HTTP→SQLite/Mongo의 신청·합의·상호 완료, 보관 등록·예약·인수/반환·비공개 차단과 9개 화면을 확인했고, 최신 내장 Debug APK의 서명·정렬·자산·해시를 확인했다. [변경 기록](../Changes/2026-10-05-neighborhood-collaboration-r1.md)에 검토용 인증/배송과 실제 운영 로그인·개인 단말·Android 지도 키·Azure 운영을 구별한다. 이전 r8 지도와 r7 배송 근거는 아래 보존한다.

## 이전 완료: 공통 생활 지도 홈 r8 (2026-10-05)

- 사용자 승인 계획에 따라 Web와 SsalddelApp Android의 기본 진입을 `/community/map`으로 연결했다. 제공·필요 글, 본인 진행 배송, 선택 공공자료를 지도 또는 목록에서 탐색하고 기기별 표시 설정을 기억한다. 작성·배송 접수·업무 수행은 독립 화면에 유지하며 기존 전문 역할 선택은 `/apps`에 보존했다.
- 초기 공개 위치는 공식 자료로 대조한 서울30행정동의 대표점만 사용한다. 공개 글의 동네 선택은 선택 사항이고 기존 글을 임의 배치하지 않는다. 정확 배송 주소·기사 GPS는 본인 인증과 현재 배정 증적을 통과한 개인 조회에만 제공한다. 최근 위치의 측정·수신 모두10분 이내 검증과 재배정·종결·이탈 시 정리를 추가했다.
- 공개 HTTP·생산용 UseCase·SQLite 저장, 실제 Google Web 지도 타일, 동네 선택→작성→지도 복귀와 설정 저장, 320/390/1100px 화면을 확인했다. 개인 배송 화면은 명시한 검토용 데이터이며 실제 기사 배정/GPS·외부 예정 경로 실행과 구별한다. 집중 시험93/93, 최종 Task `20261005-123154` 전체3.5 제품 빌드(오류0·경고95)와 전수 시험7,168/7,168을 통과했다. [변경 기록](../Changes/2026-10-05-community-map-home-r1.md)에 실행 범위와 원시 근거를 연결한다.
- Android 네이티브 코드 빌드와 최신 코드·런타임을 내장한 Debug APK 생성, 서명·ZIP 정렬·ARM64/x86_64 런타임·사본 해시를 확인했다. 전용 지도 키·실제 타일·개인 휴대폰·Azure 공개 통신은 별도 검증이 남는다. 브라우저용 키를 Android에 재사용하지 않는다. 배차·운임·플랫폼 결제 권위는 유지하며 다른 dirty 작업을 보존했다. 커밋·푸시·외부 배포는 수행하지 않았다.

## 이전 구현: 일반인 생활 교류·생활 배송 r7 (2026-10-05)

- 사용자의 최신 문답 방향과 구현 요청에 따라 [생활 교류 최소판 r6](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/exchange-mvp.r6.md)를 구현했다. 목록·글 작성·공개 문의의 세 라우트를 기존 게시글 API/DB에 연결하고 통합 앱 홈/커뮤니티 메뉴와 01 웹 메뉴에 진입점을 추가했다.
- [생활 배송 r7](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/server-dispatch.r7.md)은 공개 글과 분리된 본인 배송 의뢰를 서버 견적·기존 화물 원장/배차 큐에 연결했다. 선정 기사·추천 차수·기한에 결속한 별도 정보 제공 동의/철회를 추가하고 기사 조회·추천의 개인정보를 보호했다. 접수·제안·기사 수락·현장수금예정을 구별하며 음식 전용 흐름은 유지한다.
- 재고·예약·참여자 배차를 강제하지 않고 중단된 미검증 r5 코드만 로컬 보관 후 실행에서 분리했다. 기존 다른 dirty 변경은 보존했다. 원본의 기본 데이터 상태와 공개 게시·댓글/거래 인증의 의미를 섞지 않는다.
- 실제 공유 컴포넌트/HTTP client → 루프백 API → 생산용 UseCase/쓰기 정책 → SQLite 저장으로 다른 브라우저의 문의와 새로고침을 검증했다. 웹/Windows 통합 앱 빌드와 관련 시험·검증 결과는 [변경 기록](../Changes/2026-10-05-neighborhood-exchange-mvp-r1.md)에 기록한다. Azure·외부 공개·기기 설치·Figma node 검증은 별도다.
- 배송은 격리 원장/큐 시험과 같은 제품 공유 UI의 화면 시험을 구별하며 [서버 배차 변경 기록](../Changes/2026-10-05-neighborhood-server-dispatch-r1.md)에 근거를 기록한다. 다중 서버 DB 멱등성·실제 운영 자동 제안/수락/수행·실입금·Azure 공개 운영은 별도다.

## 이전 확장 설계: 주거 공간의 상품 등록·구매 신청·퀵 배송 r4 (2026-10-05)

- 사용자 요청으로 [주거 상품·퀵 배송 설계 r4](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/residential-goods-quick-delivery.design.r4.md)를 기존 생활권 물류거점 기획에 추가했다. 집·구획·기능·동의 → 보유 상품·재고 → 구매 신청·수량 예약 → 기존 화물 배차·픽업·전달의 정보 관계, 상태, 페이지→API/DB, 권한과 구현 순서를 정리했다. 주거 유형은 새 설계의 참여 제한으로 두지 않으며 법적 요건 검토는 이번 범위에서 제외했다.
- 현재 거점의 비주거 고정·관리자 전용 예약·용량/모의 보상, 판매상품의 필수 입고 연결, 기존 구매 요청과 실제 주문의 차이, 일반 상품과 마트 음식 배송의 경계를 읽기 대조했다. 첫 구현은 집 한 곳·상품 한 종류·주문 한 건을 기존 화물 기사까지 연결하는 순서다. 상세 설계는 제안이며 제품 코드·DB·APK·외부 운영·결제를 변경하지 않았고 새 실행 시험·커밋·푸시는 하지 않았다.

## 이전 방향 기록: 일상 공간 활용을 중심으로 한 사업 협업 (2026-10-04)

- 사용자 요청으로 [일상 공간 활용 방향](DECISIONS.md#2026-10-04-일상-공간-활용을-중심으로-한-사업-협업-방향)을 기록하고 [공용 프로젝트 컨텍스트](../ProjectOverview/GptProjectContext.md)에 연결했다. 기존 공간과 사람의 기능을 활용하는 사업을 지향하며 물류센터 신축을 참여 전제로 삼지 않는다.
- 기존 생활권물류거점의 공간·동의·용량·보상 기반을 읽기 대조했다. 현재 독립 비주거·상온 밀봉품 Pilot 제한과 음식·화물 업무 분리는 유지한다. 다음 설계는 공간과 기능·담당자 연결, 이용 시간·구획·용량 예약, 기존 주문/출고/인계와의 연결이다. 이번에는 문서 방향만 기록했으며 제품 코드·DB·APK·외부 운영을 변경하거나 새 시험을 실행하지 않았다.

## 최근 검증 완료: 휴대폰 없는 Azure·모바일 연결 보완 검증 (2026-10-04)

- [구현·통합 검증 보고](../Deployment/AzureMobileHardeningVerification-20261004.md)에 운영자 음식 관제 독립, 7앱 인증/통신, 음식점·기사 미확정 요청 보존, 음식 전용 푸시 연결, 로그 최소화, 배포·서명·복구 도구를 페이지→코드→API/DB로 연결했다. 이전 조사와 실행 기록은 아래에 보존한다.
- 최종 Task `20261004-220250` 전체 3.5 솔루션 빌드 **오류 0·경고 119**, 전수 시험 **6,975/6,975 통과**. 앞선 Fast495/495, 음식점87/87, Admin84/84도 통과했다. 401 정리·계정 보호·미확정 요청 보존을 유지하며 관찰 배차 재탐색/만료, 기사 완료 상세의 실제 주문 출처 연결, 늦은 통신 오류와 최신 상태 표시를 보완했다. Debug APK 7개를 생성·서명·정렬·내장 런타임 확인했고 전용 에뮬레이터에 설치했다.
- 격리 Development/Simulation 서버·DB에서 같은 주문의 주문자→음식점→기사→주문자 수령 확인→운영자 조회를 실제 APK UI로 완주했다. 별도 DB4컨텍스트 migration·백업/복원·readiness200, 서버 재시작 후 원 주문 재조회, 음식점·기사 응답 유실 뒤 앱 재시작/정본 조회와 중복 쓰기 부재를 확인했다. 최신 기사 APK에서도 완료 상세와 3일 열람 제한 표시, 실제 GET503 안내와 동일 앱 실행 중 오류 해소를 확인했다. 공제 미확정·지급 미요청은 그대로 보존하며 실입금으로 보고하지 않는다.
- 내부 시험 서명의 Release 7개(기존 검증된 2개 재사용·나머지 5개 새 빌드)를 완성하고 서명·정렬·두 ABI·실제 다운로드 해시를 확인했다. 전용 API36 에뮬레이터의 7개 시작 화면과 설치된 base APK의 SHA-256 일치도 확인했다. 음식·화물 기사 OS 알림 권한 안내에서 중단한 원 기록을 보존하고, 권한을 거절한 뒤 로그인 화면을 확인해 검사를 이어갔다. 주소는 시험용 `https://mirror-field-test.invalid/`이며 Release 서버 통신·업무 완주·운영 서명 증거가 아니다. ELF 16KiB 정렬 구조 검사도 실제 16KiB 휴대폰 실행과 구별한다.
- Firebase 설정이 없는 기본 APK는 조회 대체를 유지한다. 선택 설정 경로는 실제 Firebase 패키지의 Android C# 컴파일을 통과했으나 GoogleServicesJson·APK·설치·토큰 등록·외부 수신을 실행한 결과가 아니다.
- Azure 계정의 리소스 조회는 재인증 필요로 실패했다. 실제 Azure·공개 HTTPS·개인 휴대폰·운영 서명·실입금은 미검증이다. 이 경계를 로컬 에뮬레이터/내부 시험 서명으로 대체하지 않는다. 검증용 Docker 프로젝트와 전용 에뮬레이터를 종료하고 APK·DB 볼륨·백업·화면·원 실패 기록을 보존했다. 커밋·푸시는 수행하지 않았다.

## 이전 조사 완료: Azure 서버·휴대폰 APK 연결 준비 (2026-10-04)

- 사용자가 APK 다운로드·개인 휴대폰 설치·Azure 통신의 현재 준비 수준과 부족한 부분을 전수 조사해 달라고 요청했다. [종합 조사와 보완 순서](../Deployment/AzureMobileReadinessAudit-20261004.md)에 7개 모바일 앱·인증/통신·역할 API·배포/서명·DB/health·알림·복구·기존 실행 근거를 대조했다. 제품 수정·새 빌드/시험·APK 생성/설치·서버/DB 실행·Azure 배포는 하지 않았다.
- 7앱 모두 공개 주소 주입 코드는 있지만 확인된 APK는 loopback5361 Debug·API36 에뮬레이터 범위다. 음식 네 역할 Release 도구는 있으며 화물 3개 앱은 대상 밖이다. APK+AAB와 env 서명 비밀번호의 공식 제한·최종 signed package 선택/검사·웹 다운로드/실휴대폰 업데이트도 미검증이다.
- 먼저 음식 운영자 홈·관제의 화물 종속을 분리하고 기존 음식 추적/중단 검토 API를 연결한다. 격리 Staging/Simulation profile은 존재하나 공통 배포/CI 묶음에 연결되지 않았다. 공개 HTTPS·네 APK 동일 주문 완주 후 주문자/창고 실행 중 갱신, 음식점/기사 pending 종료 복구·음식 전용 push를 좁게 보완하는 순서다.
- 복구 source=restore 미차단, 부분 비밀 생성의 기존 키 교체 조건, rollback의 이전 override 미복원, 실제 초기화4/readiness3/staging migration1 범위 불일치를 코드에서 확인했다. 실제 장애 재현은 아니다. 해당 도구 사용 전 보완과 실운영의 백업/감시·민감 query/오류 로그·확장 경계를 개인 테스트 필수와 나눴다. 72시간은 이미 구현된 기사 상세 열람 제한이며 DB/로그 삭제 기간이 아니다.
- Azure CLI 계정 캐시는 Enabled지만 실제 리소스 조회는 AADSTS50132 invalid_grant/InteractionRequired로 실패했다. 현재 구독/VM/App Service·공개 host는 미확인이며 재로그인·구독 변경·기동/배포를 하지 않았다. 직전 전체 시험6,812/6,819·기존7실패와 로컬2APK 재조리 근거는 아래 기록 범위를 유지한다. 조사 원시는 Git 제외 artifacts/local/azure-mobile-readiness-audit-20261004/에 보존한다. 커밋·푸시는 하지 않았다.

## 이전 완료: 재조리·새 기사 인계와 휴대폰 서버 연결 준비 r1 (2026-10-04)

- 휴대폰 설치 후 정상 업무와 미래 Azure 서버 연결을 목표로 다음 우선순위인 픽업 후 중단 → 재조리 → 새 기사 인계를 보완했다. [페이지→코드/API→DB 책임](../ProjectOverview/page-docs/food-recooking-mobile-r1.md), [변경·Android 화면](../Changes/2026-10-04-food-recooking-mobile-r1.md)에 연결한다.
- 과거 첫 조리·준비 이력은 보존하고 현재 차수의 조리·준비·가능 행동을 같은 서버 정책으로 판정한다. 재조리 음식 준비 전 픽업을 차단하며 픽업 전 기사 교체는 같은 음식의 준비를 유지한다. 음식점·기사 기존 화면은 이번 음식의 대기·예정·실제 완료만 해당 단계에서 안내한다. 조회만으로 픽업하지 않는다. 화물 정책·DB migration·정산 정책은 변경하지 않았다.
- 최종 Fast `20261004-185802` 전체 3.5 빌드·관련 165/165 통과. Task `20261004-190141` 전체 빌드 통과·시험 6,812/6,819. 기준 `182913`의 기존 실패 7개가 이름·메시지·전체 스택까지 동일하며 신규 30개 전부 통과·새 실패 0·기존 결과 변경 0·삭제 0이다. 전체 시험 게이트는 미통과다. 동결 24개와 범위 밖 기존 dirty 337개 지문 변경 0을 확인했다.
- 격리 Development/Simulation 서버·DB에서 주문 `FOOD-20261004100525112`의 중단→재조리→새 기사→수령 확인과 중복 중단·준비 전 픽업 차단·새 시도만 완료 정산을 검증했다. 별도 주문 `FOOD-20261004100615503`은 서버 재시작 뒤 실제 음식점·기사 Android APK에서 준비 완료→픽업→전달을 처리하고 주문자 Client HTTP로 수령 확인·차수 2·12개 이력을 재조회했다. 두 APK 서명·정렬·내장 런타임·해시, 음식점 가로 넘침 0·Android fatal 0을 확인했다.
- 실제 화면은 API36 별도 시험 AVD다. 저장 공간·ADB 재연결 후 명시 조회 복구가 있었으며 공개 HTTPS 안정성·개인 휴대폰·네 역할 APK 전체 완주·지도·실결제·입금·자동 Operational 배차 증거로 확대하지 않는다. 개발 주소는 `http://127.0.0.1:5361/`과 PC reverse다. 이번 서버·에뮬레이터는 종료하고 로컬 APK·DB 볼륨·근거를 보존했다. 커밋·푸시는 하지 않았다.
- Azure App Service 포트·PFX/키링·Redis 선택·배치/인스턴스 경계와 VM 인증서 경로·Staging/Simulation 현장 검증 기준을 현재 코드에 맞춰 문서화했다. 미래 사용 계획에 따른 준비이며 Azure 리소스 생성·배포는 하지 않았다. 다음은 운영자 모바일의 음식 주문 홈·관제를 화물 기능에서 독립시키고 기존 음식 추적·중단 검토 API를 연결하는 것이다. 개인 휴대폰/공개 HTTPS·Release 서명·음식 앱 별도 푸시 연결·기존 시험 실패 7개도 남아 있다.

## 이전 완료: 음식·화물 자동 배차 분리 r1 (2026-10-04)

- 사용자가 앞서 제안한 1번에서 음식 배달과 화물을 확실히 분리하도록 요청했다. [변경 기록](../Changes/2026-10-04-food-cargo-separation-r1.md)과 [독립 업무 기준](../Architecture/BusinessWorkflowResponsibilityModel.md#독립-업무-모듈과-서버-조립)을 따른다.
- 음식 큐 스캔·추천 만료·알림의 workload/기능 플래그/배치 설정을 화물과 나누고 자기 업무유형만 선택한다. 음식 추천 기록은 음식 AppKey를 검증하는 전용 Port로 유지하며 화물 공개배차 직접 전환은 차단한다. 음식 알림이 화물 앱의 legacy 토큰을 재사용하지 않도록 별도 대상 앱 경계를 둔다.
- 최종 Fast `20261004-182837`은 서버·시험 빌드와 관련 96/96을 통과했다. Task `20261004-182913`은 전체 3.5 빌드를 통과했고 전체 시험은 6,782/6,789다. 기준 165504의 기존 7개 실패가 이름·메시지·전체 스택까지 동일하며 신규 56개 모두 통과·새 실패 0·기존 결과 변경 0·삭제 0이다. 전체 시험 게이트는 기존 실패로 미통과다. 최종 제품·시험 26개와 범위 밖 기존 dirty 128개 SHA-256 변경 0을 확인했다.
- 기존 API·DB 형태·역할 앱 UI를 유지하며 별도 서버/DB 구축·APK 업데이트·운영 외부 발송·휴대폰 수신 완료로 보고하지 않는다. 음식 앱의 별도 푸시 토큰 등록은 아직 없으며 화물 앱 토큰으로 대신 보내지 않는다. 뒤의 재조리 예외/모바일 관제/미확정 요청 복구는 이번 배치 분리의 완료 주장에 포함하지 않는다. 커밋·푸시는 하지 않았다.

## 이전 검토: 음식 배달 네 역할의 보완 우선순위 (2026-10-04)

- 사용자가 음식점·주문자·음식 기사·운영자의 관계를 기준으로 가장 부족한 부분부터 보완 순서를 제안해 달라고 요청했다. 현재 코드·기획·최근 Android/서버 검증을 읽기 전용으로 대조했으며 아래는 구현 전 제안이다.
- 우선 1: 음식 배달의 자동 후속 처리를 화물 활성화와 분리한다. [큐 스캔](../../Ssalddel/Infrastructure/BackgroundJobs/DispatchQueue/배차큐스캔Job.cs), [추천 만료](../../Ssalddel/Infrastructure/BackgroundJobs/DispatchQueue/추천만료정리Job.cs), [활성화 정책](../../Ssalddel/Infrastructure/BackgroundJobs/SsalddelBackgroundJobActivationPolicy.cs)은 음식 후보 없음 재탐색·만료도 화물 workload 조건에 묶는다. Operational 음식 ON·화물 OFF의 후속 대기 조건을 우선 재현한다. 즉시 재추천 전체가 없는 것으로 해석하지 않는다.
- 우선 2: 픽업 후 중단→재조리→새 기사 인계를 복구한다. 음식점 조회가 과거 첫 준비 완료 시각을 계속 제공하고 [행동 판정](../../Ssalddel/Application/Food/음식배달가능행동Projector.cs)은 새 준비 완료를 닫지만, 새 기사 픽업은 자기 수락 이후 준비를 요구한다. 과거 기록을 보존하며 현재 재조리 차수의 준비 상태·행동을 구분한다. 코드상 공백이며 해당 예외의 실제 APK 재현은 아직 없다.
- 우선 3~5: [모바일 관제](../../SsalddelAdminApp/Services/AdminOperationsService.cs)의 음식·화물 조회 종속을 풀고 기존 웹 주문 추적·중단 검토를 모바일에 연결한다. 인지 이후 조리 불가·미수령의 운영 접수와 당사자 결과 조회를 연결하고, 음식점·기사의 일부 미확정 요청을 앱 재시작 뒤 복구하는 범위를 검토한다. 기존 직접 취소 제한·기사 보호 시각·공제 정책을 임의 변경하지 않는다.
- 최근 Simulation 정상 1건 서버·DB 완료와 아래 APK 검증은 그대로 유효하지만, Operational 자동 후속·재조리 예외·네 역할 APK의 동일 주문 전체 완주를 대신하지 않는다. 이번 검토에서는 제품 코드·시험·설정 변경, 새 빌드·실행·커밋·푸시를 수행하지 않았다.

## 이전 완료: Android 업무 상태와 설치 준비 r1 (2026-10-04)

- 사용자가 휴대폰 APK에서 역할별 업무 OS의 현재 상태에 필요한 정보·행동만 제공하고, 인증·업무 복귀가 자연스럽게 이어지도록 요청했다. [총괄 책임·OS 기준](../ProjectOverview/page-docs/mobile-workflow-readiness-r1.md), [변경·검증 기록](../Changes/2026-10-04-mobile-workflow-readiness-r1.md)에 연결한다.
- 주문자 선택 상세·목록 복귀와 취소·거절 안내, 음식점 같은 계정의 인증 만료 후 주문 상세 복귀, 음식 기사 인증 복원·늦은 조회와 업무 명령의 충돌 방지, 대기의 고정 운행 시작·수신·위치 확인을 보완했다.
- 화물 기사 재진입·세션 판본·늦은 조회와 오류 상태의 행동 차단, 독립 로그인 복귀와 가로 넘침을 보완했다. 창고 홈은 기능·인증·역할·계정 판본을 확인한 뒤 보호 조회와 업무 화면을 연다. 화물 기사·화주·창고도 APK에 지정한 서버 주소를 실제 사용한다.
- Fast 20261004-164809 전체 3.5 빌드와 관련 시험 238/238을 통과했다. 최종 Task 20261004-165504도 전체 빌드를 통과했고 전체 시험은 6,726/6,733이다. 기준 154057의 기존 7개 실패가 이름·메시지·전체 스택까지 동일하며 새 실패 0·기존 결과 변경 0·삭제 0·새 112개 전부 통과다. 전체 시험 게이트는 기존 실패로 미통과다. 최종 제품·시험·지원 56개와 범위 밖 기존 dirty 95개의 SHA-256 변경 0을 확인했다.
- 새 격리 Development/Simulation 서버·DB에서 음식 주문 `FOOD-20261004071201176`의 인지 → 기사 배정 → 조리 → 픽업 → 전달 → 수령 확인을 완료했다. 모의 정산 재시도·중복 방지와 관리자 조회를 확인했으며 실제 결제·입금·지도·Unity 앱의 증거로 확대하지 않는다.
- 최종 Debug APK 7개는 내장 런타임·ARM64/x86_64·서명·정렬·SHA-256을 확인하고 API36 에뮬레이터에 설치했다. 주문자 선택 상세, 음식점 배차 대기, 음식 기사 운행 시작, 화물 기사 독립 로그인, 창고 비활성 안내 등을 실제 화면에서 확인했다. 전체 역할의 업무 전수 수행은 아니다.
- 시험 주소는 `http://127.0.0.1:5361/`이며 PC 서버·USB reverse가 필요한 개인 개발용이다. 공개 HTTPS 서버·정식 서명·개인 휴대폰 설치·지도 정상 표시가 남아 있다. 원시 근거·APK·격리 DB 볼륨은 Git 제외 보관하고, 시험 에뮬레이터와 이번 Docker 프로젝트는 종료했다. 다른 dirty 작업을 보존했으며 커밋·푸시는 수행하지 않았다.

## 이전 완료: 주문 작성·조리시간 변경 복구 r2 (2026-10-04)

- 사용자가 후속 보완안의1번·4번을 승인했다. [페이지 책임·코드/API·DB](../ProjectOverview/page-docs/order-workflow-polish-r2.md), [변경·검증 기록](../Changes/2026-10-04-order-workflow-polish-r2.md)에 연결한다.
- 주문 작성은 메뉴 비공개·삭제·품절의 확정 거절과 미확정 통신 오류를 구별한다. 최신 메뉴 GET은 선택·수령 입력을 보존하며 변경 단가를 보여 준다. 명시 금액 확인 뒤 새 요청으로 제출한다. 복구 재제출의 메뉴 오류도 같은 안내로 이어진다.
- 서버는 기존 주문자+요청 ID의 접수 결과를 먼저 반환하고, 신규 가격 확인 요청은 저장 전 최신 단가와 비교한다. DB 스키마·배차·정산 정책은 유지한다.
- 음식점 조리시간 변경은 원래 입력·ID·revision을 보존하고 정본 GET부터 확인한다. 적용된 시간으로 안내하며 같은 계정 만료와 다른 계정/명시 로그아웃을 구별한다. 영속 복구는 현재 프로세스 메모리 범위다.
- 최종 Fast20261004-153840 관련198/198·전체3.5 빌드 통과. Chrome390×844에서 최종 제품 Razor/CSS의 정상 주문·가격 확인·품절 복귀6상태를 조작했고 자동GET→POST0·입력 보존·넘침0·브라우저 오류0을 확인했다. 모의 서비스이며 실제 운영 HTTP/DB·MAUI/실기기 증거가 아니다. 로컬 브라우저·미리보기 서버는 종료했다.
- 최종 Task20261004-154057 전체3.5 빌드 통과·전체시험6,614/6,621이다. 기준145843의 기존7실패가 이름·메시지·전체스택까지 동일하며 새실패0·기존결과변경0·삭제0·새43케이스전부통과다. 전체 게이트는 기존7실패로 미통과다. 제품·시험27경로의 실행 전후 SHA-256 변경0을 확인했다. 원시 근거는 Git 제외 artifacts/local/order-workflow-polish-r2/. 영상/MYBOX·지도·Unity·커밋·푸시는 이번 실행에서 수행하지 않았다.

## 이전 완료: 역할 앱 기존 페이지 보완 r1 (2026-10-04)

- 사용자가 지도 제외 보완안 일곱 항목을 모두 승인했다. [페이지 책임·코드/API·DB](../ProjectOverview/page-docs/role-app-polish-r1.md)와 [통합 변경·검증](../Changes/2026-10-04-role-app-polish-r1.md)에 연결한다.
- 화물은 연락처 수정의 전체 화물 원값 보존, 동일 완료 사진의 순차 재시도 중복 방지, 내역의 계정/페이지/조회 수명과 한국시간 표시를 구현했다. [개별 기록](../Changes/2026-10-04-cargo-role-polish-r1.md)을 참조한다.
- 음식점은 인증 만료 후 같은 계정의 메뉴 입력·미확정 요청 ID 복귀와 기존 주문 요청사항/배차 예외 안내를 구현했다. 인증·HTTP의 늦은 응답이 새 계정을 덮거나 종료하는 경계도 보완했다. [책임 문서](../ProjectOverview/page-docs/restaurant-role-polish-r1.md)를 참조한다.
- 주문자는 같은 상세의 일시 조회 실패에 마지막 내용/입력을 유지하고 변경 행동을 잠그며 복귀·재연결 뒤 조회한다. 미확정 제출은 원래 payload/ID를 저장하고 본인 GET 조회 뒤 명시 동일 요청 재시도로만 복구한다. 만료는 보존, 명시 로그아웃은 정리하며 정리 실패에도 개인 화면을 숨긴다. [조회 마감](../ProjectOverview/page-docs/orderer-refresh-polish-r1.md)을 참조한다.
- 최종 관련 시험409/409·전체3.5 빌드 통과. Task 20261004-145843은6,571/6,578이며 기준135108의 기존7실패 이름/메시지/전체스택 동일·새 실패0이다. 전체 게이트는 미통과다. 의미를 유지한 시험 이름/구조 대체2건과 순증123케이스를 통합 기록에 설명했다. 제품·시험·지원72경로는 최종 실행 중·후 지문 변경0이다.
- 제품 Razor/CSS의390px 실제 메뉴 목록→입력→저장 후 목록 복귀·가로 넘침0·브라우저 오류0, 처리11·모의 HTTP34검사를 확인했다. 주문자 복구/상세 HTML과 화물 수명은 회귀시험 근거다. 실제 운영 HTTP/DB·물리 보안 저장소·앱 재시작·실기기 전체 화면은 이번 새 실행으로 검증하지 않았다. 격리 브라우저와 메뉴 미리보기 서버를 종료했다. 원시는 Git 제외 artifacts/local/role-app-polish-r1/이며 다른 dirty 작업·영상/MYBOX를 보존하고 커밋·푸시는 수행하지 않았다.

## 보류·기존 완료 근거

- 네이버 지도는 사용자가 이번 범위에서 제외했다. [기존 경로 화면 맞춤](../Changes/2026-10-04-driver-navermap-viewport-r1.md)에서 SDK 인증401·실제 타일 미완료이며 새 키/콘솔/유료 호출 변경은 없다.
- 음식 기사 [완료 목록·상세·72시간 열람](../Changes/2026-10-04-driver-history-lifetime-map-r1.md), [지도 중심 현재 수행](../Changes/2026-10-04-driver-map-first-r2.md), [카드·고정 글자](../Changes/2026-10-04-driver-fixed-font-r1.md)는 기존 구현을 유지한다.
- [음식 알림·전표·당일 정산](../Changes/2026-10-03-food-notification-daily-settlement-r1.md), [예외 명령·신규 수신](../Changes/2026-10-03-food-exception-intent-r1.md), [인증·업무 분리](../Changes/2026-10-03-auth-workspace-separation-r1.md)의 실행 근거는 각 기록에 있다. 이번 소스 보완을 물리 단말·실제 지도·인쇄·입금 완료로 확대하지 않는다.
- 비교 기준 Task 20261004-135108의 기존 실패7개는 별도 마감 대상이며 이번 보완에서 성공으로 변경하지 않았다. 최종 동일 여부와 원시 비교 결과는 통합 기록에 연결한다.
