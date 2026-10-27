# 기사 새 배차 추천의 단발 진동·홍달배달 음성 r18

사용자가 요청한 **새 추천마다 짧은 진동 한 번과 ‘홍달배달’ 음성 한 번**을 기존 화물 기사 앱 알림에 연결했다. 추천은 아직 수락 전이므로 배차 확정이라고 표현하거나 자동 수락하지 않는다. 기존 알림 설정을 켠 상태에서만 동작한다.

승인 범위는 [r18 작업지시](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-recommendation-signal.r18.md), 기준 commit은 `d3a19163ba96a7dbed31028f639f3bf0c534ae79` / `dev/mirror-integration`이다. 최종 PLAN SHA-256은 `36d4016e71208228ec6b93651db7898e4798001e12a02aa05b57573bad567070`다. 수정 전 자료와 별도 최종 변경 해시를 [로컬 검증함](../../artifacts/local/driver-recommendation-signal-r18/)에 보존한다. 기존 r17 운행 보완과 관련 없는 dirty 변경을 유지한다.

## 바뀐 연결

- 지도에서 최신 서버 정본을 확인하고 실제 표시한 추천을 기존 `IDriverCargoRecommendationAlertService`로 보낸다. 기존 Push 복구도 같은 서비스와 게시 이력을 쓴다. 화면 재그리기나 카운트다운은 별도 알림을 만들지 않는다.
- 전경의 새 조회·페이지 이탈·백그라운드·계정/세션 교체는 이전 알림 조회를 취소한다. 알림 조회가 느리거나 실패해도 업무 지도는 계속 쓸 수 있다. 취소된 옛 조회의 실패로 다른 경로의 새 알림을 지우지 않는다. 교차검토에서 발견한 검사→취소 사이의 경쟁을 보완해, 예외/거절 네 곳의 최종 확인과 실제 취소도 새 게시와 같은 잠금 안에서 직렬화한다.
- 소리/진동 독립 설정, 야간 제한, 본인·현재 근무·배차 수신 ON·유효 추천 검사를 유지한다. Android 알림 권한·앱 차단·채널 차단·무음·방해금지를 별도 재생기로 우회하지 않는다.
- Android 새 채널 `cargo_dispatch_alerts.v2`에 이름 기반 `android.resource://{package}/raw/hongdal_dispatch` 음원과 `[0,180]` 단일 진동 패턴을 연결했다. 이전 v1과 새 v2의 차단·무음 상태를 함께 보존한다. Manifest에 `VIBRATE` 권한을 명시했다. Android 8 미만 경로도 같은 음원/단일 패턴을 쓴다.
- 고정 OS 알림 ID에 `OnlyAlertOnce`를 무조건 붙이면 다음의 다른 추천까지 소리가 억제될 수 있어 붙이지 않는다. 반복 플래그·루프를 켜지 않고 본인/의뢰/추천 라운드 이력으로 재조회를 억제한다. [Android 채널의 사용자 설정·생성 후 변경 제한](https://developer.android.com/develop/ui/compose/notifications/channels), [채널 sound·vibration API](https://developer.android.com/reference/android/app/NotificationChannel)를 대조했다.

## 중복과 실패 의미

기존 Preferences 값을 그대로 사용하면서 단일 계정 기록을 계정별 해시 이력으로 이행한다. 같은 설치에서 재시작·A→B→A 계정 왕복·동시 호출·전경/Push 중복이 같은 유효 추천을 다시 게시하지 않도록 했다. 이전 기록의 살아 있는 추천도 보존하며, 만료된 기록만 정리한다.

OS 게시 전에 이력 저장과 재읽기를 확인한다. 저장 실패·손상·미래 형식이면 소리를 먼저 내지 않고 보류한다. 저장 뒤 OS 게시 실패·취소·프로세스 중단도 재생을 반복하지 않도록 소비된 이력은 남긴다. **이력 예약은 알림 전달 성공이 아니다.** Android 게시와 단말 저장의 원자적 전달, 디스크 손실·데이터 삭제·재설치까지 영구 한 번을 보장하는 구조는 아니다.

## 음원

`DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav`: ‘홍달배달’ 한 문장, Windows System.Speech / Microsoft Heami Desktop(ko-KR), rate -1, 파일 출력만 사용해 한 번 합성했다. PCM16·모노·22,050Hz, 84,000바이트, 1.9037188209초이며 SHA-256은 `6e0da9aaa1b36521b71a7ce0dff5412edd315cc6531d96c308e385a4c4cbbab2`다.

생성 스크립트·문구 코드포인트·PCM 신호·SHA 근거는 [음원 기록](../../artifacts/local/driver-recommendation-signal-r18/audio/manifest.json)과 [12개 PCM 확인](../../artifacts/local/driver-recommendation-signal-r18/audio/pcm-verification.json)에 있다. 음원은 서비스 취소 경쟁 보완 전 승인 PLAN(`f35319677032de41aaa7ed6e5bded0058764adeb474a7bca59fa3e44471815a2`) 아래 합성했으며 이후 문구/바이트를 바꾸지 않았다. 스피커로 재생하지 않았으며 발음 청취·휴대폰 볼륨·진동 체감은 미확인이다. 기존 음식 기사 앱 음원은 교체하지 않았다. Windows 음성의 공개 배포 조건 독립 확인도 아직 별도다.

## 검증 현황

첫 판본의 Fast174/174와 Task16,353통과·환경16생략을 확인했고, APK132의 음원·Manifest·서명·두 ABI의 실제 앱 DLL/endpoint도 확인했다. 교차검토 후 서비스 취소 경쟁을 추가 보완했으므로 이 결과를 최종 판본에 승계하지 않는다. APK132와 이전 기록은 보존한다.

| 최종 판본 확인 | 결과와 근거 |
| --- | --- |
| Fast | 174통과·0실패·0생략, [최종 실행](../../artifacts/local/validation/20261027-114430/)에서 DriverApp Android와 시험 project 빌드 포함 |
| Task | 16,353통과·0실패·환경16생략, [최종 실행](../../artifacts/local/validation/20261027-114549/)의 v3.5 solution 빌드와 전체 시험. 실제 Mongo/MySQL 환경 의존 시험 생략을 성공으로 합산하지 않음 |
| 음원 | 문구·PCM·SHA 12개 검사 통과. 청취 없음 |
| 검토 APK | [0.1.133 / code133](../../artifacts/local/driver-recommendation-signal-r18/apk-driver-release-r133/kr.hongdal.driver.cargoreview-Signed.apk), 패키지 `kr.hongdal.driver.cargoreview`, 서명/정렬/버전 확인. 새 출력에 생성했으며 설치하지 않음 |
| APK 안의 자료 | WAV 바이트/해시 일치·병합 Manifest의 VIBRATE/POST_NOTIFICATIONS 확인, [포장 검증](../../artifacts/local/driver-recommendation-signal-r18/apk-signal-verification-r133.json) |
| 실제 앱 DLL | 최종 APK의 arm64-v8a/x86_64에서 압축된 DriverApp DLL을 풀어 현재 빌드 DLL SHA와 고정 loopback 접속 주소를 각각 대조, [검증](../../artifacts/local/driver-recommendation-signal-r18/apk-driver-endpoint-proof-r133.json). 실제 통신 성공과 구분 |
| 소스 고정 | 선택한 3,053파일의 빌드 전후 변경0. 이전132와의 앱 입력 차이는 취소 보완 서비스 한 파일로 제한해 [대조](../../artifacts/local/driver-recommendation-signal-r18/build-driver-r133-input-delta.json). 모든 전이 입력/도구/환경의 완전한 재현성 증거는 아님 |

APK133 SHA-256: `bd8957c2d7737a69bd67c53530fc85263365a69de7b309714ecfb1941536b05f`. 이전 빌드의 성공한 격리 중간 캐시를 좁게 재사용했으며 APK132·로그·해시는 보존했다. 중간 캐시 내부 DLL이 원래 판본 그대로 보존됐다는 뜻은 아니다. 자동시험은 실제 Android 알림음·진동 체감이나 FCM 수신의 증거가 아니다.

새 직접 회귀는 이력 15개·전경 수명 10개·취소된 조회의 늦은 실패 2개다. 별도 교차검토에서 Android 설정 이전/이력과 전경 수명/게시 직렬화도 대조했다. [최종 경로·해시·링크 점검](../../artifacts/local/driver-recommendation-signal-r18/final-check.json)을 함께 보존한다. 화면 배치를 바꾸지 않았으므로 새 실제 화면 스크린샷을 완료 근거로 만들지 않았다.

## 남는 실제 확인

사용자는 이번에 휴대폰 소리·진동을 확인할 수 없다고 명시했다. 시험 알림이나 실제 음향·진동을 발생시키지 않았고 기기 앱도 갱신하지 않는다. r17의 실제 휴대폰 APK131 운행 확인을 이번 알림 검증으로 승계하지 않는다. 다음 기기 확인에서 첫 추천·같은 추천 재조회·새 추천/라운드, 소리/진동 각각 OFF, 야간·무음·방해금지, 백그라운드 수신을 각각 확인해야 한다.

FCM 자격/연결·서버/배차 규칙·운영 플래그·공개 배포·실제 운행·commit/push는 이번 변경으로 수행하지 않았다. 새 APK는 로컬 검토용 고정 loopback 접속 대상으로 포장하며 실제 운영 연결을 뜻하지 않는다.
