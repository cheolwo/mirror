# 화주 인수증 등록 r70

## 페이지 책임 카드

| 항목 | 책임 |
| --- | --- |
| 주 사용자·대상 | 로그인한 화주가 선택한 운송 의뢰 한 건 |
| 질문·결과 | 운송 완료 내용을 확인하고 해당 인수증 번호를 원장에 등록했는가 |
| 진입·복귀 | 기존 `/shipper/request/{RequestId}/proofs`로 진입하고 같은 의뢰 상세로 복귀 |
| 기본 정보 | 현재 운송·정산 상태, 등록된 인수증 번호·시각, 미등록이면 번호·선택 메모·수령 검수 확인 |
| 주 행동 | 등록 가능한 상태에서는 인수증 등록, 결과 미확인이면 같은 의뢰의 등록 결과 확인 |
| 실패·수명 | 중복 클릭을 막고 계정 변경·다른 의뢰 선택·화면 이탈 시 입력과 늦은 결과를 폐기한다. 불확실한 POST 뒤에는 정본 GET부터 확인하고 자동 재전송하지 않는다. |
| 독립 업무 | 인수증 등록은 청구·입금·정산·기사 지급 완료가 아니다. 기사 사진·서명의 생성과 검수는 기사 증빙 저장소 책임이다. |
| 책임 판정 | 기존 증빙 관리 목적의 명시 입력 단계. 공통 패널은 표시·콜백, 모바일 ViewModel은 입력·응답 수명, 서버는 권한·완료·인계·멱등성을 소유한다. |

## 페이지 → 코드 → 원장

- [모바일 증빙 route](../../../SsalddelApp/Components/Pages/ShipperRequestProofsPage.razor)는 기존 공통 증빙 Screen에 등록 패널을 조립한다. Web은 이 패널을 전달하지 않아 기존 읽기 전용 동작을 유지한다.
- [등록 ViewModel](../../../SsalddelApp/ViewModels/Shipper/ShipperRequestDetailPageViewModel.Receipt.cs)은 같은 의뢰의 서버 조회와 입력을 연결한다. [서버 adapter](../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs)는 기존 `GET api/v1/shipper/requests/{id}`와 `POST .../{id}/settlement/receipt`만 사용한다. 등록·결과 조회에는 sample fallback을 사용하지 않는다.
- 서버의 기존 [인수증 등록 handler](../../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰인수증등록CommandHandler.cs)가 요청자 권한, 실제 완료 운송·도착과 완료 인계를 확인한다. 이미 기록된 같은 번호는 멱등이고 다른 번호는 충돌이다. 진행된 금융 상태를 되돌리지 않는다.

UI의 상태 안내는 버튼 준비를 위한 보조이며 서버 허가를 대신하지 않는다. 결과 재조회에서 같은 의뢰·번호·등록시각을 확인해야 등록 완료로 표시한다. 재조회 실패·미존재·다른 번호는 성공으로 바꾸지 않는다. 입력은 화면 메모리에만 있고 로그아웃·의뢰 변경·이탈 뒤 자동 복원·전송하지 않는다.

## 검증 상태

ViewModel·HTTP 대역·공통 패널 정적 렌더 30개를 포함한 Fast 155개와 Task 11,425개가 통과했다. 환경 의존 MongoDB 15개는 건너뛰었고 전체 소비 앱 빌드 오류는 0개다. 실제 loopback 호스트의 공통 패널 네 상태도 확인했다. 브라우저 연결이 없어 paint·작은 화면 배치·클릭·터치는 미검증이다. 물리 휴대폰·운영 운송·실제 금융은 별도 증거다. [전체 결과와 APK 상태](../../Changes/2026-10-15-private-apk-preparation-r70.md)를 따른다. 이번 준비는 기능 기본값·서버 정책·외부 저장소·지급 상태를 변경하지 않는다.
