# 미국·호주 수입식품 통관 규정 카탈로그

## 변경 맥락

미국과 호주 공동수입 여정에서 세관 신고만으로 식품의 통관·판매 가능 여부를 확정하지 않도록 공식 법령과 기관별 확인 절차를 구조화했다.

## 변경 내용

- 미국 CBP, FDA, USDA APHIS·FSIS 절차를 관할 분류, 세관 신고, 시설·공급자 검증, 사전신고, 품목별 관리, 검사·방출 단계로 분리했다.
- 호주 ABF, DAFF, FSANZ 절차를 import declaration, BICON 생물보안, IFIS, risk food 증명, Food Standards Code와 원산지 표시 단계로 분리했다.
- 품목 범위를 넣으면 일반 확인 항목과 수산물, 주스, 산성화·저산성 통조림, FSIS 품목, 동식물성 상품, 호주 risk food·소매 표시 항목을 조합하는 공통 카탈로그를 추가했다.
- 공식 근거 URL과 검토일을 requirement에서 조회할 수 있게 했다.
- 호주는 정보 조사 목적지로만 추가하고 실제 운영시장으로 활성화하지 않았다.
- 자동 신고, 통관 승인, importer·broker 자동 선택은 모두 비활성으로 고정했다.

## 화면 변경

화면 없음. 공통 Contract와 아키텍처 문서에서 간접 확인한다.

## 검증

- `Hongdal.Contracts` build
- `ImportedFoodComplianceCatalogTests`
- 공식 출처 URL과 requirement-reference 연결 검사
