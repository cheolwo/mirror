# 30개 행정동 지형 높이 공식 규정 근거 심화 감사

[자료·준비도 감사 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r36]

- 상태: `OfficialStandardInterpretedMetresButExportMetadataBlocked`
- 전체 적용 상태: `PrecisionTerrainBlockedBySourceMetadataAndConsumerContract`
- 기준: [지형 consumer 준비도 감사 r33](thirty-admin-dong-terrain-consumer-readiness-audit.implementation.r33.md), [지형 공통 격자 r29](thirty-admin-dong-terrain-private-generation.implementation.r29.md)
- 자료: [서울 열린데이터광장 OA-22241](https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do), [수치지형도 지형지물 속성목록 별표 2](https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flNm=%5B%EB%B3%84%ED%91%9C+2%5D+%EC%88%98%EC%B9%98%EC%A7%80%ED%98%95%EB%8F%84+%EC%A7%80%ED%98%95%EC%A7%80%EB%AC%BC+%EC%86%8D%EC%84%B1%EB%AA%A9%EB%A1%9D%28%EA%B5%AC%EC%A1%B0%ED%99%94%29&flSeq=145877779), [지형도 도식적용규정](https://www.law.go.kr/LSW/admRulInfoP.do?admRulSeq=2100000214070&chrClsCd=010201), [공간정보의 구축 및 관리 등에 관한 법률 제6조](https://www.law.go.kr/LSW/lsInfoP.do?lsiSeq=245323), [같은 법 시행령 제7조](https://www.law.go.kr/LSW/lsInfoP.do?lsiSeq=251663)

## 결론

r33 이후 공식 1차 출처를 더 깊게 대조한 결과, 정확한 배포 ZIP의 `CONT/NUME`와 국가 수치지형도 규정 사이에는 강한 의미 사슬이 있다. 다만 ZIP 자체의 수직 CRS·단위·필드 변환 이력과 `HEIGHT` 정의는 없다. 따라서 `CONT/NUME`를 **공식 규정으로 해석한 metre 수치**로 다룰 근거는 생겼지만, 이를 `SourceMetadataDeclared`, 정확한 수직 CRS 또는 절대 고도 적용 승인으로 올리지는 않는다.

기존 r29는 `HEIGHT`를 읽고 `CONT/NUME`와 값이 같은지 검사한다. 다음 세대는 이 순서를 뒤집어 `F001.CONT`와 `F002.NUME`를 원본 의미 필드로 읽고 `HEIGHT`는 동등성 검사에만 써야 한다. 이 변경과 전용 Unity consumer·halo/seam 검증이 끝나기 전에는 기존 r29를 높이 Mesh에 적용하지 않는다.

## 정확한 배포물과 축척

고정 입력은 2025-03-20 `서울시 등고선.zip`, 45,852,601 bytes, SHA-256 `4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF`다. ZIP 내부의 실제 경로는 `등고선 5000/N3L_F001`과 `표고 5000/N3P_F002`이므로 이번 배포물에는 1:5,000 규정을 적용한다. 1:1,000 규정이 함께 존재한다는 이유로 더 정밀한 축척을 주장하지 않는다.

동봉된 `등고선,표고.txt`는 `CONT : 등고수치`, `NUME : 수치`를 정의하지만 `HEIGHT`는 정의하지 않는다. SHP XML은 `EPSG:5174`와 수평 단위 metre만 나타내며 `VERTCS`, compound CRS, 수직 datum, 높이 필드 단위가 없다. 2D Polyline·Point의 수평 CRS 단위를 속성 높이 단위로 전용하지 않는다.

## 공식 규정 사슬

국가법령정보센터의 수치지형도 지형지물 속성목록 별표 2 p.98은 `F001`을 등고선, 속성을 `등고수치 DOUBLE 000.0`으로, p.99는 `F002`를 표고점, 속성을 `수치 DOUBLE 000.0`으로 정의한다. 두 항목 모두 1:1,000과 1:5,000에 수록된다.

2023년 원자료에 적용되던 지형도 도식적용규정은 다음을 연결한다.

- 제3조: 위치 기준은 공간정보의 구축 및 관리 등에 관한 법률 제6조와 시행령 제7조를 따른다.
- 제35조제2항: 1:5,000 등고선은 인천만 평균해수면을 기준으로 표시한다.
- 제36조·제38조·제39조: 1:5,000 주곡선·간곡선·조곡선 간격을 각각 5m·2.5m·1.25m로 정한다.
- 제42조제2항: 표고점의 표고수치는 m 단위로 소수 첫째 자리까지 표시한다.

같은 시기의 법률 제6조는 높이를 평균해수면으로부터의 높이로 표시하고 대한민국 수준원점을 사용하도록 하며, 시행령 제7조는 대한민국 수준원점을 인천만 평균해수면으로부터 26.6871m 높이로 정한다.

## 근거 수준 분리

| 항목 | 판정 |
| --- | --- |
| `N3L_F001.CONT` 의미 | 공식 속성목록과 동봉 정의로 등고수치 확인 |
| `N3P_F002.NUME` 의미 | 공식 속성목록과 동봉 정의로 표고점 수치 확인 |
| `CONT/NUME` 물리 단위 | 1:5,000 도식 규정에 따른 `OfficialStandardInterpretedMetres` |
| 국가 높이 기준 | `IncheonBayMeanSeaLevelNationalOriginStandard` 확인 |
| ZIP 자체의 수직 메타데이터 | 없음, `SourceMetadataDeclared=false` |
| `HEIGHT` 의미·단위 | 미확인, 원본 높이 필드로 사용 불가 |
| 수직 CRS 식별자·실현판·epoch | 미확인 |
| 절대 고도 적용 | `absoluteElevationAuthorized=false` |

서울 DB의 XML lineage는 2025년 `CopyFeatures`까지 설명하지만 원래 국토지리정보원 성과에서 `SUPISDB`로 들어오는 필드 생성·변환 이력을 제시하지 않는다. 따라서 국가 표준과 같은 값 범위라는 사실만으로 정확한 export-level 선언을 만들지 않는다.

## 다음 구현 계약

1. 새 지형 세대는 contour를 `CONT`, point를 `NUME`에서 읽고 각 행의 `HEIGHT` 값 일치 여부만 감사 counter로 기록한다.
2. `heightEvidenceLevel=OfficialStandardInterpreted`, `heightUnitSourceDeclared=false`, `verticalCrsIdentifierVerified=false`, `absoluteElevationAuthorized=false`를 동시에 유지한다.
3. r33의 47개 원거리 halo 표본을 삭제하지 않고 core·halo 정책을 새 consumer 계약에서 다시 판정한다.
4. Unity consumer는 공통 수직 기준을 유지하고 tile별 최저값 차감을 금지하며, no-data·shared edge height bit·법선·Mesh 예산을 fail-closed로 검증한다.
5. 서울시 생산 부서의 공식 답변이나 원본 제품 메타데이터가 `CONT/NUME` 단위·기준면과 `HEIGHT` 복제 관계를 직접 확인하기 전에는 `SourceMetadataDeclared`와 절대 고도 권위를 올리지 않는다.

이번 감사에서는 generation·Unity 코드·Mesh·Collider·Scene·DB·Mongo·current pointer를 변경하지 않았다. 공개·Runtime·Traversal·Gameplay 적용도 없다. **새 디오라마 규칙 후보 없음**.
