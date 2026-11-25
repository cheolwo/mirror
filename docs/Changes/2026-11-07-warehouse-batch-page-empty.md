# 창고 웹의 현재 묶음 페이지만 비었을 때 첫 페이지 복구

선택한 창고·구간·공정에 처리 묶음이 남아 있어도 현재 페이지의 행만 비면 별도 설명 없이 페이지 번호만 남았다. 웹 단계 묶음 목록에 현재 페이지 안내와 **첫 묶음 페이지로**를 추가했다. 기존 **이전 묶음**도 이미 동작했으며 그대로 유지한다.

| 기준 | 대상 | 변경 경계 |
| --- | --- | --- |
| `dev/mirror-integration` · HEAD `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86` · 로컬 미커밋 | 통합 웹과 역할05 창고 웹의 `StageEntry=true` 처리 묶음 목록 | 기존 조회·주소·권한·공정과 원출고 연결을 유지 |

## 발생 조건과 작은 변경

가상 창고101·11월7일 한국 시간 09:00~12:00·피킹 공정에서 전체 묶음은 2개지만 `BatchPage=3`의 `Groups=[]`인 성공 응답을 실제 Chrome에서 확인했다. 기존에는 `4페이지 · 전체 2개`와 이전/다음 버튼만 보였다. `BatchPage=int.MaxValue` 주소에서는 표시 덧셈이 넘쳐 `-2147483648페이지`가 됐다. API는 요청 페이지와 전체 수를 그대로 반환하는 기존 계약이다.

[공용 패널](../../Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelOutboundWorkflowPanel.razor)의 웹 단계 진입에서, 로딩·오류가 없고 전체 묶음이 양수이며 현재 행만 비었을 때 다음을 표시한다.

- **이 페이지에 표시할 처리 묶음이 없습니다.**
- 선택한 창고·조회 구간·공정은 유지하고 첫 페이지를 다시 확인해 주세요.
- **첫 묶음 페이지로**: 기존 `ChangeBatchPageAsync(0)`과 주소 갱신을 사용한다. 창고·UTC 구간·공정·원출고 페이지를 유지하며 자동 조회나 업무 처리를 추가하지 않는다.

웹 단계의 페이지 표시만 더 넓은 정수로 덧셈해 상한 주소도 `2147483648페이지`로 읽는다. 기존 이전/다음, 정상 표, 전체 0건, 입력 변경에 따른 기존 결과 무효화와 실패 재시도는 보존했다. [보존 대조](../../artifacts/local/warehouse-current-batch-page-empty-20261107/preservation.json)에서 새 안내 블록과 번호 표현만 되돌리면 기존 dirty 패널 전체와 같고 VM·부모는 바이트가 동일하다.

## 창고 역할의 정보와 같은 업무 확인

이 화면의 질문은 ‘작업이 없는지 현재 페이지만 비었는지 구분하고 같은 작업 범위의 첫 페이지로 돌아갈 수 있는가’다. 선택 창고·구간·공정, 실제 묶음/구성 수량, 현재 페이지와 전체 수를 우선한다. 기존 공정 선택과 수량 설명·완료/연결 미확인을 유지하며 판매자의 검색·초안 적용 방식을 가져오지 않았다.

정상 가상 묶음 `picking:batch:501`은 출고2건·작업3개·수량40개·완료34개다. 같은 원출고1102의 작업4개 완료+6개 대기가 상세의 수량10개와 `VIRTUAL-SKU-1102`·`VIRTUAL-ORDER-1102`로 이어지고, 구성과 목록 복귀에서 같은 창고·구간·공정·원출고 페이지가 유지됨을 수정 전후 대조했다. 실제 배정·피킹 완료·운송 인계를 실행한 결과는 아니다.

## 수행한 검증과 전후 결과물

[신규 실제 Renderer 회귀](../../Ssalddel.Tests/Ui/Common/OutboundBatchPageEmptyRecoveryTests.cs)의 핵심 두 사례는 수정 전에 안내 부재와 음수 번호로 실패했다. 수정 후 신규14개를 포함한 관련 Fast **170개 통과·실패0개**, 통합 소비 빌드와 Task **17,443개 통과·실패0개·환경 의존17개 생략**이다. Fast Tests 빌드 경고249개·Common 경고0개, Task 빌드 경고138개·오류0개를 [코드 검증](../../artifacts/local/warehouse-current-batch-page-empty-20261107/code-validation.json)에 구분한다. 정상·전체0·첫 페이지의 항목만0·로딩·503의 이전 결과·401/403·기존 회차 모드와 이전 이동을 보호한다.

설치된 실제 Chrome/WASM·라우터·버튼·입력과 격리 가상 응답으로 통합 웹 **175개**, 역할05 창고 웹 **8개** 확인을 마쳤다. 첫 페이지 버튼의 Enter/클릭, 같은 묶음·원출고 상세·목록 복귀, 상한 번호, 전체0·503 재시도·지연 로딩·미조회 날짜 입력의 기존 결과 무효화와 입력 보존을 대조했다. 320px·390px·1280px의 가로 넘침과 버튼 초점, 조회 조건 펼치기도 확인했다. 역할05는 같은 출처의 기존 가상 인증 문맥을 재사용했으며 새 역할05 로그인 자체를 검증한 것은 아니다.

[수정 전후 비교 열기](../../artifacts/local/warehouse-current-batch-page-empty-20261107/comparison/comparison.html): 같은 가상 자료·주소·조회 응답·스크롤0의 정상/현재 빈 페이지를 390px·1280px **4쌍**으로 보존했다. [원본·조건 대조](../../artifacts/local/warehouse-current-batch-page-empty-20261107/comparison/verification.json)와 [실제 비교 페이지 표시 검사](../../artifacts/local/warehouse-current-batch-page-empty-20261107/comparison/presentation/verification.json)를 연결한다. 정상 본문·수량·행 순서는 같지만 포커스 표시와 넓은 화면의 약4px 위치 차이가 있어 PNG 바이트/픽셀 동일성을 주장하지 않는다. 원본은 보정하지 않았다. 실제 과업 시간을 측정하지 않았으며 사용 시간 단축도 단정하지 않는다.

초기 검증 도구의 7초 대기는 Debug WASM의 새 문서 로드보다 짧았다. 이후 실제 화면이 정상 로드되고 페이지 오류가 없음을 확인해 도구의 유한 대기만 30초로 조정했다. 최초 실패 결과와 원래 도구는 보존했고 제품 코드를 추가 수정하거나 호스트를 재시작하지 않았다. [현재 소스·PDB·소비 DLL·WASM 결속](../../artifacts/local/warehouse-current-batch-page-empty-20261107/runtime-binding-after.json)과 실제 브라우저 검증은 별도 근거다.

## 범위와 다음 한 가지

이 표시는 웹 `StageEntry=true`에 한정한다. 기존 회차 `StageEntry=false`의 내용과 동작은 회귀로 보호했고 통합 소비 빌드를 수행했다. 물리 휴대폰·APK·운영 인증/서버/DB·업무 상태·외부 쓰기·커밋·푸시는 실행하지 않았다. 기존 W1 호스트와 다른 작업은 보존하고 소유 Chrome만 종료했다.

다음은 **선택 묶음의 구성 MemberPage만 빈 경우**다. 같은 묶음의 `MemberCount>0`·`Members=[]`와 기존 이전 내역/복귀·번호 경계를 다시 확인한다. 이번 목록 복구를 구성 페이지 복구 완료로 확대하지 않는다. 최종 연결 근거는 [validation.json](../../artifacts/local/warehouse-current-batch-page-empty-20261107/validation.json), 이전 화면과 로그인 복귀는 [W1 기록](2026-11-07-warehouse-login-return.md)에 유지한다.
