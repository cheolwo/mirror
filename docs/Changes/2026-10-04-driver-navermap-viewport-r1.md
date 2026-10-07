# 기사 네이버 지도 경로 화면 맞춤 r1

사용자의 최신 지도 선택은 네이버다. 기존 Android Mobile Dynamic Map SDK를 유지하고, 현위치 한 점만 중심에 놓아 현재 건의 핀이 화면 밖으로 나가는 문제를 보완한다. [구현 전 책임 카드](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#네이버-지도-경로-화면-맞춤-r4--구현-전-책임-카드)를 따른다.

## 변경

- 첫 유효 현재 경로와 선택 배달·픽업/전달 단계 전환에서 해당 경로·픽업 핀·전달 핀·유효 현위치의 범위를 실제 지도 영역에 한 번 맞춘다. 다른 활성 배달의 회색 핀·참고선은 유지하며, 먼 다른 건을 화면 맞춤 범위에 넣어 현재 경로를 축소하지 않는다.
- 같은 배달·단계의 GPS나 경로 재조회는 화면 맞춤을 반복하지 않는다. 사용자가 움직인 중심과 줌은 View가 보관하여 native handler 재생성 때 복원한다. 현위치 버튼의 줌 유지·따라가기 재개는 기존 동작을 유지한다.
- SDK 로고를 작은 지도에서도 표시하고, 범례·현위치 버튼의 상하 여백을 화면 맞춤에 반영한다. 픽업 주황 `#F57C00`, 전달 파랑 `#2563EB`, 다른 건 회색 `#64748B`의 핀·실선은 보존한다.
- Debug 인증 진단은 허용한 오류 코드만 기록한다. 키·좌표·SDK 예외 원문은 기록하지 않는다. 지도 객체 준비와 타일 준비를 구분하는 기존 실패 안내를 유지한다.

제품 경로는 `FDriverNativeMapView`, `FDriverMapCameraState`, Android handler 세 개이며 카메라 상태 시험을 확장했다. 서버 API/DB·배차·배달료·단일 앱 GPS 소유는 기존 처리를 재사용한다.

## SDK와 영상 지도 구분

영상의 Static Maps는 HTTP 지도 이미지이고 기사 앱의 SDK는 이동·확대·현재 위치 갱신을 지원하는 지도다. 같은 ID로 서버 경로가 성공했어도 Mobile SDK의 타일 인증 성공을 뜻하지 않는다. [공식 카메라 이동](https://navermaps.github.io/android-map-sdk/guide-ko/3-2.html)의 `fitBounds`와 [마커 색상](https://navermaps.github.io/android-map-sdk/guide-ko/5-2.html)의 BLACK 아이콘+tint 사용법을 대조했다.

콘솔에서 확인할 항목은 해당 Application의 Dynamic Map 선택·Android 패키지 `kr.ssalddel.fdriver`·현재 Maps와 legacy AI·NAVER API의 인증 유형이다. [공식 Application 등록](https://guide.ncloud-docs.com/docs/application-maps-app-vpc)·[SDK 시작 안내](https://navermaps.github.io/android-map-sdk/guide-ko/1.html)를 따른다. 401만으로 ID·인증 유형·패키지 중 원인을 확정하지 않는다. 콘솔 설정·키·결제는 변경하지 않았다.

## 검증

Fast `20261004-134500` 기사/시험 빌드와 카메라24/24가 통과했다. 후속 연결 점검에서 핀이 늦게 도착하면 최초 맞춤을 기다리도록 했고, 맞춤 뒤 줌을 즉시 바인딩하며 이전 handler 수명의 늦은 MapReady/인증 콜백을 폐기한다. 최종 Task `20261004-135108` 전체3.5 빌드·6,448/6,455, 기존7실패가 남아 전체 게이트는 미통과다. 기준 `20261004-130807`과 대조하여 기존 시험 결과 변경0·제거0·신규 카메라11케이스 전부 통과, 실패7개의 이름·메시지·전체 스택 원문 일치를 확인했다.

인증 진단 설치본에서 SDK가 허용 로그 `401`을 반환했다. 예시 주황 픽업선·주황/파랑 핀·회색 표시·현위치가 그려졌지만 실제 배경 타일은 실패 안내와 함께 비어 있다. 핀/선의 표현을 지도 사용 가능·실제 도로 검증으로 보고하지 않는다. 키 ID·인증 유형·패키지 중 원인은 콘솔 확인 전 미확정이다.

최종 Debug APK의 로컬/기기 설치본 SHA-256은 `33BABE9CD1169AB51F6F88BE7E65F20FC571B7D848B98B1D7F6AAE8CFA5C9F29`로 일치한다. 제품/시험4경로의 최종 지문도 유지됐다. 실제 Android 에뮬레이터에서 현재 경로가 픽업 주황 → 전달 파랑으로 바뀌고 해당 핀·현위치가 지도 영역 안에 들어가는 것을 확인했다. 회색 다른 건의 표시는 일부 겹치거나 화면 밖에 있을 수 있으며 모든 건의 전체 화면 포함을 주장하지 않는다.

지도 이동 후 약30초간 반복 GPS 입력·업무 재조회가 있어도 탐색 화면이 유지됐고, SDK 확대 버튼으로 축척500m → 200m 전환을 확인했다. 실제 타일과 준비 상태 `Ready`, 앱의 현위치 버튼, native handler 재생성의 실제 조작은 인증 차단 상태에서 검증하지 못했다. 현위치 복귀·카메라 snapshot·단계별 최초 맞춤은 순수 상태 시험의 별도 근거다. 화면의0.0km/0분은 이 private fixture의 시험값이며 실제 이동 거리·시간이 아니다.

| 최종 설치본 확인 | 화면 |
| --- | --- |
| 픽업 주황 선·현재 핀·GPS, SDK401 안내 | [픽업 화면](../assets/changes/2026-10-04-driver-navermap-viewport-r1/final-pickup-map.png) |
| 전달 파랑 선과 단계 전환 | [전달 화면](../assets/changes/2026-10-04-driver-navermap-viewport-r1/final-dropoff-map.png) |
| 사용자가 이동한 화면 | [이동 직후](../assets/changes/2026-10-04-driver-navermap-viewport-r1/final-dropoff-pan.png) |
| GPS 입력/재조회 후 탐색 유지 | [후속 화면](../assets/changes/2026-10-04-driver-navermap-viewport-r1/final-pan-after-gps.png) |
| SDK 확대 조작 | [확대 화면](../assets/changes/2026-10-04-driver-navermap-viewport-r1/final-native-zoom.png) |

대표 PNG5개의 원시/문서 사본 지문을 대조했다. 전용 fixture·GPS 입력·에뮬레이터와5574 ADB reverse는 PID/시작시각/실행 경로/AVD를 확인 후 정리했다. 공유 ADB 서버는 유지했다. 물리 단말·실제 도로/배차/입금·운영 배포는 이번 검증이 아니며 커밋·푸시는 하지 않았다.

원시 근거는 Git 제외 `artifacts/local/driver-navermap-viewport-r1/`다. 새 native 검토 경로는 직접 만든 예시 형상이며 저장된 네이버 Directions 응답을 재생하거나 새 경로 API를 호출하지 않는다. 예시 선의 표시 시험은 실제 도로·요금·배달 증거와 다르다. 브라우저 도구에 사용 가능한 브라우저가 없어 네이버 콘솔의 기존 등록 상태는 확인하지 못했다. 이전 [내역/수행 수명 검증](2026-10-04-driver-history-lifetime-map-r1.md)은 별도 이력으로 보존한다.
