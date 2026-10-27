# 외부 서버·통합앱 푸시 연결 r106

2026-10-17. `LocalServerAndDevicePushVerified / FinancialEffectsDisabled`.

[통합 공개 r105](2026-10-16-integrated-platform-preparation-r105.md)의 외부 통신·푸시 후속이다. 사용자는 USB 휴대폰을 다시 연결하고 활용하도록 요청했다. 설치, 앱 설정, 실제 Google 발송, Android 표시/청취, 외부 서버 업무 수행을 각각 구분한다.

후속 요청 `서버는 로컬에서 검증`에 따라 이번 실행은 로컬 API·USB 휴대폰·실제 Google FCM 통신으로 범위를 정했다. Azure 로그인 대기는 종료했으며 리소스 생성·설정 변경·배포는 하지 않는다. 외부 배포 코드는 준비 상태로 보존하고 실제 서버/웹 배포 묶음 제작도 보류한다.

## 현재 확인

- USB 디버깅 재승인과 통합앱188→189→190 업데이트 설치를 확인했다. 190에는 전용 Firebase 구성이 포함되며 앱 데이터 삭제·서명 교체는 하지 않았다.
- 화면 보완 APK191은 제작만 했으며 통신 보안 의존성 정렬 뒤 APK192를 새로 제작·업데이트 설치했다. 192의 제작/설치 base.apk SHA-256 일치, 알림 권한, 기존 로그인/기기 등록 유지, 서버의 활성 기기 버전0.1.192를 확인했다.
- 기존 Firebase 프로젝트에서 Android 기사 앱만 등록된 것을 확인한 뒤 통합앱 `com.companyname.ssalddelapp`을 추가했다. 기존 앱은 유지했다.
- 브라우저 구성 다운로드가 완료되지 않아, 기존 서버 서비스계정으로 공식 Firebase Management API의 Android SDK 구성을 조회·다운로드했다. 새 서비스계정·발송 키·IAM 권한은 만들지 않았다. 파일은 저장소 밖에 두며 원문/키를 코드·문서에 복사하지 않는다.
- 외부 SDK 파일의 패키지·명시 프로젝트·위치·제작 전후 hash를 기존 USB/HTTPS 포장에 결속했다. 합성 반례13개와 기존 USB 포장21개가 통과했다.
- r106 전용 MySQL/Mongo·키링과 합성 계정9개를 준비하고 루프백 API5363을 실행했다. 초기 root 비TLS 인증 실패는 보존하고 TLS 접속으로 초기화를 통과했다. 휴대폰190에서 실제 로그인 API와 Android 알림 권한 허용, 앱의 소리·진동 선택을 확인했다. 이는 실사용자 가입·실제 위치 증거가 아니다.
- 시험 도구의 배열 조회 호환 오류와 실제 MySQL 재시도 전략/명시 트랜잭션 충돌을 구분하여 기록했다. 실패한 case01/02는 보존했다. 참여 저장·등록·추천 생성 수정의 집중 시험5개와 실제 MySQL의 case03/04 제안 생성을 통과했다. 수락·증빙·완료·복구·수령/보존 처리도 같은 SQL 변경 경계를 보완하고 최종 회귀 시험을 통과했다. 모든 명령의 실제 MySQL/기기 수행 완료를 뜻하지 않는다.
- case03은 기존 Firebase 발송기를 통한 Google HTTP v1 승인과 Android 실제 알림 기록을 확인했다. 해당 알림을 탭해 같은 합성 제안의 5,000원·2.5km를 조회했고, 중요도4 및 앱 알림음 자원을 확인했다. 사람의 실제 청취 답변은 대기 중이다. 수락·실제 GPS·사진·전달·수령·금융 완료 증거로 확대하지 않는다.
- 최종 서버 바이너리와 APK192에서는 새 case04를 준비한 뒤 홈 버튼으로 앱을 뒤로 보냈다. 새 제안의 Google 승인·정확한 offer tag의 Android 게시·알림 선택 뒤10:24 기한/5,000원/2.5km 조회를 확인했다. force-stop 시험이나 추천 배너 전체 화면 캡처로 확대하지 않는다.
- 통합 웹·API 전용 배포 프로필과 포장 검사를 준비했다. 이미지의 전체 파일층 및 Config 환경값·라벨·history, 웹 비밀 파일, 추가/중복 이미지 태그·Simulation 생활배송 경계를 포함한46개 자체검사와 빌드 준비11개 모의 검사가 통과했다. 실제 최종 서버/웹 산출물 검사와 Azure 배포는 별도다.
- 현재 Azure에서 보이는 무료 App Service는 이전 .NET7/F1 환경이며 현재 통합 서버·DB·키·배포를 확인한 대상이 아니다. 덮어쓰지 않았다. 예전 VM 구독의 로그인 갱신을 요청했고, 확인 전 새로운 서버를 만들지 않았다.

## 검증 흐름

```text
통합앱에 전용 Firebase 설정 포함
→ 실제 기기 토큰 생성
→ 합성 계정 로그인·배송 참여·기기 등록
→ 같은 계정의 현재 유효한 배송 제안 준비
→ 별도 단발 도구에서 기존 Firebase 발송기 호출
→ Google 승인 / Android 표시 / 알림음 청취 / 탭 뒤 정본 조회를 각각 확인

외부 서버 후속
→ 같은 소스의 서버 이미지·통합 웹·격리 profile 묶음
→ 전용 DB·키·HTTPS로 배포
→ HTTPS 주소로 APK 재제작
→ 휴대폰의 USB 경로 없는 독립 통신 검증
```

일반회원 배송 제안은 기존 음식·화물 기사 앱의 추천 카드와 다른 경로다. 로컬 API+실제 Google FCM 통신을 Azure 서버 통신으로 표시하지 않는다. 제공자의 발송 승인을 사용자의 알림 청취로 표시하지 않는다.

## 보존 경계

기존 일반회원 Push Outbox의 Simulation 발송 차단은 유지한다. 단발 통신 도구는 현재 제안·참여·원 의뢰·기기 owner/appKey를 재검사하고 1회만 발송하며 제품 Outbox를 Sent로 바꾸지 않는다. 토큰 원문은 메모리에서만 사용하고 증거에는 hash·일치 여부만 남긴다.

기존 r67 호스트·DB·키링은 보존하며 별도 r106 시험 DB를 준비한다. 현재 준비 중인 외부 profile은 Simulation·금융/지급·자동 배차·주간 명세 준비 OFF다. Simulation만으로 모든 HostedService가 꺼지지는 않으므로 일반 투영/알림함 등까지 전체 worker OFF로 주장하지 않는다. 원본 정산·신고·실금융 보호를 바꾸지 않는다.

## 실제 기기 근거

- APK190 제작본과 설치된 base.apk의 SHA-256 일치를 확인했다. 전용 Firebase SDK 구성과 서버 서비스계정의 프로젝트 및 기기 owner 결속을 확인했다.
- `device/fcm-device-evidence-03.json`에 Google 승인·Android 게시·알림 탭·제안 조회·금융 불변·청취 미확인을 구분했다. 화면은 `device/fcm-offer-open-03.png`, `device/fcm-offer-fee-03.png`이며 다른 앱/알림 화면은 저장하지 않았다.
- 실제 FCM 검증은 `host/server-binary-r6.json`의 서버 바이너리와 APK190에서 수행했다. 뒤따르는 서버 코드 수정의 검증은 별도 결과로 기록한다. 제품 Push Outbox는 Sent로 바꾸지 않았다.
- 화면에서 중량의 불필요한 긴 소수점, 로그인 뒤 방문자 배지, 고정 하단 메뉴가 마지막 명령을 가리는 문제를 관측했다. 기존 내 정보 경로의 사람 아이콘, 값 손실 없는 중량 표기, 하단 안전 여백으로 소스를 보완하고 APK192의 실제 표시를 검증했다.
- APK192의 `fcm-offer-open-04.png`와 `fcm-offer-fee-04.png`에서 사람 아이콘·`1 개 · 1 kg`·배송비/거리·하단 메뉴 위의 마지막 명령을 실제로 확인했다. `최신 상태 확인`은 bounds `[438,1787][835,1925]`로 하단 메뉴 시작보다 위에 있고 실제 읽기 명령을 눌렀다. 수락/거절은 확정하지 않았다.
- `device/fcm-device-evidence-04.json`은 실제 Google/Android/배경 상태/조회와 청취 미확인·금융 불변을 나눠 기록한다. 최종 서버는 `host/server-binary-patched.json`에 결속했고, 초기 case03의 서버r6/APK190 근거와 합치지 않는다.
- 실제 UTC 기한01:24:04 이후 기기 시각을 바꾸지 않고 같은 제안을 재조회했다. `기한 만료`·`지금은 수락할 수 없습니다` 표시와 수락 명령 부재를 실제 UI에서 확인했다(`device/offer-expiry-04.json`, `fcm-offer-expired-04.png`). 초기 APK190의 가려진 버튼으로 실패한 case03 만료 조회 기록은 보존하며 성공으로 바꾸지 않는다.

## MySQL 저장 경계 보완

실제 기본 MySQL 연결은 `EnableRetryOnFailure`를 사용한다. 참여 저장의 명시 transaction이 그 실행 전략 밖에 있어 `ExecutionStrategy.OnFirstExecution`에서 실패했다. SQLite 기본 시험만으로는 드러나지 않았던 연결 조건이며, 시험 호스트의 재시도 설정을 끄지 않고 제품의 SQL 변경 경계를 수정했다.

- 참여 저장·등록·추천·수락/거절·취소/복구·수령 확인·증빙의 prepare/uploadLock/final·픽업/전달·문제 접수·사진 보존 정리를 기존 DB 실행 전략에 연결했다.
- 같은 ClientRequestId/입력 해시/증적/판본을 재조회하고 재시도 때만 추적 상태를 비운다. MySQL 일시 오류는 전략에 전달하고 영구 충돌의 기존 오류 코드는 유지한다. 요청 취소 시 재시도 대기도 취소한다.
- 네이버 경로 추정·지원 사건 생성·Mongo 후속 투영은 무조건 재전송하지 않는다. 사진 업로드/삭제는 기존 행 잠금과 만료 보호를 유지하며 같은 호출의 물리 쓰기를 한 번만 보내고, 재시도에서 실제 객체 hash와 정본을 다시 확인한다.
- 최초 통합 Fast는 빌드와 생활배송 관련145개 시험을 통과했다(`artifacts/local/validation/20261017-094659/`). 사진 예산 카운트는 같은 SQL transaction의 롤백 뒤 다시 반영하고, 물리 객체 쓰기의 1회 캐시와 구분했다. 이후 Fast149개도 통과했다(`20261017-095153`).
- 최초 전체 Task는 빌드 성공 뒤 통과12,410개·실패33개·환경 의존 생략16개였다(`20261017-095352`). 기존 SQLite 시험의 전역 EF 모델 캐시에 다른 시험의 암호화 변환기가 섞이는 원인을 확인했다. 실패 TRX를 보존하며 해당 생활배송 fixture에 전용 provider를 적용하고 기본/재조회/재시도 모델의 보호 형식과 실제 주소 재조회를 대조하는 회귀시험을 추가했다. 제품 암호화·엄격한 시험용 보호 검사·실제 MySQL 연결 옵션은 완화하지 않았다. 최종 재실행 결과와 배포 산출물은 별도 기록한다.
- fixture 격리 후 Fast150개(`20261017-095910`), 보안 의존성 정렬 후 세 솔루션 빌드와 생활배송150개(`20261017-100609`)를 통과했다. 후자의 Simulation/Unity 필터 시험은0건이므로 해당 영역 전체 시험 통과로 집계하지 않는다.
- 최종 전체 Task(`artifacts/local/validation/20261017-101740/`)는 세 솔루션 빌드, Simulation2,001개·Unity 코드970개·제품12,444개를 통과했다. 합계15,415개 통과·실패0개이며 환경 의존16개는 생략이다. 초기 실패와 최종 TRX를 각각 보존한다. Unity 에디터나 실제 게임 실행 검증은 추가하지 않았다.

## 통신 의존성과 배포 파일 범위

기존 SignalR.Client10.0.9의 전이 의존성 Common10.0.9가 제품 웹·앱에도 포함된 것을 확인했다. [Microsoft의 CVE-2026-70159 공지](https://github.com/dotnet/announcements/issues/452)에 따라 Client10.0.13과 그 최소 의존성 DI.Abstractions10.0.13으로 정렬했다. 첫 제작 시 DI의 구버전 직접 참조로 발생한 NU1605 로그(`20261017-100544`)는 보존한다. PC의 기존 ASP.NET runtime10.0.9를 교체하거나 인터넷에 노출하지는 않는다.

외부 배포 준비에는 전용 `Dockerfile.integrated-mobile-field-test`의 ASP.NET10.0.13 고정과 실제 이미지 Config.Env 버전 검사를 추가했다. 모의 검사12개 통과는 실제 컨테이너 제작·실행 증거가 아니다. 서버의 `appsettings.Testing*.json`은 게시 출력에서 제외하도록 설정했다. 실제 게시 산출물 확인과는 구분하며 사용자 후속 범위에 따라 이번에는 Azure 배포와 최종 서버/웹 묶음 제작을 하지 않는다. 제품 웹/공통 클라이언트/앱의 실제 복원 의존성은 `security-dependency-evidence.json`에 기록했다.

기본 출력은 Git 제외 `artifacts/local/external-connection-r106/`에 둔다. 실제 실행 결과가 생기면 이 기록을 갱신하며 준비만 한 항목을 실행 완료로 승격하지 않는다. 이번에 commit·push·실수납·실송금은 요청되지 않았다.

근거: [Android FCM 설정](https://firebase.google.com/docs/cloud-messaging/android/get-started), [서버 FCM 인증](https://firebase.google.com/docs/cloud-messaging/send/v1-api), [Android SDK 구성 조회](https://firebase.google.com/docs/reference/firebase-management/rest/v1beta1/projects.androidApps/getConfig).
