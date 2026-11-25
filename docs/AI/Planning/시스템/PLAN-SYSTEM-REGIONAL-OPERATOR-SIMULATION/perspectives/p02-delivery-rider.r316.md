# [기획 · 입장 — 배달 기사 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r316]

2026-11-20 13:37 KST · Claude 제작실 · `PerspectiveFirstPlanningConfirmed / AllRolesRegrouped / FrameAndIndexOnly / NotReadyToDispatch / ProductUnchanged`

[입장별 기획 목차](README.md) · 9칸의 뜻은 목차 2절. 이 갈래의 단계: **다음**.

## 이 갈래의 입장

| 입장 | 자리 | 하는 일(r302 제목) | 기존 기획 | 단계 |
| --- | --- | --- | --- | --- |
| **음식 배달기사** | 시작 후보 · 관찰 | 배정 확인 · 픽업 · 전달 | [r249 기사 휴대폰과 업무 상태](../role-centered-observation.design.r241.md) · [r287 길을 따르는 배달 선](../soft-road-following-delivery-lines.design.r212.md) · [r145 직접 타는 배달](../direct-riding-delivery-minigame.design.r145.md) · [r191 기사 · 오토바이 모델](../3d-rider-motorcycle-modeling-and-unity-preparation.design.r191.md) | 다음 |

자료 근거: 비공개 배달 · 픽업 관측의 집계와 공개 출입구 · 도로 자료가 있다. 가게↔기사 관찰은 Windows 가상 주문에서 검증됐다. 중랑구 첫 공간 시험의 전달지는 가상 설정이다(r300). 본인 배달 기록은 내부판에서만 쓴다. [r237 기록된 픽업 표본](../hongdal-place-recorded-pickup-profiles-first.proposal.r237.md)

## 음식 배달기사 — 9칸(틀)

기존 기획에 있는 것만 옮겼다. 비어 있는 칸은 이 입장을 열 때 하나씩 정한다.

| 칸 | 지금 아는 것(근거) |
| --- | --- |
| 1. 누구 · 카드 한 줄 | 음식 배달기사. 카드 한 줄은 정할 것 |
| 2. 자리 | r308 입장 예시에 있다. 독립 시작으로 열지, 플랫폼 · 매장 입장에서 관찰로만 볼지는 미정(r308) [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도) |
| 3. 하는 일 | 배달 제안 확인 → 수락 → 픽업 → 전달. 지사의 세트 할당과 기사의 개별 수락은 따로 기록한다 [r293 세트 · 플랫폼과 지사](../delivery-branch-growth-play-loop.design.r257.md#r293-보존--배달-세트와-플랫폼지사-역할) |
| 4. 보는 정보 · 못 보는 정보 | 보는 것: 내 빛, 픽업지 · 전달지, 현재 주문 경로, 픽업 준비 · 도착 예상 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도). 못 보는 것: 정할 것 |
| 5. 데이터 | **실제 요금 규칙**(픽업 700 · 전달 700 · 100m당 80 버림 · 시간대 할증 · 지역 할증)과 **정산 구조**(미션비, 고용보험 · 산재보험 · 소득세 · 지방소득세 공제) — [r320](p01-delivery-platform.r316.md#r320-내-배달-기록으로-맞춘-첫-값) · 본인 배달 기록 집계(내부판만) [r237 기록된 픽업 표본](../hongdal-place-recorded-pickup-profiles-first.proposal.r237.md) · 길 · 출입구(내부판 주소 자료) · 게임 주문 |
| 6. 화면 | 첫 업무 화면은 해당 주문의 인계와 경로 [r308 입장 선택과 역할별 지도](../role-perspective-research-and-relaxed-observation.design.r242.md#r308-보존--홍달-첫-화면의-입장-선택과-역할별-지도) · 기사 휴대폰 [r249 기사 휴대폰과 업무 상태](../role-centered-observation.design.r241.md). 지도 축척(구 지도에서 기사 한 사람을 어떻게 보일지)은 정할 것 |
| 7. 유니티 표현 | 길을 따르는 빛 궤적 [r287 길을 따르는 배달 선](../soft-road-following-delivery-lines.design.r212.md) · 직접 타는 배달 [r145 직접 타는 배달](../direct-riding-delivery-minigame.design.r145.md) · 기사 · 오토바이 모델 [r191 기사 · 오토바이 모델](../3d-rider-motorcycle-modeling-and-unity-preparation.design.r191.md) |
| 8. 같은 건의 상대 | 지사(배차 제안) · 매장(픽업 인계) · 주문자(전달) [r244 같은 배달의 세 입장](../same-delivery-three-perspectives.confirmed.r244.md) |
| 9. 정할 것 | ① 시작 입장으로 열지 관찰로만 볼지 ② 구 지도(출시판)에서 기사의 모습 ③ 못 보는 정보의 경계 |
