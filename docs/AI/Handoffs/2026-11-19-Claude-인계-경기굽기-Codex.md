# 인계 — 행정동 정밀판 굽기를 Codex가 이어서 하기(경기 판 · 다른 시도)

2026-11-19 16:39 KST · Claude 제작실 → Codex. 내부 정밀판(배포 아님).

- **사용자(11-19):** 「그거 다 구운 다음에 그 구운 거 아무튼 코덱스가 이어서 구울 수 있도록 여기 인계 작업을 한번 준비를 한번 해줘 봐봐. 클로드에서는 좀 약간 아키텍처랑 종합 개발 계획 같은 거 세우고 싶어가지고 … 코덱스가 좀 이게 너가 한 대로 좀 구울 수 있도록」
- **나눔:**
  - Codex: 굽기를 돌리고 결과를 확인 · 저장 · 커밋한다.
  - Claude: 아키텍처 · 종합 개발 계획을 맡는다(시골 길 잇기 같은 굽기 도구 고침의 설계 포함).
- **방향(11-19 사용자 확인):** 동 디오라마 = **내부 정밀판**이다. 출시판 놀이 지도는 구(시군) 단위다. → [제안](2026-11-19-Claude-제안-출시판과내부정밀판.md)
- **인계 묶음:** `artifacts/local/hongdal-unity-claude-20261112/dong-bake-gyeonggi-20261118-r1/codex-handoff-r1/`
- **Claude 판 결과:** 같은 폴더의 `gg-cand-navi-r1/<무리>.tar` — 무리마다 packed · reports · inputs. 반환 문서는 굽기가 끝나면 따로 쓴다.

## 1. 지킬 것

- **주소 원문(주소기반산업지원서비스 자료)은 PC 밖으로 내보내지 않는다.**
  - 대상: `public-data/juso-*/raw/`, 내비게이션용DB 7z와 거기서 푼 txt, `tools-pc`가 만드는 캐시(npz).
  - 이유: 보안각서 「국외 반출 금지」.
  - 구운 것(동별 덧입힘 · 굽기 결과)은 써도 된다(사용자 11-17).
- 결과는 **내부 검토용 · 배포 아님**이다. 게임 Resources에 경기 판을 넣지 않는다(지금 Resources는 서울 26동).
- **`C:\Users\user\ssalddel`에서는 git을 쓰지 않는다.** 읽기만 한다(hongdal-buildings.json · Tools~/make_dong_thumbs.py).
- 사용자 세이브 폴더(`AppData/LocalLow/Ssalddel/…`) · Player.log는 건드리지 않는다.
- 사람 이름 · 전화번호 · 실제 상호는 문서에 쓰지 않는다. 역 이름 · 동 이름은 된다.

## 2. 폴더

```
dong-bake-gyeonggi-20261118-r1/
  sources/                         건물 입력 SQLite 조각(sqlite-parts, 80개) · 녹지 zip · OSM 작은 길 · 대장 표제부 경기 발췌
  navi-extras/                     내비 덧입힘 경기 전체(구운 것) · OSM 역 · tools(그때 쓴 원본)
  gg-cand-navi-r1/                 Claude 판 결과(무리별 tar 248개 · _parts는 조각 원본)
  codex-handoff-r1/
    bake-config-gyeonggi.json      실행 설정(경로는 이 파일 기준)
    tools/                         굽기 도구(run_bake.py 실행기 · make_dong_modules.py 등)
    tools-pc/                      주소 원문을 읽는 도구(PC 안에서만): navi_7z · navi_cache · navi_match · gemd_convert · 녹지 · 작은 길 · OSM 역
    data/                          무리 계획(plan-candidate.json, 248무리) · 후보 경계 · 무리별 덧입힘(extras-navi-r1/) · 층수 보충 · 빈 횡단보도 · 서울 차선폭
public-data/juso-gyeonggi-20261118-r1/raw/navi-db-gyunggi/   내비게이션용DB 경기분(원문, receipt.json)
```

## 3. 처음 한 번 준비

1. 파이썬 3.10 이상과 numpy · scipy · Pillow를 준비한다. 건물 입력을 새로 만들 때만 matplotlib도 필요하다.
2. 건물 입력 SQLite를 붙인다(1.67 GB, sha256 `527e55c405972394…`).
   `python tools/assemble_parts.py ../sources/sqlite-parts gyeonggi-building-inputs.sqlite ../sources/gyeonggi-building-inputs.sqlite 527e55c405972394`
3. 노드링크를 푼다. `public-data/its-nodelink-20260914-r1/NODELINKDATA.zip`의 `MOCT_LINK.shp` · `MOCT_LINK.dbf`를 `../sources/nodelink/`에 둔다.
4. `bake-config-gyeonggi.json`의 절대 경로 두 개(ssalddel의 hongdal-buildings.json, make_dong_thumbs.py)가 맞는지 본다.
5. 시험: `python tools/run_bake.py bake-config-gyeonggi.json --only 41310-3 --workers 1`
   - 1분 남짓 걸린다.
   - 결과 `../codex-out/gg-cand-navi-r2/41310-3/packed/`가 Claude 판 `gg-cand-navi-r1/41310-3.tar` 안의 packed와 sha가 같아야 한다.
   - `hongdal-dong-thumbs.json`만 다를 수 있다(미리보기 입력 파일의 sha). 클라우드에서 확인한 결과다.

## 4. 굽기 실행(run_bake.py)

- 사용: `python tools/run_bake.py <설정> [--workers N] [--only 무리,무리] [--dry]`
- 무리마다 하는 일:
  1. 건물 꺼내기(extract_dong_buildings)
  2. 굽기 입력(make_dong_inputs, 층수 보충 포함)
  3. 건물 id · 작은 길 자르기
  4. 굽기(make_dong_modules)
  5. 싸기(pack_dong_resources) · 미리보기
  6. `codex-out/<이름>/<무리>/{packed,reports,inputs}`로 복사
- **다시 돌려도 된다.** done이 있으면 건너뛴다. 두 번 실패하면 failed다. 메모리로 죽은 무리는 마지막에 혼자 다시 굽는다.
- 상태 기록: `codex-work/status-<이름>.log`. 줄 하나에 `checks n / m · main(길 주 덩어리) · doors(문 있는 건물) · official(공식 출입구)`가 찍힌다.
- **메모리:** 큰 시골 면 하나가 4.7 GB까지 썼다(파주). PC 메모리에 맞춰 `--workers`를 정한다.
  - 16 GB면 2, 32 GB면 3을 권한다.
  - 그래도 죽으면 설정 env의 `HONGDAL_CLUSTER_REGION_CELLS`를 2000000으로 낮춰 그 무리만 다시 굽는다(`--only`).
  - 그래도 죽으면 **무리를 동 하나씩으로 나눈다.** 연천 41800-4(4동)는 타일을 1,500,000으로 줄여도 죽었는데, 한 동씩 굽자 1~2분에 끝났다.
    - 무리가 넓으면 타일 수가 늘어 메모리를 쓴다.
    - 예: `data/plan-split.json`(41800-4 → a~d, 41670-8 → a~b).
    - 덧입힘은 원래 무리 파일을 새 이름으로 복사하면 된다(다른 동이 섞여 있어도 쓰지 않는다).
- **시간:** 클라우드 2코어에서 도시 무리 2~4분, 시골 무리 4~9분이었다. 09:45~18:31에 일꾼 둘로 굽고, 19:30까지 메모리로 죽은 무리를 다시 구웠다.
- **주의(run_gg.reference.sh):** 처음 판에는 bash 예약 변수 GROUPS 탓에 혼자 다시 굽기가 돌지 않았다. 참고본은 고쳤다. Codex는 run_bake.py를 쓴다.

## 5. 일감 A — 경기 판 마무리

1. **Claude 판에서 빠진 것:** 새솔동(4159151000, 화성시만세구) 하나다. 후보 경계 안에 AL_D010 건물이 0채라 make_dong_inputs가 멈춘다.
   - 공식 경계로 다시 볼 것.
   - 새 동네라 건물 자료가 아직 없을 수 있다.
   - 나머지 601동은 모두 구웠다(연천 · 여주 두 무리는 동 하나씩 나눠서).
2. **공식 경계로 다시 굽기**(사용자가 juso 로그인으로 「구역의도형_전체분_경기도.zip」을 받은 뒤):
   1. 경계 바꾸기:
      `python tools-pc/gemd_convert.py <구역의도형 zip> data/gyeonggi-cand-wgs84.geojson.gz <out wgs84.geojson.gz> <out 5186.json.gz> 41000`
   2. 무리 계획: 동별 건물 수 · 이름을 공식 코드로 다시 세어 `tools/make_plan.py`를 돌린다. 차례는 `data/sgg-plan-candidate.json`, 무리는 12000채 · 6동이다.
   3. 주소 덧입힘 다시 만들기(PC 안):
      - `tools-pc/navi_match.py out <공식 5186> <공식 코드 json> <out>` → 경기 전체 덧입힘
      - `tools/split_navi_extras.py` → 무리별 덧입힘
      - 캐시 `cache-41` · 표적 `targets-41.npz` · 맞춤 `match-41.npz`는 아래 6절 순서로 다시 만들 수 있다.
   4. 설정 바꾸기: rings · geo를 공식 경계로, profile을 `juso-gemd-202610`으로, name을 새 이름으로 바꾼다.
   5. 전자지도 · 도로명주소출입구 · 기타자료(경기도)까지 받았다면 `tools/make_juso_extras.py`로 무리별 덧입힘을 만든다. 서울 공식 판과 같은 방식이고, 지하철 역 출입구 · 도로구간 작은 길까지 들어간다. 이쪽이 내비 덧입힘보다 낫다.
3. **횡단보도:**
   - 사용자 다운로드 폴더에 `gyeonggi-crosswalks-std-20261118.csv`가 있다(공공데이터 표준, 18개 시군 16,988곳).
   - 홍달 폴더로 옮긴 뒤 설정의 crosswalks에 넣는다. 형식은 make_dong_modules의 표준 CSV 칸이다.
4. **시골 길 잇기:** 이건 도구 고침이라 Claude가 설계한다(아래 7절). Codex는 고친 도구가 오면 다시 굽는다.

## 6. 일감 B — 다른 시도(예: 인천 28)

1. **건물:**
   - AL_D010_<시도> zip(브이월드 GIS건물통합정보)을 쓴다.
   - `tools/build_building_inputs.py <zip> <후보 경계 geojson.gz(admdongkor 2026-10-01 전국 판에서 그 시도)> <시도 두 자리> <out.sqlite>`
2. **도구 두 곳에 시도 한 줄씩 더한다.**
   - `tools/make_dong_inputs.py`의 SIDO_INFO
   - `tools-pc/gemd_convert.py`의 SIDONM(공식 경계를 쓸 때)
3. **녹지 · 작은 길(OSM):**
   - `tools-pc/make_green_sources.py <south-korea-261115-free.shp.zip> <national-vectors.sqlite> <W,S,E,N> <out.zip>`
   - `tools-pc/make_osm_roadlines.py <osm zip> <W,S,E,N> <out.json.gz>`
4. **주소 덧입힘(PC 안):**
   1. 7z에서 꺼내기:
      - `tools-pc/navi_7z.py list <7z>`
      - `tools-pc/navi_7z.py extract <7z> match_build_<열쇠>.txt <raw 폴더>/…`
      - 열쇠 예: incheon · seoul · busan …. CRC를 대조한다.
      - 7z: `public-data/juso-20261117-r1/raw/202610_내비게이션용DB_전체분.7z`
   2. 캐시: `tools-pc/navi_cache.py <raw 폴더> cache-<시도> build <열쇠>`를 돌리고, 같은 명령을 jibun으로 한 번 더 돌린다.
   3. 표적: `tools/make_navi_targets.py <sqlite> targets-<시도>.npz`
   4. 맞추기:
      - 환경 변수: `NAVI_CACHE=cache-<시도> NAVI_TARGETS=… NAVI_MATCH=match-<시도>.npz NAVI_SIDO=<시도> NAVI_REGION=<이름>`
      - `tools-pc/navi_match.py match` → `navi_match.py out <경계 5186> <codes.json> <out>`
   5. 역: `tools-pc/osm_stations.py <osm zip> <W,S,E,N> <out>`
   6. 나누기: `tools/split_navi_extras.py`
      - 대장 보충 파일이 없으면 `{"meta":{},"rows":{}}`인 빈 gz를 준다.
      - 표제부 발췌는 `tools/register_supplement.py`(PC의 mart_djy_03.txt를 시군구코드로 거른 것).
5. **계획 · 설정 · 굽기:** `make_plan.py` → 설정 파일 복사 · 수정(scopePrefix, name 등) → `run_bake.py`

## 7. 알려진 한계(Claude 판 숫자 · 반환 문서에서 자세히)

- **도시 동은 좋다.**
  - 예: 구리 세 동 묶음. 문 있는 건물 92%, 길 주 덩어리 87%.
- **시골은 길이 끊긴다.**
  - 와부읍: 문 있는 건물 43%, 주 덩어리 40%.
  - 가평 한 면: 문 있는 건물 14%, 주 덩어리 12%.
  - 까닭: 문은 가장 큰 길 덩어리에만 건다(make_dong_modules build_routes의 in_main). 노드링크 · 작은 길이 동 경계와 무리 경계에서 잘린다.
  - 고침은 Claude가 설계한다. 동 경계 밖 길을 덧붙이고, 덩어리를 가까운 큰길에 잇는다.
- **주소는 있는데 건물 모양이 없는 곳:** 김포 · 화성 등 새 동네다. 내비 건물 165만 곳 중 33만 곳이 AL_D010에 없다.
- **report의 표기:** 모듈 attribution 「녹지 · 횡단보도 서울시」, 역 파일 note 「주소 자료 지하철역」은 서울 문구 그대로다. 실제 원천은 수치지형도 · OSM · 내비DB다. 도구 문구를 고칠 때 함께 고친다.

## 8. 끝나면

- 결과 폴더와 상태 기록의 끝 줄(done / left)을 확인한다.
- 반환 문서: `docs/AI/Handoffs/<날짜>-Codex-…`. 무리 수, 실패 무리, 대표 동 숫자(문 · 주 덩어리)를 적는다.
- 커밋은 Codex가 한다.
