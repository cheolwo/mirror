# 다중 정차 지도의 실제 휴대폰 확인 r22

상태: Approved / ReadyToDispatch. 2026-10-27 사용자가 r21 완료 보고 뒤 `휴대폰 화면으로 검증해봐`라고 직접 요청했다. 같은 checkout의 실행 수용이며 [지도 구현 r21](cargo-ordered-map.r21.md)의 구현/회귀와 실제 기기 화면을 구분해 추가 확인한다.

## 실행 범위

- 기준 HEAD `8a97384604e6c68a1b00fc71a612d699a3651825`, 미커밋 r20/r21·원본·비공개 자료를 보존한다. r21의 소스 지문과 실제 APK 입력을 대조하고 이전 APK를 최신 검증으로 승계하지 않는다.
- USB SM-A346N과 기존 검토 패키지 `kr.hongdal.driver.cargoreview`만 대상으로 한다. 기존 일반 기사 앱/다른 앱 데이터·기기 잠금/보안 설정을 변경하지 않는다. 최신 검토 APK134를 같은 서명으로 갱신하고 APK·기기 설치본 SHA·버전·실제 DLL/endpoint를 확인한다.
- 합성 자료를 제공하는 로컬 loopback5384 HTTP fixture를 사용한다. 실제 로그인 화면을 통해 검토 전용 일시 계정으로 접속하며 실제 계정/토큰을 재사용하지 않는다. fixture는 영구 DB·외부 배차/알림·금융·운영 효과를 만들지 않고 허용하지 않은 업무 POST는 거절한다. 제어 API는 loopback·비공개 일시 키로 제한하고 요청 로그에서 인증/연락처/원문을 제외한다.
- 2건 B 우선·상차 뒤 하차 전환·B 완료 뒤 A·3/4건 교차 순서·접기/펼치기·앱 재실행·보류/좌표 공백을 실제 native 화면과 순서대로 대조한다. 현재/다음 운송 정보·주황/파랑/회색 핀·번호·마감·실제 지도 SDK 준비/오류를 관찰한다. 화면 부족이나 표시 결함이 있으면 가장 좁은 기존 소스만 보완하고 같은 판본을 다시 빌드/검증한다.
- fixture의 상태 전환은 명시적 시험 조작이며 실제 기사 상하차·제품 서버 배차/DB·실 GPS/실도로·최단 도로 geometry 검증이 아니다. 기기 지도의 배경 타일/핀 렌더, 합성 데이터 표시, 실제 운영 판단을 각각 기록한다.
- 실제 계정·고객 화면은 공유 캡처하지 않는다. 검토 앱 foreground를 확인한 캡처만 비공개로 저장하며 합성 자료뿐인 대표 화면만 문서에 첨부한다. 시스템 재난 안내/권한·기기 보안이 막으면 우회하지 않고 필요한 사용자 조치만 요청한다.
- 종료 때 이번 소유 fixture·USB reverse만 정리하고 검토 앱 프로세스를 종료한다. 앱 데이터·기존 APK·DB·이전 실행·다른 서비스는 보존한다. commit/push·공개 배포·MYBOX·무인 자동화는 포함하지 않는다.

## 소유권

- 실행 스크립트·fixture·입력 SHA·로그·비공개 캡처: `artifacts/local/cargo-ordered-map-device-r22/`. 빌드 캐시는 새 `artifacts/local/u/` 하위에 생성한다.
- 루트가 USB·휴대폰 입력/설치·빌드·프로세스 실행을 독점한다. fixture 담당은 지정 artifact 코드만 작성하고 실행하지 않는다.
- 결과 문서: `docs/Changes/2026-10-27-cargo-ordered-map-device-r22.md`, `docs/assets/changes/2026-10-27-cargo-ordered-map-device-r22/`, `docs/AI/{CURRENT_WORK,PLANNING}.md`, `docs/Changes/README.md`. r21 과거 미검증 기록을 지우지 않고 새 결과를 링크한다.
- 제품 결함의 수정 경로는 발견한 구체 증거와 함께 같은 기존 지도/페이지 책임 파일로 먼저 한정하고 r21 범위의 Fast/Task·APK 결과를 다시 기록한다. 현재는 제품 변경 없이 실행 검증만 수행한다.

## 실제 화면에서 발견한 가림의 좁은 보완

APK134의 실제 SM-A346N 화면에서 기본 방문 순서 설명과 기존 운송 요약이 중앙 핀을 가리는 것을 확인했다. 캡처 SHA `a6820b530c683fe4e048bb02637f0a1e583eb93e0a42026853a5e388f079d5bd`는 초기 판본 근거로 보존한다. 사용자의 화면 검증과 앞선 지도 보완 요청 안에서 다음 표시 변경만 수용한다.

- 기본 방문 순서 패널을 한 줄 제목·접기 버튼으로 줄이고 상세 설명/색상 범례/마감 목록은 펼침 상태에서 제공한다. 실제 도로 경로가 아니라는 문구와 위치 공백/보류 경고는 기본에서도 보존한다.
- 운행 지도에서만 운송 요약의 반복 주소/운임 줄을 펼침으로 옮긴다. 현재 운송 식별·다음 행동·상세/목록 버튼을 유지하고 다른 호출 화면의 기본 밀도는 바꾸지 않는다.
- 루트 추가 writePaths: `DriverApp/NativeDriverHomePage.xaml`, `DriverApp/NativeDriverHomePage.xaml.cs`, `DriverApp/Controls/DriverTransportFooterBar.xaml.cs`, `DriverApp/Services/DriverHomeRoutePlanningService.cs`, `Ssalddel.Tests/Clients/CargoOrderedMapPresentationR21Tests.cs`. 좌표 공백의 명시적 표시 flag만 추가하며 업무·순서·권한·서버 계약은 바꾸지 않는다.
- 수정 전 사본과 r21 입력 차이를 보존하고 같은 소스를 다시 Fast/Task 검증한 뒤 APK135로 새 빌드·서명·실제 DLL/설치 해시·화면을 확인한다. APK134의 결과를 새 화면 검증으로 승계하지 않는다.
