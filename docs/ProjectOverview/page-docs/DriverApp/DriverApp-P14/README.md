# DriverApp-P14 - 월정산 확인

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [전체 route](../../current-pages.md) / [이번 보완 결과](../../implementation-r1.md)

## 기존 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P14.png" alt="DriverApp-P14 이전 화면 캡처" width="720">

이전 캡처를 보존했다. 2026-10-02 소스 검토·수정 결과를 새 캡처로 확인한 것은 아니다.

## 1. 페이지: 이번 달 플랫폼 이용료 조회

| 항목 | 현재 연결 |
| --- | --- |
| route / 소스 | `/driver/settlements/current-month` / [월정산Page](../../../../../DriverApp/Components/Pages/Driver/05_Settlement/월정산Page.razor) |
| 역할/단계 | 기사 / 이용료 조회 데이터 페이지. [이용료 안내](../DriverApp-P14-1/), [계좌 정보](../DriverApp-P14-2/)는 별도 책임 |
| 표시 | 년·월, 배차건수, 이용료, 월 상한·잔여, 이용료 결제 상태 |
| 로딩/실패 | ViewModel 처리중·초기화됨·오류를 구분. 실패/취소/미초기화 때 기본 0원/0년/결제 안내를 숨기고 재시도 |
| 새로고침 | 현재 월 API 재호출. API 실패 후 이전 값도 현재 확정액으로 표시하지 않음 |

`IsPaid`는 **이용료 결제** 상태다. 기사에게 소득/보험 공제 후 대금이 지급됐다는 의미가 아니다. 이번에는 문구를 `이번 달 이용료 결제가 완료되었습니다`로 명확히 했다. 공제/미션/지사 프로모션·기간별 순수령액을 이 화면에 새로 넣지 않았다.

## 2. 코드: PageViewModel → Feature → Client → Query

| 단계 | 실제 코드 |
| --- | --- |
| PageViewModel | [기사정산PageViewModels](../../../../../DriverApp/ViewModels/Driver/Settlement/기사정산PageViewModels.cs): 조회 결과 없거나 하위 오류이면 실패, [공통 PageViewModelBase](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/PageViewModelBase.cs)가 로딩/오류/재시도 수명 관리 |
| Feature/Client | [기사정산기능ViewModel](../../../../../DriverApp/ViewModels/Driver/Features/기사정산기능ViewModel.cs) → [DriverSettlementApiService](../../../../../DriverApp/Services/DriverSettlementApiService.cs) → 기존 인증 DriverApiClient |
| Controller | [기사정산Controller](../../../../../Ssalddel/Controllers/Driver/06_Settlement/기사정산Controller.cs): 기사 역할, V2.0 Settlement/Browse, token의 기사Id |
| Query | GET `api/v1/driver/settlements/current-month` → [현재월 Handler](../../../../../Ssalddel/Application/Driver/Settlement/Handlers/기사정산현재월조회QueryHandler.cs) → [공통 매퍼](../../../../../Ssalddel/Application/Driver/Settlement/Handlers/기사정산공통매퍼.cs) |

이 페이지는 SampleDataService나 음식 배달용 `api/v1/drivers/{driverId}/monthly-settlements/current`를 직접 호출하지 않는다. 기존 문서에 Client 전체 API를 펼치던 설명을 실제 호출 한 개로 좁혔다. 목록/월별 조회는 같은 Client의 별도 기능이며 현재 페이지 실행 증거로 합산하지 않는다.

## 3. DB·원장: 이용료 월별 행과 소득 정산 경계

| 자료 | 키·열/관계 | 읽기·쓰기 |
| --- | --- | --- |
| [기사월정산 Configuration](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Driver/기사월정산Configuration.cs) | 테이블 기사월정산, id PK; `driver_id`, `year`, `month`, `dispatch_count`, `usage_fee`, `is_paid`, 시각 | 조회는 기사Id·UTC 현재 년/월. 없으면 해당 월 0건/0원·미결제 DTO를 반환하며 DB에 새 행을 저장하지 않음 |
| [기사월정산Service](../../../../../Ssalddel/Services/Settlement/기사월정산Service.cs) | 기사/년/월 논리 식별. [기사이용료정책Options](../../../../../Ssalddel/Services/Options/기사이용료정책Options.cs)의 현행 계산·월 상한 | 배차확정반영 때 건수 증가/이용료 저장. 월마감은 기존 결제완료 처리. 이 조회 페이지는 쓰기/결제 API를 호출하지 않음 |
| 기사 지급·법정 공제·지사 지급 원장 | 이용료 행과 별도 업무 의미 | [지급 상세 기획](../../../../AI/Planning/공통/PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL/README.md)의 후속 범위. 행 존재/IsPaid를 입금 증거로 해석하지 않음 |

현재 Configuration에는 기사/년/월 복합 unique index와 월확정 revision이 없다. 동시 반영·중복 배차 과금·월마감과 실제 PG 결과의 결속은 기존 보완 필요 사항이다. 이 문서 작업에서 운영 schema/청구 정책을 임의로 바꾸지 않는다. 월 경계는 현행 UTC이며 한국 정산일과의 차이는 정책 결정·고정시각 시험이 필요한 후속 항목이다.

## 결손과 검증

기존 ViewModel은 오류를 보유하지만 화면은 기본 0원 값을 항상 표시했다. 이번에 로딩·실패·성공을 실제 ViewModel 상태와 결속하고 재시도/새로고침을 추가했다. 이용료·상한·금액 계산식은 그대로다. 실제 PG·은행 입금·운영 MySQL·기기 실행은 미검증이다. 자동 시험/렌더 상한은 [보완 결과](../../implementation-r1.md)에 기록한다.
