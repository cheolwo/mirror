# SsalddelAdmin-P22-3 - 운송 정산 상세

[전체 화면 문서](../../README.md) / [SsalddelAdmin 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/SsalddelAdmin/SsalddelAdmin-P22-3.png" alt="SsalddelAdmin-P22-3 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | SsalddelAdmin |
| 페이지 ID / 제목 | SsalddelAdmin-P22-3 - 운송 정산 상세 |
| 라우트 | /transports/{RequestId}/settlement |
| 소스 파일 | [SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor](../../../../../SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [SsalddelAdmin-P22-3 - 운송 정산 상세](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 왜 필요한가

이 화면은 운송 정산 상세을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 관리자, 운영자 / 보조 참여자: 화주, 기사, 파트너, 문서 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 운송 정산 상세 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

### 페이지 책임 카드 · 2026-10-22 r20

| 항목 | 책임 |
| --- | --- |
| 주 사용자·대상 | 기존 서버관리자 권한으로 선택한 화물 의뢰 한 건을 확인하는 운영자 |
| 질문·결과 | 화주 수납, 기사 지급준비, 지급요청과 실지급의 확인 범위를 구분한다. 조회만 하며 수납·지급을 실행하지 않는다. |
| 기본 정보 | 화주 수납과 운송 기준, 현재 UTC 승인월의 `GET api/v1/admin/driver-payouts` 중 운송 ID와 원천 의뢰 ID가 모두 일치하는 지급요청·실지급 확인 |
| 미연결·실패 | 관리자 지급준비 조회는 미연결로 표시한다. 기사 본인 전용 준비 API를 우회하지 않는다. 지급요청 조회 실패는 0건으로 바꾸지 않고 새로고침으로 재확인한다. 빈 결과도 해당 승인월에 한정하며 다른 월은 미확인이다. |
| 독립 업무 | 기사 월 이용료는 운송료 지급 후보로 표시하지 않고 기존 `/settlements` 조회로 인계한다. 화주 수납·시험 지급 검증·월 이용료 완료는 실지급 완료가 아니다. |
| 주 행동·복귀 | 새로고침으로 같은 의뢰를 재조회하고 관제 개요·이벤트·증빙으로 기존 복귀 문맥을 유지한다. |
| 실행·검증 경계 | 권한·정산 정책·원장·기본 비활성을 변경하지 않는다. 코드·회귀 시험, 실제 렌더, APK·단말, 실제 수납·송금 증거는 별도로 판정한다. |

- 주 책임: 운송 정산 상세
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [SsalddelAdmin-P22-2 - 운송 증빙/POD](../SsalddelAdmin-P22-2/)
- 다음 화면: [SsalddelAdmin-P23 - 관리자 활동 로그](../SsalddelAdmin-P23/)
- 상위 화면: [SsalddelAdmin-P22 - 운송 상세 원장](../SsalddelAdmin-P22/)
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 관리자는 여러 앱에서 발생한 상태 변경을 모아 보고, 막힌 배차·증빙·정산·문서 문제에 개입합니다.

## API 경로와 코드 연결

- 화면 소스: [SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor](../../../../../SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor)
- 클라이언트 서비스/계약: [SsalddelAdmin/Services/I백오피스Service.cs](../../../../../SsalddelAdmin/Services/I백오피스Service.cs), [SsalddelAdmin/Services/백오피스메모리Service.cs](../../../../../SsalddelAdmin/Services/백오피스메모리Service.cs), [SsalddelAdmin/Services/백오피스조회Service.cs](../../../../../SsalddelAdmin/Services/백오피스조회Service.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 2.0 운송 문서 | - | `api/v1/payments` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | ``GET api/v1/payments`` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>``GET api/v1/payments/toss/config`` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>``POST api/v1/payments/prepare`` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs)<br>``POST api/v1/payments/confirm`` [Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs](../../../../../Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

관리자 권한, 감사 로그, 민감 운영 정보 접근 통제가 필요합니다. 인증 필요 화면은 현재 로그인 장벽 캡처로 남겨 둡니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
