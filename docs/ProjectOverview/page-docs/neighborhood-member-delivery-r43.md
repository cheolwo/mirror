# 일반 회원 생활 배송 페이지 책임 r43

[기준](../../Architecture/WholeRoadmapPagePrinciple.md) · [승인 구현](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/member-delivery.implementation.r39.md) · [실행 결과](../../Changes/2026-10-09-neighborhood-member-delivery-r43.md)

## 페이지 책임

2026-10-10 예외 지속 보완: 수행 지도의 앱 중지/재개 수명은 배송 단계와 분리한다. 재개 때 같은 업무의 지도를 다시 표시하며 실패 시 현재 카드·사진·업무 원장은 유지한다. 같은 표시 요청의 연속 실패는 자동 반복하지 않고 `지도 다시 표시`로 명시 재시도한다. [승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.implementation.r1.md).

| 항목 | 기록 |
| --- | --- |
| 주 사용자·페이지/소스 | 일반 회원, 공통 생활 배송 화면과 SsalddelApp 호스트. 전문 기사 앱은 기존 책임 유지 |
| 대상·진입 문맥 | 내 정보의 참여 설정, 생활 지도에서 받은 제안, 알림에서 선택한 offerId, 본인 수락 의뢰의 requestId |
| 한 문장 목적 | 회원이 배송 참여 의사를 설정하고 제안 한 건을 판단한 뒤 본인에게 확정된 배송의 현재 단계를 완료한다 |
| 완료 결과·상태별 주 행동 | 설정: 저장 후 재조회. 제안: 상세 확인 후 수락. 픽업 대기: 사진 촬영/전송 후 픽업 완료. 전달 중: 사진 촬영/전송 후 전달 완료. 완료/문제: 원장 재조회와 복구 안내 |
| 기본 정보 | 설정은 참여·기준 위치·반경·수단·여력. 제안은 공개 동네·물품·확정 배송 보수·시간과 남은 유효기간. 수락 후 해당 당사자에게 필요한 인계 주소·요청 사항·현재 단계 |
| 보조 정보 | 동일 건의 조건과 변경 근거·사진 전송 상태·문제 처리. 정확한 활동 기준점은 본인 설정에서만 사용 |
| 독립 업무·제외 정보 | 참여 설정/제안 판단/배송 수행을 독립 경로로 구성. 거래대금·지급 확인은 기존 원장으로 인계. 제품 화면에서 내부 ID·원시 좌표·Firebase 진단 제외 |
| 진입·실패·복귀 | 로그인은 독립 경로, 원 제안으로 돌아와 재조회. loading/empty/error/retry/disabled 표시. 계정이 바뀌면 이전 작업 snapshot 제거. 응답 손실은 stable 요청 키를 유지하고 조회부터 수행 |
| 코드·API·DB | Common Razor → ViewModel/Session → 회원 배송 Client → Common 인증 API → 기존 생활 의뢰/운송원장과 회원 설정·제안·작업·증빙/알림. 서버가 권한·판본·경합·상태 전이를 결정 |
| 책임 판정·검증 | 페이지별 같은 목적의 구성. 구현·시험·실제 예시 호스트 렌더·APK·단말·운영 증거는 r43 변경 기록에서 각각 확인 |

## 경로와 독립 결과

| 경로 | 질문 | 결과 |
| --- | --- | --- |
| `/community/exchange/deliveries/participation` | 근처 배송을 맡겠는가? | 본인 설정 저장·재조회 |
| `/community/exchange/deliveries/offers` | 현재 받은 제안이 있는가? | 선택한 제안 상세로 이동 |
| `/community/exchange/deliveries/offers/{offerId}` | 이 조건으로 수행할 수 있는가? | 서버 수락 결과 또는 만료/충돌/거절 결과 |
| `/community/exchange/deliveries/work` | 내가 맡은 배송은 무엇인가? | 본인 작업 선택 |
| `/community/exchange/deliveries/work/{requestId}` | 지금 어떤 단계를 처리해야 하는가? | 사진 전송과 상태별 완료 후 같은 원장 재조회 |

일반 앱의 `/roles/01` prefix는 기존 호스트 조립 규칙을 따른다. 지도·카드 접힘은 표시 상태이며 서버 업무 상태를 바꾸지 않는다. 알림 탭은 상세 조회이며 자동 수락이 아니다.

의뢰자는 기존 `/community/exchange/deliveries/{requestId}` 상세에서 같은 원 의뢰의 회원 배송 진행과 픽업 전 취소를 확인한다. 전문 기사 배차 방식·기사 정보 제공 카드를 이 화면의 회원 의뢰에 함께 표시하지 않는다. 서버도 기존 배차 방식 변경·협업 대기 취소 명령으로 회원 수행 절차를 우회하지 못하게 검사한다.

## 원장과 저장 책임

| 테이블 | 책임 |
| --- | --- |
| `neighborhood_member_participation` | 본인 참여 동의·활동 위치·반경·수단·여력과 업무 경합 보호 |
| `neighborhood_member_offer` | 회원별 제안·유효기간·수락 판정 |
| `neighborhood_member_delivery` | 원 의뢰에 연결된 수행자·단계·현재 판본 |
| `neighborhood_member_receipt` | 계정·명령 키에 결속한 재전송 결과 |
| `neighborhood_member_photo` | 비공개 사진 객체의 해시·검증·단계 연결·보존 상태 |

기존 SQL 의뢰·운송원장을 거래·운송의 기준으로 유지한다. 새 테이블은 회원 참여와 수행 절차의 책임을 맡으며 거래대금·사진 전송·전달·수령·실지급을 하나의 완료 상태로 합치지 않는다. migration과 고정 snapshot은 로컬 시험으로 대조했으며 운영 DB 적용은 별도다.
