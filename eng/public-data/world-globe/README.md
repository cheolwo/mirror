# 세계 지구본 국가 관찰 자료 생성기

`build-world-globe-catalog.mjs`는 Natural Earth 1:110m 국가 경계와 World Bank 국가 메타데이터·인구를 Unity가 읽는 동결 국가 관찰 catalog로 변환한다.

```powershell
node eng/public-data/world-globe/build-world-globe-catalog.mjs `
  --output C:\Users\user\ssalddel\Assets\Ssalddel\Resources\WorldGlobeCountryCatalog.json
```

입력과 출력의 SHA-256, 자료 판본, 좌표계, license와 귀속은 출력 `sources`에 기록된다. 같은 원본 byte는 같은 catalog byte를 만들어야 한다. Natural Earth 경계는 법적·외교적 경계 확정 자료가 아니라 저해상 관찰 표현 자료다.

네트워크에서 받은 원본을 동결한 뒤에는 다음 선택 인수로 같은 byte를 다시 사용한다.

```powershell
node eng/public-data/world-globe/build-world-globe-catalog.mjs `
  --natural-earth <ne_110m_admin_0_countries.geojson> `
  --world-bank-countries <world-bank-countries.json> `
  --world-bank-population <world-bank-population.json> `
  --output <WorldGlobeCountryCatalog.json>
```

생성기는 입력 원본을 저장소에 자동 복제하지 않는다. Unity에는 검증된 catalog만 두고, 원본 보관·DB 적재·정기 갱신은 별도 승인 범위로 관리한다.
