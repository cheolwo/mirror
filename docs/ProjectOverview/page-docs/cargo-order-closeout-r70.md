# 퀵 한 건의 화주·기사·운영자 화면 책임 r70

## 2026-10-21 화주 조회의 업무 문맥 r2

같은 의뢰의 진행 조회와 접수 요청 결과 조회를 각각 기존 OS 단계에 결속한다. [단계별 대응표](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/domain-workflow-recovery.matrix.r2.md)에 따라 API의 업무 사유·결과 구분을 화면 문구/가능 행동으로 표시한다. 조회404를 접수 미적용·취소 완료로 추정하지 않으며 중요한 결제/인수증 미확정 안내와 기존 요청 수명을 유지한다. 공통 안내가 원장·권한·업무 전이를 소유하지 않는다.

2026-10-19 · **CompletedLocalValidation — 한 건의 로컬 업무·역할 조회 마감 / 실제 운영 미검증**

[승인 기획](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.design.r70.md) · [변경·검증 기록](../../Changes/2026-10-19-cargo-order-closeout-r70.md) · [기존 기기별 책임](quick-driver-usability-r110.md#기기별-마감-범위-r111--2026-10-18-사용자-확정)

이번에는 주문 한 건을 끝내는 데 필요한 기존 화면만 보완한다. 기사 모바일 우선, 화주 모바일·기존 Windows MAUI/웹 지원, 운영자 기존 조회 권한을 유지한다. 개발 PC에서 보는 SSR은 실제 휴대폰이나 Windows 앱 실행 결과가 아니다.

## 화주 오류 복구 r1 — 구현 전 책임 확인

2026-10-21 · 같은 의뢰 조회가 실패하면 짧은 공통 안내 창에서 GET 재조회 또는 기존 목록 복귀를 선택한다. 창 닫기로 실패를 성공/빈 상태로 바꾸지 않는다. 기술 원문은 표시하지 않고, 중요한 인수증·결제 결과 미확인 안내는 유지한다. 자동 재조회가 성공하면 이전 조회 오류만 정리한다. [공통 계획](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/role-error-recovery.r1.md)을 따른다.

[이번 구현·통신·시험 범위](../../Changes/2026-10-21-role-error-recovery-r1.md)에서 공통 상세 Frame·모바일 조회 복구와 다른 화면의 후속 범위를 구분한다. 기존 로컬 한 건 마감이나 이전 캡처를 이번 APK·실기기 오류 복구 검증으로 대체하지 않는다.

## 화주 문구 최소화 r1 — 구현 전 책임 카드

2026-10-21 · 사용자 승인 범위는 의뢰 상세·진행·증빙·결제의 표시 문구다. [승인 기획](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-concise-ui.r1.md)을 따른다.

| 항목 | 기록 |
| --- | --- |
| 주 사용자·대상 | 화주가 기존 의뢰 상세 route에서 같은 의뢰의 현재 상태·다음 행동을 확인한다. |
| 한 문장 목적 | 화주가 의뢰의 진행·증빙·금액을 짧은 상태와 행동으로 판단한다. |
| 기본 정보 | 현재 운송·결제·정산 상태, 상하차 주소, 금액, 담당과 다음 행동. 서로 다른 완료 상태를 합치지 않는다. |
| 보조 정보 | 기존 기사·차량·위치 시각·화물 규격·인수증 번호·등록 시각은 해당 문맥에 유지한다. |
| 제외 정보 | 화면 번호·중복 제목 설명·정상 조회 알림·원시 좌표·GUID 강조·adapter/endpoint·원장 설명을 기본 화면에서 제외한다. |
| 실패·복귀 | 오류·결과 미확인·로그인/권한·재조회·테스트 데이터 가능성·실제 금전 미이동 안내는 짧게 유지한다. 기존 링크·수명·실행 조건은 바꾸지 않는다. |
| 코드·권위 | 기존 `ShipperRequest*Screen`·`ShipperRequestDetailPresentation`의 표시만 변경한다. API·DTO·상태판정·원장·금액·결제 실행은 기존 권위를 유지한다. |
| 책임 판정·검증 | 같은 목적의 구성. 기존 시험 기대 문구와 소비 앱 빌드·실제 호스트 렌더는 통합 담당이 검증하며 이 사전 카드만으로 화면 검증 완료를 보고하지 않는다. |

### 문구 반영 결과

공통 상세 헤더의 내부 화면 번호·중복 설명과 정상 재조회 알림을 숨겼다. 화물명·차량·현재 상태·주소·금액·담당·기존 이동 링크를 남기고 GUID와 원시 위치 좌표 강조를 제거했다. 진행 이력은 단계·상태·해당 근거만 표시하며, 증빙의 `의뢰 상태 생성됨` 중복 항목은 제거했다. 상차·하차 상태 기록을 사진 검수 완료로 바꾸지 않는다.

결제 문구는 `테스트 결제`로 통일했고 실제 결제·송금이 발생하지 않는 안내를 유지했다. 오류·미확인 결과·동의·검수와 지급의 구분·재조회·권한·비활성 조건은 유지한다. API·DTO·상태 판정 분기·금액·라우트·사진/인수증/결제 실행 코드는 변경하지 않았다. 테스트 데이터가 허용되거나 명시된 미리보기는 짧은 안내로 구분한다.

정상 재조회 안내는 Web·MAUI 조회 생산자에서 빈 문구로 정리한다. 공통 프레임은 성공·안내 등급을 통째로 숨기지 않으며, 실제 행동 결과인 `인수증 등록 완료`와 `테스트 결제 완료 · 실제 결제 없음`은 유지한다. 등록·결제의 상태 전이와 재조회 조건은 바꾸지 않는다.

기존 회귀의 문자열 기대값만 조정했다. `git diff --check`는 통과했으며, 소비 client 빌드·집중 시험·변경 화면 검증 결과는 [통합 변경 기록](../../Changes/2026-10-21-cargo-concise-ui-r1.md)에 기록한다. 이 문구 수정 담당은 새 APK·실제 화면·업무 재수행을 실행하지 않았다.

## 역할별 한 가지 책임

| 항목 | 화주 의뢰 확인 | 기사 추천·수행 | 운영자 운송 확인 |
| --- | --- | --- | --- |
| 사용자·대상 | 본인 또는 현재 담당 권한이 있는 운송 의뢰 | 현재 본인에게 온 제안과 수락한 운송 | 기존 서버관리자가 조회하는 같은 의뢰·운송 |
| 화면 질문 | 내가 보낸 의뢰는 지금 어디까지 진행됐고 무엇을 확인해야 하나 | 얼마를 받는 어떤 업무이며 지금 무엇을 완료해야 하나 | 어느 단계가 막혔으며 현재 담당자·증빙·후속은 무엇인가 |
| 주 행동 | 같은 의뢰 재조회, 진행·증빙·결제 문맥으로 이동 | 추천 조건 확인·수락, 현재 단계 도착·사진·완료 | 기존 상세·이벤트·증빙·정산 조회 |
| 기본 정보 | 의뢰 ID, 의뢰/배차/운송/결제/정산의 별도 상태, 다음 담당 | 추천 시각·기사 약정 보수·장소, 수락 후 현재 행동·동결 보수 | 같은 의뢰 ID, 전문 단계·상태·확인 필요 항목 |
| 보조 정보 | 기존 기사·위치·인수증·내역 | 차량 경고·주문 조건·문제 신고 | 기존 POD·이벤트·정산 후보 |
| 제외 | 전달·인수증·입금을 한 완료 상태로 합치지 않음 | 고객 금액으로 기사 보수를 추정하지 않음 | 정상 건마다 새 승인 절차나 새 권한을 추가하지 않음 |
| 실패·복귀 | 접수 결과가 불명확하면 원래 요청을 재조회 | 추천 만료·보수 변경 시 재확인, 불명확한 명령 자동 재전송 금지 | 실패한 조회를 빈 목록·처리 완료로 바꾸지 않음 |

## 페이지 → 코드 → API·원장

| 문맥 | 기존 구현 | 서버·원장 |
| --- | --- | --- |
| 화주 요약 `/shipper/request/{RequestId}` | [공통 요약](../../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestSummaryScreen.razor), [공통 표시 투영](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/ShipperRequestDetailPresentation.cs), [MAUI 소비 페이지](../../../SsalddelApp/Components/Pages/ShipperRequestDetail.razor) | `GET api/v1/shipper/requests/{id}`. 화주 의뢰와 최신 운송 실행 정보를 권한 검사 후 제공 |
| 화주 진행 `/shipper/request/{RequestId}/timeline` | [공통 진행 화면](../../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestTimelineScreen.razor) | 같은 의뢰 응답의 전문 운송 상태와 별도 결제·정산 상태 |
| 기사 제안 `/workspace/cargo-driver/offers/{ItemId}` | [제안 화면](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionScreen.razor), [제안 VM](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs), [보수 표시](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoDriverPayoutPresentation.cs) | 기존 추천/의뢰 GET·명시 수락 API. 현재 추천 시각·차수·후보·원의뢰 기사 약정 근거 |
| 기사 현재 운송·단계 완료 | [수행 VM](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionViewModel.cs), [공통 API 소비자](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceApiClients.cs) | 기존 기사 운송 상세·사진·상차/하차 API. 수락 때 동결한 운송 원장 기사액·사진 영수증 |
| 운영자 PC `/transports/{RequestId}` | [상세 페이지](../../../SsalddelAdmin/Components/Pages/TransportWorkflowDetail.razor), [기존 요약 카드](../../../SsalddelAdmin/Components/Shared/AdminTransportWorkflowSummaryCard.razor), [조회 조합](../../../SsalddelAdmin/Services/백오피스조회Service.Workflow.cs) | 기존 의뢰·결제·배차대기·운송·이벤트·POD·기사월정산의 GET 자료를 기존 Factory로 조합 |
| 운영자 모바일 `/operations` | [기존 모바일 관제](../../../SsalddelAdminApp/Components/Pages/Operations.razor) | `GET api/v1/admin/transports`, `GET api/v1/admin/drivers/operating`. 상세 화면과 별개인 운송·기사 현황 |
| 같은 건의 교차 조회 | [전문 원장 조회](../../../Ssalddel/Application/Admin/Progress/Handlers/운송원장이벤트조회QueryHandler.cs) | `GET api/v1/transport-request-ledgers/{requestId}/events`. 본인/관리자 검사 후 의뢰·운송·Mongo 존재/단계·이벤트 제공 |

실제 역할 검증에서 관리자 정산 enum 파싱과 GET5개의 기능 경계 미분류404가 확인됐다. [제품 관리자 투영](../../../SsalddelAdmin/Services/AdminShipperRequestProjection.cs)을 기존 서비스의 목록·단건·취소 성공 응답에 연결했고, 해당 GET action에만 국내 화물 기능 메타데이터를 추가했다. POST·기존 권한·기능 설정·운영 기본값은 유지한다.

상위 주문의 업무 이유·하위 원장 참조 소유 원칙은 유지한다. 일반 퀵 의뢰에 상위 통합 `order` 원장을 자동 연결한 구현은 현재 확인되지 않았다. `transport:{requestId}`는 [운송 전문원장 투영](../../../Ssalddel/Services/Community/운송원장Mongo동기화Service.cs)이며 상위 주문 원장으로 부르지 않는다. 이번에는 이 공백을 기록하고 새 연결 기능을 확장하지 않는다.

## 보수와 시간의 표시 기준

1. 추천 시작·만료는 UTC 의미를 공유 응답에서 유지하며 원래 만료 검사와 유효시간을 보존한다.
2. 수락 전에는 소형 전용 동결 요금 또는 원의뢰에 결속된 기사 약정액을 표시한다. 일반 화물의 고객 청구액을 기사 보수로 바꾸지 않는다.
3. 수락 전 재조회에서 보수가 달라지면 현재 금액을 다시 확인한다. 보수 근거 누락·충돌은 미확인으로 남긴다.
4. 수락 후에는 운송 원장에 저장된 기사 동결액을 표시하며 현재 다른 요금 정책으로 다시 계산하지 않는다.
5. 화면에 표시한 보수·정산 준비·실지급은 서로 다른 결과다.

## 이번 검증의 판정 범위

최종 제품 Fast114/114·실패0·생략0, 솔루션 빌드 오류0·경고207과 검증 호스트/client 빌드 오류0·경고0을 확인했다. 전수 Task `20261019-112105`는 EF 수정 후 통과12,690·생략16·실패0이며 후속 관리자 투영/GET 분류 추가 이전 판본이다. 초기 전수 `110229`의 통과12,689·생략16도 별도 보존한다. 최종 소스 전체를 다시 전수 실행한 결과로 합치지 않고, 마지막 보완은 집중114개·소비자 빌드·실제 GET·SSR로 구분한다.

새 case2 `71783878-452b-e95f-153d-d51020d79867` / 운송5에서 공통 VM 수락 POST1회와 제안/동결6,200원 일치, 픽업·전달 사진·인수완료, 사진 없는 완료400·다른 기사 상세404를 확인했다. 화주 운송 완료, SQL/Mongo 완료, 기사 활성0·대기 복귀가 일치했다. 최종 `roles/completed-r3/result.json`은 같은 의뢰의 GET9개 모두200·화주 요약/진행/운영자 요약의 제품 SSR3개 통과다. 기사 전달·운송 완료와 실제 인수증·지급은 같은 결과가 아니다.

case1 EF 조회 실패, `roles/accepted`의 관리자 enum 계약 실패, GET9개 성공 뒤 도구 Navigation URI의 끝 `/` 누락으로 실패한 `completed-r2`를 보존했다. 마지막 도구 한 줄 수정 뒤 새 `completed-r3`로 재검증했으며 이전 실패 결과를 성공으로 덮지 않았다. 단계별 증거·대표 캡처와 판본별 시험은 [변경 기록](../../Changes/2026-10-19-cargo-order-closeout-r70.md)을 따른다.

[역할별 SSR 도구](../../../eng/CargoOrderCloseoutR70/RoleCloseoutScreens.cs)는 실제 합성 의뢰 GET을 기존 화주 요약·이력·운영자 요약 컴포넌트에 넣고 같은 ID·상태·Mongo 연결을 대조한다. 이 도구는 GET만 허용하고 업무 명령·역할 부여·DB 초기화를 하지 않는다. 정적 SSR 표시와 실제 화면 버튼 조작은 구분한다.

최종 화주 의뢰 헤더는 `생성됨`, 전문 운송은 `인수완료`, 결제는 `결제대기`, 정산은 `입금대기`이며 인수증 미등록(`receiptRecorded=false`)·기존 POD0건을 유지했다. 가상계좌 결제대기 fixture는 실제 결제 검증이 아니다. 운송 첨부 사진 영수증과 기존 운영자 POD 표는 별개이며 사진을 임의로 POD 검수 완료로 바꾸지 않는다. 실제 단말·GPS·지도·카메라·알림음·외부 HTTPS·ready/migration·FCM·실결제/입금·공개 GitHub 다운로드는 이번 로컬 증거와 별개다.

## APK 인계

통합앱 `0.1.194 / versionCode194 / Debug`를 기존 USB 제작 도구로 만들었고 서명·정렬·빌드 중 소스 유지·인계 사본 해시 검사를 통과했다. 제작 뒤 추가 수정은 서버·관리자 경로이며 통합앱 소스는 유지했다. 내장 서버는 `http://127.0.0.1:5363/`이며 PC 서버·USB 연결이 필요한 시험판이다. 실제 휴대폰 설치·통신은 확인하지 않았고, 업무 API 검증의 별도5364 호스트와 같은 실행으로 합치지 않는다.

[다운로드 안내](../../../eng/release/downloads/README.md)에 기록한 APK를 Drive 웹으로 비공개 업로드했다. 도구의 최초 업로드 시도는 100MiB 제한에 따른 업로드 전413으로 중단됐으며, 브라우저 업로드 뒤 파일 ID·이름·138,097,264bytes·소유자 전용 권한을 재조회했다. 원격 SHA가 제공되지 않았고 전체 재다운로드를 실행하지 않았으므로 전체 사본 해시 일치까지 검증한 것은 아니다.

익명 공개 권한 변경과 GitHub push는 하지 않았다. 기존 제작·서명·버전·해시 인계 체계를 재사용하되 비공개 보관·설치 링크 문서와 실제 외부 다운로드·업데이트·기기 통신을 각각 구분한다. 외부 서버 주소가 없는 로컬 시험판을 외부 운영용으로 표시하지 않는다.
