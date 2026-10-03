# DriverApp-P05 - 운송/배달 이력 조회

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [전체 route](../../current-pages.md) / [이번 보완 결과](../../implementation-r1.md)

## 기존 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P05.png" alt="DriverApp-P05 이전 화면 캡처" width="720">

이전 캡처를 보존했다. 2026-10-02 소스 검토·수정 결과를 새 캡처로 확인한 것은 아니다.

## 1. 페이지: 인증 기사의 화물 기록과 원장 운임

| 항목 | 현재 연결 |
| --- | --- |
| route / 소스 | `/driver/transports/history` / [배달내역Page](../../../../../DriverApp/Components/Pages/Driver/03_Progress/배달내역Page.razor) |
| 사용자/단계 | 기사 / 조회 데이터 페이지. 메뉴·추천 운송으로 이동 |
| 상태 | 로그인 필요, 로딩, 오류+재시도, 기록 없음, 기록 목록 구별 |
| 표시 | 수정 시각 역순의 진행·완료 운송, 출발/도착지·상태·원장 운임. 운임 null은 미확인 |
| 권한/효과 | 읽기 전용. 상하차 Command/지급 확정 없음. token에서 기사 ID 결정 |

이 route의 실제 API는 **용달 화물**만 반환한다. 음식 배달 완료 이력까지 포함하는 통합 기록 페이지는 아직 아니다. 거리 필드가 없는 현재 계약에서 0km를 만들어 표시하지 않으며 `거리 자료 없음`으로 표시한다. 원장 운임은 공제 후 실수령액이 아니다.

## 2. 코드: 전용 목록 API로 완료 기록까지 조회

| 단계 | 코드와 책임 |
| --- | --- |
| 페이지 인증 | [IAuthSession](../../../../../DriverApp/Services/IAuthSession.cs) RestoreAsync 후 인증 여부 확인, 미인증 시 기존 LoginFor 복귀 경로 |
| Client | [DriverTransportApiService](../../../../../DriverApp/Services/DriverTransportApiService.cs) `목록조회Async` → GET `api/v1/driver/transports` |
| 인증 전송 | [DriverApiClient](../../../../../DriverApp/Services/DriverApiClient.cs): token 갱신·HTTP 오류 전달 |
| Controller | [기사운송진행Controller](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs): 기사 역할, V2.0 운송, 현재기사Id → `운송목록조회Query` |
| Handler | [운송목록조회QueryHandler](../../../../../Ssalddel/Application/Driver/Transport/Handlers/운송목록조회QueryHandler.cs): 본인 용달 운송 전체 조회·필요한 증빙 조건 연결 |
| 계약 | [기사운송Dtos](../../../../../Ssalddel.Contracts/Driver/Transport/기사운송Dtos.cs): 운임 nullable, 거리 없음, 운송번호·시각·상태 |

기존 `IDriverSampleDataService`는 추천/예약/정산/운행 전체를 함께 갱신하고 작업공간의 **활성운송목록**만 옮겼다. 완료된 `인수완료`가 이 화면에서 사라지고 관련 없는 API 오류에도 기록 조회가 실패했다. 이제 기존 전용 목록 API를 직접 호출하므로 완료 기록과 독립적인 실패/재조회 경계를 유지한다. 다른 화면의 Sample 인터페이스·캐시는 변경하지 않았다.

## 3. DB·원장: 기사와 업무 유형으로 분리

| 자료 | 키/관계 | 이 화면의 사용 |
| --- | --- | --- |
| [운송실행투영 Configuration](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Transport/운송원장Configuration.cs) | Id PK, 의뢰Id unique, 기사_운송자=`기사_운송자`, 배차업무유형=`business_type` | 기사_운송자=token 기사 AND 업무 유형=용달운송. 상태로 완료 건을 제외하지 않음, AsNoTracking |
| 화주운송의뢰 | 운송번호 ↔ 의뢰Id 논리 연결 | 증빙/결제수단/수령자 조건 조합. 이 화면은 연락처나 서명을 표시하지 않음 |
| 운임·출발_픽업·도착·UpdatedAt | 투영에 저장한 값 | 미확인 값과 실제 0원 구별. 지도 거리·소득세·보험료 역산 없음 |

목록 GET은 영속 상태를 수정하거나 Event/Outbox를 만들지 않는다. 운송 상태 변경의 저장·Mongo 원장 동기화는 기존 Command 소유이며 수동 새로고침/재진입은 서버를 다시 읽는다. 고객 주소는 인증 기사의 관련 운송 범위에서만 노출된다. Web 역할 host의 [별도 이력 페이지](../../../../../Ssalddel.WebApp/Pages/DriverTransportHistoryPage.razor)는 과거 상태만 분류하며 이 MAUI 파일과 다른 소스다.

## 결손과 검증

완료 이력 누락·관련 없는 조회 의존성·미확인 운임/거리의 0 대체 표시를 보완했다. SQLite 저장→추적 해제→본인 전체 목록 조회에서 .NET/EF의 배열 Contains 식 평가 오류도 재현하여 명시적 Enumerable.Contains로 수정했다. 완료/활성 포함과 다른 기사/음식배달 제외를 다시 확인한다. API/DB 계약에 음식 이력이나 거리 필드를 새로 만들지는 않았다. 실제 기기·제품 로그인·MySQL은 미검증이며 [결과 문서](../../implementation-r1.md)를 따른다.
