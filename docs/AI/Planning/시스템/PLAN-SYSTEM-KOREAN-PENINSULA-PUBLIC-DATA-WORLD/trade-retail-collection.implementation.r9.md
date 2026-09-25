# HS 무역 통계·소매 상품 관찰 실제 수집 r9

- 상위 기획: `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD`
- 구현일: `2026-09-21`
- 상태: `Implemented / LocalMySqlStoredAndIndependentlyVerified / IdempotentReplayVerified / PrivateReviewOnly / UnityProjectionDeferred`

## 실제로 수집한 범위

공식 UN Comtrade 공개 API에서 대한민국(`reporterCode=410`)의 2025년 연간 수입(`flowCode=M`)을 HS 2022 6자리 분류로 수집했다. 전체 131행에서 세계 합계와 품목별 수입금액 상위 5개 상대국을 정규화했다.

| HS6 | 분류 | 2025 수입금액 | 순중량 | 상위 상대국(금액 비중) |
| --- | --- | ---: | ---: | --- |
| `090121` | 볶은 커피, 카페인을 제거하지 않은 것 | USD 361,173,321 | 17,179,143.041kg | 미국 44.4%, 스위스 27.6%, 이탈리아 11.8%, 베트남 4.5%, 네덜란드 3.0% |
| `080390` | 플랜틴 이외의 바나나, 신선하거나 건조한 것 | USD 315,897,628 | 360,823,989.254kg | 필리핀 59.9%, 베트남 22.6%, 에콰도르 9.6%, 코스타리카 4.5%, 페루 2.8% |
| `030617` | 그 밖의 냉동 새우류 | USD 545,729,404 | 68,588,541.626kg | 베트남 36.3%, 중국 25.8%, 페루 10.8%, 말레이시아 6.7%, 아르헨티나 5.4% |

상대국은 원료의 생산국이나 개별 상품의 원산지가 아니라 대한민국 수입 신고 통계의 교역 상대국이다. 특히 볶은 커피의 미국·스위스 수치는 커피 재배국 의미가 아니다.

관세법령정보포털의 2026년 한국 분류표에서 `0901210000`, `0803900000`, 흰다리새우 후보 `0306179091`을 교차 확인했다. 다만 개별 쇼핑 상품의 성분·가공·품종·카페인 여부를 통관 서류로 확인하지 않았으므로 상품의 한국 10자리 세번은 확정하지 않았다.

## 상품 관찰 결속

- 네이버 가격비교 3건, 쿠팡 3건을 저장했다.
- 모두 검색 첫 화면의 광고였으므로 `ObservedManualPaidPlacement`로 기록했다.
- `hs6Candidate`는 카테고리 연결 후보일 뿐이며 `PendingHumanReview`를 유지한다.
- 가격은 KRW 관찰값이고 기준가·배송비를 별도 JSON 필드로 보존한다.
- 판매 페이지의 `필리핀산`, `베트남 로부스타` 같은 문구는 `MerchantPageClaim` 또는 `ProductTitleClaim`이며 공공 원산지 증거가 아니다.
- `affiliate=false`, `distributionApproved=false`를 유지한다.

## 구현

- [수집기](../../../../../eng/Ssalddel.PublicDataPortalImport/무역소매대표상품Acquisition.cs)는 로그인·키 없이 공식 공개 API 원본과 HS·상대국 참고 목록을 내려받고 URL·시각·SHA-256 영수증을 생성한다.
- [가져오기](../../../../../eng/Ssalddel.PublicDataPortalImport/무역소매대표상품Import.cs)는 범위·동결 합계·광고·승인 상태·URL 허용 도메인을 검증한다.
- [상품 관찰 원본](../../../../../eng/public-data/trade-retail/representative-products.reviewed.json)은 추적 parameter와 이미지를 포함하지 않는다.
- 기존 `public_data_ingestion_runs`, `public_data_raw_snapshots`, `public_data_normalized_records`를 재사용했으며 migration이나 새 테이블은 만들지 않았다.

## 저장·검증 결과

| 항목 | 결과 |
| --- | ---: |
| 공식 원본 응답 | 131행 |
| 무역 금액·중량 정규화 | 36행 |
| HS6 분류 | 3행 |
| 상품 관찰 | 6행 |
| MySQL 최초 삽입 | 45행 |
| 독립 재조회 | 45행 |
| 동일 입력 재실행 | 신규 0행, 기존 45행 |

저장 대상은 `hongdal-mysql-1 / hongdal_dev`다. 네 원본 SHA-256은 다음과 같다.

- 무역 원본: `6aeda22da8ba18f4c6337bcbb253a2a2984f7dcda93edc8f71f7976906d73b60`
- HS6 참고 선택: `5d85745302f2e4d5e90a614455140b7e667bb35e8eb55feeee8c07ef6c449b9e`
- 상대국 참고 선택: `e9b97e74804a11fe9ecb99e7d7416e3c4316be5a5a5add91e98ab92419465a1c`
- 상품 관찰: `f35325d5e7de7589d9d322adc4d9e951b2b890bfbb6378b7f6094500631d4f6e`

## 검증 상한과 남은 일

- 이번 결과는 로컬 비공개 검토용 MySQL 원장까지다.
- UN Comtrade 공개 preview 집계는 향후 정정될 수 있어 `MayBeRevised`를 유지한다.
- 상품 링크·가격·원산지는 현재 판매·재고·최저가의 정본이 아니다.
- 서버 읽기 API, 카테고리 승인 화면, Unity 지구본 카드와 흐름 표현은 구현하지 않았다.
- Unity 공개 전에는 상품별 HS 결속 승인, 링크 재검사, 외부 이동 고지와 배포 권리 검토가 필요하다.
