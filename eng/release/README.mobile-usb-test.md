# 음식 배달 네 역할 개인 USB 시험 r27

이 APK는 PC의 격리 Simulation 서버를 USB로 연결하는 로컬 시험용이다. 주소는 `http://127.0.0.1:5363/`, 판본은 `0.1.27/27`이다. 애저 HTTPS 서버용 APK와 실제 기기 검증은 별도다.

## PC에서 준비

```powershell
pwsh -NoProfile -File eng/verification/food-mobile-four-role.ps1 -Action Probe
```

`Ready`와 `/health/ready` 200을 확인한다. 처음 환경을 만드는 절차는 `Prepare → Build → Initialize → Start → Probe`다. 기존 r27 환경에서는 임의로 새 준비를 시작하거나 다른 시험 DB를 삭제하지 않는다. 비공유 시험 계정은 `artifacts/local/food-mobile-four-role-r27/server/.private/accounts.json`에 있다.

APK 재생성은 기존 저장소 밖 debug key와 Android SDK/JDK를 사용한다.

```powershell
pwsh -NoProfile -File eng/release/publish-mobile-usb-test.ps1
```

이 명령은 설치하지 않는다. 실행별·앱별 출력 폴더를 격리하고 선택 소스가 빌드 중 바뀌면 완료 manifest를 만들지 않는다. 완료 파일은 `artifacts/local/food-mobile-four-role-r27/apk/0.1.27-27/handoff/`와 `usb-test-manifest.json`이다. 같은 완료 판본을 재생성하려면 새 `OutputRoot`를 지정해 이전 결과를 보존한다.

## 실제 휴대폰을 연결한 뒤

Android SDK의 `adb`에서 연결한 대상의 serial을 먼저 확인한다. 다음의 `<serial>`은 그 기기 값으로 바꾼다.

```powershell
adb devices -l
adb -s <serial> reverse tcp:5363 tcp:5363
adb -s <serial> install -r artifacts/local/food-mobile-four-role-r27/apk/0.1.27-27/handoff/orderer-usb-debug.apk
```

나머지는 `restaurant-usb-debug.apk`, `driver-usb-debug.apk`, `admin-usb-debug.apk`다. 기존 앱과 서명이 다르면 설치 오류를 기록하고 데이터를 지우거나 앱을 자동 삭제하지 않는다. PC 서버가 꺼지거나 USB reverse가 끊기면 통신할 수 없다.

## 확인 순서와 기록

1. 네 앱 각각의 실제 로그인과 역할 첫 화면을 확인한다.
2. 같은 주문의 번호를 따라 주문 제출 → 음식점 수락 → 기사 제안·직접 수락 → 조리 시작·준비 완료 → 음식점 도착·픽업 → 전달 → 주문자 수령 확인을 처리한다.
3. 기사 배정 전 조리 제한, 처리 중 중복 입력 방지, 전달 뒤 화면 재진입, 조회 실패 후 재조회, 완료 내역의 고객 정보 제한을 확인한다.
4. 화면·서버 응답·DB 상태와 실제 휴대폰 결과를 따로 기록한다. 전달 완료·수령 확인·공제 확정·입금 확인을 하나의 완료로 합치지 않는다.

고정 시험 거리 `0.350km`와 합성 위치는 실제 경로·GPS 측정값이 아니다. 현재 서버는 지급을 요청하지 않으며 정산 공제와 수령액이 미확정일 수 있다. 브라우저 완주와 에뮬레이터 로그인은 실제 휴대폰 전체 업무 검증을 대신하지 않는다.
