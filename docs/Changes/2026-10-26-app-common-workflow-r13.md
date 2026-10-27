# 앱 공통 제작 절차와 화물 첫 적용 자료

앱 개발 요청이 업무 한 건의 준비부터 역할 간 인계·복구·검증 마감까지 이어지도록 기존 제작 입력과 통합 1.0 마감표를 연결했다. 정본은 [공통 제작 안내](../AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md)이며 [승인한 범위](../AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-common-workflow.r13.md)와 [결과](../AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-common-workflow.r13.result.md)를 따른다.

## 변경

- AGENTS와 제작 도구 사용법·통합 1.0 마감에서 공통 안내를 참조한다. 입력/출력·통과·실패 복귀, 같은 소스/서버/APK, 미확정 결과의 확인과 재개 조건을 작업 카드에 남긴다.
- 기존 app-production에 화주·창고 설명 역할과 필터를 추가했다. 기존 4역할·식별자·입력 형식을 유지하고 실제 앱 대상/권한과 구별한다.
- 현재 검증/포장 의존과 기본 7앱 계약에 맞춰 시험 fixture를 보수했다.
- 화물의 화주·전문 기사·운영자 프로필/입력/검토 초안을 기존 관계 대장에 연결했다. 새 관리 DB·단계 자동 승격·업무 실행 엔진은 만들지 않았다.
- 옛 3.5 제품 목표 표현을 통합 1.0과 호환 분류로 정리했다. 실제 외부 운영 공개는 r105 통합 조건을 유지한다.

## 검증

전용 관리 도구 시험 97/97·실패/생략 0, 생성기 Write/Validate·관계 Error 0, 변경 링크 57개 누락 0, 범위 Fast의 diff 검사 통과. 실제 로컬 브라우저 1280×720에서 역할별 기록·미검토/NotRun·창고 빈 상태와 가로 넘침 없음을 확인했다. 원 화면은 `artifacts/local/app-common-workflow-20261026-r13/browser-viewer-review.png`에 보존한다.

화물의 세 입력은 준비 정보 `Ready`와 사람 검토 `Pending`·환경 `Unknown`을 구분해 예상 IntakeCheck 종료 3을 유지했다. 관리 도구 검증은 실제 제품 API/DB·APK·실휴대폰·운영 증거가 아니다. 기존 원장·서비스·기기·개인자료·다른 변경과 과거 검증은 보존했다. commit/push·배포는 수행하지 않았다.
