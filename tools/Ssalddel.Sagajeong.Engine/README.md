# 홍달 경영 시뮬레이션

[Windows 실행판](../../artifacts/local/hongdal-unity-operator-expansion-20261111/player-release/HongdalSimulation.exe)을 열고 **운영 시작**을 누른다. 지도 위에서 휠을 올리면 확대하고, 내리면 축소한다. 동네 상세는 70%까지 보이며, 그 화면에서 한 번 더 축소하면 구 단위로 넘어간다. 구 단위에서 더 축소하면 서울 전역을 본다. 구 단위에서 휠을 올리면 직전 동네 상세로 돌아온다. 상세 지도에서는 왼쪽 버튼을 누른 채 끌어 주변 도로를 살펴본다. 실행 파일 옆의 데이터와 `Simulation` 폴더를 함께 둔다.

기본 경영 화면의 하단에는 **기사 운영 → 가맹점 운영 → 생산자 연결 → 공장 운영** 영역이 있다. 각 영역을 누르면 오른쪽 설명이 바뀌고, **가맹점 둘러보기**는 기존 매장 상세를 연다. **배차 보기**에서 현재 주문과 가상 정산을 확인하며 **기존 화면**, **지도만 보기**도 이용할 수 있다. 현재 기사·대기·정산 건수는 실제 회차 상태다. 경 유지 시간과 시간당 순소득은 원자료 기록을 기다리는 자리이며 9건/6건은 가상 계획 예시다. 가맹점·생산·공장 경영 규칙은 후속이다. [실제 경영 화면](../../artifacts/local/hongdal-unity-operator-expansion-20261111/manual-release/00-overview.png) · [전후 비교와 검증](../../artifacts/local/hongdal-unity-operator-expansion-20261111/compare.html)

가상 시계 하나로 1일차부터 이어서 진행한다. 상단의 **일시정지 · 기본 · 2배 · 4배**를 운영 중 선택할 수 있다. 기본은 현실 1초당 게임 12초다. 매일 11:30에 새 주문이 열리고 18:00에 영업을 마감한다. 진행 중인 배송은 마감 뒤에도 이어지며, 비는 시간에는 배차 데스크에서 영업 마감 또는 다음 영업 시작 시각으로 이동할 수 있다.

3일차 18:00에는 시계를 멈추고 다음 운영 방침을 고른다. **전차**는 장안 주문을 추가하고, **절제**는 사가정 A매장 주문 한 건으로 제한하며, **여제**는 A매장을 먼저 열고 B매장 주문을 게임 시간 2분 뒤에 받는다. 확정하면 같은 시각과 마지막으로 사용한 배속으로 운영을 이어간다. [3일차 실제 화면](../../artifacts/local/hongdal-unity-clock-campaign-20261110/proof-release/04-day3-policy.png) · [검증 기록](../../artifacts/local/hongdal-unity-clock-campaign-20261110/result.json)

방침 카드에는 파멜라 콜먼 스미스가 그린 1909년 라이더–웨이트–스미스 덱 실물 스캔을 사용한다. 카드를 골라 **그림 크게 보기**를 열고, **카드 목록으로** 돌아오면 고른 카드와 시각을 유지한다. [세 카드 화면](../../artifacts/local/hongdal-unity-tarot-art-20261110/proof/04-day3-policy.png) · [확대 화면](../../artifacts/local/hongdal-unity-tarot-art-20261110/proof/04b-day3-policy-art.png) · [카드별 출처](../../artifacts/local/hongdal-unity-tarot-art-20261110/player/Tarot-NOTICE.md)

**그림 크게 보기**에는 웨이트 원전을 참고한 카드 해설, 홍달 운영에 던지는 질문, 게임 방침의 실제 효과가 따로 표시된다. 전차에는 기존 저녁학당의 ‘통합된 정진’ 요약을 **기존 학습 메모**로 표시한다. [전차 해설 화면](../../artifacts/local/hongdal-unity-tarot-notes-20261110/proof-final/04c-day3-chariot-notes.png) · [해설과 학습 메모 출처](../../artifacts/local/hongdal-unity-tarot-notes-20261110/player-final/Tarot-NOTICE.md)

사가정 상세 지도에서 **기사 B**는 보라색 오토바이로 보인다. **운영 시작 → 기사 B → 매장 A**로 배차하면 도로를 따라 매장까지 이동한다. 매장에서 픽업을 기다리는 동안 멈추고, 픽업 뒤 배송지로 출발한다. 축소한 상세 지도에서는 점으로 표시한다. 기존 스테이지는 두 주문의 수령과 가상 정산까지 진행한다. [실제 실행 영상](../../artifacts/local/hongdal-unity-motorcycle-20261110/기사-B-배달-실행.mp4) · [검증 기록](../../artifacts/local/hongdal-unity-motorcycle-20261110/result.json)

지도 아래 **배달권 설정**을 열고 이름을 입력한 뒤 법정동을 골라 저장한다. 목록의 법정동 일곱 곳을 구에 관계없이 묶을 수 있으며, 각 동은 한 배달권에 속한다. 저장한 구성을 열면 그 권역의 법정동 카드만 표시된다. 이전·다음으로 저장한 배달권을 골라 편집한다. 가상 배달권은 PC의 게임 설정에 저장하며, 배달권 구성을 열어 지도를 살펴볼 수 있다. [설정과 축척 확인 화면](../../artifacts/local/hongdal-unity-delivery-zones-20261110/compare.html)

면목동·상봉동·중화동·묵동 카드는 각각 수집한 주변 도로·건물·녹지 표본을 연다. 건물은 외곽과 짧은 그림자로 그리고, 진행 중인 배달 경로와 기사는 그 위에 표시한다. 상세 지도 상단의 **면목역 보기**를 누르면 면목역 조망으로 이동한다. [네 동네 자료 범위](../../artifacts/local/hongdal-unity-legal-city-20261110/coverage.md) · [사가정 배차 전후와 면목역 조망](../../artifacts/local/hongdal-unity-city-board-20261110/compare.html)

구 단위 지도에서 구 이름을 누르면 해당 구의 법정동 카드만 열린다. 중랑구는 면목동·상봉동·중화동·묵동을 볼 수 있다. 지도 상단의 **법정동 선택**을 누르면 구 네 곳의 카드 일곱 장이 열리며, 목록에 마우스를 두고 휠을 돌려 아래 카드를 볼 수 있다. 카드에는 법정동과 소속 구, 주변 장소나 도로명, 도로 미리보기가 있다. 카드를 누르면 오른쪽 지도가 해당 동네의 도로 표본으로 이동한다. 다른 동네를 보는 동안 사가정 주문은 왼쪽 목록에서 관리한다. 면목동을 다시 선택하면 매장과 기사 마커로 돌아온다. [전후 화면과 실제 조작](../../artifacts/local/hongdal-unity-jungnang-dongs-20261110/compare.html)

법정동 명칭과 코드는 행정안전부 자료를, 지도 중심과 이미지는 기존 동북권 도로 표본을 사용한다. 상봉동은 망우로38길 인근, 중화동은 동일로129길 인근, 묵동은 동일로·공릉로 인근을 보여준다. [행정표준코드](https://www.code.go.kr/stdcode/normalCodeL.do) · [도로 표본 출처](../Ssalddel.BatchMonitor.Windows/Samples/SeoulEast/NOTICE.md)

카드의 대표 장소는 각 구의 공개 자료에서 확인했다. [면목동 사가정역](https://council.jungnang.go.kr/kr/minutes/qnaview.do?cl_cd_sch=&cmt_sch=&flag=all&item_sch=&list_style=&mem_sch=&page=6&schwrd=&sess_sch=&th_sch=&uid=2645) · [장안동 장안사거리](https://www.ddm.go.kr/www/selectBbsNttList.do?bbsNo=329&key=3330) · [중곡동 중곡역](https://www.gwangjin.go.kr/portal/main/contents.do?menuNo=200570) · [공릉동 경춘선숲길](https://www.nowon.kr/dong/intrcn/intrcn4/intrcn4_05.jsp)

지도나 왼쪽 목록에서 기사 또는 음식점을 누르면 오른쪽에 그 대상의 상태, 연결된 주문과 최근 진행이 열린다. 음식점에서는 추천 또는 담당 기사를, 기사에서는 맡은 매장과 메뉴를 확인할 수 있다. 스테이지를 시작한 뒤 배차 가능한 기사와 대기 주문 매장을 차례로 누르면 기사에게 주문을 배정한다. 상세 패널을 닫으면 진행 중인 기사 선택도 해제된다. [음식점 상세](../../artifacts/local/hongdal-unity-focus-20261110/03-store-after-assignment.jpg) · [기사 상세와 결산](../../artifacts/local/hongdal-unity-focus-20261110/06-courier-settled.jpg)

음식 배달은 하나의 업무 묶음이다. 기사·음식점·주문은 각자의 ID를 유지하고, 주문이 담당 기사와 픽업 음식점을 연결한다. 상세 화면은 선택한 대상에 연결된 주문과 그 주문의 진행 기록을 현재 시뮬레이션 상태에서 가져온다.

서울 화면에는 Natural Earth의 개략 외곽선을, 구 화면에는 중랑·동대문·광진·노원 표본을 표시한다. 상세 도로는 같은 OpenStreetMap 표본을 로컬 타일 18개로 나눠 화면에 들어온 타일만 읽는다. 가상 시계와 주문은 지도 단계와 독립적으로 진행한다.

이번 판은 WPF 음식 배달 캠페인의 `food-campaign-01`을 실행한다. 두 기사, 두 매장, 가상 배차 마감 6분, 제시간에 배차하고 정산한 주문당 600원 보너스를 기존 코드로 계산한다. 배송지와 주문은 합성 표본이며, 도로는 [공개 도로 표본](../Ssalddel.BatchMonitor.Windows/Samples/SeoulEast/NOTICE.md)을 사용한다.

Unity 화면은 기사 선택과 매장 클릭을 보내고, 로컬 엔진이 반환한 기사 위치·경로·업무 상태를 그린다. 이 엔진은 WPF와 같은 소스를 컴파일하며, 실행판 옆에서 표준 입출력으로 연결된다. 지도 SDK와 서버 연결 없이 재생된다.

엔진을 다시 만들려면 저장소에서 다음 명령을 실행한다.

```powershell
dotnet publish tools/Ssalddel.Sagajeong.Engine/Ssalddel.Sagajeong.Engine.csproj -c Release -r win-x64 --self-contained true -o <Unity 실행판 폴더>/Simulation
```
