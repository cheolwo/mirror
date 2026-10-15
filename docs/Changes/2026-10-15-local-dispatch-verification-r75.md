# USB 없이 주문·배차·기사 앱 검증 r75

2026-10-15. 상태 `Implemented / LocalValidated / DeviceCloudDeferred`. [계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.plan.r75.md)과 [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.implementation.r75.md)에 따라 기존 미커밋 작업을 보존하고 로컬 검증을 진행한다.

## 변경 결과

- 화물 후보 조회에서 .NET 10의 span 기반 Contains 선택으로 EF 쿼리가 실패하는 문제를 `Enumerable.Contains` 명시 호출로 보완했다. 새 시험이 실제 후보 정책을 실행한다.
- 화물 배차 알림의 단건 `requestId`와 일괄 `topRequestId`/`requestIds`를 함께 해석한다. 알림 클릭 뒤 현재 계정·회차·기한과 서버 정본을 재조회하고, 조회 실패에서는 복귀 포인터를 보존한다. 알림이 자동 수락을 실행하지 않는다.
- 화물 운행 시작 ID와 공개 선택을 암호화 저장소에 보관한다. 응답 유실·재진입 시 새 POST 대신 기존 요청을 조회하고, 서버 확정과 위치 송신을 구분한다. 위치 송신 실패 후에도 서버 운행 종료가 가능하다.
- 현재 운송과 추천/유가·정산·예약 조회를 분리한다. 부가 조회 실패가 확인된 현재 운송을 막지 않으며, 미확인을 정상 0건/0원 또는 예시 자료로 표시하지 않는다. 추천 재조회 중 이전 추천의 상세·수락 접근도 보호한다.
- 음식 기사 수락 후 현재 업무에 서버의 업무 종류·배정 작업·배달료 산정 근거를 보존한다. 주 카드의 총 배달료는 유지하고 상세에서 직선 산정거리·거리비·단가·구성·정책 판본을 확인한다. 지도 전체 화면과 기존 단계별 카드·주 행동은 유지한다.
- 기존 음식 기사 알림 WAV를 약 1.965초의 오프라인 한국어 합성음성 ‘살뜰 배달!’로 준비했다. 이전 기계음은 비공개 로컬 산출물에 보존했다. 알림 권한·사용자 채널 설정을 바꾸지 않았으며 실제 억양/음량/청취 확인은 휴대폰 연결 뒤 필요하다. 화물 앱에는 이번에 같은 맞춤 음성을 추가하지 않았다.

## 로컬 시나리오와 대체 경계

전용 [실행기](../../eng/Ssalddel.DispatchVerificationR75/README.md)는 지정기사를 넣는 화면 제어 API를 사용하지 않고 기존 엔진 카탈로그·후보 정책·전환/수락 코어를 호출한다.

| 범위 | 실행과 확인할 결과 | 구별할 한계 |
| --- | --- | --- |
| 음식 정상 3종 | 주문·큐/추천·푸시 입력·실제 loopback HTTP 모바일 클라이언트·수락·도착·준비·픽업·전달·별도 고객 수령 | 합성 경로/날씨·모델 응답·fixture 인증·기록형 알림, Mongo/정산 후속은 대체 |
| 복구 4종 | 수신 OFF/부적격·복구, 중복 알림, 만료, 수락 응답 유실 뒤 정본 복원 | Android OS 표시·실제 FCM·카메라 시험 아님 |
| 일반 화물 | 실제 견적·요금수정·모의 후불 승인·큐·정책 선정·수락/재수락 | 고객 견적과 기사 보수는 별개이며 기사 보수 미확인 유지 |
| 생활 소형 배송 | 실제 등록·현장 지급 예정 조건·정책 보수 계산/동결·엔진 선정·수락 뒤 금액 대조 | 명시 시험 정책·합성 거리 자료, 운영 동의/시장 자격 일부 대체 |

공통 SQL은 격리 SQLite다. 실제 MySQL/Mongo·운영 JWT·실제 Ollama 추론·외부 경로/유가 API·화물 HTTP/사진/완료는 이 실행기의 통과 주장에 포함하지 않는다. 음식의 loopback HTTP와 화물의 직접 Command 실행도 구별한다. 원주문·전문 원장·제안·보수·수령은 별도 결과로 남긴다.

## 검증 결과

| 검증 | 결과와 근거 |
| --- | --- |
| 전체 소비 앱 빌드 | 오류 0개. [Task 실행 로그](../../artifacts/local/dispatch-verification-r75-implementation/validation-task.log) |
| 전체 회귀 시험 | 11,865개 통과·실패 0개·환경 의존 16개 건너뜀. [TRX](../../artifacts/local/validation/20261015-165514/Ssalddel.Tests.trx) |
| 집중 시험 | 202개 통과·실패 0개. [TRX](../../artifacts/local/dispatch-verification-r75-implementation/test-results/focused-final-r75.trx) |
| 실제 정책/복구 전용 실행 | 9개 통과. 정책 선정 5개와 복구 4개 구분·실행 전후 소스 해시 일치. [실행 근거](../../artifacts/local/dispatch-verification-r75-implementation/runs/20261015T0755562918475Z-e2e331b6/result.json) |
| APK 준비 | 음식·화물 모두 0.1.77/177, Debug·arm64·같은 외부 시험 서명키·로컬 서버 5363. 패키지 ID/버전/서명/해시와 빌드 후 소비 소스 해시 재대조 완료. 설치하지 않음 |
| 음성 준비 | 약 1.965초 PCM WAV·음성 있음·APK 내부 파일과 원본 SHA-256 일치. 실제 청취 미확인. [음원 기록](../../artifacts/local/dispatch-verification-r75-implementation/audio/manifest.json) |

[음식 기사 APK](../../artifacts/local/dispatch-verification-r75-implementation/apk/driver/mirror-driver-0.1.77-177.apk) · [화물 기사 APK](../../artifacts/local/dispatch-verification-r75-implementation/apk/cargo-driver/mirror-cargo-driver-0.1.77-177.apk) · [패키지 검증 기록](../../artifacts/local/dispatch-verification-r75-implementation/apk-summary.json). USB reverse가 필요한 로컬 서버용이며 독립 외부 통신/공개 배포용 APK는 아니다.

초기 집중 시험과 Task 두 차례에서 발견한 쿼리 오류·시험 환경 등록/반환 계약·구성 시험의 이전 호출 위치·타입 이름 충돌을 보완한 뒤 최종 결과를 얻었다. 실패 로그도 로컬에 보존하며 초기 실패를 통과 기록으로 덮지 않았다. 기존 SignalR 취약성 경고와 APK의 AndroidX 버전 제약 경고는 남아 있다. 새 스크린샷·실기기 시각 검수는 진행하지 않았다.


## Azure 읽기 확인

기존 F1 앱은 Running·HTTPS Only이며 PC의 실제 HTTPS 요청에 응답했다. 다만 `/health/live`와 `/health/ready` 모두 404라 현재 Hongdal 서버의 배포/업무 준비를 확인한 결과는 아니다. 리소스·설정·요금제·배포는 변경하지 않았다. [읽기 증거](../../artifacts/local/dispatch-verification-r75-implementation/azure-https-readonly.json). 무료 플랜 존재, HTTPS 응답, 실제 앱·DB·배차 실행은 별개다.

## 남은 확인

휴대폰 USB가 해제돼 실제 설치·화면·사진 촬영·FCM 수신·음성 청취·잠금/종료 복귀를 실행하지 않았다. r74의 USB 로컬 호스트는 배차 작업자가 제거된 기존 시험 환경으로 보존했으며, 이번 runner 통과가 그 호스트의 상주 자동 배차 활성화를 뜻하지 않는다.

운행 시작 API가 HTTP 거절 상태를 보존하지 않는 경우에는 실패 문자열을 파싱해 시작 요청을 새로 보내지 않는다. 요청과 정본이 불일치하면 pending을 보존하고 운영자 확인을 안내한다. 일반 화물 기사 보수 미확인, 실제 Mongo 조회, 운영 migration/readiness와 기존 `Microsoft.AspNetCore.SignalR.Common 10.0.9`의 `NU1903` 보안 경고도 후속이다. 실제 결제/송금·Azure 배포·공개 APK 배포·commit/push는 하지 않았다.
