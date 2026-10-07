# 기사 앱 Google 지도 적용 조사

2026-10-04 공식 문서 확인. 사용자 요청은 네이버에 한정하지 않고 Google 지도를 대안으로 조사하는 것이다. 결론은 **지도와 핀 표시는 대안이지만, 국내 배달 경로·거리 계산까지 Google로 전환하는 것은 현행 지원 근거가 부족하다**는 것이다. 조사만 했으며 새 SDK 설치·키 발급·계정 생성·유료 호출·지도 전환은 하지 않았다.

## 지도 표시와 주행 경로

| 필요 | 확인 결과 | 앱에 미치는 영향 |
| --- | --- | --- |
| 국내 지도·위치 핀 | Google 공식 한국 행은 지도와 지오코딩 지원으로 표시됨 | 지도 표면·자체 픽업/전달 좌표 핀의 후보로 사용 가능 |
| 국내 자동차 경로·거리 | 한국 Driving Directions / Snap to Roads는 `—` | 표 정의상 미지원 또는 낮은 데이터 품질/가용성. 배달료의 주행 거리 근거로 채택하지 않음 |
| 국내 오토바이 경로 | Routes TWO_WHEELER 지원 국가에 대한민국 없음 | 자동차 응답을 오토바이 경로라고 표시하거나 요금 근거로 사용하지 않음 |
| 상태별 색상 선·핀 | MAUI Map은 핀·Polyline 지원 | 기존 주황 픽업/파랑 전달/회색 다른 건 표현을 구현할 기술 수단은 있음. 실제 도로선 데이터 지원과는 별개 |
| 외부 Google 지도 열기 | Maps URLs는 무료·API키 불필요 | 앱/브라우저로 위치 링크를 열 수 있음. 앱으로 계산 거리·Polyline을 반환하는 엔진은 아님 |

[Google 국가별 커버리지](https://developers.google.com/maps/coverage)는 Google Maps Platform API/SDK 표이며 소비자 Google Maps의 모든 기능을 설명하는 표가 아니다. [오토바이 지원국](https://developers.google.com/maps/documentation/routes/coverage-two-wheeled)과 [TWO_WHEELER 제약](https://developers.google.com/maps/documentation/routes/route_two_wheel)을 별도로 확인했다. 자동차/오토바이 지원을 지도 표시 가능성으로 추정하지 않는다.

## 비용·키·구현

| 방식 | 확인한 비용 조건 | 필요한 설정 |
| --- | --- | --- |
| 기본 Android/iOS 네이티브 Maps SDK, map ID 없음 | Maps SDK SKU 무제한 무료 | 결제 연결 Cloud 프로젝트·SDK 활성화·API키는 필요 |
| 네이티브 map ID 사용 또는 JavaScript 지도 | Dynamic Maps 월10,000회 무료, 이후 첫 구간 1,000회당 USD7 | 무료 네이티브 지도 표시와 같은 요금이라고 가정하지 않음 |
| Compute Routes Essentials | 월10,000회 무료, 이후 첫 구간 1,000회당 USD5 | 경로 조회는 별도 과금. 옵션에 따라 Pro/Enterprise로 달라지며 오토바이에 이 단가를 그대로 적용하지 않음 |
| Maps URLs | 무료·API키 불필요 | `api=1` 위치/길찾기 링크. 앱 미설치 시 브라우저. 국내 API 지원을 추가하는 우회책은 아님 |

가격은 확인일의 글로벌 공식 USD 구간이며 무료 이용량·map ID·사용 옵션에 따라 달라진다. [가격표](https://developers.google.com/maps/billing-and-pricing/pricing), [SKU 조건](https://developers.google.com/maps/billing-and-pricing/sku-details), [Android 사용·결제](https://developers.google.com/maps/documentation/android-sdk/usage-and-billing), [iOS 사용·결제](https://developers.google.com/maps/documentation/ios-sdk/usage-and-billing), [Maps URLs](https://developers.google.com/maps/architecture/maps-url)를 근거로 한다.

[Microsoft MAUI Map](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/map?view=net-maui-10.0)은 Android에 Google Maps SDK, iOS/Mac Catalyst에 Apple MapKit을 사용한다. Android에서는 기존 지도 상태 바인딩과 카메라 의도를 보존해 Google 구현을 추가할 수 있지만 현재의 사용자 선택/따라가기/준비 상태/오류와 플랫폼별 빌드 호환성은 따로 검증해야 한다. 기본 MAUI Map을 iOS에 적용했다고 Google 지도로 통일됐다고 표현하지 않는다.

## 공급자 결합과 현재 코드

Google Routes 내용을 지도에 표시한다면 Google 지도를 사용해야 하며 Google이 아닌 지도와 결합하는 것은 제한된다. [서비스 약관19.1·19.2](https://cloud.google.com/maps-platform/terms/maps-service-terms)와 [Routes 표시 정책](https://developers.google.com/maps/documentation/routes/policies)을 확인했다. 이 조건은 네이버 경로를 Google 지도에 표시해도 된다는 허가가 아니다. 다른 공급자의 경로·주소·지도 데이터를 혼합하기 전에는 해당 공급자의 이용 조건을 별도로 확인한다.

현재 `FDriverApp/Controls/FDriverNativeMapView.cs`는 지도 표시 계약을, `FDriverApp/Handlers/FDriverNativeMapViewHandler.Android.cs`는 네이버 SDK 표시를 소유한다. `FDriverApp/PageModels/MainPageModel.FoodMap.cs`는 기존 `GetRouteAsync` 서버 호출과 반환 경로/따라가기/재조회 상태를 사용한다. 표시 공급자 교체만으로 서버 경로 공급자나 배달료 기준이 바뀌지 않도록 이 경계를 유지한다.

현재 판단은 **Google 지도 표시 후보를 보존하고 국내 자동차/오토바이 경로 공급자와 결합 허용을 먼저 확인**하는 것이다. 이를 확인한 뒤 좁은 구현 범위로 SDK·지도 준비/오류·핀/색상 선·카메라·기존 수행 행동을 검증한다. SDK 미설정 안내를 해결하기 위해 무조건 Google로 바꾸면 된다는 결론이나 현재 실제 지도/경로 실행 완료 주장으로 확대하지 않는다.
