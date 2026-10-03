# 페이지 단일 책임 기준과 UI·UX 작업 연결

| 커밋 | 변경 | 검증 수준 |
| --- | --- | --- |
| 커밋 전 | 기존 페이지 원칙 보강·AGENTS 참조·책임 카드·최근 세 화면 수동 점검 | 화면 변경 없음. 문서/지침 검증이며 새 앱 실행·제품 build 아님 |

[기준 문서](../Architecture/WholeRoadmapPagePrinciple.md) · [책임 카드](../ProjectOverview/page-docs/page-responsibility-template.md) · [최근 화면 점검](../ProjectOverview/page-docs/page-responsibility-review-r1.md)

단일 책임은 사용자의 목적과 완료 결과로 판단한다. 접기만으로 독립 업무 분리가 완료되는 것으로 취급하지 않는다. 기존 기사 화면의 정산/내 정보와 관리자 지급 테스트의 분리 필요를 명시하고, 주문자 목록/선택 상세/수령 확인은 같은 목적의 구성으로 기록했다. 기존 route·ID·코드·원장과 r2 화면을 보존한다.

이번 경로의 문서 Fast·diff·새 로컬 파일 참조와 r2 소스 SHA-256 일치를 검사한다. 과거 UI/시험 증거는 [r2 기록](2026-10-03-ui-user-simplification-r2.md)의 범위에서만 재사용하며 전체 페이지 준수 인증으로 확대하지 않는다.

검증: 소유 10경로 문서 Fast `20261003-112249` 통과, 로컬 파일 참조 13개 유효·r2 소스 18개 지문 일치. 상세 결과는 Git 제외 `artifacts/local/page-responsibility-policy-r1/verification.json`이다. 제품 build/시험은 문서 전용 검사에서 생략했다.
