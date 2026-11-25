# 공통 근거 판본과 건별 배차 기록 재사용 r25

상태: `Implemented / ValidatedLocal / RuntimePending / OperationalDisabled`. 2026-10-28 사용자의 직접 요청을 `ApprovedByUser / ReadyToDispatch` 범위로 구현했다. [구현·검증 결과](../../../../Changes/2026-10-28-cargo-dispatch-evidence-library-r25.md)와 [r24](cargo-dispatch-explanation-feedback.r24.md)의 보호된 불변 추천 사건/의견 연결을 유지한다.

## 이번 완료 결과

- 공통 근거의 의미·설명·항목 종류를 불변 코드/판본/내용 SHA로 독립 catalog에 보관한다. 새 판본은 추가하며 기존 판본을 수정하지 않는다. 알고리즘·모델 버전과 설명 catalog 판본은 다르다.
- 새 추천 사건의 기존 보호 payload에 공통 판본과 당시 기록된 항목/누락 항목의 참조를 함께 저장한다. 별도 추천/승인 원장이나 개인별 문구 사본을 중복 생성하지 않는다. 접수 정보와 검증 결과를 구분하며 확인하지 않은 항목은 누락으로 남긴다.
- 제공된 실제 판정 근거에서 현재 의뢰·기사에 해당하는 입력 지문과 조회 시각/범위를 첫 await 전에 복사해 같은 보호 사건에 보존한다. 다른 후보 사전·원시 사유·점수를 복사하거나 내부 지문을 개인 안내에 노출하지 않는다.
- 기존 조회에서 정확한 판본과 건별 사실을 결합하고 기록량·누락을 보여준다. 구형 사건은 공통 판본 미연결, 판본 부재/불일치는 보류로 표시한다. 현재 정책/RAG 검색으로 과거 원인을 보충하지 않는다.
- 본인 권한을 다시 확인하는 읽기 전용 안내 자료 API를 준비한다. 저장한 사건 ID/공통 판본/표현 판본으로 개인 안내의 입력을 만들며 조회가 발송·수신 동의·검토 완료를 뜻하지 않는다. 이메일 주소·초안 저장·발송·스케줄·자동 학습·배차 변경은 포함하지 않는다.

‘접수 정보’는 추천 저장 시 접수 원장에서 관측한 값이다. 첫 Mongo CAS 때의 최초 접수 사실을 새로 확보한 것으로 표현하지 않는다. 최초 접수/전체 후보 비교·실제 알고리즘 버전이 미연결이면 그대로 남긴다. 기록 항목 수는 근거 품질·확률·선택 기여도 점수가 아니다.

## 허용 쓰기/소유권

root: `Ssalddel/Services/Dispatch/Explanation/`, 기존 Common 설명 Controller·DTO 연결, 관련 기존 시험, 이 PLAN/Changes/CURRENT_WORK/PLANNING·`artifacts/local/cargo-dispatch-evidence-r25/`. 공통 담당: 신규 `Ssalddel.Contracts/Common/Dispatch/CargoDispatchBasisCatalog.cs`, `CargoDispatchBasisDtos.cs`와 독립 catalog 회귀만 소유한다. 시험 담당: 신규 r25 서버/HTTP 회귀 partial 파일만 소유한다. 기존 r24 식별자·의견 지문·세션/복구·원본 payload·전체 dirty source를 보존하고 같은 파일을 동시에 편집하지 않는다.

## 검증

공통 판본/hash·불변 컬렉션, 새 추천과 건별 사실 결속·누락 수량·false/unknown 구분, v1 구형 호환/소급 보충 금지, 판본 불일치 보류, 안내 재조회 권한/원 업무·의견·발송 무변경과 HTTP 회귀를 확인한다. Fast/Task·공유 소비자 빌드를 수행하고 코드·HTTP 시험 인증·DB·휴대폰·실제 메일을 구분한다. 이번에는 UI 구조 변경·운영 활성화·commit/push를 하지 않는다.

최종 Task `20261028-084143`의 전체 솔루션/공유 소비 앱 빌드·16,611통과/환경17생략·실패0, 최종 관련122/122를 확인했다. 신규35는 서버17·catalog18이며 신규 실제 Kestrel HTTP6은 시험 인증/메모리 SQL/모의 store다. 내부 입력 참조의 선택 의뢰/기사 한정·원사전 변경 불변·공개 응답 비노출도 확인했다. 소스10개 해시와 HEAD를 고정했고 이전 dirty76 중 허용5개 외71개를 보존했다. 실제 제품 인증/SQL/Mongo 통합·휴대폰·메일 발송은 미검증/미실행이다.
