# 역할 앱 개인 USB 시험 · 기본 음식 네 역할 r27

이 도구는 PC의 격리 Simulation 서버를 USB로 연결하는 로컬 시험용 APK를 만든다. 기본 설정은 음식 네 역할과 `0.1.27/27`이며 주소는 `http://127.0.0.1:5363/`다. 통합/화주·화물 기사·창고를 추가로 선택할 수 있다. 애저 HTTPS 서버용 APK와 실제 기기 검증은 별도다.

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

## 역할 선택·Windows 제작 환경 · 2026-10-17

`AppNames`를 생략하면 기존 음식 네 역할을 유지한다. 지원 대상은 총 일곱 역할이며 선택 지원과 실제 제작·설치 결과를 구별한다.

| AppNames | 앱 | 기본 선택 |
| --- | --- | --- |
| `orderer` | 주문자 | 예 |
| `restaurant` | 음식점 | 예 |
| `driver` | 음식 기사 | 예 |
| `admin` | 운영자 | 예 |
| `shipper` | 통합/화주 | 아니오 |
| `cargo-driver` | 화물 기사 | 아니오 |
| `warehouse` | 창고 관리자 | 아니오 |

```powershell
& ./eng/release/publish-mobile-usb-test.ps1 -AppNames @('shipper','cargo-driver','warehouse') -PlanOnly
```

`PlanOnly`는 제작 계획만 출력한다. 실제 제작에서는 검토한 `Version`·`VersionCode`·새 `OutputRoot`를 명시하고 기존 완료 manifest를 덮어쓰지 않는다. 역할별 APK 이름은 `handoff/<AppNames>-usb-debug.apk`다. 판매자·인사 앱을 이 USB 제작 지원 목록에 자동 포함하지 않는다.

Android native 도구의 Windows 경로 길이 제한을 피하기 위해 실행별 전체 고유 ID와 짧은 역할 키를 사용한 `artifacts/local/u/<run-id>/<role-key>/`에 중간 출력을 둔다. 참조 프로젝트도 격리하며 다른 실행의 중간 출력을 공유·삭제하지 않는다. 최종 APK·manifest는 지정한 실행 출력에 남긴다.

현재 PowerShell 프로세스에 설치한 JDK의 `JAVA_HOME`과 해당 `bin`의 `PATH`를 설정해야 한다. 제작·서명 확인에 사용하는 `java`와 `keytool`이 같은 JDK에서 실행되는지 확인한다. Android SDK 설정과 JDK 설정은 별개이며 전역 환경이나 서명 키를 자동 교체하지 않는다.

## 이번 제작·설치 사실 · 0.1.188/188

통합/화주 `shipper` **APK 한 개**를 실제 제작했다. 서명·정렬·Debug 판본·독립 실행 assembly 포함·handoff 해시 대조를 통과했고 빌드 중 선택 소스 변경은 없었다. 일곱 역할 전체를 이번 판본으로 제작했다는 뜻은 아니다.

- APK: `artifacts/local/integrated-platform-r105/apk/0.1.188-188/handoff/shipper-usb-debug.apk`
- SHA-256: `18f6ff2455484598f99b58d579d61462062cdc14ac89b49b99faeee0e0477fc4`
- 제작·검증 원장: 같은 판본 폴더의 `usb-test-manifest.json`
- 연결 기기 설치: `artifacts/local/integrated-platform-r105/device/install.log`의 `Success`와 `package-after.txt`의 `versionCode=188`, `versionName=0.1.188` 확인

이 기록 시점의 188 기기 화면·로그인·카메라·알림·전체 업무 흐름은 미검증이다. APK 설치 성공과 실제 앱 수행을 같은 완료로 기록하지 않는다. 기존 5363 서버는 이전 작업의 격리 시험 호스트이므로 이번 188 클라이언트와 서버의 소스·판본·API가 일치한다고 가정하지 않는다. 최신 서버 실행·USB 통신·실서버 HTTPS·실수납·실송금은 각각 별도 검증한다. 사용자는 화면 시험을 다음으로 미뤘다. [전체 역할 통합 공개 준비 r105](../../docs/Changes/2026-10-16-integrated-platform-preparation-r105.md)의 남은 조건을 따른다.

## 실제 휴대폰을 연결한 뒤

후속 정본 capability 등록까지 반영한 **0.1.189/code189**는 같은 제작 검사를 통과했고 아직 설치하지 않았다. 최종 파일은 `artifacts/local/integrated-platform-r105/apk/0.1.189-189/handoff/shipper-usb-debug.apk`, SHA-256은 `3e0d01fddc054ecfcc07e4fcb138dbbbd6b40ef5c11969cd40c29c5bf5b4125f`다. 사용자의 화면 시험 연기 뒤 추가 기기 조작은 하지 않았다. 휴대폰188 설치와 로컬189 제작은 별개 상태다.

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
