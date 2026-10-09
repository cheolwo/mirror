# 분산 보관 합의·사진 완료: 페이지 → 코드 → DB

## 페이지 책임과 상태

| 역할·페이지 | 업무 질문 | 현재 상태별 행동 |
| --- | --- | --- |
| 공간 제공자·보관 합의 | 이 위치에 누구의 물건을 어떤 조건으로 보관할까? | 조건 작성 → 양측 동의 확인 → 변경 시 새 판본 동의 |
| 공간 관리·입고 확인 | 이번에 어떤 물건을 몇 개 적재했나? | 현재 위치·물품·수량 확인 → 사진 촬영 → 사진 보내고 입고 완료 |
| 화물 기사·현재 순회 | 지금 어디서 인수하거나 전달해야 하나? | 현재 정차의 물품/고객만 표시 → 촬영/다시 촬영 → 사진 보내고 피킹/전달 완료 |
| 공간 관리·반품 입고 | 실제 돌려받은 물품이 무엇인가? | 반환 대상·위치·수량 → 촬영 → 사진 보내고 반품 입고 완료 |
| 주문자·수령 확인 | 기사 전달 뒤 실제 받았나? | 기존 수령 확인·문제 접수 유지, 추가 사진 강요 없음 |

사진 전송 후 완료 응답이 끊기면 같은 요청 번호의 결과부터 조회한다. 전송이 시작된 사진의 업무·수량·판본을 다른 입력으로 바꾸지 않는다. 지도·공통 역할 홈 구성과 다음 업무 선택 원칙은 [r36](distributed-fulfillment-r36.md)을 유지한다.

## 코드

- 계약·사진·묶음 요청: `Ssalddel.Contracts/Common/Warehouse/DistributedStorageProofDtos.cs`, `DistributedPhotoEvidenceDtos.cs`.
- 현재 원장·합의·권한/판본 검사와 원자적 묶음 명령: `Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.*.cs`.
- 비공개 업로드·바이트 검증·업무 결속·당사자 재검사: `DistributedPhotoEvidenceService.cs`.
- 잔여 재고·운송 중 수량·문제 접수 보호·24시간/30일 정리: `DistributedPhotoEvidenceService.Retention.cs`, `DistributedPhotoHoldService.cs`, `DistributedPhotoRetentionWorker.cs`.
- 사진 HTTP: `POST api/v1/distributed-fulfillment/evidence`, 인증된 `GET evidence/{id}` 및 `GET evidence/{id}/content`.
- 실제 촬영 어댑터: `Ssalddel.Client.Infrastructure/Native/Maui업무사진촬영Adapter.cs`를 주문자·화주·화물 기사·창고 관리 앱에 연결. 공통 화면의 촬영 흐름은 UI 공통 기능이 담당한다.
- 문제 접수: `Ssalddel/Services/PrivacySupport/보호지원UseCase.cs`, `보호지원SourceResolver.cs`, `보호지원보존Adapter.cs`, `Mongo보호지원Store.cs`.

## DB와 저장소

| 원본 | 관계·저장 책임 |
| --- | --- |
| `distributed_storage_agreements` | 공간·위치·공간 소유자·물품 소유자, 조건 판본·동의·역사 |
| `distributed_lots` 추가 합의 ID/보호된 snapshot | 실제 입고 수량에 적용한 합의 판본 보존 |
| `distributed_photo_evidence` | 업로드/완료 요청, 사진 해시, 결속, 업로드/소비/보관·삭제 결과 |
| `distributed_photo_holds` | 사건 단위 원본·순번·보존 보호/해제 tombstone |
| 기존 입고상품·예약·주문·운송·명령 영수증 | 수량·배차·현재 단계의 원본 권위, 사진만으로 자동 완료하지 않음 |
| 기존 보호지원 Mongo | 입고 Lot 또는 배송 Order 사건, 암호화 payload와 정확한 출처 hash 조회 |
| 비공개 파일 저장소 | 재인코딩 사진 바이트, 전용 접두어 밖 삭제 금지 |

조건 본문/역사, Lot snapshot, 사진의 저장소 위치·당사자 결속과 보존 근거는 저장 경계에서 보호한다. 마이그레이션 `20261008170000_AddDistributedStorageProof`는 기존 원장을 보존하며 신규 세 테이블과 Lot 두 필드를 추가한다. 공용 DB 적용 여부는 [변경 기록](../../Changes/2026-10-08-distributed-storage-photo-proof-r37.md)에서 별도 확인한다.

기준 정책은 [승인 기획 r31](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/storage-agreement-photo-proof.implementation.r31.md) 하나에 둔다.
