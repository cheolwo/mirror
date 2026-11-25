# 문명 연구 화면과 홍달 운영 기술 적용 조사

- 확인일: 2026-11-17 KST.
- 범위: 사용자가 요청한 일회성 공식 자료 조사. 공공자료 수집 예약은 재개하지 않았다.
- 연결 기획: [자율 기사 모집·운영 연구 r264](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/voluntary-rider-recruitment-and-operating-tech-tree.design.r264.md).
- 한계: 게임을 새로 실행하거나 연구 화면을 직접 조작하지 않았다. 아래 공식 설명과 확인한 문서 내용을 사용했고 새 시안 이미지는 만들지 않았다.

## 1. 공식 자료에서 확인한 구조

| 출처 | 확인한 내용 | 홍달에 가져올 설계 요소 |
| --- | --- | --- |
| [문명 V 공식 매뉴얼 안내](https://support.civilization.com/hc/en-us/articles/37707647846803-Civilization-V-Digital-Manual)의 [영문 매뉴얼](https://downloads.2kgames.com/civ5/site13/community/feature_manual/Civ_V_Manual_English_v1.0.pdf#page=85), PDF85쪽 | 기술들이 선행 관계로 연결된다. 상위 기술을 목표로 고르면 필요한 선행 연구를 따라갈 수 있고, 연구 선택에서 전체 기술 트리로 들어간다. | 현재 가능한 선택과 장기 목표를 함께 볼 수 있는 창, 선행 관계·목표 경로의 표시 |
| [문명 VI 공식 배포 매뉴얼](https://steamcdn-a.akamaihd.net/steam/apps/289070/manuals/CIV_VI_25TH_ONLINE_MANUAL_ENG.pdf), Technology/Research Chooser/Technology Tree 절 | 턴마다 생산된 과학을 선택 기술에 누적한다. 연구 선택에는 예상 턴과 해금 대상이 표시되고, 전체 트리에서 현재 연구와 뒤의 기술을 함께 읽는다. | 현재 연구·누적 진척·남은 예상 턴·완료 시 생기는 기능·다음 후보의 표시 |

V는 공식 사이트에서 연결된 PDF를 메모리에서 읽었고 전체 PDF는 보관하지 않았다. [출처/취득 지문](../../../../artifacts/local/research/civilization-operating-tech-tree-20261117-r264/civ-v-manual-source.json)을 남겼다. VI는 웹 검색의 공식 PDF 색인 본문을 확인했으며 전체 PDF 열기는 도구의 크기 한도로 완료하지 못했다. 따라서 VI 전체 화면을 직접 시각 검수했다고 주장하지 않는다. 두 문서의 상세 밸런스·모든 확장판/최신작의 수치를 홍달 기준으로 쓰지 않는다.

## 2. 홍달에 적용하는 제안

- 00오행 총괄판 아래에 검토용21연구 창을 두고 기존 업무 창의 다음 할 일과 연결한다.
- 트리는 동네 운영→구/배달권 운영→전국망 운영의 깊이와 분야별 갈래를 함께 보여 준다. 각 카드는 선행 조건·현재 상태·기대하는 편의·필요한 실제 준비를 표시한다.
- 같은 유효 운영 턴의 연구 진척은 한 번만 반영하는 안을 권한다. 턴의 시간/정산 계약과 연구 비용·필요량·동시 진행 수는 홍달에서 별도로 정한다.
- 완료 효과는 모집·판단·계획·인계 도구의 확장으로 설계한다. 채용/지원자·재고·계약·운송 완료는 별도의 실제 업무 결과다.
- 문명의 과학 자원·기술/사회제도 구분·UI 외형을 그대로 도입하지 않는다. 홍달에서 연구 자원을 새로 만들지, 기존 운영/학습/투자를 사용할지는 미정이다.

## 3. 기존 기획과 맞출 부분

[r183](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/multi-entry-management-research-tree.design.r183.md)은 다섯 출발과 선택적 연구 확장을 이미 다룬다. 정확한 노드·선행 관계/비용·진척 규칙은 제안이었다. 기본 비용·현재 주문·잔액·필수 정보는 연구로 잠그지 않는 방향을 재사용한다.

지사 모집 광고는 기사 지원을 유도하는 업무다. 음식점 검색 노출 광고·고객 주문 증가·트래픽 중개와 다른 목적이며, 모집 홍보를 전국 해금 뒤로 미루지 않는다. 스티커 홍보는 자발적 참여와 실제 비용/배포를 거치는 후보로 정리했다.

## 4. 현재 구현의 재사용과 공백

현행 WindowsGame에는 여러 법정동을 묶어 저장하는 배달권 편집기, 동별 기사 수 증가 명령, 비용을 내는 엔진 레벨 상승이 있다. 이들은 행정동 기반 기본 배달권·개인별 지원/합류·연구점 원장/선행 트리와 같은 계약이 아니다. 코드·지문·승인/미정을 [r264](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/voluntary-rider-recruitment-and-operating-tech-tree.design.r264.md)에 구분했다.
