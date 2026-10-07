# OS 생명주기 보완과 Android 설치 검증 r24

## 요청과 범위

사용자는 r23 조사에서 확인한 문제를 보완하고 휴대폰에 APK를 설치해 직접 사용·검증할 수 있는 수준으로 조정하도록 요청했다. [승인 계획](../ProjectOverview/page-docs/os-lifecycle-hardening-r24-plan.md)과 [문제별 구현·남은 경계](../ProjectOverview/page-docs/os-lifecycle-hardening-r24.md)에 작업 범위를 결속한다.

음식 배차 재시도의 현재 상태 보존, 원시 상태 검증, 관계 수신자 동의와 transaction, 창고의 공통 상태/수량 관문, 확정 화물 조건 변경 차단, 교육 제출 CAS·영수증/투영 분리, 선적 이력과 현재 상태 보호를 보완했다. 마트는 연결되지 않은 기사 업무를 생성하지 않는 관문을 추가했으며 새 consumer adapter가 완성된 것은 아니다. 공통 대장은 10개 OS·62개 책임 단계로 채웠다.

실제 초기 Android 화면에서 익명 역할 진입의 로그인 버튼이 화면 밖에 놓이는 문제도 확인해 로그인·권한 안내와 업무 지도를 배타적으로 표시하도록 수정했다. 로그인 경로·권한·ReturnUrl은 유지한다.

기사 앱의 실제 완료 상세 화면에서 공제·수령액의 미확정 문구가 중복되는 문제도 정리했다. 금액·공제 상태는 바꾸지 않고 항목 이름을 한 번만 표시한다. 수정 전 APK·소스 지문·화면·검증 기록을 보존하고 수정 후 솔루션 시험과 APK 패키징을 다시 실행했다.

## 검증

최종 원시 근거는 Git 제외 `artifacts/local/os-lifecycle-hardening-r24/`에 보존한다. 초기 빌드/시험 실패도 삭제하거나 성공 결과로 바꾸지 않는다.

| 실행 | 결과와 범위 |
| --- | --- |
| Fast `20261006-132434` | 전체 솔루션 빌드·관련 402/402 통과, 실패/건너뜀 0. |
| Task `20261006-132550` | 최종 소스의 전체 솔루션 빌드·7,857/7,857 통과, 실패/건너뜀 0. 기존 AndroidX 버전 범위 경고는 남는다. |
| 실제 Mongo probe | 격리 loopback Mongo 8의 교육 큐/worker와 선적 저장 32/32 통과, 본인 GUID DB 삭제 확인. 전용 tmpfs 컨테이너도 소유 확인 후 정리했다. |
| 현행 대장 | 63/63 통과, 10개 OS·62개 책임 단계·기존 상호작용을 확인. r23의 518개 소스 전수 조사를 반복한 결과가 아니다. |
| APK | 두 최종 파일의 서명·16KB 정렬·버전/패키지/ABI·내장 runtime 검사와 전달 사본 해시 대조 통과. 설치된 base APK SHA-256도 최종 파일과 일치. |
| Android 실제 UI | API 36/x86_64에서 두 앱 설치·시작과 시험 계정 세션 복원·음식기사 조회, 통합 앱의 목록 전환을 확인. 별도 기사 앱의 완료 1건·2,500원·요금 기준 0.25km, 목록→상세→목록 복귀, 10/9 13:15의 3일 열람 기한과 미확정 공제·수령액 표시를 확인. 이전 r24 APK의 로그인 CTA·서버 교체 401 복구는 같은 Razor 소스의 별도 근거로 보존한다. |
| 실제 MySQL·역할 API | 새 GUID 볼륨의 음식 네 역할 서버에서 같은 주문을 주문→음식점 확인→기사 추천/수락→조리→픽업→전달→수령 확인까지 처리하고 300초 재조회 관찰 통과. Observer `ba1daf22ff294e72b092c84e96eb5f09`, 주문 `FOOD-20261006041413182`, 19개 사건, 최종 수령확인/배달완료, 벽시계 301.333초. 좌표·비용·외부 연계는 격리 fixture다. |

실제 실행이 발견한 Mongo 기본 `_id` 중복 인덱스와 MySQL 재시도 전략/수동 transaction 충돌도 수정했다. 새 EF 재시도 시험은 전용 공유 provider로 전체 시험의 provider cache와 격리했다. 전역 경고 억제나 운영 배차 관문 완화는 하지 않았다. 격리 서버의 가상 기사도 정상 API로 수신 의사를 On으로 설정하고 변경 시각을 재조회한다.

이전 두 실제 서버 실패는 `server/initial-mysql-failure/`와 `server/source-fixed-intent-failure/`에 보존하고 해당 GUID DB 볼륨을 지우지 않았다. 보완된 소스/새 이미지·새 GUID DB마다 Start는 한 번만 실행했다. 완료 Observer ID를 도메인 `E2eRunStableId`로 대체하지 않는다. 교육 외부 전송·실제 GPS·실제 배달·지급과 모든 역할의 운영 완주를 확인한 결과는 아니다.

격리 서버의 외부 푸시 worker는 꺼져 있으며 알림 Outbox 한 건은 시도 0의 Pending이다. 실효 적격성의 최초 미기록 분기를 사용한 관찰 기사이며 실제 운영 자격을 Eligible로 확인한 것은 아니다. APK는 서버의 자동 역할 수행과 별도로 로그인·조회·완료 기록을 확인한다.

문제별 현재 소스·관련 시험과 최종 근거는 로컬 `closure-matrix.json`/`final-evidence.json`에 결속한다. 마트 consumer adapter, 창고 재검수/재계수, 결제/환불/보상 상태축과 교육 API 분류는 남은 경계로 유지한다. 시험 통과를 이 항목들의 구현 완료로 바꾸지 않는다.

최종 결속은 원래 r23 항목 ID 23개와 중복 부재, Fast/Task 실행 전·후·현재 제품 소스, 선택된 APK 소스 2,151개, 서버 이미지 소스 15개 및 실제 설치 APK 해시를 비교한다. 이는 선택된 소스의 판본 결속이며 SDK·NuGet 전체 입력을 고정한 재현 빌드라는 뜻은 아니다. 최종 UI 실행 창의 Android crash buffer는 비어 있었으며 향후 모든 기기에서 오류가 없다는 보장은 아니다.

## 설치 인계와 실제 사용 범위

대표 앱은 Mirror 통합 `SsalddelApp`, 별도 음식 기사 앱은 `FDriverApp`이다. Debug `0.1.24 (24)` APK는 arm64-v8a/x86_64, 최소 Android API 24를 대상으로 한다. endpoint `http://127.0.0.1:5361/`는 USB reverse로 PC에 연결하는 로컬 검증용이다. 공개 Azure HTTPS 연결용 판본은 별도 설정/패키징이 필요하다.

최종 APK 사본은 `apk/Mirror-r24-usb-debug.apk`, `apk/FoodDriver-r24-usb-debug.apk`이고 같은 폴더의 `설치-검증-안내.md`에 계정 파일 위치와 USB 명령을 정리했다. 서명/정렬/패키지·SHA-256과 실제 화면은 같은 로컬 근거 폴더의 `final-apk-verification.json`, `emulator/`에 보관한다. Android API 24/arm64 실기기, 실제 지도/GPS·푸시, 공개 Azure와 실제 입금은 미검증이며 API 36/x86_64나 실패 주입 시험의 결과로 확대하지 않는다.

## 보존

시작 branch `dev/mirror-integration`, HEAD `b1b7cfc39b5183ed56246080c141d63965bf2704`, 기존 dirty 1,001개를 기준으로 소유 범위를 확인했다. 소유 경로 54개 외의 기존 dirty 991개 SHA-256이 모두 같고 HEAD·branch도 유지했다. 스테이지된 파일은 없으며 이전 index 전체 해시는 수집하지 않았으므로 index의 바이트 동일성까지 주장하지 않는다. commit/push·영상 편집·MYBOX 게시·Unity 형상 변경은 수행하지 않았다.
