# 창고 웹의 현재 구성 페이지만 빈 상태 복구

2026-11-07 · W2 구성 부분 · 통합 웹과 창고 전용 역할05의 실제 Chrome/WASM · 격리 가상 인증/읽기 자료. 코드·화면 검증과 운영 사용은 구분한다.

## 발생 조건과 변경

같은 피킹 묶음501은 출고2건·작업3개·수량40개·완료34개인데 구성 MemberPage=1의 현재 행만 비었다. 기존 **이전 내역**은 이미 활성이고 동작했다. 구성 자체가 없는지 현재 페이지만 빈 것인지 설명과 첫 구성으로 바로 돌아가는 행동이 없었으며, int.MaxValue 구성 페이지는 -2147483648페이지로 표시됐다. 서버는 전체 구성 수와 현재 페이지의 행을 따로 반환하므로 유효한 성공 응답이다.

공용 패널의 웹 단계 모드에서 성공·비로딩·오류 없음·전체 구성 양수·현재 행0일 때만 **이 페이지에 표시할 구성 내역이 없습니다.**와 **첫 구성 페이지로**를 표시했다. 기존 Member0 조회와 초점/주소 갱신을 재사용해 선택 묶음·창고·UTC 구간·공정·BatchPage·원출고 Page를 유지한다. 웹 구성 페이지 표시만 덧셈 전에 long으로 넓혔다. 기존 회차/네이티브 모드·정상 구성·전체0·이전/다음·VM·부모·읽기 API·실제 업무 상태는 보존했다.

창고 사용자가 먼저 읽을 정보는 선택 묶음과 작업 범위, 전체 구성 수와 현재 페이지, 구성별 실제 수량/상태와 기존 원출고 상세다. 새 안내는 구성 자리와 기존 페이지 행동 사이에 놓았다. 현재 빈 페이지에서 첫 구성으로 한 번에 돌아가며, 정상 구성과 기존 주 행동을 숨기지 않는다. 390px에서는 안내만큼 세로 길이가 늘어 복구 버튼까지 스크롤이 필요하다. 과업 시간 단축이나 전 역할 UI 개선을 주장하지 않는다.

## 실제 수정 전후

[같은 자료·조건의 나란한 비교](../../artifacts/local/warehouse-current-member-page-empty-20261107/comparison/comparison.html)와 [비교 원본/메타데이터 검증](../../artifacts/local/warehouse-current-member-page-empty-20261107/comparison/verification.json)은 정상 구성/현재 빈 페이지의390·1280px 네 쌍과 원본8장을 연결한다. 모두 가상 창고101·같은UTC00~03시(한국 시간09~12시)·피킹·묶음501·BatchPage3·PlanPage2·scroll0이다. 정상390 PNG는 전후 바이트가 같고 정상1280의 본문·업무 값은 같지만 PNG는 다르다. 포커스/레이아웃 위치 차이를 보정하거나 픽셀 동일성을 주장하지 않는다.

[수정 전390px](../../artifacts/local/warehouse-current-member-page-empty-20261107/comparison/originals/empty-before-390.png) · [수정 후390px](../../artifacts/local/warehouse-current-member-page-empty-20261107/comparison/originals/empty-after-390.png) · [정상 구성 전체](../assets/changes/2026-11-07-warehouse-member-page-empty/group-390-full.png). README의 후속 구성 화면은 통합 웹 정상 원본과 명시한 본문 crop이며 기존 전용 창고 웹의 정상 목록과 설명은 보존한다. [이전 목록 화면과 원본 근거](2026-11-07-warehouse-batch-page-empty.md)를 계속 찾을 수 있다.

## 검증과 한계

- 수정 전 핵심 회귀2개는 정확한 안내 부재·음수 페이지의 assertion에서 실패했다. 컴파일 실패를 제품 재현으로 쓰지 않았다.
- 신규14개를 포함한 관련 Fast184개 통과·실패0·생략0. 현재 소유2경로만 지정했다. 신규 회귀의 retained loading/error는 같은 VM 재조회와 실제 부모 렌더를 연결한 보조 검증이며 없는 웹 재조회 버튼을 실행했다고 표현하지 않는다.
- 소비 솔루션 빌드와 최종 Task17,457개 통과·실패0·환경17개 생략. Fast Tests251/Common0·Task138개 경고와 오류0을 기록했다. 전체17,474개 결과 중 생략을 통과 수에 더하지 않는다.
- 실제 통합 웹 Chrome의286개 확인과 역할05의14개 확인이 모두 통과했다. 같은 가상 인증 문맥을 역할05가 재사용했으므로 새로운 역할05 로그인 증거가 아니다. Enter/클릭 복구·상한·4완료+6대기+30완료→원출고1102 수량10→같은 구성→원래 묶음 목록, 기존 첫 묶음 복구를 대조했다.
- 통합 웹에서 정상/현재빈320·390·1280px·가로 넘침·버튼/초점·503 재시도·403·없는 묶음404·지연 로딩을 대조했다. 브라우저 fixture의 empty+batchKey는404이며 성공 MemberCount0 상세와 다르다. 전체 구성0과401·늦은 이전 응답은 회귀시험으로 확인했으며 동일 브라우저 사례 완료로 확대하지 않는다.
- 비교 HTML의 실제 Chrome 표시·이미지·상대 링크56개가 통과했다. 첫 file 주소는 제품 fixture 문맥의 의도적인 외부-origin 차단으로 열리지 않아 원본 실패 기록을 보존하고, 별도 소유 로컬 파일 전용 문맥에서 확인했다. 제품 결함이나 승인 거절로 표현하지 않는다.
- 현재 소스와 생산/통합 웹/역할05의 PDB3개, DLL/WASM·manifest와 실제 전달 자산을 결속했다. 소유 브라우저만 종료했고 기존 W1 host PID57832를 보존했다. 새 host·USB·APK·운영 인증/서버/DB·업무 상태 변경·외부 쓰기·배포·커밋·푸시는 없다.

[완료 조건](../../artifacts/local/warehouse-current-member-page-empty-20261107/acceptance.json) · [실제 웹](../../artifacts/local/warehouse-current-member-page-empty-20261107/browser-after-result.json) · [역할05 조작](../../artifacts/local/warehouse-current-member-page-empty-20261107/role05-acceptance.json) · [소스/현재 소비 판본](../../artifacts/local/warehouse-current-member-page-empty-20261107/runtime-binding-after.json) · [기존 변경 보존](../../artifacts/local/warehouse-current-member-page-empty-20261107/preservation.json) · [최종 검증 집계](../../artifacts/local/warehouse-current-member-page-empty-20261107/validation.json).

## 다음 재개

이 항목의 구현·회귀·소비 빌드·Task·실제 웹을 마감했다. 다음 최소 후보는 S2다. 판매 주문 A 조건의 성공 목록에서 B 조건만 입력하고 조회하지 않은 채 같은 주문42 상세→목록으로 돌아갈 때, 적용 조건과 미조회 초안이 섞이는지 실제 주소·조회로 대조한다. 새 정책·일괄 확정 규칙은 만들지 않으며 현재 입력으로 명시 새 조회하는 기존 S3 동작을 보존한다. [읽기 조사와 정확한 재개 사례](../../artifacts/local/warehouse-current-member-page-empty-20261107/next-resume.json).
