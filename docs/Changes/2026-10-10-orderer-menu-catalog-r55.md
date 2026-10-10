# 주문자 메뉴 사진·기록 자료 연결

2026-10-10. [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-ORDERER-MENU-CATALOG/orderer-menu-catalog.r1.md)와 [페이지 책임](../ProjectOverview/page-docs/mobile-workflow-orderer-restaurant-r1.md#메뉴-사진기록-자료-표시-책임-2026-10-10)을 따른다.

## 문제와 변경

서버와 기존 공개 메뉴 계약은 대표 이미지 주소를 제공하지만 주문자 음식점 상세 목록에서 소비하지 않았다. 같은 메뉴 행에 사진을 연결하고 누락·실패 때도 메뉴명·설명·판매가·품절·수량 선택을 유지한다.

개인 배달 기록과 수집한 공식 사진은 별도 로컬 메뉴 검토에 연결한다. 실제 건·지점·메뉴의 연결을 유지하고 표시 항목금액·주문총액을 현재 판매 단가로 변환하지 않는다. 사진은 현재 브랜드 메뉴의 참고 사진이며 실제 지점의 제공품·옵션·과거 판본·공개 사용 조건 확인과 구별한다. 이 화면에 주문 제출을 연결하지 않는다.

## 데이터와 실행 경계

- 확정 원장 574건과 메뉴 조사 자료는 읽기 전용으로 가져오고 원본 해시를 보존한다.
- 실제 가져오기는 391지점·574주문·494메뉴·511관측(구조화 497항목·주문별 요약 14항목)을 보존했다. 명시 단가는 0건이며 표시 항목금액 121건·주문총액 301건을 별도 nullable 값으로 유지했다.
- 같은 메뉴명으로 연결된 사진 후보는 123메뉴·146연결·117파일(34,471,313바이트)이다. 구성품/모호 후보 27연결과 미확보 2연결은 제외했다. 사진의 옵션·실제 지점·과거 상품 판본·공개 사용 승인은 확정하지 않는다.
- 사진은 정확한 메뉴 연결 후보만 `artifacts/local/`에 보관한다. 제품 정적자산·운영 DB로 복사하지 않는다.
- 로컬 검토는 루프백·Simulation·파일/해시 허용 목록을 따르며 외부 API·실제 주문·금전·알림을 실행하지 않는다.
- 기존 메뉴 API·DB·주문 계산·인증·품절·서버 가격 재확인을 유지한다.

## 검증 상태

가져오기 회귀시험 24개가 통과했다. 실제 자료 2회 가져오기의 출력 SHA-256은 `449503ee5f7f67ec873d3fd3d4646e040e7e1c7ff0c145a70221e452c2224047`로 같으며 원본 192파일 변경이 없고 복사 사진 117파일의 해시가 일치한다. 원본·출력의 모든 관측 값과 주문합계 연결을 독립 대조했다. 증거는 Git 제외 `artifacts/local/orderer-menu-catalog-r55/catalog.json`, `import-proof.json`, `root-independent-check.json`이다.

공통 UI 시험 74개와 보안·카탈로그 공급자 시험 33개를 포함한 최종 scoped Fast 219개가 통과했다. 사진 추가로 좁은 화면의 메뉴 행이 사진+본문 두 열이 되었으므로 기존 구조 시험의 한 열 기대값을 해당 배치와 가격의 본문 아래 배치로 정정했다. 첫 실패와 수정 후 통과 기록을 별도로 보존한다.

소비 호스트 빌드와 실제 320/390/1280px 화면을 확인했다. 자료 정상·미설정·손상·Operational 금지의 4종×3폭 검증 12장면이 통과했으며 사진 누락/로드 실패·음식점/메뉴/옵션 검색·주문 버튼 부재·외부 업무 API 0회·가로 넘침 0을 확인했다. 실제 기록 511관측의 연결·금액과 합성 판매 메뉴의 기존 수량/품절 계산을 따로 검증했다. 합성 판매가 4,000/6,000원은 관측 원장의 가격이 아니다. 실행 소스·바이너리 변경 없음과 로컬 이미지 응답 SHA를 대조했다. 화면 증거는 `artifacts/local/orderer-menu-catalog-r55/ui/verification.json`과 `ui/{missing,invalid,operational}/verification.json`이다.

최종 전체 앱 `Ssalddel.v3.5.slnx` 빌드와 Task 실행 9,886개가 통과했다(실패 0, Mongo 환경 시험 15개 미실행, 총 9,901개). 첫 `-NoRestore` 실행은 이전 기사 앱의 일부 플랫폼 자산 부족(`NETSDK1047`)으로 중단했으며 정상 복원 후 다시 통과했다. 기존 소스나 플랫폼 구성을 삭제해 우회하지 않았다. Fast 증거는 `artifacts/local/validation/20261010-125453/`, 최종 Task는 `artifacts/local/validation/20261010-130105/`이며 Blob 준비 Python 시험은 별도 `blob-prepare-tests-final.log`다. Task 실행 23개 코드 경로와 이후 추가한 Python 준비 도구의 독립 시험을 구별한다.

## 기존 Azure Blob 재사용

후속 요청에 따라 `Ssalddel/appsettings.json`, `AzureBlobStorageOptions`, 기존 `AzureBlobStorageService`, `deploy/azure-vm/compose.yaml`, `docs/Deployment/AzureLowCostVm.md`를 대조했다. 기존 배포는 `ObjectStorage__Provider=AzureBlob`, 공개 `community-public`, 비공개 `platform-private`이며 계정 이름은 VM 비공개 `.env`의 `SSALDDEL_STORAGE_ACCOUNT_NAME`으로 주입한다. 로컬 기본 `ServiceUri`는 비어 있으므로 계정 주소를 만들어 넣지 않았다. 기존 저장 어댑터와 `UploadImmutableAsync` 후 `DownloadAsync` 원격 바이트 SHA 대조를 재사용할 수 있다.

현재 Azure CLI 인증은 만료됐다. 기존 배포 FQDN은 DNS 조회가 실패했고 기록 IP는 저장된 SSH host key와 달라 접속을 중단했다. 호스트 신뢰 기록·인증·과금·리소스·컨테이너 ACL을 변경하지 않았다. 현재 계정 주소·컨테이너 실존·비공개 ACL·업로드 성공은 미확인이다. 이 조사 결과는 Git 제외 `azure-reuse-audit.json`에 기록했다. 계정·컨테이너 이름을 사용자에게 다시 입력하도록 요구하지 않고 인증된 기존 설정 조회를 남긴다.

`eng/RoleWorkspacePreview/prepare-menu-blob-archive.py`는 검증된 기존 가져오기 증거·카탈로그·사진의 해시를 결속한 비공개 보관 명세를 만든다. 117파일·146메뉴 연결·123메뉴·57지점·34,471,313바이트이며 같은 파일은 `menu-reference/<SHA-256>.<검증된형식확장자>` 하나로 보관 준비한다. 메뉴별 첫/후속 사진은 자료 순서의 대표/상세 **후보**일 뿐 운영 대표 확정이 아니다. `serviceUri/containerName=null`, 기존 `platform-private` 기본 이름 관측, 실제 접근/비공개 ACL 미검증과 `NotUploaded`를 유지한다. 명세 SHA-256은 `59231741b81e365eaa6ac2ab064c6fcc911a89c17cf329d35d00ef9acd8ff582`다.

기존 명세와 다르면 덮어쓰지 않고 실패하며 원자적 no-replace 생성으로 동시 생성도 보호한다. 이 도구는 네트워크·업로드 코드가 없으며 실행 증거를 대신 인증하지 않는다. 후속 SDK 실행·원격 SHA 검증·권한 있는 비공개 사진 제공 계약은 `eng/RoleWorkspacePreview/menu-blob-archive-README.md`에 기록했다. 기존 대표 사진 단일 URL 계약을 다중 상세 갤러리 계약으로 확장하거나 운영 DB/메뉴에 Blob URL을 등록한 것은 아니다.

사용자는 인증 복구 질문에 `지금은 로컬 준비까지만`으로 범위를 확정했다. 실제 업로드·계정/컨테이너 실시간 조회는 이번 완료 조건에 포함하지 않는다. Blob 준비 회귀시험은 31개 통과, 실제 symlink 생성 권한 시험 1개 환경 미실행이며 Windows reparse 거부·동시 생성 보존 시험은 통과했다. 실제 준비 재실행은 동일 명세이고 입력 120파일(사진 117·카탈로그·가져오기 증거·정본 원장)은 변경되지 않았다. importer proof 결속 전의 최초 자체 초안은 `blob-archive-manifest.pre-proof.draft.json`으로 분리 보존했다.

휴대폰·APK 설치·운영 DB·실제 Azure 업로드·외부 공개·Unity·배달 영상 변경·commit/push는 수행하지 않았다. 개인 수집 후보는 비공개 보관 준비와 운영 판매 메뉴의 공개 승인을 구별한다.
