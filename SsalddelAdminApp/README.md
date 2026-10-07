# 관리자 모바일 음식 운영

이 앱의 음식 운영 보완은 운영자가 한 음식 주문의 진행과 중단 검토 결과를 확인하는 책임을 맡는다. [페이지 단일 책임](../docs/Architecture/WholeRoadmapPagePrinciple.md)과 [역할 앱 시각 기준](../docs/Architecture/RoleAppVisualDesignStandard.md)을 따른다.

| 페이지 | 질문·결과 | 기본 정보·주 행동 | 독립 업무·실패 복귀 |
| --- | --- | --- | --- |
| /, /overview | 지금 어떤 운영 업무를 확인할까? | 음식 주문·화물 운영의 독립 진입 | 로그인은 /login. 업무 API를 함께 조회하지 않음 |
| /food-operations | 어떤 음식 주문을 확인할까? | 음식점·주문 상태·변경 시각, 검색/목록 상세 진입 | 오류를 빈 목록으로 바꾸지 않음. 페이지/계정 변경 후 늦은 응답 폐기 |
| /food-operations/orders/{OrderNo} | 이 주문은 누가 다음 일을 처리하나? | 서버 생명주기 상태·책임 주체·배달 시도·정산, 같은 주문 새로고침 | 긴 이력은 접기. 중단 검토는 별도 경로. 조회 실패 시 행동 차단 |
| /food-operations/reviews/{OrderNo} | 중단 근거와 책임 판정을 어떻게 기록할까? | 중단 시도·근거·확인, 명시 검토 저장 | 기존 공용 검토 상태의 ID/revision/원 입력·정본 GET·401/409 정책 재사용 |
| /food-operations/demand-surcharge | 기사 배정 조건을 일정 시간 높일까? | 서버 허용 금액·시간을 조회한 뒤 명시 적용 | 화물 조회와 분리. 실패 시 정본 조회 후 같은 요청 확인, 계정 변경 후 늦은 응답 폐기 |
| /overview/cargo, /operations | 화물 개요/운송·기사 상태는 어떤가? | 기존 화물 조회·동선 유지 | 음식 할증 조회에 종속시키지 않음. 기능 OFF는 오류를 감추지 않음 |

음식 목록은 고객 주소·연락처·원시 payload 없이 주문번호·음식점·진행 상태·시각만 반환한다. 목록 GET, 기존 operations-trace와 interruption-review는 서버관리자 권한과 FoodDeliveryWorkflow를 따른다. APK의 기본 홈은 업무 진입 허브이며 다른 도메인의 API를 미리 호출하지 않는다. 화물 OFF가 음식 목록/상세를 차단하지 않고 음식 OFF도 화물 진입 자체를 차단하지 않는다.

상태·가능 행동의 정본은 서버다. 조회가 성공한 같은 주문/계정에서만 검토한다. 상태를 조회했다고 조리·배차·검토·지급을 자동 실행하지 않는다. 정산은 실제/모의/미확정을 서버 값대로 구별하고 이 화면에는 지급 실행 기능을 섞지 않는다.

페이지 → AdminFoodOperationsService → 음식주문운영추적Controller → 음식주문운영추적UseCase/음식배달중단검토UseCase → 음식주문/배차/배달시도/기사정산 원장으로 연결한다. 검토 상태는 공용 FoodDeliveryInterruptionReviewState를 웹·모바일이 같은 의미로 소비한다. 계정이 바뀌면 표시와 진행 중 요청을 폐기하고 로그인 복귀 후 정본을 조회한다.

검증은 코드/관련 시험, 격리 API·DB, 실제 Android APK, 공개 HTTPS·개인 휴대폰을 분리한다. 이번 보완의 원본/지문과 실행 근거는 Git 제외 artifacts/local/azure-mobile-hardening-r1/admin/에 보관하며 통합 검증은 총괄 작업에서 수행한다.
