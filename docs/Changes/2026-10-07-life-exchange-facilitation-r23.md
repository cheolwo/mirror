# 생활 교류 약속과 선택 배송 보완

2026-10-07. 기존 생활 교류의 약속 화면에서 상태·조건·금액·전달 방법이 섞여 보이고, 직접 전달에도 기사 비용 안내가 나오던 부분을 정리했다. 지금 할 일을 먼저 확인하고, 당사자가 합의한 조건과 필요한 배송만 이어갈 수 있다.

| 작업 상태 | 변경 축 | 시각 증거 |
| --- | --- | --- |
| 미커밋 로컬 작업 | 공통 약속 작성·상세, 기사 배송 안내·복구, 항목과 값 배치 | 제품 공통 Razor·VM을 사용한 읽기 전용 예시 자료 320/390px 캡처 |

## 사용자에게 보이는 변화

- 약속 상세는 현재 상태·허용된 다음 행동 → 약속 내용 → 전달 방법 → 금액과 지급 안내 순서다. 취소·인계 정보 동의 철회는 첫 카드에 남긴다. 주 행동이 없으면 실제 배송 접수·보관 예약·상대 확인 상태에 맞는 안내를 표시한다.
- 직접 수령·직접 전달에는 기사 비용 설명을 붙이지 않는다. 기사 배송은 기존 별도 견적·동의·접수로 이어진다. 빈 금액은 미정, 명시한 0은 0원으로 표시한다.
- 배송 진행은 현재 상태와 새로고침을 먼저 보여준다. 자동 추천과 기사 수락은 구분하고, 공개 콜 단독에는 자동 추천 준비 경고를 표시하지 않는다. 응답이 불명확한 배차 방식 변경에서도 현재 기록과 원 선택 결과 확인 버튼을 유지한다.
- 약속·전달 완료를 지급 완료로 표시하지 않는다. 미확인 지급은 플랫폼 입금 미확인으로 설명하고, 기존 수금 확인 기록과 취소 상태를 보존한다.

생활 지도·선택 패널·등록/수정 route, 서버가 허용한 행동·동의 판본·취소/철회·보관 반환·문제 접수·모든 관련자의 별도 공개 동의를 재사용한다. 새 공개 API·DB 테이블·금융 확인 필드·플랫폼 결제·자동 정산을 추가하지 않았으며 음식/화물 요금이나 기사 권한도 바꾸지 않았다. 직접 지급을 법적 중개 분류의 면제 근거로 사용하지 않는다. 기존 [거래와 개인정보 보호 기준](../Architecture/CommerceIntermediaryPrivacy.md)을 유지한다.

## 대표 화면

아래는 실제 제품 컴포넌트에 읽기 전용 예시 DTO를 연결한 화면이다. 실제 업무 데이터나 배송·입금 완료 증거가 아니다.

| 장면 | 화면 |
| --- | --- |
| 직접 수령·금액 미정, 상태/행동과 조건 분리 · 390px | [화면](../assets/changes/2026-10-07-life-exchange-facilitation-r23/work-pickup-null-390.png) |
| 직접 전달·0원 유지 · 320px | [화면](../assets/changes/2026-10-07-life-exchange-facilitation-r23/work-delivery-zero-320.png) |
| 추천 중과 기사 수락 구분 · 320px | [화면](../assets/changes/2026-10-07-life-exchange-facilitation-r23/delivery-automatic-proposed-320.png) |
| 전달 완료와 직접 지급 예정 구분 · 390px | [화면](../assets/changes/2026-10-07-life-exchange-facilitation-r23/delivery-completed-unpaid-390.png) |

## 검증 결과

| 검증 | 결과와 범위 |
| --- | --- |
| Scoped Fast | 생활 교류 관련 378개 통과. 신규 31개는 금액·조건부 안내·보호/복구·실제 Razor 렌더를 검사한다. 기존 직접 전달/기사 배송·원 요청·권한 회귀 시험도 포함한다. |
| Scoped Task | 전체 3.5 제품/소비 앱 빌드 오류 0, 경고 111개. 전체 시험 8,084개 통과·실패 0, 환경 조건 Mongo 통합 시험 2개 건너뜀. |
| 화면 검증 | 17상태×320/390px, 총 34장. 가로 넘침·페이지 오류·실패 스타일 응답·업무 API 쓰기·외부 요청 0. 로딩 후 표시·금액 미정/0원·직접 전달/기사 배송·추천/수락·실패/익명 보호·원 요청 결과 조회·완료/수금 구분을 확인했다. |
| 행동 접근 | 직접 수령/전달과 자동 추천/공개 콜/병행의 두 폭, 10장면에서 현재 주 행동과 마지막 표시 진입 20개를 스크롤·포커스로 확인했다. 현재 주 행동의 조작 높이는 48px 이상이며 업무를 제출하지 않았다. |
| 최종 배치 | Task 이후 항목 열 폭과 한국어 단어 줄바꿈만 조정하고 최종 34장 렌더를 다시 확인했다. 기능 소스·컴파일 DLL과 CSS 소스의 전후 지문은 최종 렌더 기록에서 일치한다. |
| 기존 작업 보존 | 시작 기준 12,781개 파일 지문과 비교했다. 범위 밖 변경/추가·삭제·staging 없음, branch와 HEAD 유지. |

로그는 `artifacts/local/validation/20261007-092337`(Fast), `20261007-092428`(Task), `artifacts/local/life-exchange-facilitation-r23/ui-verified/verification.json`(최종 화면), `reachability.json`(행동 접근), `scope-audit.json`(작업 범위)에 있다. 일반 preview 빌드는 경고/오류 0이며 제품 DLL과 preview DLL의 해시 일치를 확인했다. 처음 Playwright에 대응하는 bundled 브라우저가 없어 설치된 Chrome을 독립 headless 프로필로 사용했다.

현재 화면 검증의 후보 대기는 확정 기사 없는 Queued DTO의 표현이다. 실제 서버의 후보 탐색·재탐색을 실행한 증거가 아니다. 기존 [r22 서버/DB 다섯 완료 흐름](2026-10-06-neighborhood-flexible-delivery-r22.md)은 이전 증거로 유지한다. 이번 작업에서 이를 새로 실행하거나 현재 입금·실제 운행으로 승격하지 않았다.

MAUI·Web는 같은 공통 Razor를 소비하며 전체 소비 앱 컴파일을 확인했다. 개인 휴대폰 설치, Azure HTTPS, 실제 지도/GPS, 실거래·운행·입금, 운영기관 연동과 법적 적합성 인증은 별도다. Figma `01 Community`는 기존 생활 지도/약속/배송 route와 대응하지만 연결된 Figma node 도구가 없어 디자인 파일 동기화는 확인하지 못했다. 대표 PNG는 Web 렌더이며 단말 캡처가 아니다.

## 구현 연결

[승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/life-exchange-facilitation.implementation.r23.md)과 [페이지 책임 카드](../ProjectOverview/page-docs/neighborhood-collaboration-r1.md#생활-교류-약속과-선택-배송-보완-r23)를 따른다. 제품은 기존 `NeighborhoodCollaborationCreate/Detail`, `NeighborhoodDeliveryCreate/Detail`, 두 Presentation helper와 `neighborhood-exchange.css`만 수정했다. 의미 있는 회귀 시험은 `NeighborhoodCollaborationFacilitationRenderTests`와 `NeighborhoodDeliveryFacilitationPresentationTests`이며, 별도 읽기 전용 렌더 호스트는 `eng/RoleWorkspacePreview/LifeFacilitationPreview*`와 `verify-life-facilitation.mjs`다.

검증기는 루프백 전용 `--LifeFacilitationPreviewPort=5567`으로 실행한다. 모든 fixture 변경 명령을 거절하며 기존 5392 검토 경로·fixture는 보존한다. 최종 검증이 사용하는 Node, `SSALDDEL_PLAYWRIGHT_CHROMIUM`, `SSALDDEL_LIFE_PREVIEW_OUTPUT`은 실행 환경 설정이며 제품 계약이나 운영 정책이 아니다.
