# 음식점 메뉴 — AI 선작성 입력 검토

상태: `Draft / HumanReviewPending / NoNewBusinessExecutionApproval`. [r9](app-production-intake.r9.md) 관리 도구의 예제이며 이 문서를 만들었다고 메뉴 업무를 새로 실행·승인한 것이 아니다.

대상 앱은 **RestaurantDeskApp Android 하나**다. 기존 연결 대장의 음식점·주문자·기사·운영자는 영향 검토 역할이지 APK 네 개를 만들라는 뜻이 아니다. 음식점 편집과 주문자 공개 메뉴 조회는 코드 근거가 있고, 기사·운영자 직접 UI 영향은 미확인이다.

- [공통 프로필](../../../../../eng/planning-inquiries/app-production/inputs/restaurant.profile.json)
- [작업 입력](../../../../../eng/planning-inquiries/app-production/inputs/restaurant-menu.intake.json)
- [검토·환경 준비](../../../../../eng/planning-inquiries/app-production/inputs/restaurant-menu.review.json)

## 이미 정해져 있어 다시 묻지 않는 부분

[메뉴 r2](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/restaurant-menu-mobile.r2.md)에 [탐색 r4](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/restaurant-navigation.r4.md)를 함께 적용한다. `/menus`에서 목록·등록·수정, 하단 주문/메뉴/가게, 새 메뉴 비공개, 입력 중 이탈 보호, 응답 미확인 중 같은 요청 재확인, 저장 성공 후 정본 재조회가 기존 범위다. 사진 업로드·옵션 그룹·삭제는 추가하지 않는다.

합성 예는 `검토 전용 메뉴 A / 합성 자료 / 7500원 / 사진 없음 / 비공개 / 품절 아님`이다. 실제 계정·주소·연락처·사진은 필요 없다.

## 이번 대조에서 드러난 차이

| 항목 | 현재 근거 | 후속에서 확인할 것 |
| --- | --- | --- |
| 입력 길이 | 화면 메뉴명100/소개2000, 서버 메뉴명200/Entity 소개1000 | 기존 데이터·계약에 맞춘 제한 조화. 이번에는 제품 수정하지 않음 |
| 사진 URL | 화면 HTTPS 검사, 서버 UseCase는 trim | 직접 API 요청의 검증 경계 |
| 재등록 방지 | 서버는 음식점+메뉴명+동일 내용 비교, 요청 ID 영속 원장 아님 | 응답 유실/동시 요청/앱 종료 후 복구를 각각 검증 |
| 합성 미리보기 | 메모리 Dictionary의 요청 ID 처리 | 서버와 판정 방법이 달라 서버 멱등성 증거로 사용 불가 |
| 수정 revision | UpdatedAtUtc.Ticks 사전 비교 | DB concurrency token 확인/동시 경합은 별도 시험. 사전 비교만으로 보장 주장 금지 |

근거: [화면](../../../../../RestaurantDeskApp/Components/Pages/Menus.razor), [UseCase](../../../../../Ssalddel/Application/Food/음식점메뉴관리UseCase.cs), [Entity](../../../../../Ssalddel.Domain/음식/음식점메뉴.cs), [DB 구성](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Food/음식점메뉴Configuration.cs), [시험 소스](../../../../../Ssalddel.Tests/Application/Food/음식점메뉴관리UseCaseTests.cs), [합성 Client](../../../../../eng/RestaurantMenuPreview/메뉴미리보기Client.cs). 여기의 대조는 코드 조사이며 현재 제품 시험 실행 기록이 아니다.

## 다음 제작에서 요구할 증거

정상 등록/수정, 응답 유실 후 중복 요청, stale revision, 저장 성공/목록 조회 실패, 다른 음식점 수정 거부를 별도 사례로 검증한다. 업무 시험·제품 Razor 화면·음식점 시험 APK는 기본 요구 결과물로 기록하되, API 연결 UI·Android 실제 화면은 각 실행 환경을 명시한다. 이전 캡처는 과거 합성 미리보기이며 이번 실행이나 네이티브 화면으로 재사용하지 않는다.

현재 입력에는 새 정책 질문이 없다. 사람 확인은 대기이며 위 기술 충돌의 후속 검토, 격리 API/DB와 Android·서명 환경 준비도 미확인이다. r9 도구 승인으로 음식점 제품 변경을 자동 실행하지 않는다.
