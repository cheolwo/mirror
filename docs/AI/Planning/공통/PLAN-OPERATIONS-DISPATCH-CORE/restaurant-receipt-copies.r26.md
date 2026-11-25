# 음식점 빌지 기본 두 장과 기기 설정 r26

상태: `Implemented / ValidatedLocalUi / PhysicalPrintPending / OperationalUnchanged`. 2026-10-28 사용자의 직접 요청을 `ApprovedByUser / ReadyToDispatch` 범위로 구현했다. [구현·화면·검증](../../../../Changes/2026-10-28-restaurant-receipt-copies-r26.md)에 실제 결과와 한계를 기록한다.

## 결과와 경계

RestaurantDeskApp의 기존 수락 후 전표/재출력 흐름을 유지한다. 주문 도착만으로 무인 인쇄하거나 주문을 중복 수락하지 않는다. 기기 설정이 없으면2장(조리용·포장용), 명시적으로1장을 저장하면 조리·포장 겸용 한 장을 만든다. 새 설정은1/2만 허용하고 다른 기기나 계정의 서버 설정을 변경하지 않는다.

동일 최신 주문 draft를 한 개의 HTML 인쇄작업에 구성한다. 두 개 인쇄 창을 반복 호출하지 않으며 장수는 출력 명령에서 한 번 고정한다. 공유 문서의 기본은1장으로 보존하고 음식점만2장을 적용한다. 화면의 출력 요청 성공과 프린터의 실제 출력·절단 완료를 구분한다. 부분 인쇄/취소를 자동 감지하거나 자동 재출력하지 않는다. 필요하면 사용자가 실제 장치를 확인한 후 재출력하며 프린터 부수 설정도 확인하도록 안내한다.

기기 출력 설정을 읽지 못해도 사용자가 선택한 주문 수락은 서버에서 처리하고 출력만 보류한다. 임의 장수로 인쇄하거나 자동으로 수락을 재시도하지 않으며, 수락 성공과 출력 보류를 따로 안내한다. 독립 재출력의 설정 조회 실패는 주문을 다시 수락하지 않고 출력만 멈춘다.

## 소유권·허용 범위

root: `Ssalddel.Contracts/Common/Documents/DocumentOutputDtos.cs`, `Ssalddel.Ui.Common/Areas/App/Services/SsalddelDocumentHtmlRenderer.cs`, `RestaurantDeskApp/Components/Pages/OrderDetail.razor.cs`, `RestaurantDeskApp/MauiProgram.cs`, 테스트/Preview csproj와 포트 등록, 관련 회귀·문서·`artifacts/local/restaurant-receipt-copies-r26/`.

설정/문서 담당: 신규 `RestaurantDeskApp/Services/I음식점전표출력설정Service.cs`, `음식점전표출력설정Service.cs`, `Maui음식점전표출력설정Store.cs`, `음식점전표출력구성.cs`, 신규 직접 회귀만 소유한다. UI 담당: 신규 `Components/Pages/ReceiptSettings.razor`, 기존 NavMenu/가게 관리와 OrderDetail.razor의 설정 연결, 페이지 책임 문서·신규 화면 회귀만 소유한다. 같은 기존 파일을 함께 편집하지 않는다. 기존 dirty source와 주문/전표/알림·실제 프린터 설정은 보존한다.

## 검증

기본2/저장1·2/유효성/재조회/저장 오류, 조리/포장 구분과 같은 메뉴·수량·바코드 보존, 단일 HTML·단일 출력 요청, 재출력의 무재수락·선택/계정 전환·실패를 회귀한다. 공유 일반 문서 기본1장을 검증한다. Fast/Task와 Android/Windows 빌드를 수행하며 가능하면 실제 Razor의 좁은 화면을 확인한다. 물리 인쇄·무인 발송·운영 활성화·commit/push는 이 요청에 포함하지 않는다.

최종 Fast `20261028-090606` 관련62통과·실패/생략0, 최종 Task `20261028-090639`의 전체 소비자 빌드와16,640통과/17환경생략·실패0을 확인했다. 실제 제품 Razor의390/1280px 미리보기에서2→1저장·재진입1유지·2저장과 가로 넘침 없음을 확인했다. 기기 저장소는 메모리 대역이며 실제 MAUI Preferences 재실행·물리 인쇄는 미검증이다. 최종 소스20개 hash·HEAD 불변, 기존 dirty81 중 허용된 시험 프로젝트 링크 추가 외80개 보존을 확인했다. 소유 미리보기·탭·viewport를 정리했고 운영 활성화·commit/push는 하지 않았다.
