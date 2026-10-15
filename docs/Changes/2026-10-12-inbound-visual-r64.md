# 입고·공간·보관 협의 화면 정돈 r64

상태: `Implemented / LocalValidated / OperationalDeferred`.

사용자는 r63 화면의 긴 텍스트 나열을 줄이고 아름답게 정돈하되 색이 들어간 이모지는 피하도록 요청했다. [수용 설계](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.implementation.r64.md)·[작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.r64.work-order.json)에 따라 기존 공간 조건·입고 협의·보관 조건의 외형을 다듬었다. [r63 업무 구현](2026-10-12-supplier-space-inbound-r63.md)과 [페이지 책임](../ProjectOverview/page-docs/distributed-inbound-collaboration-r63.md)은 유지한다.

## 화면에서 달라진 점

- 기존 Material Outlined 아이콘을 단색으로 사용한다. 상자·공간·일정·당사자·동의·수정·기록을 짧은 제목/버튼과 함께 표시하고 장식 아이콘은 보조기기에서 중복 읽지 않는다. 컬러 이모지·새 이미지·외부 폰트/아이콘 패키지는 추가하지 않았다.
- 현재 상태, 예정 수량과 실제 입고 수량, 다음 담당과 대기 이유를 먼저 읽는다. 수량은 숫자 축을 맞추고 예약과 실입고를 합치지 않는다.
- 물품 정보·일정과 보관·공급과 책임·양측 동의를 얇은 구분선과 여백으로 구분한다. 짧은 항목은 2열, 긴 조건은 전체 너비로 표시하며 320px에서는 읽는 항목과 입력을 1열로 되돌린다.
- 공간의 허용·거부 품목은 작은 중립색 칩으로 분리하고 용량과 출입 조건을 묶는다. 현재 보관 조건과 미확정 변경안도 제목부터 구별한다.
- 주 버튼, 보조 행동, 기존 거절/취소·메모·기록 접기를 정돈한다. 확인 체크는 사용자가 직접 해야 하며 권한·판본·사진·승인 조건은 바꾸지 않는다.

새 스타일은 기존 distributed-work 안의 inbound-visual 클래스로 범위를 제한한다. Community·다른 역할 앱·전체 UI 테마 변경을 주장하지 않는다. 서버·API·DB·VM·계약·정산 계산과 기존 영수증 복구 코드는 수정하지 않았다. 기존 r63 화면과 캡처는 별도 파일로 보존한다.

## 검증

- Fast: 기존 동의·권한·부분 수령·화면 회귀 시험 74건 통과, 실패/건너뜀 0건. 기록: artifacts/local/validation/20261012-200138/.
- Task: 전체 시험 11,167건 통과, 실패 0건, 환경 의존 MongoDB 시험 15건 건너뜀. 전체 Ssalddel.v3.5.slnx 및 소비 앱 빌드 오류 0개, 경고 144개. 기록: artifacts/local/validation/20261012-200533/. 경고가 없는 빌드로 보고하지 않는다.
- 실제 Razor HtmlRenderer 출력에 공통 foundation과 소스 CSS를 적용해 로컬 Edge 320·390·768px의 3가지 화면, 총 9개 배치를 확인했다. 가로 넘침·48px 미만 버튼/선택 입력·브라우저 오류 0건, 키보드 포커스 및 단색 SVG 아이콘을 확인했다. 대표 390px PNG 3장도 직접 검토했다.
- 기존 r63 서버·계약·도메인·VM·복구 등 보호 소스 31개는 해시가 같다. 새 시각 변경의 제품/시험 소스 11개는 artifacts/local/inbound-r64/final-product-source-fingerprints.json에 결속한다.
- 화면은 시험 자료를 사용하는 실제 Razor의 정적 렌더다. 인증 HTTP·물리 휴대폰·네이티브 카메라·실제 DB 실행 검증으로 확대하지 않는다. 화면과 제어를 보존해 총 스크롤 길이가 줄었다고 주장하지 않는다.

브라우저·소스 보존·문서 링크와 최종 차이 검사 기록은 artifacts/local/inbound-r64/에 보관한다.

대표 화면:

- [공간 수용 조건](../assets/changes/2026-10-12-inbound-r64-policy.png)
- [입고 협의](../assets/changes/2026-10-12-inbound-r64-collaboration.png)
- [현재 계약과 변경안](../assets/changes/2026-10-12-inbound-r64-pending-terms.png)

기본 OFF·Simulation은 그대로다. APK 설치·물리 휴대폰·Azure·공유 DB 적용·실제 계약/지급·Unity·commit/push는 이번 시각 변경에 포함하지 않았다.
