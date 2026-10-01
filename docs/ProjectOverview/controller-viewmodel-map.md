# Controller와 ViewModel 조립 구조

## 목적

Razor 페이지가 `HttpClient`, URL, 로딩·오류 상태를 직접 다루지 않도록 API 제어를 ViewModel 계층으로 옮겼다.

구조는 다음 두 층이다.

1. **Controller 카탈로그 층**: 서버 Controller 기본 경로 전체를 빠짐없이 제공한다.
2. **타입드 업무 ViewModel 층**: 화면에서 자주 쓰는 안정된 API를 요청·응답 타입과 업무 단위로 묶는다.

새 API는 카탈로그를 통해 즉시 조립할 수 있고, 반복 사용되는 API는 타입드 업무 ViewModel로 승격한다.

## 공통 기반

- `CommunityToolkit.Mvvm 8.4.2`
- `Api작업ViewModel<TResult>`: 매개변수 없는 API의 실행·성공·실패·취소·결과 상태
- `Api작업ViewModel<TParameter, TResult>`: 요청값이 있는 API의 동일한 상태 관리
- `조립ViewModelBase`: 하위 ViewModel 변경을 상위 PageViewModel에 전달
- `MvvmComponentBase<TViewModel>`: Razor Component에 ViewModel을 주입하고 `PropertyChanged` 때 다시 렌더링
- `IHongdalJsonApiClient`: 인증 헤더, 오류 변환, JSON 직렬화의 공통 경계
- `HongdalProtectedApiClient`: ISMS-P 보호 속성이 있는 요청의 보호 직렬화

## Controller 경로 대응 현황

`Hongdal`과 `Hongdal.FoodApi`의 Controller 클래스 기본 `[Route]`를 `Controller기능카탈로그`와 대조했다.

| 영역 | 카탈로그 | 기본 경로 수 |
|---|---|---:|
| 공통·앱·보안 | `공통` | 45 |
| 기사 | `기사` | 17 |
| 음식 배달 기사 | `음식배달기사` | 2 |
| 화주 | `화주` | 4 |
| 주문자 | `주문자` | 12 |
| 음식 | `음식` | 6 |
| 관리자(음식 관리자 포함) | `관리자` | 41 |
| 합계 |  | **127** |

서버 기본 경로 127개와 카탈로그 경로 127개가 일치한다. 인사 앱은 관리자 카탈로그의 HR 4개 Controller를, 창고 앱은 공통 카탈로그의 `warehouse-operations` Controller를 역할별 부분집합으로 제공한다.

## 역할별 하위 ViewModel

### 기사

`기사Api기능모음ViewModel` 아래를 프로필, 근무, 추천, 탐색 캠페인, 배차 액션, 예약, 운송, 설정, 정산, 알림, 명령 기능 설정, 개발 도구로 나눴다. 여기에 `기사Controller기능모음ViewModel`과 `공통Controller기능모음ViewModel`을 함께 조립한다.

### 음식 배달 기사

`음식배달기사Api기능모음ViewModel`

- `업무`: 작업 공간, 운행 시작·종료, 위치 갱신, 단건·묶음 수락, 픽업, 배달 완료, 경로
- `음식배달Controllers`: 음식 배달 전용 Controller
- `기사Controllers`: 기사 공통 Controller
- `공통Controllers`: 전 역할 공통 Controller

### 화주

`화주Api기능모음ViewModel`

- `운송의뢰`: 목록·상세·공개 화물·차량·운임·등록·일괄 등록
- `창고`: 창고·입고·재고 조회
- `판매`: 판매 채널 계정·상품·출품
- `화주Controllers`, `공통Controllers`: 아직 별도 타입드 ViewModel로 승격하지 않은 Controller API

### 주문자

`주문자Api기능모음ViewModel`

- `공동구매.업무흐름.모집`: 목록·제안·수요 참여·이의 검토
- `공동구매.업무흐름.합의`: 모집 마감·결의문·전자서명·절차 상태
- `공동구매.업무흐름.공급`: 생산자 연결·공급 제안·적합성·협상
- `공동구매.업무흐름.물류`: 이행 경로 미리보기와 발주·원장 생성 초안
- `공동구매.업무흐름.실행.자동집단`: 상품·배송권 자동집단 목록, 수요 등록과 실행 ID 선택
- `공동구매.업무흐름.실행.주문원장`: 주문자 보호형/역할별 조회, 하위 원장 연결·분리, 계약 서명
- `공동구매.업무흐름.실행.커머스이행`: 공동구매 ID/문서번호 조회, 입고·출품·출고 진행 단계와 다음 작업 안내
- `공동구매`: 기존 해외 선적 조회와 수입 단가 시뮬레이션 API 작업도 호환용으로 유지
- `음식점탐색`: 음식점 탐색 정책
- `주문자Controllers`, `음식Controllers`, `공통Controllers`

실행 영역에서는 `공동구매실행상태ViewModel` 하나가 자동집단 ID, 주문 루트 원장 ID와 선택한 커머스 계획을 공유한다. 자동집단 ID는 커머스 이행 조회로 전달하지만, 커뮤니티 투표 원장과 주문 루트 원장은 서로 다른 문서일 수 있으므로 자동으로 같은 ID를 사용하지 않는다. 주문 루트 원장 ID는 발주·원장 생성 결과에서 명시적으로 연결한다.

자동수요 초안에는 전체 참여자의 합계 수량을 개인 수량으로 복사하지 않으며 현재 주문자의 식별키·표시명도 화면에서 명시적으로 받는다. 공동구매 결의문 번호와 해시는 주문원장 서명 화면의 참고 근거로만 보여 주고, 개별 주문계약의 문서번호·해시와 혼용하지 않는다.

| 실행 하위 ViewModel | 대응 Controller API |
|---|---|
| `공동구매자동집단ViewModel` | `GET api/v1/orderer/group-purchase-auto-groups`, `POST .../demands` |
| `공동구매주문원장조회ViewModel` | `GET api/v1/community/order-ledgers/{id}`, `GET .../views/{role}` |
| `공동구매하위원장ViewModel` | `POST .../{id}/children`, `DELETE .../{id}/children/{childId}` |
| `공동구매주문원장서명ViewModel` | `GET .../{id}/signature`, `POST .../signature-request`, `POST .../signatures` |
| `공동구매커머스이행ViewModel` | `GET .../group-purchase-commerce-fulfillment-plans/by-group-purchase/{id}`, `GET .../lookup` |

주문자 커머스 API는 조회 전용이다. 물류 대행·입고·출품·출고 상태 변경은 관리자 API에 남겨 역할 경계를 유지한다.

### 음식점 데스크

`음식점Api기능모음ViewModel`

- `주문`: 서버 주문 목록·상세, 데스크 목록, 실시간 알림 반영, 수락 및 전표 준비, 출력 완료
- `음식Controllers`, `공통Controllers`

### 창고

`창고Api기능모음ViewModel`

- `작업`: `WarehouseOperationsController`의 12개 액션과 요청·응답 타입으로 직접 대응
- `창고Controllers`, `공통Controllers`

### 인사

`인사Api기능모음ViewModel`

- `고용계약`: 목록·상세·초안·서명·급여 스케줄
- `참여혜택`: 목록·전환
- `역할`: 목록·배정·해제
- `사회보험`: 목록·상세·가입 요건 평가·계획·상태
- `인사Controllers`, `공통Controllers`

### 관리자

`관리자전체Api기능모음ViewModel`은 관리자 41개와 공통 45개 Controller 기능을 조립한다. 기존 백오피스 타입드 서비스는 유지하고, 아직 타입드 서비스가 없는 Controller는 카탈로그를 통해 페이지별 하위 ViewModel을 만든다.

## Razor 페이지에서 사용

페이지는 공통 기반 클래스를 상속하면 ViewModel 주입과 변경 알림 렌더링을 함께 얻는다.

```razor
@using Hongdal.Ui.Common.Areas.App.Components
@inherits MvvmComponentBase<주문자Api기능모음ViewModel>

@if (ViewModel.공동구매.해외선적조회.처리중)
{
    <p>조회 중...</p>
}

@code {
    private Task 조회Async(string 문서관리번호)
        => ViewModel.공동구매.해외선적조회.실행Async(문서관리번호);
}
```

가벼운 페이지는 루트 모음 대신 `주문자공동구매기능ViewModel`처럼 필요한 하위 ViewModel 하나만 주입한다.

## 아직 타입드 ViewModel이 없는 API 사용

`Controller기능ViewModel`은 고정·동적 경로에 대해 다음 작업을 만든다.

- `조회<TResult>()`
- `경로조회<TResult>()`
- `명령<TRequest, TResult>()`
- `경로명령<TRequest, TResult>()`
- 응답 본문이 없는 명령

경로 템플릿 값도 치환한다.

```csharp
var controller = 주문자Controllers["orderer.negotiation"];
var 작업 = controller.경로조회<협상상태응답>();

await 작업.실행Async(new ControllerApi경로요청(
    경로값: new Dictionary<string, string>
    {
        ["campaignId"] = campaignId.ToString()
    }));
```

작업 ViewModel은 Razor 렌더링 중 매번 만들지 않고 PageViewModel 생성자에서 한 번 만들어 속성으로 보관한다.

## 인증 연결

공통 API 클라이언트는 앱이 등록한 `IHongdalAccessTokenProvider`의 토큰을 사용한다. 기사, 음식 배달 기사, 화주, 관리자, 관리자 앱, WebAssembly 앱은 기존 인증 세션과 연결돼 있다.

창고·인사 앱은 아직 자체 로그인 세션이 없으므로 현재 빈 토큰 공급자를 사용한다. 해당 서버 Controller는 인증이 필요하므로 실제 호출을 열기 전에 각 앱 로그인 세션이 `IHongdalAccessTokenProvider`를 구현하고 `AddHongdalUiCommonAppServices<TSession>()`로 등록되어야 한다.
