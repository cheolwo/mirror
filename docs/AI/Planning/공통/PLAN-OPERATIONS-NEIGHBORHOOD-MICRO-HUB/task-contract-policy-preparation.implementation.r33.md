# 생활 업무의 건별 계약 정책 준비 · 승인 구현 r33

상태: `Implemented / LocalValidated / ServerPolicyOnly / MinorParticipationDisabled`. 2026-10-08 사용자는 범위 확인에 **“서버에 꺼진 정책 옵션까지 준비”**라고 답했다. 새 건별 의뢰의 프리랜서 계약 준비 기본값과 향후 미성년 참여 정책 후보를 서버 설정에 마련했다. 전용 22개를 포함한 Fast 101개·Task 9,008개를 통과했다(환경 의존 MongoDB 시험은 각각 1개·2개 건너뜀). 실제 계약 성립·미성년자 배차·법정대리인 확인·앱 화면·APK 제작은 이번 승인이 아니다. [정산 r32](neighborhood-settlement.implementation.r32.md)의 원장과 지급 확인을 보존한다.

## 1. 계약 준비와 현재 운영을 구분한다

`NeighborhoodTaskContracts:DefaultContractModel`은 `PerTaskFreelance`다. 이는 새 **건별 의뢰 제안의 준비 기본값**이며 기존 HR 고용계약이나 현재 업무 계약을 프리랜서로 전환하지 않는다. 계약 명칭만으로 근로자성·세금·보험 적용을 확정하지 않는다. 현재 정책 조회는 `CanCreateContracts=false`, `IsLegalClassificationDetermined=false`를 반환한다.

기존 통신판매 신규 거래의 만 19세 성인 확인 경계를 그대로 둔다. 이는 현재 통신판매 운영 Guard의 기준이며 모든 기존 기사·조수 참여 경로에 통합된 나이 검증이 완료됐다는 뜻은 아니다. 이번 변경이 다른 업무의 자격 제한을 해제하거나 새 자격 승인을 만들지는 않는다.

## 2. 미성년 참여 옵션은 꺼진 준비 자료다

`MinorParticipation.Enabled`의 기본값은 `false`다. 이 코드 판본은 법적 근거와 미래 날짜를 모두 채워도 `true` 설정을 거부한다. 서버 시작의 옵션 검증과 정책 서비스의 직접 호출 검증을 함께 두며, 조회 응답도 항상 `IsMinorParticipationEnabled=false`, `MinorParticipationStatusCode=ReservedDisabled`다.

다음 값은 이후 검토할 **후보**만 보관한다. 빈 값·`null`·빈 배열은 미확정이며 허용 조건으로 자동 보충하지 않는다.

| 후보 설정 | 의미 |
| --- | --- |
| `PolicyRevision` | 검토할 정책 판본 참조 |
| `LegalBasisReference`, `LegalReviewReference` | 적용 법령 및 별도 검토 결과 참조 |
| `EffectiveFromUtc`, `EffectiveUntilUtc` | 검토 대상 시행·종료 시각(UTC) |
| `MinimumAgeYears` | 미확정 또는 미성년 나이 범위 후보. 현재 참가 가능 나이가 아님 |
| `PermittedWorkKinds` | 음식 배송·화물 배송·입고·피킹·배송 보조의 검토 후보 |
| `PermittedLocalTimeWindows` | `HH:mm-HH:mm` 시간대 후보. 현재 업무 시간이나 야간 허가가 아님 |

이 설정에는 주민등록번호·생년월일·계좌·대리인 동의서 원문을 넣지 않는다. 일반 인증 계정용 조회 응답에는 위 비공개 후보 판본·근거·날짜·시간을 전달하지 않는다. 설정은 운영 환경 구성으로 관리하며 DB 테이블·migration은 추가하지 않는다.

```json
{
  "NeighborhoodTaskContracts": {
    "DefaultContractModel": "PerTaskFreelance",
    "MinorParticipation": {
      "Enabled": false,
      "PolicyRevision": "",
      "LegalBasisReference": "",
      "LegalReviewReference": "",
      "EffectiveFromUtc": null,
      "EffectiveUntilUtc": null,
      "MinimumAgeYears": null,
      "PermittedWorkKinds": [],
      "PermittedLocalTimeWindows": []
    }
  }
}
```

설정 생략 시에도 위 준비 기본값과 비활성 상태를 사용한다. 법률 개정 자동 감시·판정·자동 활성화는 구현하지 않는다.

## 3. 이후 활성화 전에 필요한 별도 구현

1. 실제 시행 중인 법령, 대상 업무와 계약 실질, 검토 결과를 특정 정책 판본에 연결한다. 법안 발의·공포만으로 시행 또는 참여 허가를 판단하지 않는다.
2. 참여자 본인의 나이·신원, 법정대리인의 대리 권한을 검증하는 경로를 마련한다. 다른 성인이 계정을 등록하거나 계좌만 대신 받는 방식으로 본인 확인을 대체하지 않는다.
3. 정확한 업무·대가·기간·철회/만료 조건에 결속된 동의를 저장하고 다시 조회한다. 일반 동의 체크박스만으로 모든 미래 업무를 허가하지 않는다.
4. 음식·화물·입고·피킹·보조를 구별해 시간대·운송 수단·보험·세무·사고 책임을 검토한다. 동승자와 실제 수행자, 작업 사진 촬영자와 지급 대상도 구분한다.
5. 제안·수락·배차·현장 작업·보조 참여 경로가 같은 서버 정책을 따르도록 검증한다. 한 경로의 나이 확인만으로 다른 경로의 완료를 선언하지 않는다.
6. 검증 결과와 적용 판본을 확인한 후 별도 구현과 운영 전환을 승인한다. 현재 `Enabled=true` 차단은 그때 검토해 교체한다.

## 4. 법적 근거와 적용 한계

2026-10-08 확인한 [민법 제5조](https://www.law.go.kr/LSW/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1026990807)는 미성년자의 법률행위에 대한 법정대리인 동의와 취소 가능성을 규정한다. 동의가 있다는 사실만으로 모든 업무·운송·지급 조건이 충족되는 것은 아니다.

근로관계에 해당하면 [근로기준법 제64조](https://www.law.go.kr/lsLawLinkInfo.do?chrClsCd=010202&lsJoLnkSeq=900552042)의 최저 연령 조건과 [18세 미만 근로자의 야간·휴일 근로 제한 안내](https://www.easylaw.go.kr/CSP/OnhunqueansInfoRetrieve.laf?onhunqnaAstSeq=89&onhunqueSeq=5531&search_put=) 등을 따로 검토한다. 이는 모든 프리랜서 계약에 동일한 고용 규칙이 무조건 적용된다는 설명이 아니다. 계약 형식과 실제 관계의 판단은 [판례](https://www.law.go.kr/LSW/precInfoP.do?precSeq=207500)를 참고하며 구체적인 업무별 검토를 남긴다.

보험·세무 의무는 계약 명칭이나 당사자 직접 계좌이체만으로 결정하지 않는다. [플랫폼 종사자 고용보험 안내](https://easylaw.go.kr/CSP/CnpClsMain.laf?ccfNo=2&cciNo=1&cnpClsNo=1&csmSeq=1589)와 [국세청 소득 자료 안내](https://g.nts.go.kr/nts/cm/cntnts/cntntsView.do?cntntsId=239047&mi=41003)를 검토 근거로 남기며, 이번 옵션에 일괄 보험료·원천세율을 적용하지 않는다.

## 5. 수용 기준과 검증 반환

- 설정 없이도 건별 프리랜서 준비 기본값과 꺼진 미성년 정책을 조회한다.
- 미래 후보를 채워도 계약 생성·법적 분류·미성년 참여가 허용되지 않는다.
- 잘못된 시행 시각·기간·나이·업무·시간 후보와 활성화 설정을 거부한다.
- 인증 없는 조회와 계정 식별자가 없는 인증을 거부하고 후보 근거를 응답에 노출하지 않는다.
- 기존 성인 Guard·HR 계약·배차·정산·사진 원장과 다른 작업을 보존한다.

실제 코드와 검증 범위는 [페이지·서버·DB 책임](../../../../ProjectOverview/page-docs/neighborhood-task-contract-policy-r39.md), [변경·검증 반환](../../../../Changes/2026-10-08-neighborhood-task-contract-policy-r39.md)에 연결한다.
