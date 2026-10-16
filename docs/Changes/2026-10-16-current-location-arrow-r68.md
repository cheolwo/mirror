# 화물 기사 현재 위치 방향 표시 r68

2026-10-16. 구현·전체 로컬 검증·APK181 설치·정지한 실기기 표시 검증 완료.

사용자는 지도에 있는 현재 위치 점 대신 SDK가 제공하는 방향 화살표를 옵션으로 사용할 수 있도록 요청했다. 이번 범위는 현재 확인 중인 화물 기사 앱이다. 기존 화면 설정에서 선택하고 지도에서 반영하는 구조로 연결하며, 음식 기사 앱·서버 원장·위치 전송 주기·지도 카메라 회전은 변경하지 않는다. [화면 책임 카드](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#화물-기사-현재-위치-방향-옵션-r18--r68).

## 공식 지원 확인

네이버 SDK는 `LocationOverlay.DefaultSubIconArrow`, `SubIcon`, `Bearing`으로 기본 방향 화살표와 방위를 지정한다. 지도 카메라까지 회전시키는 `Face` 모드는 이번 선택에 포함하지 않는다. [네이버 위치 오버레이](https://navermaps.github.io/android-map-sdk/guide-ko/5-5.html), [위치 추적과 커스텀 구현](https://navermaps.github.io/android-map-sdk/guide-ko/4-2.html).

방향은 휴대폰 센서로 관측하며 GPS 이동 방향과 구분한다. MAUI 컴퍼스는 자기 북쪽을 기준으로 하므로 Android의 지자기 편각으로 지도 북쪽 기준을 보정한다. [MAUI 센서](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/sensors?view=net-maui-10.0), [Android GeomagneticField](https://developer.android.com/reference/android/hardware/GeomagneticField).

## 구현과 검증

- 기존 `/driver/settings/views`에 현재 위치점·방향 화살표 선택을 추가했다. [기기 표시 설정](../../DriverApp/Services/기사위치표시설정Service.cs)은 기본 위치점을 보존하고, 선택을 이 APK의 로컬 Preferences에 저장한다.
- [Android 지도 방향 처리](../../DriverApp/Handlers/DriverNativeMapViewHandler.방향.Android.cs)는 SDK 기본 보조 화살표와 방위를 사용한다. 전경·옵션·지도 제공자·위치 확인 조건이 맞을 때만 센서를 구독하고, 직접 시작한 센서만 중지한다. 화면/지도 세대가 바뀌면 대기 중인 콜백도 적용하지 않는다.
- [방향 상태](../../DriverApp/Models/Driver/Samples/기사위치방향State.cs)는 유효한 관측과 미확인을 구분하고, 방향이 10초를 넘으면 점으로 돌아간다. 359→1도 사이를 원형으로 평활 처리한다. 실제 북쪽 0도와 미확인 `null`을 합치지 않는다.
- 방향 옵션 mapper는 카메라를 갱신하지 않는다. 기존 현재 위치의 확인/만료 기준과 서버 전송 주기는 유지한다. 방향은 휴대폰 자체 상단 기준이며 가로 화면의 위쪽으로 재해석하는 별도 보정은 하지 않았다.

[관련 집중 시험](../../artifacts/local/validation/20261016-location-direction-r68/focused-tests.log)과 Fast 검증에서 **58개 통과, 실패·생략 0개**를 확인했다. 순수 정책의 만료·역순 관측·NaN/Infinity·편각·북쪽 경계와 기존 위치/업무 흐름 회귀를 포함한다. Fast는 DriverApp과 시험 프로젝트 빌드도 통과했다.

[전체 Task 검증](../../artifacts/local/validation/20261016-location-direction-r68/task.log)은 솔루션 전체 빌드와 제품 시험 **12,238개 통과, 실패 0개, 환경 의존 16개 생략**으로 마쳤다. [시험 결과](../../artifacts/local/validation/20261016-161753/Ssalddel.Tests.trx)를 보존했다.

## APK와 실제 휴대폰 확인

기존 연결 휴대폰의 화물 기사 앱을 **0.1.181 / 181**로 업데이트했다. [APK 제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-location-direction/cargo-driver/manifest.json)과 [설치 검증](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r181.json)에서 버전·기기 APK 해시 일치를 확인했다. SHA-256은 `b5e5337a09e516db65704694b979ec520734aa55e66e0051eaacb8c6b747ad98`이다. 앱 데이터와 기존 로그인은 보존했다.

연결된 Galaxy A34 / Android16의 실제 앱 버튼을 사용했고, 캡처 PNG를 직접 확인했다. 원본은 위치가 포함되는 비공개 로컬 증거에 남기며 문서의 공개 이미지 자산으로 복사하지 않는다.

| 실제 확인 | 증거 | 결과 |
| --- | --- | --- |
| 기본 위치점 | [D05](../../artifacts/local/physical-phone-r67/screens/D05-r181-point.png) | 파란 점만 표시 |
| 기존 화면 설정에서 방향 화살표 선택 | [D09](../../artifacts/local/physical-phone-r67/screens/D09-r181-settings-arrow.png) | 선택 버튼과 저장 안내 표시 |
| 지도에서 방향 표시 | [D10](../../artifacts/local/physical-phone-r67/screens/D10-r181-arrow-map.png) | 파란 점 옆 SDK 기본 방향 화살표 표시 |
| 현재 위치점으로 다시 선택 | [D11](../../artifacts/local/physical-phone-r67/screens/D11-r181-arrow-disabled.png) | 화살표가 사라지고 점만 표시 |
| 화살표 ON 후 홈으로 나갔다 복귀 | [D12](../../artifacts/local/physical-phone-r67/screens/D12-r181-background-return.png) | 복귀한 지도에서 화살표 표시 |
| 앱 프로세스를 종료·다시 실행한 뒤 지도 진입 | [D14](../../artifacts/local/physical-phone-r67/screens/D14-r181-restarted-arrow.png) | 다시 선택하지 않아도 화살표 표시 |

마지막에는 방향 화살표 ON, 신규 배차 받기 OFF를 유지했다. 재실행 검증으로 중단된 기기의 위치 송신은 기존 운행 시작 화면의 정상 복구로 재개했고, 기존 운행 확인·공개 설정 미변경 안내와 [최종 지도 D20](../../artifacts/local/physical-phone-r67/screens/D20-r181-final-arrow.png)의 서버 수신 시각 갱신을 확인했다. 새 배차·주문·금융 처리·기능 활성화는 수행하지 않았다.

## 확인 한계

실기기는 제자리에 둔 상태다. 실제 휴대폰 회전·진북 정확도·자기장 간섭·주행 중 정확도·가로 화면 재보정·센서 미지원/10초 무응답의 기기 재현은 확인하지 않았다. 10초 만료·잘못된 값·역순 콜백 등은 순수 정책 시험 근거이며 실기기 장애 재현으로 표시하지 않는다. 배경 복귀 화면 확인은 센서의 실제 배경 중단 측정과 별개이며, 센서 구독/소유권 해제는 코드 검토로 확인했다. 기존 위치 확인·위치 만료 관문도 유지한다. 이번 요청에서 음식 기사 앱·서버·Unity·배포·commit/push는 변경/실행하지 않았다.

원본 화면은 비공개로 유지하며, 검토한 대표 파생 화면과 판본별 설명은 [휴대폰 UI 진행 요약](2026-10-16-phone-ui-progress-summary.md)에서 확인한다.
