# 모바일 가상 퀵 운영 r58

2026-10-16. [승인 설계](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/virtual-quick-operations.implementation.r58.md)에 따라 기존 살뜰 관리자 Android 앱에 로컬 퀵 운영 연습을 연결했다. 실제 운영 데이터·결제·송금·외부 알림·모델 가중치 학습은 포함하지 않는다.

현재 상태: `Implemented / LocalApiValidated / ApkPrepared / UiVisualDeferred / DeviceDeferred / OperationalDisabled`. 격리 실제 API 7건을 모두 마감했고 8슬롯은 중지·재시작으로 생략했다. 15건 전체 일정은 별도의 가짜 시계 검증이다.

## 사용자가 할 수 있는 일

```text
관리자 로그인 → 화물 운영 → 가상 퀵 운영
├─ 새 훈련 시작: 30분, 2분 간격, 최대 15건
├─ 신규 주문 일시정지·재개·발생 종료
│  └─ 이미 접수한 업무의 추천·수락·사진 픽업·전달은 계속
├─ 주문 대기·수행·완료와 기사별 작업량 확인
├─ 기사 부족 시 지원 기사 1명씩 명시 참여(최대 2명)
└─ 배차 판단·AI 비교: 실제 후보 점수·탈락 사유·AI 연결 상태
```

판단 기록은 기존 규칙의 실제 계산에서 관찰한다. Ollama는 익명 후보를 비교할 뿐 실제 추천을 바꾸지 않는다. 미확인 거리·점수를 0으로 만들지 않으며, 동일한 후보 근거의 반복 기록·모델 호출을 줄인다. 이전 미정인 장기 대기·4,000원 평균 단가 정책을 새 규칙으로 확정하지 않았다.

## 기존 책임과 복구

- 전용 `eng/Ssalddel.DispatchTraining` 호스트(5324)·MySQL(13312)·Mongo(27023)를 사용한다. 제품 호스트에는 훈련 endpoint를 등록하지 않는다. capability의 활성 여부 외에는 전용 합성 관리자 인증을 요구한다.
- 접수·운행·추천·수락·수행·사진 업로드는 기존 API와 전문 정책을 통과한다. SQL·Mongo 업무 상태를 도구가 직접 완료로 덮지 않는다. 합성 주소·운임·유가·사진은 실제 자료가 아니다.
- 응답 유실은 요청 ID·회차·revision을 보존해 정본을 다시 읽는다. 미확정 POST는 무조건 반복하지 않는다. 준비 실패로 주문 POST를 보내지 않은 경우도 별도로 기록하고 다른 주문은 계속 처리한다.
- 재시작은 신규 발생을 일시정지하며 놓친 슬롯을 몰아서 만들지 않는다. 종료는 진행 업무 마감을 기다린다. 세션 손상은 자동 초기화하지 않는다. 새 회차 전 완료 원문 보존·해시 대조와 명시적인 미완료 회차 보관을 구분한다.
- 모바일은 전경에서 5초마다 조회하고 복귀 시 다시 확인한다. 계정 변경 후 늦은 응답을 폐기하며 불명확한 제어 요청 결과를 확인하기 전에 새 명령을 보내지 않는다.

실제 API 시험에서 자동 배차 정책 블록이 붙은 신청 메모의 동의 GUID를 읽지 못하는 제품 버그를 발견했다. 정상 메모의 GUID·출처·회원/정책 블록을 검사하도록 보완했다. 원래 신청 동의의 업무·출처·소유자·철회·확정 기사·추천 차수 검사는 유지한다. 가상 기사 입력의 stable ID, 정상 `확정` 상태, 운행 종료 뒤 상태 조회와 합성 유가 의존성도 대조했다.

## 검증 기록

| 검증 | 결과·근거 |
| --- | --- |
| 정책·모바일·AI 집중 | 53/53, `artifacts/local/validation/dispatch-training-mobile-20261016/` |
| 프로젝트 Fast | 62/62, `artifacts/local/validation/20261016-101745/` |
| 동의 파싱 회귀 | 40/40(신규 30), `artifacts/local/validation/20261016-103003/` |
| 최종 제품 Task | 12,100 통과·실패 0·환경 의존 16 미실행, `artifacts/local/validation/20261016-103110/` |
| 훈련 도구 빌드 | 경고 0·오류 0, `artifacts/local/dispatch-training-r58/build.log` |
| 가짜 시계 세션·복구 | 최종 14/14, `artifacts/local/dispatch-training-r58/selfchecks/self-checks/20261016-014125-2beff632b76b43149352b2e785df04da/` |
| 통신 장애에도 검증 기록 보존 | 가짜 HTTP 7개 + 후보 필드 보존 2개 통과, `artifacts/local/validation/dispatch-verify-*/` |
| 실제 HTTP 인증 경계 | capability 최소 응답·상태/판단/사건/제어 미인증 401, 5/5, `http-anonymous-boundary-checks.json` |
| 격리 실제 HTTP·DB 회차 | 7건 모두 `Completed`, 지원 2명, `http-verification-20261016-103804-aef6d1c40e2949b5b9facaffa4564aec.json`·`completed-audit.json` |
| SQL·Mongo 대조 | SQL 운송 7건 `인수완료`, Mongo `cargo-transport` 7건 `완료`·수락 기록 7건, `mongo-completed-audit.json` |
| 관리자 APK | 131,481,931 bytes, SHA-256 `6EE033A36DCD3F7AEFE2DFA123D9E769D56916737C14E625762FCFEEC3206339`; 내장 실행 코드와 컴파일 DLL 일치 |

실제 30분 회차는 개발 중 재시작·일시정지를 겪었다. 생략된 슬롯과 실제 생성·완료한 주문 수를 구분한다. 15건 전체 일정은 가짜 시계 검증의 결과이며 실제 회차의 15건 무중단 완료로 표시하지 않는다. Ollama 응답 초과·회차 호출 한도 소진은 성공 비교나 판단 개선으로 세지 않는다.

발생 창은 01:08:45~01:38:45 UTC, 마지막 진행 업무 마감은 01:41:40 UTC의 실제 API 재조회에서 확인했다. 픽업·전달 합성 사진 각 7개와 실제 완료 명령 7개를 대조했다. 재시작 확인 메모 1개는 `ReviewRequired`로 보존하며 사건이 전부 해결됐다고 표시하지 않는다. 개발 중 실패·반복 관측 기록도 삭제하지 않았다. 최종 코드에는 같은 후보 근거의 연속 중복 비교 생략을 포함한다. 소스 50개·도구/서버 DLL 해시는 `source-evidence.json`에 연결했다.

## 사용과 후속 확인

[실행·APK 안내](../../eng/Ssalddel.DispatchTraining/README.md)에 준비·서버 실행·USB 연결·관리자 로그인·검증 도구·보존 절차를 정리했다. 준비 APK는 `artifacts/local/dispatch-training-r58/android/ssalddel-admin-training-usb-20261016.apk`이며 PC 루프백 5324와 USB reverse를 사용하는 Debug 시험용이다.

브라우저 미리보기 숨김 실행은 자동 승인 검토에서 `blocked by policy`로 거절됐다. 우회하거나 다시 기동하지 않았으므로 실제 화면 픽셀 검토는 미실행이다. 실제 휴대폰 설치·화면·배너/알림음·카메라·외부 통신·실제 지급 검증도 별도다. 관련 없는 WAV·Git 보호 도구와 이전 기획을 보존했으며 commit/push는 수행하지 않았다.
