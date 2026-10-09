# 음식 기사 지도 교통정보 표시 끄기

사용자가 대로변을 따라 나타나는 주황색·초록색 선을 숨기도록 요청했다. 음식 기사 지도에서 별도로 켜던 실시간 교통정보 레이어를 끈다. 지도·현재 위치·픽업 주황색/전달 파란색/대기 회색 업무 경로와 핀은 기존 설정을 사용한다.

## 변경 범위

- `FDriverApp/Pages/MainPage.xaml`의 `FoodDeliveryMap.ShowTrafficLayer`를 `False`로 지정한다.
- `FDriverApp/Controls/FDriverNativeMapView.cs`의 같은 속성 기본값을 `false`로 변경한다.

Android handler의 `SetLayerGroupEnabled(NaverMap.LayerGroupTraffic, ...)` 배선은 유지한다. 업무 경로의 `PathOverlay`와 매장/전달지 `Marker`는 별도이므로 색이나 경로 계산을 변경하지 않는다. 기존 위치 복구 보완도 보존한다. 화면의 카드·상태별 주 행동·글자 크기는 바꾸지 않으며 [페이지 책임](../ProjectOverview/page-docs/food-mobile-four-role-r27.md)과 [역할 앱 시각 기준](../Architecture/RoleAppVisualDesignStandard.md)을 따른다.

근거: [네이버 Android SDK 지도 옵션 공식 안내](https://navermaps.github.io/android-map-sdk/guide-ko/2-3.html)의 교통정보 레이어와 로컬 SDK handler를 대조했다. 콘솔 앱·패키지·키·API 한도와 SDK 판본은 기존 값을 사용한다.

## 검증과 휴대폰 적용

직접 영향 기사 프로젝트 Fast 빌드가 통과했다(`artifacts/local/validation/20261008-010245/`, 오류 0·기존 경고 12). 전체 Task도 `Ssalddel.v3.5.slnx` 빌드 오류 0·기존 경고 55, **8,589개 통과·실패 0·Mongo 환경 조건 2개 건너뜀**이다(`artifacts/local/validation/20261008-010349/`). 지정한 두 소스 파일은 검증 전후 지문이 일치한다. 이번 두 설정에 맞춰 구현을 반복하는 새 시험은 추가하지 않았다.

원시 근거는 Git 제외 `artifacts/local/physical-usb-r30-20261008/`에 보관한다. 기존 r29 소스·APK를 보존한 별도 사본에 승인한 두 파일만 적용해 `0.1.30/30` APK를 만들었다. 같은 Debug 서명·USB 5363·기존 지도 ID·양 ABI 포장을 확인했으며 키는 출력하지 않았다. APK SHA-256은 `9bb23d173b8823380698e7cb79d0a1b55612376faad82249aad0df81634affa3`다.

실제 내장 어셈블리의 기본값 `false`와 로드하는 MainPage XAML의 `False`를 arm64-v8a/x86_64 각각 확인했다. 업무 경로/핀 handler·경로 상태·지도 모델은 r29와 지문이 일치한다. `apk/build-result.json`과 `apk/traffic-verification.json`이 근거다. 실제 단말 지도 화면은 잠금 해제 대기 상태이므로 지도 표시 결과는 **간접 확인**이며 소스·내장 설정 확인을 화면 검증으로 바꾸지 않는다.

연결된 실제 Samsung Android 13 휴대폰에 기존 `0.1.29/29`를 덮어 설치했다. 단말의 `0.1.30/30` 판본과 `base.apk` SHA-256이 준비한 APK와 일치함을 별도 읽기로 확인했다. 앱 삭제·자료 초기화는 하지 않았다. 설치 도구는 설치 성공 후 결과 JSON을 저장할 때 PowerShell 자동 변수 충돌이 났으므로 변수명을 바로잡고 **재설치 없이** 단말 판본·바이트를 다시 대조했다. 이 기록 오류는 제품 설치 실패와 구분하며 `install-helper-recording-failure.json`과 `driver-r30-installed-bytes.json`에 보존한다.

이 기록을 작성한 직후에는 휴대폰이 잠금 상태였다. 이후 잠금 해제 후 **r30의 실제 지도 타일과 도로 교통정보 선 미표시, USB 연결 복구 후 서버 통신**을 확인했다. GPS 수신 안정성은 별도 진단 대상이며 [실제 휴대폰 후속 결과](2026-10-08-physical-phone-runtime-r31.md)를 따른다. 일반 원장 서버 보완은 현재 실행 서버에 적용하지 않았다.
