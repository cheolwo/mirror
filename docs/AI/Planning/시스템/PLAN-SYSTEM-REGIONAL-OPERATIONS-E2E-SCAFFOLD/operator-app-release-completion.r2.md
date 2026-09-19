[기획 · 시스템·모바일 출시 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · 운영자 앱 출시 완수 r2]

# `SsalddelAdminApp 0.1.0` 내부 테스트 작업 명세

## 1. 목표

첫 모바일 출시물을 다음처럼 좁힌다.

```text
제품        SsalddelAdminApp Android
표시 판본   0.1.0
배포 단계   Google Play 내부 테스트 후보
사용자      운영자 본인 + 지정 테스터
서버        단일 Ssalddel HTTPS 시험 환경
실행 효과   Development + Simulation
핵심 증거   실제 기기 UI + 같은 서버 원장 revision 재조회
```

`0.1.0`의 목적은 기능을 많이 공개하는 것이 아니라, 설치한 앱으로 안전한 운영 폐루프 하나를 반복해서 끝내는 것이다. Google Play 업로드와 실제 운영 활성화는 각각 별도 승인 전에는 수행하지 않는다.

## 2. 출시 화면 범위 제안

### 2.1 출시 프로필에 포함할 화면

| 순서 | route | 목적 | 쓰기 여부 |
| --- | --- | --- | --- |
| 1 | `/login` | 서버관리자 인증·세션 복원 | 인증 |
| 2 | `/overview` | 운영 상태·예외·마지막 갱신 확인 | 읽기 |
| 3 | `/operations` | 운송·기사·배차 조건 관찰 | 읽기, Simulation 한시 할증만 제한적 쓰기 |
| 4 | `/operations/follow-up-recovery` | 서버 허용 안전 재시도 예약 | 제한적 쓰기 |
| 5 | `/operations/finance` | 현금 유입·유출·순변동과 기간 의무 조회 | 읽기 |

### 2.2 첫 판에서 숨길 화면

- `/trade-readiness`
- `/community-management`
- `/information-review`
- `/prajna`
- `/page-catalog`

코드와 route를 삭제하지 않는다. `OperatorPreview` 출시 프로필의 navigation에서만 숨기고 직접 route 접근도 같은 프로필 정책으로 막는다. 이후 각 기능이 실제 서버·장치 검증을 통과하면 판본별로 추가한다.

### 2.3 항상 보일 환경 표시

앱 상단에 다음을 숨길 수 없는 표식으로 표시한다.

- `내부 테스트`
- `Simulation`
- 서버 환경 표시명
- 앱 판본

운영 효과가 비활성이라는 사실을 메뉴 안쪽 도움말에만 숨기지 않는다.

## 3. 작업 묶음

### WP0 — 현재 기준선 마감

- 현재 진행 중인 현금 흐름 계약·UseCase·Controller·앱 화면·시험과 출시 기획을 검증한다.
- 전체 시험의 기존 실패와 이번 범위 실패를 분리한다.
- 별도 요청 전 commit·push는 수행하지 않는다.

### WP1 — `OperatorPreview` 출시 프로필

예상 수정 경로:

- `SsalddelAdminApp/Components/Layout/MainLayout.razor`
- `SsalddelAdminApp/Components/Routes.razor`
- `SsalddelAdminApp/MauiProgram.cs`
- 새 출시 프로필·route 정책 파일

규칙과 완료 조건:

- 출시 프로필이 메뉴와 route 허용 목록의 단일 기준이다.
- 숨긴 기능의 코드를 삭제하거나 권한이 없다는 거짓 상태로 바꾸지 않는다.
- URL 직접 접근도 허용 목록 밖이면 `/overview` 또는 출시 범위 안내로 돌린다.
- Debug 기본 프로필은 기존 전체 메뉴를 유지할 수 있다.
- `OperatorPreview`에서 다섯 route만 열리고 route 정책 자동시험과 390px navigation 검증이 존재한다.

### WP2 — 출시 서버 주소와 환경 경계

예상 수정 경로:

- `SsalddelAdminApp/SsalddelAdminApp.csproj`
- `SsalddelAdminApp/MauiProgram.cs`
- 새 출시 설정 resolver와 시험
- 기존 `SsalddelServerEndpoint`는 공통 URI 정규화에 재사용

계약과 완료 조건:

- Debug는 현재 Android emulator 기본 주소를 유지할 수 있다.
- Release는 명시적인 `SsalddelServerBaseAddress` build 입력이 없으면 닫힌 채 실패한다.
- Release 주소는 `https`만 허용하고 localhost, `10.0.2.2`, query·fragment를 거절한다.
- 서버 주소는 build metadata로 넣을 수 있지만 토큰·계정·서명 암호는 넣지 않는다.
- sample fallback이나 자동 서버 전환을 두지 않는다.
- 유효한 HTTPS 주소는 앱 진단 화면에서 host와 환경명만 확인할 수 있다.

### WP3 — Android 전송 보안

예상 수정 경로:

- `SsalddelAdminApp/Platforms/Android/AndroidManifest.xml`
- 필요 시 Debug 전용 manifest overlay 또는 manifest placeholder 설정
- merged manifest 검사 시험·스크립트

규칙과 완료 조건:

- Release merged manifest는 평문 HTTP를 허용하지 않는다.
- Debug emulator의 `10.0.2.2` HTTP 예외가 필요하면 Debug 산출물에만 제한한다.
- 인증서 오류를 우회하거나 모든 인증서를 신뢰하는 handler를 만들지 않는다.
- 실제 HTTPS 서버에서는 연결되고 HTTP 주소와 잘못된 인증서는 실패한다.

### WP4 — 판본·서명·재현 가능한 AAB

예상 수정 경로:

- `SsalddelAdminApp/SsalddelAdminApp.csproj`
- 새 `eng/release/publish-admin-android.ps1`
- release manifest schema와 관련 문서

출시 명령 입력:

- `VersionName`, 단조 증가 `VersionCode`, `ServerBaseAddress`
- 저장소 밖 `KeyStorePath`, `KeyAlias`
- 저장소 밖 암호 파일 또는 CI secret 참조

출력:

```text
artifacts/local/releases/ssalddel-admin/0.1.0+<code>/
  SsalddelAdminApp.aab
  release-manifest.json
  SHA256SUMS.txt
```

`release-manifest.json`에는 secret 없이 commit, dirty 여부, 앱 판본, version code, target API, 서버 host, build 시각, AAB hash를 기록한다.

완료 조건:

- 전용 upload key로 서명된 AAB를 같은 입력에서 재생성한다.
- keystore·alias 암호·토큰이 Git, binlog, 일반 로그, release manifest에 없다.
- dirty worktree의 정식 후보 생성은 기본 거절하고 명시적 개발 후보만 `Unofficial`로 허용한다.

### WP5 — 실제 서버 운영 폐루프

사용 route:

```text
POST api/v1/auth/login
POST api/v1/auth/refresh
GET  api/v1/admin/dashboard
GET  api/v1/admin/transports
GET  api/v1/admin/drivers/operating
GET  api/v1/admin/operations/follow-up-recoveries
POST api/v1/admin/operations/follow-up-recoveries/{sourceCode}/{sourceItemId}/retry
GET  api/v1/admin/operations/finance/cash-flow-summary
```

검증 사례:

1. 정상 로그인·개요 조회
2. 운송 예외 선택·정본 갱신
3. `RetryIdempotent` 예약·같은 원장 새 revision 재조회
4. 같은 요청 재전송 시 중복 후속 업무 없음
5. access token 만료 뒤 refresh와 원래 화면 복귀
6. 네트워크 단절 시 마지막 정상 자료 읽기 전용 표시·복구 뒤 재조회
7. 현금 흐름 조회가 전표·송금·운영 상태를 변경하지 않음

완료 조건:

- 앱 성공 메시지, API 응답, 원장 revision, Event/Outbox 결과가 같은 사례 ID로 연결된다.
- 화면이 서버보다 앞선 완료 상태를 만들지 않는다.
- 운영 효과는 `Development + Simulation`에서만 발생한다.

### WP6 — 실제 Android 장치

필수 확인:

- 설치와 업데이트, 첫 실행·로그인·로그아웃
- 세로 390px 상당 폭의 판독과 터치 영역
- drawer 열기·닫기와 Android 뒤로가기
- 백그라운드 1분·10분 뒤 복귀
- Wi-Fi 단절·재연결
- 앱 강제 종료·재실행과 세션 복원
- 화면 회전 정책과 키보드 표시 시 로그인 화면
- 토큰·상세 주소·연락처가 시스템 로그에 남지 않음

기기 연결이 없으면 WP6은 `Blocked`이며 emulator나 Windows build로 대체 통과시키지 않는다.

### WP7 — Play Console 내부 테스트

준비물:

- 앱 이름·간단 설명·상세 설명
- 아이콘·휴대폰 스크린샷
- 지원 연락처와 Privacy policy URL
- Data safety 응답 근거표
- 내부 테스터 목록과 `0.1.0` release note

완료 조건:

- 내부 테스트 link로 설치된다.
- Play가 제공한 앱으로 WP5·WP6 핵심 사례를 다시 실행한다.
- version code를 올린 `0.1.1` 시험 업데이트가 기존 앱 위에 설치된다.
- rollback은 이전 바이너리 재설치가 아니라 다음 수정 version code 배포 절차로 기록한다.

## 4. 작업 상태 어휘

| 상태 | 의미 |
| --- | --- |
| `Planned` | 범위·소유·완료 조건만 존재 |
| `Implemented` | 코드·설정이 존재 |
| `Built` | Release 산출물 생성 성공 |
| `ServerVerified` | 실제 HTTPS 서버와 원장 대조 성공 |
| `DeviceVerified` | 실제 Android UI·복귀·재연결 성공 |
| `InternalReleased` | Play 내부 테스트 설치·업데이트 성공 |
| `DogfoodAccepted` | 반복 사용 뒤 다음 단계 진입을 사람이 승인 |
| `Blocked` | 정확한 차단 조건과 재개 지점이 기록됨 |

`Built`를 `InternalReleased`나 `DogfoodAccepted`로 표현하지 않는다.

## 5. 우선순위

```text
WP0 현재 변경 마감
  → WP1 출시 화면 동결
  → WP2 주소·환경 경계
  → WP3 전송 보안
  → WP4 판본·서명·AAB
  → WP5 서버 폐루프
  → WP6 실제 장치
  → WP7 Play 내부 테스트
```

WP2~WP4의 코드 준비는 병렬 가능하지만, 정식 출시 후보 AAB는 WP0과 WP1이 닫힌 뒤에만 만든다. WP5의 headless 서버 검증은 WP6 이전에 가능하지만 실제 장치 증거를 대신하지 않는다.

## 6. 확정·제안·미정

### 확정

- `SsalddelAdminApp` Android를 첫 실제 모바일 출시 대상으로 삼는다.
- 새 기능 확대보다 출시 폐루프 완수를 우선한다.
- 실결제·실정산·실배차·Unity는 `0.1.0` 필수 범위가 아니다.
- build, 서버, 장치, Play 배포 증거를 분리한다.

### 제안

- 첫 채널은 Google Play 내부 테스트다.
- 첫 서버는 `Development + Simulation` HTTPS 환경이다.
- 첫 화면은 로그인·개요·운송·후속 복구·현금 흐름 다섯 route다.
- 그 밖의 관리자 메뉴는 코드를 보존한 채 `OperatorPreview`에서 숨긴다.

### 미정

- 내부 테스트 확정 여부와 Play Console 계정 준비 상태
- 실제 HTTPS 시험 서버 host
- upload key의 보관 위치와 복구 담당
- 첫 실제 Android 기기

## 7. 다음 질문 하나

**`0.1.0` 내부 테스트판에서는 위 다섯 route만 노출하고 공동구매·커뮤니티·콘텐츠·페이지 관리 메뉴는 코드를 보존한 채 숨길까?**

추천은 `예`다. 첫 릴리스의 실제 완료 범위를 한 사람이 반복 검증할 수 있는 크기로 고정하고, 나머지는 검증이 끝난 판본에서 다시 여는 편이 안전하다.
