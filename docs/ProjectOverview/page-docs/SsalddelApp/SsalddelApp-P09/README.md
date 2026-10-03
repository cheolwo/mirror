# SsalddelApp-P09 - 운송 업무 워크스페이스

[전체 화면 문서](../../README.md) / [SsalddelApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

현재 전용 캡처를 다시 생성해야 한다. 페이지 구현과 라우트는 확인했으며 카탈로그에는 `캡처 대기`로 표시한다.

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 라우트 | `/shipper/transport` |
| 내비게이션 단계 | 3단계 데이터 페이지 |
| 목표 진입 문맥 | 운송 다이어그램의 `운송` 또는 `운송 의뢰` 노드 행동 |
| 소스 | [TransportWorkspace.razor](../../../../../SsalddelApp/Components/Pages/TransportWorkspace.razor) |
| 분류 | 필수 |
| 캡처 | 대기 |

## 왜 필요한가

홈은 현재 상태를 요약하는 데 머물고, 의뢰별 결제·배차·상차·하차·정산 확인은 이 화면에 모아 홈의 단일 책임을 지킨다.

## 사용자와 화면 책임

주 사용자는 화주다. 로그인된 사용자의 운송 의뢰를 조회하고, 상태별 다음 확인 행동, 타임라인, 새 의뢰 등록으로 연결한다. 창고·판매·통관 처리는 각각의 전용 워크스페이스로 넘긴다.

### 조회·복귀 책임 (2026-10-03)

목록의 초기·수동·SignalR·주기 갱신은 [ShipperQueryLifetime](../../../../../SsalddelApp/ViewModels/Shipper/ShipperQueryLifetime.cs)으로 직렬화한다. 최신 요청·현재 로그인 수명·화면 수명이 모두 맞는 결과만 표시한다. 로그아웃 시 이전 의뢰 목록을 비우고 로그인 안내로 돌아간다. 의뢰별 입력·모의 결제 안내·운송 상태의 상세 책임은 [의뢰 상세](../SsalddelApp-P03/)에 남긴다. [보완 범위·시험 경계](../../cargo-workflow-recovery-r1.md).

## API와 보안

`IShipperOperationsService`로 의뢰 목록을 읽고 `ShipperRequestDetail`로 이동한다. 비로그인 사용자는 데이터 대신 홈 로그인 안내를 본다. 주소, 연락처, 결제 정보는 필요한 확장 영역에서만 노출해야 한다.

## 다른 화면과의 관계

- 이전: [SsalddelApp-P01 화주 홈](../SsalddelApp-P01/)
- 다음: [SsalddelApp-P02 운송 의뢰 작성](../SsalddelApp-P02/), [SsalddelApp-P03 의뢰 상세](../SsalddelApp-P03/)
- 목표 흐름: 사방괘에서 운송 영역 선택 → 운송 다이어그램 → 운송 노드의 `운송 업무` 행동 → 이 페이지
- 돌아가기: 출발 다이어그램의 선택 노드, 스크롤·확대, 레이어 상태를 복원한다.
