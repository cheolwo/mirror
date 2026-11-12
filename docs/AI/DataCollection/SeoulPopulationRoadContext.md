# 서울 유동인구·상권 원본 수집과 법정동·보행링크 연결

기준일: 2026-11-12. 사용자가 공공자료 확인·실제 취득·정기 수집을 요청했다. 기존 `city-activity` 수집과 활성 예약을 확장하며 다른 맥락의 예약·USB·DB·게임 구현은 변경하지 않는다. 수집 정책 정본은 [jobs.json](../../../eng/data-collection/jobs.json), 공통 절차는 [데이터 수집 진입](README.md)이다.

## 실제 취득한 공개 자료

| 공식 출처 | 실제 원본 행 수·바이트 | 원래 공간·시간 단위 |
| --- | --- | --- |
| [OA-15568 길단위인구-상권](https://data.seoul.go.kr/dataList/OA-15568/S/1/datasetView.do) | 36,281행 / 8,543,807바이트 | 상권별 추정 유동인구, 성별·연령·시간대·요일. 2021년 1분기~2026년 2분기. 최신 분기 1,648상권. |
| [OA-22178 길단위인구-행정동](https://data.seoul.go.kr/dataList/OA-22178/S/1/datasetView.do) | 9,350행 / 2,201,460바이트 | 행정동별 추정 유동인구. 같은 22분기, 최신 분기 425행정동. |
| [OA-15560 영역-상권](https://data.seoul.go.kr/dataList/OA-15560/S/1/datasetView.do) | 1,650행 / 175,854바이트 | 상권 코드·대표좌표·행정동·면적. CSV는 폴리곤 경계가 아니다. |
| [OA-22160 영역-행정동](https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do) | 425행 / 20,616바이트 | 행정동 코드·대표좌표·면적. 법정동 자료가 아니다. |

2026-11-12 첫 취득은 HTTP 200 원본 네 개, 합계 10,941,737바이트다. **취득일과 관측 기준인 2026년 2분기는 다르다.** 기간 열이 없는 영역 CSV의 기준시각은 `null`로 보존한다. 원본은 CP949이며 해석은 별도로 수행하고 저장 바이트를 바꾸지 않는다. 인구의 원래 분기 집계 정의를 보존하며 일평균·고유 방문자·도로 통행량으로 임의 변환하지 않는다.

[원본·출처별 latest 보관 위치](../../../artifacts/local/data-collection-runs/city-activity/) · [첫 실제 취득 영수증](../../../artifacts/local/data-collection-runs/city-activity/runs/20261112T060755Z-823365aba2e4.json) · [법정동·보행·상권 파생 색인 r2](../../../artifacts/local/seoul-activity-context-20261112-r2/index.json) · [마감·예약·검증 기록](../../../artifacts/local/seoul-activity-collection-20261112/completion.json)

공식 Sheet 화면의 무료·무키 CSV 다운로드 POST를 사용한다. `SAMPLE_VIEW`는 공급자가 공개 화면에 제공한 입력이며 계정 인증키가 아니다. 전체 공개 표를 취득하므로 고정된 과거 분기를 URL에 넣지 않고 새 분기 추가를 다음 수집에서 포함한다. 화면 안내·컬럼·기간이 바뀌면 검증 실패로 남긴다.

공식 페이지의 갱신일 표기는 2026-09-11이다. 주기 표시는 상권 인구 월간·행정동 인구 분기·상권 영역 비정기·행정동 영역 월간으로 다르다. [서비스 출처 안내](https://golmok.seoul.go.kr/source.do)에서 추정·배분 방식과 원천 기준시점을 확인하며 실제로 파일에 있는 기간을 최종 기준으로 쓴다. 공공누리 제1유형 출처표시 안내를 보존한다. 내부 원본 수집과 이용자용 공개 배포 검토는 별도다.

## 법정동·도로와 연결하는 방법

공개 CSV의 ‘길단위인구’라는 이름만으로 개별 도로의 관측값이라고 해석하지 않는다. 상권·행정동 집계와 법정동, 도로 링크는 서로 다른 식별자·공간 단위다.

1. 기존 법정동 코드 원장과 MOIS 행정동↔법정동 관할 자료를 보존한다. 이번 연결 후보는 기존 `KIKmix.20260301`의 서울 관할관계를 사용한다. 행정동은 8자리, 법정동은 10자리로 구별하며 다대다 관계를 유지한다. 과거 관할표를 현재 경계로 승격하지 않는다.
2. 기존 OA-21208 중랑 보행망에서 `LINK` 행의 링크 ID와 법정동 코드를 읽는다. 원본 21,448행은 NODE 9,267행과 LINK 12,181행이며 합계를 도로 수로 세지 않는다. 보행망 좌표계는 EPSG:4326, 수집 판본은 2026-09의 기존 동결 자료다.
3. `보행링크 → 법정동 → 관할 행정동 후보 → 해당 행정동 전체 인구`를 참조로 연결한다. 상권은 상권 코드로 인구와 영역 CSV를 연결한 뒤 행정동·법정동 후보를 참조한다. 법정동·도로 인구는 `NotAllocated`로 남기며 합계·비율 배분을 하지 않는다.
4. 각 원본 SHA-256·판본·단위·코드·미해결 관계를 출력한다. 기존 법정동 경계 SHP는 EPSG:5186, 오래된 서울 경계 SHP는 EPSG:5181이므로 좌표를 바로 겹치지 않는다. 이번 색인은 공간 교차 계산을 수행하지 않는다.

원본의 실제 보유와 기하 검증은 별개다. 법정동 경계가 있더라도 행정동 인구를 도로에 배분하려면 별도 검토한 추정 모델과 오차 표시가 필요하다. 이번에는 그런 추정이나 Unity 보행 적용을 하지 않는다. 서울 밖의 구리시 등은 이번 네 CSV의 적용 범위 밖이다.

## 수집·검증·연결 실행

```powershell
$python = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python -B -X utf8 eng/data-collection/collect_public_snapshots.py --manifest eng/data-collection/jobs.json --job city-activity
& $python -B -X utf8 -m unittest discover -s eng/data-collection -p 'test_*.py' -q
& $python -B -X utf8 eng/data-collection/build_activity_context.py --output-dir artifacts/local/seoul-activity-context-새판본
```

저장소 루트에서 실행한다. 파생 출력은 새 디렉터리를 사용하며 기존 원본·구판·공통 `latest.json`을 덮어쓰지 않는다. 연결 도구는 네트워크를 사용하지 않고 기존 원본 SHA·행수·코드·출처를 대조한다. 입력 경로가 없거나 해시가 다르면 실패한다. 새로 받은 원본이 없으면 같은 파생 색인을 반복 생성할 필요가 없다.

수집기는 기존 HTTPS allowlist·TLS 검증·OS 잠금·주기·원본 해시 보존을 재사용한다. CSV는 엄격한 인코딩, 필수 열, 분기 형식, 복합키 중복, 지정 숫자 열과 결측을 검사한다. 결측을 0으로 바꾸지 않으며 schema 변경은 수집 실패로 남기고 이전 정상 원본을 보존한다. ZIP 인계에서도 같은 CSV 검증과 정책 지문을 확인한다.

`city-activity`만 회차 상한을 7요청·20MiB로 두며 원본별 20MiB·100,000행 하드 상한과 요청당 30초를 유지한다. 실제 첫 네 원본은 약 10.4MiB다. 다른 작업의 6요청·12MiB 기본 상한은 바꾸지 않았다. 같은 정책에서 주기 미도달 시 HTTP를 하지 않는 것을 확인했다.

## 정기 예약과 실패 처리

기존 활성 Codex 예약 **‘데이터 수집 · 인구·이동·교통’ / `automation-7` / 매일 한국시각 09:20**을 사용한다. 새 예약을 만들지 않는다. 안내 메타데이터는 기존 최소 20시간, 새 CSV 네 개는 최소 648시간(27일) 경과 때 재취득한다. 매일 확인하되 원본은 약 월간으로 갱신하며, 원문 변경 없음·주기 미도달·새 판본을 구분한다. 공급자 파일 갱신이 늦으면 과거 관측을 최신 관측으로 바꾸지 않는다.

예약은 정해진 수집기와 정책만 실행하고 오류를 이유로 출처·정책·키를 임의 변경하지 않는다. HTTP 거절·접근 제한은 기록하며 재요청·우회하지 않는다. 새로운 원본이 저장되면 연결 도구로 새 판본 색인을 만들 수 있고 미해결 코드를 함께 보고한다. 로컬 파일 수집이므로 PC와 앱이 실행 가능한 상태여야 한다. [공식 예약 안내](https://learn.chatgpt.com/docs/automations?surface=app)

새 CSV 저장 성공과 전체 회차 성공은 별개다. 기존 250m 생활인구·이동·TOPIS 미준비 항목과 아래 도로 원천은 `blocked`이므로 전체 회차는 `blocked`/종료코드 2일 수 있다. `summary.datasetSaved`, `notDue`, `blocked`, `failed`와 출처별 영수증을 함께 읽는다.

## 자동 수집으로 확보하지 못한 자료

- **10m 도로구간별 추정 유동인구 B030:** [빅데이터 캠퍼스](https://bigdata.seoul.go.kr/cnts.do?r_id=P212)의 방문·내부 이용과 반출 심사가 필요하다. [반출 정책](https://bigdata.seoul.go.kr/common/file/directFileDown.do?ad=tkout&file_nm=%EB%B9%85%EB%8D%B0%EC%9D%B4%ED%84%B0_%EC%BA%A0%ED%8D%BC%EC%8A%A4_%EB%B0%98%EC%B6%9C_%EC%A0%95%EC%B1%85.pdf)을 확인했으며 로그인·신청·원본 취득은 수행하지 않았다. 공개 집계 CSV를 그 대신 도로 인구로 표시하지 않는다.
- **신규 250m 생활인구:** [공식 개편 공지](https://data.seoul.go.kr/together/notice/boardView.do?seq=721010a1522630fbf7a78d381a8326ee)에 따라 기존 생산은 2026-07-31 종료됐다. [새 행정동 내국인 자료 OA-23016](https://data.seoul.go.kr/dataList/OA-23016/S/1/datasetView.do)의 2026-10 ZIP은 약 35.9MB로 현재 원본 상한을 넘으므로 이번 대상에 넣지 않았다.

도로별 실제 수요 판정·법정동 인구 재배분·새 경계 자동 취득·운영 DB 반입·게임 적용은 미완료다. 이번 결과는 원본 네 개의 실제 수집과 제한이 표시된 지역·보행 참조 색인이다.
