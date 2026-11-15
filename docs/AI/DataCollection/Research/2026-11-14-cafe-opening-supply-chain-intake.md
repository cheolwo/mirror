# 2026-11-14 카페 개업·공급망 자료 수집과 절차 색인

사용자가 커피 가맹점의 원두·기계·공간/인테리어·공급/설치 과정을 공공자료로 수집·색인하고, 원형 절차 노드에서 관련 지도로 이동/강조할 준비를 요청했다. 실제 자료 저장과 [r190 기획 제안](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/cafe-opening-supply-chains-and-map-linked-process.proposal.r190.md)을 분리했다. 게임 구현은 하지 않았다.

## 실제 저장 위치와 수량

- [자료 보관함](../../../../artifacts/local/cafe-opening-intake-20261114/README.md) · [통합 출처/파일 JSON](../../../../artifacts/local/cafe-opening-intake-20261114/catalog.json)
- [로컬 출처·절차 조회 SQLite](../../../../artifacts/local/cafe-opening-intake-20261114/source-catalog.sqlite) · [자료 공백](../../../../artifacts/local/cafe-opening-intake-20261114/gaps.json)
- [수입 분야 원문/상태 색인](../../../../artifacts/local/cafe-opening-intake-20261114/imports/index.json) · [시설·설비 분야 색인](../../../../artifacts/local/cafe-opening-intake-20261114/facilities/index.json)
- [출처/지문 영속 명세](2026-11-14-cafe-opening-supply-chain-intake.json) · [독립 검증](../../../../artifacts/local/cafe-opening-intake-20261114/verification.json)

공식 신규 원문은 **14개·2,466,867바이트**다. 실제 수치 원본2개, 메타데이터8개, 안내4개로 나눴다. 기존12개 고유 자료 참조는 지문으로 확인하고 재다운로드/복사하지 않았다. 이26참조에는 파생 참고자료도 포함되어26개 고유 공공데이터셋이나26회 수집 성공을 뜻하지 않는다.

| 분야 | 실제로 확보/재사용한 것 | 확인 상한 |
| --- | --- | --- |
| 원두 수입 | 신규2025년 한국 수입 관측9행: HS0901 세계+선택4상대국5행, 선택HS6 형태4행 | 연간 상품/국가 집계. 특정 농장·항만·선박·수입업체/납품처·실제 배송망 아님 |
| 분류/국가 | 신규 공식 국가/무역지역310행, HS2022 6940행·커피 부분집합5분류 | 코드 사전. 수입 관측 수와 합산하지 않음. HS와 국내HSK·조달코드·카페SKU는 서로 다른 식별체계 |
| 점포 문맥 | 기존4구 자료에서 정상 기록·업태커피숍인 인허가1743관측 추출 | 중랑307·광진528·동대문415·노원493. 고유 물리 카페·현장 영업·가맹/관리·입점 가능 확정 아님 |
| 국내 물류 | 기존 NLIC 등록창고6011행 원본 재사용 | 원본 SHA와 기존 BIFF 구조 감사 동일성 확인. 이번 새 행 재추출/커피 취급거점 배정은 아님 |
| 기계·제조·가맹 | 신규 공장/공장통계·가맹 부담금·조달 물품 카탈로그4개, 기존 제조사3원문·조건부16참고사실 | 공장/모델/가격/재고/브랜드별 실제 데이터 관측은 새로 취득하지 않음. 제조사 자료는 공공데이터와 구별 |
| 건물·절차 | 신규 건축/정보공개서 안내2개, 건축 용어정의12개, 통관/해외제조업소 등록 안내2개 | 정의/절차 참고. 기존 건축8동 표본은 학교부지여서 카페 후보 제외. 품목/원산국별 최신 요건 전체 확인 아님 |

수치 관측·코드 사전·인허가 기록·필드 정의·참고사실은 다른 단위이므로 하나의 총 관측행으로 합산하지 않는다.

## 수입 통계의 근거와 제한

[UN Comtrade 공식 무인증 preview](https://comtradeapi.un.org/public/v1/preview/C/A/HS?period=2025&reporterCode=410&cmdCode=0901&flowCode=M&partnerCode=0%2C76%2C704%2C170%2C231&partner2Code=0&customsCode=C00&motCode=0&maxRecords=500)에서 HS0901 한국 수입의 세계/선택 상대국을, 별도 요청에서090111·090112·090121·090122의 세계 집계를 저장했다. 원문2JSON과 [원형 CSV](../../../../artifacts/local/cafe-opening-intake-20261114/imports/coffee-import-observations.csv)·[참조 연결 CSV](../../../../artifacts/local/cafe-opening-intake-20261114/imports/coffee-import-observations.indexed.csv)를 보존했다. 국가·품목 이름이 원문에서 null인 경우 [공식 참조표](https://comtradeapi.un.org/files/v1/app/reference/H6.json)를 별도 열에 연결하고 원문을 덮지 않았다.

세계HS0901 값은1,699,151,318USD·순중량201,293,929.331kg다. 원문 `isReported=false / isAggregate=true` 및 추정 플래그를 유지했다. 세계+파트너·HS4+HS6는 중복 범위다. HS0901에는 선택4개HS6 밖의 분류도 있으며4개를 전체 분해로 표시하지 않는다. 선택국(브라질·콜롬비아·에티오피아·베트남)은 표본이며 순위/전수 원산지 목록이 아니다. 해당 통계로 개별 구매단가·계약·항만/창고·실시간 이동을 생성하지 않는다.

[관세청 수입통관 일반 안내](https://customs.go.kr/kcs/cm/cntnts/cntntsView.do?cntntsId=817&mi=2819), [식약처 해외제조업소 등록 안내](https://www.foodsafetykorea.go.kr/portal/board/boardDetail.do?bbs_no=bbs300&menu_grp=MENU_NEW01&menu_no=4458&ntctxt_no=1058518)를 실제 저장했다. 후자는 국내 점포 영업신고 안내가 아니다. 생두·볶음·추출/혼합제품별 현행 원산국/가공도 요건과 검역/식품검사·세율 전부는 미확인이다. HSK 전체 페이지와 신고Q&A는 응답 크기 상한 초과, 검역본부 자료는 DNS 실패로3출처가 미저장 상태다. 차단 우회/계정 인증을 하지 않았다.

## 시설·기계·공간의 근거와 제한

[전국등록공장15105482](https://www.data.go.kr/data/15105482/fileData.do)·[공장통계15085508](https://www.data.go.kr/data/15085508/fileData.do)·[가맹 부담금15125475](https://www.data.go.kr/data/15125475/openapi.do)·[조달 물품목록15129417](https://www.data.go.kr/data/15129417/openapi.do)의 카탈로그 JSON을 저장했다. 전체 공장/브랜드/물품 본문을 취득한 상태는 아니다. 공장 카탈로그의 자료명 기준2025-12-31과 수정일2026-09-23도 서로 다른 필드로 보존했다.

[건축HUB 총괄표제부 정의](https://www.hub.go.kr/portal/opn/tyb/idx-bdrg-totldr.do)에서 필드12개를 정리했다. 기존8동 자료는 학교부지 표본으로, 카페나 상가의 실제 공간/입점 후보로 확장하지 않았다. [정부24 정보공개서 등록](https://www.gov.kr/mw/AA020InfoCappView.do?CappBizCD=11300000028&HighCtgCD=A09006)은 본부의 해당 절차 참고이며 모든 가맹점주의 필수 개업 노드로 붙이지 않는다.

기존 La Marzocco·Scotsman 원문은 [이전 수집 보고](2026-11-14-management-research-field-intake.md)의 지문으로 참조했다. 특정 해외 모델의 추출/전력·급배수/조건 등 참고이며 국내 모델 동일성·가격/현재 재고·설치 견적/실측도면·공장 소재지를 증명하지 않는다.

## 점포 후보와 지도 결속

기존 인허가 정리92655기록을 지문 확인 후 재조회하여 `OA-23059 + 커피숍 + OperatingReported`인1743관측을 추렸다. 원본/행 번호·업태/상태·주소·법정동 결속 방식·행정동 후보를 유지했다. 행정동 관계는 다중후보1433·유일후보309·미해결1로, 유일후보도 현재 공간 경계 결속의 확정을 뜻하지 않는다. 원본 좌표계가 미확정이므로 이번에는 좌표를 변환하거나 지도에 배치하지 않았다.

등록창고6011행 역시 커피 물류망·현재 가동·거래/재고를 뜻하지 않는다. 실제 커피 공급기업/공장/창고/점포 사이의 확인된 거래·운송·설치 간선은 **0개**다. [제안 절차 색인](../../../../artifacts/local/cafe-opening-intake-20261114/process-index.json)의13노드·16조건부 연결은 기획 예시다. 지도 강조 대상은 모두 미결속이며 관련 근거/목록을 찾는 단계다.

## 관리·검증과 남은 자료

기존 연구 색인 형식의 출처·분야·파일·원본 증거·공백 테이블을 재사용하고, 조회용 절차/출처·조건부 의존·인허가/수입 관측 관계를 로컬 SQLite에 저장했다. 원형 자료와 참고 연결, 상태/단위/기간·출처 지문을 보존한다. 자료색인 DB는 운영/공용 DB나 게임 원장이 아니다.

독립 검사에서26출처·33색인파일의 지문/크기, 수입9행의 원문 행/금액·중량/추정 플래그 일치, 인허가1743행의 원래 필터/값 일치, SQLite 읽기 전용 재접속·무결성/외래키, 제안 그래프 비순환과 미결속 상태를 확인했다. NLIC 행 수는 원본 지문+이전 BIFF 감사 재사용 상한으로 명시했다.

미확보8항목은 실제 공급/설치망, 공간 좌표/건물 결속, 품목별 수입 조건, 모델별 가격/재고/납기, 시공BOM/견적, 인력/작업, 현재 원재료 납품조건, 브랜드/개업 조건이다. 원본·출처/권리와 **팩/지도 적용·실제 플레이·배포 준비는 별도**다. 기존 blocked/not_due·공통 latest·운영/공용 DB·수집 예약·제품 코드·Graph Map/Goal·commit/push는 변경하지 않았다. 새 관측의 정기 갱신은 현재 예약을 자동 확장하지 않고 별도 정책 검토 대상으로 남긴다.
