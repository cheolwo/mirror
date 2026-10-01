[기획 · 음식점 모바일·USB 시험 준비 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · restaurant-usb-apk-preparation r1]

# 음식점 앱 USB 설치용 APK 준비

- 요청: 2026-09-28 사용자가 휴대폰을 데스크톱에 연결해 APK를 설치할 수 있도록 미리 준비해 달라고 했다.
- 범위: 기존 `RestaurantDeskApp`의 현재 로컬 소스를 개발용 APK로 포장하고 설치 절차를 남긴다. 제품 코드·서버·DB·금전 정책·공개 배포는 변경하지 않는다.
- 선행: [음식점 앱 우선](restaurant-mobile-workflow.r1.md), [앱 완성 방향 r60](../PLAN-SYSTEM-FRANCHISE-OPERATIONS/delivery-app-parallel-completion.direction.r60.md).
- 상태: `ApkPrepared / SignatureVerified / DevicePending / ServerPending`.

## 기존 배포 계획과 구분

이번 요청은 본인의 PC에 USB로 연결하는 개발 시험 준비다. 기존 [메뉴 대표 실행 r12](../PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-reference-run.r12.md)의 공개 HTTPS·외부 전용 키를 사용하는 Release 포장과 구별한다. 그 조건이나 `publish-mobile-field-test.ps1`을 완화하지 않으며, 이번 개발 APK를 r12의 Release 성공이나 메뉴 실제 API/UI 검증 성공으로 대체하지 않는다.

기존 Debug의 로컬 주소 허용과 Android의 loopback 예외만 재사용한다. 기존 개발 서명 키를 사용하고 새 배포 키를 만들지 않는다. 앱 표시명은 빌드 인자로 `살뜰 식당 USB 시험`을 사용한다. Debug에서는 개발용 화면도 열릴 수 있으므로 본인 검증 외 타인 배포·운영 계정 사용·스토어 업로드를 하지 않는다.

## 고정한 패키지 조건

| 항목 | 값 |
| --- | --- |
| 앱 | `RestaurantDeskApp` |
| 패키지 ID | `com.ssalddel.restaurantdeskapp` |
| 앱 판본 | `0.1.0 (1)` — 기존 값 유지 |
| 구성 | `Debug`, APK 내부에 관리 어셈블리 포함 |
| 서버 주소 | `http://127.0.0.1:5321/` — 기존 food-observer의 전용 로컬 포트 |
| 연결 | 휴대폰의 해당 포트를 `adb reverse`로 PC의 동일 포트에 연결 |
| 저장 위치 | `artifacts/local/mobile-field-test/restaurant-usb-20260928/RestaurantDeskApp-usb-debug.apk` |

인증 정보는 APK에 넣지 않는다. 설치되어도 PC의 격리 서버가 준비되지 않았으면 로그인·메뉴 조회는 성공하지 않는다. 서버 준비 시 기존 `Development + Simulation`과 전용 DB·계정을 확인하고, 다른 서버로 임의 연결하거나 실패를 샘플 성공으로 대체하지 않는다.

## 재현한 빌드 명령

저장소 루트에서 실행한다. 기존 파일과 서명 키를 사용하며 공개 배포 스크립트는 호출하지 않는다.

```powershell
dotnet build RestaurantDeskApp/RestaurantDeskApp.csproj `
  -t:SignAndroidPackage -f net10.0-android -c Debug `
  -p:EmbedAssembliesIntoApk=true -p:AndroidPackageFormats=apk `
  -p:SsalddelServerBaseAddress=http://127.0.0.1:5321/ `
  -p:UseSharedCompilation=false '-p:ApplicationTitle=살뜰 식당 USB 시험'
```

`EmbedAssembliesIntoApk=true`는 개발 도구의 별도 파일 전송에 의존하지 않도록 하는 설정이다. [Microsoft의 Fast Deployment 안내](https://learn.microsoft.com/en-us/dotnet/android/messages/xa0130)를 확인했다. 설치·실행 성공은 실제 장치에서 별도로 검증한다.

## 휴대폰 연결 후 절차

1. Android 개발자 옵션의 USB 디버깅을 켜고 데이터 전송 가능한 케이블로 연결한다. 휴대폰의 이 PC 디버깅 허용 창을 직접 확인한다.
2. 아래 `devices -l`에서 본인 장치가 `device` 상태인지 확인한다. `unauthorized`면 휴대폰 승인을 기다리고, 여러 장치가 있으면 정확한 장치 식별자를 선택한다.
3. 기록한 SHA-256과 APK를 대조한 다음 해당 장치에만 설치한다. `-r`은 데이터 보존 업데이트를 시도한다. 서명 불일치·판본 역행 오류가 나면 자동 삭제·다운그레이드를 하지 않고 사용자에게 확인한다.
4. 기존 food-observer 전용 호스트와 DB·계정·실행 모드가 올바른지 별도로 검증한 뒤 USB 포트를 연결한다. 임의 서버 시작·초기화·주문 생성은 이 설치 절차에 포함하지 않는다.
5. 앱을 휴대폰에서 열어 로그인→메뉴 목록→등록→수정→재조회, 백그라운드 복귀를 확인한다. 주문·알림·결제·정산 종단은 이 검증과 구분한다.

```powershell
$taskAdb = 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
& $taskAdb devices -l

# 실제 목록에서 확인한 본인 장치 식별자로 교체한 후 실행한다.
$taskSerial = '<확인한 장치 식별자>'
$taskApk = Join-Path (Get-Location) 'artifacts/local/mobile-field-test/restaurant-usb-20260928/RestaurantDeskApp-usb-debug.apk'
Get-FileHash -LiteralPath $taskApk -Algorithm SHA256
& $taskAdb -s $taskSerial install -r $taskApk

# 설치 성공 및 전용 서버 확인 후에만 실행한다.
& $taskAdb -s $taskSerial reverse tcp:5321 tcp:5321

# 시험 종료 후 이 포트 연결만 해제한다. 앱/앱 데이터는 지우지 않는다.
& $taskAdb -s $taskSerial reverse --remove tcp:5321
```

## 결과와 남은 검증

- 2026-09-28 현재 작업 트리에서 빌드 성공: 경고 0개·오류 0개, 2분 43초. HEAD는 `ca3b6e65d742bc5c00ebfb7f316ecca706061dc9`이고 기존 미커밋 음식점 변경을 포함하므로 HEAD만으로 이 APK 소스 전체를 재현할 수 있다고 주장하지 않는다.
- 준비 APK: 127,650,508바이트(약 128MB). SHA-256: `14349A0BC05500BA246B3004A6D88980D48E4C0DED43715D7F877F79DF7959DA`. 원본 Signed APK와 전달 사본 hash가 일치한다.
- `apksigner verify`: Android Debug 인증서·v2/v3 서명 검증 통과. `zipalign -c -P 16 4` 통과. 전용 배포 키나 스토어 제출용 서명이 아니다.
- 패키지 판독: 앱 ID·판본·시험 표시명 일치, 최소 API 24/목표 API 36, `arm64-v8a`·`x86_64`. 32비트 전용 휴대폰은 이 파일 대상이 아니므로 연결 후 CPU 호환성을 확인한다.
- 두 CPU용 패키지 내부에 앱 DLL과 .NET 런타임이 포함되어 있고, 각 앱 DLL 안의 서버 주소가 `http://127.0.0.1:5321/`인지 확인했다. Fast Deployment용 외부 어셈블리 설치를 요구하지 않도록 포장했다. APK 항목에서 `appsettings.Local.json`·`.env`·keystore·PEM/PFX 파일이 발견되지 않았으며 이것만으로 포괄적 보안 감사를 완료한 것은 아니다.
- 로컬 `build.log`·`package-manifest.json`·`README.md`를 APK와 같은 폴더에 둔다. 바이너리·빌드 산출물은 Git 대상이 아니다.
- 현재 `adb devices -l`의 연결 장치는 0대다. 설치·앱 실행·화면·로그인·업무 저장은 미검증이다.
- 준비 시점 PC의 5321 포트는 열려 있지 않다. 이번에는 서버/DB를 시작·변경하지 않았다.
- 기존 HTTPS 주소·전용 키가 필요한 Release/스토어 배포, r12 전체 완료, 실제 결제·지급은 이번 범위 밖이다.
- 기기 연결 전 추가 기획 질문은 없다. 연결 후 환경·호환 문제만 실제 결과를 바탕으로 확인한다.
