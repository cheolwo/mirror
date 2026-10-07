# Google 지도 SDK 연결 점검 r13

사용자는 다운로드한 Google 지도 SDK의 앱 적용을 요청했다. 통합 `SsalddelApp`의 SDK 의존성과 Android 네이티브 표시 연결은 이미 존재한다. SDK 파일을 중복 설치하거나 다운로드 폴더의 과거 브라우저 키를 Android 키로 임의 재사용하지 않는다.

## 확인한 연결

`Microsoft.Maui.Controls.Maps` 10.0.20과 `Xamarin.GooglePlayServices.Maps` 119.2.0.3이 복원돼 있다. `NeighborhoodNativeMapViewHandler.Android.cs`의 Google `MapView`와 `MauiNeighborhoodMapHost`가 공통 역할 작업공간을 표시한다. 픽업/전달 핀과 선 색상·마커 선택·카메라·타일 로딩 확인·화면 이탈 정리가 구현돼 있다. 지도 표시 SDK와 서버 경로 계산은 별개이며 기존 국내 `NaverDirections5` 경로를 Google 경로로 승격하지 않는다.

현재 생성 APK/manifest의 지도 키는 미설정 placeholder이고 해당 환경 변수도 없다. 다운로드 폴더에서 확인한 `GoogleMaps BrowserApiKey.txt`는 과거 파일이며 Android용 제한·API 활성화 근거가 없어 사용하지 않았다. 비공개 키 원문은 출력·추적 파일에 기록하지 않는다. [Google 공식 설정](https://developers.google.com/maps/documentation/android-sdk/get-api-key)은 Cloud 프로젝트의 계정/결제 전제·SDK 활성화·API 키 구성을 각각 요구한다. 계정 상태는 다운로드 파일의 존재만으로 확정하지 않는다.

## 위치 접근 보완

통합 앱의 Android manifest에는 coarse/fine 위치 권한이 없지만 역할 위치 공급자는 GPS를 요청하고 기사 행동은 해당 위치를 필요로 한다. 앱 사용 중 위치 권한과 선택적 GPS/network 기능을 선언하고, 사용자가 현재 위치가 필요한 행동을 할 때 UI 스레드에서 권한 확인/요청하도록 보완한다. 거절·위치 꺼짐·미지원은 기존 위치 미확인 안내로 복구하며 취소와 모의 위치 제외를 유지한다. [MAUI 공식 위치 설정](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation?view=net-maui-10.0&tabs=android)을 근거로 한다.

지도 키의 기존 빌드 입력은 `GoogleMapsAndroidApiKey`다. 앱 ID는 `com.companyname.ssalddelapp`, 현재 Debug APK 서명 SHA-1은 `B1:57:28:63:97:08:1D:7C:40:66:A4:F5:98:45:73:B1:A7:FE:AC:46`이다. Android용 키 제한은 이 두 값과 Maps SDK for Android를 기준으로 확인해야 한다. Debug 인증서와 실제 출시 인증서를 혼동하지 않는다. 원시 근거는 `artifacts/local/google-map-sdk-connection-r13/`다.

## 검증 결과와 남은 입력

- Fast `20261005-190402`: 통합 앱 빌드 오류 0·기존 경고 10. 플랫폼 코드에 대한 별도 단위 시험은 추가하지 않았다.
- Task `20261005-190557`: 규정된 `Ssalddel.v0.0.slnx` slice 빌드 오류/경고 0, 전체 시험 **7,447/7,447** 통과.
- 실제 생성 Android manifest에서 앱 ID·coarse/fine 권한·optional gps/network·background 권한 없음과 지도 키 미설정을 확인했다. `manifest-verification.json`에 결과와 SHA-256을 결속했다. 비공개 키 값은 기록하지 않았다.
- 변경한 위치 공급자와 권한 선언을 빌드 검증했지만 실제 기기의 권한 허용/거절 대화상자·GPS·지도 타일은 미검증이다. Maps SDK 다운로드·복원·컴파일을 타일 표시 성공으로 확대하지 않는다.

상태는 `LocationAccessImplemented / ValidatedBuildAndTests / AndroidMapKeyPending / MapRuntimePending`이다. 다운로드한 항목의 종류와 Android용 키 설정은 사용자 확인을 기다린다. 기존 지도 키 빌드 입력을 유지했고 키를 설정한 새 내장 APK 및 실제 지도 타일 검증은 진행하지 않았다. Google Cloud 설정·결제 연결·키 발급·API 호출·개인 휴대폰 설치도 수행하지 않았다. 기존 r12와 관련 없는 dirty 작업을 보존했다.
