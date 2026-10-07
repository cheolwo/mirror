# 화물 의뢰 원값·완료 재시도·운송 내역 보완 r1

사용자가 승인한 역할 앱 마감 항목 중 화물 세 항목을 구현한다. 기존 route·권한·운임·상태 전이·DB schema를 유지하며 지도와 인증 설정은 범위에서 제외한다.

## 구현 전 책임과 범위

| 대상 | 한 가지 업무 질문·결과 | 상태·실패·복귀 | 구현 범위 |
| --- | --- | --- | --- |
| 기존 MAUI 화주 의뢰 수정 | 같은 의뢰의 연락처 또는 화물종류를 수정하면서 다른 화물 원값을 보존하는가 | 조회 원값의 독립 사본, 제출 실패 후 초안 유지, 구판 응답에서 미확인 화물 원값을 만들지 않음 | 기존 nullable 응답 확장·mapper·앱 model/adapter/Crud 사본. 연락처만 바꾸면 화물 수정 DTO를 보내지 않음 |
| 기사 상차·하차 완료 | 같은 완료 사진을 다시 전송해도 같은 원장 증빙 하나를 유지하는가 | 기존 서버 권한·상태 검증 유지, 같은 단계/ObjectName 재시도만 중복 방지 | 기존 증빙 writer와 회귀시험. 사진 업로드 재사용·재고 차감·후속 전달 체계는 별도 책임 |
| 기사 운송 내역 | 현재 기사 계정의 기록을 조회하고 국내 시각으로 확인하는가 | loading/login/empty/error/retry, 이탈·계정 변경 후 결과 폐기, 최신 조회만 적용 | 기존 내역 route와 전용 ViewModel. 기존 예약·운송 예정 시각의 한국시간 표시 정리 |

내역은 조회만 수행하며 상하차 명령·새 정산 기능·개인정보 보존기간 정책을 추가하지 않는다. 원장 운임과 기사 실수령액은 구분한다. 공용 음식 UI와 FDriver 파일은 변경하지 않는다.

## 검증 상태

편집 전 사본·해시는 Git 제외 `artifacts/local/role-app-polish-r1/cargo/source-before.json`과 `before/`에 보존했다. 범위 내 `git diff --check`는 통과했다. 회귀시험 작성은 완료했고 실행은 root의 단일 통합 검증을 기다린다. 이번 판본의 실제 HTTP/DB·앱 렌더·APK·기기 실행은 아직 검증하지 않았다.

## 구현 내용과 경계

- 화주 응답에 기존 필드를 유지하면서 nullable `화물` 원값 사본을 추가했다. 서버 mapper는 종류·설명·수량·가로/세로/높이·중량·부피·팔레트·파손주의·온도를 반환한다. 앱 조회와 수정 초안은 각각 독립 사본을 가지며 연락처만 바꾸면 화물 DTO를 생략한다. 종류를 실제 바꾸면 원값을 복사하여 종류만 바꾼다. 구판 서버 응답의 화물 원값이 없을 때 연락처는 수정 가능하지만 종류 수정은 재조회 안내로 차단한다. 생성 기본값은 변경하지 않는다.
- 완료 사진 writer는 `pickup-complete-photo` 또는 `dropoff-complete-photo`의 같은 ObjectName 재전송만 건너뛴다. 최초 증빙 시각·URL·메타데이터를 유지하며 다른 사진·다른 완료 단계·예외 신고는 별도 기록한다. 인증·상태 전이·재고·후속 발행은 기존 executor 책임이다. 이는 순차 재시도 증빙 중복 방지이며 동시 서버 요청 전체의 exactly-once 처리를 뜻하지 않는다.
- 내역 route는 전용 VM의 계정 revision·페이지 취소·조회 generation으로 가장 최근 현재 계정 응답만 적용한다. 로그아웃/계정 변경은 즉시 기록을 지우며 같은 계정 토큰 갱신은 기록을 유지한다. 화면 이탈은 취소와 구독 해제를 수행한다. 운송 내역·예약은 한국시간과 날짜를 표시하고 기존 현재 운송의 예정 시각 사본도 UTC에서 한국시간으로 변환한다. 서버 시각·운임 원값은 보존한다.

## 소스와 회귀 범위

| 연결 | 제품 경로 | 회귀 |
| --- | --- | --- |
| 조회 원값 → 수정 초안 → PUT | `Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs`, `Ssalddel/Application/Shipper/Request/화주운송의뢰매퍼.cs`, `SsalddelApp/Models/Shipper/ShipperRequestItem.cs`, `SsalddelApp/Services/ServerBackedShipperOperationsService.cs`, `SsalddelApp/ViewModels/Shipper/화주운송의뢰CrudViewModels.cs` | `ShipperUpdatePricingPreservationTests`, `화주운송의뢰조회QueryHandlerTests`: 연락처만 수정, 실제 종류 수정, 파손주의 true/false, nullable/0 원값, 구판 응답, 기존 생성·정산 조건 |
| 완료 재시도 → 증빙 원장 | `Ssalddel/Application/Driver/Transport/Services/운송증빙첨부JsonWriter.cs` | `운송증빙첨부JsonWriterTests`, `기사운송원장상호작용Tests`: 최초 증빙 보존, 다른 사진/단계/예외 신고, 실제 상·하차 handler/executor의 동일 사진 2회 처리 |
| 내역 route → 조회 수명, 국내 시각 | `DriverApp/Components/Pages/Driver/03_Progress/배달내역Page.razor`, `DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor`, `DriverApp/Services/기사운송표시Mapper.cs`, `DriverApp/Services/DriverServiceCollectionExtensions.cs`, `DriverApp/ViewModels/Driver/Transport/기사운송내역PageViewModel.cs`, `DriverApp/ViewModels/Driver/Transport/기사국내시각표시.cs` | `CargoTransportHistoryLifetimeTests`, `CargoTransportKstDisplayTests`: 늦은 성공/실패 응답, 조회 경합, 이탈/로그아웃/계정 전환, 토큰 갱신, 재시도, KST 날짜 경계 및 SQL Kind 없는 시각 |

화물의 별도 개인정보 만료 정책, 예약 화면 전체의 새 조회 수명, 네이버 지도 인증·표현, 신규 업무 기능·은행 입금·커밋·푸시는 이번 세 항목의 구현에 포함되지 않는다.

최종 관련 시험은 Fast `20261004-145754`의409/409에 포함해 통과했고 전체 역할 앱 빌드도 통과했다. 내역의 이전 inline 소스 기대는 전용 ViewModel의 로그인 복귀·조회·재시도·계정/이탈 정리 계약으로 교체했다. 기존 다른4개 페이지 검사와 실제 내역 수명 시험은 유지한다. Task의 기존7실패·새 실패0 및 화면/기기 실행 범위는 [통합 기록](2026-10-04-role-app-polish-r1.md)에 연결한다.
