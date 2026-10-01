# 식약처 수입식품 공공데이터 모듈

이 폴더는 수입식품정보마루와 공공데이터포털에서 제공하는 식품의약품안전처(MFDS) 데이터를 서버 내부의 공식 확인 자료로 변환한다. 화면에서 직접 판정하거나 수입 가능 여부를 보장하지 않으며, 조회 결과와 조회시각을 함께 보관하고 실제 수입 전에는 공식 자료를 다시 확인한다.

## 현재 구현 범위

| 데이터셋 | 구현 | 서버 진입점 | 공식 문서 |
|---|---|---|---|
| 수입식품 제품DB | typed `HttpClient` 서비스 | 내부 서비스 `I수입식품제품조회Service` | [공공데이터포털](https://www.data.go.kr/data/15073949/openapi.do) |
| 해외제조업소 정보 | typed `HttpClient`, MediatR query, 화주 API | `GET /api/v1/shipper/import-food/oversea-manufacturers` | [공공데이터포털](https://www.data.go.kr/data/15073967/openapi.do) |
| 제품별 한글표시사항 | typed `HttpClient`, MediatR query, 화주 API | `GET /api/v1/shipper/import-food/korean-labels` | [공공데이터포털](https://www.data.go.kr/data/15110214/openapi.do) |

화면 컴포넌트는 연결하지 않았다. 공개 API 계약과 활용 주의사항은 `PublicDataApiMetadataCatalog`의 `ImportedFood` 도메인에서도 조회할 수 있다.

## 설정

모든 서비스는 각 섹션의 `ServiceKey`를 우선 사용한다. 값이 비어 있으면 `PublicData:DataGoKrServiceKey`, 그다음 `PublicData:ServiceKey`를 공통 키로 사용한다. 키는 저장소에 커밋하지 않고 `appsettings.Local.json`, 사용자 비밀 저장소 또는 환경변수에 둔다.

```json
{
  "PublicData": {
    "DataGoKrServiceKey": "YOUR_DATA_GO_KR_SERVICE_KEY"
  },
  "해외제조업소조회": {
    "ServiceKey": ""
  },
  "수입식품제품조회": {
    "ServiceKey": ""
  },
  "수입식품한글표시사항조회": {
    "ServiceKey": ""
  }
}
```

각 서비스는 XML과 JSON 응답을 모두 지원한다. 기본 제한시간은 20초이며 `TimeoutSeconds`로 조정한다.

## 한글표시사항 계약

공식 요청 필터는 제품구분, 수입업체명, 한글/영문 제품명, 해외제조업소명, 품목, 수출국, 제조국, 한글표시문구, 원재료명과 유통기한·처리일자 범위다. 응답은 다음 필드를 제공한다.

- 제품구분, 수입업체명
- 한글/영문 제품명
- 유통기한과 처리일자
- 해외제조업소명, 품목, 수출국, 제조국
- 변환된 한글표시사항과 원재료명
- 유통기한 시작일자와 종료일자

이 API 응답에는 수입식품 관리번호와 해외제조업소 코드가 없다. 따라서 제품DB 및 해외제조업소 데이터와 연결할 때는 정규화한 제품명, 제조업소명, 제조국, 품목을 이용해 **연결 후보**만 만들고, 사용자 확인 또는 추가 공식 조회 전에는 확정 연결로 저장하지 않는다.

## 데이터 결합 원칙

1. 제품DB의 `IPRT_FOOD_MNG_NO`를 공식 제품 후보의 기준 식별자로 사용한다.
2. 해외제조업소는 `OCTR_MNFT_BSSH_CD`를 기준 식별자로 사용하고 인증·취소·중단 상태를 함께 기록한다.
3. 한글표시사항은 제품 표시와 원재료 검색 자료로 사용하되, 원료의 법적 사용 가능 여부 판정으로 사용하지 않는다.
4. 외부 응답을 저장할 때 `조회시각Utc`, 데이터셋키, 공식 문서 URL, 원문 해시를 함께 저장한다.
5. 식약처 품목코드는 관세청 HSK 코드가 아니다. HS 후보는 관세청 데이터와 별도 연결하고 자동 확정하지 않는다.

## 후속 모듈 후보

다음 데이터는 현재 구현하지 않았으며 필요 시 같은 어댑터 경계로 추가한다.

- [수입식품 원료정보](https://www.data.go.kr/data/15111777/openapi.do): 원료 사용 가능 여부, 사용 부위와 조건
- [수입식품 품목별 규격정보](https://www.data.go.kr/data/15111776/openapi.do): 시험 항목과 기준·규격
- [해외제조업소 중단정보](https://www.data.go.kr/data/15073972/openapi.do): 중단 사유·기간·해제 이력
- [검사 부적합 식품정보](https://www.data.go.kr/data/15056516/openapi.do): 제품·제조업소별 부적합 이력

이 자료들은 경고와 검토 근거로만 사용하고, 자동 수입 승인이나 법률적 확정 판정으로 사용하지 않는다.
