# SsalddelAdmin-P30 - 음식 주문/배달 운영

[전체 화면 문서](../../README.md) / [SsalddelAdmin 화면 목록](../README.md) / [전체 route](../../current-pages.md) / [이번 보완 결과](../../implementation-r1.md)

## 기존 화면 캡처

<img src="../../../assets/app-pages/SsalddelAdmin/SsalddelAdmin-P30.png" alt="SsalddelAdmin-P30 이전 화면 캡처" width="720">

이전 캡처를 보존했다. 2026-10-02 소스 검토·수정 결과를 새 캡처로 확인한 것은 아니다.

## 1. 페이지: 요금 정책 수정과 리뷰 운영 조회

| 항목 | 현재 연결 |
| --- | --- |
| route | `/food/operations`, 별칭 `/admin/food-delivery/operations` (같은 페이지) |
| 소스 | [FoodOperations.razor](../../../../../SsalddelAdmin/Components/Pages/FoodOperations.razor) |
| 주 사용자/단계 | 서버관리자 / 운영 데이터 페이지. 사방괘·다이어그램 문맥 전달은 이번 검토 대상 아님 |
| 입력 | 플랫폼 기본/최소/거리 요금, 기사 기본/최소/거리 지급액, 기본 포함 거리·거리 단위 |
| 조회 | 리뷰 검토 대상 수·연속 저평점 수·최대 5개 목록과 리뷰 정책 |
| 초기화/실패 | 세 조회가 모두 성공해야 편집 가능. 실패는 오류·재시도 표시, 기본 객체를 저장하지 않음 |
| 저장 | 중복 클릭 차단 → PUT → 서버 응답으로 정책 교체. 실패는 성공 문구를 표시하지 않음 |

견적 거리 테스트는 현재 화면의 기본·거리 요금만 계산한다. 기상/한시 수요 할증과 실제 주문 상태를 반영하는 배차 견적은 아니다. `플랫폼 마진`은 이 단순 비교의 차액이며 회계상 순이익으로 확정하지 않는다. UI에서 편집하지 않는 `DriverPickupPayout`·기상 정책은 조회 객체를 그대로 PUT하여 보존한다. 새 요율·기간 프로모션·공제 정책을 만들지 않았다.

## 2. 코드: 화면 → 인증 Client → Controller → UseCase

| 단계 | 코드와 책임 |
| --- | --- |
| Client | [음식운영Service](../../../../../SsalddelAdmin/Services/음식운영Service.cs): 세 GET과 정책 PUT. [관리자인증세션Service](../../../../../SsalddelAdmin/Services/관리자인증세션Service.cs)의 Bearer token 사용, HTTP 실패/빈 JSON 응답은 오류 |
| 계약 | [음식점리뷰관리Dtos](../../../../../Ssalddel.Contracts/Admin/Restaurants/음식점리뷰관리Dtos.cs): 단위는 원·m, 선택 픽업 배분은 null 보존 |
| 서버 | [음식운영관리Controller](../../../../../Ssalddel/Controllers/Admin/음식운영관리Controller.cs): 서버관리자 전용 정책과 V3.0 음식배달 기능/업무 메타데이터. 이전 `Ssalddel.FoodApi` 경로에서 단일 서버로 이관됨 |
| Application | [음식운영관리UseCase](../../../../../Ssalddel/Application/Admin/Restaurants/음식운영관리UseCase.cs): 수치 검증, DB 정책 조회·저장, 수정자/시각 기록 |
| 별도 계산 검토 | [음식배달지급검토UseCase](../../../../../Ssalddel/Application/Admin/Restaurants/음식배달지급검토UseCase.cs): `/payout-preview`, `/settlement-preview`는 이 페이지에서 호출하지 않으며 저장 없는 검토 API |

| 실제 호출 | 요청 | 읽기/쓰기 |
| --- | --- | --- |
| GET/PUT | `api/v1/admin/food-delivery-pricing-policy` | 정책 조회/수정 |
| GET | `api/v1/admin/restaurant-reviews` | 검토 대상 최신 최대 500개, 페이지는 그중 최대 5개 표시 |
| GET | `api/v1/admin/restaurant-reviews/policy` | 리뷰 정책 조회 |

서버 권한이 최종 경계다. 페이지 URL만 알거나 Client에 token이 있다는 것으로 수정 권한을 부여하지 않는다. 리뷰 정책 변경/조치 API와 한시 수요 할증 API는 별도 화면·Simulation 검증 범위다.

## 3. DB·원장: 정책과 동결된 배차는 별도

| 자료 | 키/관계·조회 조건 | 이 페이지의 효과 |
| --- | --- | --- |
| [음식운영정책](../../../../../Ssalddel.Domain/음식/음식운영정책.cs) / [Configuration](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Food/음식운영정책Configuration.cs) | 기본 정책 Id=1, 기본/거리/최소 요금·기사 지급액·리뷰 정책. Id 자동 생성 안 함. 한시 수요 revision은 concurrency token |
| 음식운영정책 수정 | 서버 검증 후 수치와 수정자·UTC 시각 저장 | 기존 제안에 소급 반영하지 않음. 별도 Outbox/송금 생성 없음 |
| 음식점리뷰 → 음식점공개프로필 | 리뷰의 음식점Id와 공개프로필 Id를 논리적으로 연결, 관리자검토필요여부 필터 | 읽기 전용. 주문자 ID·내용은 필요한 관리자 권한 안에서만 취급 |
| 운송실행투영 | 별도 배차 Service가 제안 시 총액·정책 판본·계산 근거를 동결 | 이 페이지는 해당 행을 수정하지 않음 |

정책 행이 없는 경우 조회는 기본 정책 객체를 반환하며 실제 수정 때 생성·저장한다. 저장 응답은 저장한 행의 DTO이며, 재진입 GET으로 다시 확인한다. nullable 픽업 배분/계산 근거 schema와 MySQL 미적용 상태는 [배차 연결 r8](../../../../AI/Planning/공통/PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL/dispatch-calculation.integration.r8.md)를 따른다.

## 결손과 검증

기존 조회 실패 후 기본 객체의 폼과 저장 버튼이 노출되던 결손, null 응답을 정상 기본값/저장 성공으로 취급하던 결손을 보완했다. 상세 오류는 서버 응답이며 사용자 입력을 성공 데이터로 대체하지 않는다. 실제 제품 로그인·운영 MySQL·브라우저 연속 조작은 미검증이다. 재현/시험 결과는 [이번 보완 결과](../../implementation-r1.md)에 기록한다.
