# [개발 인계 · 월드·데이터·관찰 · 세계 구체 지구본 국가 정보 첫 수직 슬라이스 · r4]

## 승인 범위

사용자 승인에 따라 `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD r4`의 첫 구현 후보를 실제 Unity 수직 슬라이스로 만든다.

```text
Natural Earth 1:110m 국가 경계 동결 사본
  + World Bank 국가 메타데이터·최근 비결측 인구 동결 사본
    → 재현 가능한 국가 관찰 catalog
      → SimulationWorldShell / WorldMapRoot / GlobeRoot
        → 구체 지구본 자전·직접 회전·확대
          → 국가 판독·선택·정보 패널
            → 대한민국 상세 관찰 전환
              → 지구본 복귀
```

이 슬라이스는 국가 정보를 읽고 공간적으로 탐색하는 기능이다. 국가 경계·통계·카메라 선택은 운영 상태나 Simulation 상태를 변경하지 않는다.

## 자료 기준선

- 국가 경계: Natural Earth `1:110m Admin 0 Countries`, 공식 저장소 commit `ca96624a56bd078437bca8184e78163e5039ad19`, WGS84 `EPSG:4326`, public domain.
- 국가 메타데이터: World Bank API v2 `/country`, API key 불필요.
- 인구: World Bank indicator `SP.POP.TOTL`, 국가별 최근 비결측 값. 값과 함께 관측 연도와 API `lastupdated`를 보존한다.
- Natural Earth 경계는 법적·외교적 경계 확정 자료가 아니라 저해상 관찰 표현 자료다. 분쟁·속령·ISO 결손을 추정으로 고치지 않는다.
- World Bank WDI는 CC BY 4.0 귀속을 유지하고, 집계 지역을 국가로 오인하지 않도록 Natural Earth 국가 코드에 결속된 항목만 사용한다.

## 허용 쓰기 경로

- `Hongdal/eng/public-data/world-globe/**`
- `Hongdal/docs/AI/Planning/시스템/PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/**`
- `Hongdal/docs/AI/PLANNING.md`
- `Hongdal/docs/AI/CURRENT_WORK.md`
- `ssalddel/Assets/Ssalddel/Runtime/WorldMap/**`
- `ssalddel/Assets/Ssalddel/Presentation/WorldMap/**`
- `ssalddel/Assets/Ssalddel/Presentation/World/사가정운영디오라마View.cs`
- `ssalddel/Assets/Ssalddel/Presentation/World/역방어준비View.cs`
- `ssalddel/Assets/Ssalddel/Editor/세계지구본Builder.cs*`
- `ssalddel/Assets/Ssalddel/Editor/SimulationWorldShellBuilder.cs`
- `ssalddel/Assets/Ssalddel/Resources/WorldGlobeCountryCatalog.json*`
- `ssalddel/Assets/Ssalddel/Tests/EditMode/세계지구본Tests.cs*`
- `ssalddel/Assets/Ssalddel/Scenes/SimulationWorldShell.unity`
- 로컬 시험·캡처 산출물은 두 저장소의 `artifacts/` 아래만 사용한다.

기존 사가정 건물·공간 자료와 배치는 변경하지 않는다. 다만 지구본 overlay와 기존 IMGUI가 동시에 그려지지 않도록 두 기존 View에 표현 가시성 gate만 추가한다. Hongdal 모바일 앱 변경은 이번 슬라이스에 포함하지 않는다.

## 상호작용 계약

1. 초기 화면은 구체 지구본이고 유휴 상태에서 서쪽에서 동쪽으로 천천히 자전한다.
2. 마우스 drag는 지구본을 직접 회전하며 자동 자전을 일시 중단한다. wheel은 허용 거리 안에서 확대·축소한다.
3. UI 위 입력은 지구본 회전·선택에 전달하지 않는다.
4. 국가 표면 클릭은 위경도로 환산하고 동결 국가 경계에서 하나의 국가를 판독한다.
5. 선택 패널은 국가명·ISO·대륙·지역·수도·인구·관측 연도·자료 가용성·출처·경계 고지를 표시한다.
6. 대한민국만 첫 상세 버튼을 활성화한다. 상세 전환은 기존 사가정 관찰 표현을 덮어쓰지 않고 지구본 전용 카메라와 UI를 숨긴다.
7. `지구본으로 돌아가기`는 같은 회전·확대·선택 상태를 복원한다.

## E1~E7 수직 검증 명세

| 단계 | Logic | Presentation | 통과 증거 |
| --- | --- | --- | --- |
| E1 | 읽기 전용 국가 선택 의미와 권위 불변 확정 | 지구본·국가 패널·상세 전환의 사용자 판독 순간 확정 | 본 문서와 r4 hash |
| E2 | 출처·판본·원본 hash·ISO 예외·결손을 가진 catalog 계약 | 국가명·출처·기준 시점·결손 표시 필드 | 생성기 결정성 시험과 catalog 검증 |
| E3 | 위경도↔구면 좌표, 날짜변경선 대응 point-in-polygon, stable ID 중복 거절 | 선택 강조 대상 계산 | 서울·도쿄·베이징·뉴욕·대양 표본 시험 |
| E4 | 국가 선택이 운영·Simulation 명령을 만들지 않음 | 구체·육지 표본 mesh·국경선·정보 패널·입력 충돌 방지 설계 | EditMode 조립 시험 |
| E5 | 동결 catalog를 같은 revision으로 읽음 | `SimulationWorldShell/WorldMapRoot/GlobeRoot` 실제 저장 Scene 결속 | 저장 Scene 구조·resource 시험 |
| E6 | 선택·상세·복귀 동안 권위 revision 불변 | 실제 Play Mode에서 자전·drag·wheel·국가 클릭·카메라 전환 | PlayMode·Console·상태 기록 |
| E7 | 해당 없음: 실제 업무 명령이 없는 관찰 WI | Game View에서 지구 전체·대한민국 선택 패널·사가정 전환·복귀 판독 | 실제 입력 절차와 대표 캡처 |

서버 live 국가 API와 운영 DB 독립 재조회는 이번 첫 표현 슬라이스의 완료 근거가 아니다. 따라서 서버 상태 사본 결속 전 Logic 상한은 E4이며, 실제 Game View가 성공해도 전체 통합 E는 E4를 넘지 않는다.

## 완료 조건

- 같은 세 원본 byte로 두 번 생성한 catalog의 SHA-256이 같다.
- catalog의 모든 국가 stable ID가 유일하고 좌표 범위·출처·경계 고지가 유효하다.
- 서울·도쿄·베이징·뉴욕이 올바른 국가로 판독되고 대표 대양은 국가로 판독되지 않는다.
- 공식 `SimulationWorldShell`에 `GlobeRoot`가 한 번만 존재하고 기존 사가정 표현과 기존 더티 변경을 덮어쓰지 않는다.
- Play Mode에서 자동 자전, 직접 회전, wheel 확대, 대한민국 선택 카드, 상세 전환, 지구본 복귀가 동작한다.
- Console 오류와 입력 이중 소비가 없고, 지구본 입력이 서버·Simulation revision을 바꾸지 않는다.

## 이번 범위에서 제외

- 전 세계 정밀 지형·위성영상·건물·도로·NPC 적재.
- 국가 경계를 법적 경계나 대한민국 공식 외교 입장으로 확정하는 것.
- 중국·일본·동남아시아·아메리카의 행정동·건물 상세 구현.
- 실시간 구름·기상·야간 조명.
- 국가 통계의 Unity 직접 인터넷 호출.
- 새 공식 Scene, 운영 명령, 결제·배차·정책 변경, Graph Map·E 자동 승격.

## 구현·검증 결과

- Natural Earth와 World Bank 동결 입력으로 177개 국가, 289개 ring, 10,654개 점의 catalog를 생성했다.
- 입력 SHA-256은 Natural Earth `6866C877D39CBA9C357620878839B336D569F8C662D3CFAB4CB1DBE2D39C977F`, World Bank 국가 `D29D57F8ADF954C5E2A1520A02FB2C7B45575D8DB3BD327A9DFF47D66914231C`, World Bank 인구 `4657C2140922547408F0652A5A4075EBEFD28F31B10BFEB24FC4A78362D56CFE`다.
- 같은 세 원본 byte로 두 번 만든 catalog의 SHA-256은 모두 `B18AB971C957620A8184E0A0B53FC0F3060158B0366E2E06ABB8A4963F767D9A`로 일치했다.
- canonical `SimulationWorldShell`에 `GlobeRoot`, 구체·육지·국경선·전용 카메라·국가 정보 UI를 결속했다.
- 실제 Game View에서 대한민국 선택 정보 패널, 기존 사가정 상세 전환과 지구본 복귀를 확인했다. 대표 캡처는 `artifacts/world-globe/world-globe-korea-ui-final.png`, `artifacts/world-globe/world-globe-korea-detail-final.png`이며 유휴 자전은 실행 중 회전값 변화로 확인했다.
- 집중 EditMode 시험은 `11/11` 통과했다. 새 지구본 코드 전용 runtime 예외는 확인되지 않았고, 서버가 꺼진 canonical Scene의 기존 서버 연결·재생 hash 오류는 별도 선행 결손으로 남았다.
- drag·wheel은 코드와 조립 시험까지 확인했으며 실제 포인터 입력을 주입한 수동 증거는 아직 없다.
- live 서버 국가 상태 사본 API와 운영 DB 저장·독립 재조회는 후속이다. 따라서 통합 증거 상한은 E4다.
