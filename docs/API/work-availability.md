# 근무 가능 요청과 인력 계획 API

기본 경로는 `api/v1/hr/work-availability`다. 기존 출고 읽기와 창고 등록을 재사용하고, 별도 수신 동의·본인 의향·관리자 인력 계획만 저장한다. 고용·HR 역할·실제 작업 배정·출근·급여를 생성하지 않는다. 공용 DTO는 `Ssalddel.Contracts/Common/Hr/근무가능요청Dtos.cs`다.

| 경로 | 동작 | 접근 |
|---|---|---|
| `GET /preferences`, `PUT /preferences` | 본인 수신 설정 조회·판본을 이용한 저장 | 로그인 + HrParticipationWorkflow |
| `GET /inbox?page=0` | 본인이 받은 요청·본인 답변만 조회 | 동일 |
| `POST /invitations/{invitationId}/reply` | 본인 가능·불가·철회 | 동일 |
| `GET /warehouses/{warehouseId}/context` | 기존 공정·구간·묶음·등록 인력·연락 연결 상태 | 기존 창고 범위 HR 관리자 + WarehouseFulfillmentWorkflow |
| `GET /warehouses/{warehouseId}/requests?page=0` | 관리자 요청·답변 목록 | 동일 |
| `POST /warehouses/{warehouseId}/requests` | 현재 묶음·동의를 다시 검증해 요청 등록 | 동일 |
| `POST /warehouses/{warehouseId}/invitations/{invitationId}/confirm` | 가능 답변을 정원 안에서 계획 확정 | 동일 |

모든 경로는 인증과 HrParticipationWorkflow가 필요하다. 관리자 경로는 기존 창고 API의 `운영사용자전용` 정책도 적용한다. 창고 ID는 본문·실제 요청의 창고와 일치해야 하며 기존 HR 범위·접근 제한과 서버 소유/등록 관계를 함께 검사한다. 조회는 private/no-store다. 목록은 0부터 시작하는 50개 페이지와 `HasMore`를 반환한다.

문맥은 `stage=picking|packing`, UTC `periodStartUtc/periodEndUtc`(최대 24시간), `groupPage`(기존 공정과 같은 6개), 선택 `groupKey`를 받는다. 조회 구간은 접수 구간이며 실제 근무 회차가 아니다. 원래 묶음 키와 페이지·구간을 요청 결과에 보존한다.

등록은 고정 `ClientRequestId`, 실제 `GroupKey`, 미래 UTC `WorkStartUtc/WorkEndUtc`(최대 24시간), 미래 `ReplyByUtc`(업무 시작 이하), `NeededWorkers`(1~50), 같은 창고의 동의한 `RecipientUserIds`(1~50), 선택 `ProposedHourlyAmount`, `Channel`, `ConfirmedAvailabilityOnly=true`가 필요하다. 수량·담당을 클라이언트 값으로 확정하지 않고 기존 출고 UseCase의 명시 구성·미완료 수량을 재조회한다. 정원은 대상 수를 넘지 않는다.

동일 작성자·요청 ID·내용은 같은 결과로 확인하고 다른 내용은 409다. 금액은 저장 정밀도인 소수 둘째 자리까지 받고 조용히 반올림하지 않는다. 같은 묶음·시간의 기존 요청이 업무 종료 전이면 별도로 중복 생성하지 않는다. 답변은 대상 `Revision`을 사용한다. 본인 의향은 `Pending → Available/Unavailable → Withdrawn`, 관리자 계획은 가능한 답변만 `Confirmed`로 확인한다. 가능·불가는 답변 기한 전, 계획 확정은 이미 가능한 답변을 업무 시작 전까지 확인한다. 본인은 업무 시작 전에 확정 의향도 철회할 수 있다. 정원·현재 답변·겹치는 확정 시간과 동시 판본을 다시 검사한다.

초기 범위는 고정된 요청 대상의 의향 확인이다. 관리자 취소·대상 추가·마감 후 재모집·교대표·실제 근무 배정은 별도 후속 범위다. 마감 뒤 공석은 이미 가능한 답변 중에서만 업무 시작 전 확정할 수 있다.

연락은 기본 `InApp`(웹 수신함 등록, 읽음 미확인)이다. `Kakao`는 기존 실행기의 활성 설정·인력 요청용 승인 템플릿·HTTPS 응답 주소·본인의 별도 동의·인증된 기존 전화번호가 모두 필요하다. 전화번호를 요청/대상 테이블에 복제하지 않는다. 신규 대상에게만 한 번 전송하며 `ProviderAccepted`는 공급자 접수, `Uncertain`은 접수 불명확, `NotSent`는 전송 전 조건 불충족이다. 불명확 상태는 자동 재전송하지 않는다. `Sms`는 실행기 미연결로 거부한다. 구현 검증에서는 실제 공급자를 호출하지 않는다.

입력은 400, 미인증은 401, HR 범위 위반은 기존 403, 미노출 대상·비활성 기능은 404, 중복·원천/동의 변경·판본/정원/시간 충돌은 409다. 접속 실패를 정상 빈 결과로 취급하지 않는다. DB 마이그레이션과 외부 템플릿·연결을 운영 환경에 적용한 상태는 별도 검증 대상이다.
