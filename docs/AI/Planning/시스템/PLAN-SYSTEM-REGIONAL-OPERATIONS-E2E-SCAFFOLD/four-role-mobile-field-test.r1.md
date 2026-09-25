[기획 · 시스템·운영 통합·모바일 현장 검증 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · four-role-mobile-field-test r1]

# 음식 주문 네 역할 Android 현장 검증 기반

## 목적

주문자·음식점·음식 배달 기사·운영자가 각자의 Android 앱으로 같은 음식 주문 원장을 관찰하고, 서버가 허용한 행동만 수행한 뒤 정본을 다시 조회하는 첫 현장 검증 기반을 만든다. 실제 결제와 운영 영업은 포함하지 않으며 사가정 합성 자료와 `Simulation` 실행 모드만 사용한다.

## 확정 범위

- 역할 앱은 `OrdererApp`, `RestaurantDeskApp`, `FDriverApp`, `SsalddelAdminApp` 네 개를 독립 패키지로 유지한다.
- 서버가 주문·음식점 처리·배차·배달·복구의 최종 권위를 가진다. 앱은 역할별 조회와 허용 행동의 전달자다.
- 내부 검증 판본은 `0.1.0`, Android 최소 API는 24다.
- 모든 앱은 `내부 테스트 / Simulation / 서버 host / 앱 판본`을 항상 표시한다.
- Release 빌드는 명시적으로 주입된 공인 HTTPS 서버 주소만 허용한다. 주소가 없거나 HTTP·loopback·에뮬레이터 주소이면 시작을 거절한다.
- 실제 결제, 광고, Unity 조작 권위, 운영 개인정보 공개는 제외한다.

## 구현된 기반

### 네 역할 Android 앱

- 주문자 앱과 음식점 앱에 Android 대상, Manifest, 앱 아이콘과 splash 자원을 추가했다.
- 기사 앱과 운영자 앱을 포함한 네 앱의 패키지 ID와 `0.1.0` 판본을 동결했다.
- Release에서는 역할별 현장 검증 route만 허용하고 다른 화면 진입은 닫는다. Debug에서는 기존 개발 탐색을 보존한다.
- 공통 상단 배너로 현재 내부 검증 환경과 연결 서버를 판독할 수 있게 했다.

### 주문자용 기사 위치

- 주문자 자신의 주문 상세에만 배정 기사의 최신 위치 사본을 포함한다.
- 배정 전에는 좌표를 내보내지 않고, 배달이 종료되면 즉시 좌표를 제거한다.
- 30초를 넘긴 위치는 `갱신지연`으로 표시하며 현재 위치라고 단정하지 않는다.
- 응답에는 기사 고유 식별자나 이동 이력을 포함하지 않는다.
- 주문자 앱은 추적 중에 10초 간격으로 서버 정본을 다시 조회한다.

### 기사 추천 알림 경계

- 기사 추천의 모바일 알림 transport는 FCM을 사용하고, 앱은 알림을 받은 뒤 음식 배달 업무 공간 API를 다시 조회한다.
- FCM payload는 갱신 힌트일 뿐 수락 가능 여부·revision·지급액의 정본이 아니다.
- 기사 앱의 SignalR client는 제거하고 10초 서버 재조회를 FCM 누락·지연·앱 복귀의 복구 경로로 유지한다.
- 서버 FCM 전송과 기사 push token API는 존재한다. 다만 `kr.ssalddel.fdriver`용 Firebase 설정과 실제 Android 수신 adapter·장치 증거는 아직 없다.

### 사가정 합성 현장 표본

- `MobileFieldTest:Enabled`와 `Simulation`을 함께 만족할 때만 사가정 합성 음식점 3곳과 메뉴 9개를 멱등하게 준비한다.
- 예약 ID가 다른 자료에 이미 사용됐으면 덮어쓰지 않고 초기화를 실패시킨다.
- 주문자·음식점·기사·운영자 개발 계정을 서로 분리한다.
- `Staging + Simulation`용 compose override를 제공하며 외부 작업, 실제 결제와 마트 기능은 기본 비활성이다.

### 서명 패키지 준비

- `eng/release/publish-mobile-field-test.ps1`은 네 앱을 같은 서버 주소와 판본으로 빌드하고 APK·AAB 및 SHA-256 목록을 생성한다.
- 실제 실행은 저장소 밖 keystore와 환경 변수 비밀번호를 요구한다. 비밀값은 source, tracked config, 로그에 기록하지 않는다.
- `-PlanOnly`로 네 패키지와 필요한 입력을 변경 없이 점검할 수 있다.

## 역할별 첫 route

| 역할 | 패키지 | 첫 현장 검증 범위 |
| --- | --- | --- |
| 주문자 | `com.ssalddel.ordererapp` | 음식점 탐색, 음식 주문, 주문 진행·기사 위치 사본 조회 |
| 음식점 | `com.ssalddel.restaurantdeskapp` | 로그인, 주문 목록·상세, 준비 시간과 픽업 준비 처리 |
| 기사 | `kr.ssalddel.fdriver` | 근무 상태, 배차 제안, 10초 위치 전송, 픽업·전달 |
| 운영자 | `com.ssalddel.adminapp` | 운영 개요, 음식 배달 예외, 안전 재시도, 읽기 전용 현금 흐름 |

## 검증 상태

| 관문 | 상태 | 의미 |
| --- | --- | --- |
| 주문자 위치 계약·권한 자동시험 | 통과 | 소유자 한정, 종료 시 좌표 제거를 자동시험으로 확인 |
| 합성 표본·개발 계정 자동시험 | 통과 | 멱등 초기화와 충돌 거절을 자동시험으로 확인 |
| 네 앱 Android Debug build | 통과 | 소스와 Android 자원이 컴파일됨 |
| Release HTTPS·route 차단 | 통과 | 공인 HTTPS 주소를 주입한 네 앱 Release 컴파일과 정책 집중 시험 통과 |
| 배포 스크립트 계획 점검 | 통과 | 패키지 목록과 입력 검사를 확인 |
| 실제 MySQL·공인 HTTPS 서버 | 미수행 | compose override를 실제 호스트에서 실행하지 않음 |
| 전용 서명 APK·AAB | 대기 | keystore와 비밀번호가 제공되지 않음 |
| 실제 휴대폰 설치·화면·백그라운드 | 대기 | 연결된 Android 장치 증거 없음 |
| Google Play 내부 테스트 | 미수행 | 업로드·설치·업데이트 증거 없음 |

## 알려진 제한

- 기사 위치 전송은 현재 앱이 전경에 있는 동안의 10초 heartbeat다. Android foreground service, 백그라운드 권한과 package 전용 FCM 설정·수신 adapter는 후속 작업이다.
- 주문자 추적은 현재 10초 polling이다. SignalR이나 push는 필수 정확성 경로가 아니며 앱 복귀 시 정본 재조회가 우선이다.
- 합성 음식점·메뉴는 현실 사업장·실제 메뉴·판매 가능 자료가 아니다.
- Android build는 실제 터치·권한·GPS·배터리·통신 전환·장시간 운행 증거가 아니다.

## 다음 재개 순서

1. 공인 HTTPS 현장 검증 서버와 전용 MySQL을 준비하고 `Staging + Simulation`으로 시작한다.
2. 네 계정 로그인과 합성 음식점·메뉴 독립 재조회를 확인한다.
3. 외부 keystore로 같은 `0.1.0` APK·AAB를 서명하고 hash manifest를 보존한다.
4. 실제 Android 휴대폰에 네 앱을 설치해 주문 등록부터 수령 확인까지 같은 주문 revision으로 수행한다.
5. 전경·화면 꺼짐·통신 단절·앱 복귀에서 기사 위치와 역할별 정본 재조회를 확인한다.
6. 통과한 같은 AAB만 Google Play 내부 테스트에 올려 설치·업데이트·rollback을 검증한다.

## 증거 경계

이 문서와 코드는 현장 검증을 위한 기반이다. 실제 서버 실행, 서명 산출물, 휴대폰 조작, Google Play 설치, Unity Game View를 수행했다는 증거가 아니다. 별도 요청 전 commit·push·배포도 수행하지 않는다.
