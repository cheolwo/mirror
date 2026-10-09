# 생활 배송 엑셀 양식 생성 r29

빈 업무 양식과 안내 시트를 생성·재계산·검사·렌더한 후 `.xlsx`로 내보내는 개발 도구다. 개인정보·합성 배송·수식을 입력하지 않는다. 실제 입력 파서는 서버의 `생활배송엑셀초안Service`가 소유하며, 열·판본 변경 시 계약·파서·시험·양식을 함께 검증해야 한다.

`@oai/artifact-tool`을 제공하는 Node 환경에서 저장소 루트 기준으로 실행한다. 외부 패키지를 자동 설치하지 않는다.

```powershell
node eng/NeighborhoodDeliveryInputTemplate/build-template.mjs outputs/neighborhood-simple-input-r29
```

산출물은 `생활배송입력.r29.xlsx`, `input-left.png`, `input-right.png`, `guide.png`와 검사 자료다. 눈으로 렌더를 확인하고 빈 양식·수식 없음·전화 문자 서식·날짜 서식·고정 열을 시험한 후, 양식만 `Ssalddel/Resources/NeighborhoodDelivery/생활배송입력.r29.xlsx`에 복사한다. 서버는 이 파일을 EmbeddedResource로 제공한다. 출력 폴더와 `node_modules`는 임시 산출물이며 source로 stage하지 않는다.

승인 범위와 실행 결과는 [r29 변경 기록](../../docs/Changes/2026-10-07-neighborhood-simple-input-r29.md)을 참조한다. 실제 배송 원 파일을 도구·시험 fixture·문서 자산으로 보존하지 않는다.
